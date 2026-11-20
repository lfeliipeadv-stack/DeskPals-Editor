using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>"+ From a model": choose a ready-made behaviour, click its frames in the sheet, and it is added and wired.</summary>
	internal static class BehaviorDialog
	{
		/// <summary>The id of the new animation, or -1.</summary>
		public static int Show(MainForm main, PetDoc doc)
		{
			if (doc.Frames.Count == 0) { main.Message(L.T("sheet.empty")); return -1; }
			int created = -1;
			using var f = new Form { Text = L.T("beh.title"), Size = new Size(980, 640), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Background, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Background, Padding = new Padding(14) };
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			var list = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Ui.S(68), BorderStyle = BorderStyle.None, BackColor = Theme.Surface, IntegralHeight = false };
			foreach (var k in Behaviors.All) list.Items.Add(k);
			list.DrawItem += (s, e) =>
			{
				if (e.Index < 0) return;
				var k = (Behaviors.Kind)list.Items[e.Index];
				bool sel = (e.State & DrawItemState.Selected) != 0;
				using (var b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
				TextRenderer.DrawText(e.Graphics, L.T("beh." + k.Key), Ui.Font(13.5f, true), new Rectangle(e.Bounds.X + Ui.S(10), e.Bounds.Y + Ui.S(6), e.Bounds.Width - Ui.S(14), Ui.S(20)), sel ? Theme.Accent : Theme.Text, TextFormatFlags.Left);
				TextRenderer.DrawText(e.Graphics, L.T("beh." + k.Key + ".d"), Ui.Font(11.5f), new Rectangle(e.Bounds.X + Ui.S(10), e.Bounds.Y + Ui.S(28), e.Bounds.Width - Ui.S(14), Ui.S(36)), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
			};
			var left = new Card { Dock = DockStyle.Fill };
			left.Controls.Add(list);
			root.Controls.Add(left, 0, 0);

			var right = new Card { Dock = DockStyle.Fill, Margin = new Padding(0) };
			var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 250, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Surface };
			var name = Ui.Box("", 220);
			var lottery = new Toggle(L.T("beh.lottery"), true);
			var strip = new FrameStrip { Doc = doc, Width = 560 };
			strip.Fit();
			var need = Ui.Hint("", 560);
			top.Controls.Add(Ui.Row(Ui.Labeled(L.T("beh.name"), name), lottery));
			top.Controls.Add(Ui.Text(L.T("beh.frames"), false, 13.5f));
			top.Controls.Add(need);
			top.Controls.Add(strip);
			var sheet = new SheetView { Dock = DockStyle.Fill, Doc = doc };
			sheet.FrameClicked += i => { strip.Frames.Add(i); strip.Fit(); strip.Invalidate(); };
			strip.AddClicked += () => { };
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(0, 6, 0, 0) };
			var ok = new FlatButton(L.T("beh.create"), primary: true);
			var cancel = new FlatButton(L.T("build.cancel"));
			cancel.Click += (s, e) => f.Close();
			bar.Controls.Add(ok); bar.Controls.Add(cancel);
			right.Controls.Add(sheet);
			right.Controls.Add(bar);
			right.Controls.Add(top);
			root.Controls.Add(right, 1, 0);
			f.Controls.Add(root);

			list.SelectedIndexChanged += (s, e) =>
			{
				if (list.SelectedItem is not Behaviors.Kind k) return;
				name.Text = k.Name;
				lottery.On = k.Lottery; lottery.Invalidate();
				need.Text = L.T("beh.need", k.MinFrames) + "  " + L.T("beh." + k.Key + ".d");
			};
			list.SelectedIndex = 0;
			ok.Click += (s, e) =>
			{
				if (list.SelectedItem is not Behaviors.Kind k) return;
				if (strip.Frames.Count < k.MinFrames) { main.Message(L.T("beh.need", k.MinFrames)); return; }
				string nm = string.IsNullOrWhiteSpace(name.Text) ? k.Name : name.Text.Trim();
				var a = Behaviors.Create(doc.Root, k.Key, strip.Frames.ToArray(), nm, 1, lottery.On ? 10 : 0);
				created = a.Id;
				doc.Changed("anims");
				f.Close();
			};
			f.Shown += (s, e) => sheet.UpdateSize();
			f.ShowDialog(main);
			return created;
		}
	}
}
