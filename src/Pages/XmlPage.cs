using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using DesktopPet;
using XmlData;

namespace PetEditor
{
	/// <summary>
	/// The XML of the pet as it will be saved, coloured. The big blocks (sprite sheet, icon, sounds) are shown as short
	/// marks, so the text stays readable. It can be edited: "Apply" reads it back (the marks bring the blocks back).
	/// </summary>
	internal sealed class XmlPage : Page
	{
		RichTextBox box;
		Label state;
		TextBox find;
		bool edited, loading;

		// the big blocks (picture, icon, sounds) are shown as a short mark in the language of the editor; Apply knows
		// the marks of every language (the text may have been made before the language was changed)
		static string ImageMark => "«" + L.T("xml.markImage") + "»";
		static string IconMark => "«" + L.T("xml.markIcon") + "»";
		static bool IsMark(string text, string key) => L.All(key).Any(m => text?.Trim() == "«" + m + "»");
		static string SoundMark(int i) => $"«{L.T("xml.markSound")} {i + 1}»";

		public override void Bind(MainForm main, PetDoc doc)
		{
			base.Bind(main, doc);
			Controls.Clear();
			var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
			var top = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 0, 4), BackColor = Theme.Surface };
			var col = Ui.Column(Ui.Title(L.T("xml.title"), 20), Ui.Hint(L.T("xml.intro"), 1000));
			var apply = new FlatButton(L.T("xml.apply"), primary: true); apply.Click += (s, e) => Apply();
			var undo = new FlatButton(L.T("xml.discard")); undo.Click += (s, e) => ShowXml();
			var copy = new FlatButton(L.T("xml.copy")); copy.Click += (s, e) => { Clipboard.SetText(PetXmlWriter.Write(Doc.Root)); state.Text = L.T("xml.copied"); };
			var export = new FlatButton(L.T("xml.export"));
			export.Click += (s, e) =>
			{
				using var dlg = new SaveFileDialog { Filter = L.T("open.filter"), FileName = "animations.xml" };
				if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dlg.FileName, PetXmlWriter.Write(Doc.Root), new UTF8Encoding(false));
			};
			find = Ui.Box("", 200); find.PlaceholderText = L.T("xml.find");
			find.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Find(); e.SuppressKeyPress = true; } };
			state = Ui.Text("", true, 12); state.Margin = new Padding(8, 8, 0, 0);
			col.Controls.Add(Ui.Row(apply, undo, copy, export, find, state));
			top.Controls.Add(col);
			box = new RichTextBox
			{
				Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 10f), WordWrap = false, DetectUrls = false,
				BackColor = Theme.IsDark ? Color.FromArgb(30, 30, 33) : Color.FromArgb(252, 252, 253), ForeColor = Theme.Text, AcceptsTab = true,
			};
			box.TextChanged += (s, e) => { if (loading) return; edited = true; state.Text = L.T("xml.edited"); };
			card.Controls.Add(box);
			card.Controls.Add(top);
			Controls.Add(card);
		}

		/// <summary>The XML of the pet with the marks in place of the big blocks.</summary>
		string XmlText()
		{
			var r = Doc.Root;
			string png = r.Image.Png, icon = r.Header.Icon;
			var sounds = r.Sounds.Sound.Select(s => s.Base64).ToArray();
			try
			{
				r.Image.Png = string.IsNullOrEmpty(png) ? "" : ImageMark;
				r.Header.Icon = string.IsNullOrEmpty(icon) ? "" : IconMark;
				for (int i = 0; i < sounds.Length; i++) r.Sounds.Sound[i].Base64 = string.IsNullOrEmpty(sounds[i]) ? "" : SoundMark(i);
				return PetXmlWriter.Write(r);
			}
			finally
			{
				r.Image.Png = png; r.Header.Icon = icon;
				for (int i = 0; i < sounds.Length; i++) r.Sounds.Sound[i].Base64 = sounds[i];
			}
		}

		void ShowXml()
		{
			loading = true;
			box.Rtf = Rtf(XmlText());
			loading = false;
			edited = false;
			state.Text = "";
		}

		void Apply()
		{
			var r = AnimationXML.ParseXML(box.Text);
			if (r == null) { Main.Message(L.T("xml.bad")); return; }
			var old = Doc.Root;
			if (IsMark(r.Image?.Png, "xml.markImage")) r.Image.Png = old.Image.Png;
			if (IsMark(r.Header?.Icon, "xml.markIcon")) r.Header.Icon = old.Header.Icon;
			foreach (var s in r.Sounds?.Sound ?? Array.Empty<SoundNode>())
			{
				var m = Regex.Match(s.Base64 ?? "", "«(?:" + string.Join("|", L.All("xml.markSound").Select(Regex.Escape)) + @") (\d+)»");
				if (m.Success && int.Parse(m.Groups[1].Value) - 1 < old.Sounds.Sound.Length) s.Base64 = old.Sounds.Sound[int.Parse(m.Groups[1].Value) - 1].Base64;
			}
			Doc.Replace(r);
			ShowXml();
			state.Text = L.T("xml.applied");
		}

		void Find()
		{
			string q = find.Text;
			if (q.Length == 0) return;
			int start = box.SelectionStart + box.SelectionLength;
			int i = box.Text.IndexOf(q, start, StringComparison.OrdinalIgnoreCase);
			if (i < 0) i = box.Text.IndexOf(q, 0, StringComparison.OrdinalIgnoreCase);
			if (i < 0) { state.Text = L.T("xml.notFound"); return; }
			box.Select(i, q.Length);
			box.ScrollToCaret();
			box.Focus();
		}

		/// <summary>The text as RTF with colours for tags, attributes, values and the marks (fast even for big pets).</summary>
		static string Rtf(string xml)
		{
			bool dark = Theme.IsDark;
			Color text = Theme.Text, tag = dark ? Color.FromArgb(110, 170, 255) : Color.FromArgb(0, 90, 180), attr = dark ? Color.FromArgb(230, 160, 90) : Color.FromArgb(170, 70, 0),
				val = dark ? Color.FromArgb(140, 210, 130) : Color.FromArgb(20, 120, 40), mark = Theme.Accent;
			string C(Color c) => $"\\red{c.R}\\green{c.G}\\blue{c.B};";
			var sb = new StringBuilder("{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Consolas;}}{\\colortbl;" + C(text) + C(tag) + C(attr) + C(val) + C(mark) + "}\\f0\\fs20 ");
			void Out(string s, int color)
			{
				sb.Append("\\cf").Append(color).Append(' ');
				foreach (char ch in s)
				{
					if (ch == '\\' || ch == '{' || ch == '}') sb.Append('\\').Append(ch);
					else if (ch == '\n') sb.Append("\\par\n");
					else if (ch == '\r') { }
					else if (ch > 127) sb.Append("\\u").Append((int)ch).Append('?');
					else sb.Append(ch);
				}
			}
			int i = 0;
			while (i < xml.Length)
			{
				int lt = xml.IndexOf('<', i);
				if (lt < 0) { Out(xml.Substring(i), 1); break; }
				if (lt > i) Out(xml.Substring(i, lt - i), xml.IndexOf('«', i, lt - i) >= 0 ? 5 : 1);
				int gt = xml.IndexOf('>', lt);
				if (gt < 0) { Out(xml.Substring(lt), 1); break; }
				string t = xml.Substring(lt, gt - lt + 1);
				if (t.StartsWith("<![CDATA["))
				{
					int end = xml.IndexOf("]]>", lt, StringComparison.Ordinal);
					if (end < 0) end = xml.Length - 3;
					t = xml.Substring(lt, end + 3 - lt);
					Out(t, 5);
					i = end + 3;
					continue;
				}
				// <name attr="value">: the name and the brackets in one colour, attributes and values in others
				var m = Regex.Match(t, @"^(</?[\w:.-]*)(.*?)(/?>)$", RegexOptions.Singleline);
				if (!m.Success) { Out(t, 2); i = gt + 1; continue; }
				Out(m.Groups[1].Value, 2);
				string inner = m.Groups[2].Value;
				int at = 0;
				foreach (Match a in Regex.Matches(inner, @"(\s+)([\w:.-]+)(=)(""[^""]*"")"))
				{
					if (a.Index > at) Out(inner.Substring(at, a.Index - at), 2);   // "?xml", a lone word…
					Out(a.Groups[1].Value, 1); Out(a.Groups[2].Value, 3); Out(a.Groups[3].Value, 1); Out(a.Groups[4].Value, 4);
					at = a.Index + a.Length;
				}
				if (at < inner.Length) Out(inner.Substring(at), 2);
				Out(m.Groups[3].Value, 2);
				i = gt + 1;
			}
			sb.Append('}');
			return sb.ToString();
		}

		public override void Shown() { if (!edited) ShowXml(); }
		public override void Reload(string what) { if (!edited && Visible) ShowXml(); }
		public override void Hidden()
		{
			if (edited && MessageBox.Show(this, L.T("xml.leave"), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Apply();
			edited = false;
		}
	}
}
