using System.Drawing;
using System.Reflection;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// Developer: "--wikishots=&lt;folder&gt;" takes the pictures of the wiki: every page, every window, the assistant step by
	/// step, the preview with its test window, the checker with problems, the map of a small pet and the tour. The
	/// name of the Windows user and its folder are replaced by neutral words before each picture.
	/// </summary>
	internal sealed partial class MainForm
	{
		async Task WikiShots(string dir)
		{
			Directory.CreateDirectory(dir);
			WindowState = FormWindowState.Normal;
			Size = Program.SnapSize.IsEmpty ? Ui.S(new Size(1360, 860)) : Program.SnapSize;
			string original = Doc.FilePath;

			// 1. every page of the pet
			foreach (var key in new[] { "project", "header", "sheet", "animations", "spawns", "childs", "sounds", "check", "map", "xml", "settings" })
			{
				nav.Select(key);
				await Task.Delay(key == "animations" ? 1500 : 700);
				Shot(this, dir, "page-" + key);
			}

			// 2. the animations: what comes next (the "on a border" tab) and the preview with its test window
			nav.Select("animations");
			await Task.Delay(800);
			var border = Every(this).OfType<Label>().FirstOrDefault(l => l.Text.StartsWith(L.T("next.border")));
			if (border != null)
			{
				ClickOn(border);
				await Task.Delay(400);
				if (Ancestor<ScrollableControl>(border, s => s.AutoScroll) is ScrollableControl host) host.ScrollControlIntoView(border);
				await Task.Delay(500);
				Shot(this, dir, "animations-next");
			}
			var stage = Every(this).OfType<PreviewStage>().FirstOrDefault();
			if (stage != null)
			{
				stage.ToggleWindow(true);
				stage.Onion = true;
				await Task.Delay(2500);
				Shot(this, dir, "preview-test-window");
				stage.ToggleWindow(false);
				stage.Onion = false;
			}

			// 3. a copy with problems: the checker, the marks in the list and the numbers in the side bar
			var broken = PetDoc.Open(original);
			var anims = broken.Root.Animations.Animation;
			if (anims.Length > 4)
			{
				anims[1].Sequence.Next = new[] { new XmlData.NextNode { Value = 999, Probability = 10, OnlyFlag = "none" } };
				anims[2].Start.Interval = "0"; anims[2].End.Interval = "0";
				anims[3].Name = anims[0].Name;
			}
			SetDoc(broken, "check");
			await Task.Delay(1500);
			Shot(this, dir, "checker-problems");
			nav.Select("animations");
			await Task.Delay(1200);
			Shot(this, dir, "animations-problems");

			// 4. a small pet (the model of "New pet"): its animation map can be read
			SetDoc(Template.New(), "map");
			await Task.Delay(1500);
			Shot(this, dir, "map-small-pet");
			nav.Select("animations");
			await Task.Delay(1200);
			Shot(this, dir, "new-pet-model");

			// 5. back to the pet: the tour (the window and its balloon in one picture)
			SetDoc(PetDoc.Open(original), "project");
			await Task.Delay(1200);
			Tour.Start(this);
			for (int step = 1; step <= 4; step++)
			{
				await Task.Delay(900);
				var bubble = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "Bubble");
				if (bubble == null) break;
				if (step is 1 or 4) Shot(this, dir, "tour-" + step, bubble);
				Every(bubble).OfType<FlatButton>().FirstOrDefault(b => b.Text == L.T("tour.next"))?.PerformClick();
			}
			Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.GetType().Name == "Bubble")?.Close();
			nav.Select("project");
			await Task.Delay(600);

			// 6. the assistant, step by step
			string pics = Path.Combine(Path.GetTempPath(), "PetEditor-wiki");
			Directory.CreateDirectory(pics);
			var files = new List<string>();
			for (int i = 0; i < Math.Min(8, Doc.Frames.Count); i++) { string p = Path.Combine(pics, $"frame_{i + 1}.png"); Doc.Frame(i)?.Save(p); files.Add(p); }
			await Dialog(() => Wizard.Show(this, files.ToArray()), async f =>
			{
				Shot(f, dir, "assistant-1");
				Every(f).OfType<FlatButton>().First(b => b.Text == L.T("wiz.next")).PerformClick();
				await Task.Delay(700);
				var roles = Every(f).OfType<ListBox>().First();
				var view = Every(f).OfType<SheetView>().First();
				void Pick(int role, params int[] frames) { roles.SelectedIndex = role; foreach (var k in frames) view.ClickFrame(k); }
				Pick(0, 0); Pick(1, 1, 2, 3, 2); Pick(3, 4);
				roles.SelectedIndex = 1;
				await Task.Delay(600);
				Shot(f, dir, "assistant-2");
				Every(f).OfType<FlatButton>().First(b => b.Text == L.T("wiz.next")).PerformClick();
				await Task.Delay(700);
				Shot(f, dir, "assistant-3");
			});

			// 7. the other windows
			var dialogs = new (string key, Action open)[]
			{
				("ready-made-animations", () => BehaviorDialog.Show(this, Doc)),
				("copy-from-another-pet", () => ImportDialog.Show(this, Doc, Doc.FilePath)),
				("build-sheet", () => SheetBuilder.Show(this, Doc, files.ToArray())),
				("frame-picker", () => FramePicker.Pick(this, Doc, L.T("head.iconFrame"))),
				("pixel-editor", () => PixelEditor.Show(this, Doc, 0)),
				("skin", () => SkinMaker.Show(this, Doc)),
				("optimizer", () => Optimizer.Show(this, Doc)),
				("gallery", () => GalleryDialog.Show(this, Doc)),
			};
			foreach (var (key, open) in dialogs)
				await Dialog(open, f => { Shot(f, dir, key); return Task.CompletedTask; });

			// 8. the dark theme
			Theme.Mode = "dark";
			ThemeChanged();
			nav.Select("animations");
			await Task.Delay(1500);
			Shot(this, dir, "dark-theme");
			Environment.Exit(0);
		}

		/// <summary>Open a window of the editor, let <paramref name="work"/> picture it, then close it.</summary>
		async Task Dialog(Action open, Func<Form, Task> work)
		{
			var done = new TaskCompletionSource();
			void Opened(Form f)
			{
				Ui.DialogShown -= Opened;
				f.BeginInvoke(new Action(async () =>
				{
					await Task.Delay(800);
					try { await work(f); } catch (Exception e) { File.AppendAllText(Path.Combine(Program.WikiDir, "erros.txt"), e + Environment.NewLine); }
					f.DialogResult = DialogResult.Cancel;
					f.Close();
					done.TrySetResult();
				}));
			}
			Ui.DialogShown += Opened;
			BeginInvoke(open);
			if (await Task.WhenAny(done.Task, Task.Delay(15000)) != done.Task) Ui.DialogShown -= Opened;
			await Task.Delay(400);
		}

		/// <summary>A picture of a window (and, drawn over it, a balloon of the tour), without the name of the Windows user.</summary>
		static void Shot(Form f, string dir, string name, Form over = null)
		{
			Clean(f);
			if (over != null) Clean(over);
			using var b = new Bitmap(f.Width, f.Height);
			f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
			if (over != null)
			{
				using var o = new Bitmap(over.Width, over.Height);
				over.DrawToBitmap(o, new Rectangle(0, 0, over.Width, over.Height));
				using var g = Graphics.FromImage(b);
				g.DrawImage(o, over.Left - f.Left, over.Top - f.Top);
			}
			b.Save(Path.Combine(dir, name + ".png"));
		}

		/// <summary>The user's folder and name become neutral words in every text (the pictures are published).</summary>
		static void Clean(Control root)
		{
			string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string shortTemp = Path.GetTempPath();
			string user = Environment.UserName;
			foreach (var c in Every(root).Prepend(root))
			{
				string t = c.Text;
				if (string.IsNullOrEmpty(t)) continue;
				string n = t.Replace(profile, @"C:\Users\you", StringComparison.OrdinalIgnoreCase)
					.Replace(profile.Replace('\\', '/'), "C:/Users/you", StringComparison.OrdinalIgnoreCase)
					.Replace(shortTemp, @"C:\Users\you\AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase);
				foreach (var who in new[] { user, Path.GetFileName(profile) }) if (who.Length > 2) n = n.Replace(who, "you", StringComparison.OrdinalIgnoreCase);
				if (n != t && c is not Form) c.Text = n;
			}
		}

		static IEnumerable<Control> Every(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var k in Every(c)) yield return k; } }

		static T Ancestor<T>(Control c, Func<T, bool> ok) where T : Control
		{
			for (var p = c.Parent; p != null; p = p.Parent) if (p is T t && ok(t)) return t;
			return null;
		}

		/// <summary>As a click of the mouse on a control (its Click event).</summary>
		static void ClickOn(Control c) => typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(c, new object[] { EventArgs.Empty });
	}
}
