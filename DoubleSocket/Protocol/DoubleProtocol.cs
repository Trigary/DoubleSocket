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
		/// The exclusive maximum value of the UDP packet timestamps.
		/// </summary>
		public const uint MaxPacketTimestampValue = 1 << 20;

		/// <summary>
		/// The bitmask which only lets through valid UDP packet timestamp values.
		/// </summary>
		public const uint PacketTimestampValueMask = 0b11111111111111111111;



		/// <summary>
		/// Determines whether the current CLR is Mono or not.
		/// </summary>
		public static bool IsMonoClr { get; } = Type.GetType("Mono.Runtime") != null;



		/// <summary>
		/// The current time since the epoch in milliseconds.
		/// </summary>
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		/// <summary>
		/// Calculates the current packet timestamp for a specific connection.
		/// </summary>
		/// <param name="connectionStartTimestamp">The timestamp of the connection's establishment.</param>
		/// <returns></returns>
		public static uint PacketTimestamp(long connectionStartTimestamp) {
			return (uint)(TimeMillis - connectionStartTimestamp) & PacketTimestampValueMask;
		}



		/// <summary>
		/// Calculates how much it took for the packet to arrive here. Since the "counter" wraps around after ~17.5 minutes,
		/// the value returned by this method may be completely off if the packet was received at least as much later.
		/// </summary>
		/// <param name="connectionStartTimestamp">The timestamp of the connection's establishment.</param>
		/// <param name="packetTimestamp">The packet's timestamp, this is passed as a parameter to the UDP handler.</param>
		/// <returns></returns>
		public static uint TripTime(long connectionStartTimestamp, uint packetTimestamp) {
			return (PacketTimestamp(connectionStartTimestamp) - packetTimestamp) & PacketTimestampValueMask;
		}



		/// <summary>
		/// Determines whether the packet which was just received is newer than the previously newest packet.
		/// This method is only reliable if packets are sent at least every minute, assuming a 0% packet loss.
		/// </summary>
		/// <param name="previousNewestPacketTimestamp">The timestamp of the connection's establishment.</param>
		/// <param name="packetTimestamp">The packet's timestamp, this is passed as a parameter to the UDP handler.</param>
		/// <returns></returns>
		public static bool IsPacketNewest(ref uint previousNewestPacketTimestamp, uint packetTimestamp) {
			if (IsPacketTimestampInThreshold(packetTimestamp, previousNewestPacketTimestamp)
				|| IsPacketTimestampInThreshold((long)packetTimestamp + MaxPacketTimestampValue, previousNewestPacketTimestamp)) {
				previousNewestPacketTimestamp = packetTimestamp;
				return true;
			}
			return false;
		}

		private static bool IsPacketTimestampInThreshold(long newTimestamp, long previousTimestamp) {
			return newTimestamp > previousTimestamp && newTimestamp - (MaxPacketTimestampValue / 10) < previousTimestamp;
		}
	}
}
