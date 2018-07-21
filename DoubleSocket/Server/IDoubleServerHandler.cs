using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Server {
	public interface IDoubleServerHandler {
		bool TcpAuthenticateClient(IDoubleServerClient client, ByteBuffer buffer, out byte[] encryptionKey, out byte errorCode);
		void OnFullAuthentication(IDoubleServerClient client);

		void OnTcpReceived(IDoubleServerClient client, ByteBuffer buffer);
		void OnUdpReceived(IDoubleServerClient client, ByteBuffer buffer, ushort packetTimestamp);

		void OnLostConnection(IDoubleServerClient client, DoubleServer.ClientState state);
	}
}
