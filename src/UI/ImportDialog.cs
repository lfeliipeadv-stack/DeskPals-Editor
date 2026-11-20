using System.Drawing;
using System.Xml.Serialization;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// Copy animations from another pet: the chosen ones (and, if wanted, all that come after them, their child windows
	/// and sounds) come with their frames, which are added to this pet's sheet; every number is changed to a free one.
	/// </summary>
	internal static class ImportDialog
	{
		/// <summary>The id of the first copied animation, or -1.</summary>
		/// <summary><paramref name="file"/>: the other pet already chosen (dropped on the window); otherwise it asks.</summary>
		public static int Show(MainForm main, PetDoc doc, string file = null)
		{
			if (file == null)
			{
				using var open = new OpenFileDialog { Filter = L.T("open.filter"), Title = L.T("imp.choose") };
				string store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "pets");
				if (Directory.Exists(store)) open.InitialDirectory = store;
				if (open.ShowDialog(main) != DialogResult.OK) return -1;
				file = open.FileName;
			}
			PetDoc other;
			try { other = PetDoc.Open(file); }
			catch (Exception ex) { main.Message(L.T("msg.openFail", ex.Message)); return -1; }

			int result = -1;
			using var f = new Form { Text = L.T("imp.title", other.Name), Size = new Size(620, 640), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Surface, Font = Ui.Font(13) };
			Theme.ApplyWindowChrome(f);
			Ui.AutoFit(f);
			var top = Ui.Column(Ui.Title(L.T("imp.title", other.Name), 16), Ui.Hint(L.T("imp.intro"), 560));
			var follow = new Toggle(L.T("imp.follow"), true);
			var search = Ui.Box("", 300); search.PlaceholderText = L.T("anim.search");
			top.Controls.Add(follow); top.Controls.Add(search);
			top.Dock = DockStyle.Top; top.Padding = new Padding(14, 12, 14, 0);
			var list = new CheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, CheckOnClick = true, IntegralHeight = false };
			var anims = other.Root.Animations.Animation;
			void Fill()
			{
				var keep = list.CheckedItems.Cast<AnimItem>().Select(a => a.Id).ToHashSet();
				list.Items.Clear();
				foreach (var a in anims.Where(a => search.Text.Length == 0 || (a.Name ?? "").Contains(search.Text, StringComparison.OrdinalIgnoreCase)))
					list.Items.Add(new AnimItem(a.Id, a.Name), keep.Contains(a.Id));
			}
			search.TextChanged += (s, e) => Fill();
			Fill();
			var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Surface, Padding = new Padding(10, 8, 10, 0) };
			var ok = new FlatButton(L.T("imp.ok"), primary: true);
			var cancel = new FlatButton(L.T("build.cancel")); cancel.Click += (s, e) => f.Close();
			bar.Controls.Add(ok); bar.Controls.Add(cancel);
			var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 6, 14, 6), BackColor = Theme.Surface };
			wrap.Controls.Add(list);
			f.Controls.Add(wrap); f.Controls.Add(bar); f.Controls.Add(top);
			ok.Click += (s, e) =>
			{
				var chosen = list.CheckedItems.Cast<AnimItem>().Select(a => a.Id).ToList();
				if (chosen.Count == 0) return;
				result = Copy(doc, other, chosen, follow.On);
				f.Close();
			};
			f.ShowDialog(main);
			return result;
		}

		static readonly XmlSerializer animSer = new(typeof(AnimationNode));

		/// <summary>Names of the everyday animations of a pet (never copied along with a scene).</summary>
		static readonly HashSet<string> Shared = new() { "walk", "idle", "turn", "fall", "fall fast", "fall soft", "fall hard", "drag", "toss", "kill", "sync", "run", "sit", "sleep", "blink", "climb", "jump", "walk_task", "stand", "look around" };

		static AnimationNode Clone(AnimationNode a)
		{
			using var ms = new MemoryStream();
			animSer.Serialize(ms, a);
			ms.Position = 0;
			return (AnimationNode)animSer.Deserialize(ms);
		}

		/// <summary>Copy the animations (and what follows them) into <paramref name="doc"/>. Returns the new id of the first one.</summary>
		public static int Copy(PetDoc doc, PetDoc other, List<int> chosen, bool follow)
		{
			var src = other.Root.Animations.Animation.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
			// the common animations (walking, turning, falling…) are not copied: they go to the ones of this pet with the same name
			var mine = doc.Root.Animations.Animation.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
			bool Common(int id) => src.TryGetValue(id, out var x) && (Shared.Contains((x.Name ?? "").Trim().ToLowerInvariant()) || mine.ContainsKey((x.Name ?? "").Trim().ToLowerInvariant()));
			var set = new HashSet<int>();
			var todo = new Stack<int>(chosen);
			while (todo.Count > 0)
			{
				int id = todo.Pop();
				if (!src.TryGetValue(id, out var a) || !set.Add(id) || !follow) continue;
				foreach (var n in (a.Sequence.Next ?? Array.Empty<NextNode>()).Concat(a.Border?.Next ?? Array.Empty<NextNode>()).Concat(a.Gravity?.Next ?? Array.Empty<NextNode>()))
					if (!Common(n.Value)) todo.Push(n.Value);
				foreach (var c in other.Root.Childs.Child.Where(c => c.Id == id)) todo.Push(c.Next);
			}
			// frames: this pet's ones keep their numbers, the copied ones go after them
			var used = set.SelectMany(id => src[id].Sequence.Frame ?? Array.Empty<int>()).Where(i => i >= 0 && i < other.Frames.Count).Distinct().OrderBy(i => i).ToList();
			var frameMap = new Dictionary<int, int>();
			var frames = doc.CopyFrames();
			foreach (int fr in used) { frameMap[fr] = frames.Count; frames.Add(new Bitmap(other.Frame(fr))); }
			doc.SetFrames(frames, doc.Frames.Count > 0 ? Math.Max(1, doc.Root.Image.TilesX) : 8);
			foreach (var b in frames) b.Dispose();
			// animations: free numbers; links inside the copy follow it, links outside go to this pet's "walk" (or are left out)
			int next = doc.NextAnimId();
			var idMap = set.OrderBy(i => i).ToDictionary(i => i, i => next++);
			int walk = doc.Root.Animations.Animation.FirstOrDefault(a => string.Equals(a.Name, "walk", StringComparison.OrdinalIgnoreCase))?.Id ?? -1;
			NextNode[] Map(NextNode[] list) => (list ?? Array.Empty<NextNode>())
				.Select(n => idMap.TryGetValue(n.Value, out int v) ? new NextNode { Value = v, Probability = n.Probability, OnlyFlag = n.OnlyFlag }
					: src.TryGetValue(n.Value, out var t) && mine.TryGetValue((t.Name ?? "").Trim().ToLowerInvariant(), out int same) ? new NextNode { Value = same, Probability = n.Probability, OnlyFlag = n.OnlyFlag }
					: walk >= 0 ? new NextNode { Value = walk, Probability = n.Probability, OnlyFlag = n.OnlyFlag } : null)
				.Where(n => n != null).ToArray();
			var added = new List<AnimationNode>();
			foreach (var id in idMap.Keys)
			{
				var a = Clone(src[id]);
				a.Id = idMap[id];
				a.Sequence.Frame = (a.Sequence.Frame ?? Array.Empty<int>()).Where(frameMap.ContainsKey).Select(x => frameMap[x]).ToArray();
				a.Sequence.Next = Map(a.Sequence.Next);
				if (a.Border != null) { a.Border.Next = Map(a.Border.Next); if (a.Border.Next.Length == 0) a.Border = null; }
				if (a.Gravity != null) { a.Gravity.Next = Map(a.Gravity.Next); if (a.Gravity.Next.Length == 0) a.Gravity = null; }
				added.Add(a);
			}
			doc.Root.Animations.Animation = doc.Root.Animations.Animation.Concat(added).ToArray();
			var childs = other.Root.Childs.Child.Where(c => idMap.ContainsKey(c.Id) && idMap.ContainsKey(c.Next))
				.Select(c => new ChildNode { Id = idMap[c.Id], Next = idMap[c.Next], X = c.X, Y = c.Y });
			doc.Root.Childs.Child = doc.Root.Childs.Child.Concat(childs).ToArray();
			var sounds = other.Root.Sounds.Sound.Where(s => idMap.ContainsKey(s.Id))
				.Select(s => new SoundNode { Id = idMap[s.Id], Probability = s.Probability, Loop = s.Loop, Base64 = s.Base64 });
			doc.Root.Sounds.Sound = doc.Root.Sounds.Sound.Concat(sounds).ToArray();
			doc.Changed("image");
			return idMap.TryGetValue(chosen[0], out int first) ? first : -1;
		}
	}
}
