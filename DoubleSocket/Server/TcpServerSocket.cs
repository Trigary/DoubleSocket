using System;
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

		private readonly Queue<SocketAsyncEventArgs> _sendEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly Queue<SocketAsyncEventArgs> _receiveEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly NewConnectionHandler _newConnectionHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly ConnectionLostHandler _connectionLostHandler;
		private readonly ICollection<Socket> _connectedSockets;
		private readonly Socket _socket;
		private bool _accepting;
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
		public TcpServerSocket(NewConnectionHandler newConnectionHandler, ReceiveHandler receiveHandler,
								ConnectionLostHandler connectionLostHandler, ICollection<Socket> connectedSockets,
								int maxPendingConnections, int port) {
			_newConnectionHandler = newConnectionHandler;
			_receiveHandler = receiveHandler;
			_connectionLostHandler = connectionLostHandler;
			_connectedSockets = connectedSockets;

			_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
				ReceiveBufferSize = DoubleProtocol.TcpSocketBufferSize,
				SendBufferSize = DoubleProtocol.TcpSocketBufferSize,
				ReceiveTimeout = DoubleProtocol.SocketOperationTimeout,
				SendTimeout = DoubleProtocol.SocketOperationTimeout,
				NoDelay = true
			};
			_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

			_socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
			_socket.Listen(maxPendingConnections);
			StartAccepting();
		}



		/// <summary>
		/// Start accepting new connections again.
		/// </summary>
		public void StartAccepting() {
			lock (_socket) {
				_accepting = true;
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
			lock (_socket) {
				_accepting = false;
			}
		}



		/// <summary>
		/// Stops the server from accepting new connections and closes all previously created connections.
		/// </summary>
		public void Close() {
			lock (_socket) {
				_accepting = false;
			}
			_socket.Close();
			lock (_connectedSockets) {
				foreach (Socket socket in _connectedSockets) {
					lock (_sendEventArgsQueue) {
						TcpHelper.DisconnectAsync(socket, _sendEventArgsQueue, OnSent);
					}
				}
			}
		}

		/// <summary>
		/// Kicks the speicifed socket from the server.
		/// </summary>
		/// <param name="socket">The socket to kick.</param>
		public void Disconnect(Socket socket) {
			lock (_sendEventArgsQueue) {
				TcpHelper.DisconnectAsync(socket, _sendEventArgsQueue, OnSent);
			}
		}

		// ReSharper disable once MemberCanBeMadeStatic.Local
		private bool ForcedDisconnect(Socket socket) {
			try {
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();
				return true;
			} catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException) {
				return false;
			}
		}

		/// <summary>
		/// Sends the specified connection the specified data.
		/// </summary>
		/// <param name="recipient">The recipient for the data.</param>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(Socket recipient, byte[] data, int offset, int size) {
			SocketAsyncEventArgs eventArgs;
			lock (_sendEventArgsQueue) {
				if (_sendEventArgsQueue.Count == 0) {
					eventArgs = new SocketAsyncEventArgs();
					eventArgs.Completed += OnSent;
					eventArgs.SetBuffer(new byte[DoubleProtocol.TcpBufferArraySize], 0, DoubleProtocol.TcpBufferArraySize);
				} else {
					eventArgs = _sendEventArgsQueue.Dequeue();
				}
			}

			Buffer.BlockCopy(data, offset, eventArgs.Buffer, eventArgs.Offset, size);
			eventArgs.SetBuffer(0, size);

			bool isAsync;
			try {
				isAsync = recipient.SendAsync(eventArgs);
			} catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException) {
				return;
			}

			if (!isAsync) {
				OnSent(null, eventArgs);
			}
		}

		private void OnAccepted(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (eventArgs.SocketError != SocketError.Success) {
					if (eventArgs.SocketError == SocketError.OperationAborted
						|| eventArgs.SocketError == SocketError.Shutdown) {
						return;
					}
					throw new SocketException((int)eventArgs.SocketError);
				}

				Socket newSocket = eventArgs.AcceptSocket;
				eventArgs.AcceptSocket = null;
				lock (_socket) {
					if (!_accepting) {
						// ReSharper disable once PossibleNullReferenceException
						newSocket.Shutdown(SocketShutdown.Both);
						newSocket.Disconnect(false);
						_stoppedAccepting = true;
						return;
					}
				}

				_newConnectionHandler(newSocket);
				StartReceiving(newSocket);
				if (_socket.AcceptAsync(eventArgs)) {
					break;
				}
			}
		}

		private void StartReceiving(Socket socket) {
			SocketAsyncEventArgs eventArgs;
			lock (_receiveEventArgsQueue) {
				if (_receiveEventArgsQueue.Count == 0) {
					eventArgs = new SocketAsyncEventArgs();
					eventArgs.SetBuffer(new byte[DoubleProtocol.TcpBufferArraySize], 0, DoubleProtocol.TcpBufferArraySize);
					eventArgs.Completed += OnReceived;
					eventArgs.UserToken = new UserToken();
				} else {
					eventArgs = _receiveEventArgsQueue.Dequeue();
				}
			}

			((UserToken)eventArgs.UserToken).Initialize(socket);
			if (!socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (TcpHelper.ShouldHandleError(eventArgs, out bool isRemoteShutdown)) {
					if (isRemoteShutdown) {
						UserToken userToken = (UserToken)eventArgs.UserToken;
						if (!ForcedDisconnect(userToken.Socket)) {
							return;
						}

						lock (_connectedSockets) {
							_connectionLostHandler(userToken.Socket);
						}

						userToken.Deinitialize();
						lock (_receiveEventArgsQueue) {
							_receiveEventArgsQueue.Enqueue(eventArgs);
						}
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

		private void OnSent(object sender, SocketAsyncEventArgs eventArgs) {
			if (TcpHelper.ShouldHandleError(eventArgs, out _)) {
				return;
			}

			lock (_sendEventArgsQueue) {
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
				_hasSkipped = DoubleProtocol.IsMonoClr;
				//The first receive event fires for an empty buffer when not using Mono
			}

			public void Deinitialize() {
				Socket = null;
			}

			public bool ShouldHandle() {
				if (_hasSkipped) {
					return true;
				}
				_hasSkipped = true;
				return false;
			}
		}
	}
}
