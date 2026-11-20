namespace PetEditor
{
	/// <summary>Developer: how long each part of the start takes (only with the variable PE_TIMING = a file).</summary>
	internal static class Timing
	{
		static readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
		static readonly string file = Environment.GetEnvironmentVariable("PE_TIMING");
		public static void Mark(string what) { if (file != null) File.AppendAllText(file, $"{watch.ElapsedMilliseconds,6} ms  {what}{Environment.NewLine}"); }
	}
}
