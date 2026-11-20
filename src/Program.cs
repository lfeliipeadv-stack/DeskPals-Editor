using DesktopPet;

namespace PetEditor
{
	internal static class Program
	{
		public static string SnapDir;
		/// <summary>Developer: "--wikishots=<folder>", the pictures of the wiki (every page, window and state), then close.</summary>
		public static string WikiDir;
		/// <summary>Developer: "--demoframes=<folder>", the frames of the animated demo of the README, then close.</summary>
		public static string DemoDir;
		public static System.Drawing.Size SnapSize;
		/// <summary>The page to show at the start ("--page=settings": the editor opened again after a change of size).</summary>
		public static string StartPage => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--page="))?.Substring(7);

		/// <summary>
		/// "PetEditor.exe [animations.xml | pet folder]": opens that pet, otherwise the last one (or the Project page).
		/// "--lang=en": developer, the editor in this language for this run only.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Timing.Mark("start"); EditorSettings.Load();
			var s = EditorSettings.Current;
			// developer: "--lang=en" or "--lang=pt" for this run only (the setting is not changed)
			L.Use(args.FirstOrDefault(a => a.StartsWith("--lang="))?.Substring(7) ?? s.Language);
			Theme.Mode = s.Theme;
			Theme.AccentId = s.Accent;
			ApplicationConfiguration.Initialize();
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += (o, e) =>
			{
				// taking the pictures nobody is there to close a message: the error goes to a file
				if ((SnapDir ?? WikiDir) != null) { try { Directory.CreateDirectory(SnapDir ?? WikiDir); File.AppendAllText(Path.Combine(SnapDir ?? WikiDir, "erros.txt"), e.Exception + Environment.NewLine); } catch { } return; }
				MessageBox.Show(L.T("msg.openFail", e.Exception.Message), L.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
			};
			// developer: every pet of a folder read and written again must stay the same ("--roundtrip=C:\Pets")
			// developer: how long the check of a pet takes ("--checktime=animations.xml")
			var ct = args.FirstOrDefault(a => a.StartsWith("--checktime="));
			if (ct != null)
			{
				var d = PetDoc.Open(ct.Substring(12));
				var sw = System.Diagnostics.Stopwatch.StartNew();
				var p = Validator.Run(d);
				File.WriteAllText(Path.Combine(Path.GetTempPath(), "PetEditor-check.txt"), $"{p.Count} problems in {sw.ElapsedMilliseconds} ms\n" + string.Join("\n", p.Take(30).Select(x => (x.Error ? "E " : "W ") + x.Text)));
				return;
			}
			// developer: a sheet built from pictures or a GIF ("--buildsheet=out.png" followed by the pictures)
			var bs = args.FirstOrDefault(a => a.StartsWith("--buildsheet="));
			if (bs != null)
			{
				var frames = SheetBuilder.Load(args.Where(a => !a.StartsWith("--")));
				var (png, c, r) = SheetBuilder.Build(frames, 8);
				File.WriteAllBytes(bs.Substring(13), Convert.FromBase64String(png));
				File.WriteAllText(bs.Substring(13) + ".txt", $"{frames.Count} frames, {c} x {r}, cell {frames.Max(f => f.Width)}x{frames.Max(f => f.Height)}");
				return;
			}
			// developer: the assistant, the copy between pets, the GIF and the compression, without the window ("--devtests=C:\Pets")
			var dt = args.FirstOrDefault(a => a.StartsWith("--devtests="));
			if (dt != null) { Environment.Exit(DevTests.Run(dt.Substring(11))); return; }
			var rt = args.FirstOrDefault(a => a.StartsWith("--roundtrip="));
			if (rt != null) { Environment.Exit(RoundTrip(rt.Substring(12))); return; }
			string open = args.FirstOrDefault(a => !a.StartsWith("--") && (File.Exists(a) || Directory.Exists(a)));
			// developer: a picture of every page, light and dark, in this folder ("--snap=C:\folder"), then it closes
			SnapDir = args.FirstOrDefault(a => a.StartsWith("--snap="))?.Substring(7);
			WikiDir = args.FirstOrDefault(a => a.StartsWith("--wikishots="))?.Substring(12);
			DemoDir = args.FirstOrDefault(a => a.StartsWith("--demoframes="))?.Substring(13);
			WikiDir ??= DemoDir;   // the same quiet mode (errors to a file)
			var size = args.FirstOrDefault(a => a.StartsWith("--snapsize="))?.Substring(11).Split('x');
			if (size?.Length == 2) SnapSize = new System.Drawing.Size(int.Parse(size[0]), int.Parse(size[1]));
			var form = new MainForm(open); Timing.Mark("window built");
			Application.Run(form);
		}

		/// <summary>Read each pet, write it with the editor, read it again: the two models must be equal.</summary>
		static int RoundTrip(string dir)
		{
			var ser = new System.Xml.Serialization.XmlSerializer(typeof(XmlData.RootNode));
			string Dump(XmlData.RootNode r) { using var sw = new StringWriter(); ser.Serialize(sw, r); return sw.ToString(); }
			var log = new List<string>();
			int bad = 0;
			foreach (var file in Directory.GetDirectories(dir).Select(d => Path.Combine(d, "animations.xml")).Where(File.Exists))
			{
				var a = XmlData.AnimationXML.ParseXML(File.ReadAllText(file));
				string written = PetXmlWriter.Write(new PetDoc(a, file).Root);
				var b = XmlData.AnimationXML.ParseXML(written);
				var a2 = XmlData.AnimationXML.ParseXML(File.ReadAllText(file));
				if (b == null)
				{
					try { new System.Xml.XmlDocument().LoadXml(written); log.Add("   (parses as XML, not as a pet)"); }
					catch (Exception e) { log.Add("   not XML: " + e.Message); }
				}
				string da = Dump(PetDocNormalize(a2)), db = b == null ? "" : Dump(PetDocNormalize(b));
				bool same = b != null && da == db;
				if (!same)
				{
					bad++;
					int i = 0; while (i < Math.Min(da.Length, db.Length) && da[i] == db[i]) i++;
					int s0 = Math.Max(0, i - 150);
					log.Add("   before: " + da.Substring(s0, Math.Min(260, da.Length - s0)).Replace("\n", " ").Replace("\r", ""));
					log.Add("   after:  " + db.Substring(s0, Math.Min(260, db.Length - s0)).Replace("\n", " ").Replace("\r", ""));
				}
				log.Add($"{(same ? "OK  " : "DIFF")} {Path.GetFileName(Path.GetDirectoryName(file))}  ({new FileInfo(file).Length / 1024} KB -> {written.Length / 1024} KB)");
			}
			File.WriteAllLines(Path.Combine(Path.GetTempPath(), "PetEditor-roundtrip.txt"), log);
			return bad;
		}

		static XmlData.RootNode PetDocNormalize(XmlData.RootNode r) => new PetDoc(r, null).Root;
	}
}
