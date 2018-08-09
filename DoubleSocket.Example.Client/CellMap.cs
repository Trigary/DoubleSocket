using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace DoubleSocket.Example.Client {
	public class CellMap {
		public const int Dimension = 16;
		public const int Scale = 100;
		public static readonly Color DefaultColor = Color.White;
		public static readonly Brush DefaultBrush = new SolidBrush(DefaultColor);

		private readonly Bitmap _bitmap = new Bitmap(Dimension * Scale, Dimension * Scale);
		private readonly Graphics _graphics;

		public CellMap() {
			_graphics = Graphics.FromImage(_bitmap);
			_graphics.FillRectangle(DefaultBrush, 0, 0, Dimension * Scale, Dimension * Scale);
		}



		public void Set(int x, int y, Brush color) {
			_graphics.FillRectangle(color, x * Scale, y * Scale, Scale, Scale);
		}

		public void AsSourceOf(System.Windows.Controls.Image image) {
			using (MemoryStream memory = new MemoryStream()) {
				_bitmap.Save(memory, ImageFormat.Bmp);
				memory.Position = 0;
				BitmapImage source = new BitmapImage();
				source.BeginInit();
				source.StreamSource = memory;
				source.CacheOption = BitmapCacheOption.OnLoad;
				source.EndInit();
				image.Source = source;
			}
		}
	}
}
