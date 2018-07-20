using System.Net.Sockets;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Client {
	public interface IDoubleClientHandler {
		void OnConnectionFailure(SocketError error);
		void OnAuthenticationFailure(byte errorCode);

		void OnSuccessfulAuthentication();

		void OnTcpReceived(ByteBuffer buffer);
		void OnUdpReceived(ByteBuffer buffer, ushort packetTimestamp);

		void OnConnectionLost();
	}
}
