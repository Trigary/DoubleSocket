using System.Net;
using System.Net.Sockets;

namespace DoubleSocket.Client {
	public class TcpClientSocket { //TODO length-prefix and packet reassembly in this class
		public delegate void ConnectHandler(TcpClientSocket client);
		public delegate void ReceiveHandler(byte[] buffer, int size);

		private readonly ConnectHandler _connectHandler;
		private readonly ReceiveHandler _receiveHandler;
		private readonly Socket _socket;

		public TcpClientSocket(ConnectHandler connectHandler, ReceiveHandler receiveHandler, int receiveBufferArraySize,
								string ip, int port, int socketBufferSize, int timeout) {
			_connectHandler = connectHandler;
			_receiveHandler = receiveHandler;

			_socket = new Socket(SocketType.Stream, ProtocolType.Tcp) {
				ReceiveBufferSize = socketBufferSize,
				SendBufferSize = socketBufferSize,
				ReceiveTimeout = timeout,
				SendTimeout = timeout,
				NoDelay = true
			};

			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnConnected;
			eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
			eventArgs.SetBuffer(new byte[receiveBufferArraySize], 0, receiveBufferArraySize);
			if (!_socket.ConnectAsync(eventArgs)) {
				OnConnected(null, eventArgs);
			}
		}



		public void Close() {
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
		}

		public void Send(byte[] data, int size) {
			_socket.Send(data, size, SocketFlags.None);
		}



		private void OnConnected(object sender, SocketAsyncEventArgs eventArgs) {
			if (eventArgs.SocketError != SocketError.Success) {
				throw new SocketException((int)eventArgs.SocketError);
			}

			_connectHandler(this);
			eventArgs.Completed -= OnConnected;
			eventArgs.Completed += OnReceived;
			if (!_socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}

		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
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
}
