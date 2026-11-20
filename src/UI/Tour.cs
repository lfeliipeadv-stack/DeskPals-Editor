using System.Drawing;
using DesktopPet;

namespace PetEditor
{
	/// <summary>
	/// The welcome tour: a balloon goes through the sections (each one opens while it is explained) and ends at the
	/// test button. Shown the first time; Settings can show it again.
	/// </summary>
	internal static class Tour
	{
		static readonly (string page, string text)[] Steps =
		{
			("project", "tour.welcome"),
			("project", "tour.project"),
			("sheet", "tour.sheet"),
			("animations", "tour.animations"),
			("spawns", "tour.spawns"),
			("check", "tour.check"),
			("animations", "tour.test"),
			("settings", "tour.settings"),
		};

		public static void Start(MainForm main)
		{
			int step = 0;
			var bubble = new Bubble { Owner = main };
			void Show()
			{
				var (page, key) = Steps[step];
				main.Go(page);
				bubble.Set(L.T(key), $"{step + 1} / {Steps.Length}", step == Steps.Length - 1);
				// next to the section in the side bar; the last steps point at the test button and the settings
				var r = main.Nav.ItemScreenBounds(page);
				var p = key == "tour.welcome" ? new Point(main.Left + main.Width / 2 - bubble.Width / 2, main.Top + main.Height / 2 - bubble.Height / 2)
					: key == "tour.test" ? new Point(main.Right - bubble.Width - 30, main.Top + 110)
					: new Point(r.Right + 14, Math.Max(main.Top + 40, r.Top - 20));
				bubble.Location = p;
				if (!bubble.Visible) bubble.Show(main);
				bubble.Invalidate();
			}
			bubble.Next += () => { if (++step >= Steps.Length) Done(); else Show(); };
			bubble.Skip += Done;
			void Done()
			{
				bubble.Close();
				EditorSettings.Current.TourDone = true;
				EditorSettings.Save();
			}
			Show();
		}

		/// <summary>A rounded balloon with the text, the step and the buttons.</summary>
		sealed class Bubble : Form
		{
			public event Action Next, Skip;
			readonly Label text, count;
			readonly FlatButton next, skip;

			public Bubble()
			{
				FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = false;
				Size = Ui.S(new Size(380, 200)); BackColor = Theme.Accent; Padding = new Padding(2);
				var inner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(16, 14, 16, 12) };
				text = new Label { Dock = DockStyle.Fill, Font = Ui.Font(13.5f), ForeColor = Theme.Text, BackColor = Theme.Surface };
				var bar = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Theme.Surface };
				count = new Label { AutoSize = true, Location = new Point(0, 10), Font = Ui.Font(12), ForeColor = Theme.TextMuted, BackColor = Theme.Surface };
				skip = new FlatButton(L.T("tour.skip"));
				next = new FlatButton(L.T("tour.next"), primary: true);
				skip.Click += (s, e) => Skip?.Invoke();
				next.Click += (s, e) => Next?.Invoke();
				bar.Controls.AddRange(new Control[] { count, skip, next });
				bar.Layout += (s, e) => PlaceButtons();
				inner.Controls.Add(text);
				inner.Controls.Add(bar);
				Controls.Add(inner);
				Ui.Scaled(this);   // grows with the zoom of Windows (the window itself has its size above)
				KeyPreview = true;
				KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Skip?.Invoke(); if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Right) Next?.Invoke(); };
			}

			public void Set(string t, string step, bool last)
			{
				text.Text = t;
				count.Text = step;
				next.Text = L.T(last ? "tour.done" : "tour.next");
				int h = TextRenderer.MeasureText(t, text.Font, new Size(Width - Ui.S(40), 1000), TextFormatFlags.WordBreak).Height;
				Height = h + Ui.S(90);
				PlaceButtons();
			}

			void PlaceButtons()
			{
				var bar = next.Parent;
				if (bar == null) return;
				var n = new Point(bar.Width - next.Width, Ui.S(4)); if (next.Location != n) next.Location = n;
				var k = new Point(next.Left - skip.Width - Ui.S(8), Ui.S(4)); if (skip.Location != k) skip.Location = k;
			}

			protected override bool ShowWithoutActivation => false;
		}
	}
}
