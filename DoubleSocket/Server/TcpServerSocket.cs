using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Server {
	/// <summary>
	/// A TCP server which is able to accept new connections and send, receive bytes.
	/// Does not manipulate the sent, received bytes. This class is safe to use from multiple threads.
	/// </summary>
	public class TcpServerSocket {
		/// <summary>
		/// Fired when a new connection is created.
		/// </summary>
		/// <param name="socket">The newly created socket.</param>
		public delegate void NewConnectionHandler(Socket socket);

		/// <summary>
		/// Fired when data is received.
		/// </summary>
		/// <param name="sender">The sender of the data.</param>
		/// <param name="buffer">The buffer containing the data.</param>
		/// <param name="size">The length of the data in the buffer.</param>
		public delegate void ReceiveHandler(Socket sender, byte[] buffer, int size);

		/// <summary>
		/// Fired when the shutdown of the remote peer is detected.
		/// </summary>
		/// <param name="socket">The disconnected socket.</param>
		public delegate void ConnectionLostHandler(Socket socket);

		/// <summary>
		/// The max count of cached SocketAsyncEventArgs instances.
		/// </summary>
		public const int MaxCachedSendEventArgs = 100;

		/// <summary>
		/// Whether the server is currently accepting new connections. True by default.
		/// </summary>
		public bool Accepting { get; private set; }

		private readonly Queue<SocketAsyncEventArgs> _sendEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly Queue<SocketAsyncEventArgs> _receiveEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly NewConnectionHandler _newConnectionHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly ConnectionLostHandler _connectionLostHandler;
		private readonly ICollection<Socket> _connectedSockets;
		private readonly int _receiveBufferArraySize;
		private readonly Socket _socket;
		private bool _stoppedAccepting = true;

		/// <summary>
		/// Creates a new instance with the specified options and instantly starts it.
		/// </summary>
		/// <param name="newConnectionHandler">The handler of new connections.</param>
		/// <param name="receiveHandler">The handler of received data.</param>
		/// <param name="connectionLostHandler">The handler of disconnects.</param>
		/// <param name="connectedSockets">A collection which always contains all connected sockets.</param>
		/// <param name="maxPendingConnections">The maximum count of pending connections (backlog).</param>
		/// <param name="port">The port the server should listen on.</param>
		/// <param name="socketBufferSize">The size of the socket's internal send and receive buffers.</param>
		/// <param name="timeout">The timeout in millis for the socket's functions.</param>
		/// <param name="receiveBufferArraySize">The size of the buffer used in the ReceiveHandler.</param>
		public TcpServerSocket(NewConnectionHandler newConnectionHandler, ReceiveHandler receiveHandler,
								ConnectionLostHandler connectionLostHandler, ICollection<Socket> connectedSockets,
								int maxPendingConnections, int port, int socketBufferSize, int timeout, int receiveBufferArraySize) {
			lock (this) {
				_newConnectionHandler = newConnectionHandler;
				_receiveHandler = receiveHandler;
				_connectionLostHandler = connectionLostHandler;
				_connectedSockets = connectedSockets;
				_receiveBufferArraySize = receiveBufferArraySize;

				_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
					ReceiveBufferSize = socketBufferSize,
					SendBufferSize = socketBufferSize,
					ReceiveTimeout = timeout,
					SendTimeout = timeout,
					NoDelay = true
				};
				_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

				_socket.Bind(new IPEndPoint(IPAddress.Any, port));
				_socket.Listen(maxPendingConnections);
				StartAccepting();
			}
		}



		/// <summary>
		/// Stops the server from accepting new connections and closes all previously created connections.
		/// </summary>
		public void Close() {
			lock (this) {
				Accepting = false;
				_socket.Close();
				foreach (Socket socket in _connectedSockets) {
					TcpHelper.DisconnectAsync(socket, _sendEventArgsQueue, OnSent);
				}
			}
		}

		/// <summary>
		/// Kicks the speicifed socket from the server.
		/// </summary>
		/// <param name="socket">The socket to kick.</param>
		public void Disconnect(Socket socket) {
			lock (this) {
				TcpHelper.DisconnectAsync(socket, _sendEventArgsQueue, OnSent);
			}
		}

		private void ForcedDisconnect(Socket socket) {
			socket.Shutdown(SocketShutdown.Both);
			socket.Close();
		}

		/// <summary>
		/// Sends the specified connection the specified data.
		/// </summary>
		/// <param name="recipient">The recipient for the data.</param>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(Socket recipient, byte[] data, int offset, int size) {
			lock (this) {
				SocketAsyncEventArgs eventArgs;
				if (_sendEventArgsQueue.Count == 0) {
					eventArgs = new SocketAsyncEventArgs();
					eventArgs.Completed += OnSent;
				} else {
					eventArgs = _sendEventArgsQueue.Dequeue();
				}

				eventArgs.SetBuffer(data, offset, size);
				if (!recipient.SendAsync(eventArgs)) {
					OnSent(null, eventArgs);
				}
			}
		}



		/// <summary>
		/// Start accepting new connections again.
		/// </summary>
		public void StartAccepting() {
			lock (this) {
				Accepting = true;
				if (_stoppedAccepting) {
					_stoppedAccepting = false;
					SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
					eventArgs.Completed += OnAccepted;
					if (!_socket.AcceptAsync(eventArgs)) {
						OnAccepted(null, eventArgs);
					}
				}
			}
		}

		/// <summary>
		/// Stop accepting new connections.
		/// </summary>
		public void StopAccepting() {
			lock (this) {
				Accepting = false;
			}
		}



		private void OnAccepted(object sender, SocketAsyncEventArgs eventArgs) {
			lock (this) {
				while (true) {
					if (eventArgs.SocketError != SocketError.Success) {
						if (eventArgs.SocketError == SocketError.OperationAborted) {
							return;
						}
						throw new SocketException((int)eventArgs.SocketError);
					}

					Socket newSocket = eventArgs.AcceptSocket;
					eventArgs.AcceptSocket = null;

					if (!Accepting) {
						// ReSharper disable once PossibleNullReferenceException
						newSocket.Shutdown(SocketShutdown.Both);
						newSocket.Disconnect(false);
						_stoppedAccepting = true;
						return;
					}

					_newConnectionHandler(newSocket);
					if (Accepting) {
						StartReceiving(newSocket);
						if (_socket.AcceptAsync(eventArgs)) {
							break;
						}
					} else {
						_stoppedAccepting = true;
						return;
					}
				}
			}
		}

		private void StartReceiving(Socket socket) {
			SocketAsyncEventArgs eventArgs;
			if (_receiveEventArgsQueue.Count == 0) {
				eventArgs = new SocketAsyncEventArgs();
				eventArgs.SetBuffer(new byte[_receiveBufferArraySize], 0, _receiveBufferArraySize);
				eventArgs.Completed += OnReceived;
				eventArgs.UserToken = new UserToken();
			} else {
				eventArgs = _receiveEventArgsQueue.Dequeue();
			}

			((UserToken)eventArgs.UserToken).Initialize(socket);
			if (!socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			lock (this) {
				while (true) {
					if (TcpHelper.ShouldHandleError(eventArgs, out bool isRemoteShutdown)) {
						if (isRemoteShutdown) {
							UserToken userToken = (UserToken)eventArgs.UserToken;
							ForcedDisconnect(userToken.Socket);
							_connectionLostHandler(userToken.Socket);
							userToken.Deinitialize();
							_receiveEventArgsQueue.Enqueue(eventArgs);
						}
						return;
					}

					UserToken token = (UserToken)eventArgs.UserToken;
					if (token.ShouldHandle()) {
						_receiveHandler(token.Socket, eventArgs.Buffer, eventArgs.BytesTransferred);
					}

					if (token.Socket.ReceiveAsync(eventArgs)) {
						break;
					}
				}
			}
		}

		private void OnSent(object sender, SocketAsyncEventArgs eventArgs) {
			lock (this) {
				if (TcpHelper.ShouldHandleError(eventArgs, out _)) {
					return;
				}

				if (_sendEventArgsQueue.Count < MaxCachedSendEventArgs) {
					_sendEventArgsQueue.Enqueue(eventArgs);
				}
			}
		}



		private class UserToken {
			public Socket Socket { get; private set; }
			private bool _hasSkipped;

			public void Initialize(Socket socket) {
				Socket = socket;
				_hasSkipped = false;
			}

			public void Deinitialize() {
				Socket = null;
			}

			public bool ShouldHandle() { //The first receive event fires for an empty buffer
				if (_hasSkipped) {
					return true;
				}
				_hasSkipped = true;
				return false;
			}
		}
	}
}
