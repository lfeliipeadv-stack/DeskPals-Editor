using System.Drawing;
using System.Xml.Serialization;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// Animations: the list (with search), the editor of the chosen one (frames, repeat, movement, what comes next),
	/// the live preview and the sprite sheet (a click puts the frame in the animation).
	/// </summary>
	internal sealed class AnimationsPage : Page
	{
		ListBox list;
		TextBox search;
		Label countLbl, statusLbl;
		Panel editorHost;
		PreviewStage stage;
		SheetView sheet;
		FrameStrip strip;
		FlatButton pauseBtn;
		AnimationNode cur;
		int tab;   // 0 next, 1 border, 2 gravity
		bool loading;

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			// three dividers the user can drag: list | right, editor / bottom, preview | sheet (remembered)
			var s = EditorSettings.Current;
			var root = Split(Orientation.Vertical, () => s.SplitList, v => s.SplitList = v, 220, 200, 420);
			root.Panel1.Controls.Add(Pad(BuildList(), 0, 0, 6, 0));
			var right = Split(Orientation.Horizontal, () => s.SplitEditor, v => s.SplitEditor = v, 0.52, 0.25, 0.8);
			var editorCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0) };
			editorHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
			editorCard.Controls.Add(editorHost);
			right.Panel1.Controls.Add(Pad(editorCard, 6, 0, 0, 4));
			var bottom = Split(Orientation.Vertical, () => s.SplitPreview, v => s.SplitPreview = v, 0.56, 0.3, 0.8, 330);
			bottom.Panel1.Controls.Add(Pad(BuildPreview(), 6, 4, 4, 0));
			bottom.Panel2.Controls.Add(Pad(BuildSheet(), 4, 4, 0, 0));
			right.Panel2.Controls.Add(bottom);
			root.Panel2.Controls.Add(right);
			Controls.Add(root);
			FillList();
			if (Doc.Root.Animations.Animation.Length > 0) list.SelectedIndex = 0;
			else ShowEditor(null);
		}

		/// <summary>
		/// A divider the user can drag. <paramref name="get"/> is the saved place: pixels when above 1 (the list), a share
		/// of the space otherwise; it is kept between <paramref name="min"/> and <paramref name="max"/> (same unit).
		/// </summary>
		static SplitContainer Split(Orientation o, Func<double> get, Action<double> set, double fallback, double min, double max, int minFirst = 0)
		{
			var sc = new SplitContainer { Dock = DockStyle.Fill, Orientation = o, SplitterWidth = 8, BackColor = Theme.Background, FixedPanel = max > 1 ? FixedPanel.Panel1 : FixedPanel.None };
			sc.Panel1.BackColor = sc.Panel2.BackColor = Theme.Background;
			bool placing = false;
			int Size() => o == Orientation.Vertical ? sc.Width : sc.Height;
			void Place()
			{
				if (Size() < 300 || placing) return;
				placing = true;
				try { PlaceNow(); } finally { placing = false; }
			}
			void PlaceNow()
			{
				// pixels (the list) are at the zoom of Windows: the limits grow with it
				double k = max > 1 ? Ui.Scale : 1;
				double v = get(); if (v <= 0) v = fallback * k;
				v = Math.Max(min * k, Math.Min(max * k, v));
				int px = max > 1 ? (int)v : (int)(Size() * v);
				if (max > 1) px = Math.Min(px, (int)(Size() * 0.32));   // in a narrow window the list gives way
				if (minFirst > 0) px = Math.Max(px, Math.Min(Ui.S(minFirst), Size() / 2));   // the preview keeps room for its buttons
				px = Math.Max(sc.Panel1MinSize, Math.Min(Size() - sc.Panel2MinSize - sc.SplitterWidth, px));
				try { if (px > 0 && sc.SplitterDistance != px) sc.SplitterDistance = px; } catch (InvalidOperationException) { }
			}
			// placed again whenever the size changes (the saved place, never the one squeezed by a small window)
			sc.SizeChanged += (s, e) => Place();
			sc.HandleCreated += (s, e) => Place();
			// only a drag of the user is remembered (not the moves of a resize)
			bool dragging = false;
			sc.MouseDown += (s, e) => dragging = true;
			sc.SplitterMoved += (s, e) =>
			{
				bool mine = dragging; dragging = false;
				if (!mine || placing || Size() < 300 || !sc.Created) return;
				set(max > 1 ? sc.SplitterDistance : (double)sc.SplitterDistance / Size());
				EditorSettings.Save();
			};
			// the divider: a thin line in the middle of the gap
			sc.Paint += (s, e) =>
			{
				using var pen = new Pen(Theme.Border);
				var r = sc.SplitterRectangle;
				if (o == Orientation.Vertical) e.Graphics.DrawLine(pen, r.X + r.Width / 2, r.Y + 20, r.X + r.Width / 2, r.Bottom - 20);
				else e.Graphics.DrawLine(pen, r.X + 20, r.Y + r.Height / 2, r.Right - 20, r.Y + r.Height / 2);
			};
			return sc;
		}

		static Control Pad(Control c, int left, int top, int right, int bottom)
		{
			var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(left, top, right, bottom), BackColor = Theme.Background };
			c.Dock = DockStyle.Fill;
			c.Margin = new Padding(0);
			p.Controls.Add(c);
			return p;
		}

		// ------------------------------------------------------------------ list

		Control BuildList()
		{
			var card = new Card { Dock = DockStyle.Fill };
			var top = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Surface };
			countLbl = new Label { AutoSize = false, AutoEllipsis = true, Height = 30, Font = Ui.Font(16, true), ForeColor = Theme.Text, Location = new Point(0, 4) };
			var add = Ui.IconButton('',L.T("anim.new") + " — " + L.T("tip.anim.new"));   // "+" (the search gets the room)
			add.Click += (s, e) =>
			{
				// blank, from a ready-made behaviour, or copied from another pet
				var menu = new ContextMenuStrip();
				Theme.ApplyTo(menu);
				menu.Items.Add(L.T("anim.newBlank"), null, (o, a) => NewAnimation());
				menu.Items.Add(L.T("anim.newModel"), null, (o, a) => { int id = BehaviorDialog.Show(Main, Doc); if (id >= 0) { FillList(); SelectAnimation(id); } });
				menu.Items.Add(L.T("anim.newImport"), null, (o, a) => { int id = ImportDialog.Show(Main, Doc); if (id >= 0) { FillList(); SelectAnimation(id); } });
				menu.Show(add, new Point(0, add.Height));
			};
			var find = new SearchBox(L.T("anim.searchShort"), L.T("anim.search")) { Width = 260, Location = new Point(0, 44) };
			search = find.Box;
			search.TextChanged += (s, e) => FillList();
			top.Controls.AddRange(new Control[] { countLbl, add, find });
			// the title on its own line; the search and "+ New" side by side under it
			top.Layout += (s, e) => { countLbl.Width = top.Width; add.Location = new Point(top.Width - add.Width, Ui.S(40)); find.Bounds = new Rectangle(0, Ui.S(40), Math.Max(Ui.S(60), add.Left - Ui.S(8)), add.Height); };
			list = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.Thumb + Ui.S(16), BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, IntegralHeight = false };
			list.DrawItem += DrawItem;
			// the whole name under the mouse (long names end in "…" in the list)
			var tip = new ToolTip { InitialDelay = 400 }; int tipAt = -1;
			list.MouseMove += (s, e) =>
			{
				int i = list.IndexFromPoint(e.Location);
				if (i == tipAt) return;
				tipAt = i;
				string text = "";
				if (i >= 0 && i < list.Items.Count && list.Items[i] is AnimationNode a)
				{
					text = a.Name + Environment.NewLine + Sub(a);
					// and what the checker found in it
					foreach (var p in ProblemsOf(a.Id).Take(5)) text += Environment.NewLine + (p.Error ? "⛔ " : "⚠ ") + p.Text;
				}
				tip.SetToolTip(list, text);
			};
			// the marks of the checker follow the changes
			Action marks = () => { if (!list.IsDisposed) { tipAt = -1; list.Invalidate(); } };
			Main.ProblemsChanged += marks;
			list.Disposed += (s, e) => Main.ProblemsChanged -= marks;
			list.SelectedIndexChanged += (s, e) => { if (list.SelectedItem is AnimationNode a && a != cur) ShowEditor(a); };
			list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteCurrent(); };
			card.Controls.Add(list);
			card.Controls.Add(top);
			return card;
		}

		/// <summary>"#3 · 4 frames" (one frame: "1 frame").</summary>
		static string Sub(AnimationNode a) { int n = a.Sequence.Frame?.Length ?? 0; return n == 1 ? L.T("anim.sub1", a.Id) : L.T("anim.sub", a.Id, n); }

		IEnumerable<Validator.Problem> ProblemsOf(int id) => Main?.Problems?.Where(p => p.Anim == id) ?? Enumerable.Empty<Validator.Problem>();

		/// <summary>The worst problem of an animation (null: none).</summary>
		(bool error, int count)? ProblemMark(int id)
		{
			var mine = ProblemsOf(id).ToList();
			return mine.Count == 0 ? null : (mine.Any(p => p.Error), mine.Count);
		}

		void FillList()
		{
			string q = search.Text.Trim();
			var all = Doc.Root.Animations.Animation;
			var shown = all.Where(a => q.Length == 0 || (a.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || a.Id.ToString() == q).ToList();
			list.BeginUpdate();
			list.Items.Clear();
			foreach (var a in shown) list.Items.Add(a);
			if (cur != null && shown.Contains(cur)) list.SelectedItem = cur;
			list.EndUpdate();
			countLbl.Text = L.T("anim.count", all.Length);
		}

		void DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0) return;
			var a = (AnimationNode)list.Items[e.Index];
			var g = e.Graphics; Theme.Prepare(g);
			bool sel = (e.State & DrawItemState.Selected) != 0;
			using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, e.Bounds);
			var r = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);
			if (sel) { Theme.FillRounded(g, Theme.AccentSoft, r, 6); Theme.DrawRounded(g, Theme.Accent, r, 6); }
			int th = Ui.Thumb;
			var f = a.Sequence.Frame;
			Ui.DrawFrame(g, Doc.Frame(f?.Length > 0 ? f[0] : -1), new Rectangle(r.X + Ui.S(6), r.Y + (r.Height - th) / 2, th, th));
			int tx = r.X + th + Ui.S(14), tw = r.Right - tx - Ui.S(6);
			// a problem of the checker: a mark at the right of the name (the tooltip says which)
			var mark = ProblemMark(a.Id);
			if (mark != null)
			{
				tw -= Ui.S(20);
				TextRenderer.DrawText(g, mark.Value.error ? "⛔" : "⚠", Ui.Font(12), new Rectangle(r.Right - Ui.S(24), r.Y + r.Height / 2 - Ui.S(18), Ui.S(22), Ui.S(20)), mark.Value.error ? Theme.Danger : Ui.Warning, TextFormatFlags.Left);
			}
			TextRenderer.DrawText(g, a.Name, Ui.Font(13.5f, sel), new Rectangle(tx, r.Y + r.Height / 2 - Ui.S(18), tw, Ui.S(20)), Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			string sub = Sub(a) + (a.Picker == "no" ? "  ·  " + L.T("anim.hiddenShort") : "");
			TextRenderer.DrawText(g, sub, Ui.Font(11.5f), new Rectangle(tx, r.Y + r.Height / 2 + 1, r.Right - tx - Ui.S(6), Ui.S(18)), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
		}

		public void SelectAnimation(int id)
		{
			var a = Doc.Anim(id);
			if (a == null) return;
			if (!list.Items.Contains(a)) { search.Text = ""; FillList(); }
			list.SelectedItem = a;
		}

		// ------------------------------------------------------------------ editor of one animation

		void ShowEditor(AnimationNode a)
		{
			cur = a;
			loading = true;
			editorHost.SuspendLayout();
			foreach (Control c in editorHost.Controls) c.Dispose();
			editorHost.Controls.Clear();
			if (a == null)
			{
				editorHost.Controls.Add(Ui.Hint(L.T("anim.none"), 500));
				editorHost.ResumeLayout(); loading = false;
				return;
			}
			int W = Math.Max(380, editorHost.ClientSize.Width - 36);
			FlowLayoutPanel R(params Control[] c) { var r = Ui.Row(c); r.MaximumSize = new Size(W, 0); return r; }
			var col = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Surface, Location = new Point(0, 0) };

			// name, id, picker, duplicate / delete
			var name = Ui.Box(a.Name, 260); name.Font = Ui.Font(18, true);
			name.TextChanged += (s, e) => { if (loading) return; a.Name = name.Text; Doc.Changed("anim", typing: true); list.Invalidate(); };
			var shown = new Toggle(L.T("anim.inPicker"), a.Picker != "no");
			shown.Changed += (s, e) => { a.Picker = shown.On ? null : "no"; Doc.Changed("anim"); list.Invalidate(); };
			var dup = new FlatButton(L.T("anim.duplicate")); dup.Click += (s, e) => Duplicate();
			var del = new FlatButton(L.T("anim.delete")) { Danger = true }; del.Click += (s, e) => DeleteCurrent();
			var idLbl = Ui.Text(L.T("anim.idLabel", a.Id), true); idLbl.Margin = new Padding(0, 8, 14, 0);
			col.Controls.Add(R(name, idLbl, shown, dup, del));
			string key = KeyHint(a.Name);
			if (key != null) col.Controls.Add(Ui.Hint(key, W));

			// frames
			col.Controls.Add(Ui.Text(L.T("anim.frames"), false, 13.5f));
			col.Controls.Add(Ui.Hint(L.T("anim.framesHint"), W));
			strip = new FrameStrip { Doc = Doc, Frames = (a.Sequence.Frame ?? Array.Empty<int>()).ToList(), Width = Math.Max(400, editorHost.ClientSize.Width - 40) };
			strip.Fit();
			strip.Changed += () => { a.Sequence.Frame = strip.Frames.ToArray(); Doc.Changed("frames"); AfterFramesChanged(); };
			strip.AddClicked += () => Main.Message(L.T("anim.addHint"));
			// the time of each frame on its first pass (the interval goes from start to end over all the steps)
			strip.TimeOf = i =>
			{
				var c = new ExprContext();
				double s0 = Expr.Value(a.Start.Interval, c, 200), s1 = Expr.Value(a.End.Interval, c, 200);
				int n = a.Sequence.Frame?.Length ?? 1, rep = (int)Math.Max(0, Expr.Value(a.Sequence.RepeatCount, c));
				int total = Math.Max(1, n + (n - a.Sequence.RepeatFromFrame) * rep);
				return $"{s0 + (s1 - s0) * i / total:0} ms";
			};
			strip.EditPixels += f => PixelEditor.Show(Main, Doc, f);
			col.Controls.Add(strip);

			// repeat
			var repeat = Ui.Box(a.Sequence.RepeatCount, 150, mono: true);
			repeat.TextChanged += (s, e) => { if (loading) return; a.Sequence.RepeatCount = repeat.Text; Doc.Changed("anim", typing: true); };
			repeat.Leave += (s, e) => Replay();
			int maxFrom = Math.Max(0, (a.Sequence.Frame?.Length ?? 1) - 1);
			var from = Ui.Number(0, Math.Max(0, maxFrom), Math.Min(a.Sequence.RepeatFromFrame, maxFrom), 80);
			from.ValueChanged += (s, e) => { if (loading) return; a.Sequence.RepeatFromFrame = (int)from.Value; Doc.Changed("anim"); Replay(); };
			var action = Ui.Combo(170);
			action.Items.AddRange(new object[] { L.T("action.none"), L.T("action.flip") });
			bool custom = !string.IsNullOrWhiteSpace(a.Sequence.Action) && a.Sequence.Action != "flip" && a.Sequence.Action != "none";
			if (custom) action.Items.Add(a.Sequence.Action);
			action.SelectedIndex = a.Sequence.Action == "flip" ? 1 : custom ? 2 : 0;
			action.SelectedIndexChanged += (s, e) => { a.Sequence.Action = action.SelectedIndex == 1 ? "flip" : action.SelectedIndex == 2 ? (string)action.SelectedItem : null; Doc.Changed("anim"); };
			col.Controls.Add(R(Ui.Labeled(L.T("anim.repeat"), repeat, L.T("anim.repeatHint")), Ui.Labeled(L.T("anim.repeatFrom"), from), Ui.Labeled(L.T("anim.action"), action, L.T("anim.actionHint"))));

			// movement
			col.Controls.Add(Ui.Text(L.T("anim.move"), false, 13.5f));
			col.Controls.Add(Movement(a));
			col.Controls.Add(Ui.Hint(L.T("anim.moveHint"), W));

			// what comes next
			var tabs = Ui.Wrap(new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Surface, Margin = new Padding(0, 10, 0, 4) });   // the tabs go to a second line when narrow
			var next = new NextEditor();
			void ShowTab(int t)
			{
				tab = t;
				foreach (Label l in tabs.Controls) { bool on = (int)l.Tag == t; l.Font = Ui.Font(13.5f, on); l.ForeColor = on ? Theme.Accent : Theme.TextMuted; l.Invalidate(); }
				if (t == 0) next.Bind(Doc, () => a.Sequence.Next ?? Array.Empty<NextNode>(), v => a.Sequence.Next = v);
				else if (t == 1) next.Bind(Doc, () => a.Border?.Next ?? Array.Empty<NextNode>(), v => a.Border = v.Length == 0 ? null : new HitNode { Next = v });
				else next.Bind(Doc, () => a.Gravity?.Next ?? Array.Empty<NextNode>(), v => a.Gravity = v.Length == 0 ? null : new HitNode { Next = v });
			}
			string[] tabKeys = { "next.end", "next.border", "next.gravity" };
			for (int i = 0; i < 3; i++)
			{
				int n = i == 0 ? a.Sequence.Next?.Length ?? 0 : i == 1 ? a.Border?.Next?.Length ?? 0 : a.Gravity?.Next?.Length ?? 0;
				var l = new Label { Text = $"{L.T(tabKeys[i])} ({n})", Tag = i, AutoSize = true, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 22, 0), BackColor = Theme.Surface };
				l.Paint += (s, e) => { if ((int)l.Tag == tab) using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, l.Height - 3, l.Width, 3); };
				l.Click += (s, e) => ShowTab((int)l.Tag);
				l.Padding = new Padding(0, 0, 0, 6);
				tabs.Controls.Add(l);
			}
			next.Changed += () =>
			{
				for (int i = 0; i < 3; i++)
				{
					int n = i == 0 ? a.Sequence.Next?.Length ?? 0 : i == 1 ? a.Border?.Next?.Length ?? 0 : a.Gravity?.Next?.Length ?? 0;
					tabs.Controls[i].Text = $"{L.T(tabKeys[i])} ({n})";
				}
			};
			col.Controls.Add(tabs);
			col.Controls.Add(Ui.Hint(L.T("next.hint"), W));
			col.Controls.Add(next);
			ShowTab(tab);

			editorHost.Controls.Add(col);
			editorHost.ResumeLayout();
			editorHost.Resize -= FitStrip; editorHost.Resize += FitStrip;
			loading = false;
			MarkSheet();
			Replay();
		}

		void FitStrip(object s, EventArgs e) { if (strip != null && !strip.IsDisposed) { strip.Width = Math.Max(400, editorHost.ClientSize.Width - 40); strip.Fit(); } }

		/// <summary>The start / end table: x and y per step, offset, opacity and the time of a step.</summary>
		Control Movement(AnimationNode a)
		{
			// one line per value, a column for the start and one for the end (fits a narrow editor)
			var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 6, BackColor = Theme.Surface, Margin = new Padding(0, 4, 0, 4) };
			string[] heads = { L.T("move.x"), L.T("move.y"), L.T("move.offset"), L.T("move.opacity"), L.T("move.interval") };
			for (int r = 0; r < heads.Length; r++) t.Controls.Add(new Label { Text = heads[r], AutoSize = true, Font = Ui.Font(12), ForeColor = Theme.TextMuted, Margin = new Padding(0, 7, 12, 0) }, 0, r + 1);
			for (int col = 0; col < 2; col++)
			{
				var m = col == 0 ? a.Start : a.End;
				t.Controls.Add(new Label { Text = L.T(col == 0 ? "move.start" : "move.end"), AutoSize = true, Font = Ui.Font(13, true), ForeColor = Theme.Text, Margin = new Padding(0, 0, 8, 2) }, col + 1, 0);
				var x = Ui.Box(m.X, 140, true); x.TextChanged += (s, e) => { if (loading) return; m.X = x.Text; Doc.Changed("move", true); };
				var y = Ui.Box(m.Y, 140, true); y.TextChanged += (s, e) => { if (loading) return; m.Y = y.Text; Doc.Changed("move", true); };
				var off = Ui.Number(-2000, 2000, m.OffsetY, 90); off.ValueChanged += (s, e) => { if (loading) return; m.OffsetY = (int)off.Value; Doc.Changed("move", true); };
				var op = Ui.Number(0, 1, (decimal)Math.Max(0, Math.Min(1, m.Opacity)), 80, 2, 0.05m); op.ValueChanged += (s, e) => { if (loading) return; m.Opacity = (double)op.Value; Doc.Changed("move", true); };
				var iv = Ui.Box(m.Interval, 140, true); iv.TextChanged += (s, e) => { if (loading) return; m.Interval = iv.Text; Doc.Changed("move", true); };
				foreach (var c in new Control[] { x, y, iv }) c.Leave += (s, e) => Replay();
				t.Controls.Add(x, col + 1, 1); t.Controls.Add(y, col + 1, 2); t.Controls.Add(off, col + 1, 3); t.Controls.Add(op, col + 1, 4); t.Controls.Add(iv, col + 1, 5);
			}
			return t;
		}

		/// <summary>Names DesktopPet uses for itself (drag, fall…): explained under the name.</summary>
		static string KeyHint(string name) => (name ?? "").Trim().ToLowerInvariant() switch
		{
			"drag" => L.T("key.drag"), "fall" => L.T("key.fall"), "kill" => L.T("key.kill"), "sync" => L.T("key.sync"),
			"toss" => L.T("key.toss"), "fall soft" => L.T("key.fallSoft"), "fall hard" => L.T("key.fallHard"), _ => null,
		};

		void AfterFramesChanged()
		{
			list.Invalidate();
			MarkSheet();
			Replay();
		}

		// ------------------------------------------------------------------ new, duplicate, delete

		void NewAnimation()
		{
			int id = Doc.NextAnimId();
			var a = new AnimationNode
			{
				Id = id, Name = L.T("anim.newName"),
				Start = new MovingNode { X = "0", Y = "0", Interval = "150", Opacity = 1 },
				End = new MovingNode { X = "0", Y = "0", Interval = "150", Opacity = 1 },
				Sequence = new SequenceNode { RepeatCount = "0", Frame = Doc.Frames.Count > 0 ? new[] { 0 } : Array.Empty<int>(), Next = Array.Empty<NextNode>() },
			};
			Doc.Root.Animations.Animation = Doc.Root.Animations.Animation.Append(a).ToArray();
			Doc.Changed("anims");
			search.Text = "";
			FillList();
			list.SelectedItem = a;
		}

		void Duplicate()
		{
			if (cur == null) return;
			var xs = new XmlSerializer(typeof(AnimationNode));
			using var ms = new MemoryStream();
			xs.Serialize(ms, cur);
			ms.Position = 0;
			var a = (AnimationNode)xs.Deserialize(ms);
			a.Id = Doc.NextAnimId();
			a.Name = cur.Name + " " + L.T("anim.copy");
			var l = Doc.Root.Animations.Animation.ToList();
			l.Insert(l.IndexOf(cur) + 1, a);
			Doc.Root.Animations.Animation = l.ToArray();
			Doc.Changed("anims");
			FillList();
			list.SelectedItem = a;
		}

		void DeleteCurrent()
		{
			if (cur == null) return;
			var uses = Doc.UsesOf(cur.Id);
			string text = uses.Count == 0 ? L.T("anim.deleteAsk", cur.Name) : L.T("anim.deleteUsed", cur.Name, string.Join("\n• ", uses.Take(12)));
			if (MessageBox.Show(this, text, L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
			int id = cur.Id;
			int index = list.SelectedIndex;
			Doc.Root.Animations.Animation = Doc.Root.Animations.Animation.Where(a => a != cur).ToArray();
			// the other animations stop pointing at it (an id that is gone stops the pet in DesktopPet)
			foreach (var a in Doc.Root.Animations.Animation)
			{
				a.Sequence.Next = a.Sequence.Next?.Where(n => n.Value != id).ToArray();
				if (a.Border != null) { a.Border.Next = a.Border.Next?.Where(n => n.Value != id).ToArray(); if (a.Border.Next?.Length == 0) a.Border = null; }
				if (a.Gravity != null) { a.Gravity.Next = a.Gravity.Next?.Where(n => n.Value != id).ToArray(); if (a.Gravity.Next?.Length == 0) a.Gravity = null; }
			}
			Doc.Root.Sounds.Sound = Doc.Root.Sounds.Sound.Where(s => s.Id != id).ToArray();
			Doc.Root.Childs.Child = Doc.Root.Childs.Child.Where(c => c.Id != id).ToArray();
			cur = null;
			Doc.Changed("anims");
			FillList();
			if (list.Items.Count > 0) list.SelectedIndex = Math.Min(Math.Max(0, index), list.Items.Count - 1);
			else ShowEditor(null);
		}

		// ------------------------------------------------------------------ preview and sheet

		Control BuildPreview()
		{
			var card = new Card { Dock = DockStyle.Fill };
			var top = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.Surface };
			var title = Ui.Title(L.T("preview.title"), 15); title.Location = new Point(0, 2);
			statusLbl = new Label { AutoSize = false, AutoEllipsis = true, Height = 22, Font = Ui.Font(12), ForeColor = Theme.TextMuted, Location = new Point(title.PreferredWidth + 12, 6) };
			top.Layout += (s, e) => statusLbl.Width = Math.Max(20, top.Width - statusLbl.Left);
			top.Controls.AddRange(new Control[] { title, statusLbl });
			stage = new PreviewStage { Dock = DockStyle.Fill, Doc = Doc };
			stage.Status += s => { if (!statusLbl.IsDisposed) statusLbl.Text = L.T("preview.playing", s); };
			// as tall as its rows of buttons, by itself (computed by hand it was sometimes one row too tall and hid the ground)
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Surface, Padding = new Padding(0, 8, 0, 0), WrapContents = true };
			// small icon buttons (what each does is in its tooltip) and the switches in a menu: one line in most sizes
			pauseBtn = Ui.IconButton(PauseGlyph, L.T("preview.pause") + " (" + L.T("key.space") + ")");
			pauseBtn.Click += (s, e) => { stage.Pause(!stage.Paused); ShowPaused(stage.Paused); };
			var step = Ui.IconButton('', L.T("preview.step") + " (→) — " + L.T("tip.preview.step"));
			step.Click += (s, e) => { stage.StepOnce(); ShowPaused(true); };
			var skip = Ui.IconButton('', L.T("preview.next")); skip.Click += (s, e) => stage.Skip();
			var again = Ui.IconButton('', L.T("preview.restart") + " (R)"); again.Click += (s, e) => Replay();
			var speed = Ui.Combo(76);
			speed.Items.AddRange(new object[] { "0.25x", "0.5x", "1x", "2x" });
			speed.SelectedIndex = 2;
			speed.SelectedIndexChanged += (s, e) => stage.Speed = new[] { 0.25, 0.5, 1, 2 }[speed.SelectedIndex];
			var options = new FlatButton(L.T("preview.options") + " ▾");
			var menu = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };
			Theme.ApplyTo(menu);
			ToolStripMenuItem Option(string key, string tip, Action<bool> set)
			{
				var item = new ToolStripMenuItem(L.T(key)) { CheckOnClick = true, ToolTipText = tip };
				item.CheckedChanged += (s, e) => set(item.Checked);
				menu.Items.Add(item);
				return item;
			}
			Option("preview.loop", null, on => stage.LoopOne = on);
			Option("preview.windowToggle", L.T("tip.preview.window"), on => stage.ToggleWindow(on));
			Option("preview.onion", L.T("tip.preview.onion"), on => { stage.Onion = on; stage.Invalidate(); });
			options.Click += (s, e) => menu.Show(options, new Point(0, options.Height));
			bar.Controls.AddRange(new Control[] { pauseBtn, step, skip, again, speed, options });
			stage.Tip(L.T("tip.preview.mouse"));
			card.Controls.Add(stage);
			card.Controls.Add(bar);
			card.Controls.Add(top);
			return card;
		}

		const char PauseGlyph = '', PlayGlyph = '';

		/// <summary>The pause button shows "play" while the preview is stopped (and says so in its tooltip).</summary>
		void ShowPaused(bool paused)
		{
			if (pauseBtn == null || pauseBtn.IsDisposed) return;
			pauseBtn.Text = (paused ? PlayGlyph : PauseGlyph).ToString();
			pauseBtn.Tip(L.T(paused ? "preview.play" : "preview.pause") + " (" + L.T("key.space") + ")");
		}

		Control BuildSheet()
		{
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			var top = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 0, 4), BackColor = Theme.Surface };
			var title = Ui.Title(L.T("sheet.title"), 15); title.Location = new Point(0, 0);
			var hint = Ui.Text(L.T("sheet.clickHint"), true, 12); hint.Location = new Point(0, 24);
			top.Controls.AddRange(new Control[] { title, hint });
			sheet = new SheetView { Dock = DockStyle.Fill, Doc = Doc };
			sheet.FrameClicked += f =>
			{
				if (cur == null || strip == null) return;
				strip.Frames.Add(f); strip.Fit();
				cur.Sequence.Frame = strip.Frames.ToArray();
				Doc.Changed("frames");
				strip.Invalidate();
				AfterFramesChanged();
			};
			card.Controls.Add(sheet);
			card.Controls.Add(top);
			return card;
		}

		void MarkSheet()
		{
			if (sheet == null) return;
			sheet.Marked = cur?.Sequence.Frame?.ToHashSet() ?? new HashSet<int>();
			sheet.UpdateSize();
		}

		void Replay()
		{
			if (cur == null || stage == null || Doc.Frames.Count == 0) { stage?.Stop(); return; }
			stage.Play(cur.Id);
			ShowPaused(false);
		}

		// ------------------------------------------------------------------ page events

		public override void Reload(string what)
		{
			if (what is "all" or "image" or "header")
			{
				var id = cur?.Id ?? -1;
				cur = null;
				stage.Doc = Doc; sheet.Doc = Doc;
				FillList();
				var a = Doc.Anim(id) ?? Doc.Root.Animations.Animation.FirstOrDefault();
				if (a != null) list.SelectedItem = a; else ShowEditor(null);
				if (a != null && a != cur) ShowEditor(a);
				sheet.UpdateSize();
			}
			else if (what is "anims") list.Invalidate();
		}

		/// <summary>Keys of the preview: Space pauses or continues, → one step, R from the start.</summary>
		public bool PreviewKey(Keys key)
		{
			if (stage == null || stage.IsDisposed) return false;
			switch (key)
			{
				case Keys.Space: stage.Pause(!stage.Paused); ShowPaused(stage.Paused); return true;
				case Keys.Right: stage.StepOnce(); ShowPaused(true); return true;
				case Keys.R: Replay(); return true;
				default: return false;
			}
		}

		public override void Shown() { Replay(); }
		public override void Hidden() { stage?.Stop(); }
	}
}
