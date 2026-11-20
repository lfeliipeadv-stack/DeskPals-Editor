using System.Drawing;
using System.Drawing.Imaging;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// A sprite sheet made from separate pictures (PNG, BMP) or the frames of a GIF: all frames go on cells of the same
	/// size, centred with the feet on the bottom line. Added to an existing sheet, the old frames keep their numbers.
	/// </summary>
	internal static class SheetBuilder
	{
		/// <summary>The frames of the chosen files, in order (a GIF gives all of its frames).</summary>
		public static List<Bitmap> Load(IEnumerable<string> files)
		{
			var frames = new List<Bitmap>();
			foreach (var f in files)
			{
				using var img = Image.FromFile(f);
				int count = 1;
				try { count = img.GetFrameCount(FrameDimension.Time); } catch { }
				for (int i = 0; i < count; i++)
				{
					if (count > 1) img.SelectActiveFrame(FrameDimension.Time, i);
					var b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
					using (var g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, img.Width, img.Height);
					frames.Add(b);
				}
			}
			return frames;
		}

		/// <summary>The new sheet (PNG in base64) and its grid.</summary>
		public static (string png, int cols, int rows) Build(List<Bitmap> frames, int cols)
		{
			int w = frames.Max(f => f.Width), h = frames.Max(f => f.Height);
			cols = Math.Max(1, Math.Min(cols, frames.Count));
			int rows = (frames.Count + cols - 1) / cols;
			using var sheet = new Bitmap(cols * w, rows * h, PixelFormat.Format32bppArgb);
			using (var g = Graphics.FromImage(sheet))
			{
				g.Clear(Color.Transparent);
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
				for (int i = 0; i < frames.Count; i++)
				{
					var f = frames[i];
					int x = i % cols * w + (w - f.Width) / 2, y = i / cols * h + (h - f.Height);   // centred, feet at the bottom
					g.DrawImage(f, new Rectangle(x, y, f.Width, f.Height));
				}
			}
			using var ms = new MemoryStream();
			sheet.Save(ms, ImageFormat.Png);
			return (Convert.ToBase64String(ms.ToArray()), cols, rows);
		}

		/// <summary>The window: choose pictures, add them to the sheet or make a new one.</summary>
		/// <summary>Pictures named frame_1, frame_2 … frame_10 keep their number order.</summary>
		public static List<string> InOrder(IEnumerable<string> files)
			=> files.OrderBy(f => System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(f), @"\d+", m => m.Value.PadLeft(8, '0'))).ToList();

		/// <summary><paramref name="chosen"/>: pictures already chosen (dropped on the window); otherwise it asks.</summary>
		public static void Show(MainForm owner, PetDoc doc, string[] chosen = null)
		{
			if (chosen == null || chosen.Length == 0)
			{
				using var dlg = new OpenFileDialog { Filter = L.T("build.filter"), Multiselect = true, Title = L.T("build.title") };
				if (dlg.ShowDialog(owner) != DialogResult.OK) return;
				chosen = dlg.FileNames;
			}
			var files = InOrder(chosen);
			List<Bitmap> added;
			try { added = Load(files); }
			catch (Exception ex) { owner.Message(L.T("msg.openFail", ex.Message)); return; }
			if (added.Count == 0) return;

			bool hasSheet = doc.Frames.Count > 0;
			using var f = new Form { Text = L.T("build.title"), Size = new Size(560, 330), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = false, BackColor = Theme.Surface, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var col = Ui.Column(Ui.Title(L.T("build.found", added.Count, files.Count), 15),
				Ui.Hint(L.T("build.size", added.Max(b => b.Width), added.Max(b => b.Height)), 500));
			var append = new Toggle(L.T("build.append", doc.Frames.Count), hasSheet) { Visible = hasSheet };
			var cols = Ui.Number(1, 64, hasSheet ? Math.Max(1, doc.Root.Image.TilesX) : Math.Min(8, added.Count), 80);
			col.Controls.Add(append);
			col.Controls.Add(Ui.Labeled(L.T("sheet.cols"), cols, L.T("build.colsHint")));
			var ok = new FlatButton(L.T("build.ok"), primary: true);
			ok.Click += (s, e) => { f.DialogResult = DialogResult.OK; f.Close(); };
			var cancel = new FlatButton(L.T("build.cancel"));
			cancel.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; f.Close(); };
			col.Controls.Add(Ui.Row(ok, cancel));
			col.Location = new Point(18, 14);
			f.Controls.Add(col);
			f.AcceptButton = null;
			if (f.ShowDialog(owner) != DialogResult.OK) { foreach (var b in added) b.Dispose(); return; }

			var all = new List<Bitmap>();
			int first = 0;
			if (append.On && hasSheet) { all.AddRange(doc.Frames.Select(x => new Bitmap(x))); first = all.Count; }
			all.AddRange(added);
			var (png, c, r) = Build(all, append.On && hasSheet ? Math.Max(1, doc.Root.Image.TilesX) : (int)cols.Value);
			foreach (var b in all) b.Dispose();
			doc.Root.Image.Png = png;
			doc.Root.Image.TilesX = c;
			doc.Root.Image.TilesY = r;
			if (!append.On || !hasSheet) doc.Root.Image.Skins = 0;
			doc.Changed("image");
			owner.Message(L.T("build.done", added.Count, first, first + added.Count - 1, doc.FrameW, doc.FrameH));
		}
	}
}
