using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DoubleSocket.Protocol;
using DoubleSocket.Server;
using DoubleSocket.Utility.ByteBuffer;

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
		public const int MaxPlayerCount = 3;
		private readonly HashSet<Player> _players = new HashSet<Player>();
		private readonly Queue<int> _colors = new Queue<int>();
		private readonly ResettingByteBuffer _sendBuffer = new ResettingByteBuffer(MaxPlayerCount * 2);
		private readonly DoubleServer _server;
		private readonly Thread _senderThread;
		private byte _idCounter;

		public Program(int port) {
			_colors.Enqueue(Color.Red.ToArgb());
			_colors.Enqueue(Color.Green.ToArgb());
			_colors.Enqueue(Color.Blue.ToArgb());

			_server = new DoubleServer(this, MaxPlayerCount, MaxPlayerCount, port);
			
			_senderThread = new Thread(() => {
				try {
					while (true) {
						lock (_server) {
							byte[] data;
							using (_sendBuffer) {
								foreach (Player player in _players) {
									_sendBuffer.Write(player.Id);
									_sendBuffer.Write((byte)(player.X | (player.Y << 4)));
								}
								data = _sendBuffer.CloneArray();
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



		public bool TcpAuthenticateClient(IDoubleServerClient client, ByteBuffer buffer, out byte[] encryptionKey, out byte errorCode) {
			encryptionKey = buffer.ReadBytes();
			errorCode = 0;
			return true;
		}

		public void OnFullAuthentication(IDoubleServerClient client) {
			byte newId;
			do {
				newId = _idCounter++;
			} while (_players.Any(p => p.Id == newId));

			Player newPlayer = new Player(client, newId, _colors.Dequeue());
			client.ExtraData = newPlayer;

			foreach (Player player in _players) {
				_server.SendTcp(player.ServerClient, buffer => {
					buffer.Write((byte)1);
					buffer.Write(newPlayer.Id);
					buffer.Write(newPlayer.Color);
				});
			}

			_players.Add(newPlayer);
			_server.SendTcp(client, buffer => {
				buffer.Write((byte)0);
				foreach (Player player in _players) {
					buffer.Write(player.Id);
					buffer.Write(player.Color);
				}
			});
		}



		public void OnTcpReceived(IDoubleServerClient client, ByteBuffer buffer) {
			Console.WriteLine("Received " + buffer.BytesLeft + " bytes over TCP; this shouldn't happen.");
		}

		public void OnUdpReceived(IDoubleServerClient client, ByteBuffer buffer, ushort packetTimestamp) {
			Player player = (Player)client.ExtraData;
			if (!DoubleProtocol.IsPacketNewest(ref player.NewestPacketTimestamp, packetTimestamp)) {
				return;
			}

			byte value = buffer.ReadByte();
			player.X = (byte)(value & 0b00001111);
			player.Y = (byte)(value >> 4);
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
					buffer.Write((byte)2);
					buffer.Write(disconnectedPlayer.Id);
				});
			}
		}
	}
}
