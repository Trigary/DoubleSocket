using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using DoubleSocket.Protocol;

namespace DoubleSocket.Server {
	public interface IDoubleServerClient {
		DoubleServer.ClientState State { get; }
		object ExtraData { get; set; }
	}

	public class DoubleServerClient : IDoubleServerClient {
		private static readonly Random Random = new Random();
		private static readonly byte[] RandomBytes = new byte[8];

		public DoubleServer.ClientState State { get; private set; } = DoubleServer.ClientState.TcpAuthenticating;
		public object ExtraData { get; set; }

		public Socket TcpSocket { get; }
		public EndPoint UdpEndPoint { get; private set; }
		public byte[] EncryptionKey { get; private set; }
		public byte SequenceIdBound { get; private set; }
		public long ConnectionStartTimestamp { get; private set; }
		private byte _sendSequenceId;
		private byte _receiveSequenceId;

		public DoubleServerClient(Socket socket) {
			TcpSocket = socket;
		}



		public void TcpAuthenticated(byte[] encryptionKey, ICollection<ulong> usedKeys, out ulong udpAuthenticationKey) {
			State = DoubleServer.ClientState.UdpAuthenticating;
			EncryptionKey = encryptionKey;
			lock (Random) {
				SequenceIdBound = (byte)Random.Next(128, 256);
				do {
					Random.NextBytes(RandomBytes);
					udpAuthenticationKey = BitConverter.ToUInt64(RandomBytes, 0);
				} while (usedKeys.Contains(udpAuthenticationKey));
			}
			ConnectionStartTimestamp = DoubleProtocol.TimeMillis;
		}

		public void UdpAuthenticated(EndPoint endPoint) {
			State = DoubleServer.ClientState.Authenticated;
			UdpEndPoint = endPoint;
		}

		public void Disconnected() {
			State = DoubleServer.ClientState.Disconnected;
		}



		public byte NextSendSequenceId() {
			byte value = _sendSequenceId;
			if (++_sendSequenceId == SequenceIdBound) {
				_sendSequenceId = 0;
			}
			return value;
		}

		public bool CheckReceiveSequenceId(byte id) {
			if (id != _receiveSequenceId) {
				return false;
			}
			if (++_receiveSequenceId == SequenceIdBound) {
				_receiveSequenceId = 0;
			}
			return true;
		}
	}
}
