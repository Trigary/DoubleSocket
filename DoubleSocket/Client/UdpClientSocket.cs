using System.Net;
using System.Net.Sockets;

namespace DoubleSocket.Client {
	public class UdpClientSocket {
		public delegate void ReceiveHandler(byte[] buffer, int size);
		
		private readonly ReceiveHandler _receiveHandler;
		private readonly Socket _socket;

		public UdpClientSocket(ReceiveHandler receiveHandler, int receiveBufferArraySize,
							string ip, int port, int socketBufferSize, int timeout) {
			_receiveHandler = receiveHandler;

			_socket = new Socket(SocketType.Dgram, ProtocolType.Udp) {
				ReceiveBufferSize = socketBufferSize,
				SendBufferSize = socketBufferSize,
				ReceiveTimeout = timeout,
				SendTimeout = timeout
			};
			
			_socket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
			SocketAsyncEventArgs eventArgs = new SocketAsyncEventArgs();
			eventArgs.Completed += OnReceived;
			eventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
			eventArgs.SetBuffer(new byte[receiveBufferArraySize], 0, receiveBufferArraySize);
			if (!_socket.ReceiveAsync(eventArgs)) {
				OnReceived(null, eventArgs);
			}
		}



		public void Close() {
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
		}

		public void Send(byte[] data, int size) {
			_socket.Send(data, size, SocketFlags.None);
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
