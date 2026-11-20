using System.Drawing;

namespace PetEditor
{
	/// <summary>
	/// An animated GIF (looping, transparent background) from frames of the same size. Pixel art has few colours: up
	/// to 255 are kept exactly, more are reduced by dropping the lowest bits.
	/// </summary>
	internal static class GifWriter
	{
		public static byte[] Write(List<Bitmap> frames, int delayMs)
		{
			int w = frames[0].Width, h = frames[0].Height;
			// palette: index 0 = transparent
			var colors = new Dictionary<int, int>();
			int shift = 0;
			while (true)
			{
				colors.Clear();
				bool full = false;
				foreach (var f in frames)
				{
					for (int y = 0; y < h && !full; y++)
						for (int x = 0; x < w; x++)
						{
							var c = f.GetPixel(x, y);
							if (c.A < 128) continue;
							int k = Key(c, shift);
							if (!colors.ContainsKey(k)) { if (colors.Count == 255) { full = true; break; } colors[k] = colors.Count + 1; }
						}
					if (full) break;
				}
				if (!full) break;
				shift++;
			}
			using var ms = new MemoryStream();
			using var bw = new BinaryWriter(ms);
			bw.Write("GIF89a".ToCharArray());
			bw.Write((short)w); bw.Write((short)h);
			bw.Write((byte)0xF7);   // global colour table of 256 entries
			bw.Write((byte)0); bw.Write((byte)0);
			var table = new byte[256 * 3];
			foreach (var kv in colors)
			{
				var c = FromKey(kv.Key, shift);
				table[kv.Value * 3] = c.R; table[kv.Value * 3 + 1] = c.G; table[kv.Value * 3 + 2] = c.B;
			}
			bw.Write(table);
			// loop forever
			bw.Write(new byte[] { 0x21, 0xFF, 0x0B });
			bw.Write("NETSCAPE2.0".ToCharArray());
			bw.Write(new byte[] { 3, 1, 0, 0, 0 });
			foreach (var f in frames)
			{
				bw.Write(new byte[] { 0x21, 0xF9, 4, 0x09 });   // dispose to background, transparent colour on
				bw.Write((short)Math.Max(2, delayMs / 10));
				bw.Write((byte)0); bw.Write((byte)0);
				bw.Write((byte)0x2C); bw.Write((short)0); bw.Write((short)0); bw.Write((short)w); bw.Write((short)h); bw.Write((byte)0);
				var idx = new byte[w * h];
				for (int y = 0; y < h; y++)
					for (int x = 0; x < w; x++)
					{
						var c = f.GetPixel(x, y);
						idx[y * w + x] = c.A < 128 ? (byte)0 : (byte)colors[Key(c, shift)];
					}
				Lzw(bw, idx);
			}
			bw.Write((byte)0x3B);
			bw.Flush();
			return ms.ToArray();
		}

		static int Key(Color c, int shift) => ((c.R >> shift) << 16) | ((c.G >> shift) << 8) | (c.B >> shift);
		static Color FromKey(int k, int shift) => Color.FromArgb(((k >> 16) & 255) << shift, ((k >> 8) & 255) << shift, (k & 255) << shift);

		/// <summary>LZW with 8-bit codes, written in sub-blocks of up to 255 bytes.</summary>
		static void Lzw(BinaryWriter bw, byte[] data)
		{
			const int minCode = 8, clear = 1 << minCode, end = clear + 1;
			bw.Write((byte)minCode);
			var output = new List<byte>();
			int bitBuf = 0, bitCount = 0, codeSize = minCode + 1, next = end + 1;
			void Emit(int code)
			{
				bitBuf |= code << bitCount; bitCount += codeSize;
				while (bitCount >= 8) { output.Add((byte)(bitBuf & 255)); bitBuf >>= 8; bitCount -= 8; }
			}
			var dict = new Dictionary<(int, byte), int>();
			Emit(clear);
			int prefix = data[0];
			for (int i = 1; i < data.Length; i++)
			{
				byte c = data[i];
				if (dict.TryGetValue((prefix, c), out int code)) { prefix = code; continue; }
				Emit(prefix);
				if (next < 4096)
				{
					dict[(prefix, c)] = next++;
					if (next > (1 << codeSize) && codeSize < 12) codeSize++;
				}
				else { Emit(clear); dict.Clear(); next = end + 1; codeSize = minCode + 1; }
				prefix = c;
			}
			Emit(prefix);
			Emit(end);
			if (bitCount > 0) output.Add((byte)(bitBuf & 255));
			for (int i = 0; i < output.Count; i += 255)
			{
				int n = Math.Min(255, output.Count - i);
				bw.Write((byte)n);
				bw.Write(output.GetRange(i, n).ToArray());
			}
			bw.Write((byte)0);
		}
	}
}
