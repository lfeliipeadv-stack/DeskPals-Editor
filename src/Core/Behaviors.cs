using System.Globalization;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// Ready-made behaviours: an animation already set up (movement, borders, gravity, what comes next) that only needs
	/// its frames. A new one is wired to the animations of the pet by their names: "walk" turns at the sides with
	/// "turn", falls with "fall"; a pose goes back to "walk", and so on.
	/// </summary>
	internal static class Behaviors
	{
		public sealed record Kind(string Key, string Name, int MinFrames, bool Lottery);

		/// <summary>Key = name used in the pet (DesktopPet looks for some of them: drag, fall, kill…).</summary>
		public static readonly Kind[] All =
		{
			new("walk", "walk", 2, false),
			new("turn", "turn", 1, false),
			new("idle", "idle", 1, true),
			new("blink", "blink", 2, true),
			new("run", "run", 2, true),
			new("jump", "jump", 1, true),
			new("climb", "climb", 2, false),
			new("sit", "sit", 1, true),
			new("sleep", "sleep", 1, true),
			new("happy", "happy", 2, true),
			new("dance", "dance", 2, true),
			new("talk", "talk", 2, false),
			new("fall", "fall", 1, false),
			new("land", "fall soft", 1, false),
			new("drag", "drag", 1, false),
			new("kill", "kill", 1, false),
		};

		static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		static int NextId(RootNode r) => r.Animations.Animation.Length == 0 ? 1 : r.Animations.Animation.Max(a => a.Id) + 1;
		static AnimationNode ByName(RootNode r, string name) => r.Animations.Animation.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
		static NextNode N(int id, int prob, string only = "none") => new() { Value = id, Probability = prob, OnlyFlag = only };

		static AnimationNode A(RootNode r, string name, int[] frames, string x, string y0, string y1, int interval, string repeat, double op0 = 1, double op1 = 1, string x1 = null)
		{
			var a = new AnimationNode
			{
				Id = NextId(r), Name = name,
				Start = new MovingNode { X = x, Y = y0, Interval = interval.ToString(Inv), Opacity = op0 },
				End = new MovingNode { X = x1 ?? x, Y = y1, Interval = interval.ToString(Inv), Opacity = op1 },
				Sequence = new SequenceNode { RepeatCount = repeat, Frame = frames, Next = Array.Empty<NextNode>() },
			};
			r.Animations.Animation = r.Animations.Animation.Append(a).ToArray();
			return a;
		}

		static void AddNext(AnimationNode a, NextNode n) { if (a != null && !(a.Sequence.Next ?? Array.Empty<NextNode>()).Any(x => x.Value == n.Value)) a.Sequence.Next = (a.Sequence.Next ?? Array.Empty<NextNode>()).Append(n).ToArray(); }
		static void AddBorder(AnimationNode a, NextNode n) { if (a == null) return; a.Border ??= new HitNode { Next = Array.Empty<NextNode>() }; if (!a.Border.Next.Any(x => x.Value == n.Value && x.OnlyFlag == n.OnlyFlag)) a.Border.Next = a.Border.Next.Append(n).ToArray(); }
		static void AddGravity(AnimationNode a, NextNode n) { if (a == null) return; a.Gravity ??= new HitNode { Next = Array.Empty<NextNode>() }; if (!a.Gravity.Next.Any(x => x.Value == n.Value)) a.Gravity.Next = a.Gravity.Next.Append(n).ToArray(); }

		/// <summary>
		/// Add a behaviour to the pet with these frames. <paramref name="speed"/>: 1 normal, below slower, above faster.
		/// <paramref name="lottery"/>: “walk” may now and then go into it (with this chance), when it exists.
		/// Returns the new animation.
		/// </summary>
		public static AnimationNode Create(RootNode r, string key, int[] f, string name = null, double speed = 1, int lottery = 0)
		{
			int Ms(int ms) => Math.Max(20, (int)Math.Round(ms / Math.Max(0.2, speed)));
			string Px(double px) => Math.Round(px * speed, 2).ToString(Inv);
			var walk = ByName(r, "walk");
			var fall = ByName(r, "fall");
			var turn = ByName(r, "turn");
			AnimationNode a;
			switch (key)
			{
				case "walk":
					a = A(r, name ?? "walk", f, "-" + Px(2), "0", "0", Ms(150), "random/4+6");
					AddNext(a, N(a.Id, 60));
					if (turn != null) { AddBorder(a, N(turn.Id, 100)); AddNext(a, N(turn.Id, 10)); AddNext(turn, N(a.Id, 100)); }
					if (fall != null) AddGravity(a, N(fall.Id, 100));
					break;
				case "turn":
					a = A(r, name ?? "turn", f, "0", "0", "0", Ms(140), "0");
					a.Sequence.Action = "flip";
					if (walk != null) { AddNext(a, N(walk.Id, 100)); AddBorder(walk, N(a.Id, 100)); AddNext(walk, N(a.Id, 10)); }
					break;
				case "run":
					a = A(r, name ?? "run", f, "-" + Px(6), "0", "0", Ms(70), "random/4+8");
					if (turn != null) AddBorder(a, N(turn.Id, 100));
					if (fall != null) AddGravity(a, N(fall.Id, 100));
					break;
				case "jump":
					a = A(r, name ?? "jump", f, "-" + Px(3), "-" + Px(9), Px(9), Ms(50), Math.Max(0, 18 / Math.Max(1, f.Length) - 1).ToString(Inv));
					AddBorder(a, N(walk?.Id ?? a.Id, 100, "taskbar")); AddBorder(a, N(walk?.Id ?? a.Id, 100, "window"));
					if (turn != null) AddBorder(a, N(turn.Id, 100, "vertical"));
					if (walk != null) AddNext(a, N(walk.Id, 100));
					break;
				case "climb":
					// up the side of the screen; at the top it lets go and falls
					a = A(r, name ?? "climb", f, "0", "-" + Px(2), "-" + Px(2), Ms(120), "random/4+12");
					AddNext(a, N(a.Id, 100));
					if (fall != null) AddBorder(a, N(fall.Id, 100, "horizontal"));
					if (walk != null) AddBorder(walk, N(a.Id, 40, "vertical"));
					break;
				case "fall":
					a = A(r, name ?? "fall", f, "0", Px(2), Px(12), Ms(60), "20");
					AddNext(a, N(a.Id, 100));
					var land = ByName(r, "fall soft");
					AddBorder(a, N(land?.Id ?? walk?.Id ?? a.Id, 100, "taskbar"));
					AddBorder(a, N(land?.Id ?? walk?.Id ?? a.Id, 100, "window"));
					if (walk != null) AddGravity(walk, N(a.Id, 100));
					break;
				case "land":
					a = A(r, name ?? "fall soft", f, "0", "0", "0", Ms(120), "0");
					if (walk != null) AddNext(a, N(walk.Id, 100));
					if (fall != null) { foreach (var n in fall.Border?.Next ?? Array.Empty<NextNode>()) if (n.Value == walk?.Id || n.Value == fall.Id) n.Value = a.Id; }
					break;
				case "drag":
					a = A(r, name ?? "drag", f, "0", "0", "0", Ms(100), "200");
					AddNext(a, N(a.Id, 100));
					break;
				case "kill":
					a = A(r, name ?? "kill", f, "0", "0", "0", Ms(100), "10", 1, 0);
					break;
				case "sleep":
					a = A(r, name ?? "sleep", f, "0", "0", "0", Ms(400), "random/6+12", 1, 1);
					a.Sequence.RepeatFromFrame = Math.Max(0, f.Length - 1);
					if (walk != null) AddNext(a, N(walk.Id, 100));
					break;
				case "sit":
					a = A(r, name ?? "sit", f, "0", "0", "0", Ms(300), "random/6+6");
					a.Sequence.RepeatFromFrame = Math.Max(0, f.Length - 1);
					if (walk != null) AddNext(a, N(walk.Id, 100));
					break;
				default:   // idle, blink, happy, dance, talk: a pose in place that goes back to walking
					string rep = key switch { "idle" => "random/8+4", "dance" => "4", "happy" => "2", "talk" => "random/3+2", _ => "0" };
					int ms = key switch { "blink" => 110, "dance" => 160, "talk" => 120, _ => 200 };
					a = A(r, name ?? key, f, "0", "0", "0", Ms(ms), rep);
					if (walk != null) AddNext(a, N(walk.Id, 100));
					break;
			}
			if (lottery > 0 && walk != null && walk != a) AddNext(walk, N(a.Id, lottery));
			return a;
		}
	}
}
