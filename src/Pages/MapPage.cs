using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// Map of the animations: every animation is a card, placed in columns by how far it is from an entrance; arrows
	/// show what comes next (grey at the end, orange on a border, blue without ground). Drag to move, wheel to zoom,
	/// click a card to see its arrows, double click to open it.
	/// </summary>
	internal sealed class MapPage : Page
	{
		MapView view;

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			var top = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Surface, Padding = new Padding(0, 0, 0, 6) };   // as tall as its texts (the intro breaks its line when narrow)
			var col = Ui.Column(Ui.Title(L.T("map.title"), 20), Ui.Hint(L.T("map.intro"), 1000));
			var fit = new FlatButton(L.T("map.fit")); fit.Click += (s, e) => view.Fit();
			var legend = Ui.Text(L.T("map.legend"), true, 12); legend.Margin = new Padding(12, 8, 0, 0);
			col.Controls.Add(Ui.Row(fit, legend));
			top.Controls.Add(col);
			view = new MapView { Dock = DockStyle.Fill, Doc = Doc };
			view.Open += id => Main.Go("animations", id);
			card.Controls.Add(view);
			card.Controls.Add(top);
			Controls.Add(card);
		}

		public override void Reload(string what) { view.Doc = Doc; view.Build(); }
		public override void Shown() { view.Build(); view.Fit(); }

		sealed class MapView : Control
		{
			public PetDoc Doc;
			public event Action<int> Open;
			readonly Dictionary<int, RectangleF> pos = new();
			readonly List<(int from, int to, int kind, int prob)> edges = new();
			float zoom = 1; PointF pan = new(20, 20);
			Point dragStart; PointF panStart; bool dragging;
			int selected = -1, hover = -1;
			const float W = 170, H = 54, GapX = 90, GapY = 18;

			public MapView()
			{
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			}

			/// <summary>Columns: 0 = first animations of the entrances, then one column per step; what nothing reaches goes last.</summary>
			public void Build()
			{
				pos.Clear(); edges.Clear();
				if (Doc == null) return;
				var anims = Doc.Root.Animations.Animation.GroupBy(a => a.Id).Select(g => g.First()).ToList();
				var ids = anims.Select(a => a.Id).ToHashSet();
				foreach (var a in anims)
				{
					void Add(NextNode[] l, int kind) { foreach (var n in l ?? Array.Empty<NextNode>()) if (ids.Contains(n.Value)) edges.Add((a.Id, n.Value, kind, n.Probability)); }
					Add(a.Sequence.Next, 0); Add(a.Border?.Next, 1); Add(a.Gravity?.Next, 2);
				}
				foreach (var c in Doc.Root.Childs.Child) if (ids.Contains(c.Id) && ids.Contains(c.Next)) edges.Add((c.Id, c.Next, 3, 100));
				var depth = new Dictionary<int, int>();
				var q = new Queue<int>();
				foreach (var s in Doc.Root.Spawns.Spawn) if (ids.Contains(s.Next) && depth.TryAdd(s.Next, 0)) q.Enqueue(s.Next);
				while (q.Count > 0)
				{
					int id = q.Dequeue();
					foreach (var e in edges.Where(e => e.from == id))
						if (depth.TryAdd(e.to, depth[id] + 1)) q.Enqueue(e.to);
				}
				int last = depth.Count == 0 ? 0 : depth.Values.Max() + 1;
				foreach (var a in anims) if (!depth.ContainsKey(a.Id)) depth[a.Id] = last;
				// a long column is split into several side by side (a pet with hundreds of scenes stays readable)
				int maxRows = Math.Max(8, (int)Math.Ceiling(Math.Sqrt(anims.Count) * 1.3));
				float x = 0;
				foreach (var g in anims.GroupBy(a => depth[a.Id]).OrderBy(g => g.Key))
				{
					var items = g.OrderBy(a => a.Id).ToList();
					int cols = (items.Count + maxRows - 1) / maxRows;
					for (int i = 0; i < items.Count; i++)
						pos[items[i].Id] = new RectangleF(x + i / maxRows * (W + 24), i % maxRows * (H + GapY), W, H);
					x += cols * (W + 24) - 24 + GapX;
				}
				Invalidate();
			}

			public void Fit()
			{
				if (pos.Count == 0) return;
				float w = pos.Values.Max(r => r.Right) + 40, h = pos.Values.Max(r => r.Bottom) + 40;
				zoom = Math.Max(0.15f, Math.Min(1.2f * Ui.Scale, Math.Min(Width / w, Height / h)));
				pan = new PointF(20, 20);
				Invalidate();
			}

			RectangleF Screen(RectangleF r) => new(pan.X + r.X * zoom, pan.Y + r.Y * zoom, r.Width * zoom, r.Height * zoom);
			int Hit(Point p) { foreach (var kv in pos) if (Screen(kv.Value).Contains(p)) return kv.Key; return -1; }

			protected override void OnMouseDown(MouseEventArgs e) { dragStart = e.Location; panStart = pan; dragging = false; Focus(); }
			protected override void OnMouseMove(MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left && (Math.Abs(e.X - dragStart.X) > 3 || Math.Abs(e.Y - dragStart.Y) > 3))
				{
					dragging = true;
					pan = new PointF(panStart.X + e.X - dragStart.X, panStart.Y + e.Y - dragStart.Y);
					Invalidate();
					return;
				}
				int h = Hit(e.Location);
				if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.SizeAll; Invalidate(); }
			}
			protected override void OnMouseUp(MouseEventArgs e) { if (!dragging) { selected = Hit(e.Location); Invalidate(); } dragging = false; }
			protected override void OnMouseDoubleClick(MouseEventArgs e) { int h = Hit(e.Location); if (h >= 0) Open?.Invoke(h); }
			protected override void OnMouseWheel(MouseEventArgs e)
			{
				float old = zoom;
				zoom = Math.Max(0.1f, Math.Min(2.5f, zoom * (e.Delta > 0 ? 1.15f : 1 / 1.15f)));
				// zoom around the mouse
				pan = new PointF(e.X - (e.X - pan.X) * zoom / old, e.Y - (e.Y - pan.Y) * zoom / old);
				Invalidate();
			}

			static readonly Color[] Kinds = { Color.FromArgb(140, 146, 156), Color.FromArgb(230, 140, 40), Color.FromArgb(60, 130, 230), Color.FromArgb(160, 90, 210) };

			protected override void OnPaint(PaintEventArgs e)
			{
				var g = e.Graphics; Theme.Prepare(g);
				g.Clear(Theme.Surface);
				if (Doc == null || pos.Count == 0) return;
				// arrows: those of the chosen card on top and stronger
				foreach (var pass in new[] { false, true })
					foreach (var ed in edges)
					{
						bool mine = selected >= 0 && (ed.from == selected || ed.to == selected);
						if (mine != pass) continue;
						if (selected >= 0 && !mine && pos.Count > 60) continue;   // a big map shows only the arrows of the chosen card
						var a = Screen(pos[ed.from]); var b = Screen(pos[ed.to]);
						var c = Kinds[ed.kind];
						using var pen = new Pen(Color.FromArgb(mine ? 255 : selected >= 0 ? 40 : 110, c), mine ? 2.2f : 1.2f) { CustomEndCap = new AdjustableArrowCap(4, 5) };
						if (ed.kind == 1) pen.DashStyle = DashStyle.Dash;
						if (ed.kind == 2) pen.DashStyle = DashStyle.Dot;
						if (ed.from == ed.to)
						{
							g.DrawArc(pen, a.Right - 18 * zoom, a.Y - 14 * zoom, 26 * zoom, 22 * zoom, 160, 270);
							continue;
						}
						var p1 = new PointF(a.Right, a.Y + a.Height / 2);
						var p2 = new PointF(b.X, b.Y + b.Height / 2);
						float dx = Math.Max(40 * zoom, Math.Abs(p2.X - p1.X) / 2);
						if (p2.X <= p1.X) { p2 = new PointF(b.Right, b.Y + b.Height / 2); dx = 60 * zoom; g.DrawBezier(pen, p1, new PointF(p1.X + dx, p1.Y), new PointF(p2.X + dx, p2.Y), p2); }
						else g.DrawBezier(pen, p1, new PointF(p1.X + dx, p1.Y), new PointF(p2.X - dx, p2.Y), p2);
					}
				// cards
				// the map has its own zoom (the boxes too): the letters follow it, not the zoom of Windows
				var font = Ui.Font(Math.Max(7, 12.5f * zoom) / Ui.Scale, false);   // kept by Ui (not disposed here)
				var bold = Ui.Font(Math.Max(7, 12.5f * zoom) / Ui.Scale, true);
				foreach (var kv in pos)
				{
					var r = Screen(kv.Value);
					if (!r.IntersectsWith(ClientRectangle)) continue;
					var a = Doc.Anim(kv.Key);
					bool sel = kv.Key == selected, hov = kv.Key == hover;
					bool spawn = Doc.Root.Spawns.Spawn.Any(s => s.Next == kv.Key);
					Theme.FillRounded(g, sel ? Theme.AccentSoft : hov ? Theme.Hover : Theme.SurfaceAlt, r, 6 * zoom);
					Theme.DrawRounded(g, sel ? Theme.Accent : spawn ? Theme.Accent : Theme.Border, r, 6 * zoom, sel || spawn ? 2 : 1);
					if (zoom < 0.35f) continue;
					var f = a.Sequence.Frame;
					float th = r.Height - 8 * zoom;
					Ui.DrawFrame(g, Doc.Frame(f?.Length > 0 ? f[0] : -1), Rectangle.Round(new RectangleF(r.X + 4 * zoom, r.Y + 4 * zoom, th, th)));
					var tr = Rectangle.Round(new RectangleF(r.X + th + 10 * zoom, r.Y + 6 * zoom, r.Width - th - 14 * zoom, r.Height / 2));
					TextRenderer.DrawText(g, a.Name, sel ? bold : font, tr, Theme.Text, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
					tr.Y += (int)(r.Height / 2.2f);
					TextRenderer.DrawText(g, "#" + a.Id + (spawn ? "  · " + L.T("map.entrance") : ""), font, tr, Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
				}
			}
		}
	}
}
