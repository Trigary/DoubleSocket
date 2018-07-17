using System.Net;
using System.Net.Sockets;

namespace DoubleSocket.Server {
	public class UdpServerSocket {
		public delegate void ReceiveHandler(EndPoint sender, byte[] buffer, int size);

		private readonly ReceiveHandler _receiveHandler;
		private readonly int _port;
		private readonly Socket _socket;

		public UdpServerSocket(ReceiveHandler receiveHandler, int receiveBufferArraySize,
								int port, int socketBufferSize, int timeout) {
			_receiveHandler = receiveHandler;
			_port = port;

			_socket = new Socket(SocketType.Dgram, ProtocolType.Udp) {
				ReceiveBufferSize = socketBufferSize,
				SendBufferSize = socketBufferSize,
				ReceiveTimeout = timeout,
				SendTimeout = timeout
			};

			_socket.Bind(new IPEndPoint(IPAddress.Any, port));
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnReceived;
			eventArgs.SetBuffer(new byte[receiveBufferArraySize], 0, receiveBufferArraySize);
			eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, _port);
			if (!_socket.ReceiveFromAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}



		public void Close() {
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
		}

		public void Send(EndPoint recipient, byte[] data, int size) {
			_socket.SendTo(data, size, SocketFlags.None, recipient);
		}



		private void OnReceived(object sender, SocketAsyncEventArgs eventArgs) {
			while (true) {
				if (eventArgs.SocketError != SocketError.Success) {
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
