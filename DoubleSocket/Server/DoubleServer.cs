using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.BitBuffer;
using DoubleSocket.Utility.KeyCrypto;

namespace DoubleSocket.Server {
	/// <summary>
	/// A server which has a TCP and an UDP socket. It has multiple authentication phases to make the TCP and the UDP packets
	/// sychronized on this (server) side as well. It sends the packets in an encrypted form, makes sure that the received packets
	/// are reassembled and are valid. It locks on itself (the current instance) for thread safety reason,
	/// which can be used to achieve even greater thread safety; but to make using this library easier,
	/// all methods internally silenty fail if this current server is disconnected.
	/// </summary>
	public class DoubleServer {
		/// <summary>
		/// The timeout for the client's TCP authenticating state.
		/// </summary>
		public const int TcpAuthenticationTimeout = 3000;

		/// <summary>
		/// The timeout for the client's UDP authenticating state.
		/// </summary>
		public const int UdpAuthenticationTimeout = 3000;

		private readonly IDictionary<Socket, DoubleServerClient> _tcpClients = new Dictionary<Socket, DoubleServerClient>();
		private readonly IDictionary<EndPoint, DoubleServerClient> _udpClients = new Dictionary<EndPoint, DoubleServerClient>();
		private readonly IDictionary<ulong, DoubleServerClient> _udpAuthenticationKeys = new Dictionary<ulong, DoubleServerClient>();
		private readonly MutableBitBuffer _receiveBuffer = new MutableBitBuffer();
		private readonly ResettingBitBuffer _sendBuffer = new ResettingBitBuffer(DoubleProtocol.SendBufferArraySize);
		private readonly AnyKeyCrypto _crypto = new AnyKeyCrypto();
		private readonly IDoubleServerHandler _handler;
		private readonly int _maxAuthenticatedCount;
		private readonly TcpServerSocket _tcp;
		private readonly UdpServerSocket _udp;
		private int _authenticatedCount;
		private bool _closed;

		/// <summary>
		/// Create a new instance of the client with the specified options.
		/// </summary>
		/// <param name="handler">The handler of the events.</param>
		/// <param name="maxAuthenticatedCount">The max. count of connected authenticated clients.</param>
		/// <param name="maxPendingConnections">The max. count of socket level pending connections.</param>
		/// <param name="port">The port on which the server should listen.</param>
		public DoubleServer(IDoubleServerHandler handler, int maxAuthenticatedCount, int maxPendingConnections, int port) {
			lock (this) {
				_handler = handler;
				_maxAuthenticatedCount = maxAuthenticatedCount;
				TcpHelper tcpHelper = new TcpHelper(OnTcpPacketAssembled);
				_tcp = new TcpServerSocket(OnTcpConnected, tcpHelper.OnTcpReceived, OnTcpLostConnection, _tcpClients.Keys,
					maxPendingConnections, port);
				_udp = new UdpServerSocket(OnUdpReceived, port);
			}
		}



		/// <summary>
		/// Closes the server, making it kick all clients and dispose of its resources.
		/// </summary>
		public void Close() {
			lock (this) {
				if (_closed) {
					return;
				}

				_closed = true;
				_tcp.Close();
				_udp.Close();
				_crypto.Dispose();
			}
		}

		/// <summary>
		/// Kicks a specific client.
		/// </summary>
		/// <param name="client">The client in question.</param>
		public void Disconnect(IDoubleServerClient client) {
			lock (this) {
				if (_closed) {
					return;
				}

				DoubleServerClient impl = (DoubleServerClient)client;
				impl.Disconnected();
				_tcpClients.Remove(impl.TcpSocket);
				_tcp.Disconnect(impl.TcpSocket);
				if (impl.UdpEndPoint != null) {
					_udpClients.Remove(impl.UdpEndPoint);
				}

				if (impl.State != ClientState.TcpAuthenticating && _authenticatedCount-- == _maxAuthenticatedCount) {
					_tcp.StartAccepting();
				}
			}
		}



		/// <summary>
		/// Sends the specified payload over TCP to the specified client.
		/// </summary>
		/// <param name="recipient">The client in question.</param>
		/// <param name="payloadWriter">The action which writes the payload to a buffer.</param>
		public void SendTcp(IDoubleServerClient recipient, Action<BitBuffer> payloadWriter) {
			lock (this) {
				if (_closed) {
					return;
				}

				DoubleServerClient client = (DoubleServerClient)recipient;
				SendEncryptedLengthPrefixOnlyTcp(client.TcpSocket, client.EncryptionKey, buffer => {
					buffer.Write(client.NextSendSequenceId());
					payloadWriter(buffer);
				});
			}
		}

		private void SendEncryptedLengthPrefixOnlyTcp(Socket recipient, byte[] encryptionKey, Action<BitBuffer> payloadWriter) {
			byte[] encrypted;
			using (_sendBuffer) {
				payloadWriter(_sendBuffer);
				encrypted = _crypto.Encrypt(encryptionKey, _sendBuffer.Array, 0, _sendBuffer.Size);
			}
			using (_sendBuffer) {
				_sendBuffer.Write((ushort)encrypted.Length);
				_sendBuffer.Write(encrypted);
				_tcp.Send(recipient, _sendBuffer.Array, 0, _sendBuffer.Size);
			}
		}

