using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DoubleSocket.Protocol;
using DoubleSocket.Server;
using DoubleSocket.Utility.BitBuffer;

namespace DoubleSocket.Example.Server {
	public class Program : IDoubleServerHandler {
		public static void Main() {
			string port;
			do {
				Console.Write("Please enter the port the server should listen on: ");
				port = Console.ReadLine();
				// ReSharper disable once AssignNullToNotNullAttribute
			} while (!Regex.IsMatch(port, "[0-9]+"));

			Program program = new Program(int.Parse(port));
			Console.WriteLine("The server started, press a key to stop it.");
			Console.ReadKey(false);
			program.Stop();
			Console.WriteLine("Server stopped, exiting.");
		}



		public const int UpdateSendFrequency = 30;
		private readonly HashSet<Player> _players = new HashSet<Player>();
		private readonly Queue<byte> _colors = new Queue<byte>();
		private readonly ResettingBitBuffer _sendBuffer = new ResettingBitBuffer(4);
		private readonly DoubleServer _server;
		private readonly Thread _senderThread;

		public Program(int port) {
			_colors.Enqueue(0);
			_colors.Enqueue(1);
			_colors.Enqueue(2);

			_server = new DoubleServer(this, 3, 3, port);

			_senderThread = new Thread(() => {
				try {
					while (true) {
						lock (_server) {
							byte[] data;
							using (_sendBuffer) {
								_sendBuffer.WriteBits((ulong)_players.Count, 2);
								foreach (Player player in _players) {
									_sendBuffer.WriteBits(player.Id, 2);
									_sendBuffer.WriteBits(player.X, 4);
									_sendBuffer.WriteBits(player.Y, 4);
								}
								data = _sendBuffer.ReadBytes();
							}

							foreach (Player player in _players) {
								_server.SendUdp(player.ServerClient, buffer => buffer.Write(data));
							}
						}
						Thread.Sleep(1000 / UpdateSendFrequency);
					}
				} catch (ThreadInterruptedException) {
				}
			});
			_senderThread.Start();
		}

		public void Stop() {
			_senderThread.Interrupt();
			_senderThread.Join();
			_server.Close();
		}



		public bool TcpAuthenticateClient(IDoubleServerClient client, BitBuffer buffer, out byte[] encryptionKey, out byte errorCode) {
			encryptionKey = buffer.ReadBytes();
			errorCode = 0;
			return true;
		}

		public Action<BitBuffer> OnFullAuthentication(IDoubleServerClient client) {
			byte newId = 0;
			while (_players.Any(p => p.Id == newId)) {
				newId++;
			}

			Player newPlayer = new Player(client, newId, _colors.Dequeue());
			client.ExtraData = newPlayer;

			foreach (Player player in _players) {
				_server.SendTcp(player.ServerClient, buffer => {
					buffer.Write(true);
					buffer.WriteBits(newPlayer.Id, 2);
					buffer.WriteBits(newPlayer.Color, 2);
				});
			}

			_players.Add(newPlayer);
			return buffer => {
				buffer.WriteBits((ulong)_players.Count, 2);
				foreach (Player player in _players) {
					buffer.WriteBits(player.Id, 2);
					buffer.WriteBits(player.Color, 2);
				}
			};
		}



		public void OnTcpReceived(IDoubleServerClient client, BitBuffer buffer) {
			Console.WriteLine("Received " + buffer.TotalBitsLeft + " bits over TCP; this shouldn't happen.");
		}

		public void OnUdpReceived(IDoubleServerClient client, BitBuffer buffer, ushort packetTimestamp) {
			Player player = (Player)client.ExtraData;
			if (DoubleProtocol.IsPacketNewest(ref player.NewestPacketTimestamp, packetTimestamp)) {
				player.X = (byte)buffer.ReadBits(4);
				player.Y = (byte)buffer.ReadBits(4);
			}
		}



		public void OnLostConnection(IDoubleServerClient client, DoubleServer.ClientState state) {
			if (state != DoubleServer.ClientState.Authenticated) {
				return;
			}

			Player disconnectedPlayer = (Player)client.ExtraData;
			_players.Remove(disconnectedPlayer);
			_colors.Enqueue(disconnectedPlayer.Color);

			foreach (Player player in _players) {
				_server.SendTcp(player.ServerClient, buffer => {
					buffer.Write(false);
					buffer.WriteBits(disconnectedPlayer.Id, 2);
				});
			}
		}
	}
}
