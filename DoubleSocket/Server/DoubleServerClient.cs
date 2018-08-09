using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Server {
	/// <summary>
	/// A unique client. Instances are used as paremeters to identify clients.
	/// </summary>
	public interface IDoubleServerClient {
		/// <summary>
		/// The current state of the client.
		/// </summary>
		DoubleServer.ClientState State { get; }

		/// <summary>
		/// Extra data which is not specified by this library, but instead by the user of the library.
		/// Example usage: linking this instance to another client instance which holds more information.
		/// </summary>
		object ExtraData { get; set; }

		/// <summary>
		/// The IP address of the client.
		/// </summary>
		IPEndPoint Address { get; }
	}

	/// <summary>
	/// A unique client. Used to hold data regarding them and to be passed as paramters to handlers.
	/// </summary>
	public class DoubleServerClient : IDoubleServerClient {
		private static readonly Random Random = new Random();
		private static readonly byte[] RandomBytes = new byte[8];

		/// <summary>
		/// The current state of the client.
		/// </summary>
		public DoubleServer.ClientState State { get; private set; } = DoubleServer.ClientState.TcpAuthenticating;

		/// <summary>
		/// The extra data of the client, shouldn't be used on the library-level.
		/// </summary>
		public object ExtraData { get; set; }

		/// <summary>
		/// The IP address of the client.
		/// </summary>
		public IPEndPoint Address => (IPEndPoint)TcpSocket.RemoteEndPoint;

		/// <summary>
		/// The TCP socket of the client.
		/// </summary>
		public Socket TcpSocket { get; }

		/// <summary>
		/// The UDP endpoint of the client.
		/// </summary>
		public EndPoint UdpEndPoint { get; private set; }

		/// <summary>
		/// The encryption key used for the sent and receied packets.
		/// </summary>
		public byte[] EncryptionKey { get; private set; }

		/// <summary>
		/// The boundary of the TCP sequence IDs.
		/// </summary>
		public byte SequenceIdBound { get; private set; }

		/// <summary>
		/// The timestamp of the connection's establishment.
		/// </summary>
		public long ConnectionStartTimestamp { get; private set; }

		private byte _sendSequenceId;
		private byte _receiveSequenceId;

		/// <summary>
		/// Creates a new instance of the client using the specified TCP socket.
		/// </summary>
		/// <param name="socket">The TCP socket of the client.</param>
		public DoubleServerClient(Socket socket) {
			TcpSocket = socket;
		}



		/// <summary>
		/// Updates the client's state to TCP authenticated.
		/// </summary>
		/// <param name="encryptionKey">The encryption key of the client.</param>
		/// <param name="usedKeys">The currently used UDP authentication keys.</param>
		/// <param name="udpAuthenticationKey">The UDP authentication key of this client.</param>
		public void TcpAuthenticated(byte[] encryptionKey, ICollection<ulong> usedKeys, out ulong udpAuthenticationKey) {
			State = DoubleServer.ClientState.UdpAuthenticating;
			EncryptionKey = encryptionKey;
			SequenceIdBound = (byte)Random.Next(128, 256);
			do {
				Random.NextBytes(RandomBytes);
				udpAuthenticationKey = BitConverter.ToUInt64(RandomBytes, 0);
			} while (usedKeys.Contains(udpAuthenticationKey));
			ConnectionStartTimestamp = DoubleProtocol.TimeMillis;
		}

		/// <summary>
		/// Updates the client's state to UDP authenticated.
		/// </summary>
		/// <param name="endPoint">The endpoint of the UDP socket.</param>
		public void UdpAuthenticated(EndPoint endPoint) {
			State = DoubleServer.ClientState.Authenticated;
			UdpEndPoint = endPoint;
		}

		/// <summary>
		/// Updates the client's state to disconnected.
		/// </summary>
		public void Disconnected() {
			State = DoubleServer.ClientState.Disconnected;
		}



		/// <summary>
		/// Calculates the next sequence ID.
		/// </summary>
		/// <returns>The next sequence ID.</returns>
		public byte NextSendSequenceId() {
			byte value = _sendSequenceId;
			if (++_sendSequenceId == SequenceIdBound) {
				_sendSequenceId = 0;
			}
			return value;
		}

		/// <summary>
		/// Checks whether the received sequence ID is valid.
		/// </summary>
		/// <param name="id">The received sequence ID.</param>
		/// <returns>Whether the sequence ID is the expected ID.</returns>
		public bool CheckReceiveSequenceId(byte id) {
			if (id != _receiveSequenceId) {
				return false;
			}
			if (++_receiveSequenceId == SequenceIdBound) {
				_receiveSequenceId = 0;
			}
			return true;
		}
	}
}
