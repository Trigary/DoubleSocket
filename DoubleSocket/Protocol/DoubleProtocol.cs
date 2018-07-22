using System;

namespace DoubleSocket.Protocol {
	public static class DoubleProtocol {
		public const int TcpBufferArraySize = ushort.MaxValue + 2;
		public const int TcpSocketBufferSize = 3 * TcpBufferArraySize;
		public const int UdpBufferArraySize = 1536; //https://gafferongames.com/post/packet_fragmentation_and_reassembly/
		public const int UdpSocketBufferSize = 3 * UdpBufferArraySize;
		public const int SocketOperationTimeout = 100;



		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		public static ushort PacketTimestamp(long connectionStartTimestamp) {
			return (ushort)((TimeMillis - connectionStartTimestamp) / 10);
		}



		public static long TripTime(long connectionStartTimestamp, ushort packetTimestamp) {
			int currentPacketTimestamp = PacketTimestamp(connectionStartTimestamp);
			int diff = currentPacketTimestamp - packetTimestamp;
			if (diff < 0) {
				diff += ushort.MaxValue;
			}
			return diff * 10;
		}



		public static bool IsPacketNewest(ref ushort previousNewestPacketTimestamp, ushort packetTimestamp) {
			if ((packetTimestamp > previousNewestPacketTimestamp
					&& !(IsInFirstQuarter(previousNewestPacketTimestamp) && IsInLastQuarter(packetTimestamp)))
				|| (IsInFirstQuarter(packetTimestamp) && IsInLastQuarter(previousNewestPacketTimestamp))) {
				previousNewestPacketTimestamp = packetTimestamp;
				return true;
			}
			return false;
		}

		private static bool IsInFirstQuarter(ushort packetTimestamp) {
			return packetTimestamp < ushort.MaxValue / 4;
		}

		private static bool IsInLastQuarter(ushort packetTimestamp) {
			return packetTimestamp > (ushort.MaxValue / 4) * 3;
		}
	}
}
