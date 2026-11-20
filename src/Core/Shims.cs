namespace DesktopPet
{
	/// <summary>The shared files of DesktopPet write their ignored errors here; the editor keeps them in the debug output.</summary>
	internal static class LogFile
	{
		public static void Ignored(Exception e) => System.Diagnostics.Debug.WriteLine("ignored: " + e.Message);
		public static void Important(string text) => System.Diagnostics.Debug.WriteLine(text);
	}
}

namespace DesktopPet
{
	/// <summary>The shared drawing code reports to the debug window of DesktopPet; the editor sends it to the debug output.</summary>
	internal static class StartUp
	{
		public enum DEBUG_TYPE { info, warning, error }
		public static void AddDebugInfo(DEBUG_TYPE type, string text) => System.Diagnostics.Debug.WriteLine(type + ": " + text);
	}
}
