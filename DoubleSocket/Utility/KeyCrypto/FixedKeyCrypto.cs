using System;
using System.IO;
using System.Security.Cryptography;

namespace DoubleSocket.Utility.KeyCrypto {
	/// <summary>
	/// A class which gets initialized with a specific encryption key and can encrypt data with that.
	/// </summary>
	public class FixedKeyCrypto : IDisposable {
		private readonly Aes _aes;

		/// <summary>
		/// Create and initialize a new instance.
		/// </summary>
		/// <param name="encryptionKey">The encryption key to use in future encryptions.</param>
		public FixedKeyCrypto(byte[] encryptionKey) {
			_aes = Aes.Create();
			// ReSharper disable once PossibleNullReferenceException
			_aes.Mode = CipherMode.CBC;
			_aes.Padding = PaddingMode.PKCS7;
			_aes.Key = encryptionKey;
			_aes.IV = new byte[16];
		}



		/// <summary>
		/// Dispose of the AES instance.
		/// </summary>
		public void Dispose() {
			_aes.Dispose();
		}

		/// <summary>
		/// Encrypt the specified data.
		/// </summary>
		/// <param name="data">The data which to encrypt.</param>
		/// <param name="offset">The offset of the data.</param>
		/// <param name="size">The size of the data.</param>
		/// <returns></returns>
		public byte[] Encrypt(byte[] data, int offset, int size) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform encryptor = _aes.CreateEncryptor()) {
					using (CryptoStream crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, offset, size);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}

		/// <summary>
		/// Decrypt the specified data.
		/// </summary>
		/// <param name="data">The data which to decrypt.</param>
		/// <param name="offset">The offset of the data.</param>
		/// <param name="size">The size of the data.</param>
		/// <returns></returns>
		public byte[] Decrypt(byte[] data, int offset, int size) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform decryptor = _aes.CreateDecryptor()) {
					using (CryptoStream crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, offset, size);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}
	}
}
