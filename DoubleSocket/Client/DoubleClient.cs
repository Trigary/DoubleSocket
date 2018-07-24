using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.ByteBuffer;
using DoubleSocket.Utility.KeyCrypto;

namespace DoubleSocket.Client {
	public class DoubleClient {
		public const int TcpAuthenticationTimeout = 3000;
		public const int UdpAuthenticationPacketFrequency = 30;
		public const int UdpAuthenticationPacketSendCount = 3 * UdpAuthenticationPacketFrequency;

		public State CurrentState { get; private set; } = State.Disconnected;

		private readonly MutableByteBuffer _receiveBuffer = new MutableByteBuffer();
		private readonly ResettingByteBuffer _sendBuffer = new ResettingByteBuffer(ushort.MaxValue);
		private readonly IDoubleClientHandler _handler;
		private readonly FixedKeyCrypto _crypto;
		private readonly TcpClientSocket _tcp;
		private readonly UdpClientSocket _udp;
		private readonly IPAddress _ip;
		private readonly int _port;
		private byte[] _authenticationData;
		private byte _sequenceIdBound;
		private long _connectionStartTimestamp;
		private byte _sendSequenceId;
		private byte _receiveSequenceId;

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



		public void Start() {
			lock (this) {
				CurrentState = State.TcpAuthenticating;
				_tcp.Start(_ip, _port);
			}
		}

		public void Close() {
			lock (this) {
				CurrentState = State.Disconnected;
				_tcp.Close();
				_udp.Close();
				_crypto.Dispose();
			}
		}


		
		public void SendTcp(Action<ByteBuffer> payloadWriter) {
			lock (this) {
				byte[] encrypted;
				using (_sendBuffer) {
					_sendBuffer.Write(_sendSequenceId);
					if (++_sendSequenceId == _sequenceIdBound) {
						_sendSequenceId = 0;
					}
					payloadWriter(_sendBuffer);
					encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
				}
				using (_sendBuffer) {
					_sendBuffer.Write((ushort)encrypted.Length);
					_sendBuffer.Write(encrypted);
					_tcp.Send(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
				}
			}
		}

		public void SendUdp(Action<ByteBuffer> payloadWriter) {
			lock (this) {
				using (_sendBuffer) {
					UdpHelper.WritePrefix(_sendBuffer, _connectionStartTimestamp, payloadWriter);
					byte[] encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
					_udp.Send(encrypted, 0, encrypted.Length);
				}
			}
		}



		private void OnTcpConnected() {
			lock (this) {
				using (_sendBuffer) {
					_sendBuffer.WriteIndex = 2;
					_sendBuffer.Write(_authenticationData);
					_authenticationData = null;
					ushort size = (ushort)(_sendBuffer.WriteIndex - 2);
					_sendBuffer.Array[0] = (byte)size;
					_sendBuffer.Array[1] = (byte)(size >> 8);
					_tcp.Send(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);

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
				Close();
				_handler.OnConnectionFailure(error);
			}
		}

		private void OnTcpPacketAssembled(Socket ignored, byte[] buffer, int offset, int size) {
			lock (this) {
				_receiveBuffer.Array = _crypto.Decrypt(buffer, offset, size);
				_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
				_receiveBuffer.ReadIndex = 0;

				if (CurrentState == State.TcpAuthenticating) {
					if (_receiveBuffer.Array.Length == 1) {
						Close();
						_handler.OnTcpAuthenticationFailure(_receiveBuffer.ReadByte());
					} else {
						CurrentState = State.UdpAuthenticating;
						_sequenceIdBound = _receiveBuffer.ReadByte();
						byte[] udpAuthenticationKey = _receiveBuffer.ReadBytes(8);
						_connectionStartTimestamp = _receiveBuffer.ReadLong();

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
					if (_receiveBuffer.Array.Length == 1 && _receiveBuffer.ReadByte() == 0) {
						CurrentState = State.Authenticated;
						_handler.OnFullAuthentication();
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
				_receiveBuffer.Array = _crypto.Decrypt(buffer, 0, size);
				_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
				_receiveBuffer.ReadIndex = 0;

				if (UdpHelper.PrefixCheck(_receiveBuffer, out ushort packetTimestamp)) {
					_handler.OnUdpReceived(_receiveBuffer, packetTimestamp);
				}
			}
		}



		public enum State {
			Disconnected,
			TcpAuthenticating,
			UdpAuthenticating,
			Authenticated
		}
	}
}
