using System.Drawing;

namespace DoubleSocket.Example.Client {
	public class Player {
		public delegate void CellLooper(int x, int y);

		public Brush Brush { get; }
		public byte X { get; set; } = byte.MaxValue;
		public byte Y { get; set; } = byte.MaxValue;

		public Player(Color color) {
			Brush = new SolidBrush(color);
		}



		public void LoopOverCells(CellLooper action) {
			if (X == byte.MaxValue || Y == byte.MaxValue) {
				return;
			}

			for (int x = 0; x < CellMap.Dimension; x++) {
				action(x, Y);
			}
			for (int y = 0; y < CellMap.Dimension; y++) {
				action(X, y);
			}
		}
	}
}
