using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>A page of the editor (a section of the side bar).</summary>
	internal abstract class Page : Panel
	{
		protected PetDoc Doc;
		protected MainForm Main;

		protected Page() { Dock = DockStyle.Fill; BackColor = Theme.Background; Padding = new Padding(16, 14, 16, 14); Ui.AutoFit(this); }

		/// <summary>Build the page for a document (called again after opening another pet).</summary>
		public virtual void Bind(MainForm main, PetDoc doc) { Main = main; Doc = doc; }
		/// <summary>The document changed elsewhere (undo, another page): show it again.</summary>
		public virtual void Reload(string what) { }
		/// <summary>The page is shown / hidden.</summary>
		public virtual void Shown() { }
		public virtual void Hidden() { }
	}

	/// <summary>
	/// The editor window: header with the file actions and "Test on the desktop", the side bar with the sections,
	/// and the page of the chosen section.
	/// </summary>
	internal sealed partial class MainForm : Form
	{
		public PetDoc Doc { get; private set; }
		readonly NavBar nav = new();
		readonly Panel header = new() { Dock = DockStyle.Top, Height = 64 };
		readonly Panel host = new() { Dock = DockStyle.Fill };
		readonly Dictionary<string, Page> pages = new();
		Page current;
		FlatButton undoBtn, redoBtn, checkBtn;
		Label titleLbl, pathLbl;
		PictureBox iconBox;

		public MainForm(string open)
		{
			Text = L.T("app.name");
			Icon = Ui.AppIcon;
			Ui.SharpIcons(this);
			StartPosition = FormStartPosition.CenterScreen;
			var s = EditorSettings.Current;
			// the sizes grow with the zoom of Windows, but never past the screen
			var screen = Screen.PrimaryScreen?.WorkingArea.Size ?? new Size(1920, 1040);
			Size Fit(Size z) => new(Math.Min(z.Width, screen.Width), Math.Min(z.Height, screen.Height));
			MinimumSize = Fit(Ui.S(new Size(980, 640)));
			Size = Fit(new Size(Math.Max(Ui.S(1000), s.WinW), Math.Max(Ui.S(680), s.WinH)));
			if (s.WinMax) WindowState = FormWindowState.Maximized;
			KeyPreview = true;
			AutoScaleMode = AutoScaleMode.None;   // the editor grows by itself (Ui.Scaled): sizes are written at 100%
			Font = Ui.Font(13);
			Ui.Scaled(this);
			AcceptDrops(this);
			Theme.ApplyWindowChrome(this);
			BuildChrome();
			Theme.Changed += ThemeChanged;
			Load += (o, e) =>
			{
				Timing.Mark("load"); PetDoc d = null;
				string path = open ?? (s.OpenLast ? s.Recent.FirstOrDefault(File.Exists) : null);
				if (path != null) { try { d = PetDoc.Open(path); } catch (Exception ex) { Message(L.T("msg.openFail", ex.Message)); } }
				Timing.Mark("pet opened"); SetDoc(d ?? Template.New(), Program.StartPage ?? (d == null && open == null ? "project" : "animations")); Timing.Mark("page shown");
				Application.Idle += FirstIdle;
				void FirstIdle(object o2, EventArgs e2) { Application.Idle -= FirstIdle; Timing.Mark("first idle (window ready)"); }
				if (Program.SnapDir != null) { BeginInvoke(new Action(async () => await Snap(Program.SnapDir))); return; }
				if (Program.WikiDir != null) { BeginInvoke(new Action(async () => await WikiShots(Program.WikiDir))); return; }
				BeginInvoke(new Action(() => { OfferRecovery(); if (!s.TourDone) Tour.Start(this); }));
			};
			// a copy of the changes every minute: brought back if the editor closes without saving
			var autosave = new System.Windows.Forms.Timer { Interval = 60_000 };
			autosave.Tick += (o, e) => { if (Doc?.Dirty == true) Recovery.Save(Doc); };
			autosave.Start();
		}

		/// <summary>Work left by an editor that did not close normally: open it again?</summary>
		void OfferRecovery()
		{
			foreach (var item in Recovery.Pending())
			{
				var r = MessageBox.Show(this, L.T("rec.ask", item.Name, item.When.ToString("g"), item.Original ?? L.T("doc.notSaved")), L.T("app.name"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if (r == DialogResult.Cancel) return;
				if (r == DialogResult.Yes)
				{
					try
					{
						var d = PetDoc.Open(item.Copy);
						d.FilePath = item.Original;
						d.Changed("all");
						SetDoc(d);
						Recovery.Discard(item);
						Recovery.Save(d);
						return;
					}
					catch (Exception ex) { Message(L.T("msg.openFail", ex.Message)); }
				}
				else Recovery.Discard(item);
			}
		}

		// ------------------------------------------------------------------ files dropped on the window

		static readonly string[] PictureTypes = { ".png", ".gif", ".bmp", ".jpg", ".jpeg" };

		/// <summary>
		/// The window takes files. Only the window is registered: Windows looks for a place to drop from the control
		/// under the mouse up to its window (registering every control cost seconds when a page was shown).
		/// </summary>
		void AcceptDrops(Control c)
		{
			AllowDrop = true;
			DragEnter += OnDragEnter;
			DragDrop += OnDragDrop;
		}

		/// <summary>What was dropped: a pet (animations.xml or its folder), pictures, or nothing the editor can use.</summary>
		static (string pet, string[] pictures) Dropped(IDataObject data)
		{
			if (data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return (null, null);
			foreach (var f in files)
			{
				if (Directory.Exists(f) && File.Exists(Path.Combine(f, "animations.xml"))) return (Path.Combine(f, "animations.xml"), null);
				if (f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && File.Exists(f)) return (f, null);
			}
			var pics = files.Where(f => File.Exists(f) && PictureTypes.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
			return (null, pics.Length > 0 ? pics : null);
		}

		void OnDragEnter(object sender, DragEventArgs e)
		{
			var (pet, pics) = Dropped(e.Data);
			e.Effect = pet != null || pics != null ? DragDropEffects.Copy : DragDropEffects.None;
		}

		void OnDragDrop(object sender, DragEventArgs e)
		{
			var (pet, pics) = Dropped(e.Data);
			if (pet == null && pics == null) return;
			var at = new Point(e.X, e.Y);
			// after the drop has ended (Explorer waits for it): a small menu with what can be done
			BeginInvoke(new Action(() =>
			{
				Activate();
				var menu = new ContextMenuStrip();
				Theme.ApplyTo(menu);
				if (pet != null)
				{
					menu.Items.Add(L.T("drop.open", Path.GetFileName(Path.GetDirectoryName(pet))), null, (o, a) => OpenPath(pet));
					if (Doc != null && !string.Equals(Doc.FilePath, pet, StringComparison.OrdinalIgnoreCase))
						menu.Items.Add(L.T("drop.copy"), null, (o, a) => { int id = ImportDialog.Show(this, Doc, pet); if (id >= 0) Go("animations", id); });
				}
				else
				{
					menu.Items.Add(L.T("drop.wizard", pics.Length), null, (o, a) => Wizard.Show(this, pics));
					if (Doc != null) menu.Items.Add(L.T("drop.sheet"), null, (o, a) => SheetBuilder.Show(this, Doc, pics));
				}
				menu.Show(at);
			}));
		}

		/// <summary>
		/// Open the editor again (a new size is only taken at the start): the same pet, on the Settings page. Changes
		/// not saved are asked first, as when closing.
		/// </summary>
		public void Restart()
		{
			if (!ConfirmDiscard()) return;
			var args = new List<string> { "--page=settings" };
			if (Doc?.FilePath != null) args.Insert(0, Doc.FilePath);
			var start = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = false };
			foreach (var a in args) start.ArgumentList.Add(a);
			System.Diagnostics.Process.Start(start);
			Close();
		}

		/// <summary>The side bar (for the tour).</summary>
		public NavBar Nav => nav;

		// ------------------------------------------------------------------ window parts

		void BuildChrome()
		{
			SuspendLayout();
			Controls.Clear();
			BackColor = Theme.Background;
			header.Controls.Clear();
			header.BackColor = Theme.Surface;
			header.Paint += (o, e) => { using var p = new Pen(Theme.Border); e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1); };
			// three columns: the pet (icon, name, file) | the file buttons | check and test
			var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Surface, Padding = new Padding(12, 0, 12, 0) };
			grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
			grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			var pet = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(0) };
			iconBox = new PictureBox { Location = new Point(2, 10), Size = new Size(44, 44), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Surface };
			titleLbl = new Label { Location = new Point(54, 9), AutoSize = false, Size = new Size(280, 28), AutoEllipsis = true, Font = Ui.Font(18, true), ForeColor = Theme.Text, BackColor = Theme.Surface };
			pathLbl = new PathLabel { Location = new Point(55, 38), AutoSize = false, Size = new Size(280, 18), AutoEllipsis = true, Font = Ui.Font(11.5f), ForeColor = Theme.TextMuted, BackColor = Theme.Surface };
			pet.Controls.AddRange(new Control[] { iconBox, titleLbl, pathLbl });
			var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(0, 15, 0, 0), Margin = new Padding(0) };
			FlatButton B(string key, Action a) { var b = new FlatButton(L.T(key)).Tip(L.T("tip." + key)); b.Click += (o, e) => a(); tools.Controls.Add(b); return b; }
			FlatButton newBtn = null;
			newBtn = B("act.new", () =>
			{
				// the assistant (from pictures) or the ready model
				var menu = new ContextMenuStrip();
				Theme.ApplyTo(menu);
				menu.Items.Add(L.T("proj.wizard"), null, (o, e) => Wizard.Show(this));
				menu.Items.Add(L.T("proj.new"), null, (o, e) => NewPet());
				menu.Show(newBtn, new Point(0, newBtn.Height));
			});
			B("act.open", () => OpenDialog(false)); B("act.save", () => Save(false));
			// undo and redo as small icon buttons: the bar fits also in a narrow window
			undoBtn = B("act.undo", () => Doc?.Undo()); undoBtn.Font = Ui.Icons; undoBtn.Text = "";
			redoBtn = B("act.redo", () => Doc?.Redo()); redoBtn.Font = Ui.Icons; redoBtn.Text = "";
			var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(0, 15, 0, 0), Margin = new Padding(0) };
			checkBtn = new FlatButton(L.T("act.check")).Tip(L.T("tip.act.check"));
			checkBtn.Click += (o, e) => Go("check");
			var test = new FlatButton("▶  " + L.T("act.test"), primary: true).Tip(L.T("tip.act.test"));
			test.Click += (o, e) => DesktopTest.Run(this, Doc);
			right.Controls.Add(checkBtn); right.Controls.Add(test);
			grid.Controls.Add(pet, 0, 0); grid.Controls.Add(tools, 1, 0); grid.Controls.Add(right, 2, 0);
			header.Controls.Add(grid);
			// in a narrow window the name of the pet gives room to the buttons (its name and file end in "…")
			bool fitting = false;
			grid.Layout += (o, e) =>
			{
				if (fitting || grid.ColumnStyles.Count < 3) return;
				fitting = true;
				try
				{
					int buttons = tools.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal) + tools.Padding.Horizontal;
					int petW = grid.ClientSize.Width - grid.Padding.Horizontal - right.Width - right.Margin.Horizontal - buttons - Ui.S(8);
					petW = Math.Max(Ui.S(110), Math.Min(Ui.S(340), petW));
					if (Math.Abs(grid.ColumnStyles[0].Width - petW) > 1) grid.ColumnStyles[0].Width = petW;
					int textW = Math.Max(Ui.S(60), petW - titleLbl.Left - Ui.S(6));
					titleLbl.Width = textW; pathLbl.Width = textW;
				}
				finally { fitting = false; }
			};

			nav.Items.Clear();
			void Item(string key, bool enabled = true) => nav.Items.Add((key, L.T("nav." + key), enabled));
			foreach (var k in new[] { "project", "header", "sheet", "animations", "spawns", "childs", "sounds" }) Item(k);
			nav.Items.Add(("-", L.T("nav.tools"), false));
			foreach (var k in new[] { "check", "map", "xml" }) Item(k);
			nav.Items.Add(("-", "", false));
			Item("settings");
			nav.Selected -= Show;
			nav.Selected += Show;
			host.BackColor = Theme.Background;
			Controls.Add(host);
			Controls.Add(nav);
			Controls.Add(header);
			ResumeLayout();
		}

		void ThemeChanged()
		{
			if (IsDisposed) return;
			Theme.ApplyWindowChrome(this);
			string at = nav.Current ?? "animations";
			foreach (var p in pages.Values) p.Dispose();
			pages.Clear();
			current = null;
			BuildChrome();
			UpdateState();
			RunCheck();
			Show(at);
			nav.Select(at);
		}

		/// <summary>Language changed: rebuild everything with the new texts.</summary>
		public void Retranslate() { Text = L.T("app.name"); ThemeChanged(); }

		Page Create(string key) => key switch
		{
			"project" => new ProjectPage(),
			"header" => new HeaderPage(),
			"sheet" => new SheetPage(),
			"animations" => new AnimationsPage(),
			"spawns" => new SpawnsPage(),
			"childs" => new ChildsPage(),
			"sounds" => new SoundsPage(),
			"settings" => new SettingsPage(),
			"check" => new CheckPage(),
			"map" => new MapPage(),
			"xml" => new XmlPage(),
			_ => null,
		};

		void Show(string key)
		{
			if (Doc == null) return;
			if (!pages.TryGetValue(key, out var page))
			{
				page = Create(key);
				if (page == null) return;
				page.Bind(this, Doc);
				pages[key] = page;
			}
			if (current == page) return;
			current?.Hidden();
			host.SuspendLayout();
			host.Controls.Clear();
			Ui.Grow(page);   // grown while it is not in the window yet (fast)
			host.Controls.Add(page);
			host.ResumeLayout();
			current = page;
			page.Shown();
		}

		/// <summary>Go to a section (and, for animations, to one of them).</summary>
		public void Go(string key, int animation = -1)
		{
			nav.Select(key);
			if (animation >= 0 && pages.TryGetValue("animations", out var p) && p is AnimationsPage ap) ap.SelectAnimation(animation);
		}

		// ------------------------------------------------------------------ document

		public void SetDoc(PetDoc d, string page = "animations")
		{
			if (Doc != null) { Doc.Updated -= DocUpdated; Doc.StateChanged -= UpdateState; if (Doc != d) Recovery.Clear(Doc); }
			foreach (var p in pages.Values) { p.Hidden(); p.Dispose(); }
			pages.Clear(); current = null;
			Doc = d;
			Problems = null;   // checked at once for the new pet
			Doc.Updated += DocUpdated;
			Doc.StateChanged += UpdateState;
			UpdateState();
			nav.Select(page);
		}

		void DocUpdated(string what)
		{
			foreach (var p in pages.Values) p.Reload(what);
			if (what is "header" or "image" or "all") UpdateState();
			DesktopTest.Refresh(Doc);
		}

		void UpdateState()
		{
			if (Doc == null) return;
			Text = $"{(Doc.Dirty ? "• " : "")}{Doc.Name} — {L.T("app.name")}";
			titleLbl.Text = Doc.Name;
			pathLbl.Text = Doc.FilePath ?? L.T("doc.notSaved");
			pathLbl.Tip(Doc.FilePath ?? "");   // the whole path under the mouse
			var old = iconBox.Image;
			iconBox.Image = Icons.FromIco(Doc.Root.Header.Icon, 48) ?? (Doc.Frame(0) is Bitmap f ? new Bitmap(f) : null);
			old?.Dispose();
			undoBtn.Enabled = Doc.CanUndo; redoBtn.Enabled = Doc.CanRedo;
			// the checker runs a moment after the last change (not at every key)
			if (checkTimer == null)
			{
				checkTimer = new System.Windows.Forms.Timer { Interval = 400 };
				checkTimer.Tick += (o, e) => { checkTimer.Stop(); RunCheck(); };
			}
			checkTimer.Stop();
			if (Problems == null) RunCheck(); else checkTimer.Start();
		}

		System.Windows.Forms.Timer checkTimer;

		/// <summary>What the checker found in the pet now (the side bar, the list of animations and the header show it).</summary>
		public List<Validator.Problem> Problems { get; private set; }
		public event Action ProblemsChanged;

		void RunCheck()
		{
			if (Doc == null || IsDisposed) return;
			try { Problems = Validator.Run(Doc); } catch { Problems = new(); }
			int errors = Problems.Count(p => p.Error), warnings = Problems.Count - errors;
			checkBtn.Text = errors + warnings == 0 ? "✓  " + L.T("act.checkOk") : L.T("act.checkCount", errors, warnings);
			checkBtn.Danger = errors > 0;
			checkBtn.Invalidate();
			// a number per section of the side bar, and the checker's own count
			nav.Badges.Clear();
			foreach (var g in Problems.Where(p => p.Page != null).GroupBy(p => p.Page))
				nav.Badges[g.Key] = (g.Count(p => p.Error), g.Count(p => !p.Error));
			nav.Invalidate();
			ProblemsChanged?.Invoke();
		}

		/// <summary>Ask to save the changes. False if the user cancelled.</summary>
		public bool ConfirmDiscard()
		{
			if (Doc == null || !Doc.Dirty) return true;
			var r = MessageBox.Show(this, L.T("msg.saveChanges", Doc.Name), L.T("app.name"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
			if (r == DialogResult.Cancel) return false;
			if (r == DialogResult.Yes) return Save(false);
			return true;
		}

		public void NewPet()
		{
			if (!ConfirmDiscard()) return;
			SetDoc(Template.New(), "header");
		}

		public void OpenDialog(bool folder)
		{
			if (!ConfirmDiscard()) return;
			string path = null;
			if (folder)
			{
				using var dlg = new FolderBrowserDialog { Description = L.T("open.folder"), UseDescriptionForTitle = true };
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				path = dlg.SelectedPath;
				if (!File.Exists(Path.Combine(path, "animations.xml"))) { Message(L.T("msg.noXmlInFolder")); return; }
			}
			else
			{
				using var dlg = new OpenFileDialog { Filter = L.T("open.filter"), Title = L.T("act.open") };
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				path = dlg.FileName;
			}
			OpenPath(path, confirm: false);
		}

		public void OpenPath(string path, bool confirm = true)
		{
			if (confirm && !ConfirmDiscard()) return;
			try
			{
				var d = PetDoc.Open(path);
				EditorSettings.Current.AddRecent(d.FilePath);
				SetDoc(d);
			}
			catch (Exception ex) { Message(L.T("msg.openFail", ex.Message)); }
		}

		public bool Save(bool saveAs)
		{
			if (Doc == null) return false;
			string path = Doc.FilePath;
			if (saveAs || path == null)
			{
				using var dlg = new SaveFileDialog { Filter = L.T("open.filter"), FileName = "animations.xml", Title = L.T("act.saveAs") };
				if (dlg.ShowDialog(this) != DialogResult.OK) return false;
				path = dlg.FileName;
			}
			var (errors, _) = Validator.Count(Doc);
			if (errors > 0 && MessageBox.Show(this, L.T("msg.saveWithErrors", errors), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
			{
				Go("check");
				return false;
			}
			try { Recovery.Clear(Doc); Doc.Save(path); Recovery.Clear(Doc); return true; }
			catch (Exception ex) { Message(L.T("msg.saveFail", ex.Message)); return false; }
		}

		/// <summary>Texts, buttons and boxes that go past the right edge of what holds them (the check of the pictures).</summary>
		static IEnumerable<string> Cuts(Control root)
		{
			foreach (var c in AllControls(root))
			{
				if (!c.Visible || c is not (Label or FlatButton or Toggle or TextBoxBase or ComboBox or Choice or NumericUpDown or Card)) continue;
				if (c is PathLabel || (c is Label l && l.AutoEllipsis)) continue;
				var host = c.Parent;
				while (host != null && host.AutoSize && host is not Form) host = host.Parent;
				if (host == null || !host.IsHandleCreated || !c.IsHandleCreated) continue;
				var r = host.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
				int over = r.Right - host.ClientSize.Width;
				string t = (c.Text ?? "").Replace('\n', ' ').Replace('\r', ' ');
				if (t.Length > 50) t = t.Substring(0, 50);
				if (over > 1) yield return $"{c.GetType().Name} “{t}” {L.T("snap.cut")} ({over} px)";
			}
		}

		static IEnumerable<Control> AllControls(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var k in AllControls(c)) yield return k; } }

		/// <summary>Developer: a picture of every page in the light and the dark theme (and the cuts found), then close.</summary>
		async Task Snap(string dir)
		{
			Directory.CreateDirectory(dir);
			var cuts = new List<string>();
			File.WriteAllText(Path.Combine(dir, "escala.txt"), $"zoom do Windows {DeviceDpi * 100 / 96}%  escala do editor {Ui.Scale}");
			foreach (var mode in new[] { "light", "dark" })
			{
				Theme.Mode = mode;
				if (!Program.SnapSize.IsEmpty) { WindowState = FormWindowState.Normal; Size = Program.SnapSize; }
				ThemeChanged();
				foreach (var key in new[] { "project", "header", "sheet", "animations", "spawns", "childs", "sounds", "check", "map", "xml", "settings" })
				{
					nav.Select(key);
					await Task.Delay(key == "animations" ? 1500 : 500);
					using var b = new Bitmap(Width, Height);
					DrawToBitmap(b, new Rectangle(0, 0, Width, Height));
					b.Save(Path.Combine(dir, $"{mode}-{key}.png"));
					if (mode == "light") cuts.AddRange(Cuts(this).Select(c => $"{Width}x{Height} {key}: {c}"));
				}
				await SnapDialogs(dir, mode, cuts);
			}
			File.WriteAllLines(Path.Combine(dir, "cortes.txt"), cuts.Count == 0 ? new[] { "nenhum corte" } : cuts);
			Environment.Exit(0);
		}

		/// <summary>The windows of the editor (assistant, import, gallery…): each one opened, pictured, checked and closed.</summary>
		async Task SnapDialogs(string dir, string mode, List<string> cuts)
		{
			// a few frames of the pet as pictures, for the windows that start from pictures
			string pics = Path.Combine(Path.GetTempPath(), "PetEditor-snap");
			Directory.CreateDirectory(pics);
			var files = new List<string>();
			for (int i = 0; i < Math.Min(6, Doc.Frames.Count); i++) { string p = Path.Combine(pics, $"frame_{i + 1}.png"); Doc.Frame(i)?.Save(p); files.Add(p); }
			var steps = new (string key, Action open)[]
			{
				("wizard", () => Wizard.Show(this, files.ToArray())),
				("behaviors", () => BehaviorDialog.Show(this, Doc)),
				("import", () => { if (Doc.FilePath != null) ImportDialog.Show(this, Doc, Doc.FilePath); }),
				("gallery", () => GalleryDialog.Show(this, Doc)),
				("pixels", () => PixelEditor.Show(this, Doc, 0)),
				("skin", () => SkinMaker.Show(this, Doc)),
				("optimizer", () => Optimizer.Show(this, Doc)),
				("build", () => SheetBuilder.Show(this, Doc, files.ToArray())),
				("framePicker", () => FramePicker.Pick(this, Doc, L.T("head.iconFrame"))),
			};
			foreach (var (key, open) in steps)
			{
				var done = new TaskCompletionSource();
				void Opened(Form f)
				{
					Ui.DialogShown -= Opened;
					f.BeginInvoke(new Action(async () =>
					{
						await Task.Delay(700);
						using (var b = new Bitmap(f.Width, f.Height))
						{
							f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
							b.Save(Path.Combine(dir, $"{mode}-dlg-{key}.png"));
						}
						if (mode == "light") cuts.AddRange(Cuts(f).Select(c => $"{f.Width}x{f.Height} {key}: {c}"));
						f.DialogResult = DialogResult.Cancel;
						f.Close();
						done.TrySetResult();
					}));
				}
				Ui.DialogShown += Opened;
				BeginInvoke(open);
				if (await Task.WhenAny(done.Task, Task.Delay(10000)) != done.Task) { Ui.DialogShown -= Opened; cuts.Add($"{key}: a janela não abriu"); }
				await Task.Delay(300);
			}
		}

		public void Message(string text) => MessageBox.Show(this, text, L.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);

		// ------------------------------------------------------------------ keys and closing

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			switch (keyData)
			{
				case Keys.Control | Keys.S: Save(false); return true;
				case Keys.Control | Keys.Shift | Keys.S: Save(true); return true;
				case Keys.Control | Keys.O: OpenDialog(false); return true;
				case Keys.Control | Keys.N: NewPet(); return true;
				case Keys.F5: DesktopTest.Run(this, Doc); return true;
			}
			var focused = KeyboardControl();
			bool typing = focused is TextBoxBase or ComboBox or UpDownBase;
			// undo / redo of the pet (a text box keeps its own Ctrl+Z while typing)
			if ((keyData == (Keys.Control | Keys.Z) || keyData == (Keys.Control | Keys.Y)) && !typing)
			{
				if (keyData == (Keys.Control | Keys.Z)) Doc?.Undo(); else Doc?.Redo();
				return true;
			}
			// the preview of the animations: Space pauses, → one step, R from the start (not while typing)
			if (!typing && current is AnimationsPage ap && ap.PreviewKey(keyData)) return true;
			return base.ProcessCmdKey(ref msg, keyData);
		}

		/// <summary>The control that has the keyboard (inside the dividers and panels, not only the first level).</summary>
		Control KeyboardControl()
		{
			Control c = ActiveControl;
			while (c is ContainerControl cc && cc.ActiveControl != null) c = cc.ActiveControl;
			return c;
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (!ConfirmDiscard()) { e.Cancel = true; return; }
			if (Doc != null) Recovery.Clear(Doc);
			var s = EditorSettings.Current;
			s.WinMax = WindowState == FormWindowState.Maximized;
			if (WindowState == FormWindowState.Normal) { s.WinW = Width; s.WinH = Height; }
			EditorSettings.Save();
			DesktopTest.Close();
			base.OnFormClosing(e);
		}
	}
}
