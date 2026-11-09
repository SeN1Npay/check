using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Metamorph.Core.Remote;

public static class FlickerMeter
{
	public readonly record struct Pair(int Index, double Changed, double MeanDiff, int Left, int Top, int Right, int Bottom)
	{
		public bool HasBox
		{
			get
			{
				if (Right >= Left)
				{
					return Bottom >= Top;
				}
				return false;
			}
		}
	}

	public sealed record Report(IReadOnlyList<Pair> Pairs, IReadOnlyList<int> Blinks, double MaxChanged, double AvgChanged, string Verdict)
	{
		public string Format(int width, int height, double intervalMs)
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(27, 4, stringBuilder2);
			handler.AppendLiteral("frames=");
			handler.AppendFormatted(Pairs.Count + 1);
			handler.AppendLiteral(" size=");
			handler.AppendFormatted(width);
			handler.AppendLiteral("x");
			handler.AppendFormatted(height);
			handler.AppendLiteral(" interval≈");
			handler.AppendFormatted(intervalMs, "0");
			handler.AppendLiteral(" ms");
			stringBuilder3.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
			handler.AppendLiteral("verdict: ");
			handler.AppendFormatted(Verdict);
			stringBuilder4.AppendLine(ref handler);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(53, 3, stringBuilder2);
			handler.AppendLiteral("changed pixels per step: avg ");
			handler.AppendFormatted(AvgChanged, "P2");
			handler.AppendLiteral(", max ");
			handler.AppendFormatted(MaxChanged, "P2");
			handler.AppendLiteral("; blinks (A→B→A): ");
			handler.AppendFormatted((Blinks.Count == 0) ? "none" : string.Join(", ", Blinks));
			stringBuilder5.AppendLine(ref handler);
			stringBuilder.AppendLine("step  changed   meanΔ  box(l,t,r,b in analysed pixels)");
			foreach (Pair pair in Pairs)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder6 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 4, stringBuilder2);
				handler.AppendFormatted(pair.Index, 4);
				handler.AppendLiteral("  ");
				handler.AppendFormatted(pair.Changed, 7, "P2");
				handler.AppendLiteral("  ");
				handler.AppendFormatted(pair.MeanDiff, 6, "0.0");
				handler.AppendLiteral("  ");
				handler.AppendFormatted(pair.HasBox ? $"{pair.Left},{pair.Top},{pair.Right},{pair.Bottom}" : "-");
				stringBuilder6.AppendLine(ref handler);
			}
			return stringBuilder.ToString();
		}
	}

	public const int PixelThreshold = 24;

	public static Pair Compare(byte[] a, byte[] b, int width, int height, int index = 0)
	{
		if (a.Length != b.Length || a.Length != width * height)
		{
			throw new ArgumentException("Кадры разного размера");
		}
		long num = 0L;
		long num2 = 0L;
		int num3 = width;
		int num4 = height;
		int num5 = -1;
		int num6 = -1;
		for (int i = 0; i < height; i++)
		{
			int num7 = i * width;
			for (int j = 0; j < width; j++)
			{
				int num8 = Math.Abs(a[num7 + j] - b[num7 + j]);
				num2 += num8;
				if (num8 > 24)
				{
					num++;
					if (j < num3)
					{
						num3 = j;
					}
					if (j > num5)
					{
						num5 = j;
					}
					if (i < num4)
					{
						num4 = i;
					}
					if (i > num6)
					{
						num6 = i;
					}
				}
			}
		}
		double num9 = Math.Max(1, a.Length);
		return new Pair(index, (double)num / num9, (double)num2 / num9, num3, num4, num5, num6);
	}

	public static Report Analyze(IReadOnlyList<byte[]> frames, int width, int height)
	{
		List<Pair> list = new List<Pair>();
		for (int i = 1; i < frames.Count; i++)
		{
			list.Add(Compare(frames[i - 1], frames[i], width, height, i));
		}
		List<int> list2 = new List<int>();
		for (int j = 1; j + 1 < frames.Count; j++)
		{
			double changed = list[j - 1].Changed;
			if (!(changed < 0.01))
			{
				double changed2 = Compare(frames[j - 1], frames[j + 1], width, height).Changed;
				double changed3 = list[j].Changed;
				if (changed2 < changed * 0.25 && changed3 > changed * 0.5)
				{
					list2.Add(j);
				}
			}
		}
		double num = ((list.Count == 0) ? 0.0 : list.Max((Pair p) => p.Changed));
		double avgChanged = ((list.Count == 0) ? 0.0 : list.Average((Pair p) => p.Changed));
		string verdict = ((list.Count == 0) ? "need at least 2 frames" : ((list2.Count >= 2) ? $"FLICKER: {list2.Count} blink frame(s) — something appears and disappears" : ((list2.Count == 1) ? "single blink — possible flicker, repeat the burst" : ((num < 0.002) ? "static — screen does not change" : ((list.Count((Pair p) => p.Changed > 0.002) * 2 < list.Count) ? "occasional changes, no blinking pattern" : "continuous motion (video/GIF/animation), no blinking pattern")))));
		return new Report(list, list2, num, avgChanged, verdict);
	}

	public static byte[] ToLuma(byte[] bgra, int width, int height, int stride, int step, out int outWidth, out int outHeight)
	{
		step = Math.Max(1, step);
		outWidth = Math.Max(1, width / step);
		outHeight = Math.Max(1, height / step);
		byte[] array = new byte[outWidth * outHeight];
		for (int i = 0; i < outHeight; i++)
		{
			int num = Math.Min(height - 1, i * step + step / 2);
			for (int j = 0; j < outWidth; j++)
			{
				int num2 = Math.Min(width - 1, j * step + step / 2);
				int num3 = num * stride + num2 * 4;
				array[i * outWidth + j] = (byte)(bgra[num3] * 29 + bgra[num3 + 1] * 150 + bgra[num3 + 2] * 77 >> 8);
			}
		}
		return array;
	}
}
