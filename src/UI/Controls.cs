using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>Small helpers to build the pages in code with the colors of DesktopPet.</summary>
	internal static class Ui
	{
		// ------------------------------------------------------------------ the zoom of Windows (125%, 150%…)

		static float scale;
		/// <summary>How much bigger than at 100%: the zoom of Windows ("--scale=1.5" tries another one).</summary>
		public static float Scale => scale > 0 ? scale : scale = FindScale();
		static float FindScale()
		{
			var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--scale="));
			if (arg != null && float.TryParse(arg.Substring(8), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && f >= 0.75f && f <= 3) return f;
			// the size chosen in the settings
			int chosen = EditorSettings.Current.UiSize;
			if (chosen >= 75 && chosen <= 300) return chosen / 100f;
			// the zoom of Windows (a Graphics of the screen says 96 in this mode, so it is asked to Windows itself)
			return WindowsZoom;
		}

		/// <summary>The zoom of Windows (1.25 = 125%).</summary>
		public static float WindowsZoom
		{
			// the zoom of the main screen now (the "system" zoom of Windows only changes after signing in again: a
			// change from 125% to 100% kept the editor big)
			get
			{
				try
				{
					var mon = MonitorFromPoint(new POINT(0, 0), 1);   // MONITOR_DEFAULTTOPRIMARY
					if (GetDpiForMonitor(mon, 0, out uint x, out _) == 0 && x > 0) return Math.Max(1f, x / 96f);   // MDT_EFFECTIVE_DPI
				}
				catch { }
				try { return Math.Max(1f, GetDpiForSystem() / 96f); } catch { return 1f; }
			}
		}
		struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetDpiForSystem();
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
		[System.Runtime.InteropServices.DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
		/// <summary>A size in pixels at 100%, at the zoom of Windows.</summary>
		public static int S(int px) => (int)Math.Round(px * Scale);
		/// <summary>Amber of the warnings (the errors use the red of the theme).</summary>
		public static Color Warning => Theme.IsDark ? Color.FromArgb(232, 172, 40) : Color.FromArgb(196, 128, 0);
		public static Size S(Size s) => new(S(s.Width), S(s.Height));

		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> scaled = new();

		/// <summary>
		/// Everything put in <paramref name="root"/>, now and later, grows with the zoom of Windows: sizes, places,
		/// margins and the height of the lines of lists (once each). The pages are written in pixels at 100%.
		/// </summary>
		public static void Scaled(Control root)
		{
			Mark(root, false);
			foreach (Control c in root.Controls) Grow(c);
		}

		/// <summary>
		/// Grow <paramref name="c"/> now, before it goes into the window: done after, every control is already a window
		/// of Windows and is moved one by one (seconds for a big page). Things added later grow by themselves anyway.
		/// </summary>
		public static void Grow(Control c)
		{
			if (scaled.TryGetValue(c, out _)) return;
			// its children too (the height of the lines of the lists is given already grown, with S)
			if (Scale != 1f) c.Scale(new SizeF(Scale, Scale));
			Mark(c, true);
		}

		static void Mark(Control c, bool children)
		{
			if (scaled.TryGetValue(c, out _)) return;
			scaled.AddOrUpdate(c, null);
			c.ControlAdded += (s, e) => Grow(e.Control);
			if (children) foreach (Control k in c.Controls) Mark(k, true);
		}

		/// <summary>The letters of the editor (size in pixels at 100%), made once per size and kept: never disposed by who uses them.</summary>
		public static Font Font(float px, bool bold = false)
		{
			var key = ((int)Math.Round(px * Scale * 4), bold);
			if (!fonts.TryGetValue(key, out var f)) fonts[key] = f = Theme.CreateFont(key.Item1 / 4f, bold ? FontStyle.Bold : FontStyle.Regular);
			return f;
		}
		static readonly Dictionary<(int, bool), Font> fonts = new();
		static Font mono, icons;
		/// <summary>Consolas for formulas and code (9.5 pt at 100%).</summary>
		public static Font Mono => mono ??= new Font("Consolas", 12.7f * Scale, GraphicsUnit.Pixel);
		/// <summary>The icons of Windows (undo, play, pause…): drawn the same in every theme and size.</summary>
		public static Font Icons => icons ??= new Font("Segoe MDL2 Assets", 14f * Scale, GraphicsUnit.Pixel);

		/// <summary>A small square button with an icon of Windows; what it does is in its tooltip.</summary>
		public static FlatButton IconButton(char glyph, string tip)
		{
			var b = new FlatButton(glyph.ToString()) { Font = Icons };
			b.Width = 40;
			return b.Tip(tip);
		}

		public static Label Title(string text, float px = 17) => Wrap(new Label { Text = text, AutoSize = true, Font = Font(px, true), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 2) });
		public static Label Text(string text, bool muted = false, float px = 13) => Wrap(new Label { Text = text, AutoSize = true, Font = Font(px), ForeColor = muted ? Theme.TextMuted : Theme.Text, Margin = new Padding(0, 2, 0, 2) });
		public static Label Hint(string text, int width) => Wrap(new Label { Text = text, AutoSize = true, MaximumSize = new Size(width, 0), Font = Font(12), ForeColor = Theme.TextMuted, Margin = new Padding(0, 2, 0, 8) }, width);

		// ------------------------------------------------------------------ widths that follow the window

		sealed class FitRule { public int Min, Max; public bool Stretch; }
		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, FitRule> fit = new();

		/// <summary>Text (or a row of controls) that breaks its lines at the width there is, up to <paramref name="max"/>.</summary>
		public static T Wrap<T>(T c, int max = 4000) where T : Control { fit.AddOrUpdate(c, new FitRule { Max = max }); return c; }

		/// <summary>A box or a card as wide as there is room for, between <paramref name="min"/> and <paramref name="max"/>.</summary>
		public static T Stretch<T>(T c, int min, int max) where T : Control { fit.AddOrUpdate(c, new FitRule { Min = min, Max = max, Stretch = true }); return c; }

		/// <summary>The width a control can use inside the first ancestor that does not grow with its content.</summary>
		static int Room(Control c)
		{
			int x = 0;
			Control p = c;
			while (p.Parent != null && (p.Parent is FlowLayoutPanel { AutoSize: true } || p.Parent.AutoSize && p.Parent.Dock == DockStyle.None && p.Parent is not Form))
			{
				x += p.Left + p.Parent.Padding.Left;
				p = p.Parent;
			}
			var host = p.Parent;
			if (host == null) return -1;
			x += p.Left;
			int width = host.ClientSize.Width - host.Padding.Right - x - S(6);
			// room for the scroll bar is always kept: otherwise the bar showing up narrows the texts, which makes
			// them taller or shorter, which shows or hides the bar... and the widths never settle
			if (host is ScrollableControl s && s.AutoScroll && !s.VerticalScroll.Visible) width -= SystemInformation.VerticalScrollBarWidth;
			return width;
		}

		/// <summary>Apply the widths of every text, row, box and card of <paramref name="root"/> (twice: one change moves the next).</summary>
		public static bool FitAll(Control root)
		{
			bool changed = false;
			var all = All(root).ToList();
			// the containers that will move: their layout waits while the widths change, then is done once (each
			// width changed alone made the whole column lay out again: 1.7 s for the animation of a big pet)
			var boxes = all.Where(c => c.HasChildren).ToList();
			for (int pass = 0; pass < 2; pass++)
			{
				var waiting = new List<Control>();
				foreach (var c in all)
				{
					// columns and rows that grow with their content also shrink again (the default is "only grow")
					if (pass == 0 && c is Panel { AutoSize: true, AutoSizeMode: AutoSizeMode.GrowOnly } panel) panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
					if (!fit.TryGetValue(c, out var rule) || !c.Visible) continue;
					int room = Room(c);
					if (room <= 0) continue;
					int w = rule.Stretch
						? Math.Max(S(rule.Min), Math.Min(S(rule.Max), room - c.Margin.Right))
						: Math.Max(S(120), Math.Min(S(rule.Max), room - c.Margin.Right));
					if (rule.Stretch ? c.Width == w : c.MaximumSize.Width == w) continue;
					if (waiting.Count == 0) { waiting = boxes; foreach (var b in boxes) b.SuspendLayout(); }
					if (rule.Stretch) c.Width = w; else c.MaximumSize = new Size(w, 0);
					changed = true;
				}
				// the inner ones first: each container is laid out once, with its content already final
				for (int i = waiting.Count - 1; i >= 0; i--) waiting[i].ResumeLayout(true);
				if (waiting.Count == 0) break;   // nothing moved: the second pass would find the same
			}
			return changed;
		}

		static IEnumerable<Control> All(Control root)
		{
			foreach (Control c in root.Controls)
			{
				yield return c;
				foreach (var k in All(c)) yield return k;
			}
		}

		/// <summary>The widths follow the size of this window or page from now on.</summary>
		static Icon appIcon;
		/// <summary>The icon of the editor (the one in the exe).</summary>
		public static Icon AppIcon => appIcon ??= LoadIcon();
		/// <summary>
		/// The icons of a window at the sizes Windows asks at this zoom (20 and 40 px at 125%): Windows Forms gives 16 and
		/// 32, and the taskbar stretched them (a blurred icon).
		/// </summary>
		public static void SharpIcons(Form f)
		{
			void Set()
			{
				if (!f.IsHandleCreated) return;
				int dpi = f.DeviceDpi;
				int small = GetSystemMetricsForDpi(49, (uint)dpi), big = GetSystemMetricsForDpi(11, (uint)dpi);   // SM_CXSMICON, SM_CXICON
				var s = new Icon(AppIcon, small, small); var b = new Icon(AppIcon, big, big);
				SendMessage(f.Handle, 0x80, (IntPtr)0, s.Handle);   // WM_SETICON small
				SendMessage(f.Handle, 0x80, (IntPtr)1, b.Handle);   // WM_SETICON big
				f.Disposed += (o, e) => { s.Dispose(); b.Dispose(); };
			}
			f.HandleCreated += (o, e) => Set();
			f.DpiChanged += (o, e) => f.BeginInvoke(new Action(Set));
			f.Shown += (o, e) => Set();   // after Windows Forms has given its own
		}
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index, uint dpi);
		[System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

		static Icon LoadIcon()
		{
			// every size of icon.ico (16 to 256): Windows takes the right one for the title, the taskbar and Alt+Tab
			using var s = typeof(Ui).Assembly.GetManifestResourceStream("PetEditor.icon.ico");
			return s != null ? new Icon(s) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}

		/// <summary>A window of the editor (a dialog) was opened: the pictures of the developer take it too.</summary>
		public static event Action<Form> DialogShown;

		public static void AutoFit(Control root)
		{
			// a window of the editor: grows with the zoom of Windows (its size too, but never past the screen)
			if (root is Form form)
			{
				var screen = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1920, 1040);
				form.AutoScaleMode = AutoScaleMode.None;
				form.Icon = AppIcon;   // the icon of the editor, not the default one of Windows Forms
				SharpIcons(form);
				form.Size = new Size(Math.Min(S(form.Width), screen.Width), Math.Min(S(form.Height), screen.Height));
				if (!form.MinimumSize.IsEmpty) form.MinimumSize = new Size(Math.Min(S(form.MinimumSize.Width), screen.Width), Math.Min(S(form.MinimumSize.Height), screen.Height));
				Scaled(form);
				form.Shown += (s, e) => DialogShown?.Invoke(form);
			}
			// the Layout event comes before the docked panels get their new size, so the widths are applied
			// right after it (once per round of layouts), when every size is the final one
			bool busy = false, queued = false;
			int streak = 0; Size lastSize = Size.Empty; long lastRun = 0;
			void Run()
			{
				queued = false;
				if (busy || root.IsDisposed) return;
				busy = true;
				try { streak = FitAll(root) ? streak + 1 : 0; } finally { busy = false; lastRun = Environment.TickCount64; }
			}
			root.Layout += (s, e) =>
			{
				// a new size or a later change (new content) starts again
				if (root.Size != lastSize || Environment.TickCount64 - lastRun > 300) { lastSize = root.Size; streak = 0; }
				if (busy || queued || streak >= 4) return;   // a few rounds settle it; more means it would only swing
				if (!root.IsHandleCreated) { Run(); return; }
				queued = true;
				root.BeginInvoke(Run);
			};
			root.HandleCreated += (s, e) => { if (!queued) { queued = true; root.BeginInvoke(Run); } };
		}

		public static TextBox Box(string text = "", int width = 160, bool mono = false)
		{
			var t = new TextBox { Text = text ?? "", Width = width, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Surface, ForeColor = Theme.Text, Margin = new Padding(0, 0, 8, 6) };
			t.Font = mono ? Mono : Font(13);
			return t;
		}

		/// <summary>A list to choose from, drawn with the theme colours (also the closed box in the dark theme).</summary>
		public static ComboBox Combo(int width = 200)
		{
			var c = new ComboBox
			{
				Width = width, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Theme.Surface, ForeColor = Theme.Text,
				Font = Font(13), Margin = new Padding(0, 0, 8, 6), DrawMode = DrawMode.OwnerDrawFixed,
			};
			// no ItemHeight: Windows takes it from the letters (already at the zoom). Setting it made the box in Windows at
			// once, and every item added after was a call to Windows (a row of "what comes next" took 70 ms)
			// the open list is as wide as its longest item
			c.DropDown += (s, e) => { int w = c.Width; foreach (var it in c.Items) w = Math.Max(w, TextRenderer.MeasureText(c.GetItemText(it), c.Font).Width + 30); c.DropDownWidth = Math.Min(w, 600); };
			c.DrawItem += (s, e) =>
			{
				bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
				using (var b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
				string text = e.Index >= 0 ? c.GetItemText(c.Items[e.Index]) : c.Text;
				TextRenderer.DrawText(e.Graphics, text, c.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			};
			return c;
		}

		public static NumericUpDown Number(decimal min, decimal max, decimal value, int width = 90, int decimals = 0, decimal step = 1)
			=> new() { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = width, DecimalPlaces = decimals, Increment = step, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Font(13), Margin = new Padding(0, 0, 8, 6) };

		/// <summary>A label above a control, as one block.</summary>
		public static Control Labeled(string label, Control c, string hint = null)
		{
			var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 14, 4), BackColor = Color.Transparent };
			p.Controls.Add(new Label { Text = label, AutoSize = true, Font = Font(12), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 3) });
			p.Controls.Add(c);
			if (hint != null) p.Controls.Add(Wrap(new Label { Text = hint, AutoSize = true, MaximumSize = new Size(Math.Max(c.Width, 260), 0), Font = Font(11.5f), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 2) }, Math.Max(c.Width, 520)));
			return p;
		}

		/// <summary>Controls side by side; they go on the next line when there is no room.</summary>
		public static FlowLayoutPanel Row(params Control[] items)
		{
			var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 6), BackColor = Color.Transparent };
			p.Controls.AddRange(items);
			return Wrap(p);
		}

		public static FlowLayoutPanel Column(params Control[] items)
		{
			var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Color.Transparent };
			p.Controls.AddRange(items);
			return p;
		}

		/// <summary>Draw a frame of the pet crisp (nearest neighbour), centred and as big as fits.</summary>
		public static void DrawFrame(Graphics g, Image img, Rectangle box, bool mirror = false, float opacity = 1f)
		{
			if (img == null) return;
			float sc = Math.Min((float)box.Width / img.Width, (float)box.Height / img.Height);
			if (sc >= 1) sc = (float)Math.Floor(sc);
			int w = Math.Max(1, (int)(img.Width * sc)), h = Math.Max(1, (int)(img.Height * sc));
			var dest = new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
			var st = g.Save();
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			if (mirror) { g.TranslateTransform(dest.X * 2 + dest.Width, 0); g.ScaleTransform(-1, 1); }
			if (opacity < 0.999f)
			{
				using var ia = new System.Drawing.Imaging.ImageAttributes();
				ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = Math.Max(0, opacity) });
				g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
			}
			else g.DrawImage(img, dest);
			g.Restore(st);
		}

		static readonly ToolTip tips = new() { InitialDelay = 500, AutoPopDelay = 15000 };

		/// <summary>An explanation shown when the mouse stays over the control.</summary>
		public static T Tip<T>(this T c, string text) where T : Control { tips.SetToolTip(c, text); return c; }

		/// <summary>Thumbnail size from the settings (small, medium, large).</summary>
		public static int Thumb => S(EditorSettings.Current.Thumbs switch { 0 => 32, 2 => 64, _ => 46 });
	}

	/// <summary>A rounded, flat button (accent colour when primary).</summary>
	internal sealed class FlatButton : Control
	{
		bool hover, down;
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Primary { get; set; }
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool Danger { get; set; }

		public FlatButton(string text, bool primary = false)
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			Text = text; Primary = primary;
			Font = Ui.Font(13, primary);
			Cursor = Cursors.Hand;
			BackColor = Color.Transparent;
			Margin = new Padding(0, 0, 8, 6);
			Height = 32;
			Width = TextRenderer.MeasureText(text, Font).Width + 28;
		}

		/// <summary>As a click (keyboard shortcuts).</summary>
		public void PerformClick() => OnClick(EventArgs.Empty);

		bool grown;
		/// <summary>Grown with the zoom of Windows: the width comes from the text (already in the bigger letters).</summary>
		protected override void ScaleControl(SizeF factor, BoundsSpecified specified) { base.ScaleControl(factor, specified); grown = true; FitWidth(); }
		void FitWidth() { Width = Font == Ui.Icons ? (grown ? Ui.S(40) : 40) : TextRenderer.MeasureText(Text, Font).Width + (grown ? Ui.S(28) : 28); }
		protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitWidth(); Invalidate(); }
		protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
		protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
		protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			var r = new Rectangle(0, 0, Width - 1, Height - 1);
			Color back = Primary ? (hover ? ControlPaint.Light(Theme.Accent, 0.15f) : Theme.Accent)
				: Danger && hover ? Theme.Mix(Theme.Surface, Theme.Danger, 0.15f) : hover ? Theme.Hover : Theme.Surface;
			if (down) back = Theme.Mix(back, Theme.Text, 0.08f);
			Theme.FillRounded(g, back, r, 6);
			if (!Primary) Theme.DrawRounded(g, Theme.Border, r, 6);
			var fore = !Enabled ? Theme.TextMuted : Primary ? Theme.AccentText : Danger ? Theme.Danger : Theme.Text;
			TextRenderer.DrawText(g, Text, Font, r, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
		}
	}

	/// <summary>An on/off switch with its text (Windows 11 style).</summary>
	internal sealed class Toggle : Control
	{
		bool on;
		public event EventHandler Changed;
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool On { get => on; set { if (on == value) return; on = value; Invalidate(); } }

		public Toggle(string text, bool value)
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			Text = text; on = value; Font = Ui.Font(13); BackColor = Color.Transparent; Cursor = Cursors.Hand;
			Margin = new Padding(0, 0, 12, 6);
			Fit();
			Ui.Wrap(this);   // a long text goes on more lines when there is no room
		}

		bool grown;
		/// <summary>Pixels at 100% before growing with the zoom of Windows, at the zoom after.</summary>
		int P(int px) => grown ? Ui.S(px) : px;

		/// <summary>One line when it fits; otherwise the text breaks its lines (and the switch stays at the first one).</summary>
		void Fit()
		{
			int lead = P(56), line = P(30);
			int max = MaximumSize.Width > 0 ? MaximumSize.Width : int.MaxValue;
			int one = TextRenderer.MeasureText(Text, Font).Width;
			if (lead + one <= max) { Size = new Size(lead + one, line); return; }
			int h = TextRenderer.MeasureText(Text, Font, new Size(max - lead, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
			Size = new Size(max, Math.Max(line, h + P(10)));
		}

		public override Size MaximumSize { get => base.MaximumSize; set { base.MaximumSize = value; Fit(); Invalidate(); } }
		protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Fit(); Invalidate(); }
		protected override void OnClick(EventArgs e) { on = !on; Invalidate(); Changed?.Invoke(this, EventArgs.Empty); base.OnClick(e); }

		// grown with the zoom of Windows: the width comes from the text (already in the bigger letters)
		protected override void ScaleControl(SizeF factor, BoundsSpecified specified) { base.ScaleControl(factor, specified); grown = true; Fit(); }

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			// the switch at the size of the first line (which grows with the zoom of Windows)
			int line = P(30);
			float k = line / 30f;
			Theme.DrawToggle(g, new Rectangle(0, (int)(5 * k), (int)(42 * k), (int)(20 * k)), on);
			int x = (int)(52 * k);
			bool many = Height > line + 2;
			TextRenderer.DrawText(g, Text, Font, new Rectangle(x, many ? (int)(5 * k) : 0, Width - x, many ? Height : line), Theme.Text,
				(many ? TextFormatFlags.WordBreak : TextFormatFlags.VerticalCenter) | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
		}
	}

	/// <summary>A white rounded card that holds a part of a page.</summary>
	internal class Card : Panel
	{
		public Card()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
			BackColor = Theme.Background;
			Padding = new Padding(16, 14, 16, 14);
			Margin = new Padding(0, 0, 14, 14);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Theme.Background);
			var r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRounded(g, Theme.Surface, r, 10);
			Theme.DrawRounded(g, Theme.Border, r, 10);
		}

		/// <summary>The controls inside a card are drawn on its white surface.</summary>
		protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); Tint(e.Control); }

		static void Tint(Control c)
		{
			if (c is Panel or FlowLayoutPanel or TableLayoutPanel or Label or Toggle or FlatButton) c.BackColor = c is FlatButton or Toggle ? Color.Transparent : Theme.Surface;
			c.ControlAdded += (s, e) => Tint(e.Control);
			foreach (Control k in c.Controls) Tint(k);
		}
	}

	/// <summary>The side bar of the main window: one entry per page.</summary>
	internal sealed class NavBar : Control
	{
		public readonly List<(string key, string text, bool enabled)> Items = new();
		int selected, hover = -1;
		public event Action<string> Selected;
		static int ItemH => Ui.S(40);
		static int Top0 => Ui.S(16);
		/// <summary>Problems of the checker per section: shown as a small number at the right (red when errors).</summary>
		public readonly Dictionary<string, (int errors, int warnings)> Badges = new();

		public NavBar()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			Width = 220; Dock = DockStyle.Left; Font = Ui.Font(14);
		}

		public string Current => selected >= 0 && selected < Items.Count ? Items[selected].key : null;

		public void Select(string key)
		{
			int i = Items.FindIndex(x => x.key == key);
			if (i < 0 || !Items[i].enabled) return;
			selected = i; Invalidate();
			Selected?.Invoke(key);
		}

		int Y(int i) => Top0 + i * ItemH;

		/// <summary>Where an entry is on the screen (the tour points at it).</summary>
		public Rectangle ItemScreenBounds(string key)
		{
			int i = Items.FindIndex(x => x.key == key);
			return i < 0 ? Rectangle.Empty : RectangleToScreen(new Rectangle(Ui.S(10), Y(i), Width - Ui.S(20), ItemH - Ui.S(6)));
		}

		readonly ToolTip tip = new() { InitialDelay = 600, AutoPopDelay = 12000 };

		protected override void OnMouseMove(MouseEventArgs e)
		{
			int i = (e.Y - Top0) / ItemH;
			int h = i >= 0 && i < Items.Count && Items[i].enabled && Items[i].key != "-" ? i : -1;
			if (h != hover)
			{
				hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate();
				// what the section is for, under the mouse
				if (h >= 0 && L.Has("tip.nav." + Items[h].key)) tip.Show(L.T("tip.nav." + Items[h].key), this, Width - 4, Y(h) + 4, 8000);
				else tip.Hide(this);
			}
		}
		protected override void OnMouseLeave(EventArgs e) { hover = -1; tip.Hide(this); Invalidate(); }
		protected override void OnMouseClick(MouseEventArgs e) { if (hover >= 0) Select(Items[hover].key); }

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Theme.Surface);
			using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
			for (int i = 0; i < Items.Count; i++)
			{
				var it = Items[i];
				var r = new Rectangle(Ui.S(10), Y(i), Width - Ui.S(20), ItemH - Ui.S(6));
				int pad = Ui.S(16);
				if (it.key == "-")
				{
					TextRenderer.DrawText(g, it.text, Ui.Font(11, true), new Rectangle(r.X + pad, r.Y + Ui.S(8), r.Width, r.Height), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.Top);
					continue;
				}
				bool sel = i == selected;
				if (sel) { Theme.FillRounded(g, Theme.AccentSoft, r, 6); using var b = new SolidBrush(Theme.Accent); g.FillRectangle(b, r.X, r.Y + Ui.S(6), Ui.S(3), r.Height - Ui.S(12)); }
				else if (i == hover) Theme.FillRounded(g, Theme.Hover, r, 6);
				var color = !it.enabled ? Theme.Mix(Theme.TextMuted, Theme.Surface, 0.45f) : sel ? Theme.Accent : Theme.Text;
				int textRight = r.Right;
				if (Badges.TryGetValue(it.key, out var bd) && bd.errors + bd.warnings > 0)
				{
					// a pill with the number of problems: red with errors, amber with only warnings
					string n = (bd.errors > 0 ? bd.errors : bd.warnings).ToString();
					var f = Ui.Font(11, true);
					int w = Math.Max(Ui.S(22), TextRenderer.MeasureText(n, f).Width + Ui.S(10)), h = Ui.S(20);
					var pill = new Rectangle(r.Right - w - Ui.S(6), r.Y + (r.Height - h) / 2, w, h);
					Theme.FillRounded(g, bd.errors > 0 ? Theme.Danger : Ui.Warning, pill, h / 2);
					TextRenderer.DrawText(g, n, f, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					textRight = pill.X - Ui.S(4);
				}
				TextRenderer.DrawText(g, it.text, sel ? Ui.Font(14, true) : Font, new Rectangle(r.X + pad, r.Y, textRight - r.X - pad, r.Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
			}
		}
	}

	/// <summary>
	/// The frames of an animation as thumbnails: drag one to change the order, the × removes it, the + tile asks
	/// to click a frame in the sprite sheet.
	/// </summary>
	internal sealed class FrameStrip : Control
	{
		public PetDoc Doc;
		public List<int> Frames = new();
		public event Action Changed;
		int hover = -1, dragFrom = -1, dropAt = -1;
		bool hoverX;
		int Cell => Ui.Thumb + Ui.S(18);
		static int Gap => Ui.S(6);
		static int Below => Ui.S(14);

		public FrameStrip()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			Height = 80; Margin = new Padding(0, 0, 0, 6);
		}

		Rectangle Tile(int i)
		{
			int perRow = Math.Max(1, (Width - 4) / (Cell + Gap));
			return new Rectangle(2 + i % perRow * (Cell + Gap), 2 + i / perRow * (Cell + Below), Cell, Cell + Ui.S(10));
		}

		public void Fit()
		{
			int perRow = Math.Max(1, (Width - 4) / (Cell + Gap));
			int rows = (Frames.Count + 1 + perRow - 1) / perRow;
			Height = Math.Max(1, rows) * (Cell + Below) + 4;
		}

		protected override void OnResize(EventArgs e) { base.OnResize(e); Fit(); }

		/// <summary>The × that removes a frame (top right of its tile).</summary>
		static Rectangle XBox(Rectangle tile) { int s = Ui.S(16); return new Rectangle(tile.Right - s - 2, tile.Y + 2, s, s); }

		int HitTile(Point p) { for (int i = 0; i <= Frames.Count; i++) if (Tile(i).Contains(p)) return i; return -1; }

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (dragFrom >= 0 && e.Button == MouseButtons.Left)
			{
				int t = HitTile(e.Location); if (t > Frames.Count - 1) t = Frames.Count - 1;
				if (t != dropAt) { dropAt = t; Invalidate(); }
				return;
			}
			int h = HitTile(e.Location);
			bool x = h >= 0 && h < Frames.Count && XBox(Tile(h)).Contains(e.Location);
			if (h != hover || x != hoverX) { hover = h; hoverX = x; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
		}

		protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

		protected override void OnMouseDown(MouseEventArgs e)
		{
			int h = HitTile(e.Location);
			if (e.Button == MouseButtons.Left && h >= 0 && h < Frames.Count && !hoverX) { dragFrom = h; dropAt = h; }
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			int h = HitTile(e.Location);
			if (dragFrom >= 0 && dropAt >= 0 && dropAt != dragFrom)
			{
				int f = Frames[dragFrom]; Frames.RemoveAt(dragFrom); Frames.Insert(dropAt, f);
				Changed?.Invoke();
			}
			else if (e.Button == MouseButtons.Left && h >= 0 && h < Frames.Count && hoverX) { Frames.RemoveAt(h); Fit(); Changed?.Invoke(); }
			else if (e.Button == MouseButtons.Right && h >= 0 && h < Frames.Count) TileMenu(h, e.Location);
			else if (e.Button == MouseButtons.Left && h == Frames.Count) AddClicked?.Invoke();
			dragFrom = dropAt = -1; Invalidate();
		}

		public event Action AddClicked;
		/// <summary>How long the frame stays on the screen (shown under it), when set.</summary>
		public Func<int, string> TimeOf;
		/// <summary>"Edit pixels" was chosen for this frame of the sheet.</summary>
		public event Action<int> EditPixels;

		/// <summary>Right click on a frame: hold it longer, edit its pixels, remove it.</summary>
		void TileMenu(int index, Point at)
		{
			var menu = new ContextMenuStrip();
			Theme.ApplyTo(menu);
			foreach (int times in new[] { 2, 3 })
			{
				int n = times;
				menu.Items.Add(L.T("strip.hold", n), null, (o, e) => { for (int k = 1; k < n; k++) Frames.Insert(index, Frames[index]); Fit(); Changed?.Invoke(); });
			}
			menu.Items.Add(L.T("strip.duplicateEnd"), null, (o, e) => { Frames.Add(Frames[index]); Fit(); Changed?.Invoke(); });
			if (EditPixels != null) menu.Items.Add(L.T("strip.pixels"), null, (o, e) => EditPixels(Frames[index]));
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add(L.T("strip.reverse"), null, (o, e) => { Frames.Reverse(); Changed?.Invoke(); });
			menu.Items.Add(L.T("strip.remove"), null, (o, e) => { Frames.RemoveAt(index); Fit(); Changed?.Invoke(); });
			menu.Show(this, at);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Theme.Surface);
			for (int i = 0; i < Frames.Count; i++)
			{
				var r = Tile(i);
				bool target = dragFrom >= 0 && i == dropAt && dropAt != dragFrom;
				Theme.FillRounded(g, i == dragFrom ? Theme.AccentSoft : Theme.SurfaceAlt, r, 6);
				Theme.DrawRounded(g, target ? Theme.Accent : Theme.Border, r, 6, target ? 2 : 1);
				Ui.DrawFrame(g, Doc?.Frame(Frames[i]), new Rectangle(r.X + Ui.S(6), r.Y + Ui.S(14), r.Width - Ui.S(12), r.Height - Ui.S(30)));
				TextRenderer.DrawText(g, Frames[i].ToString(), Ui.Font(10.5f), new Rectangle(r.X + Ui.S(4), r.Y + 2, r.Width, Ui.S(14)), Theme.TextMuted, TextFormatFlags.Left);
				if (TimeOf != null) TextRenderer.DrawText(g, TimeOf(i), Ui.Font(10), new Rectangle(r.X, r.Bottom - Ui.S(15), r.Width, Ui.S(14)), Theme.Accent, TextFormatFlags.HorizontalCenter);
				if (i == hover && dragFrom < 0)
				{
					var x = XBox(r);
					Theme.FillRounded(g, hoverX ? Theme.Danger : Theme.Border, x, x.Width / 2);
					TextRenderer.DrawText(g, "×", Ui.Font(13, true), x, hoverX ? Color.White : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				}
			}
			var add = Tile(Frames.Count);
			Theme.FillRounded(g, hover == Frames.Count ? Theme.AccentSoft : Theme.Surface, add, 6);
			Theme.DrawRounded(g, Theme.Accent, add, 6);
			TextRenderer.DrawText(g, "+", Ui.Font(24), add, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
	}

	/// <summary>The sprite sheet as a grid of numbered frames; clicking one raises <see cref="FrameClicked"/>.</summary>
	internal sealed class SheetView : ScrollableControl
	{
		public PetDoc Doc;
		public HashSet<int> Marked = new();
		public int Selected = -1;
		public event Action<int> FrameClicked;
		/// <summary>0: the frames in cells of the thumbnail size; otherwise the whole sheet at this zoom, with its grid.</summary>
		public float SheetZoom;
		int hover = -1;

		public SheetView()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			AutoScroll = true;
		}

		int Count => Doc?.Frames.Count ?? 0;
		int CellW => SheetZoom > 0 ? (int)(Doc.FrameW * SheetZoom) : Math.Max(Ui.Thumb + Ui.S(16), Ui.S(54));
		int CellH => SheetZoom > 0 ? (int)(Doc.FrameH * SheetZoom) : (int)(CellW * Math.Min(1.6f, Math.Max(0.8f, (Doc?.FrameH ?? 1) / (float)(Doc?.FrameW ?? 1))));
		int Cols => SheetZoom > 0 ? Math.Max(1, Doc.Root.Image.TilesX) : Math.Max(1, (ClientSize.Width - 4) / (CellW + 4));

		Rectangle Cell(int i) => SheetZoom > 0
			? new Rectangle(2 + i % Cols * CellW + AutoScrollPosition.X, 2 + i / Cols * CellH + AutoScrollPosition.Y, CellW, CellH)
			: new Rectangle(2 + i % Cols * (CellW + 4) + AutoScrollPosition.X, 2 + i / Cols * (CellH + 4) + AutoScrollPosition.Y, CellW, CellH);

		public void UpdateSize()
		{
			if (Doc == null || Count == 0) { AutoScrollMinSize = Size.Empty; Invalidate(); return; }
			int rows = (Count + Cols - 1) / Cols;
			AutoScrollMinSize = SheetZoom > 0 ? new Size(Cols * CellW + 4, rows * CellH + 4) : new Size(0, rows * (CellH + 4) + 4);
			Invalidate();
		}

		protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateSize(); }

		int Hit(Point p) { for (int i = 0; i < Count; i++) if (Cell(i).Contains(p)) return i; return -1; }

		protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); } }
		protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
		protected override void OnMouseClick(MouseEventArgs e) { int h = Hit(e.Location); if (h >= 0) { Selected = h; Invalidate(); FrameClicked?.Invoke(h); } }
		/// <summary>As a click on frame <paramref name="i"/> (the pictures of the wiki drive the windows with it).</summary>
		public void ClickFrame(int i) { if (i < 0 || i >= Count) return; Selected = i; Invalidate(); FrameClicked?.Invoke(i); }
		protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
		protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Theme.Surface);
			if (Doc == null || Count == 0)
			{
				TextRenderer.DrawText(g, L.T("sheet.empty"), Ui.Font(13), ClientRectangle, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
				return;
			}
			if (SheetZoom > 0) Checker(g, new Rectangle(2 + AutoScrollPosition.X, 2 + AutoScrollPosition.Y, Cols * CellW, (Count + Cols - 1) / Cols * CellH));
			for (int i = 0; i < Count; i++)
			{
				var r = Cell(i);
				if (!r.IntersectsWith(ClientRectangle)) continue;
				bool marked = Marked.Contains(i);
				if (SheetZoom > 0)
				{
					Ui.DrawFrame(g, Doc.Frame(i), r);
					using var pen = new Pen(i == hover ? Theme.Accent : Color.FromArgb(90, Theme.TextMuted)) { DashStyle = i == hover ? DashStyle.Solid : DashStyle.Dot };
					g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
				}
				else
				{
					Theme.FillRounded(g, marked ? Theme.AccentSoft : i == hover ? Theme.Hover : Theme.SurfaceAlt, r, 5);
					Theme.DrawRounded(g, marked || i == Selected ? Theme.Accent : Theme.Border, r, 5, i == Selected ? 2 : 1);
					Ui.DrawFrame(g, Doc.Frame(i), new Rectangle(r.X + Ui.S(5), r.Y + Ui.S(12), r.Width - Ui.S(10), r.Height - Ui.S(16)));
				}
				TextRenderer.DrawText(g, i.ToString(), Ui.Font(10.5f), new Rectangle(r.X + Ui.S(3), r.Y + 1, r.Width, Ui.S(14)), marked ? Theme.Accent : Theme.TextMuted, TextFormatFlags.Left);
			}
		}

		static void Checker(Graphics g, Rectangle r)
		{
			using var a = new SolidBrush(Theme.IsDark ? Color.FromArgb(48, 48, 52) : Color.FromArgb(236, 238, 241));
			using var b = new SolidBrush(Theme.IsDark ? Color.FromArgb(58, 58, 63) : Color.White);
			const int s = 8;
			for (int y = r.Y; y < r.Bottom; y += s)
				for (int x = r.X; x < r.Right; x += s)
					g.FillRectangle(((x - r.X) / s + (y - r.Y) / s) % 2 == 0 ? a : b, x, y, Math.Min(s, r.Right - x), Math.Min(s, r.Bottom - y));
		}
	}

	/// <summary>
	/// The animations that can follow (at the end, on a border or without ground): one row per animation with its
	/// chance, where it counts ("only") and a bar with its share.
	/// </summary>
	internal sealed class NextEditor : FlowLayoutPanel
	{
		PetDoc doc;
		Func<NextNode[]> get;
		Action<NextNode[]> set;
		public event Action Changed;

		public NextEditor()
		{
			FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoSize = true; Margin = new Padding(0); BackColor = Theme.Surface;
		}

		public void Bind(PetDoc d, Func<NextNode[]> getter, Action<NextNode[]> setter) { doc = d; get = getter; set = setter; Rebuild(); }

		public static readonly string[] OnlyKeys = { "none", "taskbar", "window", "horizontal", "horizontal+", "vertical", "vertical+", "windowSide", "petSide" };

		public void Rebuild()
		{
			SuspendLayout();
			foreach (Control c in Controls) c.Dispose();
			Controls.Clear();
			var list = get?.Invoke() ?? Array.Empty<NextNode>();
			int total = Math.Max(1, list.Sum(n => Math.Max(0, n.Probability)));
			// every animation of the pet, made once for all the rows
			var all = doc.Root.Animations.Animation.Select(a => new AnimItem(a.Id, a.Name)).ToArray();
			for (int i = 0; i < list.Length; i++)
			{
				int idx = i;
				var n = list[i];
				// light choice boxes (the lists of Windows cost 40 ms each to show: a pet with many "next" took seconds)
				var anim = new Choice(230) { Empty = "#" + n.Value + " ?" };
				anim.Items.AddRange(all);
				anim.SelectedIndex = Array.FindIndex(all, x => x.Id == n.Value);
				anim.SelectedIndexChanged += (s, e) => { if (anim.SelectedItem is AnimItem ai && ai.Id != list[idx].Value) { list[idx].Value = ai.Id; Commit(list); } };
				var only = new Choice(190);
				foreach (var k in OnlyKeys) only.Items.Add(L.T("only." + k));
				only.SelectedIndex = Math.Max(0, Array.IndexOf(OnlyKeys, string.IsNullOrWhiteSpace(n.OnlyFlag) ? "none" : n.OnlyFlag));
				only.SelectedIndexChanged += (s, e) => { list[idx].OnlyFlag = OnlyKeys[only.SelectedIndex]; Commit(list); };
				var prob = Ui.Number(0, 1000, n.Probability, 70);
				prob.ValueChanged += (s, e) => { list[idx].Probability = (int)prob.Value; Commit(list, typing: true); };
				var bar = new ShareBar { Share = Math.Max(0, n.Probability) / (float)total, Width = 110, Height = 28, Margin = new Padding(0, 0, 6, 6) };
				var del = new FlatButton("×") { Width = 32, Danger = true };
				del.Click += (s, e) => { var l = list.ToList(); l.RemoveAt(idx); Commit(l.ToArray(), rebuild: true); };
				var row = Ui.Row(new Label { Text = "→", AutoSize = true, Font = Ui.Font(14), ForeColor = Theme.TextMuted, Margin = new Padding(0, 4, 4, 0) }, anim, only, prob, bar, del);
				Controls.Add(row);
			}
			var add = new LinkLabel { Text = "+ " + L.T("next.add"), AutoSize = true, Font = Ui.Font(13), LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(4, 4, 0, 4) };
			add.Click += (s, e) =>
			{
				var l = list.ToList();
				l.Add(new NextNode { Probability = 10, OnlyFlag = "none", Value = doc.Root.Animations.Animation.FirstOrDefault()?.Id ?? 1 });
				Commit(l.ToArray(), rebuild: true);
			};
			Controls.Add(add);
			if (list.Length == 0) Controls.Add(Ui.Hint(L.T("next.none"), 600));
			ResumeLayout();
		}

		void Commit(NextNode[] list, bool typing = false, bool rebuild = false)
		{
			set(list);
			doc.Changed("next", typing);
			Changed?.Invoke();
			if (rebuild) Rebuild();
			else
			{
				// only the bars change
				int total = Math.Max(1, list.Sum(n => Math.Max(0, n.Probability)));
				int i = 0;
				foreach (var row in Controls.OfType<FlowLayoutPanel>())
				{
					var bar = row.Controls.OfType<ShareBar>().FirstOrDefault();
					if (bar != null && i < list.Length) { bar.Share = Math.Max(0, list[i].Probability) / (float)total; bar.Invalidate(); }
					i++;
				}
			}
		}

		sealed class ShareBar : Control
		{
			public float Share;
			public ShareBar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics; Theme.Prepare(g); g.Clear(Theme.Surface);
				int tw = Ui.S(40); var r = new RectangleF(0, Height / 2f - Ui.S(5), Width - tw, Ui.S(10));
				Theme.FillRounded(g, Theme.Hover, r, r.Height / 2);
				if (Share > 0) Theme.FillRounded(g, Theme.Accent, new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * Share), r.Height), r.Height / 2);
				TextRenderer.DrawText(g, $"{Share * 100:0}%", Ui.Font(12), new Rectangle((int)r.Right + 4, 0, tw, Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
			}
		}
	}

	/// <summary>An animation in a combo box: shows "name (#id)".</summary>
	internal sealed class AnimItem
	{
		public readonly int Id; readonly string name;
		public AnimItem(int id, string name) { Id = id; this.name = name; }
		public override string ToString() => $"{name}  (#{Id})";
	}
}

