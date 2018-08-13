using System;
using DoubleSocket.Utility;
using DoubleSocket.Utility.BitBuffer;

namespace DoubleSocket.Protocol {
	/// <summary>
	/// A utility class which helps the creation and validation of UDP packets.
	/// </summary>
	public static class UdpHelper {
		/// <summary>
		/// Writes the calculated prefix and the specified payload to the specified buffer.
		/// </summary>
		/// <param name="buffer">The buffer to use.</param>
		/// <param name="connectionStartTimestamp">The timestamp of the connection's establishment.</param>
		/// <param name="payloadwriter">The action which writes the payload to a buffer.</param>
		public static void WritePrefix(BitBuffer buffer, long connectionStartTimestamp, Action<BitBuffer> payloadwriter) {
			buffer.AdvanceWriter(32);
			buffer.WriteBits(DoubleProtocol.PacketTimestamp(connectionStartTimestamp), 20);
			payloadwriter(buffer);
			uint crc = Crc32.Get(buffer.Array, 4, buffer.Size - 4);
			byte[] array = buffer.Array;
			array[0] = (byte)crc;
			array[1] = (byte)(crc >> 8);
			array[2] = (byte)(crc >> 16);
			array[3] = (byte)(crc >> 24);
		}

		/// <summary>
		/// Checks whether the packet is valid.
		/// </summary>
		/// <param name="buffer">The buffer in which the packet is stored.</param>
		/// <param name="packetTimestamp">The packet's timestamp.</param>
		/// <returns>Whether the packet is valid.</returns>
		public static bool PrefixCheck(BitBuffer buffer, out uint packetTimestamp) {
			if (buffer.Size <= 7 || buffer.ReadUInt() != Crc32.Get(buffer.Array, buffer.Offset, buffer.Size)) {
				packetTimestamp = 0;
				return false;
			}
			packetTimestamp = (uint)buffer.ReadBits(20);
			return true;
		}
	}
}
