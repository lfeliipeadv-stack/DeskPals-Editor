using System.Drawing;
using System.Drawing.Imaging;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// Sprite sheet: import / export the PNG, the grid (columns × rows), the transparent colour and the skins; the
	/// sheet is shown with its grid and the number of every frame.
	/// </summary>
	internal sealed class SheetPage : Page
	{
		SheetView view;
		NumericUpDown cols, rows, skins;
		ComboBox transp, zoom;
		Label info;
		bool loading;
		static readonly string[] Colors = { "Magenta", "Fuchsia", "Lime", "Cyan", "White", "Black", "Transparent" };

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background };
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			var left = new Card { Dock = DockStyle.Fill };
			var col = Ui.Column(Ui.Title(L.T("sheet.page"), 20), Ui.Hint(L.T("sheet.intro"), 290));
			var import = new FlatButton(L.T("sheet.import"), primary: true); import.Click += (s, e) => Import();
			var export = new FlatButton(L.T("sheet.export")); export.Click += (s, e) => Export();
			col.Controls.Add(Ui.Row(import, export));
			var build = new FlatButton(L.T("build.button")); build.Click += (s, e) => SheetBuilder.Show(Main, Doc);
			col.Controls.Add(build);
			col.Controls.Add(Ui.Hint(L.T("build.hint"), 290));
			var pixels = new FlatButton(L.T("px.button")).Tip(L.T("tip.px.button"));
			pixels.Click += (s, e) => { int f = FramePicker.Pick(Main, Doc, L.T("px.button")); if (f >= 0) PixelEditor.Show(Main, Doc, f); };
			var skin = new FlatButton(L.T("skin.button")).Tip(L.T("tip.skin.button"));
			skin.Click += (s, e) => SkinMaker.Show(Main, Doc);
			var opt = new FlatButton(L.T("opt.button")).Tip(L.T("tip.opt.button"));
			opt.Click += (s, e) => Optimizer.Show(Main, Doc);
			col.Controls.Add(Ui.Row(pixels, skin));
			col.Controls.Add(opt);
			cols = Ui.Number(1, 200, Doc.Root.Image.TilesX, 80);
			rows = Ui.Number(1, 400, Doc.Root.Image.TilesY, 80);
			col.Controls.Add(Ui.Row(Ui.Labeled(L.T("sheet.cols"), cols), Ui.Labeled(L.T("sheet.rows"), rows)));
			transp = Ui.Combo(170);
			foreach (var c in Colors) transp.Items.Add(c);
			string cur = string.IsNullOrWhiteSpace(Doc.Root.Image.Transparency) ? "Magenta" : Doc.Root.Image.Transparency;
			if (!Colors.Contains(cur)) transp.Items.Add(cur);
			transp.SelectedItem = cur;
			col.Controls.Add(Ui.Labeled(L.T("sheet.transp"), transp, L.T("sheet.transpHint")));
			skins = Ui.Number(1, 20, Math.Max(1, Doc.Root.Image.Skins), 80);
			col.Controls.Add(Ui.Labeled(L.T("sheet.skins"), skins, L.T("sheet.skinsHint")));
			zoom = Ui.Combo(120);
			zoom.Items.AddRange(new object[] { L.T("sheet.zoomThumbs"), "1x", "2x", "3x", "4x" });
			zoom.SelectedIndex = 2;
			col.Controls.Add(Ui.Labeled(L.T("sheet.view"), zoom));
			info = Ui.Text("", true, 12.5f); info.MaximumSize = new Size(290, 0);
			col.Controls.Add(info);
			col.Location = new Point(16, 14);
			left.Controls.Add(col);
			root.Controls.Add(left, 0, 0);

			var right = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			view = new SheetView { Dock = DockStyle.Fill, Doc = Doc, SheetZoom = 2 };
			// a double click on a frame opens it in the pixel retouch
			view.DoubleClick += (s, e) => { if (view.Selected >= 0) PixelEditor.Show(Main, Doc, view.Selected); };
			right.Controls.Add(view);
			root.Controls.Add(right, 1, 0);
			Controls.Add(root);

			cols.ValueChanged += (s, e) => Grid();
			rows.ValueChanged += (s, e) => Grid();
			skins.ValueChanged += (s, e) => { if (loading) return; Doc.Root.Image.Skins = (int)skins.Value; Doc.Changed("imageInfo"); };
			transp.SelectedIndexChanged += (s, e) => { if (loading) return; Doc.Root.Image.Transparency = (string)transp.SelectedItem; Doc.Changed("image"); };
			zoom.SelectedIndexChanged += (s, e) => { view.SheetZoom = zoom.SelectedIndex; view.UpdateSize(); };
			Info();
		}

		void Grid()
		{
			if (loading) return;
			Doc.Root.Image.TilesX = (int)cols.Value;
			Doc.Root.Image.TilesY = (int)rows.Value;
			Doc.Changed("image");
		}

		void Import()
		{
			using var dlg = new OpenFileDialog { Filter = L.T("sheet.filter"), Title = L.T("sheet.import") };
			if (dlg.ShowDialog(this) != DialogResult.OK) return;
			try
			{
				using var img = new Bitmap(dlg.FileName);
				using var ms = new MemoryStream();
				img.Save(ms, ImageFormat.Png);
				Doc.Root.Image.Png = Convert.ToBase64String(ms.ToArray());
				// a guess of the grid when the old one does not fit the new picture
				if (img.Width % Doc.Root.Image.TilesX != 0 || img.Height % Doc.Root.Image.TilesY != 0)
				{
					int side = Math.Min(img.Width, img.Height);
					Doc.Root.Image.TilesX = Math.Max(1, img.Width / side);
					Doc.Root.Image.TilesY = Math.Max(1, img.Height / side);
				}
				Doc.Changed("image");
				Main.Message(L.T("sheet.imported", img.Width, img.Height, Doc.Frames.Count, Doc.FrameW, Doc.FrameH));
			}
			catch (Exception ex) { Main.Message(L.T("msg.openFail", ex.Message)); }
		}

		void Export()
		{
			if (string.IsNullOrWhiteSpace(Doc.Root.Image.Png)) return;
			using var dlg = new SaveFileDialog { Filter = "PNG|*.png", FileName = (Doc.Name + "-sprites.png") };
			if (dlg.ShowDialog(this) != DialogResult.OK) return;
			File.WriteAllBytes(dlg.FileName, Convert.FromBase64String(Doc.Root.Image.Png.Trim()));
		}

		void Info()
		{
			var s = Doc.Sheet;
			info.Text = s == null ? L.T("sheet.empty")
				: L.T("sheet.info", s.Width, s.Height, Doc.Frames.Count, Doc.FrameW, Doc.FrameH)
				  + (s.Width % Math.Max(1, Doc.Root.Image.TilesX) != 0 || s.Height % Math.Max(1, Doc.Root.Image.TilesY) != 0 ? "\n\n⚠ " + L.T("sheet.notExact") : "");
			view.UpdateSize();
		}

		public override void Reload(string what)
		{
			if (what is not ("all" or "image")) return;
			loading = true;
			cols.Value = Math.Max(1, Math.Min(200, Doc.Root.Image.TilesX));
			rows.Value = Math.Max(1, Math.Min(400, Doc.Root.Image.TilesY));
			skins.Value = Math.Max(1, Math.Min(20, Doc.Root.Image.Skins));
			loading = false;
			view.Doc = Doc;
			Info();
		}
	}
}
