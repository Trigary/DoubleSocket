using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.BitBuffer;
using DoubleSocket.Utility.KeyCrypto;

namespace DoubleSocket.Client {
	/// <summary>
	/// A client which has a TCP and an UDP socket. It has multiple authentication phases to make the TCP and the UDP packets
	/// sychronized on the server side as well. It sends the packets in an encrypted form, makes sure that the received packets
	/// are reassembled and are valid. It locks on itself (the current instance) for thread safety reason,
	/// which can be used to achieve even greater thread safety; but to make using this library easier,
	/// all methods internally silenty fail if this current client is disconnected.
	/// </summary>
	public class DoubleClient {
		/// <summary>
		/// The timeout for the TCP authenticating state.
		/// </summary>
		public const int TcpAuthenticationTimeout = 3000;

		/// <summary>
		/// The count of packets which should be sent each second while in the UDP authenticating state.
		/// </summary>
		public const int UdpAuthenticationPacketFrequency = 30;

		/// <summary>
		/// After how many packets should the UDP authenticating state time out.
		/// </summary>
		public const int UdpAuthenticationPacketSendCount = 3 * UdpAuthenticationPacketFrequency;

		/// <summary>
		/// The current state of the client.
		/// </summary>
		public State CurrentState { get; private set; } = State.Disconnected;

		/// <summary>
		/// The timestamp of the connection's establishment or -1 if the connection is yet to be established.
		/// </summary>
		public long ConnectionStartTimestamp { get; private set; } = -1;

		private readonly MutableBitBuffer _receiveBuffer = new MutableBitBuffer();
		private readonly ResettingBitBuffer _sendBuffer = new ResettingBitBuffer(DoubleProtocol.SendBufferArraySize);
		private readonly IDoubleClientHandler _handler;
		private readonly FixedKeyCrypto _crypto;
		private readonly TcpClientSocket _tcp;
		private readonly UdpClientSocket _udp;
		private readonly IPAddress _ip;
		private readonly int _port;
		private byte[] _authenticationData;
		private byte _sequenceIdBound;
		private byte _sendSequenceId;
		private byte _receiveSequenceId;

		/// <summary>
		/// Create a new instance of the client with the specified options.
		/// </summary>
		/// <param name="handler">The handler of the events.</param>
		/// <param name="encryptionKey">The key which should be used to encrypt the packets.</param>
		/// <param name="authenticationData">The data based on which the server can authenticate this client.</param>
		/// <param name="ip">The ip of the server.</param>
		/// <param name="port">The port of the server.</param>
		public DoubleClient(IDoubleClientHandler handler, byte[] encryptionKey, byte[] authenticationData, IPAddress ip, int port) {
			lock (this) {
				_handler = handler;
				_crypto = new FixedKeyCrypto(encryptionKey);
				TcpHelper tcpHelper = new TcpHelper(OnTcpPacketAssembled);
				_tcp = new TcpClientSocket(OnTcpConnected, OnTcpConnectionFailed, ((buffer, size) =>
					tcpHelper.OnTcpReceived(null, buffer, size)), OnTcpLostConnection);
				_udp = new UdpClientSocket(OnUdpReceived);

				_authenticationData = authenticationData;
				_ip = ip;
				_port = port;
			}
		}



		/// <summary>
		/// Starts the client, making it connect to the server.
		/// </summary>
		public void Start() {
			lock (this) {
				CurrentState = State.TcpAuthenticating;
				_tcp.Start(_ip, _port);
			}
		}

		/// <summary>
		/// Closes the client, making it disconnect and dispose of its resources.
		/// </summary>
		public void Close() {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				CurrentState = State.Disconnected;
				_tcp.Close();
				_udp.Close();
				_crypto.Dispose();
			}
		}