namespace PetEditor
{
	/// <summary>A file path that, when it does not fit, is shortened in the middle (the folder and the file stay visible).</summary>
	internal sealed class PathLabel : Label
	{
		public PathLabel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true); }

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.Clear(BackColor);
			TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
		}
	}
}

namespace PetEditor
{
	/// <summary>
	/// A search box with a magnifying glass: the short hint inside, the whole one in the tooltip; Esc clears it.
	/// The text box is <see cref="Box"/>.
	/// </summary>
	internal sealed class SearchBox : Panel
	{
		public readonly TextBox Box;
		readonly Label glass;

		public SearchBox(string shortHint, string hint)
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			BackColor = Theme.Surface; Height = 32; Width = 200; Margin = new Padding(0, 0, 8, 6); Cursor = Cursors.IBeam;
			glass = new Label { Text = "", Font = Ui.Icons, ForeColor = Theme.TextMuted, BackColor = Theme.Surface, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.IBeam };
			Box = new TextBox { BorderStyle = BorderStyle.None, AutoSize = false, Font = Ui.Font(13), BackColor = Theme.Surface, ForeColor = Theme.Text, PlaceholderText = shortHint };
			Controls.Add(glass); Controls.Add(Box);
			glass.Click += (s, e) => Box.Focus();
			Click += (s, e) => Box.Focus();
			Box.GotFocus += (s, e) => Invalidate();
			Box.LostFocus += (s, e) => Invalidate();
			Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && Box.Text.Length > 0) { Box.Text = ""; e.SuppressKeyPress = true; } };
			this.Tip(hint); glass.Tip(hint); Box.Tip(hint);
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);
			if (glass == null || Box == null) return;   // still being built
			int g = Height - 4;
			glass.Bounds = new Rectangle(3, 2, g, g);
			int x = glass.Right + 2;
			int h = Box.PreferredHeight;   // the height of its letters (it can come smaller after growing with the zoom)
			Box.Bounds = new Rectangle(x, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - x - 8), h);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Parent?.BackColor ?? Theme.Surface);
			var r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRounded(g, Theme.Surface, r, 6);
			bool focused = Box?.Focused == true;
			Theme.DrawRounded(g, focused ? Theme.Accent : Theme.Border, r, 6, focused ? 2 : 1);
		}
	}
}

