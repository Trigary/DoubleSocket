using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Server {
	/// <summary>
	/// An UDP server which is able to accept new connections and send, receive bytes.
	/// Does not manipulate the sent, received bytes. This class is safe to use from multiple threads.
	/// </summary>
	public class UdpServerSocket {
		/// <summary>
		/// Fired when data is received.
		/// </summary>
		/// <param name="sender">The sender of the data.</param>
		/// <param name="buffer">The buffer containing the data.</param>
		/// <param name="size">The length of the data in the buffer.</param>
		public delegate void ReceiveHandler(EndPoint sender, byte[] buffer, int size);

		private readonly ReceiveHandler _receiveHandler;
		private readonly int _port;
		private readonly Socket _socket;

		/// <summary>
		/// Creates a new instance with the specified options and instantly starts it.
		/// </summary>
		/// <param name="receiveHandler">The handler of received data.</param>
		/// <param name="port">The port the server should listen on.</param>
		public UdpServerSocket(ReceiveHandler receiveHandler, int port) {
			lock (this) {
				_receiveHandler = receiveHandler;
				_port = port;

				_socket = new Socket(SocketType.Dgram, ProtocolType.Udp) {
					ReceiveBufferSize = DoubleProtocol.UdpSocketBufferSize,
					SendBufferSize = DoubleProtocol.UdpSocketBufferSize,
					ReceiveTimeout = DoubleProtocol.SocketOperationTimeout,
					SendTimeout = DoubleProtocol.SocketOperationTimeout
				};

				_socket.Bind(new IPEndPoint(IPAddress.Any, port));
				SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
				eventArgs.Completed += OnReceived;
				eventArgs.SetBuffer(new byte[DoubleProtocol.UdpBufferArraySize], 0, DoubleProtocol.UdpBufferArraySize);
				eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, _port);
				if (!_socket.ReceiveFromAsync(eventArgs)) {
					OnReceived(null, eventArgs);
				}
			}
		}



		/// <summary>
		/// Closes the socket, therefore no more packets will be received and sending will be impossible.
		/// </summary>
		public void Close() {
			lock (this) {
				_socket.Shutdown(SocketShutdown.Both);
				_socket.Close();
			}
		}



		/// <summary>
		/// Sends the specified endpoint the specified data.
		/// </summary>
		/// <param name="recipient">The recipient for the data.</param>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(EndPoint recipient, byte[] data, int offset, int size) {
			lock (this) {
				_socket.SendTo(data, offset, size, SocketFlags.None, recipient);
			}
		}



		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			lock (this) {
				while (true) {
					if (eventArgs.SocketError != SocketError.Success) {
						if (eventArgs.SocketError == SocketError.OperationAborted) {
							return;
						}
						throw new SocketException((int)eventArgs.SocketError);
					}

					_receiveHandler(eventArgs.RemoteEndPoint, eventArgs.Buffer, eventArgs.BytesTransferred);
					eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, _port);
					if (_socket.ReceiveFromAsync(eventArgs)) {
						break;
					}
				}
			}
		}
	}
}
