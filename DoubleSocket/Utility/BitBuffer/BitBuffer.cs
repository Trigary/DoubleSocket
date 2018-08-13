using System;

namespace DoubleSocket.Utility.BitBuffer {
	/// <summary>
	/// A buffer which stores bits in a fixed-size underlying array.
	/// </summary>
	public abstract class BitBuffer {
		private static readonly uint[] BitMasks = {1, 3, 7, 15, 31, 63, 127, 255};

		private int _writeIndex;
		private int _readIndex;
		private int _bitsLeftInWriteByte = 8;
		private int _bitsLeftInReadByte = 8;

		/// <summary>
		/// The underlying array of the buffer.
		/// </summary>
		public byte[] Array { get; protected set; }

		/// <summary>
		/// The count of remaining (fully or only partially written) bytes in the buffer.
		/// Can be used in combination with ReadBytes to read all remaining bytes in the buffer.
		/// </summary>
		public int StartedBytesLeft => _writeIndex - _readIndex + ((_bitsLeftInReadByte - _bitsLeftInWriteByte + 7) / 8);

		/// <summary>
		/// The count of remaining written bits in the buffer.
		/// </summary>
		public int TotalBitsLeft => (_writeIndex - _readIndex) * 8 + _bitsLeftInReadByte - _bitsLeftInWriteByte;

		/// <summary>
		/// The offset of the remaining bytes in the buffer. This is the same as the ReadIndex.
		/// Useful when copying the bytes from the array, since this way no written bits are left out.
		/// </summary>
		public int Offset => _readIndex;

		/// <summary>
		/// The size of the remaining bytes in the buffer.
		/// Useful when copying the bytes from the array, since this way no written bits are left out.
		/// </summary>
		public int Size => _writeIndex + (8 - _bitsLeftInWriteByte + 7) / 8 - Offset;



		/// <summary>
		/// Sets the whole state of this BitBuffer except the underlying array.
		/// </summary>
		/// <param name="writeIndex">The new write index.</param>
		/// <param name="readIndex">The new read index.</param>
		/// <param name="bitsLeftInWriteByte">The count of bits left in the new write byte.</param>
		/// <param name="bitsLeftInReadByte">The count of bits left in the new read byte.</param>
		protected void SetState(int writeIndex, int readIndex, int bitsLeftInWriteByte = 8, int bitsLeftInReadByte = 8) {
			_writeIndex = writeIndex;
			_readIndex = readIndex;
			_bitsLeftInWriteByte = bitsLeftInWriteByte;
			_bitsLeftInReadByte = bitsLeftInReadByte;
		}



		private static void AdvanceIndex(int bitCount, ref int bitIndex, ref int byteIndex) {
			bitIndex -= bitCount;
			if (bitIndex <= 0) {
				int full = (bitIndex / 8) - 1;
				bitIndex -= full * 8;
				byteIndex -= full;
			} else if (bitIndex > 8) {
				int full = (bitIndex - 1) / 8;
				bitIndex -= full * 8;
				byteIndex -= full;
			}
		}

		/// <summary>
		/// Increments the written-bits-counter by the specified amount of bits.
		/// Use with a negative bit count to decrement instead.
		/// </summary>
		/// <param name="bitCount">The size of the increment.</param>
		public void AdvanceWriter(int bitCount) {
			AdvanceIndex(bitCount, ref _bitsLeftInWriteByte, ref _writeIndex);
		}

		/// <summary>
		/// Increments the read-bits-counter by the specified amount of bits.
		/// Use with a negative bit count to decrement instead.
		/// </summary>
		/// <param name="bitCount">The size of the increment.</param>
		public void AdvanceReader(int bitCount) {
			AdvanceIndex(bitCount, ref _bitsLeftInReadByte, ref _readIndex);
		}



		/// <summary>
		/// Writes a part of the specified value into the buffer.
		/// </summary>
		/// <param name="value">The value to (fully or partially) write.</param>
		/// <param name="bitCount">The count of bits to write.</param>
		public void WriteBits(ulong value, int bitCount) {
			if (_bitsLeftInWriteByte == 8) {
				Array[_writeIndex] = 0;
			}
			while (true) {
				Array[_writeIndex] |= (byte)(value << (8 - _bitsLeftInWriteByte));
				if (_bitsLeftInWriteByte == bitCount) {
					_writeIndex++;
					_bitsLeftInWriteByte = 8;
					break;
				} else if (_bitsLeftInWriteByte > bitCount) {
					_bitsLeftInWriteByte -= bitCount;
					break;
				} else {
					value >>= _bitsLeftInWriteByte;
					bitCount -= _bitsLeftInWriteByte;
					Array[++_writeIndex] = 0;
					_bitsLeftInWriteByte = 8;
				}
			}
		}

