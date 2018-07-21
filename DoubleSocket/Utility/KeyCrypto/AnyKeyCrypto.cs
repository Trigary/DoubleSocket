using System;
using System.IO;
using System.Security.Cryptography;

namespace DoubleSocket.Utility.KeyCrypto {
	/// <summary>
	/// A class which doesn't get initialized with a specific encryption key, therefore can encrypt data with any keys.
	/// </summary>
	public class AnyKeyCrypto : IDisposable {
		private readonly Aes _aes;
		private readonly byte[] _iv;

		/// <summary>
		/// Create and initialize a new instance.
		/// </summary>
		public AnyKeyCrypto() {
			_aes = Aes.Create();
			// ReSharper disable once PossibleNullReferenceException
			_aes.Mode = CipherMode.CBC;
			_aes.Padding = PaddingMode.PKCS7;
			_iv = FixedKeyCrypto.Iv;
		}



		/// <summary>
		/// Dispose of the AES instance.
		/// </summary>
		public void Dispose() {
			_aes.Dispose();
		}

		/// <summary>
		/// Encrypt the specified data with the specified key.
		/// </summary>
		/// <param name="encryptionKey">The key to encrypt with.</param>
		/// <param name="data">The data which to encrypt.</param>
		/// <param name="offset">The offset of the data.</param>
		/// <param name="size">The size of the data.</param>
		/// <returns></returns>
		public byte[] Encrypt(byte[] encryptionKey, byte[] data, int offset, int size) {
			using (MemoryStream memory = new MemoryStream()) {
				using (ICryptoTransform encryptor = _aes.CreateEncryptor(encryptionKey, _iv)) {
					using (CryptoStream crypto = new CryptoStream(memory, encryptor, CryptoStreamMode.Write)) {
						crypto.Write(data, offset, size);
						crypto.FlushFinalBlock();
						return memory.ToArray();
					}
				}
			}
		}

		/// <summary>
		/// Decrypt the specified data with the specified key.
		/// </summary>
		/// <param name="encryptionKey">The key to decrypt with.</param>
		/// <param name="data">The data which to decrypt.</param>
		/// <param name="offset">The offset of the data.</param>
		/// <param name="size">The size of the data.</param>
		/// <returns></returns>
		public byte[] Decrypt(byte[] encryptionKey, byte[] data, int offset, int size) {
			try {
				using (MemoryStream memory = new MemoryStream()) {
					using (ICryptoTransform decryptor = _aes.CreateDecryptor(encryptionKey, _iv)) {
						using (CryptoStream crypto = new CryptoStream(memory, decryptor, CryptoStreamMode.Write)) {
							crypto.Write(data, offset, size);
							crypto.FlushFinalBlock();
							return memory.ToArray();
						}
					}
				}
			} catch (CryptographicException) {
				return null;
			}
		}
	}
}
