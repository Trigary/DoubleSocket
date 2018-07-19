using System;
using System.Collections.Generic;
using System.Linq;

namespace DoubleSocket.Utility.ByteBuffer {
	/// <summary>
	/// A buffer with a fixed size. Instances are stored internally and reused.
	/// </summary>
	public class CachedByteBuffer : ByteBuffer, IDisposable {
		public const int MaxCachedBufferCount = 20;
		private static readonly HashSet<CachedByteBuffer> Buffers = new HashSet<CachedByteBuffer>();

		/// <summary>
		/// Get a buffer instance. This mehod will create a new buffer if there are none available.
		/// </summary>
		/// <returns>A buffer.</returns>
		public static CachedByteBuffer Get() {
			lock (Buffers) {
				return Buffers.Count == 0 ? new CachedByteBuffer() : Buffers.First();
			}
		}



		/// <summary>
		/// The underlying array of the buffer.
		/// </summary>
		public new byte[] Array => base.Array;

		private CachedByteBuffer() {
			lock (Buffers) {
				base.Array = new byte[ushort.MaxValue];
			}
		}



		/// <summary>
		/// Dispose of the buffer, returning it into the internal storage. It mustn't be used after this point.
		/// </summary>
		public void Dispose() {
			lock (Buffers) {
				if (Buffers.Count < MaxCachedBufferCount) {
					WriteIndex = 0;
					ReadIndex = 0;
					Buffers.Add(this);
				}
			}
		}
	}
}

