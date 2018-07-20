﻿using System;
using DoubleSocket.Utility;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Protocol {
	public static class UdpHelper {
		public static void WritePrefix(ByteBuffer buffer, long connectionStartTimestamp, Action<ByteBuffer> payloadwriter) {
			buffer.WriteIndex = 4;
			buffer.Write((ushort)(DoubleProtocol.TimeMillis - connectionStartTimestamp));
			payloadwriter(buffer);
			uint crc = Crc32.Get(buffer.Array, 4, buffer.WriteIndex);
			byte[] array = buffer.Array;
			array[0] = (byte)crc;
			array[1] = (byte)(crc >> 8);
			array[2] = (byte)(crc >> 16);
			array[3] = (byte)(crc >> 24);
		}

		public static bool PrefixCheck(ByteBuffer buffer, out ushort packetTimestamp) {
			if (buffer.ReadUInt() != Crc32.Get(buffer.Array, 4, buffer.WriteIndex)) {
				packetTimestamp = 0;
				return false;
			}
			packetTimestamp = buffer.ReadUShort();
			return true;
		}
	}
}
