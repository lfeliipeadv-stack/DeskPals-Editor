using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PetEditor
{
	/// <summary>The pet's icon: a multi-size .ico in base64 (PNG entries), made from a frame or a file.</summary>
	internal static class Icons
	{
		/// <summary>The largest picture of the icon (base64 .ico) at <paramref name="size"/>, or null.</summary>
		public static Bitmap FromIco(string base64, int size)
		{
			if (string.IsNullOrWhiteSpace(base64)) return null;
			try
			{
				var bytes = Convert.FromBase64String(base64.Trim());
				using var ms = new MemoryStream(bytes);
				using var ico = new Icon(ms, size, size);
				return ico.ToBitmap();
			}
			catch
			{
				try
				{
					using var ms = new MemoryStream(Convert.FromBase64String(base64.Trim()));
					using var img = Image.FromStream(ms);
					return new Bitmap(img, size, size);
				}
				catch { return null; }
			}
		}

		/// <summary>An .ico (16, 32, 48 and 64 px) in base64 made from a picture: cropped to what is drawn, kept square, pixels crisp.</summary>
		public static string MakeIco(Bitmap source)
		{
			var crop = Content(source);
			int[] sizes = { 16, 32, 48, 64 };
			var images = sizes.Select(s =>
			{
				using var bmp = Square(source, crop, s);
				using var ms = new MemoryStream();
				bmp.Save(ms, ImageFormat.Png);
				return ms.ToArray();
			}).ToList();
			using var outp = new MemoryStream();
			using var w = new BinaryWriter(outp);
			w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
			int offset = 6 + 16 * sizes.Length;
			for (int i = 0; i < sizes.Length; i++)
			{
				w.Write((byte)sizes[i]); w.Write((byte)sizes[i]); w.Write((byte)0); w.Write((byte)0);
				w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset);
				offset += images[i].Length;
			}
			foreach (var img in images) w.Write(img);
			w.Flush();
			return Convert.ToBase64String(outp.ToArray());
		}

		static Rectangle Content(Bitmap b)
		{
			int x0 = b.Width, y0 = b.Height, x1 = -1, y1 = -1;
			for (int y = 0; y < b.Height; y++)
				for (int x = 0; x < b.Width; x++)
					if (b.GetPixel(x, y).A > 20) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
			return x1 < 0 ? new Rectangle(0, 0, b.Width, b.Height) : Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
		}

		public static Bitmap Square(Bitmap src, Rectangle crop, int size)
		{
			int side = Math.Max(crop.Width, crop.Height);
			var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
			using var g = Graphics.FromImage(b);
			bool up = side <= size;
			g.InterpolationMode = up ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = up ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
			float sc = (float)size / side;
			g.DrawImage(src, new RectangleF((side - crop.Width) / 2f * sc, (side - crop.Height) / 2f * sc, crop.Width * sc, crop.Height * sc), crop, GraphicsUnit.Pixel);
			return b;
		}
	}
}
