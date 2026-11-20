using System.Diagnostics;

namespace PetEditor
{
	/// <summary>
	/// "Test on the desktop": the pet as it is now (saved or not) goes to a temporary file and a second DesktopPet
	/// shows only it ("DesktopPet.exe --test-pet=&lt;file&gt;": it does not touch the user's pet or settings). While
	/// it runs, every change in the editor is written again and that DesktopPet reloads the pet.
	/// </summary>
	internal static class DesktopTest
	{
		static Process running;
		static readonly string Folder = Path.Combine(Path.GetTempPath(), "PetEditor", "test");
		static string FilePath => Path.Combine(Folder, "animations.xml");
		static readonly System.Windows.Forms.Timer later = new() { Interval = 900 };
		static PetDoc pending;

		static DesktopTest() => later.Tick += (s, e) => { later.Stop(); if (pending != null && IsRunning) Write(pending); };

		public static bool IsRunning => running != null && !running.HasExited;

		/// <summary>DesktopPet.exe: the one chosen in the settings, next to the editor, installed, or the developer build.</summary>
		public static string FindDesktopPet()
		{
			var s = EditorSettings.Current.DesktopPetPath;
			if (!string.IsNullOrWhiteSpace(s) && File.Exists(s) && SupportsTest(s)) return s;
			string here = AppContext.BaseDirectory;
			var candidates = new List<string> { Path.Combine(here, "DesktopPet.exe") };
			// developer: the build of the repository (editor\bin\...\ next to src\bin\...\) comes before an installed copy
			var dir = new DirectoryInfo(here);
			for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
				candidates.Add(Path.Combine(dir.FullName, "src", "bin", "Release", "net10.0-windows", "win-x64", "DesktopPet.exe"));
			candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DesktopPet", "DesktopPet.exe"));
			candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "DesktopPet.exe"));
			return candidates.FirstOrDefault(c => File.Exists(c) && SupportsTest(c));
		}

		/// <summary>The first version with "--test-pet". An older one would start as a second normal DesktopPet.</summary>
		public static readonly Version TestVersion = new(2, 8, 3);

		/// <summary>This DesktopPet.exe can show a test pet (its version, or a developer build of the repository).</summary>
		public static bool SupportsTest(string exe)
		{
			try
			{
				if (exe.Replace('/', '\\').Contains(@"\src\bin\", StringComparison.OrdinalIgnoreCase)) return true;
				var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
				return new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart) >= TestVersion;
			}
			catch { return false; }
		}

		/// <summary>An installed DesktopPet too old for the test (to say so instead of "not found").</summary>
		public static string OldInstalled()
		{
			foreach (var c in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DesktopPet", "DesktopPet.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet", "DesktopPet.exe") })
				if (File.Exists(c) && !SupportsTest(c)) return System.Diagnostics.FileVersionInfo.GetVersionInfo(c).FileVersion;
			return null;
		}

		public static void Run(MainForm owner, PetDoc doc)
		{
			if (doc == null) return;
			var (errors, _) = Validator.Count(doc);
			if (errors > 0)
			{
				if (MessageBox.Show(owner, L.T("test.errors", errors), L.T("app.name"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				{
					owner.Go("check");
					return;
				}
			}
			string exe = FindDesktopPet();
			if (exe == null)
			{
				string old = OldInstalled();
				owner.Message(old != null ? L.T("test.oldApp", old, TestVersion) : L.T("test.noApp"));
				if (old == null) owner.Go("settings");
				return;
			}
			try
			{
				Write(doc);
				if (IsRunning) return;   // the running test reloads the file by itself
				running = Process.Start(new ProcessStartInfo(exe) { ArgumentList = { "--test-pet=" + FilePath }, UseShellExecute = false });
			}
			catch (Exception ex) { owner.Message(L.T("test.fail", ex.Message)); }
		}

		/// <summary>A change in the editor: the test on the desktop gets it a moment later.</summary>
		public static void Refresh(PetDoc doc)
		{
			if (!IsRunning) return;
			pending = doc;
			later.Stop(); later.Start();
		}

		static void Write(PetDoc doc)
		{
			Directory.CreateDirectory(Folder);
			File.WriteAllText(FilePath, PetXmlWriter.Write(doc.Root), new System.Text.UTF8Encoding(false));
		}

		public static void Close()
		{
			try { if (IsRunning) { running.CloseMainWindow(); if (!running.WaitForExit(800)) running.Kill(); } }
			catch (Exception e) { Debug.WriteLine("test close: " + e.Message); }
		}
	}
}
