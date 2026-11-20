using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetEditor
{
	/// <summary>The editor's own settings (%AppData%\DesktopPet\PetEditor.json).</summary>
	internal sealed class EditorSettings
	{
		/// <summary>"auto" (as Windows), "light" or "dark".</summary>
		public string Theme { get; set; } = "auto";
		/// <summary>Accent color id (the same list as DesktopPet).</summary>
		public string Accent { get; set; } = "green";
		/// <summary>"auto" (as Windows), "pt", "en" or "es".</summary>
		public string Language { get; set; } = "auto";
		/// <summary>Open the last project when the editor starts.</summary>
		public bool OpenLast { get; set; } = true;
		/// <summary>Keep a copy (animations.xml.bak) of the file it replaces when saving.</summary>
		public bool Backup { get; set; } = true;
		/// <summary>Write README.md and icon.png next to animations.xml when they are missing.</summary>
		public bool WriteExtras { get; set; } = true;
		/// <summary>Preview background: "sky", "checker" or "dark".</summary>
		public string PreviewBack { get; set; } = "sky";
		/// <summary>Preview zoom (1, 2 or 3): the same as the "scale" option of DesktopPet.</summary>
		public int PreviewZoom { get; set; } = 1;
		/// <summary>Play the sounds of the pet in the preview.</summary>
		public bool PreviewSound { get; set; } = false;
		/// <summary>Size of the frame thumbnails in the lists (small, medium, large).</summary>
		public int Thumbs { get; set; } = 1;
		/// <summary>Size of the editor in percent (0: as the zoom of Windows). Applies when the editor opens again.</summary>
		public int UiSize { get; set; }
		/// <summary>DesktopPet.exe used by "Test on the desktop" (empty: found by itself).</summary>
		public string DesktopPetPath { get; set; } = "";
		/// <summary>Where the dividers of the Animations page are: list width in px, editor and preview as shares.</summary>
		public double SplitList { get; set; }
		public double SplitEditor { get; set; }
		public double SplitPreview { get; set; }
		/// <summary>The welcome tour was seen (Settings can show it again).</summary>
		public bool TourDone { get; set; }
		/// <summary>Projects opened lately (most recent first).</summary>
		public List<string> Recent { get; set; } = new();
		/// <summary>Window size and state.</summary>
		public int WinW { get; set; } = 1360;
		public int WinH { get; set; } = 860;
		public bool WinMax { get; set; }

		[JsonIgnore] public static EditorSettings Current { get; private set; } = new();
		// "--settings=<file>": another settings file for this run (clean pictures for the README, tests)
		static string File => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--settings="))?.Substring(11)
			?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet", "PetEditor.json");

		public static void Load()
		{
			try
			{
				if (System.IO.File.Exists(File)) Current = JsonSerializer.Deserialize<EditorSettings>(System.IO.File.ReadAllText(File)) ?? new();
			}
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("settings: " + e.Message); Current = new(); }
		}

		public static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(File));
				System.IO.File.WriteAllText(File, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
			}
			catch (Exception e) { System.Diagnostics.Debug.WriteLine("settings: " + e.Message); }
		}

		public void AddRecent(string path)
		{
			Recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
			Recent.Insert(0, path);
			if (Recent.Count > 10) Recent.RemoveRange(10, Recent.Count - 10);
			Save();
		}
	}
}
