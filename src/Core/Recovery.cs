using System.Text.Json;

namespace PetEditor
{
	/// <summary>
	/// Automatic copies of the pet being edited (every minute while it has changes), in
	/// %AppData%\DesktopPet\PetEditor\recover. If the editor closes without saving (a crash, the computer turned off),
	/// the next start offers to bring the work back. A normal save or close removes the copy.
	/// </summary>
	internal static class Recovery
	{
		static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet", "PetEditor", "recover");
		static readonly string session = Guid.NewGuid().ToString("N").Substring(0, 8);

		public sealed record Item(string Copy, string Original, string Name, DateTime When);

		static string Key(PetDoc d) => d.FilePath == null ? "novo-" + session
			: Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(d.FilePath.ToLowerInvariant()))).Substring(0, 12);

		public static void Save(PetDoc d)
		{
			try
			{
				Directory.CreateDirectory(Dir);
				string key = Key(d);
				File.WriteAllText(Path.Combine(Dir, key + ".xml"), PetXmlWriter.Write(d.Root), new System.Text.UTF8Encoding(false));
				File.WriteAllText(Path.Combine(Dir, key + ".json"), JsonSerializer.Serialize(new { original = d.FilePath, name = d.Name, when = DateTime.Now }));
			}
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("recovery: " + e.Message); }
		}

		public static void Clear(PetDoc d)
		{
			try { foreach (var ext in new[] { ".xml", ".json" }) File.Delete(Path.Combine(Dir, Key(d) + ext)); }
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("recovery: " + e.Message); }
		}

		/// <summary>Copies left by an editor that did not close normally.</summary>
		public static List<Item> Pending()
		{
			var list = new List<Item>();
			if (!Directory.Exists(Dir)) return list;
			foreach (var json in Directory.GetFiles(Dir, "*.json"))
			{
				try
				{
					string xml = Path.ChangeExtension(json, ".xml");
					if (!File.Exists(xml)) { File.Delete(json); continue; }
					using var doc = JsonDocument.Parse(File.ReadAllText(json));
					var r = doc.RootElement;
					list.Add(new Item(xml, r.GetProperty("original").GetString(), r.GetProperty("name").GetString(), r.GetProperty("when").GetDateTime()));
				}
				catch (Exception e) { System.Diagnostics.Debug.WriteLine("recovery: " + e.Message); }
			}
			return list.OrderByDescending(i => i.When).ToList();
		}

		public static void Discard(Item i)
		{
			try { File.Delete(i.Copy); File.Delete(Path.ChangeExtension(i.Copy, ".json")); }
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("recovery: " + e.Message); }
		}
	}
}
