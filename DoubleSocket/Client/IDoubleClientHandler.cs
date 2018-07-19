using System.Net.Sockets;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Client {
	public interface IDoubleClientHandler {
		void OnConnectionFailure(SocketError error);
		void OnAuthenticationFailure(byte errorCode);

		void OnSuccessfulConnect();

		void OnTcpReceived(ByteBuffer buffer);
		void OnUdpReceived(ByteBuffer buffer);

		void OnConnectionLost();
	}
}
