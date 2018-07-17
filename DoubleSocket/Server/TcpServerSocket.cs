using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DoubleSocket.Server {
	public class TcpServerSocket { //TODO length-prefix and packet reassembly in this class
		public delegate void NewSocketHandler(Socket socket);
		public delegate void ReceiveHandler(Socket sender, byte[] buffer, int size);

		public bool Accepting { get; private set; }
		private readonly HashSet<Socket> _sockets = new HashSet<Socket>();
		private readonly NewSocketHandler _newSocketHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly int _receiveBufferArraySize;
		private readonly Socket _socket;
		private bool _stoppedAccepting = true;

		public TcpServerSocket(NewSocketHandler newSocketHandler, ReceiveHandler receiveHandler, int receiveBufferArraySize,
								int maxPendingConnections, int port, int socketBufferSize, int timeout) {
			_newSocketHandler = newSocketHandler;
			_receiveHandler = receiveHandler;
			_receiveBufferArraySize = receiveBufferArraySize;

			_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
				ReceiveBufferSize = socketBufferSize,
				SendBufferSize = socketBufferSize,
				ReceiveTimeout = timeout,
				SendTimeout = timeout,
				NoDelay = true
			};

			_socket.Bind(new IPEndPoint(IPAddress.Any, port));
			_socket.Listen(maxPendingConnections);
			StartAccepting();
		}



		public void Close() {
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
			foreach (Socket socket in _sockets) {
				socket.Close();
			}
		}

		public void Disconnect(Socket socket) {
			_sockets.Remove(socket);
			socket.Close();
		}

		public void Send(Socket recipient, byte[] data, int size) {
			recipient.Send(data, size, SocketFlags.None);
		}



		public void StartAccepting() {
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

		public void StopAccepting() {
			Accepting = false;
		}



		private void OnAccepted(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (eventArgs.SocketError != SocketError.Success) {
					throw new SocketException((int)eventArgs.SocketError);
				}

				Socket newSocket = eventArgs.AcceptSocket;
				eventArgs.AcceptSocket = null;

				if (!Accepting) {
					// ReSharper disable once PossibleNullReferenceException
					newSocket.Close();
					_stoppedAccepting = true;
					return;
				}

				_sockets.Add(newSocket);
				_newSocketHandler(newSocket);
				StartReceiving(newSocket);
				if (_socket.AcceptAsync(eventArgs)) {
					break;
				}
			}
		}

		private void StartReceiving(Socket socket) {
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.SetBuffer(new byte[_receiveBufferArraySize], 0, _receiveBufferArraySize);
			eventArgs.UserToken = new UserToken(socket);
			eventArgs.Completed += OnReceived;
			if (!socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (eventArgs.SocketError != SocketError.Success) {
					throw new SocketException((int)eventArgs.SocketError);
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



		private class UserToken {
			public Socket Socket { get; }
			private bool _hasSkipped;

			public UserToken(Socket socket) {
				Socket = socket;
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
