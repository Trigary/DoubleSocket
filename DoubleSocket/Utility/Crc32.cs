﻿namespace DoubleSocket.Utility {
	public static class Crc32 { //Source: https://github.com/force-net/Crc32.NET
		private static readonly uint[] Table = new uint[16 * 256];

		static Crc32() {
			const uint poly = 0xedb88320u;
			for (uint i = 0; i < 256; i++) {
				uint result = i;
				for (int j = 0; j < 16; j++) {
					for (int k = 0; k < 8; k++) {
						result = (result & 1) == 1 ? poly ^ (result >> 1) : (result >> 1);
					}
					Table[(j * 256) + i] = result;
				}
			}
		}



		public static uint Get(byte[] input, int offset, int length) {
			uint crcLocal = uint.MaxValue;

			while (length >= 16) {
				uint a = Table[(3 * 256) + input[offset + 12]]
					^ Table[(2 * 256) + input[offset + 13]]
					^ Table[(1 * 256) + input[offset + 14]]
					^ Table[(0 * 256) + input[offset + 15]];

				uint b = Table[(7 * 256) + input[offset + 8]]
					^ Table[(6 * 256) + input[offset + 9]]
					^ Table[(5 * 256) + input[offset + 10]]
					^ Table[(4 * 256) + input[offset + 11]];

				uint c = Table[(11 * 256) + input[offset + 4]]
					^ Table[(10 * 256) + input[offset + 5]]
					^ Table[(9 * 256) + input[offset + 6]]
					^ Table[(8 * 256) + input[offset + 7]];

				uint d = Table[(15 * 256) + ((byte)crcLocal ^ input[offset])]
					^ Table[(14 * 256) + ((byte)(crcLocal >> 8) ^ input[offset + 1])]
					^ Table[(13 * 256) + ((byte)(crcLocal >> 16) ^ input[offset + 2])]
					^ Table[(12 * 256) + ((crcLocal >> 24) ^ input[offset + 3])];

				crcLocal = d ^ c ^ b ^ a;
				offset += 16;
				length -= 16;
			}

			while (--length >= 0) {
				crcLocal = Table[(byte)(crcLocal ^ input[offset++])] ^ crcLocal >> 8;
			}

			return ~crcLocal;
		}
	}
}
