using System.Drawing;
using System.Drawing.Imaging;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// A new pet that already works: a little round placeholder that walks on the taskbar, turns at the sides, blinks,
	/// falls from the sky and can be picked up. Replace its sprite sheet and change the animations from there.
	/// </summary>
	internal static class Template
	{
		public static PetDoc New()
		{
			var sheet = Sheet();
			string png;
			using (var ms = new MemoryStream()) { sheet.Save(ms, ImageFormat.Png); png = Convert.ToBase64String(ms.ToArray()); }
			var r = new RootNode
			{
				Header = new HeaderNode
				{
					Author = Environment.UserName, Title = L.T("tpl.title"), Petname = L.T("tpl.name"), Version = "1.0",
					Info = L.T("tpl.info"), Application = "1", Icon = Icons.MakeIco(Crop(sheet, 0)),
				},
				Image = new ImageNode { TilesX = 5, TilesY = 1, Png = png, Transparency = "Magenta" },
				Spawns = new SpawnsNode
				{
					Spawn = new[]
					{
						new SpawnNode { Id = 1, Probability = 60, X = "random*(screenW-imageW-50)/100+25", Y = "areaH-imageH", Next = 1 },
						new SpawnNode { Id = 2, Probability = 40, X = "random*(screenW-imageW-50)/100+25", Y = "-imageH", Next = 5 },
					}
				},
				Animations = new AnimationsNode
				{
					Animation = new[]
					{
						A(1, "walk", "-2", "0", 150, "random/4+4", new[] { 1, 2 }, N((60, 1), (15, 2), (25, 3)), border: N((100, 2)), gravity: N((100, 5))),
						A(2, "turn", "0", "0", 150, "0", new[] { 0, 3, 0 }, N((100, 1)), action: "flip"),
						A(3, "blink", "0", "0", 120, "0", new[] { 0, 3, 0, 0 }, N((100, 1))),
						A(4, "drag", "0", "0", 120, "100", new[] { 4 }, N((100, 4))),
						A(5, "fall", "0", "3", 60, "60", new[] { 4 }, N((100, 5)), border: N((100, 6)), endY: "12"),
						A(6, "fall soft", "0", "0", 120, "0", new[] { 0, 3, 0 }, N((100, 1))),
						A(7, "fall hard", "0", "0", 160, "0", new[] { 3, 3, 0, 3, 0 }, N((100, 1))),
					}
				},
				Childs = new ChildsNode { Child = Array.Empty<ChildNode>() },
				Sounds = new SoundsNode { Sound = Array.Empty<SoundNode>() },
			};
			sheet.Dispose();
			return new PetDoc(r, null);
		}

		static NextNode[] N(params (int prob, int id)[] list) => list.Select(n => new NextNode { Probability = n.prob, OnlyFlag = "none", Value = n.id }).ToArray();

		static AnimationNode A(int id, string name, string x, string y, int interval, string repeat, int[] frames, NextNode[] next,
			NextNode[] border = null, NextNode[] gravity = null, string action = null, string endY = null) => new AnimationNode
		{
			Id = id, Name = name,
			Start = new MovingNode { X = x, Y = y, Interval = interval.ToString(), Opacity = 1 },
			End = new MovingNode { X = x, Y = endY ?? y, Interval = interval.ToString(), Opacity = 1 },
			Sequence = new SequenceNode { RepeatCount = repeat, Frame = frames, Next = next, Action = action },
			Border = border == null ? null : new HitNode { Next = border },
			Gravity = gravity == null ? null : new HitNode { Next = gravity },
		};

		// ------------------------------------------------------------------ the placeholder drawing (one char = one pixel)

		static readonly string[] Body =
		{
			".....oooooo.....",
			"...ooBBBBBBoo...",
			"..oBBWBBBBBBBo..",
			".oBBWBBBBBBBBBo.",
			".oBBBBBBBBBBBBo.",
			"oBBBBeBBBBeBBBBo",
			"oBBBBeBBBBeBBBBo",
			"oBBBBBBBBBBBBBBo",
			"oBBBcBBBBBBcBBBo",
			"oBBBBBBmmBBBBBBo",
			".oBBBBBBBBBBBBo.",
			".oDBBBBBBBBBBDo.",
			"..oDDBBBBBBDDo..",
			"...ooDDDDDDoo...",
			".....oooooo.....",
		};

		static Bitmap Sheet()
		{
			const int w = 20, h = 20, z = 3;
			var colors = new Dictionary<char, Color>
			{
				['o'] = Color.FromArgb(40, 52, 90), ['B'] = Color.FromArgb(110, 168, 255), ['D'] = Color.FromArgb(78, 128, 220),
				['W'] = Color.FromArgb(220, 236, 255), ['e'] = Color.FromArgb(30, 34, 50), ['c'] = Color.FromArgb(255, 150, 170),
				['m'] = Color.FromArgb(40, 52, 90), ['f'] = Color.FromArgb(40, 52, 90), ['F'] = Color.FromArgb(78, 128, 220),
			};
			var small = new Bitmap(w * 5, h, PixelFormat.Format32bppArgb);
			for (int f = 0; f < 5; f++)
			{
				var rows = Body.Select(r => r.ToCharArray()).ToArray();
				if (f == 3) foreach (var r in rows) for (int i = 0; i < r.Length; i++) if (r[i] == 'e') r[i] = rows.ToList().IndexOf(r) == 5 ? 'B' : 'm';
				int lift = f == 1 ? 1 : 0;
				for (int y = 0; y < rows.Length; y++)
					for (int x = 0; x < rows[y].Length; x++)
						if (colors.TryGetValue(rows[y][x], out var c)) small.SetPixel(f * w + 2 + x, y + 2 - lift, c);
				// feet (walking: one up, one down) and arms up when falling
				int fy = 17;
				(int x, int y)[] feet = f switch { 1 => new[] { (5, fy), (12, fy - 1) }, 2 => new[] { (5, fy - 1), (12, fy) }, _ => new[] { (5, fy), (12, fy) } };
				foreach (var p in feet) for (int dx = 0; dx < 3; dx++) small.SetPixel(f * w + 2 + p.x + dx, p.y, colors['o']);
				if (f == 4) foreach (int ax in new[] { 0, 17 }) for (int dy = 0; dy < 4; dy++) small.SetPixel(f * w + 1 + ax, 4 + dy, colors['o']);
			}
			var big = new Bitmap(small.Width * z, small.Height * z, PixelFormat.Format32bppArgb);
			using (var g = Graphics.FromImage(big))
			{
				g.Clear(Color.Magenta);
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
				g.DrawImage(small, new Rectangle(0, 0, big.Width, big.Height));
			}
			small.Dispose();
			return big;
		}

		static Bitmap Crop(Bitmap sheet, int frame)
		{
			int fw = sheet.Width / 5;
			var b = sheet.Clone(new Rectangle(frame * fw, 0, fw, sheet.Height), PixelFormat.Format32bppArgb);
			b.MakeTransparent(Color.Magenta);
			return b;
		}
	}
}
