namespace DoubleSocket.Utility.BitBuffer {
	/// <summary>
	/// A byte buffer whose underlying array can be changed.
	/// </summary>
	public class MutableBitBuffer : BitBuffer {
		/// <summary>
		/// Create a new instance with no underlying array.
		/// </summary>
		public MutableBitBuffer() {
		}

		/// <summary>
		/// Create a new instance with the specified underlying array.
		/// </summary>
		/// <param name="array">The array to use as the underlying array of the buffer.</param>
		public MutableBitBuffer(byte[] array) {
			Array = array;
		}

		/// <summary>
		/// Create a new instance with an underlying (byte) array of the specified size.
		/// </summary>
		/// <param name="size">The size of the underlying (byte) array wich will be created.</param>
		public MutableBitBuffer(int size) {
			Array = new byte[size];
		}



		/// <summary>
		/// Reinitializes the BitBuffer with the specified array as its underlying array.
		/// Sets the write index to the array's length and clears all other state of the buffer.
		/// </summary>
		/// <param name="array">The new array to use.</param>
		public void SetContents(byte[] array) {
			Array = array;
			SetState(array.Length, 0);
		}

		/// <summary>
		/// Reinitializes the BitBuffer with the specified array as its underlying array.
		/// Sets the read and write indexes based on the specified parameters.
		/// </summary>
		/// <param name="array">The new array to use.</param>
		/// <param name="offset">The offset of the contents in the array.</param>
		/// <param name="size">The size of the contents in the array.</param>
		public void SetContents(byte[] array, int offset, int size) {
			Array = array;
			SetState(offset + size, offset);
		}



		/// <summary>
		/// Clears all state of the buffer, this including setting the write index to 0.
		/// </summary>
		public void ClearContents() {
			SetState(0, 0);
		}

		/// <summary>
		/// Clears all state of the buffer (also setting the write index to 0)
		/// and sets a new array to use as the underlying array.
		/// </summary>
		/// <param name="array">The new array to use.</param>
		public void ClearContents(byte[] array) {
			Array = array;
			SetState(0, 0);
		}
	}
}
