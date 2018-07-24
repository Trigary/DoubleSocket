using System;
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
		/// Fired when the client successfully connected to the server.
		/// </summary>
		public delegate void SuccessfulConnectHandler();

		/// <summary>
		/// Fired when the client failed to connect to the server.
		/// </summary>
		/// <param name="error">The error code describing the cause of the failure.</param>
		public delegate void FailedConnectHandler(SocketError error);

		/// <summary>
		/// Fired when data is received.
		/// </summary>
		/// <param name="buffer">The buffer containing the data.</param>
		/// <param name="size">The length of the data in the buffer.</param>
		public delegate void ReceiveHandler(byte[] buffer, int size);

		/// <summary>
		/// Fired when the shutdown of the remote peer is detected.
		/// </summary>
		public delegate void ConnectionLostHandler();

		/// <summary>
		/// The max count of cached SocketAsyncEventArgs instances.
		/// </summary>
		public const int MaxCachedSendEventArgs = 5;

		private readonly Queue<SocketAsyncEventArgs> _sendEventArgsQueue = new Queue<SocketAsyncEventArgs>();
		private readonly SuccessfulConnectHandler _successfulConnectHandler;
		private readonly FailedConnectHandler _failedConnectHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly ConnectionLostHandler _connectionLostHandler;
		private readonly Socket _socket;

		/// <summary>
		/// Creates a new instance with the specified options.
		/// </summary>
		/// <param name="successfulConnectHandler">The handler of the successful connection.</param>
		/// <param name="failedConnectHandler">The handler of the failed connection.</param>
		/// <param name="receiveHandler">The handler of received data.</param>
		/// <param name="connectionLostHandler">The handler of the disconnect.</param>
		public TcpClientSocket(SuccessfulConnectHandler successfulConnectHandler, FailedConnectHandler failedConnectHandler,
								ReceiveHandler receiveHandler, ConnectionLostHandler connectionLostHandler) {
			_successfulConnectHandler = successfulConnectHandler;
			_failedConnectHandler = failedConnectHandler;
			_receiveHandler = receiveHandler;
			_connectionLostHandler = connectionLostHandler;

			_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
				ReceiveBufferSize = DoubleProtocol.TcpSocketBufferSize,
				SendBufferSize = DoubleProtocol.TcpSocketBufferSize,
				ReceiveTimeout = DoubleProtocol.SocketOperationTimeout,
				SendTimeout = DoubleProtocol.SocketOperationTimeout,
				NoDelay = true
			};
			_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
		}



		/// <summary>
		/// Starts the instance with the specified options.
		/// </summary>
		/// <param name="ip">The IP to connect to.</param>
		/// <param name="port">The port on which to connect.</param>
		public void Start(IPAddress ip, int port) {
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnConnected;
			eventArgs.RemoteEndPoint = new IPEndPoint(ip, port);
			eventArgs.SetBuffer(new byte[DoubleProtocol.TcpBufferArraySize], 0, DoubleProtocol.TcpBufferArraySize);
			if (!_socket.ConnectAsync(eventArgs)) {
				OnConnected(null, eventArgs);
			}
		}

		/// <summary>
		/// Closes the connection.
		/// </summary>
		public void Close() {
			lock (_sendEventArgsQueue) {
				TcpHelper.DisconnectAsync(_socket, _sendEventArgsQueue, OnSent);
			}
		}

		private bool ForcedClose() {
			try {
				if (_socket.Connected) {
					_socket.Shutdown(SocketShutdown.Both);
					_socket.Close();
					return true;
				} else {
					_socket.Close();
					return false;
				}
			} catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException) {
				return false;
			}
		}



		/// <summary>
		/// Sends the server the specified data.
		/// </summary>
		/// <param name="data">The data to send.</param>
		/// <param name="offset">The offset of the data in the buffer.</param>
		/// <param name="size">The size of the data.</param>
		public void Send(byte[] data, int offset, int size) {
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
				isAsync = _socket.SendAsync(eventArgs);
			} catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException) {
				return;
			}

			if (!isAsync) {
				OnSent(null, eventArgs);
			}
		}



		private void OnConnected(object sender, SocketAsyncEventArgs eventArgs) {
			switch (eventArgs.SocketError) {
				case SocketError.Success:
					break;
				case SocketError.OperationAborted:
					return;
				case SocketError.ConnectionRefused:
				case SocketError.HostDown:
				case SocketError.HostNotFound:
				case SocketError.HostUnreachable:
				case SocketError.NetworkDown:
				case SocketError.NetworkUnreachable:
				case SocketError.NoData:
				case SocketError.SystemNotReady:
				case SocketError.TryAgain:
				case SocketError.TimedOut:
					ForcedClose();
					_failedConnectHandler(eventArgs.SocketError);
					return;
				default:
					throw new SocketException((int)eventArgs.SocketError);
			}

			_successfulConnectHandler();
			eventArgs.Completed -= OnConnected;
			eventArgs.Completed += OnReceived;
			if (!_socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (TcpHelper.ShouldHandleError(eventArgs, out bool isRemoteShutdown)) {
					if (isRemoteShutdown && ForcedClose()) {
						_connectionLostHandler();
					}
					return;
				}

				_receiveHandler(eventArgs.Buffer, eventArgs.BytesTransferred);
				if (_socket.ReceiveAsync(eventArgs)) {
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
	}
}
