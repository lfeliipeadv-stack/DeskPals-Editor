using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>Settings of the editor: theme, accent colour, language, saving, preview and the DesktopPet used for tests.</summary>
	internal sealed class SettingsPage : Page
	{
		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Build();
		}

		void Build()
		{
			SuspendLayout();
			Controls.Clear();
			var s = EditorSettings.Current;
			var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };
			var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Background };
			col.Controls.Add(new Label { Text = L.T("set.title"), AutoSize = true, Font = Ui.Font(22, true), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 10) });

			// look
			var look = Section(col, L.T("set.look"));
			var theme = Segmented(new[] { ("auto", L.T("set.themeAuto")), ("light", L.T("set.themeLight")), ("dark", L.T("set.themeDark")) }, s.Theme, v =>
			{
				s.Theme = v; EditorSettings.Save();
				Theme.Mode = v;
				Main.Retranslate();   // rebuild with the new colours even when only the accent changed
			});
			look.Controls.Add(Ui.Labeled(L.T("set.theme"), theme));
			var swatches = Ui.Wrap(new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, 6) });
			foreach (var (id, color) in Theme.Accents)
			{
				var sw = new Swatch(color, id == s.Accent);
				sw.Click += (o, e) => { s.Accent = id; EditorSettings.Save(); Theme.AccentId = id; Main.Retranslate(); };
				swatches.Controls.Add(sw);
			}
			look.Controls.Add(Ui.Labeled(L.T("set.accent"), swatches));
			var lang = Ui.Combo(220);
			// Portuguese and English for now (the Spanish texts are kept for later)
			var langs = new[] { ("auto", L.T("set.langAuto")), ("pt", "Português"), ("en", "English") };
			foreach (var l in langs) lang.Items.Add(l.Item2);
			lang.SelectedIndex = Math.Max(0, Array.FindIndex(langs, l => l.Item1 == s.Language));
			lang.SelectedIndexChanged += (o, e) => { s.Language = langs[lang.SelectedIndex].Item1; EditorSettings.Save(); L.Use(s.Language); Main.Retranslate(); };
			look.Controls.Add(Ui.Labeled(L.T("set.lang"), lang));
			// the size of the editor: as the zoom of Windows or a size of its own (letters, buttons and boxes)
			var sizes = new[] { 0, 80, 90, 100, 110, 125, 150 };
			var size = Ui.Combo(280);
			foreach (var p in sizes) size.Items.Add(p == 0 ? L.T("set.uiSizeAuto", (int)Math.Round(Ui.WindowsZoom * 100)) : p + "%");
			size.SelectedIndex = Math.Max(0, Array.IndexOf(sizes, s.UiSize));
			size.SelectedIndexChanged += (o, e) =>
			{
				if (sizes[size.SelectedIndex] == s.UiSize) return;
				s.UiSize = sizes[size.SelectedIndex]; EditorSettings.Save();
				// everything is built at the size of the start: the editor opens again to take the new one
				if (MessageBox.Show(Main, L.T("set.uiSizeRestart"), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					Main.Restart();
			};
			look.Controls.Add(Ui.Labeled(L.T("set.uiSize"), size, L.T("set.uiSizeHint")));
			var thumbs = Ui.Combo(180);
			thumbs.Items.AddRange(new object[] { L.T("set.small"), L.T("set.medium"), L.T("set.large") });
			thumbs.SelectedIndex = Math.Max(0, Math.Min(2, s.Thumbs));
			thumbs.SelectedIndexChanged += (o, e) => { s.Thumbs = thumbs.SelectedIndex; EditorSettings.Save(); Main.Retranslate(); };
			look.Controls.Add(Ui.Labeled(L.T("set.thumbs"), thumbs));

			// files
			var files = Section(col, L.T("set.files"));
			files.Controls.Add(Switch(L.T("set.openLast"), s.OpenLast, v => s.OpenLast = v));
			files.Controls.Add(Switch(L.T("set.backup"), s.Backup, v => s.Backup = v));
			files.Controls.Add(Switch(L.T("set.extras"), s.WriteExtras, v => s.WriteExtras = v));
			var clear = new FlatButton(L.T("set.clearRecent"));
			clear.Click += (o, e) => { s.Recent.Clear(); EditorSettings.Save(); Main.Message(L.T("set.cleared")); };
			files.Controls.Add(clear);

			// preview
			var prev = Section(col, L.T("set.preview"));
			var back = Segmented(new[] { ("sky", L.T("set.backSky")), ("checker", L.T("set.backChecker")), ("dark", L.T("set.backDark")) }, s.PreviewBack, v => { s.PreviewBack = v; EditorSettings.Save(); });
			prev.Controls.Add(Ui.Labeled(L.T("set.back"), back));
			var zoom = Segmented(new[] { ("1", "1x"), ("2", "2x"), ("3", "3x") }, s.PreviewZoom.ToString(), v => { s.PreviewZoom = int.Parse(v); EditorSettings.Save(); });
			prev.Controls.Add(Ui.Labeled(L.T("set.zoom"), zoom, L.T("set.zoomHint")));
			prev.Controls.Add(Switch(L.T("set.sound"), s.PreviewSound, v => s.PreviewSound = v));

			// test on the desktop
			var test = Section(col, L.T("set.test"));
			test.Controls.Add(Ui.Hint(L.T("set.testHint"), 760));
			var path = Ui.Stretch(Ui.Box(s.DesktopPetPath, 520), 200, 520);
			path.PlaceholderText = DesktopTest.FindDesktopPet() ?? L.T("set.notFound");
			path.TextChanged += (o, e) => { s.DesktopPetPath = path.Text.Trim(); EditorSettings.Save(); };
			var browse = new FlatButton(L.T("set.browse"));
			browse.Click += (o, e) =>
			{
				using var dlg = new OpenFileDialog { Filter = "DesktopPet.exe|DesktopPet.exe|*.exe|*.exe" };
				if (dlg.ShowDialog(this) == DialogResult.OK) path.Text = dlg.FileName;
			};
			test.Controls.Add(Ui.Row(Ui.Labeled(L.T("set.appPath"), path), browse));
			var found = DesktopTest.FindDesktopPet();
			test.Controls.Add(Ui.Text(found != null ? "✓ " + L.T("set.found", found) : "⚠ " + L.T("set.notFound"), true, 12));

			// about
			var about = Section(col, L.T("set.about"));
			about.Controls.Add(Ui.Text(L.T("set.aboutText", Application.ProductVersion.Split('+')[0]), false));
			about.Controls.Add(Ui.Hint(L.T("set.keys"), 760));
			var tour = new FlatButton(L.T("set.tour"));
			tour.Click += (o, e) => Tour.Start(Main);
			about.Controls.Add(tour);

			scroll.Controls.Add(col);
			Controls.Add(scroll);
			ResumeLayout();
		}

		static FlowLayoutPanel Section(FlowLayoutPanel parent, string title)
		{
			// as wide as there is room for (up to 860 px), as tall as its content
			var card = Ui.Stretch(new Card { Width = 860, Height = 100 }, 320, 860);
			var inner = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(16, 14), BackColor = Theme.Surface };
			inner.Controls.Add(Ui.Title(title, 16));
			card.Controls.Add(inner);
			inner.SizeChanged += (s, e) => card.Height = inner.Bottom + Ui.S(18);
			parent.Controls.Add(card);
			return inner;
		}

		static Toggle Switch(string text, bool value, Action<bool> set)
		{
			var t = new Toggle(text, value);
			t.Changed += (o, e) => { set(t.On); EditorSettings.Save(); };
			return t;
		}

		/// <summary>A row of buttons where one is on (theme, background, zoom).</summary>
		static Control Segmented((string id, string text)[] options, string current, Action<string> set)
		{
			var row = Ui.Wrap(new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Surface, Margin = new Padding(0, 0, 0, 6) });
			foreach (var (id, text) in options)
			{
				var b = new FlatButton(text, primary: id == current);
				b.Click += (o, e) =>
				{
					foreach (FlatButton x in row.Controls) { x.Primary = x == b; x.Font = Ui.Font(13, x.Primary); x.Invalidate(); }
					set(id);
				};
				row.Controls.Add(b);
			}
			return row;
		}

		sealed class Swatch : Control
		{
			readonly Color color; readonly bool on;
			public Swatch(Color c, bool selected)
			{
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
				color = c; on = selected; Size = new Size(34, 34); Cursor = Cursors.Hand; Margin = new Padding(0, 0, 6, 0);
			}
			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics; Theme.Prepare(g); g.Clear(Theme.Surface);
				using var b = new SolidBrush(color);
				float k = Width / 34f; g.FillEllipse(b, 4 * k, 4 * k, 25 * k, 25 * k);
				if (on) using (var p = new Pen(Theme.Text, 2 * k)) g.DrawEllipse(p, k, k, 31 * k, 31 * k);
			}
		}
	}
}
