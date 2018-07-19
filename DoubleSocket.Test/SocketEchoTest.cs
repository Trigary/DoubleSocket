using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DoubleSocket.Client;
using DoubleSocket.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DoubleSocket.Test {
	[TestClass]
	[SuppressMessage("ReSharper", "PossibleNullReferenceException")]
	[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
	[SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
	public class SocketEchoTest {
		public const string Ip = "127.0.0.1";
		public const int Port = 8888;
		public const int DataLength = 1000;
		public const int SocketBufferSize = 3 * DataLength;
		public const int Timeout = 1000;
		public const int RunCount = 1000;
		private readonly Random _random = new Random();

		[TestMethod]
		public void UdpTest() {
			object monitor = new object();
			int runCounter = 0;

			UdpServerSocket server = null;
			server = new UdpServerSocket((sender, buffer, size) => server.Send(sender, buffer, 0, size),
				DataLength, Port, SocketBufferSize, Timeout);

			byte[] sendBuffer = new byte[DataLength];
			_random.NextBytes(sendBuffer);

			UdpClientSocket client = null;
			client = new UdpClientSocket((buffer, size) => {
				lock (monitor) {
					CollectionAssert.AreEqual(sendBuffer, buffer, "Sent and received data aren't equal");
					if (++runCounter == RunCount) {
						Monitor.Pulse(monitor);
					} else {
						_random.NextBytes(sendBuffer);
						client.Send(sendBuffer, 0, sendBuffer.Length);
					}
				}
			}, SocketBufferSize, Timeout);
			client.Start(Ip, Port, DataLength);

			client.Send(sendBuffer, 0, sendBuffer.Length);
			lock (monitor) {
				Monitor.Wait(monitor);
			}
		}

		[TestMethod]
		public void TcpTest() {
			object monitor = new object();
			bool clientMaySend = false;
			int runCounter = 0;

			TcpServerSocket server = null;
			server = new TcpServerSocket(socket => { }, (sender, buffer, size) =>
				server.Send(sender, buffer, 0, size), DataLength, 10, Port, SocketBufferSize, Timeout);

			byte[] sendBuffer = new byte[DataLength];
			_random.NextBytes(sendBuffer);

			TcpClientSocket client = null;
			client = new TcpClientSocket(() => {
				lock (monitor) {
					if (!clientMaySend) {
						Monitor.Wait(monitor);
					}
					client.Send(sendBuffer, 0, sendBuffer.Length);
				}
			}, (buffer, size) => {
				lock (monitor) {
					Assert.IsTrue(sendBuffer.SequenceEqual(buffer), "Sent and received data aren't equal");
					if (++runCounter == RunCount) {
						Monitor.Pulse(monitor);
					} else {
						_random.NextBytes(sendBuffer);
						client.Send(sendBuffer, 0, sendBuffer.Length);
					}
				}
			}, SocketBufferSize, Timeout);
			client.Start(Ip, Port, DataLength);

			lock (monitor) {
				clientMaySend = true;
				Monitor.Pulse(monitor);
				Monitor.Wait(monitor);
			}
		}
	}
}
