using System;
using DoubleSocket.Utility;
using DoubleSocket.Utility.ByteBuffer;

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
		public static void WritePrefix(ByteBuffer buffer, long connectionStartTimestamp, Action<ByteBuffer> payloadwriter) {
			buffer.WriteIndex = 4;
			buffer.Write(DoubleProtocol.PacketTimestamp(connectionStartTimestamp));
			payloadwriter(buffer);
			uint crc = Crc32.Get(buffer.Array, 4, buffer.WriteIndex - 4);
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
		public static bool PrefixCheck(ByteBuffer buffer, out ushort packetTimestamp) {
			if (buffer.BytesLeft <= 6 || buffer.ReadUInt() != Crc32.Get(buffer.Array, buffer.ReadIndex, buffer.BytesLeft)) {
				packetTimestamp = 0;
				return false;
			}
			packetTimestamp = buffer.ReadUShort();
			return true;
		}
	}
}
