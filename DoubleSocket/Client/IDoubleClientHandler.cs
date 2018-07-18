using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Client {
	public interface IDoubleClientHandler {
		void OnAuthenticationFailure(byte errorCode);
		void OnUdpConnectionFailure();
		void OnSuccessfulConnect();

		void OnTcpReceived(ByteBuffer buffer);
		void OnUdpReceived(ByteBuffer buffer);
	}
}
