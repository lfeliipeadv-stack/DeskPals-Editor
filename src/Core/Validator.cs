using System.Drawing;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// What would go wrong with the pet in DesktopPet: errors (the app stops or the pet cannot play) and warnings
	/// (it plays, but probably not as meant). Every problem says where it is, so the list can go there.
	/// </summary>
	internal static class Validator
	{
		public sealed record Problem(bool Error, string Text, string Page, int Anim = -1);

		static readonly string[] Keys = { "drag", "fall", "kill", "sync", "toss", "fall soft", "fall hard" };

		/// <summary>Names DesktopPet looks for (its own animations, reactions, the still pose while talking).</summary>
		static readonly HashSet<string> Looked = new(Keys.Concat(new[] { "talk", "talking", "speak", "idle", "sit", "look around", "think", "blink", "wait", "busy", "stand", "walk", "run", "wave", "hello", "happy", "laugh", "scared", "surprised", "sleep", "eat", "love", "in love", "dance", "jump", "spin", "play", "cheer", "drink", "hearts", "purr", "nap", "rest", "yawn" }));

		public static (int errors, int warnings) Count(PetDoc d)
		{
			var p = Run(d);
			return (p.Count(x => x.Error), p.Count(x => !x.Error));
		}

		public static List<Problem> Run(PetDoc d)
		{
			var list = new List<Problem>();
			var r = d.Root;
			var anims = r.Animations.Animation;
			var ids = anims.Select(a => a.Id).ToHashSet();
			var ctx = new ExprContext { ImageW = d.FrameW, ImageH = d.FrameH, ImageX = 100, ImageY = 100 };
			void E(string t, string page, int anim = -1) => list.Add(new Problem(true, t, page, anim));
			void W(string t, string page, int anim = -1) => list.Add(new Problem(false, t, page, anim));
			void Formula(string text, string what, string page, int anim = -1)
			{
				if (Expr.Eval(text, ctx, out string err) == null) E(L.T("chk.formula", what, text, err), page, anim);
			}

			if (string.IsNullOrWhiteSpace(r.Image.Png)) E(L.T("chk.noImage"), "sheet");
			else if (d.Sheet != null && (d.Sheet.Width % Math.Max(1, r.Image.TilesX) != 0 || d.Sheet.Height % Math.Max(1, r.Image.TilesY) != 0))
				W(L.T("chk.grid", d.Sheet.Width, d.Sheet.Height, r.Image.TilesX, r.Image.TilesY), "sheet");
			if (string.IsNullOrWhiteSpace(r.Header.Petname)) W(L.T("chk.noName"), "header");
			if (r.Spawns.Spawn.Length == 0) E(L.T("chk.noSpawn"), "spawns");
			if (anims.Length == 0) E(L.T("chk.noAnims"), "animations");

			foreach (var g in anims.GroupBy(a => a.Id).Where(g => g.Count() > 1)) E(L.T("chk.dupId", g.Key), "animations", g.Key);
			foreach (var g in r.Spawns.Spawn.GroupBy(s => s.Id).Where(g => g.Count() > 1)) E(L.T("chk.dupSpawn", g.Key), "spawns");

			// who leads to whom (to find the animations nothing reaches)
			var reached = new HashSet<int>();
			foreach (var s in r.Spawns.Spawn)
			{
				reached.Add(s.Next);
				if (!ids.Contains(s.Next)) E(L.T("chk.spawnNext", s.Id, s.Next), "spawns");
				Formula(s.X, L.T("chk.spawnX", s.Id), "spawns");
				Formula(s.Y, L.T("chk.spawnY", s.Id), "spawns");
			}
			var childStarts = new HashSet<int>();
			foreach (var c in r.Childs.Child)
			{
				reached.Add(c.Next); childStarts.Add(c.Next);
				if (!ids.Contains(c.Id)) E(L.T("chk.childParent", c.Id), "childs");
				if (!ids.Contains(c.Next)) E(L.T("chk.childNext", c.Id, c.Next), "childs");
				Formula(c.X, L.T("chk.childX", c.Id), "childs");
				Formula(c.Y, L.T("chk.childY", c.Id), "childs");
			}
			foreach (var s in r.Sounds.Sound)
				if (!ids.Contains(s.Id)) E(L.T("chk.soundAnim", s.Id), "sounds");
			// every animation played by a child window (it ends there on purpose, and it may leave the screen)
			var byIdAll = anims.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
			var childAnims = new HashSet<int>();
			var walk = new Stack<int>(childStarts);
			while (walk.Count > 0)
			{
				int id = walk.Pop();
				if (!childAnims.Add(id) || !byIdAll.TryGetValue(id, out var ca)) continue;
				foreach (var n in (ca.Sequence.Next ?? Array.Empty<NextNode>()).Concat(ca.Border?.Next ?? Array.Empty<NextNode>()).Concat(ca.Gravity?.Next ?? Array.Empty<NextNode>()))
					walk.Push(n.Value);
			}

			int frames = d.Frames.Count;
			foreach (var a in anims)
			{
				string name = $"“{a.Name}” (#{a.Id})";
				var f = a.Sequence.Frame ?? Array.Empty<int>();
				if (f.Length == 0) E(L.T("chk.noFrames", name), "animations", a.Id);
				else if (frames > 0 && f.Any(x => x < 0 || x >= frames)) E(L.T("chk.badFrame", name, f.First(x => x < 0 || x >= frames), frames - 1), "animations", a.Id);
				if (a.Sequence.RepeatFromFrame >= Math.Max(1, f.Length)) E(L.T("chk.repeatFrom", name), "animations", a.Id);
				var all = (a.Sequence.Next ?? Array.Empty<NextNode>()).Concat(a.Border?.Next ?? Array.Empty<NextNode>()).Concat(a.Gravity?.Next ?? Array.Empty<NextNode>());
				foreach (var n in all)
				{
					reached.Add(n.Value);
					if (!ids.Contains(n.Value)) E(L.T("chk.nextMissing", name, n.Value), "animations", a.Id);
				}
				Formula(a.Start.X, L.T("chk.what", name, "x"), "animations", a.Id);
				Formula(a.Start.Y, L.T("chk.what", name, "y"), "animations", a.Id);
				Formula(a.End.X, L.T("chk.what", name, "x"), "animations", a.Id);
				Formula(a.End.Y, L.T("chk.what", name, "y"), "animations", a.Id);
				Formula(a.Start.Interval, L.T("chk.what", name, L.T("move.interval")), "animations", a.Id);
				Formula(a.Sequence.RepeatCount, L.T("chk.what", name, L.T("anim.repeat")), "animations", a.Id);
				if (Expr.Value(a.Start.Interval, ctx, 1) <= 0) W(L.T("chk.interval", name), "animations", a.Id);
				bool isKey = Keys.Contains((a.Name ?? "").ToLowerInvariant());
				bool child = childAnims.Contains(a.Id);
				if ((a.Sequence.Next?.Length ?? 0) == 0 && !isKey && !child && (a.Name ?? "").ToLowerInvariant() != "kill")
					W(L.T("chk.noNext", name), "animations", a.Id);
				bool walks = Expr.Value(a.Start.X, ctx) != 0 || Expr.Value(a.End.X, ctx) != 0;
				if (walks && !child && !isKey && (a.Border?.Next?.Length ?? 0) == 0)
					W(L.T("chk.noBorder", name), "animations", a.Id);
			}
			foreach (var a in anims)
				if (!reached.Contains(a.Id) && !Keys.Contains((a.Name ?? "").ToLowerInvariant()))
					W(L.T("chk.unreached", $"“{a.Name}” (#{a.Id})"), "animations", a.Id);
			foreach (var k in new[] { "drag", "fall" })
				if (!anims.Any(a => string.Equals(a.Name, k, StringComparison.OrdinalIgnoreCase))) W(L.T("chk.noKey", k), "animations");

			// lists whose chances are all zero: nothing is ever chosen from them
			foreach (var a in anims)
			{
				string name = $"“{a.Name}” (#{a.Id})";
				foreach (var (lst, key) in new[] { (a.Sequence.Next, "next.end"), (a.Border?.Next, "next.border"), (a.Gravity?.Next, "next.gravity") })
					if (lst?.Length > 0 && lst.All(n => n.Probability <= 0)) W(L.T("chk.zeroProb", name, L.T(key)), "animations", a.Id);
			}
			if (r.Spawns.Spawn.Length > 0 && r.Spawns.Spawn.All(s => s.Probability <= 0)) E(L.T("chk.spawnZero"), "spawns");
			// two animations with the same name (the app finds reactions by name: only the first one is used)
			foreach (var g in anims.Where(a => !string.IsNullOrWhiteSpace(a.Name) && Looked.Contains(a.Name.Trim().ToLowerInvariant())).GroupBy(a => a.Name.Trim().ToLowerInvariant()).Where(g => g.Count() > 1))
				W(L.T("chk.dupName", g.First().Name, string.Join(", ", g.Select(a => "#" + a.Id))), "animations", g.First().Id);
			// a child window whose animations never end stays on the screen until the pet goes away
			var byId = anims.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
			foreach (var c in r.Childs.Child.Where(c => byId.ContainsKey(c.Next)))
			{
				var seen = new HashSet<int>(); var todo = new Stack<int>(); todo.Push(c.Next); bool ends = false;
				while (todo.Count > 0 && !ends)
				{
					int id = todo.Pop();
					if (!seen.Add(id) || !byId.TryGetValue(id, out var a)) continue;
					var nexts = (a.Sequence.Next ?? Array.Empty<NextNode>()).Where(n => n.Probability > 0).ToList();
					if (nexts.Count == 0) ends = true;
					// one that moves leaves the screen sooner or later, and DesktopPet closes it there
					if (Expr.Value(a.Start.X, ctx) != 0 || Expr.Value(a.End.X, ctx) != 0 || Expr.Value(a.Start.Y, ctx) != 0 || Expr.Value(a.End.Y, ctx) != 0) ends = true;
					foreach (var n in nexts) todo.Push(n.Value);
				}
				if (!ends) W(L.T("chk.childForever", byId[c.Next].Name, c.Next), "childs");
			}
			// heavy sounds make the whole pet heavy to download
			foreach (var s in r.Sounds.Sound.Where(s => (s.Base64?.Length ?? 0) * 3 / 4 > 300 * 1024))
				W(L.T("chk.bigSound", d.Anim(s.Id)?.Name ?? "#" + s.Id, (s.Base64.Length * 3 / 4) / 1024), "sounds");
			// pictures that need too much memory (very big frames)
			if (d.FrameW * d.FrameH > 400 * 400) W(L.T("chk.bigFrame", d.FrameW, d.FrameH), "sheet");
			return list;
		}
	}
}
