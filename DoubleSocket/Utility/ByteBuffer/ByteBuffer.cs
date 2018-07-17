using System;

namespace DoubleSocket.Utility.ByteBuffer {
	public abstract class ByteBuffer { //TODO change to BitBuffer
		public byte[] Array { get; protected set; }
		public int WriteIndex { get; set; }
		public int ReadIndex { get; set; }



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

		public void Write(int value) {
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

		public void Write(byte[] value, int offset, int length) {
			Buffer.BlockCopy(value, offset, Array, WriteIndex, length);
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

		public int ReadInt() {
			return BitConverter.ToInt32(Array, ReadIndex);
		}

		public long ReadLong() {
			return BitConverter.ToInt64(Array, ReadIndex);
		}

		public float ReadFloat() {
			return BitConverter.ToSingle(Array, ReadIndex);
		}

		public double ReadDouble() {
			return BitConverter.ToSingle(Array, ReadIndex);
		}

		public string ReadString(int length) {
			char[] characters = new char[length];
			for (int i = 0; i < length; i++) {
				characters[i] = (char)ReadShort();
			}
			return new string(characters);
		}

		public byte[] ReadBytes(int length) {
			byte[] bytes = new byte[length];
			Buffer.BlockCopy(Array, ReadIndex, bytes, 0, length);
			return bytes;
		}

		public byte[] ReadBytes() {
			return ReadBytes(WriteIndex - ReadIndex);
		}
	}
}
