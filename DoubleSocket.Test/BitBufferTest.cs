using System;
using DoubleSocket.Utility.BitBuffer;
using NUnit.Framework;

namespace DoubleSocket.Test {
	/// <summary>
	/// Tests the BitBuffer class.
	/// </summary>
	[TestFixture]
	public class BitBufferTest {
		public const int RunCount = 10;
		public const int BufferableCount = 10000;
		private static readonly Random Random = new Random();
		private static readonly IBufferable[] Bufferables = {
			new BufferableBool(), new BufferableBool(), new BufferableBool(), new BufferableBool(), //Increase chances
			new BufferableByte(), new BufferableSByte(), new BufferableChar(), new BufferableInt(),
			new BufferableLong(), new BufferableULong(), new BufferableFloat(), new BufferableDouble()

		};

		[Test]
		public void Test() {
			ResettingBitBuffer buffer = new ResettingBitBuffer(BufferableCount * 8);
			for (int run = 0; run < RunCount; run++) {
				using (buffer) {
					IBufferable[] bufferables = new IBufferable[BufferableCount];
					object[] values = new object[bufferables.Length];

					for (int i = 0; i < bufferables.Length; i++) {
						bufferables[i] = Bufferables[Random.Next(Bufferables.Length)];
						values[i] = bufferables[i].GenerateValue();
						bufferables[i].Write(buffer, values[i]);
					}

					for (int i = 0; i < bufferables.Length; i++) {
						Assert.AreEqual(bufferables[i].Read(buffer), values[i], $"In the {run + 1}. on the {i + 1}. iteration");
					}
				}
			}
		}



		private interface IBufferable {
			object GenerateValue();
			void Write(BitBuffer buffer, object value);
			object Read(BitBuffer buffer);
		}

		private class BufferableBool : IBufferable {
			public object GenerateValue() {
				return Random.Next(2) == 0;
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((bool)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadBool();
			}
		}

		private class BufferableByte : IBufferable {
			public object GenerateValue() {
				return (byte)Random.Next(byte.MaxValue + 1);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((byte)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadByte();
			}
		}

		private class BufferableSByte : IBufferable {
			public object GenerateValue() {
				return (sbyte)Random.Next(sbyte.MinValue, sbyte.MaxValue + 1);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((sbyte)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadSByte();
			}
		}

		private class BufferableChar : IBufferable {
			public object GenerateValue() {
				return (char)Random.Next(char.MaxValue + 1);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((char)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadChar();
			}
		}

		private class BufferableInt : IBufferable {
			public object GenerateValue() {
				return Random.Next();
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((int)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadInt();
			}
		}

		private class BufferableLong : IBufferable {
			public object GenerateValue() {
				byte[] bytes = new byte[8];
				Random.NextBytes(bytes);
				return BitConverter.ToInt64(bytes, 0);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((long)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadLong();
			}
		}

		private class BufferableULong : IBufferable {
			public object GenerateValue() {
				byte[] bytes = new byte[8];
				Random.NextBytes(bytes);
				return BitConverter.ToUInt64(bytes, 0);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((ulong)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadULong();
			}
		}

		private class BufferableFloat : IBufferable {
			public object GenerateValue() {
				byte[] bytes = new byte[4];
				Random.NextBytes(bytes);
				return BitConverter.ToSingle(bytes, 0);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((float)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadFloat();
			}
		}

		private class BufferableDouble : IBufferable {
			public object GenerateValue() {
				byte[] bytes = new byte[8];
				Random.NextBytes(bytes);
				return BitConverter.ToDouble(bytes, 0);
			}

			public void Write(BitBuffer buffer, object value) {
				buffer.Write((double)value);
			}

			public object Read(BitBuffer buffer) {
				return buffer.ReadDouble();
			}
		}
	}
}
