using System.Drawing;
using System.Drawing.Imaging;

namespace PetEditor
{
	/// <summary>
	/// Developer checks of the tools without windows ("--devtests=&lt;Pets folder&gt;"): the results go to
	/// %TEMP%\PetEditor-devtests.txt; the exit code is the number of failures.
	/// </summary>
	internal static class DevTests
	{
		public static int Run(string pets)
		{
			var log = new List<string>();
			int fails = 0;
			void Check(string name, bool ok, string detail = "") { log.Add($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  →  " + detail : "")}"); if (!ok) fails++; }
			string tmp = Path.Combine(Path.GetTempPath(), "PetEditor-devtests");
			Directory.CreateDirectory(tmp);
			try
			{
				var pingo = PetDoc.Open(Path.Combine(pets, "pingo", "animations.xml"));

				// 1. the animated preview: the GIF reads back with all its frames and the same pixels
				var gif = GalleryDialog.Preview(pingo);
				File.WriteAllBytes(Path.Combine(tmp, "preview.gif"), gif);
				using (var img = Image.FromStream(new MemoryStream(gif)))
				{
					int n = img.GetFrameCount(FrameDimension.Time);
					Check("gif: readable, several frames", n >= 8, $"{n} frames, {img.Width}x{img.Height}");
					var walk = pingo.Root.Animations.Animation.First(a => a.Name == "walk");
					int scale = Math.Max(1, 120 / pingo.FrameH);
					img.SelectActiveFrame(FrameDimension.Time, 0);
					using var b = new Bitmap(img);
					var src = pingo.Frame(walk.Sequence.Frame[0]);
					int diff = 0, opaque = 0;
					for (int y = 0; y < src.Height; y++)
						for (int x = 0; x < src.Width; x++)
						{
							var a = src.GetPixel(x, y); var c = b.GetPixel(x * scale + scale / 2, y * scale + scale / 2);
							if (a.A < 128) { if (c.A >= 128) diff++; continue; }
							opaque++;
							if (Math.Abs(a.R - c.R) + Math.Abs(a.G - c.G) + Math.Abs(a.B - c.B) > 24) diff++;
						}
					Check("gif: the first frame is the pet, pixel by pixel", diff == 0 && opaque > 0, $"{diff} different of {opaque}");
				}

				// 2. the assistant: a pet from the frames of a GIF, with every role it needs
				var frames = SheetBuilder.Load(new[] { Path.Combine(pets, "pingo", "preview.gif") });
				var (png, cols, rows) = SheetBuilder.Build(frames, 8);
				var sheet = new PetDoc(new XmlData.RootNode { Image = new XmlData.ImageNode { Png = png, TilesX = cols, TilesY = rows, Transparency = "Magenta" } }, null);
				var picks = new Dictionary<string, List<int>> { ["idle"] = new() { 0 }, ["walk"] = new() { 0, 1, 2, 3 }, ["turn"] = new(), ["fall"] = new() { 4 }, ["drag"] = new(), ["blink"] = new() { 0, 5 }, ["sleep"] = new(), ["happy"] = new() { 6, 7 }, ["jump"] = new() { 4 } };
				var made = Wizard.Create(sheet, picks, "Teste", "Editor", 1, true);
				var problems = Validator.Run(made);
				Check("assistant: the pet has no errors", problems.All(p => !p.Error), string.Join(" | ", problems.Where(p => p.Error).Select(p => p.Text)));
				Check("assistant: it walks, turns, falls and can be picked up", new[] { "walk", "turn", "fall", "drag", "fall soft", "kill" }.All(n => made.Root.Animations.Animation.Any(a => a.Name == n)));
				var walkA = made.Root.Animations.Animation.First(a => a.Name == "walk");
				Check("assistant: walking turns at the sides and falls without ground", walkA.Border?.Next?.Length > 0 && walkA.Gravity?.Next?.Length > 0);
				Check("assistant: three entrances", made.Root.Spawns.Spawn.Length == 3);
				string xml = PetXmlWriter.Write(made.Root);
				File.WriteAllText(Path.Combine(tmp, "assistant.xml"), xml);
				Check("assistant: the file reads back", XmlData.AnimationXML.ParseXML(xml) != null);
				log.Add("INFO  assistant warnings: " + string.Join(" | ", problems.Select(p => p.Text)));

				// 3. a scene copied from Pingo into the new pet: frames, numbers and children come along
				int before = made.Root.Animations.Animation.Length, framesBefore = made.Frames.Count;
				var kick = pingo.Root.Animations.Animation.First(a => pingo.Root.Childs.Child.Any(ch => ch.Id == a.Id));
				int first = ImportDialog.Copy(made, pingo, new List<int> { kick.Id }, true);
				var copied = made.Anim(first);
				Check("copy: the animation is there with a new number", copied != null && copied.Name == kick.Name, copied?.Name ?? "none");
				Check("copy: more animations (what follows)", made.Root.Animations.Animation.Length > before + 1, $"{made.Root.Animations.Animation.Length - before} new");
				Check("copy: its child window too", made.Root.Childs.Child.Any(ch => ch.Id == first));
				Check("copy: frames added after the old ones", made.Frames.Count > framesBefore && copied.Sequence.Frame.All(f => f >= framesBefore && f < made.Frames.Count), $"{framesBefore} -> {made.Frames.Count}");
				var p2 = Validator.Run(made);
				Check("copy: still no errors", p2.All(p => !p.Error), string.Join(" | ", p2.Where(p => p.Error).Select(p => p.Text)));

				// 4. optimisation: unused frames go, numbers follow; the palette PNG keeps every pixel
				var opt = PetDoc.Open(Path.Combine(pets, "pingo", "animations.xml"));
				var keep = opt.Root.Animations.Animation.ToDictionary(a => a.Id, a => a.Sequence.Frame.Select(f => opt.Frame(f)).Select(b => b.GetPixel(b.Width / 2, b.Height / 2).ToArgb()).ToArray());
				int removed = Optimizer.RemoveUnused(opt);
				bool same = opt.Root.Animations.Animation.All(a => a.Sequence.Frame.Select(f => opt.Frame(f)).Select(b => b.GetPixel(b.Width / 2, b.Height / 2).ToArgb()).SequenceEqual(keep[a.Id]));
				Check("optimise: the animations show the same frames after removing unused ones", same, $"{removed} removed");
				int oldKb = Convert.FromBase64String(opt.Root.Image.Png).Length;
				int saved = Optimizer.Compress(opt);
				log.Add($"INFO  optimise: {removed} unused frames, PNG {oldKb / 1024} KB, palette saved {saved / 1024} KB");
				Check("optimise: the pet still reads back", XmlData.AnimationXML.ParseXML(PetXmlWriter.Write(opt.Root)) != null);

				// 5. the .zip to send: one folder with the five files, and the pet in it opens again the same
				var zdoc = PetDoc.Open(Path.Combine(pets, "pingo", "animations.xml"));
				string zip = Path.Combine(Path.GetTempPath(), "PetEditor-test.zip");
				GalleryDialog.WriteZip(zdoc, zip, "pingo");
				using (var z = System.IO.Compression.ZipFile.OpenRead(zip))
				{
					var names = z.Entries.Select(e => e.FullName.Replace('\\', '/')).OrderBy(n => n).ToList();
					var want = new[] { "pingo/README.md", "pingo/README.pt-BR.md", "pingo/animations.xml", "pingo/icon.png", "pingo/preview.gif" };
					Check("zip: one folder with animations.xml, icon, preview and READMEs", want.All(names.Contains), string.Join(", ", names));
					using var xr = new StreamReader(z.GetEntry("pingo/animations.xml").Open());
					var back = XmlData.AnimationXML.ParseXML(xr.ReadToEnd());
					Check("zip: the pet in it reads back with the same animations", back?.Animations.Animation.Length == zdoc.Root.Animations.Animation.Length, $"{back?.Animations.Animation.Length} animations");
				}
				File.Delete(zip);

				// 6. scale of the window (Windows zoom): sizes written at 100% grow, fonts too
				Check("zoom: a size at 100% grows with the zoom of Windows", Ui.S(100) == (int)Math.Round(100 * Ui.Scale), $"zoom {Ui.Scale}");
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			File.WriteAllLines(Path.Combine(Path.GetTempPath(), "PetEditor-devtests.txt"), log);
			return fails;
		}
	}
}
