using System;
using System.IO;
using System.Security.Cryptography;

namespace DoubleSocket.Utility.KeyCrypto {
	public class FixedKeyCrypto : IDisposable {
		private readonly Aes _aes;

		public FixedKeyCrypto(byte[] encryptionKey) {
			_aes = Aes.Create();
			// ReSharper disable once PossibleNullReferenceException
			_aes.Mode = CipherMode.CBC;
			_aes.Padding = PaddingMode.PKCS7;
			_aes.Key = encryptionKey;
			_aes.IV = new byte[16];
		}



		public void Dispose() {
			_aes.Dispose();
		}

		public byte[] Encrypt(byte[] data, int length) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform encryptor = _aes.CreateEncryptor()) {
					using (CryptoStream crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, 0, length);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}

		public byte[] Decrypt(byte[] data, int length) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform decryptor = _aes.CreateDecryptor()) {
					using (CryptoStream crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, 0, length);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}
	}
}
