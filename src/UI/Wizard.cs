using System.Drawing;
using System.Drawing.Imaging;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// New pet in three steps: the pictures (or a GIF), which frames are standing, walking, falling…, and a name.
	/// The pet that comes out already works: it walks, turns at the sides, falls from the sky, can be picked up and
	/// now and then blinks, sleeps or shows it is happy.
	/// </summary>
	internal static class Wizard
	{
		sealed record Role(string Key, int Min);
		static readonly Role[] Roles =
		{
			new("idle", 1), new("walk", 2), new("turn", 0), new("fall", 0), new("drag", 0), new("blink", 0), new("sleep", 0), new("happy", 0), new("jump", 0),
		};

		/// <summary>The assistant; <paramref name="files"/>: pictures already chosen (dropped on the window).</summary>
		public static void Show(MainForm main, string[] files = null)
		{
			if (!main.ConfirmDiscard()) return;
			PetDoc sheet = null;   // the frames being used (a pet with only a sheet)
			var picks = Roles.ToDictionary(r => r.Key, r => new List<int>());
			int step = 0;
			string role = "idle";

			using var f = new Form { Text = L.T("wiz.title"), Size = new Size(1040, 700), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Background, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var head = new Label { Dock = DockStyle.Top, Height = 60, Font = Ui.Font(18, true), ForeColor = Theme.Text, BackColor = Theme.Surface, Padding = new Padding(18, 16, 0, 0) };
			var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(14) };
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(14, 10, 14, 0) };
			var next = new FlatButton(L.T("wiz.next"), primary: true);
			var back = new FlatButton(L.T("wiz.back"));
			var cancel = new FlatButton(L.T("build.cancel")); cancel.Click += (s, e) => f.Close();
			bar.Controls.AddRange(new Control[] { next, back, cancel });
			f.Controls.Add(body); f.Controls.Add(bar); f.Controls.Add(head);

			bool mirror = false;
			var name = Ui.Box(L.T("tpl.name"), 260);
			var author = Ui.Box(Environment.UserName, 260);
			int speed = 1;
			var extras = new Toggle(L.T("wiz.climb"), false);

			void LoadFrames(List<Bitmap> frames)
			{
				if (mirror) foreach (var b in frames) b.RotateFlip(RotateFlipType.RotateNoneFlipX);
				var (png, c, r) = SheetBuilder.Build(frames, Math.Min(8, frames.Count));
				foreach (var b in frames) b.Dispose();
				var root = new RootNode { Image = new ImageNode { Png = png, TilesX = c, TilesY = r, Transparency = "Magenta" } };
				sheet = new PetDoc(root, null);
				foreach (var l in picks.Values) l.Clear();
			}

			void Build()
			{
				body.SuspendLayout();
				foreach (Control c in body.Controls) c.Dispose();
				body.Controls.Clear();
				back.Visible = step > 0;
				next.Text = L.T(step == 2 ? "wiz.create" : "wiz.next");
				head.Text = L.T("wiz.step" + step);
				var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0) };
				if (step == 0)
				{
					var col = Ui.Column(Ui.Hint(L.T("wiz.intro0"), 900));
					var pick = new FlatButton(L.T("wiz.pick"), primary: true);
					pick.Click += (s, e) =>
					{
						using var dlg = new OpenFileDialog { Filter = L.T("build.filter"), Multiselect = true };
						if (dlg.ShowDialog(f) != DialogResult.OK) return;
						try { LoadFrames(SheetBuilder.Load(SheetBuilder.InOrder(dlg.FileNames))); }
						catch (Exception ex) { main.Message(L.T("msg.openFail", ex.Message)); }
						Build();
					};
					var useSheet = new FlatButton(L.T("wiz.useSheet")) { Visible = main.Doc?.Frames.Count > 0 };
					useSheet.Click += (s, e) => { LoadFrames(main.Doc.CopyFrames()); Build(); };
					col.Controls.Add(Ui.Row(pick, useSheet));
					var face = Ui.Combo(260);
					face.Items.AddRange(new object[] { L.T("wiz.faceLeft"), L.T("wiz.faceRight") });
					face.SelectedIndex = mirror ? 1 : 0;
					face.SelectedIndexChanged += (s, e) =>
					{
						bool m = face.SelectedIndex == 1;
						if (m == mirror) return;
						mirror = m;
						if (sheet != null) { var fr = sheet.CopyFrames(); foreach (var b in fr) b.RotateFlip(RotateFlipType.RotateNoneFlipX); mirror = false; LoadFrames(fr); mirror = m; Build(); }
					};
					col.Controls.Add(Ui.Labeled(L.T("wiz.face"), face, L.T("wiz.faceHint")));
					col.Controls.Add(Ui.Text(sheet == null ? L.T("wiz.none") : L.T("wiz.loaded", sheet.Frames.Count, sheet.FrameW, sheet.FrameH), sheet == null));
					col.Dock = DockStyle.Top;
					card.Controls.Add(new SheetView { Dock = DockStyle.Fill, Doc = sheet });
					card.Controls.Add(col);
				}
				else if (step == 1)
				{
					// roles on the left, the frames of the chosen role and the sheet on the right
					var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Surface };
					grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
					grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
					var roles = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(50), BorderStyle = BorderStyle.None, BackColor = Theme.Surface, IntegralHeight = false };
					foreach (var r in Roles) roles.Items.Add(r);
					roles.DrawItem += (s, e) =>
					{
						if (e.Index < 0) return;
						var r = (Role)roles.Items[e.Index];
						bool sel = (e.State & DrawItemState.Selected) != 0;
						using (var b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
						int n = picks[r.Key].Count;
						string state = n > 0 ? "✓ " + L.T("wiz.nFrames", n) : r.Min > 0 ? L.T("wiz.required", r.Min) : L.T("wiz.optional");
						TextRenderer.DrawText(e.Graphics, L.T("beh." + r.Key), Ui.Font(13.5f, true), new Rectangle(e.Bounds.X + Ui.S(10), e.Bounds.Y + Ui.S(5), e.Bounds.Width, Ui.S(20)), sel ? Theme.Accent : Theme.Text, TextFormatFlags.Left);
						TextRenderer.DrawText(e.Graphics, state, Ui.Font(11.5f), new Rectangle(e.Bounds.X + Ui.S(10), e.Bounds.Y + Ui.S(26), e.Bounds.Width, Ui.S(18)), n > 0 ? Theme.Success : r.Min > 0 ? Theme.Danger : Theme.TextMuted, TextFormatFlags.Left);
					};
					var right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
					var hint = Ui.Hint("", 640);
					var strip = new FrameStrip { Doc = sheet, Width = 640 };
					var top = Ui.Column(hint, strip);
					top.Dock = DockStyle.Top;
					var view = new SheetView { Dock = DockStyle.Fill, Doc = sheet };
					void ShowRole()
					{
						strip.Frames = picks[role]; strip.Fit(); strip.Invalidate();
						hint.Text = L.T("wiz.roleHint", L.T("beh." + role)) + "  " + L.T("beh." + role + ".d");
						view.Marked = picks[role].ToHashSet(); view.Invalidate();
					}
					roles.SelectedIndexChanged += (s, e) => { if (roles.SelectedItem is Role r) { role = r.Key; ShowRole(); } };
					view.FrameClicked += i => { picks[role].Add(i); ShowRole(); roles.Invalidate(); };
					strip.Changed += () => { ShowRole(); roles.Invalidate(); };
					right.Controls.Add(view); right.Controls.Add(top);
					grid.Controls.Add(roles, 0, 0); grid.Controls.Add(right, 1, 0);
					card.Controls.Add(grid);
					roles.SelectedIndex = Math.Max(0, Array.FindIndex(Roles, r => r.Key == role));
				}
				else
				{
					var col = Ui.Column(Ui.Hint(L.T("wiz.intro2"), 900));
					col.Controls.Add(Ui.Labeled(L.T("head.petname"), name, L.T("head.petnameHint")));
					col.Controls.Add(Ui.Labeled(L.T("head.author"), author));
					var sp = Ui.Combo(200);
					sp.Items.AddRange(new object[] { L.T("wiz.slow"), L.T("wiz.normal"), L.T("wiz.fast") });
					sp.SelectedIndex = speed;
					sp.SelectedIndexChanged += (s, e) => speed = sp.SelectedIndex;
					col.Controls.Add(Ui.Labeled(L.T("wiz.speed"), sp));
					col.Controls.Add(extras);
					col.Controls.Add(Ui.Hint(L.T("wiz.after"), 900));
					col.Location = new Point(16, 14);
					card.Controls.Add(col);
				}
				body.Controls.Add(card);
				body.ResumeLayout();
			}

			back.Click += (s, e) => { step--; Build(); };
			next.Click += (s, e) =>
			{
				if (step == 0 && sheet == null) { main.Message(L.T("wiz.needPictures")); return; }
				if (step == 1)
				{
					var missing = Roles.FirstOrDefault(r => picks[r.Key].Count < r.Min);
					if (missing != null) { main.Message(L.T("wiz.needRole", L.T("beh." + missing.Key), missing.Min)); return; }
				}
				if (step < 2) { step++; Build(); return; }
				var doc = Create(sheet, picks, name.Text.Trim(), author.Text.Trim(), new[] { 0.7, 1.0, 1.5 }[speed], extras.On);
				f.Close();
				main.SetDoc(doc, "animations");
			};
			if (files?.Length > 0)
			{
				try { LoadFrames(SheetBuilder.Load(SheetBuilder.InOrder(files))); }
				catch (Exception ex) { main.Message(L.T("msg.openFail", ex.Message)); }
			}
			Build();
			f.ShowDialog(main);
		}

		/// <summary>The new pet: header, sheet, behaviours for the roles and three entrances.</summary>
		internal static PetDoc Create(PetDoc sheet, Dictionary<string, List<int>> picks, string name, string author, double speed, bool climb)
		{
			int[] P(string key, string fallback = "idle") => (picks[key].Count > 0 ? picks[key] : picks[fallback].Count > 0 ? picks[fallback] : picks["idle"]).ToArray();
			if (string.IsNullOrWhiteSpace(name)) name = L.T("tpl.name");
			var root = new RootNode
			{
				Header = new HeaderNode { Author = author, Title = name, Petname = name.Length > 16 ? name.Substring(0, 16) : name, Version = "1.0", Info = L.T("tpl.info"), Application = "1", Icon = Icons.MakeIco(sheet.Frame(picks["idle"][0])) },
				Image = sheet.Root.Image,
				Spawns = new SpawnsNode { Spawn = Array.Empty<SpawnNode>() },
				Animations = new AnimationsNode { Animation = Array.Empty<AnimationNode>() },
				Childs = new ChildsNode { Child = Array.Empty<ChildNode>() },
				Sounds = new SoundsNode { Sound = Array.Empty<SoundNode>() },
			};
			var walk = Behaviors.Create(root, "walk", P("walk"), speed: speed);
			Behaviors.Create(root, "turn", picks["turn"].Count > 0 ? P("turn") : new[] { P("idle")[0], P("walk")[0] }, speed: speed);
			var fall = Behaviors.Create(root, "fall", P("fall"), speed: speed);
			Behaviors.Create(root, "land", P("idle"), speed: speed);
			Behaviors.Create(root, "drag", P("drag", "fall"), speed: speed);
			var idle = Behaviors.Create(root, "idle", P("idle"), speed: speed, lottery: 15);
			if (picks["blink"].Count > 0) Behaviors.Create(root, "blink", P("blink"), speed: speed, lottery: 12);
			if (picks["sleep"].Count > 0) Behaviors.Create(root, "sleep", P("sleep"), speed: speed, lottery: 4);
			if (picks["happy"].Count > 0) Behaviors.Create(root, "happy", P("happy"), speed: speed, lottery: 6);
			if (picks["jump"].Count > 0) Behaviors.Create(root, "jump", P("jump"), speed: speed, lottery: 5);
			if (climb) Behaviors.Create(root, "climb", P("walk"), speed: speed);
			Behaviors.Create(root, "kill", P("idle"), speed: speed);
			root.Spawns.Spawn = new[]
			{
				new SpawnNode { Id = 1, Probability = 40, X = "screenW", Y = "areaH-imageH", Next = walk.Id },
				new SpawnNode { Id = 2, Probability = 30, X = "random*(screenW-imageW-50)/100+25", Y = "-imageH", Next = fall.Id },
				new SpawnNode { Id = 3, Probability = 30, X = "random*(screenW-imageW-50)/100+25", Y = "areaH-imageH", Next = idle.Id },
			};
			var doc = new PetDoc(root, null);
			doc.Changed("all");
			return doc;
		}
	}
}
