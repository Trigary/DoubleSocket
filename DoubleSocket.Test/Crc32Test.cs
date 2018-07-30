using System.Text;
using DoubleSocket.Utility;
using NUnit.Framework;

namespace DoubleSocket.Test {
	/// <summary>
	/// Tests the CRC-32 algorithm.
	/// </summary>
	[TestFixture]
	public class Crc32Test {
		private const string First = "H";
		private const string Second = "Hello";
		private const string Third = "Hello dear human/nonhuman being looking at this!";

		[Test]
		public void Test() {
			byte[] first = GetBytes(First);
			byte[] second = GetBytes(Second);
			byte[] third = GetBytes(Third);

			Assert.AreEqual(Crc32.Get(first, 0, first.Length), Crc32.Get(first, 0, first.Length));
			Assert.AreEqual(Crc32.Get(second, 0, second.Length), Crc32.Get(second, 0, second.Length));
			Assert.AreEqual(Crc32.Get(third, 0, third.Length), Crc32.Get(third, 0, third.Length));

			Assert.AreEqual(Crc32.Get(first, 0, first.Length), Crc32.Get(second, 0, first.Length));
			Assert.AreEqual(Crc32.Get(second, 0, second.Length), Crc32.Get(third, 0, second.Length));
			Assert.AreEqual(Crc32.Get(second, 3, 2), Crc32.Get(third, 32, 2));

			Assert.AreNotEqual(Crc32.Get(first, 0, first.Length), Crc32.Get(second, 0, second.Length));
			Assert.AreNotEqual(Crc32.Get(first, 0, first.Length), Crc32.Get(third, 0, third.Length));
			Assert.AreNotEqual(Crc32.Get(second, 0, second.Length), Crc32.Get(third, 0, third.Length));
			
			Assert.AreNotEqual(Crc32.Get(second, 0, second.Length), Crc32.Get(second, 0, first.Length));
			Assert.AreNotEqual(Crc32.Get(third, 0, third.Length), Crc32.Get(third, 0, second.Length));
		}



		private static byte[] GetBytes(string value) {
			return Encoding.ASCII.GetBytes(value);
		}
	}
}
