namespace DoubleSocket.Utility.ByteBuffer {
	public class MutableByteBuffer : ByteBuffer {
		public new byte[] Array { get => base.Array; set => base.Array = value; }

		public MutableByteBuffer() {
		}

		public MutableByteBuffer(byte[] array) {
			Array = array;
		}

		public MutableByteBuffer(int length) {
			Array = new byte[length];
		}
	}
}
