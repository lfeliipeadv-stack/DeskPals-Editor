using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// A lighter pet: frames no animation uses go away (the others are renumbered), the sprite sheet is written with a
	/// palette when it has 256 colours or fewer (only if the picture stays the same, pixel by pixel), heavy sounds are
	/// pointed out.
	/// </summary>
	internal static class Optimizer
	{
		/// <summary>Frames (of the first skin) that no animation shows.</summary>
		public static List<int> Unused(PetDoc d)
		{
			var used = d.Root.Animations.Animation.SelectMany(a => a.Sequence.Frame ?? Array.Empty<int>()).ToHashSet();
			int range = d.Frames.Count / Math.Max(1, d.Root.Image.Skins);
			return Enumerable.Range(0, range).Where(i => !used.Contains(i)).ToList();
		}

		static bool Empty(Bitmap b) { for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).A > 0) return false; return true; }

		/// <summary>Remove the unused frames (pets with one skin only). Returns how many.</summary>
		public static int RemoveUnused(PetDoc d)
		{
			if (Math.Max(1, d.Root.Image.Skins) > 1) return 0;
			var unused = Unused(d).ToHashSet();
			if (unused.Count == 0) return 0;
			var map = new Dictionary<int, int>();
			var frames = new List<Bitmap>();
			for (int i = 0; i < d.Frames.Count; i++)
				if (!unused.Contains(i)) { map[i] = frames.Count; frames.Add(new Bitmap(d.Frames[i])); }
			d.RemapFrames(i => map.TryGetValue(i, out int v) ? v : null);
			d.SetFrames(frames, Math.Max(1, d.Root.Image.TilesX));
			foreach (var b in frames) b.Dispose();
			d.Changed("image");
			return unused.Count;
		}

		/// <summary>The sheet as a palette PNG when that keeps every pixel. Returns the bytes saved (0: not smaller or not possible).</summary>
		public static int Compress(PetDoc d)
		{
			if (d.Sheet == null) return 0;
			var src = d.Sheet;
			var colors = new Dictionary<int, byte>();
			for (int y = 0; y < src.Height; y++)
				for (int x = 0; x < src.Width; x++)
				{
					int c = src.GetPixel(x, y).ToArgb();
					if ((c >> 24) == 0) c = 0;   // every transparent pixel is the same
					if (!colors.ContainsKey(c)) { if (colors.Count == 256) return 0; colors[c] = (byte)colors.Count; }
				}
			using var ind = new Bitmap(src.Width, src.Height, PixelFormat.Format8bppIndexed);
			var pal = ind.Palette;
			foreach (var kv in colors) pal.Entries[kv.Value] = Color.FromArgb(kv.Key);
			ind.Palette = pal;
			var data = ind.LockBits(new Rectangle(0, 0, ind.Width, ind.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
			var row = new byte[data.Stride];
			for (int y = 0; y < src.Height; y++)
			{
				for (int x = 0; x < src.Width; x++) { int c = src.GetPixel(x, y).ToArgb(); if ((c >> 24) == 0) c = 0; row[x] = colors[c]; }
				Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride);
			}
			ind.UnlockBits(data);
			byte[] png;
			using (var ms = new MemoryStream()) { ind.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
			// the same picture? (the PNG writer must keep the transparency of the palette)
			using (var check = new Bitmap(new MemoryStream(png)))
				for (int y = 0; y < src.Height; y += 1)
					for (int x = 0; x < src.Width; x += 1)
					{
						int a = src.GetPixel(x, y).ToArgb(), b = check.GetPixel(x, y).ToArgb();
						if ((a >> 24) == 0 && (b >> 24) == 0) continue;
						if (a != b) return 0;
					}
			int old = Convert.FromBase64String(d.Root.Image.Png.Trim()).Length;
			if (png.Length >= old) return 0;
			d.Root.Image.Png = Convert.ToBase64String(png);
			d.Changed("image");
			return old - png.Length;
		}

		/// <summary>The window: what can be lighter, and buttons to do it.</summary>
		public static void Show(MainForm main, PetDoc d)
		{
			using var f = new Form { Text = L.T("opt.title"), Size = new Size(640, 470), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, BackColor = Theme.Surface, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(16), AutoScroll = true };
			f.Controls.Add(col);
			void Fill()
			{
				col.SuspendLayout();
				foreach (Control c in col.Controls) c.Dispose();
				col.Controls.Clear();
				int pngKb = string.IsNullOrWhiteSpace(d.Root.Image.Png) ? 0 : Convert.FromBase64String(d.Root.Image.Png.Trim()).Length / 1024;
				int soundsKb = d.Root.Sounds.Sound.Sum(s => (s.Base64?.Length ?? 0) * 3 / 4) / 1024;
				int total = PetXmlWriter.Write(d.Root).Length / 1024;
				col.Controls.Add(Ui.Title(L.T("opt.size", total), 16));
				col.Controls.Add(Ui.Text(L.T("opt.parts", pngKb, soundsKb), true));
				var unused = Unused(d);
				bool skins = Math.Max(1, d.Root.Image.Skins) > 1;
				col.Controls.Add(Ui.Text(L.T("opt.unused", unused.Count) + (unused.Count > 0 ? "  (" + string.Join(", ", unused.Take(20)) + (unused.Count > 20 ? "…" : "") + ")" : ""), false));
				var rem = new FlatButton(L.T("opt.removeUnused")) { Enabled = unused.Count > 0 && !skins };
				rem.Click += (s, e) => { int n = RemoveUnused(d); Fill(); main.Message(L.T("opt.removed", n)); };
				col.Controls.Add(rem);
				if (skins && unused.Count > 0) col.Controls.Add(Ui.Hint(L.T("opt.skins"), 560));
				var comp = new FlatButton(L.T("opt.compress"));
				comp.Click += (s, e) => { int saved = Compress(d); Fill(); main.Message(saved > 0 ? L.T("opt.compressed", saved / 1024) : L.T("opt.notSmaller")); };
				col.Controls.Add(Ui.Hint(L.T("opt.compressHint"), 560));
				col.Controls.Add(comp);
				foreach (var s in d.Root.Sounds.Sound.Where(s => (s.Base64?.Length ?? 0) * 3 / 4 > 100 * 1024))
					col.Controls.Add(Ui.Text("⚠ " + L.T("opt.heavySound", d.Anim(s.Id)?.Name ?? "#" + s.Id, s.Base64.Length * 3 / 4 / 1024), false));
				col.ResumeLayout();
			}
			Fill();
			f.ShowDialog(main);
		}
	}
}
