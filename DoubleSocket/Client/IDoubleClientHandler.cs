using System.Net.Sockets;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Client {
	public interface IDoubleClientHandler {
		void OnConnectionFailure(SocketError error);
		void OnTcpAuthenticationFailure(byte errorCode);
		void OnAuthenticationTimeout(DoubleClient.State state);

		void OnFullAuthentication();

		void OnTcpReceived(ByteBuffer buffer);
		void OnUdpReceived(ByteBuffer buffer, ushort packetTimestamp);

		void OnConnectionLost(DoubleClient.State state);
	}
}
