using System;

namespace DoubleSocket.Utility.BitBuffer {
	/// <summary>
	/// A buffer with a fixed size. Disposing of the buffer will reset it, making it easy-to-use with 'using' blocks.
	/// </summary>
	public class ResettingBitBuffer : BitBuffer, IDisposable {
		/// <summary>
		/// Create a new instance with an underlying (byte) array of the specified size.
		/// </summary>
		/// <param name="size">The size of the underlying (byte) array wich will be created.</param>
		public ResettingBitBuffer(int size) {
			Array = new byte[size];
		}



		/// <summary>
		/// Resets the buffer.
		/// </summary>
		public void Dispose() {
			SetState(0, 0);
		}
	}
}
