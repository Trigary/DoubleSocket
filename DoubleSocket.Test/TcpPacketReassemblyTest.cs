using System;
using System.Diagnostics.CodeAnalysis;
using DoubleSocket.Protocol;
using NUnit.Framework;

namespace DoubleSocket.Test {
	[TestFixture]
	public class TcpPacketReassemblyTest {
		public const int PayloadCount = 100;
		public const int DataSize = 1000;
		private readonly Random _random = new Random();
		private readonly byte[] _sentPayload = new byte[DataSize];
		private readonly byte[] _sendBuffer = new byte[DataSize + 2];
		private readonly byte[] _tempArray = new byte[DataSize + 2];
		private TcpHelper _helper;

		[OneTimeSetUp]
		[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
		public void OneTimeSetUp() {
			int payloadSize = _sendBuffer.Length - 2;
			_sendBuffer[0] = (byte)payloadSize;
			_sendBuffer[1] = (byte)(payloadSize >> 8);
			
			_helper = new TcpHelper((sender, buffer, offset, size) => {
				int i;
				for (i = 0; i < _sentPayload.Length; i++) {
					Assert.AreEqual(buffer[i + offset], _sentPayload[i], "Packet reassembly failed: inequal data");
				}
				Assert.AreEqual(i, size, "Received size doesn't equal expected size");
			});
		}

		[SetUp]
		public void EachTimeSetUp() {
			_random.NextBytes(_sentPayload);
			Buffer.BlockCopy(_sentPayload, 0, _sendBuffer, 2, _sentPayload.Length);
		}
		
		private void Send(byte[] buffer, int offset, int count) {
			if (offset == 0) {
				_helper.OnTcpReceived(null, buffer, count);
			} else {
				Buffer.BlockCopy(buffer, offset, _tempArray, 0, count);
				_helper.OnTcpReceived(null, _tempArray, count);
			}
		}

		

		[Test, Repeat(PayloadCount)]
		public void SendAllOnceTest() {
			Send(_sendBuffer, 0, _sendBuffer.Length);
		}

		[Test, Repeat(PayloadCount)]
		public void SendByteByByteTest() {
			for (int i = 0; i < _sendBuffer.Length; i++) {
				Send(_sendBuffer, i, 1);
			}
		}

		[Test, Repeat(PayloadCount)]
		public void SendSizeThenPayloadTest() {
			Send(_sendBuffer, 0, 2);
			Send(_sendBuffer, 2, _sendBuffer.Length - 2);
		}

		[Test, Repeat(PayloadCount)]
		public void SendHalvedTest() {
			int half = _sendBuffer.Length / 2;
			Send(_sendBuffer, 0, half);
			Send(_sendBuffer, half, _sendBuffer.Length - half);
		}

		[Test, Repeat(PayloadCount)]
		public void SendOneThenRestTest() {
			Send(_sendBuffer, 0, 1);
			Send(_sendBuffer, 1, _sendBuffer.Length - 1);
		}

		[Test, Repeat(PayloadCount)]
		public void SendOneThenOneThenRestTest() {
			Send(_sendBuffer, 0, 1);
			Send(_sendBuffer, 1, 1);
			Send(_sendBuffer, 2, _sendBuffer.Length - 2);
		}

		[Test, Repeat(PayloadCount)]
		public void SendOneThenHalvedRestTest() {
			Send(_sendBuffer, 0, 1);
			int half = (_sendBuffer.Length - 1) / 2;
			Send(_sendBuffer, 1, half);
			Send(_sendBuffer, 1 + half, _sendBuffer.Length - 1 - half);
		}

		[Test, Repeat(PayloadCount)]
		public void SendOneThenOneThenHalvedRestTest() {
			Send(_sendBuffer, 0, 1);
			Send(_sendBuffer, 1, 1);
			int half = (_sendBuffer.Length - 2) / 2;
			Send(_sendBuffer, 2, half);
			Send(_sendBuffer, 2 + half, _sendBuffer.Length - 2 - half);
		}
	}
}
