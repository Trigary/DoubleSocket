namespace DoubleSocket.Utility.ByteBuffer {
	/// <summary>
	/// A byte buffer whose underlying array can be changed.
	/// </summary>
	public class MutableByteBuffer : ByteBuffer {
		/// <summary>
		/// The (mutable) underlying array of the buffer.
		/// </summary>
		public new byte[] Array { get => base.Array; set => base.Array = value; }

		/// <summary>
		/// Create a new instance with no underlying array.
		/// </summary>
		public MutableByteBuffer() {
		}

		/// <summary>
		/// Create a new instance with the specified underlying array.
		/// </summary>
		/// <param name="array">The array to use as the underlying array of the buffer.</param>
		public MutableByteBuffer(byte[] array) {
			Array = array;
		}

		/// <summary>
		/// Create a new instance with an underlying array of the specified size.
		/// </summary>
		/// <param name="size">The size of the underlying array wich will be created.</param>
		public MutableByteBuffer(int size) {
			Array = new byte[size];
		}
	}
}
