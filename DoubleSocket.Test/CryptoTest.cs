using System;
using DoubleSocket.Utility.KeyCrypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DoubleSocket.Test {
	[TestClass]
	public class CryptoTest {
		public const int KeyLength = 16;
		public const int KeyCount = 100;
		public const int MaxDataLength = 1300;
		public const int DataPerKeyCount = 100;

		[TestMethod]
		public void TestFixedKeyCrypto() {
			Random random = new Random();
			byte[] key = new byte[KeyLength];

			for (int length = 0; length < KeyCount; length++) {
				random.NextBytes(key);
				FixedKeyCrypto crypto = new FixedKeyCrypto(key);
				byte[] original = new byte[length];

				for (int i = 0; i < DataPerKeyCount; i++) {
					random.NextBytes(original);
					byte[] encrypted = crypto.Encrypt(original, original.Length);
					byte[] decrypted = crypto.Decrypt(encrypted, encrypted.Length);
					CollectionAssert.AreEqual(original, decrypted);
				}
			}
		}

		[TestMethod]
		public void TestAnyKeyCrypto() {
			Random random = new Random();
			AnyKeyCrypto crypto = new AnyKeyCrypto();
			byte[] key = new byte[KeyLength];

			for (int length = 0; length < KeyCount; length++) {
				random.NextBytes(key);
				byte[] original = new byte[length];

				for (int i = 0; i < DataPerKeyCount; i++) {
					random.NextBytes(original);
					byte[] encrypted = crypto.Encrypt(key, original, original.Length);
					byte[] decrypted = crypto.Decrypt(key, encrypted, encrypted.Length);
					CollectionAssert.AreEqual(original, decrypted);
				}
			}
		}
	}
}
