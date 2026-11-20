using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// A small desktop with a taskbar where the pet plays as in DesktopPet: the same steps, movements, borders,
	/// gravity, turns, next animations, child windows and sounds (the window edges of the real desktop are not here).
	/// </summary>
	internal sealed class PreviewStage : Control
	{
		public PetDoc Doc;
		/// <summary>When an animation ends it starts again instead of going on to the next one.</summary>
		public bool LoopOne;
		public double Speed = 1;
		public bool Paused { get; private set; }
		public event Action<string> Status;

		sealed class Actor
		{
			public AnimationNode Anim;
			public int Step, TotalSteps, Frame;
			public double X, Y, OffsetY, Opacity = 1;
			public double Sx, Sy, Ex, Ey;
			public int IntervalStart, IntervalEnd;
			public bool Left = true;
			public bool Child;
			public bool OnWindow;   // standing on the test window
			public int PrevFrame = -1; public double PrevX, PrevY;   // the step before (onion skin)
			public double Wait;     // ms until the next step
			public bool Gone;
		}

		readonly List<Actor> actors = new();
		Actor pet;
		readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
		readonly System.Diagnostics.Stopwatch clock = new();
		double last;
		readonly Random rand = new();
		int randS = 50;
		string lastInfo = "";
		const int TaskbarH = 26;

		public PreviewStage()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			timer.Tick += (s, e) => Tick();
			clock.Start();
			Disposed += (s, e) => timer.Dispose();
		}

		int Zoom => Math.Max(1, EditorSettings.Current.PreviewZoom);
		int PetW => (Doc?.FrameW ?? 32) * Zoom;
		int PetH => (Doc?.FrameH ?? 32) * Zoom;
		int Floor => Height - TaskbarH;

		ExprContext Ctx(Actor parent = null) => new()
		{
			ScreenW = Width, ScreenH = Height, AreaW = Width, AreaH = Floor, ImageW = PetW, ImageH = PetH,
			ImageX = parent?.X ?? -1, ImageY = parent?.Y ?? -1, RandS = randS, Scale = Zoom, ParentFlipped = parent != null && !parent.Left, Rand = rand,
		};

		// ------------------------------------------------------------------ control

		/// <summary>Play an animation from the middle of the taskbar.</summary>
		public void Play(int id)
		{
			actors.Clear();
			pet = new Actor { X = Width / 2 - PetW / 2, Y = Floor - PetH, Left = pet?.Left ?? true };
			actors.Add(pet);
			Start(pet, id);
			Paused = false;
			last = clock.Elapsed.TotalMilliseconds;
			timer.Start();
			Invalidate();
		}

		/// <summary>Arrive on the stage as a spawn does (its position, then its first animation).</summary>
		public void PlaySpawn(SpawnNode s)
		{
			actors.Clear();
			randS = rand.Next(10, 90);
			var c = Ctx();
			double x = Expr.Value(s.X, c), y = Expr.Value(s.Y, c);
			pet = new Actor { Left = pet?.Left ?? true };
			pet.X = pet.Left ? x : Width - x - PetW;
			pet.Y = y;
			actors.Add(pet);
			Start(pet, s.Next);
			Paused = false;
			last = clock.Elapsed.TotalMilliseconds;
			timer.Start();
		}

		/// <summary>A random spawn, as when the pet comes back on the screen.</summary>
		void Respawn()
		{
			var spawns = Doc.Root.Spawns.Spawn;
			if (spawns.Length == 0) { Play(Doc.Root.Animations.Animation.FirstOrDefault()?.Id ?? 1); return; }
			int sum = spawns.Sum(s => Math.Max(0, s.Probability)), r = rand.Next(0, Math.Max(1, sum)), acc = 0;
			foreach (var s in spawns) { acc += Math.Max(0, s.Probability); if (r < acc) { PlaySpawn(s); return; } }
			PlaySpawn(spawns[0]);
		}

		public void Pause(bool p) { Paused = p; if (!p) last = clock.Elapsed.TotalMilliseconds; Invalidate(); }

		/// <summary>End the animation now and go on to its next one.</summary>
		public void Skip()
		{
			if (pet?.Anim == null) return;
			pet.Step = pet.TotalSteps;
			Paused = false;
			pet.Wait = 0;
		}

		public void Stop() { timer.Stop(); actors.Clear(); pet = null; Invalidate(); }

		public int CurrentId => pet?.Anim?.Id ?? -1;

		// ------------------------------------------------------------------ the engine rules

		void Start(Actor a, int id)
		{
			var an = Doc?.Anim(id);
			if (an == null) { if (a.Child) a.Gone = true; else Respawn(); return; }
			var c = Ctx(a);
			a.Anim = an;
			a.Step = 0;
			int frames = an.Sequence.Frame?.Length ?? 0;
			int repeat = (int)Expr.Value(an.Sequence.RepeatCount, c);
			a.TotalSteps = frames + (frames - an.Sequence.RepeatFromFrame) * Math.Max(0, repeat);
			// the values of the pet's own animation see its own position (as DesktopPet does)
			c.ImageX = a.X; c.ImageY = a.Y; c.ParentFlipped = false;
			a.Sx = Expr.Value(an.Start.X, c) * Zoom; a.Sy = Expr.Value(an.Start.Y, c) * Zoom;
			a.Ex = Expr.Value(an.End.X, c) * Zoom; a.Ey = Expr.Value(an.End.Y, c) * Zoom;
			a.IntervalStart = (int)Expr.Value(an.Start.Interval, c, 200);
			a.IntervalEnd = (int)Expr.Value(an.End.Interval, c, 200);
			a.OffsetY = an.Start.OffsetY * Zoom;
			a.Wait = 0;
			if (!a.Child)
			{
				lastInfo = an.Name;
				Status?.Invoke(an.Name);
				if (EditorSettings.Current.PreviewSound) Sound(id);
			}
			// child windows of this animation
			foreach (var ch in Doc.Root.Childs.Child.Where(x => x.Id == id))
			{
				var pc = Ctx(a);
				var k = new Actor { Child = true, Left = a.Left, X = Expr.Value(ch.X, pc), Y = Expr.Value(ch.Y, pc) };
				actors.Add(k);
				Start(k, ch.Next);
			}
		}

		void Sound(int id)
		{
			var s = Doc.Root.Sounds.Sound.FirstOrDefault(x => x.Id == id);
			if (s == null || rand.Next(0, 100) >= s.Probability) return;
			SoundPlayer.Play(s);
		}

		void Tick()
		{
			if (Doc == null || pet == null) return;
			double now = clock.Elapsed.TotalMilliseconds, dt = (now - last) * Speed;
			last = now;
			if (Paused) return;
			if (tossing && pet != null) TossStep(dt);
			foreach (var a in actors.ToList())
			{
				if (a.Gone) continue;
				a.Wait -= dt;
				int guard = 0;
				while (a.Wait <= 0 && !a.Gone && guard++ < 4) StepActor(a);
			}
			actors.RemoveAll(a => a.Gone && a != pet);
			Invalidate();
		}

		int Index(AnimationNode an, int step)
		{
			var f = an.Sequence.Frame;
			if (f == null || f.Length == 0) return -1;
			int rf = Math.Max(0, Math.Min(an.Sequence.RepeatFromFrame, f.Length - 1));
			return step < f.Length ? f[step] : f[(step - f.Length + rf) % Math.Max(1, f.Length - rf) + rf];
		}

		void StepActor(Actor a)
		{
			var an = a.Anim;
			int t = Math.Max(1, a.TotalSteps);
			int spriteStep = Math.Min(a.Step, t - 1);
			a.PrevFrame = a.Frame; a.PrevX = a.X; a.PrevY = a.Y;
			a.Frame = Index(an, spriteStep);
			double frac = (double)a.Step / t;
			int interval = (int)(a.IntervalStart + (a.IntervalEnd - a.IntervalStart) * frac);
			a.Wait += Math.Max(10, interval);
			a.Opacity = an.Start.Opacity + (an.End.Opacity - an.Start.Opacity) * frac;
			a.OffsetY = (an.Start.OffsetY + (an.End.OffsetY - an.Start.OffsetY) * frac) * Zoom;

			double x = a.Sx, y = a.Sy;
			if (t > 1) { x += (a.Ex - a.Sx) * a.Step / (t - 1.0); y += (a.Ey - a.Sy) * a.Step / (t - 1.0); }
			if (!a.Left) x = -x;
			int next = -1; bool newAnim = false, leaving = false;

			// held by the mouse or thrown: the frames go on, the place is the mouse's (or the throw's)
			if (a == pet && (dragging || tossing)) { if (a.Step >= a.TotalSteps) a.Step = 0; else a.Step++; return; }
			bool win = TestWindow && !a.Child;
			var wr = WindowRect;

			if (x < 0 && a.X + x < 0)
			{
				next = Border(an, "vertical");
				if (next >= 0) { a.X = 0; x = 0; newAnim = true; } else leaving = true;
			}
			else if (x > 0 && a.X + x + PetW > Width)
			{
				next = Border(an, "vertical");
				if (next >= 0) { a.X = Width - PetW; x = 0; newAnim = true; } else leaving = true;
			}
			else if (win && !a.OnWindow && x != 0 && a.Y + PetH > wr.Top + 4 && a.Y < wr.Bottom
				&& ((x < 0 && a.X >= wr.Right && a.X + x < wr.Right) || (x > 0 && a.X + PetW <= wr.Left && a.X + PetW + x > wr.Left)))
			{
				// the side of the test window
				next = Border(an, "windowSide");
				if (next >= 0) { a.X = x < 0 ? wr.Right : wr.Left - PetW; x = 0; newAnim = true; }
			}
			if (!newAnim && win && a.OnWindow && x != 0 && (a.X + PetW / 2.0 + x < wr.Left || a.X + PetW / 2.0 + x > wr.Right))
			{
				// the end of the window it walks on: a "window" border, or it steps off and falls
				next = Border(an, "window");
				if (next >= 0) { x = 0; newAnim = true; } else a.OnWindow = false;
			}
			if (!newAnim && !leaving)
			{
				if (y > 0 && win && !a.OnWindow && a.Y + PetH <= wr.Top && a.Y + PetH + y > wr.Top && a.X + PetW / 2.0 > wr.Left && a.X + PetW / 2.0 < wr.Right)
				{
					next = Border(an, "window");
					if (next >= 0) { a.Y = wr.Top - PetH; y = 0; newAnim = true; a.OnWindow = true; }
				}
				else if (y > 0 && a.Y + y > Floor - PetH)
				{
					next = Border(an, "taskbar");
					if (next >= 0) { a.Y = Floor - PetH; y = 0; newAnim = true; a.OnWindow = false; }
				}
				else if (y < 0 && a.Y + y < 0)
				{
					next = Border(an, "horizontal");
					if (next >= 0) { a.Y = 0; y = 0; newAnim = true; }
				}
				if (y < 0 && a.OnWindow && a.Y + PetH + y < wr.Top - 20) a.OnWindow = false;   // jumped off the window
			}

			if (!newAnim && a.Step >= a.TotalSteps)
			{
				if (an.Sequence.Action == "flip") a.Left = !a.Left;
				bool outside = a.X < -PetW || a.X > Width || a.Y < -PetH || a.Y > Height;
				if (outside) next = -1;
				else if (LoopOne && !a.Child) next = an.Id;
				else next = Pick(an.Sequence.Next, a.OnWindow ? "window" : a.Y + PetH + y >= Floor - 2 ? "taskbar" : "none");
				if (next < 0)
				{
					if (a.Child) { a.Gone = true; return; }
					if (LoopOne) { a.X = Width / 2 - PetW / 2; a.Y = Floor - PetH; next = an.Id; }
					else { Respawn(); return; }
				}
				newAnim = true;
			}
			else if (!newAnim && an.Gravity?.Next?.Length > 0 && !a.Child)
			{
				if (a.OnWindow) { }   // standing on the test window: it holds the pet up
				else if (a.Y + y < Floor - PetH)
				{
					if (a.Y + y + 3 >= Floor - PetH) y = Floor - PetH - a.Y;
					else { next = Pick(an.Gravity.Next, "none"); newAnim = next >= 0; }
				}
			}

			a.X += x; a.Y += y;
			if (newAnim && next >= 0) Start(a, next);
			else a.Step++;
		}

		/// <summary>One step of everything (while paused: to look at the animation step by step).</summary>
		public void StepOnce()
		{
			if (pet == null) return;
			Paused = true;
			foreach (var a in actors.ToList()) if (!a.Gone) StepActor(a);
			actors.RemoveAll(a => a.Gone && a != pet);
			Status?.Invoke($"{pet.Anim?.Name}  ·  {L.T("preview.stepOf", Math.Min(pet.Step, pet.TotalSteps), pet.TotalSteps)}");
			Invalidate();
		}

		// ------------------------------------------------------------------ the mouse: hold, drop, throw; the test window

		/// <summary>A window on the stage, to see the pet land on it, walk on it and bump into its sides.</summary>
		public bool TestWindow;
		/// <summary>The step before is drawn see-through under the current one.</summary>
		public bool Onion;
		Rectangle WindowRect => new(winPos.X, winPos.Y, Math.Min(260, Width / 2), 120);
		Point winPos = new(-1, -1);
		bool dragging, tossing, movingWindow;
		Point grab;
		double vx, vy;
		readonly List<(double t, double x, double y)> trail = new();

		public void ToggleWindow(bool on)
		{
			TestWindow = on;
			if (winPos.X < 0) winPos = new Point(Width / 2 - 40, Math.Max(20, Floor - 200));
			if (!on && pet != null) pet.OnWindow = false;
			Invalidate();
		}

		int AnimByName(string name) => Doc?.Root.Animations.Animation.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))?.Id ?? -1;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left || pet == null) return;
			var r = new Rectangle((int)pet.X, (int)pet.Y, PetW, PetH);
			if (r.Contains(e.Location))
			{
				// picked up: the "drag" animation (as DesktopPet does)
				dragging = true; tossing = false; pet.OnWindow = false;
				grab = new Point(e.X - (int)pet.X, e.Y - (int)pet.Y);
				trail.Clear();
				int drag = AnimByName("drag");
				if (drag >= 0) Start(pet, drag);
				Paused = false;
				return;
			}
			if (TestWindow && new Rectangle(WindowRect.X, WindowRect.Y, WindowRect.Width, 22).Contains(e.Location))
			{
				movingWindow = true; grab = new Point(e.X - winPos.X, e.Y - winPos.Y);
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (dragging && pet != null)
			{
				pet.X = e.X - grab.X; pet.Y = e.Y - grab.Y;
				trail.Add((clock.Elapsed.TotalMilliseconds, pet.X, pet.Y));
				if (trail.Count > 8) trail.RemoveAt(0);
				Invalidate();
				return;
			}
			if (movingWindow) { winPos = new Point(e.X - grab.X, Math.Max(0, Math.Min(Floor - 40, e.Y - grab.Y))); if (pet != null) pet.OnWindow = false; Invalidate(); return; }
			bool overPet = pet != null && new Rectangle((int)pet.X, (int)pet.Y, PetW, PetH).Contains(e.Location);
			bool overBar = TestWindow && new Rectangle(WindowRect.X, WindowRect.Y, WindowRect.Width, 22).Contains(e.Location);
			Cursor = overPet ? Cursors.Hand : overBar ? Cursors.SizeAll : Cursors.Default;
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			movingWindow = false;
			if (!dragging || pet == null) return;
			dragging = false;
			// speed of the last moves: a quick release throws the pet
			if (trail.Count >= 2)
			{
				var (t0, x0, y0) = trail[0]; var (t1, x1, y1) = trail[^1];
				double dt = Math.Max(1, t1 - t0) / 16.0;
				vx = (x1 - x0) / dt; vy = (y1 - y0) / dt;
			}
			else vx = vy = 0;
			if (Math.Sqrt(vx * vx + vy * vy) > 5)
			{
				tossing = true;
				int toss = AnimByName("toss");
				if (toss >= 0) Start(pet, toss);
			}
			else
			{
				int fall = AnimByName("fall");
				if (fall >= 0) Start(pet, fall);
			}
		}

		/// <summary>The throw: it flies, bounces on the sides and lands ("fall soft" or "fall hard").</summary>
		void TossStep(double dt)
		{
			double k = dt / 16.0;
			pet.X += vx * k; pet.Y += vy * k;
			vy += 1.5 * k;
			if (pet.X < 0) { pet.X = 0; vx = -vx * 0.5; }
			if (pet.X + PetW > Width) { pet.X = Width - PetW; vx = -vx * 0.5; }
			var wr = WindowRect;
			bool onWin = TestWindow && vy > 0 && pet.Y + PetH >= wr.Top && pet.Y + PetH - vy * k <= wr.Top && pet.X + PetW / 2.0 > wr.Left && pet.X + PetW / 2.0 < wr.Right;
			if (pet.Y + PetH >= Floor || onWin)
			{
				pet.Y = (onWin ? wr.Top : Floor) - PetH;
				pet.OnWindow = onWin;
				tossing = false;
				if ((vx < 0) != pet.Left && Math.Abs(vx) > 0.5) pet.Left = vx < 0;
				int land = AnimByName(vy < 14 ? "fall soft" : "fall hard");
				if (land < 0) land = AnimByName("fall soft");
				if (land >= 0) Start(pet, land); else Respawn();
			}
		}

		int Border(AnimationNode an, string where) => Pick(an.Border?.Next, where);

		/// <summary>The engine's choice: entries whose "only" fits where the pet is, then one by chance.</summary>
		int Pick(NextNode[] list, string where)
		{
			if (list == null || list.Length == 0) return -1;
			bool hor = where is "horizontal" or "window" or "taskbar";
			bool ver = where is "vertical" or "windowSide" or "petSide";
			bool Fits(NextNode n)
			{
				string o = string.IsNullOrWhiteSpace(n.OnlyFlag) ? "none" : n.OnlyFlag;
				return o == "none" || (hor && o == "horizontal+") || (ver && o == "vertical+") || o == where;
			}
			var ok = list.Where(Fits).ToList();
			int max = ok.Sum(n => n.Probability);
			if (ok.Count == 0 || max <= 0) return -1;
			int v = rand.Next(1, max + 1), sum = 0;
			foreach (var n in ok) { sum += n.Probability; if (sum >= v) return n.Value; }
			return -1;
		}

		// ------------------------------------------------------------------ drawing

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics; Theme.Prepare(g);
			var r = ClientRectangle;
			if (r.Width < 2 || r.Height < 2) return;   // squeezed to nothing by a small window: nothing to draw
			switch (EditorSettings.Current.PreviewBack)
			{
				case "checker":
					using (var a = new SolidBrush(Theme.IsDark ? Color.FromArgb(48, 48, 52) : Color.FromArgb(232, 234, 238)))
					using (var b = new SolidBrush(Theme.IsDark ? Color.FromArgb(58, 58, 63) : Color.White))
						for (int y = 0; y < r.Height; y += 12) for (int x = 0; x < r.Width; x += 12) g.FillRectangle((x / 12 + y / 12) % 2 == 0 ? a : b, x, y, 12, 12);
					break;
				case "dark":
					g.Clear(Color.FromArgb(28, 30, 36));
					break;
				default:
					using (var lg = new LinearGradientBrush(r, Color.FromArgb(150, 196, 240), Color.FromArgb(222, 236, 250), 90f)) g.FillRectangle(lg, r);
					break;
			}
			using (var tb = new SolidBrush(Color.FromArgb(32, 36, 46))) g.FillRectangle(tb, 0, Floor, Width, TaskbarH);
			using (var dot = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
				for (int i = 0; i < 5; i++) g.FillRectangle(dot, Width / 2 - 60 + i * 26, Floor + 7, 14, 12);
			if (TestWindow)
			{
				// a plain window: title bar (to move it) and a white body
				var w = WindowRect;
				using (var sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillRectangle(sh, w.X + 4, w.Y + 4, w.Width, w.Height);
				using (var body = new SolidBrush(Color.FromArgb(250, 251, 253))) g.FillRectangle(body, w);
				using (var bar = new SolidBrush(Color.FromArgb(225, 230, 238))) g.FillRectangle(bar, w.X, w.Y, w.Width, 22);
				using (var pen = new Pen(Color.FromArgb(150, 160, 175))) g.DrawRectangle(pen, w);
				TextRenderer.DrawText(g, L.T("preview.window"), Ui.Font(11.5f), new Rectangle(w.X + 8, w.Y + 3, w.Width - 60, 18), Color.FromArgb(70, 76, 90), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
				using (var x = new SolidBrush(Color.FromArgb(200, 70, 60))) g.FillEllipse(x, w.Right - 18, w.Y + 6, 10, 10);
			}
			if (Doc == null) return;
			foreach (var a in actors.Where(a => !a.Gone && a.Child)) DrawActor(g, a);
			// the frame before, see-through, under the current one (to see how far each frame moves)
			if (Onion && pet != null && !pet.Gone && Doc.Frame(pet.PrevFrame) is Bitmap prev)
				Ui.DrawFrame(g, prev, new Rectangle((int)Math.Round(pet.PrevX), (int)Math.Round(pet.PrevY + pet.OffsetY), PetW, PetH), mirror: !pet.Left, opacity: 0.35f);
			if (pet != null && !pet.Gone) DrawActor(g, pet);
			if (Doc.Frames.Count == 0)
				TextRenderer.DrawText(g, L.T("preview.noSheet"), Ui.Font(13), r, Color.FromArgb(60, 60, 70), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
			if (Paused) TextRenderer.DrawText(g, "❚❚ " + L.T("preview.paused"), Ui.Font(12, true), new Rectangle(8, 6, 200, 20), Color.FromArgb(60, 60, 70), TextFormatFlags.Left);
		}

		void DrawActor(Graphics g, Actor a)
		{
			var img = Doc.Frame(a.Frame);
			if (img == null) return;
			var dest = new Rectangle((int)Math.Round(a.X), (int)Math.Round(a.Y + a.OffsetY), PetW, PetH);
			Ui.DrawFrame(g, img, dest, mirror: !a.Left, opacity: (float)Math.Max(0, Math.Min(1, a.Opacity)));
		}
	}

	/// <summary>Plays a sound of the pet (MP3 in base64) with the player of DesktopPet.</summary>
	internal static class SoundPlayer
	{
		public static void Play(SoundNode s, double volume = 0.8)
		{
			if (string.IsNullOrWhiteSpace(s?.Base64)) return;
			try { DesktopPet.Mp3Player.Load(Convert.FromBase64String(s.Base64.Trim()))?.Play(volume, Math.Max(0, s.Loop)); }
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("sound: " + e.Message); }
		}
	}
}