		/// <summary>
		/// Sends the specified payload over TCP.
		/// </summary>
		/// <param name="payloadWriter">The action which writes the payload to a buffer.</param>
		public void SendTcp(Action<BitBuffer> payloadWriter) {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				byte[] encrypted;
				using (_sendBuffer) {
					_sendBuffer.Write(_sendSequenceId);
					if (++_sendSequenceId == _sequenceIdBound) {
						_sendSequenceId = 0;
					}
					payloadWriter(_sendBuffer);
					encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.Size);
				}
				using (_sendBuffer) {
					_sendBuffer.Write((ushort)encrypted.Length);
					_sendBuffer.Write(encrypted);
					_tcp.Send(_sendBuffer.Array, 0, _sendBuffer.Size);
				}
			}
		}

		/// <summary>
		/// Sends the specified payload over UDP.
		/// </summary>
		/// <param name="payloadWriter">The action which writes the payload to a buffer.</param>
		public void SendUdp(Action<BitBuffer> payloadWriter) {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				using (_sendBuffer) {
					UdpHelper.WritePrefix(_sendBuffer, ConnectionStartTimestamp, payloadWriter);
					byte[] encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.Size);
					_udp.Send(encrypted, 0, encrypted.Length);
				}
			}
		}



		private void OnTcpConnected() {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				using (_sendBuffer) {
					_sendBuffer.AdvanceWriter(16);
					_sendBuffer.Write(_authenticationData);
					_authenticationData = null;
					ushort size = (ushort)(_sendBuffer.Size - 2);
					_sendBuffer.Array[0] = (byte)size;
					_sendBuffer.Array[1] = (byte)(size >> 8);
					_tcp.Send(_sendBuffer.Array, 0, _sendBuffer.Size);

					Task.Delay(TcpAuthenticationTimeout).ContinueWith(task => {
						lock (this) {
							if (CurrentState == State.TcpAuthenticating) {
								OnAuthenticationTimeout();
							}
						}
					});
				}
			}
		}

		private void OnTcpConnectionFailed(SocketError error) {
			lock (this) {
				if (CurrentState != State.Disconnected) {
					Close();
					_handler.OnConnectionFailure(error);
				}
			}
		}

		private void OnTcpPacketAssembled(Socket ignored, byte[] buffer, int offset, int size) {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				_receiveBuffer.SetContents(_crypto.Decrypt(buffer, offset, size));
				if (CurrentState == State.TcpAuthenticating) {
					if (_receiveBuffer.Array.Length == 1) {
						Close();
						_handler.OnTcpAuthenticationFailure(_receiveBuffer.ReadByte());
					} else {
						CurrentState = State.UdpAuthenticating;
						_sequenceIdBound = _receiveBuffer.ReadByte();
						byte[] udpAuthenticationKey = _receiveBuffer.ReadBytes(8);
						ConnectionStartTimestamp = _receiveBuffer.ReadLong();

						_udp.Start(_ip, _port);
						Task.Run(() => {
							lock (this) {
								int counter = 0;
								while (counter++ < UdpAuthenticationPacketSendCount) {
									_udp.Send(udpAuthenticationKey, 0, udpAuthenticationKey.Length);
									Monitor.Wait(this, 1000 / UdpAuthenticationPacketFrequency);
									if (CurrentState != State.UdpAuthenticating) {
										return;
									}
								}
								OnAuthenticationTimeout();
							}
						});
					}
				} else if (CurrentState == State.UdpAuthenticating) {
					if (_receiveBuffer.ReadByte() == 0) {
						CurrentState = State.Authenticated;
						_handler.OnFullAuthentication(_receiveBuffer);
					}
				} else {
					if (_receiveBuffer.ReadByte() == _receiveSequenceId) {
						if (++_receiveSequenceId == _sequenceIdBound) {
							_receiveSequenceId = 0;
						}
						_handler.OnTcpReceived(_receiveBuffer);
					}
				}
			}
		}

		private void OnTcpLostConnection() {
			lock (this) {
				if (CurrentState != State.Disconnected) {
					State state = CurrentState;
					Close();
					_handler.OnConnectionLost(state);
				}
			}
		}

		private void OnAuthenticationTimeout() {
			State state = CurrentState;
			Close();
			_handler.OnAuthenticationTimeout(state);
		}

		private void OnUdpReceived(byte[] buffer, int size) {
			lock (this) {
				if (CurrentState == State.Disconnected) {
					return;
				}

				_receiveBuffer.SetContents(_crypto.Decrypt(buffer, 0, size));
				if (UdpHelper.PrefixCheck(_receiveBuffer, out ushort packetTimestamp)) {
					_handler.OnUdpReceived(_receiveBuffer, packetTimestamp);
				}
			}
		}



		/// <summary>
		/// The possible states of the client.
		/// </summary>
		public enum State {
			Disconnected,
			TcpAuthenticating,
			UdpAuthenticating,
			Authenticated
		}
	}
}
