using DoubleSocket.Server;

namespace DoubleSocket.Example.Server {
	public class Player {
		public IDoubleServerClient ServerClient { get; }
		public byte Id { get; }
		public int Color { get; }
		public byte X { get; set; } = byte.MaxValue;
		public byte Y { get; set; } = byte.MaxValue;
		public ushort NewestPacketTimestamp;

		public Player(IDoubleServerClient serverClient, byte id, int color) {
			ServerClient = serverClient;
			Id = id;
			Color = color;
		}
	}
}
