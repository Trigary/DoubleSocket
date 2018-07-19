using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Client {
	/// <summary>
	/// A TCP client hich is able to connect to a server and send, receive bytes.
	/// Does not manipulate the sent, received bytes. This class is safe to use from multiple threads.
	/// </summary>
	public class TcpClientSocket {
		/// <summary>
		/// Fired when the client connects to the server.
		/// </summary>
		public delegate void ConnectHandler();

		/// <summary>
		/// Fired when data is received.
		/// </summary>
		/// <param name="buffer">The buffer containing the data.</param>
		/// <param name="size">The length of the data in the buffer.</param>
		public delegate void ReceiveHandler(byte[] buffer, int size);

		/// <summary>
		/// The max count of cached SocketAsyncEventArgs instances.
		/// </summary>
		public const int MaxCachedSendEventArgs = 5;

		private readonly Queue<SocketAsyncEventArgs> _sendEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly ConnectHandler _connectHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly Socket _socket;

		/// <summary>
		/// Creates a new instance with the specified options.
		/// </summary>
		/// <param name="connectHandler">The handler of the connection.</param>
		/// <param name="receiveHandler">The handler of received data.</param>
		/// <param name="socketBufferSize">The size of the socket's internal send and receive buffers.</param>
		/// <param name="timeout">The timeout in millis for the socket's functions.</param>
		public TcpClientSocket(ConnectHandler connectHandler, ReceiveHandler receiveHandler,
								int socketBufferSize, int timeout) {
			lock (this) {
				_connectHandler = connectHandler;
				_receiveHandler = receiveHandler;

				_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
					ReceiveBufferSize = socketBufferSize,
					SendBufferSize = socketBufferSize,
					ReceiveTimeout = timeout,
					SendTimeout = timeout,
					NoDelay = true
				};
			}
		}



		/// <summary>
		/// Starts the instance with the specified options.
		/// </summary>
		/// <param name="ip">The IP to connect to.</param>
		/// <param name="port">The port on which to connect.</param>
		/// /// <param name="receiveBufferArraySize">The size of the buffer used in the ReceiveHandler.</param>
		public void Start(string ip, int port, int receiveBufferArraySize) {
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnConnected;
			eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
			eventArgs.SetBuffer(new byte[receiveBufferArraySize], 0, receiveBufferArraySize);
			if (!_socket.ConnectAsync(eventArgs)) {
				OnConnected(null, eventArgs);
			}
		}

		/// <summary>
		/// Closes the connection.
		/// </summary>
		public void Close() {
			lock (this) {
				TcpHelper.DisconnectAsync(_socket, _sendEventArgsQueue, OnSent);
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
				SocketAsyncEventArgs eventArgs;
				if (_sendEventArgsQueue.Count == 0) {
					eventArgs = new SocketAsyncEventArgs();
					eventArgs.Completed += OnSent;
				} else {
					eventArgs = _sendEventArgsQueue.Dequeue();
				}

				eventArgs.SetBuffer(data, offset, size);
				if (!_socket.SendAsync(eventArgs)) {
					OnSent(null, eventArgs);
				}
			}
		}



		private void OnConnected(object sender, SocketAsyncEventArgs eventArgs) {
			if (eventArgs.SocketError != SocketError.Success) {
				throw new SocketException((int)eventArgs.SocketError);
			}

			lock (this) {
				_connectHandler();
				eventArgs.Completed -= OnConnected;
				eventArgs.Completed += OnReceived;
				if (!_socket.ReceiveAsync(eventArgs)) {
					OnReceived(null, eventArgs);
				}
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			lock (this) {
				while (true) {
					if (eventArgs.SocketError != SocketError.Success) {
						throw new SocketException((int)eventArgs.SocketError);
					}

					_receiveHandler(eventArgs.Buffer, eventArgs.BytesTransferred);
					if (_socket.ReceiveAsync(eventArgs)) {
						break;
					}
				}
			}
		}

		private void OnSent(object sender, SocketAsyncEventArgs eventArgs) {
			if (eventArgs.SocketError != SocketError.Success) {
				throw new SocketException((int)eventArgs.SocketError);
			}

			lock (this) {
				if (_sendEventArgsQueue.Count < MaxCachedSendEventArgs) {
					_sendEventArgsQueue.Enqueue(eventArgs);
				}
			}
		}
	}
}
