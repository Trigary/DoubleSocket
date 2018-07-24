using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DoubleSocket.Client;
using DoubleSocket.Server;
using DoubleSocket.Utility.ByteBuffer;
using NUnit.Framework;

namespace DoubleSocket.Test {
	[TestFixture]
	public class DoubleEchoTest {
		public const int PayloadCount = 1000;
		public const int Port = 8888;
		public const int DataSize = 1000;
		public static readonly IPAddress Ip = IPAddress.Loopback;
		private static readonly Random Random = new Random();

		[Test, Timeout(2000)]
		public void Test() {
			byte[] encryptionKey = new byte[16];
			Random.NextBytes(encryptionKey);

			Console.WriteLine("Starting server");
			DoubleServerHandler serverHandler = new DoubleServerHandler();
			DoubleServer server = new DoubleServer(serverHandler, 1, 1, Port);
			serverHandler.Server = server;

			Console.WriteLine("Starting client");
			DoubleClientHandler clientHandler = new DoubleClientHandler();
			DoubleClient client = new DoubleClient(clientHandler, encryptionKey, encryptionKey, Ip, Port);
			clientHandler.Client = client;
			client.Start();

			lock (client) {
				clientHandler.MaySend = true;
				Monitor.Pulse(client);
				Console.WriteLine("Main thread waiting");
				Monitor.Wait(client);
			}

			Console.WriteLine("Closing client");
			client.Close();
			Console.WriteLine("Closing server");
			server.Close();
		}



		private static void PrintAndThrow(string message) {
			Console.WriteLine(message);
			throw new AssertionException(message);
		}

		[SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
		private static void AssertFirstArrayContainsSecond(byte[] first, int start, byte[] second) {
			for (int i = 0; i < second.Length; i++) {
				Assert.IsTrue(first[i + start] == second[i], "First array doesn't contain the second");
			}
		}



		private class DoubleServerHandler : IDoubleServerHandler {
			public DoubleServer Server { private get; set; }

			public bool TcpAuthenticateClient(IDoubleServerClient client, ByteBuffer buffer, out byte[] encryptionKey, out byte errorCode) {
				Console.WriteLine("Server TCP authenticated client");
				encryptionKey = buffer.ReadBytes();
				errorCode = 0;
				return true;
			}

			public void OnFullAuthentication(IDoubleServerClient client) {
				Console.WriteLine("Server fully authenticated client");
			}

			public void OnTcpReceived(IDoubleServerClient client, ByteBuffer buffer) {
				Console.WriteLine("SRec TCP " + buffer.BytesLeft);
				Server.SendTcp(client, buff => buff.Write(buffer.Array, buffer.ReadIndex, buffer.BytesLeft));
			}

			public void OnUdpReceived(IDoubleServerClient client, ByteBuffer buffer, ushort packetTimestamp) {
				Console.WriteLine("SRec UDP " + buffer.BytesLeft + " " + packetTimestamp);
				Server.SendUdp(client, buff => buff.Write(buffer.Array, buffer.ReadIndex, buffer.BytesLeft));
			}

			public void OnLostConnection(IDoubleServerClient client, DoubleServer.ClientState state) {
				Console.WriteLine($"Server lost connection to client ({state})");
			}
		}

		private class DoubleClientHandler : IDoubleClientHandler {
			public DoubleClient Client { private get; set; }
			public bool MaySend { private get; set; }
			private int _payloadCounter;
			private byte[] _previousPayload;

			public void OnConnectionFailure(SocketError error) {
				PrintAndThrow("Connection failure: " + error);
			}

			public void OnTcpAuthenticationFailure(byte errorCode) {
				PrintAndThrow("TCP authentication failure: " + errorCode);
			}

			public void OnAuthenticationTimeout(DoubleClient.State state) {
				PrintAndThrow("TCP authentication timeout: " + state);
			}

			public void OnFullAuthentication() {
				Console.WriteLine("Client got fully authenticated");
				if (MaySend) {
					Console.WriteLine("Client not waiting for main thread");
				} else {
					Console.WriteLine("Client waiting for main thread");
					Assert.IsTrue(Monitor.Wait(Client, 5000), "Wait for main thread timed out");
				}

				Console.WriteLine("Client sending first TCP data");
				_previousPayload = new byte[DataSize];
				Random.NextBytes(_previousPayload);
				Client.SendTcp(buff => buff.Write(_previousPayload));
			}

			public void OnTcpReceived(ByteBuffer buffer) {
				Console.WriteLine("CRec TCP " + buffer.BytesLeft);
				AssertFirstArrayContainsSecond(buffer.Array, buffer.ReadIndex, _previousPayload);
				Random.NextBytes(_previousPayload);
				if (++_payloadCounter == PayloadCount) {
					_payloadCounter = 0;
					Console.WriteLine("Client sending first UDP data");
					Client.SendUdp(buff => buff.Write(_previousPayload));
				} else {
					Client.SendTcp(buff => buff.Write(_previousPayload));
				}
			}

			public void OnUdpReceived(ByteBuffer buffer, ushort packetTimestamp) {
				Console.WriteLine("CRec UDP " + buffer.BytesLeft + " " + packetTimestamp);
				AssertFirstArrayContainsSecond(buffer.Array, buffer.ReadIndex, _previousPayload);
				if (++_payloadCounter == PayloadCount) {
					Monitor.Pulse(Client);
				} else {
					Random.NextBytes(_previousPayload);
					Client.SendUdp(buff => buff.Write(_previousPayload));
				}
			}

			public void OnConnectionLost(DoubleClient.State state) {
				Console.WriteLine($"Client lost connection to server ({state})");
			}
		}
	}
}
