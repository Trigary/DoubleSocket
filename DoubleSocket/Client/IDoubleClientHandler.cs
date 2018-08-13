using System.Net.Sockets;
using DoubleSocket.Utility.BitBuffer;

namespace DoubleSocket.Client {
	/// <summary>
	/// An interface which contains the DoubleClient's events.
	/// Apart from its methods being called it is only used in the DoubleClient's constructor.
	/// </summary>
	public interface IDoubleClientHandler {
		/// <summary>
		/// Called when the client was unable to connect to the server.
		/// </summary>
		/// <param name="error">The exact error.</param>
		void OnConnectionFailure(SocketError error);

		/// <summary>
		/// Called when the client's TCP authentication was denied.
		/// </summary>
		/// <param name="errorCode">The error code returned by the server.</param>
		void OnTcpAuthenticationFailure(byte errorCode);

		/// <summary>
		/// Called when the server doesn't respond to the authentication steps in time.
		/// </summary>
		/// <param name="state">The state in which the client was before this event.</param>
		void OnAuthenticationTimeout(DoubleClient.State state);

		/// <summary>
		/// Called when the client's UDP channel also got authenticated after its TCP channel.
		/// </summary>
		/// <param name="buffer">The buffer which holds the payload sent by the server in the final
		/// authentication confirmation packet. The buffer may be empty.</param>
		void OnFullAuthentication(BitBuffer buffer);

		/// <summary>
		/// Called when the client received data over the TCP channel.
		/// </summary>
		/// <param name="buffer">The buffer which holds the decrypted received data.</param>
		void OnTcpReceived(BitBuffer buffer);

		/// <summary>
		/// Called when the client received data over the UDP channel.
		/// </summary>
		/// <param name="buffer">The buffer which holds the decrypted received data.</param>
		/// <param name="packetTimestamp">A timestamp which can be used in the DoubleProtocol utility class.</param>
		void OnUdpReceived(BitBuffer buffer, uint packetTimestamp);

		/// <summary>
		/// Called when the client loses connection to the server.
		/// This is not called when the local client gets closed, but it is when the server kicks this client.
		/// </summary>
		/// <param name="state">The state in which the client was before this event.</param>
		void OnConnectionLost(DoubleClient.State state);
	}
}
