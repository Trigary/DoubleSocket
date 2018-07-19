using System;

namespace DoubleSocket.Utility.ByteBuffer {
	/// <summary>
	/// A buffer which stores bytes in a fixed-size underlying array.
	/// </summary>
	public abstract class ByteBuffer { //TODO change to BitBuffer
		/// <summary>
		/// The underlying array of the buffer.
		/// </summary>
		public byte[] Array { get; protected set; }

		/// <summary>
		/// The current write index of the buffer. The next written byte will occupy this index.
		/// </summary>
		public int WriteIndex { get; set; }

		/// <summary>
		/// The current read index of the buffer. The next read byte is the one at this index.
		/// </summary>
		public int ReadIndex { get; set; }



		/// <summary>
		/// Returns a new array with the same contents at this buffer's underlying array.
		/// </summary>
		/// <returns>A clone of the buffer's array.</returns>
		public byte[] CloneArray() {
			byte[] array = new byte[WriteIndex];
			Buffer.BlockCopy(Array, 0, array, 0, WriteIndex);
			return array;
		}



		public void Write(byte value) {
			Array[WriteIndex++] = value;
		}

		public void Write(short value) {
			Array[WriteIndex++] = (byte)value;
			Array[WriteIndex++] = (byte)(value >> 8);
		}

		public void Write(ushort value) {
			Array[WriteIndex++] = (byte)value;
			Array[WriteIndex++] = (byte)(value >> 8);
		}

		public void Write(int value) {
			Array[WriteIndex++] = (byte)value;
			Array[WriteIndex++] = (byte)(value >> 8);
			Array[WriteIndex++] = (byte)(value >> 16);
			Array[WriteIndex++] = (byte)(value >> 24);
		}

		public void Write(uint value) {
			Array[WriteIndex++] = (byte)value;
			Array[WriteIndex++] = (byte)(value >> 8);
			Array[WriteIndex++] = (byte)(value >> 16);
			Array[WriteIndex++] = (byte)(value >> 24);
		}

		public void Write(long value) {
			for (int i = 0; i < 8; i++) {
				Array[WriteIndex++] = (byte)(value >> i * 8);
			}
		}

		public void Write(ulong value) {
			for (int i = 0; i < 8; i++) {
				Array[WriteIndex++] = (byte)(value >> i * 8);
			}
		}

		public void Write(float value) {
			byte[] bytes = BitConverter.GetBytes(value);
			Buffer.BlockCopy(bytes, 0, Array, WriteIndex, 4);
			WriteIndex += 4;
		}

		public void Write(double value) {
			Write(BitConverter.DoubleToInt64Bits(value));
		}

		public void Write(string value) {
			foreach (char character in value) {
				Write((short)character);
			}
		}

		public void Write(byte[] value, int offset, int count) {
			Buffer.BlockCopy(value, offset, Array, WriteIndex, count);
		}

		public void Write(byte[] value) {
			Write(value, 0, value.Length);
		}



		public byte ReadByte() {
			return Array[ReadIndex];
		}

		public short ReadShort() {
			return BitConverter.ToInt16(Array, ReadIndex);
		}

		public ushort ReadUShort() {
			return BitConverter.ToUInt16(Array, ReadIndex);
		}

		public int ReadInt() {
			return BitConverter.ToInt32(Array, ReadIndex);
		}

		public uint ReadUInt() {
			return BitConverter.ToUInt32(Array, ReadIndex);
		}

		public long ReadLong() {
			return BitConverter.ToInt64(Array, ReadIndex);
		}

		public ulong ReadULong() {
			return BitConverter.ToUInt64(Array, ReadIndex);
		}

		public float ReadFloat() {
			return BitConverter.ToSingle(Array, ReadIndex);
		}

		public double ReadDouble() {
			return BitConverter.ToSingle(Array, ReadIndex);
		}

		public string ReadString(int count) {
			char[] characters = new char[count];
			for (int i = 0; i < count; i++) {
				characters[i] = (char)ReadShort();
			}
			return new string(characters);
		}

		public byte[] ReadBytes(int count) {
			byte[] bytes = new byte[count];
			Buffer.BlockCopy(Array, ReadIndex, bytes, 0, count);
			return bytes;
		}

		public byte[] ReadBytes() {
			return ReadBytes(WriteIndex - ReadIndex);
		}
	}
}
