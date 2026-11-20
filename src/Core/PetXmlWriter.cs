using System.Globalization;
using System.Security;
using System.Text;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// Writes animations.xml the way the pets of the gallery are written (two-space indent, sprite sheet and icon in
	/// CDATA), so a pet saved by the editor reads like the others and only the changed lines differ.
	/// </summary>
	internal static class PetXmlWriter
	{
		static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
		static string E(string s) => SecurityElement.Escape(s ?? "");

		public static string Write(RootNode r)
		{
			var sb = new StringBuilder();
			sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
			sb.AppendLine("<animations xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"https://esheep.petrucci.ch/ https://adrianotiger.github.io/desktopPet/Resources/animations.xsd\" xmlns=\"https://esheep.petrucci.ch/\">");
			var h = r.Header;
			sb.AppendLine("  <header>");
			sb.AppendLine($"    <author>{E(h.Author)}</author>");
			sb.AppendLine($"    <title>{E(h.Title)}</title>");
			sb.AppendLine($"    <petname>{E(h.Petname)}</petname>");
			sb.AppendLine($"    <version>{E(h.Version)}</version>");
			sb.AppendLine($"    <info>{E(h.Info)}</info>");
			sb.AppendLine($"    <application>{E(string.IsNullOrEmpty(h.Application) ? "1" : h.Application)}</application>");
			sb.AppendLine($"    <icon><![CDATA[{h.Icon ?? ""}]]></icon>");
			sb.AppendLine("  </header>");
			var i = r.Image;
			sb.AppendLine("  <image>");
			sb.AppendLine($"    <tilesx>{i.TilesX}</tilesx>");
			sb.AppendLine($"    <tilesy>{i.TilesY}</tilesy>");
			if (i.Skins > 0) sb.AppendLine($"    <skins>{i.Skins}</skins>");
			sb.AppendLine($"    <png><![CDATA[{i.Png ?? ""}]]></png>");
			sb.AppendLine($"    <transparency>{E(string.IsNullOrWhiteSpace(i.Transparency) ? "Magenta" : i.Transparency)}</transparency>");
			sb.AppendLine("  </image>");
			sb.AppendLine("  <spawns>");
			foreach (var s in r.Spawns.Spawn)
			{
				sb.AppendLine($"    <spawn id=\"{s.Id}\" probability=\"{s.Probability}\">");
				sb.AppendLine($"      <x>{E(s.X)}</x>");
				sb.AppendLine($"      <y>{E(s.Y)}</y>");
				sb.AppendLine($"      <next>{s.Next}</next>");
				sb.AppendLine("    </spawn>");
			}
			sb.AppendLine("  </spawns>");
			sb.AppendLine("  <animations>");
			foreach (var a in r.Animations.Animation)
			{
				string picker = string.IsNullOrEmpty(a.Picker) ? "" : $" picker=\"{E(a.Picker)}\"";
				sb.AppendLine($"    <animation id=\"{a.Id}\"{picker}>");
				sb.AppendLine($"      <name>{E(a.Name)}</name>");
				Moving(sb, "start", a.Start);
				Moving(sb, "end", a.End);
				var q = a.Sequence;
				sb.AppendLine($"      <sequence repeatfrom=\"{q.RepeatFromFrame}\" repeat=\"{E(string.IsNullOrWhiteSpace(q.RepeatCount) ? "0" : q.RepeatCount)}\">");
				foreach (int f in q.Frame ?? Array.Empty<int>()) sb.AppendLine($"        <frame>{f}</frame>");
				if (!string.IsNullOrWhiteSpace(q.Action)) sb.AppendLine($"        <action>{E(q.Action)}</action>");
				Nexts(sb, q.Next, "        ");
				sb.AppendLine("      </sequence>");
				if (a.Border?.Next?.Length > 0) { sb.AppendLine("      <border>"); Nexts(sb, a.Border.Next, "        "); sb.AppendLine("      </border>"); }
				if (a.Gravity?.Next?.Length > 0) { sb.AppendLine("      <gravity>"); Nexts(sb, a.Gravity.Next, "        "); sb.AppendLine("      </gravity>"); }
				sb.AppendLine("    </animation>");
			}
			sb.AppendLine("  </animations>");
			if (r.Childs.Child.Length == 0) sb.AppendLine("  <childs />");
			else
			{
				sb.AppendLine("  <childs>");
				foreach (var c in r.Childs.Child)
				{
					sb.AppendLine($"    <child animationid=\"{c.Id}\">");
					sb.AppendLine($"      <x>{E(c.X)}</x>");
					sb.AppendLine($"      <y>{E(c.Y)}</y>");
					sb.AppendLine($"      <next>{c.Next}</next>");
					sb.AppendLine("    </child>");
				}
				sb.AppendLine("  </childs>");
			}
			if (r.Sounds.Sound.Length == 0) sb.AppendLine("  <sounds />");
			else
			{
				sb.AppendLine("  <sounds>");
				foreach (var s in r.Sounds.Sound)
				{
					sb.AppendLine($"    <sound animationid=\"{s.Id}\">");
					sb.AppendLine($"      <probability>{s.Probability}</probability>");
					if (s.Loop > 0) sb.AppendLine($"      <loop>{s.Loop}</loop>");
					sb.AppendLine($"      <base64>{s.Base64 ?? ""}</base64>");
					sb.AppendLine("    </sound>");
				}
				sb.AppendLine("  </sounds>");
			}
			sb.AppendLine("</animations>");
			return sb.ToString();
		}

		static void Moving(StringBuilder sb, string tag, MovingNode m)
		{
			sb.AppendLine($"      <{tag}>");
			sb.AppendLine($"        <x>{E(string.IsNullOrWhiteSpace(m.X) ? "0" : m.X)}</x>");
			sb.AppendLine($"        <y>{E(string.IsNullOrWhiteSpace(m.Y) ? "0" : m.Y)}</y>");
			sb.AppendLine($"        <offsety>{m.OffsetY}</offsety>");
			sb.AppendLine($"        <opacity>{m.Opacity.ToString(Inv)}</opacity>");
			sb.AppendLine($"        <interval>{E(string.IsNullOrWhiteSpace(m.Interval) ? "200" : m.Interval)}</interval>");
			sb.AppendLine($"      </{tag}>");
		}

		static void Nexts(StringBuilder sb, NextNode[] list, string indent)
		{
			foreach (var n in list ?? Array.Empty<NextNode>())
				sb.AppendLine($"{indent}<next probability=\"{n.Probability}\" only=\"{E(string.IsNullOrWhiteSpace(n.OnlyFlag) ? "none" : n.OnlyFlag)}\">{n.Value}</next>");
		}
	}
}
