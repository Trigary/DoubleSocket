using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly MutableByteBuffer _receiveBuffer = new MutableByteBuffer();
		private readonly ResettingByteBuffer _sendBuffer = new ResettingByteBuffer(ushort.MaxValue);
		private readonly AnyKeyCrypto _crypto = new AnyKeyCrypto();
		private readonly IDoubleServerHandler _handler;
		private readonly int _maxAuthenticatedCount;
		private readonly TcpHelper _tcpHelper;
		private readonly TcpServerSocket _tcp;
		private readonly UdpServerSocket _udp;
		private int _authenticatedCount; //TODO decrement whenever an authenticated loses connection

		public DoubleServer(IDoubleServerHandler handler, int maxAuthenticatedCount, int maxPendingConnections,
							int port, int socketBufferSize, int timeout, int receiveBufferArraySize) {
			_handler = handler;
			_maxAuthenticatedCount = maxAuthenticatedCount;
			_tcpHelper = new TcpHelper(receiveBufferArraySize, OnTcpPacketAssembled);
			_tcp = new TcpServerSocket(OnTcpConnected, _tcpHelper.OnTcpReceived, _tcpClients.Keys,
				maxPendingConnections, port, socketBufferSize, timeout, receiveBufferArraySize);
			_udp = new UdpServerSocket(OnUdpReceived, port, socketBufferSize, timeout, receiveBufferArraySize);
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

		public void SendTcp(IDoubleServerClient recipient, Action<ByteBuffer> packetWriter) {
			lock (this) {
				DoubleServerClient client = (DoubleServerClient)recipient;
				using (_sendBuffer) {
					_tcpHelper.WriteLength(_sendBuffer, packetWriter);
					byte[] encrypted = _crypto.Encrypt(client.EncryptionKey, _sendBuffer.Array, 0, _sendBuffer.WriteIndex);
					_tcp.Send(client.TcpSocket, encrypted, 0, encrypted.Length);
				}
			}
		}

		public void SendUdp(IDoubleServerClient recipient, Action<ByteBuffer> packetWriter) {
			lock (this) {
				DoubleServerClient client = (DoubleServerClient)recipient;
				using (_sendBuffer) {
					UdpHelper.WriteCrc(_sendBuffer, packetWriter);
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
				if (client.State == ClientState.TcpAuthenticating) {
					_receiveBuffer.Array = buffer;
					_receiveBuffer.WriteIndex = size + offset;
					_receiveBuffer.ReadIndex = offset;
					if (_handler.AuthenticateClient(client, _receiveBuffer, out byte[] encryptionKey, out byte errorCode)) {
						client.TcpAuthenticated(encryptionKey, out ulong udpAuthenticationKey);

						if (++_authenticatedCount == _maxAuthenticatedCount) {
							_tcp.StopAccepting();
							foreach (DoubleServerClient connected in _tcpClients.Values) {
								if (connected.State == ClientState.TcpAuthenticating) {
									Disconnect(connected);
								}
							}
						}

						SendTcp(client, buff => buff.Write(udpAuthenticationKey));
						Task.Delay(UdpAuthenticationTimeout).ContinueWith(task => {
							lock (this) {
								if (client.State == ClientState.UdpAuthenticating) {
									Disconnect(client);
								}
							}
						});
					} else {
						SendTcp(client, buff => buff.Write(errorCode));
						Disconnect(client);
					}
				} else if (client.State == ClientState.Connected) {
					_receiveBuffer.Array = _crypto.Decrypt(client.EncryptionKey, buffer, offset, size);
					_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
					_receiveBuffer.ReadIndex = 0;
					_handler.OnTcpReceived(client, _receiveBuffer);
				} else {
					Disconnect(client); //client sent data while it was UdpAuthenticating
				}
			}
		}

		private void OnUdpReceived(EndPoint sender, byte[] buffer, int size) {
			lock (this) {
				if (_udpClients.TryGetValue(sender, out DoubleServerClient client)) {
					_receiveBuffer.Array = _crypto.Decrypt(client.EncryptionKey, buffer, 0, size);
					_receiveBuffer.WriteIndex = _receiveBuffer.Array.Length;
					_receiveBuffer.ReadIndex = 0;

					if (UdpHelper.CrcCheck(_receiveBuffer)) {
						_handler.OnUdpReceived(client, _receiveBuffer);
					}
				} else if (size == 64) {
					ulong key = BitConverter.ToUInt64(buffer, 0);
					client = _tcpClients.Values.FirstOrDefault(c => c.IsUdpAuthenticatingWith(key));
					if (client != null) {
						client.InitializeUdp(sender);
						_udpClients.Add(sender, client);
						SendTcp(client, buff => buff.Write((byte)0));
					}
				}
			}
		}



		public enum ClientState {
			TcpAuthenticating,
			UdpAuthenticating,
			Connected,
			Disconnected
		}
	}
}