		/// <summary>
		/// Reads the specified count of bits from the buffer.
		/// </summary>
		/// <param name="bitCount">The count of bits to read.</param>
		/// <returns>A value containing the read bits.</returns>
		public ulong ReadBits(int bitCount) {
			ulong value = 0;
			int readCount = 0;
			while (true) {
				ulong read = Array[_readIndex] & ((bitCount >= 8 ? 255 : BitMasks[(bitCount - 1)]) << (8 - _bitsLeftInReadByte));
				int deltaPosition = readCount + _bitsLeftInReadByte - 8;
				if (deltaPosition > 0) {
					read <<= deltaPosition;
				} else if (deltaPosition < 0) {
					read >>= -deltaPosition;
				}
				value |= read;

				if (_bitsLeftInReadByte == bitCount) {
					_readIndex++;
					_bitsLeftInReadByte = 8;
					break;
				} else if (_bitsLeftInReadByte > bitCount) {
					_bitsLeftInReadByte -= bitCount;
					break;
				} else {
					readCount += _bitsLeftInReadByte;
					bitCount -= _bitsLeftInReadByte;
					_readIndex++;
					_bitsLeftInReadByte = 8;
				}
			}
			return value;
		}



		public void Write(bool value) {
			WriteBits(value ? (uint)1 : 0, 1);
		}

		public void Write(sbyte value) {
			WriteBits((byte)value, 8);
		}

		public void Write(byte value) {
			WriteBits(value, 8);
		}

		public void Write(short value) {
			WriteBits((ushort)value, 16);
		}

		public void Write(ushort value) {
			WriteBits(value, 16);
		}

		public void Write(char value) {
			WriteBits(value, 16);
		}

		public void Write(int value) {
			WriteBits((uint)value, 32);
		}

		public void Write(uint value) {
			WriteBits(value, 32);
		}

		public void Write(long value) {
			WriteBits((ulong)value, 64);
		}

		public void Write(ulong value) {
			WriteBits(value, 64);
		}

		public void Write(float value) {
			//Thanks C# for not giving the attention singles deserve
			Write(BitConverter.GetBytes(value));
		}

		public void Write(double value) {
			Write(BitConverter.DoubleToInt64Bits(value));
		}

		public void Write(byte[] value, int offset, int count) {
			if (_bitsLeftInWriteByte == 8) {
				Buffer.BlockCopy(value, offset, Array, _writeIndex, count);
				_writeIndex += count;
			} else {
				for (int i = offset; i < count; i++) {
					Write(value[i]);
				}
			}
		}

		public void Write(byte[] value) {
			Write(value, 0, value.Length);
		}



		public bool ReadBool() {
			return ReadBits(1) == 1;
		}

		public sbyte ReadSByte() {
			return (sbyte)ReadBits(8);
		}

		public byte ReadByte() {
			return (byte)ReadBits(8);
		}

		public short ReadShort() {
			return (short)ReadBits(16);
		}

		public ushort ReadUShort() {
			return (ushort)ReadBits(16);
		}

		public char ReadChar() {
			return (char)ReadBits(16);
		}

		public int ReadInt() {
			return (int)ReadBits(32);
		}

		public uint ReadUInt() {
			return (uint)ReadBits(32);
		}

		public long ReadLong() {
			return (long)ReadBits(64);
		}

		public ulong ReadULong() {
			return ReadBits(64);
		}

		public float ReadFloat() {
			//Thanks C# for not giving the attention singles deserve
			return BitConverter.ToSingle(ReadBytes(4), 0);
		}

		public double ReadDouble() {
			return BitConverter.Int64BitsToDouble((long)ReadBits(64));
		}

		public byte[] ReadBytes(int count) {
			byte[] bytes = new byte[count];
			if (_bitsLeftInReadByte == 8) {
				Buffer.BlockCopy(Array, _readIndex, bytes, 0, count);
				_readIndex += count;
			} else {
				for (int i = 0; i < count; i++) {
					bytes[i] = ReadByte();
				}
			}
			return bytes;
		}
	}
}
