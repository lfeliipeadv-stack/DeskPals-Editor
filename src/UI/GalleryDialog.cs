using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// "Prepare for the gallery": the folder of the pet as the gallery of DesktopPet wants it — animations.xml, icon.png,
	/// README.md (English) and README.pt-BR.md, preview.gif (walking and one of its tricks) — and its line in
	/// pets.json when the chosen folder has one.
	/// </summary>
	internal static class GalleryDialog
	{
		public static void Show(MainForm main, PetDoc doc)
		{
			if (doc.Frames.Count == 0) { main.Message(L.T("sheet.empty")); return; }
			var h = doc.Root.Header;
			string slug = Regex.Replace((h.Petname ?? "pet").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD), @"[^a-z0-9]+", "_").Trim('_');
			if (slug.Length == 0) slug = "pet";
			using var f = new Form { Text = L.T("gal.title"), Size = new Size(820, 720), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Surface, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Surface, Padding = new Padding(16) };
			col.Controls.Add(Ui.Hint(L.T("gal.intro"), 740));
			var parent = Ui.Box(GuessPets(doc) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 520);
			var browse = new FlatButton(L.T("set.browse"));
			browse.Click += (s, e) => { using var dlg = new FolderBrowserDialog { SelectedPath = parent.Text }; if (dlg.ShowDialog(f) == DialogResult.OK) parent.Text = dlg.SelectedPath; };
			col.Controls.Add(Ui.Row(Ui.Labeled(L.T("gal.parent"), parent, L.T("gal.parentHint")), browse));
			var folder = Ui.Box(slug, 220);
			col.Controls.Add(Ui.Labeled(L.T("gal.folder"), folder, L.T("gal.folderHint")));
			string info = (h.Info ?? "").Replace("[br]", "\n");
			var en = Ui.Box($"# {h.Petname}\n\n{info}\n\nMade by {h.Author} with the Pet Editor of DesktopPet.\n".Replace("\n", Environment.NewLine), 740);
			en.Multiline = true; en.Height = 120; en.ScrollBars = ScrollBars.Vertical;
			var pt = Ui.Box($"# {h.Petname}\n\n{info}\n\nFeito por {h.Author} com o Editor de Pets do DesktopPet.\n".Replace("\n", Environment.NewLine), 740);
			pt.Multiline = true; pt.Height = 120; pt.ScrollBars = ScrollBars.Vertical;
			col.Controls.Add(Ui.Labeled("README.md (English)", en));
			col.Controls.Add(Ui.Labeled("README.pt-BR.md", pt));
			var json = new Toggle(L.T("gal.json"), true);
			col.Controls.Add(json);
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(12, 9, 12, 0) };
			var ok = new FlatButton(L.T("gal.ok"), primary: true);
			var cancel = new FlatButton(L.T("build.cancel")); cancel.Click += (s, e) => f.Close();
			bar.Controls.Add(ok); bar.Controls.Add(cancel);
			f.Controls.Add(col); f.Controls.Add(bar);
			ok.Click += (s, e) =>
			{
				var (errors, _) = Validator.Count(doc);
				if (errors > 0 && MessageBox.Show(f, L.T("msg.saveWithErrors", errors), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
				try
				{
					string dir = Path.Combine(parent.Text.Trim(), folder.Text.Trim());
					Directory.CreateDirectory(dir);
					string xmlPath = Path.Combine(dir, "animations.xml");
					string xml = PetXmlWriter.Write(doc.Root);
					File.WriteAllText(xmlPath, xml, new System.Text.UTF8Encoding(false));
					using (var icon = Icons.FromIco(h.Icon, 64) ?? new Bitmap(doc.Frame(0))) icon.Save(Path.Combine(dir, "icon.png"), ImageFormat.Png);
					File.WriteAllText(Path.Combine(dir, "README.md"), en.Text.Replace("\r\n", "\n"), new System.Text.UTF8Encoding(false));
					File.WriteAllText(Path.Combine(dir, "README.pt-BR.md"), pt.Text.Replace("\r\n", "\n"), new System.Text.UTF8Encoding(false));
					File.WriteAllBytes(Path.Combine(dir, "preview.gif"), Preview(doc));
					string jsonNote = "";
					string petsJson = Path.Combine(parent.Text.Trim(), "pets.json");
					if (json.On && File.Exists(petsJson)) { UpdateJson(petsJson, folder.Text.Trim(), doc, xml.Length); jsonNote = "\n" + L.T("gal.jsonDone"); }
					// the editor goes on with the copy in the gallery
					doc.FilePath = xmlPath;
					EditorSettings.Current.AddRecent(xmlPath);
					main.Message(L.T("gal.done", dir) + jsonNote);
					f.Close();
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
				}
				catch (Exception ex) { main.Message(L.T("msg.saveFail", ex.Message)); }
			};
			f.ShowDialog(main);
		}

		/// <summary>
		/// The pet in one .zip, ready to send: a folder with animations.xml, icon.png, preview.gif and the READMEs
		/// (the same as the gallery). Whoever gets it unzips the folder into the pets of DesktopPet.
		/// </summary>
		public static void Zip(MainForm main, PetDoc doc)
		{
			if (doc.Frames.Count == 0) { main.Message(L.T("sheet.empty")); return; }
			var h = doc.Root.Header;
			string slug = Regex.Replace((h.Petname ?? "pet").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD), @"[^a-z0-9]+", "_").Trim('_');
			if (slug.Length == 0) slug = "pet";
			var (errors, _) = Validator.Count(doc);
			if (errors > 0 && MessageBox.Show(main, L.T("msg.saveWithErrors", errors), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
			using var dlg = new SaveFileDialog { Filter = L.T("zip.filter"), FileName = slug + ".zip", Title = L.T("proj.zip"), OverwritePrompt = true };
			if (dlg.ShowDialog(main) != DialogResult.OK) return;
			try
			{
				Cursor.Current = Cursors.WaitCursor;
				WriteZip(doc, dlg.FileName, slug);
				Cursor.Current = Cursors.Default;
				main.Message(L.T("zip.done", dlg.FileName, new FileInfo(dlg.FileName).Length / 1024));
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{dlg.FileName}\"") { UseShellExecute = true });
			}
			catch (Exception ex) { Cursor.Current = Cursors.Default; main.Message(L.T("msg.saveFail", ex.Message)); }
		}

		/// <summary>The .zip itself: the folder <paramref name="slug"/> with the files of the pet.</summary>
		public static void WriteZip(PetDoc doc, string zipPath, string slug)
		{
			var h = doc.Root.Header;
			string work = Path.Combine(Path.GetTempPath(), "PetEditor-zip-" + Guid.NewGuid().ToString("N"));
			try
			{
				string dir = Path.Combine(work, slug);
				Directory.CreateDirectory(dir);
				var utf8 = new System.Text.UTF8Encoding(false);
				File.WriteAllText(Path.Combine(dir, "animations.xml"), PetXmlWriter.Write(doc.Root), utf8);
				using (var icon = Icons.FromIco(h.Icon, 64) ?? new Bitmap(doc.Frame(0))) icon.Save(Path.Combine(dir, "icon.png"), ImageFormat.Png);
				string info = (h.Info ?? "").Replace("[br]", "\n");
				File.WriteAllText(Path.Combine(dir, "README.md"), $"# {h.Petname}\n\n{info}\n\nMade by {h.Author} with the Pet Editor of DesktopPet.\n", utf8);
				File.WriteAllText(Path.Combine(dir, "README.pt-BR.md"), $"# {h.Petname}\n\n{info}\n\nFeito por {h.Author} com o Editor de Pets do DesktopPet.\n", utf8);
				File.WriteAllBytes(Path.Combine(dir, "preview.gif"), Preview(doc));
				if (File.Exists(zipPath)) File.Delete(zipPath);
				System.IO.Compression.ZipFile.CreateFromDirectory(work, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);
			}
			finally { try { Directory.Delete(work, true); } catch { } }
		}

		/// <summary>The Pets folder of the repository, when the pet is in it (or the editor runs from it).</summary>
		static string GuessPets(PetDoc doc)
		{
			foreach (var start in new[] { doc.FilePath == null ? null : Path.GetDirectoryName(doc.FilePath), AppContext.BaseDirectory })
			{
				var d = start == null ? null : new DirectoryInfo(start);
				for (int i = 0; i < 7 && d != null; i++, d = d.Parent)
				{
					if (File.Exists(Path.Combine(d.FullName, "pets.json"))) return d.FullName;
					if (File.Exists(Path.Combine(d.FullName, "Pets", "pets.json"))) return Path.Combine(d.FullName, "Pets");
				}
			}
			return null;
		}

		/// <summary>The animated preview: the pet walking (two rounds) and one of its tricks, about 120 px tall.</summary>
		public static byte[] Preview(PetDoc doc)
		{
			var anims = doc.Root.Animations.Animation;
			XmlData.AnimationNode Find(params string[] names) => names.Select(n => anims.FirstOrDefault(a => string.Equals(a.Name, n, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(a => a?.Sequence.Frame?.Length > 0);
			var walk = Find("walk", "idle") ?? anims.FirstOrDefault(a => a.Sequence.Frame?.Length > 0);
			var trick = Find("wave", "dance", "happy", "jump", "spin", "eat", "sleep", "blink");
			var seq = new List<int>();
			for (int k = 0; k < 2; k++) seq.AddRange(walk.Sequence.Frame);
			if (trick != null && trick != walk) seq.AddRange(trick.Sequence.Frame);
			while (seq.Count < 8) seq.AddRange(walk.Sequence.Frame);
			int scale = Math.Max(1, 120 / Math.Max(1, doc.FrameH));
			var frames = seq.Where(i => doc.Frame(i) != null).Select(i =>
			{
				var b = new Bitmap(doc.FrameW * scale, doc.FrameH * scale, PixelFormat.Format32bppArgb);
				using var g = Graphics.FromImage(b);
				g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
				g.DrawImage(doc.Frame(i), new Rectangle(0, 0, b.Width, b.Height));
				return b;
			}).ToList();
			int delay = (int)Expr.Value(walk.Start.Interval, new ExprContext(), 150);
			var gif = GifWriter.Write(frames, Math.Max(60, delay));
			foreach (var b in frames) b.Dispose();
			return gif;
		}

		/// <summary>Add or update the pet's line in pets.json (the gallery list), in the same form as the others.</summary>
		static void UpdateJson(string file, string folder, PetDoc doc, int size)
		{
			string text = File.ReadAllText(file);
			string nl = text.Contains("\r\n") ? "\r\n" : "\n";
			string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
			string E(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
			string entry = "    {" + nl +
				$"      \"folder\" : \"{E(folder)}\",{nl}      \"name\" : \"{E(doc.Root.Header.Petname)}\",{nl}      \"author\" : \"{E(doc.Root.Header.Author)}\",{nl}" +
				$"      \"lastupdate\" : \"{now}\",{nl}      \"size\" : {size},{nl}      \"animations\" : {doc.Root.Animations.Animation.Length},{nl}      \"sounds\" : {doc.Root.Sounds.Sound.Length}{nl}    }}";
			var m = Regex.Match(text, "\\{[^{}]*?\"folder\"\\s*:\\s*\"" + Regex.Escape(folder) + "\"[^{}]*\\}");
			if (m.Success) text = text.Substring(0, m.Index) + entry.TrimStart() + text.Substring(m.Index + m.Length);
			else
			{
				int i = text.IndexOf("\"pets\"", StringComparison.Ordinal);
				int open = i < 0 ? -1 : text.IndexOf('[', i);
				if (open < 0) return;
				bool empty = text.Substring(open + 1).TrimStart().StartsWith("]");
				text = text.Insert(open + 1, nl + entry + (empty ? nl : ","));
			}
			File.WriteAllText(file, text, new System.Text.UTF8Encoding(false));
		}
	}
}
