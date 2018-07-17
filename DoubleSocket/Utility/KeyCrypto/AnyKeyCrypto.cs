using System;
using System.IO;
using System.Security.Cryptography;

namespace DoubleSocket.Utility.KeyCrypto {
	public class AnyKeyCrypto : IDisposable {
		private readonly Aes _aes;
		private readonly byte[] _iv;

		public AnyKeyCrypto() {
			_aes = Aes.Create();
			// ReSharper disable once PossibleNullReferenceException
			_aes.Mode = CipherMode.CBC;
			_aes.Padding = PaddingMode.PKCS7;
			_iv = new byte[16];
		}



		public void Dispose() {
			_aes.Dispose();
		}

		public byte[] Encrypt(byte[] encryptionKey, byte[] data, int length) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform encryptor = _aes.CreateEncryptor(encryptionKey, _iv)) {
					using (CryptoStream crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, 0, length);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}

		public byte[] Decrypt(byte[] encryptionKey, byte[] data, int length) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform decryptor = _aes.CreateDecryptor(encryptionKey, _iv)) {
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
