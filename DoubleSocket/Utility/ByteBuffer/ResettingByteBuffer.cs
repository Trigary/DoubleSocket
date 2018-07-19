using System;

namespace DoubleSocket.Utility.ByteBuffer {
	/// <summary>
	/// A buffer with a fixed size. Disposing of the buffer will reset it, making it easy-to-use with using blocks.
	/// </summary>
	public class ResettingByteBuffer : ByteBuffer, IDisposable {
		/// <summary>
		/// The underlying array of the buffer.
		/// </summary>
		public new byte[] Array => base.Array;

		/// <summary>
		/// Create a new instance with an underlying array of the specified size.
		/// </summary>
		/// <param name="size">The size of the underlying array wich will be created.</param>
		public ResettingByteBuffer(int size) {
			base.Array = new byte[size];
		}



		/// <summary>
		/// Resets the buffer
		/// </summary>
		public void Dispose() {
			WriteIndex = 0;
			ReadIndex = 0;
		}
	}
}
