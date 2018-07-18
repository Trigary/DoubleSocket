using System;
using DoubleSocket.Utility;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Protocol {
	public static class UdpHelper {
		public static void WriteCrc(ByteBuffer buffer, Action<ByteBuffer> packetWriter) {
			buffer.WriteIndex = 4;
			packetWriter(buffer);
			uint crc = Crc32.Get(buffer.Array, 4, buffer.WriteIndex);
			byte[] array = buffer.Array;
			array[0] = (byte)crc;
			array[1] = (byte)(crc >> 8);
			array[2] = (byte)(crc >> 16);
			array[3] = (byte)(crc >> 24);
		}

		public static bool CrcCheck(ByteBuffer buffer) {
			return buffer.ReadUInt() == Crc32.Get(buffer.Array, 4, buffer.WriteIndex);
		}
	}
}
