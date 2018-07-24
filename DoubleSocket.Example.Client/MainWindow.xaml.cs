using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DoubleSocket.Client;
using DoubleSocket.Protocol;
using DoubleSocket.Utility.ByteBuffer;

namespace DoubleSocket.Example.Client {
	public partial class MainWindow : IDoubleClientHandler {
		public const int UpdateSendFrequency = 30;
		private readonly CellMap _cellMap = new CellMap();
		private readonly IDictionary<byte, Player> _players = new Dictionary<byte, Player>();
		private readonly DispatcherTimer _timer = new DispatcherTimer();
		private DoubleClient _client;
		private ushort _newestPacketTimestamp;

		public MainWindow() {
			InitializeComponent();
			_cellMap.AsSourceOf(DisplayedImage);

			_timer.Tick += (sender, eventArgs) => {
				System.Windows.Point mouse = Mouse.GetPosition(DisplayedImage);
				byte x = CalculateCellCoordinate(mouse.X, DisplayedImage.ActualWidth);
				byte y = CalculateCellCoordinate(mouse.Y, DisplayedImage.ActualHeight);

				lock (_client) {
					_client.SendUdp(buff => buff.Write((byte)(x | (y << 4))));
					_cellMap.AsSourceOf(DisplayedImage);
				}
			};
			_timer.Interval = TimeSpan.FromMilliseconds(1000d / UpdateSendFrequency);
		}



		private static byte CalculateCellCoordinate(double mouse, double imageSpan) {
			if (mouse <= 0) {
				return byte.MaxValue;
			}
			mouse *= CellMap.Dimension / imageSpan;
			return mouse > CellMap.Dimension ? byte.MaxValue : Convert.ToByte(mouse);
		}



		public void OnTcpReceived(ByteBuffer buffer) {
			switch (buffer.ReadByte()) {
				case 0:
					while (buffer.ReadIndex < buffer.WriteIndex) {
						_players.Add(buffer.ReadByte(), new Player(Color.FromArgb(buffer.ReadInt())));
					}
					Dispatcher.InvokeAsync(() => {
						_timer.Start();
						lock (_client) {
							PlayersText.Text = _players.Count.ToString();
						}
					});
					return;
				case 1:
					_players.Add(buffer.ReadByte(), new Player(Color.FromArgb(buffer.ReadInt())));
					break;
				case 2:
					_players.Remove(buffer.ReadByte());
					break;
				default:
					throw new Exception("Invalid TCP packet type was received");
			}

			Dispatcher.InvokeAsync(() => {
				lock (_client) {
					PlayersText.Text = _players.Count.ToString();
				}
			});
		}



		public void OnUdpReceived(ByteBuffer buffer, ushort packetTimestamp) {
			if (!DoubleProtocol.IsPacketNewest(ref _newestPacketTimestamp, packetTimestamp)) {
				return;
			}

			foreach (Player player in _players.Values) {
				player.LoopOverCells((x, y) => { _cellMap.Set(x, y, CellMap.DefaultBrush); });
			}

			while (buffer.ReadIndex < buffer.WriteIndex) {
				if (_players.TryGetValue(buffer.ReadByte(), out Player player)) {
					byte value = buffer.ReadByte();
					player.X = (byte)(value & 0b00001111);
					player.Y = (byte)(value >> 4);
				}
			}

			foreach (Player player in _players.Values) {
				player.LoopOverCells((x, y) => _cellMap.Set(x, y, player.Brush));
			}
		}



		[SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
		private void OnConnectButtonClick(object sender, RoutedEventArgs e) {
			if (!IPAddress.TryParse(AddressInput.Text, out IPAddress ip)) {
				StatusText.Text = "Invalid IP";
				return;
			}

			if (!Regex.IsMatch(PortInput.Text, "[0-9]+") || !int.TryParse(PortInput.Text, out int port)) {
				StatusText.Text = "Invalid port";
				return;
			}

			ConnectButton.IsEnabled = false;
			StatusText.Text = "Connecting...";
			byte[] encryptionKey = new byte[16]; //This is obviously not how it should be done
			_client = new DoubleClient(this, encryptionKey, encryptionKey, ip, port);
			_client.Start();
		}

		private void OnDisconnectButtonClick(object sender, RoutedEventArgs e) {
			lock (_client) {
				if (_client.CurrentState == DoubleClient.State.Disconnected) {
					return;
				}

				_client.Close();
				OnDisconnected("Disconnected");
			}
		}



		public void OnConnectionFailure(SocketError error) {
			OnFailedToConnect("Connection failure: " + error);
		}

		public void OnTcpAuthenticationFailure(byte errorCode) {
			OnFailedToConnect("TCP authentication failure; this should never happen");
		}

		public void OnAuthenticationTimeout(DoubleClient.State state) {
			OnFailedToConnect("Authentication timeout: " + state);
		}

		private void OnFailedToConnect(string newStatus) {
			Dispatcher.InvokeAsync(() => {
				StatusText.Text = newStatus;
				ConnectButton.IsEnabled = true;
			});
		}



		public void OnFullAuthentication() {
			Dispatcher.InvokeAsync(() => {
				StatusText.Text = "Connected";
				DisconnectButton.IsEnabled = true;
			});
		}

		public void OnConnectionLost(DoubleClient.State state) {
			Dispatcher.InvokeAsync(() => OnDisconnected("Connection lost"));
		}

		private void OnDisconnected(string newStatus) {
			_timer.Stop();
			lock (_client) {
				_players.Clear();
				_newestPacketTimestamp = 0;
			}

			StatusText.Text = newStatus;
			PlayersText.Text = "0";
			DisconnectButton.IsEnabled = false;
			ConnectButton.IsEnabled = true;
		}
	}
}
