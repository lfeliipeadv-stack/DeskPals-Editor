using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// Small pixel retouch of one frame: pencil, eraser (right button), bucket, eyedropper and colour swap (in this
	/// frame or in every frame), mirror and move by one pixel, with undo. Saving writes the frame back into the sheet.
	/// </summary>
	internal static class PixelEditor
	{
		public static void Show(MainForm main, PetDoc doc, int frame)
		{
			var src = doc.Frame(frame);
			if (src == null) return;
			var img = new Bitmap(src);
			var undo = new Stack<Bitmap>();
			var swaps = new List<(Color from, Color to)>();   // colour swaps to do in every frame
			Color color = Color.Black;
			string tool = "pen";

			using var f = new Form { Text = L.T("px.title", frame), Size = new Size(1000, 720), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Background, Font = Ui.Font(13), KeyPreview = true };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var side = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 270, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(12), AutoScroll = true };
			var canvas = new Canvas { Dock = DockStyle.Fill, Image = img };
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(12, 9, 12, 0) };
			f.Controls.Add(canvas); f.Controls.Add(side); f.Controls.Add(bar);

			void Keep() { undo.Push(new Bitmap(img)); if (undo.Count > 60) { var arr = undo.ToArray(); undo.Clear(); foreach (var b in arr.Take(60).Reverse()) undo.Push(b); } }
			var swatch = new Panel { Size = new Size(46, 30), BackColor = color, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 4, 8, 0) };
			void SetColor(Color c) { color = c; swatch.BackColor = c.A == 0 ? Theme.Surface : c; }

			// tools
			side.Controls.Add(Ui.Text(L.T("px.tools"), true, 12));
			var tools = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(250, 0), BackColor = Theme.Surface };
			foreach (var (key, label) in new[] { ("pen", "px.pen"), ("line", "px.line"), ("rect", "px.rect"), ("fill", "px.fill"), ("pick", "px.pick"), ("swap", "px.swap") })
			{
				var b = new FlatButton(L.T(label), primary: key == tool).Tip(L.T("tip." + label));
				b.Click += (s, e) => { tool = key; foreach (FlatButton x in tools.Controls) { x.Primary = x == b; x.Font = Ui.Font(13, x.Primary); x.Invalidate(); } };
				tools.Controls.Add(b);
			}
			side.Controls.Add(tools);
			var everywhere = new Toggle(L.T("px.everywhere"), false);
			side.Controls.Add(everywhere);
			side.Controls.Add(Ui.Hint(L.T("px.hint"), 240));

			// colour
			side.Controls.Add(Ui.Text(L.T("px.color"), true, 12));
			var more = new FlatButton(L.T("px.more"));
			more.Click += (s, e) => { using var cd = new ColorDialog { Color = color, FullOpen = true }; if (cd.ShowDialog(f) == DialogResult.OK) SetColor(cd.Color); };
			side.Controls.Add(Ui.Row(swatch, more));
			var palette = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(250, 0), BackColor = Theme.Surface };
			// the colours of the whole pet, most used first
			var counts = new Dictionary<int, int>();
			foreach (var fr in doc.Frames.Take(64))
				for (int y = 0; y < fr.Height; y += 1) for (int x = 0; x < fr.Width; x += 1) { var c = fr.GetPixel(x, y); if (c.A > 200) counts[c.ToArgb()] = counts.GetValueOrDefault(c.ToArgb()) + 1; }
			foreach (var argb in counts.OrderByDescending(kv => kv.Value).Take(32).Select(kv => kv.Key))
			{
				var p = new Panel { Size = new Size(26, 26), BackColor = Color.FromArgb(argb), Margin = new Padding(0, 0, 4, 4), Cursor = Cursors.Hand, BorderStyle = BorderStyle.FixedSingle };
				p.Click += (s, e) => SetColor(p.BackColor);
				palette.Controls.Add(p);
			}
			side.Controls.Add(palette);
			SetColor(counts.Count > 0 ? Color.FromArgb(counts.OrderByDescending(kv => kv.Value).First().Key) : Color.Black);

			// the whole frame
			side.Controls.Add(Ui.Text(L.T("px.frame"), true, 12));
			Bitmap Moved(int dx, int dy)
			{
				var n = new Bitmap(img.Width, img.Height);
				using (var g = Graphics.FromImage(n)) g.DrawImageUnscaled(img, dx, dy);
				return n;
			}
			void Replace(Bitmap b) { Keep(); using (var g = Graphics.FromImage(img)) { g.Clear(Color.Transparent); g.DrawImageUnscaled(b, 0, 0); } b.Dispose(); canvas.Invalidate(); }
			var flipH = new FlatButton("↔ " + L.T("px.mirrorH")); flipH.Click += (s, e) => { Keep(); img.RotateFlip(RotateFlipType.RotateNoneFlipX); canvas.Invalidate(); };
			var flipV = new FlatButton("↕ " + L.T("px.mirrorV")); flipV.Click += (s, e) => { Keep(); img.RotateFlip(RotateFlipType.RotateNoneFlipY); canvas.Invalidate(); };
			// a border of one pixel around the drawing (the transparent pixels that touch it)
			var outline = new FlatButton("▢ " + L.T("px.outline")).Tip(L.T("tip.px.outline"));
			outline.Click += (s, e) =>
			{
				Keep();
				var around = new List<Point>();
				for (int y = 0; y < img.Height; y++)
					for (int x = 0; x < img.Width; x++)
					{
						if (img.GetPixel(x, y).A > 0) continue;
						bool Solid(int px, int py) => px >= 0 && py >= 0 && px < img.Width && py < img.Height && img.GetPixel(px, py).A > 0;
						if (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1)) around.Add(new Point(x, y));
					}
				foreach (var p in around) img.SetPixel(p.X, p.Y, color.A == 0 ? Color.Black : color);
				canvas.Invalidate();
			};
			side.Controls.Add(Ui.Row(flipH, flipV, outline));
			var move = Ui.Row();
			foreach (var (t, dx, dy) in new[] { ("←", -1, 0), ("→", 1, 0), ("↑", 0, -1), ("↓", 0, 1) })
			{
				var b = new FlatButton(t) { Width = 40 }.Tip(L.T("px.move"));
				b.Click += (s, e) => Replace(Moved(dx, dy));
				move.Controls.Add(b);
			}
			side.Controls.Add(move);
			var undoBtn = new FlatButton(L.T("act.undo")); undoBtn.Click += (s, e) => { if (undo.Count == 0) return; var b = undo.Pop(); using (var g = Graphics.FromImage(img)) { g.Clear(Color.Transparent); g.DrawImageUnscaled(b, 0, 0); } b.Dispose(); canvas.Invalidate(); };
			side.Controls.Add(undoBtn);

			// painting
			void Flood(int x0, int y0, Color with)
			{
				var target = img.GetPixel(x0, y0);
				if (target.ToArgb() == with.ToArgb()) return;
				var q = new Queue<Point>(); q.Enqueue(new Point(x0, y0));
				while (q.Count > 0)
				{
					var p = q.Dequeue();
					if (p.X < 0 || p.Y < 0 || p.X >= img.Width || p.Y >= img.Height || img.GetPixel(p.X, p.Y).ToArgb() != target.ToArgb()) continue;
					img.SetPixel(p.X, p.Y, with);
					q.Enqueue(new Point(p.X + 1, p.Y)); q.Enqueue(new Point(p.X - 1, p.Y)); q.Enqueue(new Point(p.X, p.Y + 1)); q.Enqueue(new Point(p.X, p.Y - 1));
				}
			}
			// line and rectangle: drawn again from the picture of the press at each move (what is seen is what stays)
			Point start = Point.Empty; Bitmap before = null;
			void Plot(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < img.Width && y < img.Height) img.SetPixel(x, y, c); }
			void Line(Point a, Point b, Color c)
			{
				int dx = Math.Abs(b.X - a.X), sx = a.X < b.X ? 1 : -1, dy = -Math.Abs(b.Y - a.Y), sy = a.Y < b.Y ? 1 : -1, err = dx + dy;
				int x = a.X, y = a.Y;
				while (true)
				{
					Plot(x, y, c);
					if (x == b.X && y == b.Y) break;
					int e2 = 2 * err;
					if (e2 >= dy) { err += dy; x += sx; }
					if (e2 <= dx) { err += dx; y += sy; }
				}
			}
			canvas.Paint1 += (p, button, first) =>
			{
				Color with = button == MouseButtons.Right ? Color.Transparent : color;
				if (tool is "line" or "rect")
				{
					if (first) { Keep(); start = p; before?.Dispose(); before = new Bitmap(img); }
					else if (before != null) using (var g = Graphics.FromImage(img)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(before, 0, 0); }
					if (tool == "line") Line(start, p, with);
					else
					{
						var a = start; var tr = new Point(p.X, a.Y); var bl = new Point(a.X, p.Y);
						Line(a, tr, with); Line(tr, p, with); Line(p, bl, with); Line(bl, a, with);
					}
					canvas.Invalidate();
					return;
				}
				switch (tool)
				{
					case "pen": if (first) Keep(); img.SetPixel(p.X, p.Y, with); break;
					case "fill": if (!first) return; Keep(); Flood(p.X, p.Y, with); break;
					case "pick": if (!first) return; SetColor(img.GetPixel(p.X, p.Y)); break;
					case "swap":
						if (!first) return;
						var from = img.GetPixel(p.X, p.Y);
						Keep();
						for (int y = 0; y < img.Height; y++) for (int x = 0; x < img.Width; x++) if (img.GetPixel(x, y).ToArgb() == from.ToArgb()) img.SetPixel(x, y, with);
						if (everywhere.On) swaps.Add((from, with));
						break;
				}
				canvas.Invalidate();
			};
			f.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.Z) undoBtn.PerformClick(); };

			var save = new FlatButton(L.T("px.save"), primary: true);
			var cancel = new FlatButton(L.T("build.cancel")); cancel.Click += (s, e) => f.Close();
			bar.Controls.Add(save); bar.Controls.Add(cancel);
			save.Click += (s, e) =>
			{
				var frames = doc.CopyFrames();
				frames[frame].Dispose();
				frames[frame] = new Bitmap(img);
				foreach (var (from, to) in swaps)
					for (int i = 0; i < frames.Count; i++)
						if (i != frame) Recolor(frames[i], new Dictionary<int, Color> { [from.ToArgb()] = to });
				doc.SetFrames(frames, Math.Max(1, doc.Root.Image.TilesX));
				foreach (var b in frames) b.Dispose();
				doc.Changed("image");
				f.Close();
			};
			f.ShowDialog(main);
			img.Dispose();
			before?.Dispose();
			foreach (var b in undo) b.Dispose();
		}

		/// <summary>Every pixel of a colour in <paramref name="map"/> takes the new colour.</summary>
		public static void Recolor(Bitmap b, Dictionary<int, Color> map)
		{
			for (int y = 0; y < b.Height; y++)
				for (int x = 0; x < b.Width; x++)
					if (map.TryGetValue(b.GetPixel(x, y).ToArgb(), out var c)) b.SetPixel(x, y, c);
		}

		/// <summary>The frame big, on a checker board with its pixel grid; the mouse paints.</summary>
		sealed class Canvas : Control
		{
			public Bitmap Image;
			/// <summary>A pixel was clicked or the mouse went over one with a button down (first = the press).</summary>
			public event Action<Point, MouseButtons, bool> Paint1;
			Point last = new(-1, -1);

			public Canvas() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Cross; }

			int Zoom => Image == null ? 1 : Math.Max(1, Math.Min((Width - 20) / Image.Width, (Height - 20) / Image.Height));
			Point Origin => new((Width - Image.Width * Zoom) / 2, (Height - Image.Height * Zoom) / 2);
			Point Pixel(Point m) { var o = Origin; return new Point((m.X - o.X) / Zoom, (m.Y - o.Y) / Zoom); }
			bool Inside(Point p) => p.X >= 0 && p.Y >= 0 && p.X < Image.Width && p.Y < Image.Height;

			protected override void OnMouseDown(MouseEventArgs e) { var p = Pixel(e.Location); if (Inside(p)) { last = p; Paint1?.Invoke(p, e.Button, true); } }
			protected override void OnMouseMove(MouseEventArgs e)
			{
				if (e.Button == MouseButtons.None) return;
				var p = Pixel(e.Location);
				if (Inside(p) && p != last) { last = p; Paint1?.Invoke(p, e.Button, false); }
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics;
				g.Clear(Theme.Background);
				if (Image == null) return;
				int z = Zoom; var o = Origin;
				using (var a = new SolidBrush(Theme.IsDark ? Color.FromArgb(52, 52, 56) : Color.FromArgb(232, 234, 238)))
				using (var b = new SolidBrush(Theme.IsDark ? Color.FromArgb(62, 62, 67) : Color.White))
					for (int y = 0; y < Image.Height; y++) for (int x = 0; x < Image.Width; x++) g.FillRectangle((x + y) % 2 == 0 ? a : b, o.X + x * z, o.Y + y * z, z, z);
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = PixelOffsetMode.Half;
				g.DrawImage(Image, new Rectangle(o.X, o.Y, Image.Width * z, Image.Height * z));
				if (z >= 6)
					using (var pen = new Pen(Color.FromArgb(40, 0, 0, 0)))
					{
						for (int x = 0; x <= Image.Width; x++) g.DrawLine(pen, o.X + x * z, o.Y, o.X + x * z, o.Y + Image.Height * z);
						for (int y = 0; y <= Image.Height; y++) g.DrawLine(pen, o.X, o.Y + y * z, o.X + Image.Width * z, o.Y + y * z);
					}
			}
		}
	}

	/// <summary>A new skin: the pet in other colours (every colour can be swapped), added after the frames it has.</summary>
	internal static class SkinMaker
	{
		public static void Show(MainForm main, PetDoc doc)
		{
			if (doc.Frames.Count == 0) { main.Message(L.T("sheet.empty")); return; }
			int skins = Math.Max(1, doc.Root.Image.Skins);
			int range = doc.Frames.Count / skins;   // the frames of the first skin (the ones the animations use)
			var counts = new Dictionary<int, int>();
			for (int i = 0; i < range; i++)
			{
				var fr = doc.Frame(i);
				for (int y = 0; y < fr.Height; y++) for (int x = 0; x < fr.Width; x++) { var c = fr.GetPixel(x, y); if (c.A > 200) counts[c.ToArgb()] = counts.GetValueOrDefault(c.ToArgb()) + 1; }
			}
			var main12 = counts.OrderByDescending(kv => kv.Value).Take(16).Select(kv => kv.Key).ToList();
			var map = new Dictionary<int, Color>();
			using var f = new Form { Text = L.T("skin.title"), Size = new Size(760, 560), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Surface, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var preview = new PictureBox { Dock = DockStyle.Right, Width = 260, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.SurfaceAlt };
			void Preview()
			{
				var b = new Bitmap(doc.Frame(0));
				PixelEditor.Recolor(b, map);
				var big = new Bitmap(b.Width * 4, b.Height * 4);
				using (var g = Graphics.FromImage(big)) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(b, 0, 0, big.Width, big.Height); }
				b.Dispose();
				var old = preview.Image; preview.Image = big; old?.Dispose();
			}
			var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Surface, Padding = new Padding(14) };
			col.Controls.Add(Ui.Hint(L.T("skin.intro"), 430));
			foreach (int argb in main12)
			{
				var from = new Panel { Size = new Size(40, 26), BackColor = Color.FromArgb(argb), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 8, 0) };
				var to = new Panel { Size = new Size(40, 26), BackColor = Color.FromArgb(argb), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, Margin = new Padding(8, 2, 8, 0) };
				to.Click += (s, e) =>
				{
					using var cd = new ColorDialog { Color = to.BackColor, FullOpen = true };
					if (cd.ShowDialog(f) != DialogResult.OK) return;
					to.BackColor = cd.Color;
					if (cd.Color.ToArgb() == argb) map.Remove(argb); else map[argb] = cd.Color;
					Preview();
				};
				col.Controls.Add(Ui.Row(from, new Label { Text = "→", AutoSize = true, Font = Ui.Font(14), ForeColor = Theme.TextMuted, Margin = new Padding(0, 4, 0, 0) }, to));
			}
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(12, 9, 12, 0) };
			var ok = new FlatButton(L.T("skin.ok"), primary: true);
			var cancel = new FlatButton(L.T("build.cancel")); cancel.Click += (s, e) => f.Close();
			bar.Controls.Add(ok); bar.Controls.Add(cancel);
			f.Controls.Add(col); f.Controls.Add(preview); f.Controls.Add(bar);
			ok.Click += (s, e) =>
			{
				if (map.Count == 0) { main.Message(L.T("skin.none")); return; }
				var frames = doc.CopyFrames();
				for (int i = 0; i < range; i++) { var b = new Bitmap(frames[i]); PixelEditor.Recolor(b, map); frames.Add(b); }
				// the frames of each skin must fill whole rows (the app splits the sheet in equal parts)
				doc.SetFrames(frames, Math.Max(1, doc.Root.Image.TilesX));
				foreach (var b in frames) b.Dispose();
				doc.Root.Image.Skins = skins + 1;
				doc.Changed("image");
				main.Message(L.T("skin.done", skins + 1));
				f.Close();
			};
			Preview();
			f.ShowDialog(main);
		}
	}
}