		/// <summary>
		/// Sends the specified payload over UDP to the specified client.
		/// </summary>
		/// <param name="recipient">The client in question.</param>
		/// <param name="payloadWriter">The action which writes the payload to a buffer.</param>
		public void SendUdp(IDoubleServerClient recipient, Action<BitBuffer> payloadWriter) {
			lock (this) {
				if (_closed) {
					return;
				}

				DoubleServerClient client = (DoubleServerClient)recipient;
				using (_sendBuffer) {
					UdpHelper.WritePrefix(_sendBuffer, client.ConnectionStartTimestamp, payloadWriter);
					byte[] encrypted = _crypto.Encrypt(client.EncryptionKey, _sendBuffer.Array, 0, _sendBuffer.Size);
					_udp.Send(client.UdpEndPoint, encrypted, 0, encrypted.Length);
				}
			}
		}



		private void OnTcpConnected(Socket socket) {
			lock (this) {
				if (_closed) {
					return;
				}

				DoubleServerClient client = new DoubleServerClient(socket);
				_tcpClients.Add(socket, client);
				Task.Delay(TcpAuthenticationTimeout).ContinueWith(task => {
					lock (this) {
						if (client.State == ClientState.TcpAuthenticating) {
							Disconnect(client);
						}
					}
				});
			}
		}

		private void OnTcpPacketAssembled(Socket sender, byte[] buffer, int offset, int size) {
			lock (this) {
				if (_closed) {
					return;
				}

				DoubleServerClient client = _tcpClients[sender];
				_receiveBuffer.Reinitialize(buffer, offset, size);

				if (client.State == ClientState.TcpAuthenticating) {
					if (_handler.TcpAuthenticateClient(client, _receiveBuffer, out byte[] encryptionKey, out byte errorCode)) {
						client.TcpAuthenticated(encryptionKey, _udpAuthenticationKeys.Keys, out ulong udpAuthenticationKey);
						_udpAuthenticationKeys[udpAuthenticationKey] = client;

						if (++_authenticatedCount == _maxAuthenticatedCount) {
							_tcp.StopAccepting();
							foreach (DoubleServerClient connected in _tcpClients.Values.ToList()) {
								if (connected.State == ClientState.TcpAuthenticating) {
									Disconnect(connected);
								}
							}
						}

						SendEncryptedLengthPrefixOnlyTcp(sender, encryptionKey, buff => {
							buff.Write(client.SequenceIdBound);
							buff.Write(udpAuthenticationKey);
							buff.Write(client.ConnectionStartTimestamp);
						});
						Task.Delay(UdpAuthenticationTimeout).ContinueWith(task => {
							lock (this) {
								if (client.State == ClientState.UdpAuthenticating) {
									Disconnect(client);
								}
							}
						});
					} else {
						SendEncryptedLengthPrefixOnlyTcp(sender, encryptionKey, buff => buff.Write(errorCode));
						Disconnect(client);
					}
				} else if (client.State == ClientState.Authenticated) {
					_receiveBuffer.Reinitialize(_crypto.Decrypt(client.EncryptionKey, _receiveBuffer.Array,
						_receiveBuffer.Offset, _receiveBuffer.Size));
					if (client.CheckReceiveSequenceId(_receiveBuffer.ReadByte())) {
						_handler.OnTcpReceived(client, _receiveBuffer);
					}
				}
			}
		}

		private void OnTcpLostConnection(Socket socket) {
			lock (this) {
				if (_closed) {
					return;
				}

				if (_tcpClients.TryGetValue(socket, out DoubleServerClient client)) {
					ClientState state = client.State;
					Disconnect(client);
					_handler.OnLostConnection(client, state);
				}
			}
		}

		private void OnUdpReceived(EndPoint sender, byte[] buffer, int size) {
			lock (this) {
				if (_closed) {
					return;
				}

				if (_udpClients.TryGetValue(sender, out DoubleServerClient client)) {
					try {
						_receiveBuffer.Reinitialize(_crypto.Decrypt(client.EncryptionKey, buffer, 0, size));
						if (UdpHelper.PrefixCheck(_receiveBuffer, out uint packetTimestamp)) {
							_handler.OnUdpReceived(client, _receiveBuffer, packetTimestamp);
						}
					} catch (CryptographicException) {
					}
				} else if (size == 8) {
					if (_udpAuthenticationKeys.TryGetValue(BitConverter.ToUInt64(buffer, 0), out client)) {
						client.UdpAuthenticated(sender);
						_udpClients.Add(sender, client);
						Action<BitBuffer> payloadWriter = _handler.OnFullAuthentication(client);
						SendEncryptedLengthPrefixOnlyTcp(client.TcpSocket, client.EncryptionKey, buff => {
							buff.Write((byte)0);
							payloadWriter?.Invoke(buff);
						});
					}
				}
			}
		}



		/// <summary>
		/// The possible states of a client.
		/// </summary>
		public enum ClientState {
			TcpAuthenticating,
			UdpAuthenticating,
			Authenticated,
			Disconnected
		}
	}
}
