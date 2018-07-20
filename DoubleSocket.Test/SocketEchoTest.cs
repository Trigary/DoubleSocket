using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Threading;
using DoubleSocket.Client;
using DoubleSocket.Server;
using NUnit.Framework;

namespace DoubleSocket.Test {
	[TestFixture]
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

		[Test]
		public void UdpTest() {
			object monitor = new object();
			lock (monitor) {
				int runCounter = 0;

				Console.WriteLine("Starting server");
				UdpServerSocket server = null;
				server = new UdpServerSocket((sender, buffer, size) => {
					lock (monitor) {
						Console.WriteLine("SR " + size);
						server.Send(sender, buffer, 0, size);
					}
				}, Port, SocketBufferSize, Timeout, DataLength + 1);

				byte[] sendBuffer = new byte[DataLength];
				_random.NextBytes(sendBuffer);

				Console.WriteLine("Starting client");
				UdpClientSocket client = null;
				client = new UdpClientSocket((buffer, size) => {
					lock (monitor) {
						Console.WriteLine("CR " + size);
						AssertArrayContentsEqualInFirstArrayLengthRange(sendBuffer, buffer);
						if (++runCounter == RunCount) {
							Monitor.Pulse(monitor);
						} else {
							_random.NextBytes(sendBuffer);
							client.Send(sendBuffer, 0, sendBuffer.Length);
						}
					}
				}, SocketBufferSize, Timeout, DataLength + 1);
				client.Start(Ip, Port);

				Console.WriteLine("Client sending data first data");
				client.Send(sendBuffer, 0, sendBuffer.Length);
				Console.WriteLine("Waiting");
				Assert.IsTrue(Monitor.Wait(monitor, 5000), "Test timed out");

				Console.WriteLine("Closing client");
				client.Close();
				Console.WriteLine("Closing server");
				server.Close();
			}
		}

		[Test]
		public void TcpTest() {
			object monitor = new object();
			lock (monitor) {
				bool clientMaySend = false;
				int runCounter = 0;

				Console.WriteLine("Starting server");
				HashSet<Socket> connectedSockets = new HashSet<Socket>();
				TcpServerSocket server = null;
				server = new TcpServerSocket(socket => {
					lock (monitor) {
						lock (connectedSockets) {
							Console.WriteLine("Client connected");
							connectedSockets.Add(socket);
						}
					}
				}, (sender, buffer, size) => {
					lock (monitor) {
						Console.WriteLine("SR " + size);
						server.Send(sender, buffer, 0, size);
					}
				}, socket => {
					lock (connectedSockets) {
						Console.WriteLine("Server lost connection to client");
						connectedSockets.Remove(socket);
					}
				}, connectedSockets, 10, Port, SocketBufferSize, Timeout, DataLength + 1);

				byte[] sendBuffer = new byte[DataLength];
				_random.NextBytes(sendBuffer);

				Console.WriteLine("Starting client");
				TcpClientSocket client = null;
				client = new TcpClientSocket(() => {
						lock (monitor) {
							if (clientMaySend) {
								Console.WriteLine("Client not waiting for main thread");
							} else {
								Console.WriteLine("Client waiting for main thread");
								Assert.IsTrue(Monitor.Wait(monitor, 5000), "Wait for main thread timed out");
							}
							Console.WriteLine("Client sending data first data");
							client.Send(sendBuffer, 0, sendBuffer.Length);
						}
					}, error => {
						lock (monitor) {
							Console.WriteLine("Client failed to connect to server: " + error);
						}
					},
					(buffer, size) => {
						lock (monitor) {
							Console.WriteLine("CR " + size);
							AssertArrayContentsEqualInFirstArrayLengthRange(sendBuffer, buffer);
							if (++runCounter == RunCount) {
								Monitor.Pulse(monitor);
							} else {
								_random.NextBytes(sendBuffer);
								client.Send(sendBuffer, 0, sendBuffer.Length);
							}
						}
					}, () => Console.WriteLine("Client lost connection to server"), SocketBufferSize, Timeout, DataLength + 1);
				client.Start(Ip, Port);

				clientMaySend = true;
				Monitor.Pulse(monitor);
				Console.WriteLine("Waiting");
				Assert.IsTrue(Monitor.Wait(monitor, 5000), "Test timed out");

				Console.WriteLine("Closing client");
				client.Close();
				Console.WriteLine("Closing server");
				server.Close();
			}
		}



		// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
		private static void AssertArrayContentsEqualInFirstArrayLengthRange(byte[] first, byte[] second) {
			const string message = "The two arrays aren't equal in the first array's length's range";
			for (int i = 0; i < first.Length; i++) {
				Assert.IsTrue(first[i] == second[i], message);
			}
		}
	}
}
