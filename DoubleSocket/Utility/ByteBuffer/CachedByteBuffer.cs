using System;
using System.Collections.Generic;
using System.Linq;

namespace DoubleSocket.Utility.ByteBuffer {
	public class CachedByteBuffer : ByteBuffer, IDisposable {
		public const int MaxCachedBufferCount = 100;
		private static readonly HashSet<CachedByteBuffer> Buffers = new HashSet<CachedByteBuffer>();

		public static CachedByteBuffer Get() {
			return Buffers.Count == 0 ? new CachedByteBuffer() : Buffers.First();
		}



		public new byte[] Array => base.Array;

		private CachedByteBuffer() {
			base.Array = new byte[0]; //TODO set the length
		}



		public void Dispose() {
			if (Buffers.Count < MaxCachedBufferCount) {
				WriteIndex = 0;
				ReadIndex = 0;
				Buffers.Add(this);
			}
		}
	}
}

