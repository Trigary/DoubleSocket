﻿using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.ByteBuffer;
using DoubleSocket.Utility.KeyCrypto;

namespace DoubleSocket.Client {
	public class DoubleClient {
		public const int UdpCreatingPacketFrequency = 30;
		public const int UdpCreatingPacketSendCount = 5 * UdpCreatingPacketFrequency;

		public State CurrentState { get; private set; } = State.Disconnected;

		private readonly MutableByteBuffer _receiveBuffer = new MutableByteBuffer();
		private readonly ResettingByteBuffer _sendBuffer = new ResettingByteBuffer(ushort.MaxValue);
		private readonly IDoubleClientHandler _handler;
		private readonly FixedKeyCrypto _crypto;
		private readonly TcpHelper _tcpHelper;
		private readonly TcpClientSocket _tcp;
		private readonly UdpClientSocket _udp;
		private readonly byte[] _authenticationData;
		private readonly string _ip;
		private readonly int _port;
		private readonly int _receiveBufferArraySize;

		public DoubleClient(IDoubleClientHandler handler, byte[] encryptionKey, byte[] authenticationData,
							string ip, int port, int socketBufferSize, int timeout, int receiveBufferArraySize) {
			lock (this) {
				_handler = handler;
				_crypto = new FixedKeyCrypto(encryptionKey);
				_tcpHelper = new TcpHelper(receiveBufferArraySize, OnTcpPacketAssembled);
				_tcp = new TcpClientSocket(OnTcpConnected, OnTcpConnectionFailed, ((buffer, size) =>
					_tcpHelper.OnTcpReceived(null, buffer, size)), OnTcpLostConnection, socketBufferSize, timeout);
				_udp = new UdpClientSocket(OnUdpReceived, socketBufferSize, timeout);

				_authenticationData = authenticationData;
				_ip = ip;
				_port = port;
				_receiveBufferArraySize = receiveBufferArraySize;
			}
		}



		public void Start() {
			lock (this) {
				CurrentState = State.TcpAuthenticating;
				_tcp.Start(_ip, _port, _receiveBufferArraySize);
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

		public void SendTcp(Action<ByteBuffer> packetWriter) {
			lock (this) {
				using (_sendBuffer) {
					_tcpHelper.WriteLength(_sendBuffer, packetWriter);
					byte[] encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
					_tcp.Send(encrypted, 0, encrypted.Length);
				}
			}
		}

		public void SendUdp(Action<ByteBuffer> packetWriter) {
			lock (this) {
				using (_sendBuffer) {
					UdpHelper.WriteCrc(_sendBuffer, packetWriter);
					byte[] encrypted = _crypto.Encrypt(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
					_udp.Send(encrypted, 0, encrypted.Length);
				}
			}
		}



		private void OnTcpConnected() {
			lock (this) {
				using (_sendBuffer) {
					_tcpHelper.WriteLength(_sendBuffer, instance => instance.Write(_authenticationData));
					_tcp.Send(_sendBuffer.Array, 0, _sendBuffer.WriteIndex);
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
				if (CurrentState == State.TcpAuthenticating) {
					if (size == 1) {
						Close();
						_handler.OnAuthenticationFailure(buffer[offset]);
					} else {
						CurrentState = State.UdpAuthenticating;
						_udp.Start(_ip, _port, _receiveBufferArraySize);
						byte[] toSend = new byte[size];
						Buffer.BlockCopy(buffer, offset, toSend, 0, size);
						Task.Run(() => {
							lock (this) {
								int counter = 0;
								while (CurrentState == State.UdpAuthenticating && counter++ < UdpCreatingPacketSendCount) {
									_udp.Send(toSend, 0, toSend.Length);
									Thread.Sleep(1000 / UdpCreatingPacketFrequency);
								}
							}
						});
					}
				} else if (CurrentState == State.UdpAuthenticating) {
					CurrentState = State.Connected;
					_handler.OnSuccessfulConnect();
				} else {
					_receiveBuffer.Array = _crypto.Decrypt(buffer, offset, size);
					_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
					_receiveBuffer.ReadIndex = 0;
					_handler.OnTcpReceived(_receiveBuffer);
				}
			}
		}

		private void OnTcpLostConnection() {
			lock (this) {
				Close();
				_handler.OnConnectionLost();
			}
		}

		private void OnUdpReceived(byte[] buffer, int size) {
			lock (this) {
				_receiveBuffer.Array = _crypto.Decrypt(buffer, 0, size);
				_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
				_receiveBuffer.ReadIndex = 0;

				if (UdpHelper.CrcCheck(_receiveBuffer)) {
					_handler.OnUdpReceived(_receiveBuffer);
				}
			}
		}



		public enum State {
			Disconnected,
			TcpAuthenticating,
			UdpAuthenticating,
			Connected
		}
	}
}
