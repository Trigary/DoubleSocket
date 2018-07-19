using System.Net;
using System.Net.Sockets;

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

		/// <summary>
		/// Creates a new instance with the specified options.
		/// </summary>
		/// <param name="receiveHandler">The handler of received data.</param>
		/// <param name="socketBufferSize">The size of the socket's internal send and receive buffers.</param>
		/// <param name="timeout">The timeout in millis for the socket's functions.</param>
		public UdpClientSocket(ReceiveHandler receiveHandler, int socketBufferSize, int timeout) {
			lock (this) {
				_receiveHandler = receiveHandler;

				_socket = new Socket(SocketType.Dgram, ProtocolType.Udp) {
					ReceiveBufferSize = socketBufferSize,
					SendBufferSize = socketBufferSize,
					ReceiveTimeout = timeout,
					SendTimeout = timeout
				};
			}
		}



		/// <summary>
		/// Starts the instance with the specified options.
		/// </summary>
		/// <param name="ip">The IP to connect to.</param>
		/// <param name="port">The port on which to connect.</param>
		/// <param name="receiveBufferArraySize">The size of the buffer used in the ReceiveHandler.</param>
		public void Start(string ip, int port, int receiveBufferArraySize) {
			lock (this) {
				_socket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
				SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
				eventArgs.Completed += OnReceived;
				eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
				eventArgs.SetBuffer(new byte[receiveBufferArraySize], 0, receiveBufferArraySize);
				if (!_socket.ReceiveAsync(eventArgs)) {
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
		/// Sends the server the specified data.
		/// </summary>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(byte[] data, int offset, int size) {
			lock (this) {
				_socket.Send(data, offset, size, SocketFlags.None);
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

					_receiveHandler(eventArgs.Buffer, eventArgs.BytesTransferred);
					if (_socket.ReceiveAsync(eventArgs)) {
						break;
					}
				}
			}
		}
	}
}
