using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Client {
	/// <summary>
	/// An UDP client hich is able to connect to a server and send, receive bytes.
	/// Does not manipulate the sent, received bytes. This class is safe to use from multiple threads.
	/// </summary>
	public class UdpClientSocket {
		/// <summary>
		/// Fired when data is received.
		/// </summary>
		/// <param name="buffer">The buffer containing the data.</param>
		/// <param name="size">The length of the data in the buffer.</param>
		public delegate void ReceiveHandler(byte[] buffer, int size);

		private readonly ReceiveHandler _receiveHandler;
		private readonly Socket _socket;
		private volatile bool _disposed;

		/// <summary>
		/// Creates a new instance with the specified options.
		/// </summary>
		/// <param name="receiveHandler">The handler of received data.</param>
		public UdpClientSocket(ReceiveHandler receiveHandler) {
			_receiveHandler = receiveHandler;

			_socket = new Socket(SocketType.Dgram, ProtocolType.Udp) {
				ReceiveBufferSize = DoubleProtocol.UdpSocketBufferSize,
				SendBufferSize = DoubleProtocol.UdpSocketBufferSize,
				ReceiveTimeout = DoubleProtocol.SocketOperationTimeout,
				SendTimeout = DoubleProtocol.SocketOperationTimeout
			};
		}



		/// <summary>
		/// Starts the instance with the specified options.
		/// </summary>
		/// <param name="ip">The IP to connect to.</param>
		/// <param name="port">The port on which to connect.</param>
		public void Start(IPAddress ip, int port) {
			_socket.Connect(new IPEndPoint(ip, port));
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnReceived;
			eventArgs.RemoteEndPoint = new IPEndPoint(ip, port);
			eventArgs.SetBuffer(new byte[DoubleProtocol.UdpBufferArraySize], 0, DoubleProtocol.UdpBufferArraySize);
			if (!_socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		/// <summary>
		/// Closes the socket, therefore no more packets will be received and sending will be impossible.
		/// </summary>
		public void Close() {
			_disposed = true;
			if (_socket.Connected) {
				_socket.Shutdown(SocketShutdown.Both);
			}
			_socket.Close();
		}



		/// <summary>
		/// Sends the server the specified data.
		/// </summary>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(byte[] data, int offset, int size) {
			if (!_disposed) {
				_socket.Send(data, offset, size, SocketFlags.None);
			}
		}



		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (eventArgs.SocketError != SocketError.Success) {
					if (eventArgs.SocketError == SocketError.OperationAborted) {
						return;
					}
					throw new SocketException((int)eventArgs.SocketError);
				}

				_receiveHandler(eventArgs.Buffer, eventArgs.BytesTransferred);
				if (_socket.ReceiveAsync(eventArgs)) {
					break;
				}
			}
		}
	}
}
