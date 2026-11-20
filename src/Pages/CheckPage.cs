using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>Full check: every problem of the pet, errors first; a click goes to the place of the problem.</summary>
	internal sealed class CheckPage : Page
	{
		ListBox list;
		Label summary;
		int filter;   // 0 all, 1 errors, 2 warnings
		List<Validator.Problem> problems = new();

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			var top = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 0, 4), BackColor = Theme.Surface };
			var col = Ui.Column(Ui.Title(L.T("chk.title"), 20), Ui.Hint(L.T("chk.intro"), 900));
			summary = Ui.Text("", false, 14); summary.Font = Ui.Font(14, true);
			var filters = Ui.Row();
			foreach (var (i, key) in new[] { (0, "chk.all"), (1, "chk.errors"), (2, "chk.warnings") })
			{
				var b = new FlatButton(L.T(key), primary: i == filter);
				b.Click += (s, e) => { filter = i; foreach (FlatButton x in filters.Controls) { x.Primary = x == b; x.Font = Ui.Font(13, x.Primary); x.Invalidate(); } Fill(); };
				filters.Controls.Add(b);
			}
			var again = new FlatButton(L.T("chk.again")); again.Click += (s, e) => Run();
			filters.Controls.Add(again);
			col.Controls.Add(Ui.Row(summary));
			col.Controls.Add(filters);
			top.Controls.Add(col);
			list = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawVariable, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, IntegralHeight = false };
			list.MeasureItem += (s, e) =>
			{
				var p = (Validator.Problem)list.Items[e.Index];
				e.ItemHeight = Math.Min(255, TextRenderer.MeasureText(p.Text, Ui.Font(13), new Size(Math.Max(200, list.Width - Ui.S(52) - 18), 1000), TextFormatFlags.WordBreak).Height + Ui.S(18));
			};
			list.DrawItem += (s, e) =>
			{
				if (e.Index < 0) return;
				var p = (Validator.Problem)list.Items[e.Index];
				bool sel = (e.State & DrawItemState.Selected) != 0;
				using (var b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
				TextRenderer.DrawText(e.Graphics, p.Error ? "⛔" : "⚠", Ui.Font(14), new Rectangle(e.Bounds.X + Ui.S(8), e.Bounds.Y + Ui.S(7), Ui.S(30), Ui.S(24)), p.Error ? Theme.Danger : Ui.Warning, TextFormatFlags.Left);
				TextRenderer.DrawText(e.Graphics, p.Text, Ui.Font(13), new Rectangle(e.Bounds.X + Ui.S(42), e.Bounds.Y + Ui.S(9), e.Bounds.Width - Ui.S(52), e.Bounds.Height - Ui.S(9)), Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
				using var pen = new Pen(Theme.Border); e.Graphics.DrawLine(pen, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
			};
			list.Click += (s, e) => { if (list.SelectedItem is Validator.Problem p) Main.Go(p.Page, p.Anim); };
			card.Controls.Add(list);
			card.Controls.Add(top);
			Controls.Add(card);
			Run();
		}

		void Run() { problems = Validator.Run(Doc).OrderByDescending(p => p.Error).ToList(); Fill(); }

		void Fill()
		{
			int errors = problems.Count(p => p.Error), warnings = problems.Count - errors;
			summary.Text = problems.Count == 0 ? "✓  " + L.T("chk.allGood") : L.T("chk.summary", errors, warnings);
			summary.ForeColor = errors > 0 ? Theme.Danger : problems.Count == 0 ? Theme.Success : Theme.Text;
			list.BeginUpdate();
			list.Items.Clear();
			foreach (var p in problems.Where(p => filter == 0 || (filter == 1) == p.Error)) list.Items.Add(p);
			list.EndUpdate();
		}

		public override void Reload(string what) => Run();
		public override void Shown() => Run();
	}
}
