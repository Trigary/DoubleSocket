using System;
using System.Net;
using System.Net.Sockets;

namespace DoubleSocket.Server {
	public interface IDoubleServerClient {
		DoubleServer.ClientState State { get; }
		object ExtraData { get; set; }
	}

	public class DoubleServerClient : IDoubleServerClient {
		public DoubleServer.ClientState State { get; private set; } = DoubleServer.ClientState.Authenticating;
		public object ExtraData { get; set; }

		public Socket TcpSocket { get; }
		public EndPoint UdpEndPoint { get; private set; }
		public byte[] EncryptionKey { get; private set; }
		
		private static readonly Random Random = new Random();
		private static readonly byte[] RandomBytes = new byte[4];
		private ulong _udpAuthenticationKey;

		public DoubleServerClient(Socket socket) {
			TcpSocket = socket;
		}



		public void TcpAuthenticated(byte[] encryptionKey, out ulong udpAuthenticationKey) {
			State = DoubleServer.ClientState.UdpCreating;
			EncryptionKey = encryptionKey;
			lock (Random) {
				Random.NextBytes(RandomBytes);
				_udpAuthenticationKey = BitConverter.ToUInt64(RandomBytes, 0);
			}
			udpAuthenticationKey = _udpAuthenticationKey;
		}

		public bool IsUdpAuthenticatingWith(ulong key) {
			return State == DoubleServer.ClientState.UdpCreating && key == _udpAuthenticationKey;
		}

		public void InitializeUdp(EndPoint endPoint) {
			State = DoubleServer.ClientState.Connected;
			UdpEndPoint = endPoint;
		}
	}
}
