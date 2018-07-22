using System;
using DoubleSocket.Utility;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Protocol {
	public static class UdpHelper {
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