namespace PetEditor
{
	/// <summary>
	/// A light choice box: looks like a list to choose from, but the list is only made when it is clicked (a list of
	/// Windows costs about 40 ms to show; "what comes next" had 32 of them). Few options open as a menu; many open
	/// with a search box on top.
	/// </summary>
	internal sealed class Choice : Control
	{
		public readonly List<object> Items = new();
		int selected = -1;
		bool hover;
		public event EventHandler SelectedIndexChanged;

		public Choice(int width)
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
			Font = Ui.Font(13); Width = width; Height = 28; Margin = new Padding(0, 0, 8, 6); Cursor = Cursors.Hand; TabStop = true;
		}

		public int SelectedIndex
		{
			get => selected;
			set { if (value == selected) return; selected = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
		}
		public object SelectedItem => selected >= 0 && selected < Items.Count ? Items[selected] : null;
		/// <summary>Shown when nothing is chosen (an animation that does not exist, for instance).</summary>
		public string Empty = "";

		protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
		protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
		protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
		protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
		protected override void OnClick(EventArgs e) { base.OnClick(e); Open(); }
		protected override bool IsInputKey(Keys k) => k is Keys.Down or Keys.Up or Keys.Enter or Keys.Space || base.IsInputKey(k);
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode is Keys.Enter or Keys.Space || (e.Alt && e.KeyCode == Keys.Down)) { Open(); e.Handled = true; }
			else if (e.KeyCode == Keys.Down && selected < Items.Count - 1) { SelectedIndex = selected + 1; e.Handled = true; }
			else if (e.KeyCode == Keys.Up && selected > 0) { SelectedIndex = selected - 1; e.Handled = true; }
			base.OnKeyDown(e);
		}

		void Open()
		{
			Focus();
			if (Items.Count <= 24)
			{
				var menu = new ContextMenuStrip();
				Theme.ApplyTo(menu);
				for (int i = 0; i < Items.Count; i++)
				{
					int k = i;
					var it = new ToolStripMenuItem(Items[i].ToString()) { Checked = i == selected };
					it.Click += (s, e) => SelectedIndex = k;
					menu.Items.Add(it);
				}
				menu.Show(this, new Point(0, Height));
				return;
			}
			// many: a search box over the list (typing a part of the name or the number finds it)
			var drop = new ToolStripDropDown { Padding = new Padding(1), AutoClose = true };
			var panel = new Panel { Size = new Size(Math.Max(Width, Ui.S(280)), Ui.S(320)), BackColor = Theme.Surface };
			var box = new TextBox { Dock = DockStyle.Top, Font = Ui.Font(13), BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Surface, ForeColor = Theme.Text, PlaceholderText = L.T("anim.searchShort") };
			var list = new ListBox { Dock = DockStyle.Fill, Font = Ui.Font(13), BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, IntegralHeight = false };
			void Fill()
			{
				string q = box.Text.Trim();
				list.BeginUpdate(); list.Items.Clear();
				foreach (var it in Items) if (q.Length == 0 || it.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)) list.Items.Add(it);
				list.EndUpdate();
				if (list.Items.Count > 0) list.SelectedItem = q.Length == 0 && SelectedItem != null ? SelectedItem : list.Items[0];
			}
			void Take() { if (list.SelectedItem != null) { SelectedIndex = Items.IndexOf(list.SelectedItem); drop.Close(); } }
			box.TextChanged += (s, e) => Fill();
			box.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Enter) { Take(); e.SuppressKeyPress = true; }
				else if (e.KeyCode == Keys.Down && list.SelectedIndex < list.Items.Count - 1) { list.SelectedIndex++; e.SuppressKeyPress = true; }
				else if (e.KeyCode == Keys.Up && list.SelectedIndex > 0) { list.SelectedIndex--; e.SuppressKeyPress = true; }
				else if (e.KeyCode == Keys.Escape) drop.Close();
			};
			list.Click += (s, e) => Take();
			panel.Controls.Add(list); panel.Controls.Add(box);
			Fill();
			drop.Items.Add(new ToolStripControlHost(panel) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = panel.Size });
			drop.Opened += (s, e) => box.Focus();
			drop.Show(this, new Point(0, Height));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			g.Clear(Parent?.BackColor ?? Theme.Surface);
			var r = new Rectangle(0, 0, Width - 1, Height - 1);
			Theme.FillRounded(g, hover ? Theme.Hover : Theme.Surface, r, 4);
			Theme.DrawRounded(g, Focused ? Theme.Accent : Theme.Border, r, 4);
			int arrow = Ui.S(22);
			string text = SelectedItem?.ToString() ?? Empty;
			TextRenderer.DrawText(g, text, Font, new Rectangle(Ui.S(6), 0, Width - arrow - Ui.S(6), Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			TextRenderer.DrawText(g, "▾", Ui.Font(12), new Rectangle(Width - arrow, 0, arrow - 2, Height), Theme.TextMuted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
		}
	}
}
