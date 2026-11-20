using System.Drawing;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>A page with a list on the left (add / remove) and the editor of the chosen item on the right.</summary>
	internal abstract class ListPage<T> : Page where T : class
	{
		protected ListBox List;
		protected Panel EditorHost;
		protected bool Loading;
		Label countLbl;

		protected abstract string TitleKey { get; }
		protected abstract string IntroKey { get; }
		protected abstract T[] Items { get; set; }
		protected abstract T NewItem();
		protected abstract (string title, string sub) Describe(T item);
		protected abstract void BuildEditor(T item, FlowLayoutPanel col);
		protected virtual Control ExtraBelow() => null;
		protected virtual Image Thumb(T item) => null;
		/// <summary>Shown under the mouse (the whole text of the line, or more).</summary>
		protected virtual string TipOf(T item) { var (t, s) = Describe(item); return t + Environment.NewLine + s; }
		readonly ToolTip tip = new() { InitialDelay = 400, AutoPopDelay = 15000 };
		int tipIndex = -1;

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background };
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			var left = new Card { Dock = DockStyle.Fill };
			var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Surface };
			countLbl = Ui.Title("", 16); countLbl.Location = new Point(0, 4);
			var add = new FlatButton("+ " + L.T("list.add"));
			add.Click += (s, e) => { Items = Items.Append(NewItem()).ToArray(); Doc.Changed(TitleKey); Fill(); List.SelectedIndex = List.Items.Count - 1; };
			top.Controls.AddRange(new Control[] { countLbl, add });
			// at the right (placed at each layout: also after growing with the zoom of Windows)
			top.Layout += (s, e) => { var at = new Point(top.Width - add.Width, 0); if (add.Location != at) add.Location = at; };
			List = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Math.Max(Ui.S(52), Ui.Thumb + Ui.S(14)), BorderStyle = BorderStyle.None, BackColor = Theme.Surface, IntegralHeight = false };
			List.DrawItem += DrawItem;
			List.MouseMove += (s, e) =>
			{
				int i = List.IndexFromPoint(e.Location);
				if (i == tipIndex) return;
				tipIndex = i;
				if (i >= 0 && i < List.Items.Count) tip.SetToolTip(List, TipOf((T)List.Items[i])); else tip.SetToolTip(List, "");
			};
			List.SelectedIndexChanged += (s, e) => ShowItem(List.SelectedItem as T);
			List.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) Remove(); };
			left.Controls.Add(List);
			left.Controls.Add(top);
			root.Controls.Add(left, 0, 0);

			var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Background, Margin = new Padding(0) };
			var extra = ExtraBelow();
			right.RowStyles.Add(new RowStyle(SizeType.Percent, extra == null ? 100 : 55));
			right.RowStyles.Add(new RowStyle(SizeType.Percent, extra == null ? 0 : 45));
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			EditorHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
			card.Controls.Add(EditorHost);
			right.Controls.Add(card, 0, 0);
			if (extra != null) right.Controls.Add(extra, 0, 1);
			root.Controls.Add(right, 1, 0);
			Controls.Add(root);
			Fill();
			if (List.Items.Count > 0) List.SelectedIndex = 0; else ShowItem(null);
		}

		protected void Fill()
		{
			var sel = List.SelectedItem;
			List.BeginUpdate();
			List.Items.Clear();
			foreach (var i in Items) List.Items.Add(i);
			if (sel != null && List.Items.Contains(sel)) List.SelectedItem = sel;
			List.EndUpdate();
			countLbl.Text = L.T(TitleKey + ".count", Items.Length);
		}

		void DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0) return;
			var item = (T)List.Items[e.Index];
			var g = e.Graphics; Theme.Prepare(g);
			bool sel = (e.State & DrawItemState.Selected) != 0;
			using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, e.Bounds);
			var r = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);
			if (sel) { Theme.FillRounded(g, Theme.AccentSoft, r, 6); Theme.DrawRounded(g, Theme.Accent, r, 6); }
			var (title, sub) = Describe(item);
			int x = r.X + Ui.S(10);
			var img = Thumb(item);
			if (img != null) { int th = Math.Min(Ui.Thumb, r.Height - 6); Ui.DrawFrame(g, img, new Rectangle(r.X + Ui.S(6), r.Y + (r.Height - th) / 2, th, th)); x = r.X + th + Ui.S(14); }
			TextRenderer.DrawText(g, title, Ui.Font(13.5f, sel), new Rectangle(x, r.Y + r.Height / 2 - Ui.S(19), r.Right - x - 4, Ui.S(20)), Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			TextRenderer.DrawText(g, sub, Ui.Font(11.5f), new Rectangle(x, r.Y + r.Height / 2 + 1, r.Right - x - 4, Ui.S(18)), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
		}

		protected void ShowItem(T item)
		{
			Loading = true;
			EditorHost.SuspendLayout();
			foreach (Control c in EditorHost.Controls) c.Dispose();
			EditorHost.Controls.Clear();
			var col = Ui.Column(Ui.Title(L.T(TitleKey + ".title"), 20), Ui.Hint(L.T(IntroKey), 820));
			if (item == null) col.Controls.Add(Ui.Text(L.T(TitleKey + ".empty"), true));
			else
			{
				BuildEditor(item, col);
				var del = new FlatButton(L.T("list.remove")) { Danger = true };
				del.Click += (s, e) => Remove();
				col.Controls.Add(del);
			}
			col.Location = new Point(4, 4);
			EditorHost.Controls.Add(col);
			EditorHost.ResumeLayout();
			Loading = false;
		}

		void Remove()
		{
			if (List.SelectedItem is not T item) return;
			int i = List.SelectedIndex;
			Items = Items.Where(x => x != item).ToArray();
			Doc.Changed(TitleKey);
			Fill();
			if (List.Items.Count > 0) List.SelectedIndex = Math.Min(i, List.Items.Count - 1); else ShowItem(null);
		}

		/// <summary>The item changed: its line in the list is drawn again.</summary>
		protected void Touch(bool typing = true) { if (Loading) return; Doc.Changed(TitleKey, typing); List.Invalidate(); }

		protected ComboBox AnimCombo(int selected, Action<int> set, int width = 300)
		{
			var c = Ui.Combo(width);
			c.BeginUpdate(); c.Items.AddRange(Doc.Root.Animations.Animation.Select(a => (object)new AnimItem(a.Id, a.Name)).ToArray()); c.EndUpdate();
			c.SelectedItem = c.Items.Cast<AnimItem>().FirstOrDefault(x => x.Id == selected);
			c.SelectedIndexChanged += (s, e) => { if (c.SelectedItem is AnimItem ai) { set(ai.Id); Touch(false); } };
			return c;
		}

		protected TextBox Formula(string value, Action<string> set, int width = 360)
		{
			var t = Ui.Stretch(Ui.Box(value, width, mono: true), 160, 560);
			t.TextChanged += (s, e) => { set(t.Text); Touch(); };
			return t;
		}

		public override void Reload(string what)
		{
			if (what is "all" or "anims" or "image") { Fill(); ShowItem(List.SelectedItem as T); }
		}
	}

	// ====================================================================== entrances

	/// <summary>Entrances (spawns): where the pet appears on the screen and with which animation; tried in a preview.</summary>
	internal sealed class SpawnsPage : ListPage<SpawnNode>
	{
		PreviewStage stage;
		protected override string TitleKey => "spawns";
		protected override string IntroKey => "spawns.intro";
		protected override SpawnNode[] Items { get => Doc.Root.Spawns.Spawn; set => Doc.Root.Spawns.Spawn = value; }
		protected override SpawnNode NewItem() => new() { Id = Items.Length == 0 ? 1 : Items.Max(s => s.Id) + 1, Probability = 10, X = "random*(screenW-imageW)/100", Y = "areaH-imageH", Next = Doc.Root.Animations.Animation.FirstOrDefault()?.Id ?? 1 };
		protected override (string, string) Describe(SpawnNode s) => (L.T("spawns.item", s.Id, Doc.Anim(s.Next)?.Name ?? "?"), L.T("spawns.subText", s.Probability, Where(s)));
		protected override string TipOf(SpawnNode s) => string.Join(Environment.NewLine, L.T("spawns.item", s.Id, Doc.Anim(s.Next)?.Name ?? "?"), Where(s), L.T("spawns.sub", s.Probability, s.X, s.Y));

		/// <summary>Where the entrance puts the pet, in words (from the usual formulas).</summary>
		static string Where(SpawnNode s)
		{
			string x = (s.X ?? "").Replace(" ", ""), y = (s.Y ?? "").Replace(" ", "");
			string height = y.Contains("areaH-imageH") ? L.T("where.taskbar") : y.StartsWith("-imageH") || y.StartsWith("0-imageH") ? L.T("where.sky")
				: y.StartsWith("areaH") || y.StartsWith("screenH") ? L.T("where.below") : y.Contains("random") ? L.T("where.randomHeight") : L.T("where.height", y);
			string side = x.StartsWith("screenW") ? L.T("where.side") : x.Contains("random") ? L.T("where.random") : x == "0" || x.StartsWith("-imageW") ? L.T("where.edge") : L.T("where.x", x);
			return L.T("where.both", height, side);
		}
		protected override Image Thumb(SpawnNode s) => Doc.Anim(s.Next)?.Sequence.Frame is int[] f && f.Length > 0 ? Doc.Frame(f[0]) : null;

		protected override void BuildEditor(SpawnNode s, FlowLayoutPanel col)
		{
			var prob = Ui.Number(0, 1000, s.Probability, 90);
			prob.ValueChanged += (o, e) => { s.Probability = (int)prob.Value; Touch(); };
			col.Controls.Add(Ui.Row(Ui.Labeled(L.T("spawns.id"), Ui.Text("#" + s.Id)), Ui.Labeled(L.T("spawns.prob"), prob, L.T("spawns.probHint"))));
			col.Controls.Add(Ui.Labeled(L.T("spawns.x"), Formula(s.X, v => s.X = v), L.T("spawns.xHint")));
			col.Controls.Add(Ui.Labeled(L.T("spawns.y"), Formula(s.Y, v => s.Y = v), L.T("spawns.yHint")));
			col.Controls.Add(Ui.Labeled(L.T("spawns.next"), AnimCombo(s.Next, v => s.Next = v)));
			var quick = Ui.Row();
			foreach (var (key, x, y) in new[] { ("spawns.qGround", "random*(screenW-imageW)/100", "areaH-imageH"), ("spawns.qRight", "screenW", "areaH-imageH"), ("spawns.qSky", "random*(screenW-imageW)/100", "-imageH") })
			{
				var b = new FlatButton(L.T(key));
				b.Click += (o, e) => { s.X = x; s.Y = y; Touch(false); ShowItem(s); };
				quick.Controls.Add(b);
			}
			col.Controls.Add(Ui.Text(L.T("spawns.quick"), true, 12));
			col.Controls.Add(quick);
			var test = new FlatButton("▶  " + L.T("spawns.try"), primary: true);
			test.Click += (o, e) => stage.PlaySpawn(s);
			col.Controls.Add(test);
			// the chosen entrance plays by itself
			if (IsHandleCreated) BeginInvoke(new Action(() => { if (Doc.Frames.Count > 0 && Visible) stage?.PlaySpawn(s); }));
		}

		protected override Control ExtraBelow()
		{
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			stage = new PreviewStage { Dock = DockStyle.Fill, Doc = Doc };
			card.Controls.Add(stage);
			card.Controls.Add(new Label { Text = L.T("preview.title"), Dock = DockStyle.Top, Height = 28, Font = Ui.Font(15, true), ForeColor = Theme.Text, BackColor = Theme.Surface });
			return card;
		}

		public override void Hidden() => stage?.Stop();
	}

	// ====================================================================== children

	/// <summary>Child windows: a second picture (a ball, a bathtub, a ship) that appears when an animation starts.</summary>
	internal sealed class ChildsPage : ListPage<ChildNode>
	{
		protected override string TitleKey => "childs";
		protected override string IntroKey => "childs.intro";
		protected override ChildNode[] Items { get => Doc.Root.Childs.Child; set => Doc.Root.Childs.Child = value; }
		protected override ChildNode NewItem() { int first = Doc.Root.Animations.Animation.FirstOrDefault()?.Id ?? 1; return new ChildNode { Id = first, X = "imageX", Y = "imageY", Next = first }; }
		protected override (string, string) Describe(ChildNode c) => (L.T("childs.item", Doc.Anim(c.Id)?.Name ?? "?", Doc.Anim(c.Next)?.Name ?? "?"), $"x: {c.X}   y: {c.Y}");
		protected override Image Thumb(ChildNode c) => Doc.Anim(c.Next)?.Sequence.Frame is int[] f && f.Length > 0 ? Doc.Frame(f[0]) : null;

		protected override void BuildEditor(ChildNode c, FlowLayoutPanel col)
		{
			col.Controls.Add(Ui.Labeled(L.T("childs.when"), AnimCombo(c.Id, v => c.Id = v), L.T("childs.whenHint")));
			col.Controls.Add(Ui.Labeled(L.T("childs.anim"), AnimCombo(c.Next, v => c.Next = v), L.T("childs.animHint")));
			col.Controls.Add(Ui.Labeled(L.T("childs.x"), Formula(c.X, v => c.X = v), L.T("childs.xHint")));
			col.Controls.Add(Ui.Labeled(L.T("childs.y"), Formula(c.Y, v => c.Y = v), L.T("childs.yHint")));
			var go = new FlatButton(L.T("childs.open"));
			go.Click += (o, e) => Main.Go("animations", c.Next);
			col.Controls.Add(go);
		}
	}

	// ====================================================================== sounds

	/// <summary>Sounds: an MP3 that may play when an animation starts.</summary>
	internal sealed class SoundsPage : ListPage<SoundNode>
	{
		protected override string TitleKey => "sounds";
		protected override string IntroKey => "sounds.intro";
		protected override SoundNode[] Items { get => Doc.Root.Sounds.Sound; set => Doc.Root.Sounds.Sound = value; }
		protected override SoundNode NewItem() => new() { Id = Doc.Root.Animations.Animation.FirstOrDefault()?.Id ?? 1, Probability = 100 };
		protected override (string, string) Describe(SoundNode s) => (Doc.Anim(s.Id)?.Name ?? $"#{s.Id} ?",
			L.T("sounds.sub", s.Probability, string.IsNullOrWhiteSpace(s.Base64) ? L.T("sounds.noFile") : $"{s.Base64.Length * 3 / 4 / 1024} KB"));

		protected override void BuildEditor(SoundNode s, FlowLayoutPanel col)
		{
			col.Controls.Add(Ui.Labeled(L.T("sounds.anim"), AnimCombo(s.Id, v => s.Id = v)));
			var prob = Ui.Number(0, 100, s.Probability, 90);
			prob.ValueChanged += (o, e) => { s.Probability = (int)prob.Value; Touch(); };
			var loop = Ui.Number(0, 50, s.Loop, 90);
			loop.ValueChanged += (o, e) => { s.Loop = (int)loop.Value; Touch(); };
			col.Controls.Add(Ui.Row(Ui.Labeled(L.T("sounds.prob"), prob, L.T("sounds.probHint")), Ui.Labeled(L.T("sounds.loop"), loop, L.T("sounds.loopHint"))));
			var import = new FlatButton(L.T("sounds.import"), primary: string.IsNullOrWhiteSpace(s.Base64));
			import.Click += (o, e) =>
			{
				using var dlg = new OpenFileDialog { Filter = L.T("sounds.filter") };
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				var bytes = File.ReadAllBytes(dlg.FileName);
				if (bytes.Length > 600 * 1024 && MessageBox.Show(this, L.T("sounds.big", bytes.Length / 1024), L.T("app.name"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
				s.Base64 = Convert.ToBase64String(bytes);
				Touch(false);
				ShowItem(s);
			};
			var play = new FlatButton("▶  " + L.T("sounds.play")) { Enabled = !string.IsNullOrWhiteSpace(s.Base64) };
			play.Click += (o, e) => SoundPlayer.Play(s);
			var export = new FlatButton(L.T("sounds.export")) { Enabled = !string.IsNullOrWhiteSpace(s.Base64) };
			export.Click += (o, e) =>
			{
				using var dlg = new SaveFileDialog { Filter = "MP3|*.mp3", FileName = (Doc.Anim(s.Id)?.Name ?? "sound") + ".mp3" };
				if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllBytes(dlg.FileName, Convert.FromBase64String(s.Base64.Trim()));
			};
			col.Controls.Add(Ui.Row(import, play, export));
		}
	}
}
