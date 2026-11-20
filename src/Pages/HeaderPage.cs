using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>Header: the pet's name, title, author, version, description and icon.</summary>
	internal sealed class HeaderPage : Page
	{
		PictureBox icon;
		bool loading;

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Build();
		}

		void Build()
		{
			loading = true;
			SuspendLayout();
			Controls.Clear();
			var h = Doc.Root.Header;
			var card = new Card { Dock = DockStyle.Fill };
			var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
			var col = Ui.Column(Ui.Title(L.T("head.title"), 20), Ui.Hint(L.T("head.intro"), 820));

			TextBox F(string label, string value, Action<string> set, int width, string hint = null)
			{
				var t = Ui.Box(value, width);
				t.TextChanged += (s, e) => { if (loading) return; set(t.Text); Doc.Changed("header", typing: true); };
				col.Controls.Add(Ui.Labeled(label, t, hint));
				return t;
			}
			F(L.T("head.petname"), h.Petname, v => h.Petname = v, 260, L.T("head.petnameHint"));
			F(L.T("head.titleField"), h.Title, v => h.Title = v, 420);
			col.Controls.Add(Ui.Row(
				Ui.Labeled(L.T("head.author"), Box(h.Author, v => h.Author = v, 260)),
				Ui.Labeled(L.T("head.version"), Box(h.Version, v => h.Version = v, 120))));
			var info = Ui.Box((h.Info ?? "").Replace("[br]", Environment.NewLine), 620);
			info.Multiline = true; info.Height = 110; info.ScrollBars = ScrollBars.Vertical;
			info.TextChanged += (s, e) => { if (loading) return; h.Info = info.Text.Replace("\r\n", "[br]").Replace("\n", "[br]"); Doc.Changed("header", typing: true); };
			col.Controls.Add(Ui.Labeled(L.T("head.info"), info, L.T("head.infoHint")));

			// icon
			icon = new PictureBox { Size = new Size(96, 96), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.SurfaceAlt, Margin = new Padding(0, 0, 16, 0) };
			ShowIcon();
			var fromFrame = new FlatButton(L.T("head.iconFrame"));
			fromFrame.Click += (s, e) =>
			{
				int f = FramePicker.Pick(Main, Doc, L.T("head.iconFrame"));
				if (f < 0) return;
				h.Icon = Icons.MakeIco(Doc.Frame(f));
				Doc.Changed("header");
				ShowIcon();
			};
			var fromFile = new FlatButton(L.T("head.iconFile"));
			fromFile.Click += (s, e) =>
			{
				using var dlg = new OpenFileDialog { Filter = L.T("head.iconFilter") };
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				try
				{
					if (dlg.FileName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) h.Icon = Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
					else using (var b = new Bitmap(dlg.FileName)) h.Icon = Icons.MakeIco(b);
					Doc.Changed("header");
					ShowIcon();
				}
				catch (Exception ex) { Main.Message(L.T("msg.openFail", ex.Message)); }
			};
			col.Controls.Add(Ui.Text(L.T("head.icon"), true, 12));
			col.Controls.Add(Ui.Row(icon, Ui.Column(fromFrame, fromFile, Ui.Hint(L.T("head.iconHint"), 420))));
			scroll.Controls.Add(col);
			col.Location = new Point(4, 4);
			card.Controls.Add(scroll);
			Controls.Add(card);
			ResumeLayout();
			loading = false;
		}

		TextBox Box(string value, Action<string> set, int width)
		{
			var t = Ui.Box(value, width);
			t.TextChanged += (s, e) => { if (loading) return; set(t.Text); Doc.Changed("header", typing: true); };
			return t;
		}

		void ShowIcon()
		{
			var old = icon.Image;
			icon.Image = Icons.FromIco(Doc.Root.Header.Icon, 64);
			old?.Dispose();
		}

		public override void Reload(string what) { if (what == "all") Build(); }
	}

	/// <summary>A window with the sprite sheet to choose one frame.</summary>
	internal static class FramePicker
	{
		public static int Pick(IWin32Window owner, PetDoc doc, string title)
		{
			int chosen = -1;
			using var f = new Form { Text = title, Size = new Size(720, 560), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Background };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var view = new SheetView { Dock = DockStyle.Fill, Doc = doc };
			view.FrameClicked += i => { chosen = i; f.DialogResult = DialogResult.OK; f.Close(); };
			var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Theme.Background };
			wrap.Controls.Add(view);
			f.Controls.Add(wrap);
			f.Shown += (s, e) => view.UpdateSize();
			return f.ShowDialog(owner) == DialogResult.OK ? chosen : -1;
		}
	}
}
