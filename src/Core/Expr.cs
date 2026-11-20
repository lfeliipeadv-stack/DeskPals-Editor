using System.Data;
using System.Globalization;

namespace PetEditor
{
	/// <summary>
	/// The values of an animation can be formulas ("random*(screenW-imageW)/100"). They are worked out like
	/// DesktopPet does: the names are replaced by numbers and the text is computed by DataTable.
	/// </summary>
	internal sealed class ExprContext
	{
		public double ScreenW = 1920, ScreenH = 1080, AreaW = 1920, AreaH = 1040, ImageW = 64, ImageH = 64;
		public double ImageX = -1, ImageY = -1;
		public int RandS = 50, Scale = 1;
		public bool ParentFlipped;
		public Random Rand = new();
	}

	internal static class Expr
	{
		static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
		static readonly DataTable table = new();

		/// <summary>The value of a formula, or null if it cannot be worked out (the error in <paramref name="error"/>).</summary>
		public static double? Eval(string text, ExprContext c, out string error)
		{
			error = null;
			if (string.IsNullOrWhiteSpace(text)) return 0;
			string t = text;
			// a child placed by a parent that faces right goes to the other side (as in DesktopPet)
			if (c.ParentFlipped) t = t.Contains("-imageW") ? t.Replace("-imageW", "+imageW") : t.Replace("imageW", "(-imageW)");
			t = t.Replace("screenW", N(c.ScreenW)).Replace("screenH", N(c.ScreenH))
				.Replace("areaW", N(c.AreaW)).Replace("areaH", N(c.AreaH))
				.Replace("imageW", N(c.ImageW)).Replace("imageH", N(c.ImageH))
				.Replace("imageX", N(c.ImageX)).Replace("imageY", N(c.ImageY))
				.Replace("random", c.Rand.Next(0, 100).ToString(Inv)).Replace("randS", c.RandS.ToString(Inv))
				.Replace("scale", c.Scale.ToString(Inv));
			try
			{
				var v = table.Compute(t, "");
				return Convert.ToDouble(v, Inv);
			}
			catch (Exception e) { error = e.Message; return null; }
		}

		public static double Value(string text, ExprContext c, double fallback = 0) => Eval(text, c, out _) ?? fallback;

		/// <summary>True if the text is a formula that changes (random, positions), not a plain number.</summary>
		public static bool IsDynamic(string text) => text != null && (text.Contains("random") || text.Contains("randS") || text.Contains("image") || text.Contains("screen") || text.Contains("area"));

		static string N(double v) => v.ToString(Inv);
	}
}
