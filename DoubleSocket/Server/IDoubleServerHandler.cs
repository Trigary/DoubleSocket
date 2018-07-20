using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Server {
	public interface IDoubleServerHandler {
		bool AuthenticateClient(IDoubleServerClient client, ByteBuffer buffer, out byte[] encryptionKey, out byte errorCode);

		void OnTcpReceived(IDoubleServerClient client, ByteBuffer buffer);
		void OnUdpReceived(IDoubleServerClient client, ByteBuffer buffer, ushort packetTimestamp);

		void OnLostConnection(IDoubleServerClient client);
	}
}
