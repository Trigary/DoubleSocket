using System;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Server {
	/// <summary>
	/// An interface which contains the DoubleServer's events.
	/// Apart from its methods being called it is only used in the DoubleServer's constructor.
	/// </summary>
	public interface IDoubleServerHandler {
		/// <summary>
		/// Called when a client asks to be authenticated.
		/// </summary>
		/// <param name="client">The client in question.</param>
		/// <param name="buffer">The buffer which holds the unencrypted received data.</param>
		/// <param name="encryptionKey">The encryption key which should be used to encrypt all messages to this client.</param>
		/// <param name="errorCode">The error code of the failed authentication, this is relayed to the client.
		/// It is ignored if the authentication is successful.</param>
		/// <returns>Whether the authentication was successful.</returns>
		bool TcpAuthenticateClient(IDoubleServerClient client, ByteBuffer buffer, out byte[] encryptionKey, out byte errorCode);

		/// <summary>
		/// Called when the client's UDP channel also got authenticated after its TCP channel.
		/// </summary>
		/// <param name="client">The client in question.</param>
		/// <returns>The action which writes the payload which should be included in the final authentication confirmation
		/// packet sent to the client to a buffer or null if no extra information should be sent in the packet.</returns>
		Action<ByteBuffer> OnFullAuthentication(IDoubleServerClient client);

		/// <summary>
		/// Called when the server received data over the TCP channel from the client.
		/// </summary>
		/// <param name="client">The client in question.</param>
		/// <param name="buffer">The buffer which holds the decrypted received data.</param>
		void OnTcpReceived(IDoubleServerClient client, ByteBuffer buffer);

		/// <summary>
		/// Called when the server received data over the UDP channel from the client.
		/// </summary>
		/// <param name="client">The client in question.</param>
		/// <param name="buffer">The buffer which holds the decrypted received data.</param>
		/// <param name="packetTimestamp">A timestamp which can be used in the DoubleProtocol utility class.</param>
		void OnUdpReceived(IDoubleServerClient client, ByteBuffer buffer, ushort packetTimestamp);

		/// <summary>
		/// Called when the client loses connection to the server.
		/// This is not called when the server kicks the client, but it is when the remote client gets closed.
		/// </summary>
		/// <param name="client">The client in question.</param>
		/// <param name="state">The state in which the client was before this event.</param>
		void OnLostConnection(IDoubleServerClient client, DoubleServer.ClientState state);
	}
}
