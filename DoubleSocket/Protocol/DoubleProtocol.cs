using System;

namespace DoubleSocket.Protocol {
	/// <summary>
	/// A class containing general constants and utility methods.
	/// </summary>
	public static class DoubleProtocol {
		/// <summary>
		/// The size of the shared TCP and UDP library-level send buffers.
		/// The value is this due to the maximal length of TCP packets.
		/// </summary>
		public const int SendBufferArraySize = ushort.MaxValue + 2;

		/// <summary>
		/// The size of the TCP library-level receive buffers.
		/// The value is this, since the packet's length prefix's 2 bytes can store at most this great a value.
		/// </summary>
		public const int TcpBufferArraySize = ushort.MaxValue + 2;

		/// <summary>
		/// The size of the TCP socket-level send and receive buffers.
		/// </summary>
		public const int TcpSocketBufferSize = 3 * TcpBufferArraySize;

		/// <summary>
		/// The size of the UDP library-level receive buffers.
		/// The value is based on the following article: https://gafferongames.com/post/packet_fragmentation_and_reassembly/
		/// </summary>
		public const int UdpBufferArraySize = 1536;

		/// <summary>
		/// The size of the UDP socket-level send and receive buffers.
		/// </summary>
		public const int UdpSocketBufferSize = 3 * UdpBufferArraySize;

		/// <summary>
		/// The socket-level timeout for the UDP and TCP socket operations.
		/// </summary>
		public const int SocketOperationTimeout = 100;



		/// <summary>
		/// The current time since the epoch in milliseconds.
		/// </summary>
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		/// <summary>
		/// Calculates the current packet timestamp for a specific connection.
		/// </summary>
		/// <param name="connectionStartTimestamp">The timestamp of the connection's establishment.</param>
		/// <returns></returns>
		public static ushort PacketTimestamp(long connectionStartTimestamp) {
			return (ushort)((TimeMillis - connectionStartTimestamp) / 10);
		}



		/// <summary>
		/// Calculates how much it took for the packet to arrive here. Since the "counter" overflows after 10.92 minutes,
		/// the value returned by this method may be completely off if the packet was received at least as much later.
		/// </summary>
		/// <param name="connectionStartTimestamp">The timestamp of the connection's establishment.</param>
		/// <param name="packetTimestamp">The packet's timestamp, this is passed as a parameter to the UDP handler.</param>
		/// <returns></returns>
		public static long TripTime(long connectionStartTimestamp, ushort packetTimestamp) {
			int currentPacketTimestamp = PacketTimestamp(connectionStartTimestamp);
			int diff = currentPacketTimestamp - packetTimestamp;
			if (diff < 0) {
				diff += ushort.MaxValue;
			}
			return diff * 10;
		}



		/// <summary>
		/// Determines whether the packet which was just received is newer than the previously newest packet.
		/// This method is only reliable if packets are sent at least every minute, assuming a 0% packet loss.
		/// </summary>
		/// <param name="previousNewestPacketTimestamp">The timestamp of the connection's establishment.</param>
		/// <param name="packetTimestamp">The packet's timestamp, this is passed as a parameter to the UDP handler.</param>
		/// <returns></returns>
		public static bool IsPacketNewest(ref ushort previousNewestPacketTimestamp, ushort packetTimestamp) {
			if (IsPacketTimestampInThreshold(packetTimestamp, previousNewestPacketTimestamp)
				|| IsPacketTimestampInThreshold(packetTimestamp + ushort.MaxValue, previousNewestPacketTimestamp)) {
				previousNewestPacketTimestamp = packetTimestamp;
				return true;
			}
			return false;
		}

		private static bool IsPacketTimestampInThreshold(int newTimestamp, int previousTimestamp) {
			return newTimestamp > previousTimestamp && newTimestamp - (ushort.MaxValue / 10) < previousTimestamp;
		}
	}
}
