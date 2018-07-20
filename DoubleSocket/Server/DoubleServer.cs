using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.ByteBuffer;
using DoubleSocket.Utility.KeyCrypto;

namespace DoubleSocket.Server {
	public class DoubleServer {
		public const int TcpAuthenticationTimeout = 2000;
		public const int UdpAuthenticationTimeout = 2000;

		private readonly IDictionary<Socket, DoubleServerClient> _tcpClients = new Dictionary<Socket, DoubleServerClient>();
		private readonly IDictionary<EndPoint, DoubleServerClient> _udpClients = new Dictionary<EndPoint, DoubleServerClient>();
		private readonly IDictionary<ulong, DoubleServerClient> _udpAuthenticationKeys = new Dictionary<ulong, DoubleServerClient>();
		private readonly MutableByteBuffer _receiveBuffer = new MutableByteBuffer();
		private readonly ResettingByteBuffer _sendBuffer = new ResettingByteBuffer(ushort.MaxValue);
		private readonly AnyKeyCrypto _crypto = new AnyKeyCrypto();
		private readonly IDoubleServerHandler _handler;
		private readonly int _maxAuthenticatedCount;
		private readonly TcpServerSocket _tcp;
		private readonly UdpServerSocket _udp;
		private int _authenticatedCount;

		public DoubleServer(IDoubleServerHandler handler, int maxAuthenticatedCount, int maxPendingConnections,
							int port, int socketBufferSize, int timeout, int receiveBufferArraySize) {
			lock (this) {
				_handler = handler;
				_maxAuthenticatedCount = maxAuthenticatedCount;
				TcpHelper tcpHelper = new TcpHelper(receiveBufferArraySize, OnTcpPacketAssembled);
				_tcp = new TcpServerSocket(OnTcpConnected, tcpHelper.OnTcpReceived, OnTcpLostConnection, _tcpClients.Keys,
					maxPendingConnections, port, socketBufferSize, timeout, receiveBufferArraySize);
				_udp = new UdpServerSocket(OnUdpReceived, port, socketBufferSize, timeout, receiveBufferArraySize);
			}
		}



		public void Close() {
			lock (this) {
				_tcp.Close();
				_udp.Close();
				_crypto.Dispose();
			}
		}

		public void Disconnect(IDoubleServerClient client) {
			lock (this) {
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



		public void SendTcp(IDoubleServerClient recipient, Action<ByteBuffer> payloadWriter) {
			lock (this) {
				DoubleServerClient client = (DoubleServerClient)recipient;
				SendEncryptedLengthPrefixOnlyTcp(client.TcpSocket, client.EncryptionKey, buffer => {
					buffer.Write(client.NextSendSequenceId());
					payloadWriter(buffer);
				});
			}
		}

		private void SendEncryptedLengthPrefixOnlyTcp(Socket recipient, byte[] encryptionKey, Action<ByteBuffer> payloadWriter) {
			byte[] encrypted;
			using (_sendBuffer) {
				payloadWriter(_sendBuffer);
				encrypted = _crypto.Encrypt(encryptionKey, _sendBuffer.Array, 0, _sendBuffer.WriteIndex);
			}
			using (_sendBuffer) {
				_sendBuffer.Write((ushort)encrypted.Length);
				_sendBuffer.Write(encrypted);
				_tcp.Send(recipient, _sendBuffer.Array, 0, _sendBuffer.WriteIndex);
			}
		}

		public void SendUdp(IDoubleServerClient recipient, Action<ByteBuffer> payloadWriter) {
			lock (this) {
				DoubleServerClient client = (DoubleServerClient)recipient;
				using (_sendBuffer) {
					UdpHelper.WritePrefix(_sendBuffer, client.ConnectionStartTimestamp, payloadWriter);
					byte[] encrypted = _crypto.Encrypt(client.EncryptionKey, _sendBuffer.Array, 0, _sendBuffer.WriteIndex);
					_udp.Send(client.UdpEndPoint, encrypted, 0, encrypted.Length);
				}
			}
		}



		private void OnTcpConnected(Socket socket) {
			lock (this) {
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
				DoubleServerClient client = _tcpClients[sender];
				_receiveBuffer.Array = buffer;
				_receiveBuffer.WriteIndex = size + offset;
				_receiveBuffer.ReadIndex = offset;

				if (client.State == ClientState.TcpAuthenticating) {
					if (_handler.AuthenticateClient(client, _receiveBuffer, out byte[] encryptionKey, out byte errorCode)) {
						client.TcpAuthenticated(encryptionKey, _udpAuthenticationKeys.Keys, out ulong udpAuthenticationKey);
						_udpAuthenticationKeys[udpAuthenticationKey] = client;

						if (++_authenticatedCount == _maxAuthenticatedCount) {
							_tcp.StopAccepting();
							foreach (DoubleServerClient connected in _tcpClients.Values) {
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
					_receiveBuffer.Array = _crypto.Decrypt(client.EncryptionKey, _receiveBuffer.Array,
						_receiveBuffer.ReadIndex, _receiveBuffer.BytesLeft);
					_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
					_receiveBuffer.ReadIndex = 0;
					if (client.CheckReceiveSequenceId(_receiveBuffer.ReadByte())) {
						_handler.OnTcpReceived(client, _receiveBuffer);
					}
				} else {
					Disconnect(client); //client sent data while it was UdpAuthenticating
				}
			}
		}

		private void OnTcpLostConnection(Socket socket) {
			lock (this) {
				DoubleServerClient client = _tcpClients[socket];
				Disconnect(client);
				_handler.OnLostConnection(client);
			}
		}

		private void OnUdpReceived(EndPoint sender, byte[] buffer, int size) {
			lock (this) {
				if (_udpClients.TryGetValue(sender, out DoubleServerClient client)) {
					_receiveBuffer.Array = _crypto.Decrypt(client.EncryptionKey, buffer, 0, size);
					_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
					_receiveBuffer.ReadIndex = 0;

					if (UdpHelper.PrefixCheck(_receiveBuffer, out ushort packetTimestamp)) {
						_handler.OnUdpReceived(client, _receiveBuffer, packetTimestamp);
					}
				} else if (size == 8) {
					if (_udpAuthenticationKeys.TryGetValue(BitConverter.ToUInt64(buffer, 0), out client)) {
						client.UdpAuthenticated(sender);
						_udpClients.Add(sender, client);
						SendEncryptedLengthPrefixOnlyTcp(client.TcpSocket, client.EncryptionKey, buff => buff.Write((byte)0));
					}
				}
			}
		}



		public enum ClientState {
			TcpAuthenticating,
			UdpAuthenticating,
			Authenticated,
			Disconnected
		}
	}
}
