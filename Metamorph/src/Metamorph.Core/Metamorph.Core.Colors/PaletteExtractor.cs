using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Models;

namespace Metamorph.Core.Colors;

public static class PaletteExtractor
{
	public sealed record Result(Palette Palette, ColorMode Mode, IReadOnlyList<Rgba> Clusters);

	public static Result Extract(IReadOnlyList<Rgba> pixels, ColorMode? mode = null, int k = 8)
	{
		if (pixels.Count == 0)
		{
			throw new ArgumentException("Пустое изображение", "pixels");
		}
		List<(Rgba Color, double Weight)> source = KMeans(pixels, Math.Clamp(k, 3, 16));
		double num = pixels.Average((Rgba p) => p.Luminance);
		ColorMode colorMode = (ColorMode)(((int?)mode) ?? ((num > 0.6) ? 1 : 0));
		bool dark = colorMode == ColorMode.Dark;
		List<(Rgba, double)> list = source.OrderBy<(Rgba, double), double>(((Rgba Color, double Weight) c) => c.Color.Luminance).ToList();
		Rgba item;
		if (!dark)
		{
			item = list[list.Count - 1].Item1;
		}
		else
		{
			item = list[0].Item1;
		}
		Hsl hsl = ColorMath.ToHsl(item);
		Rgba bg = ColorMath.FromHsl(hsl with
		{
			S = Math.Min(hsl.S, 0.35),
			L = (dark ? Math.Clamp(hsl.L, 0.07, 0.13) : Math.Clamp(hsl.L, 0.92, 0.97))
		});
		Hsl hsl2 = ColorMath.ToHsl(bg);
		Rgba rgba = ColorMath.FromHsl(hsl2 with
		{
			L = (dark ? (hsl2.L + 0.08) : (hsl2.L - 0.07))
		});
		Rgba color = ColorMath.FromHsl(hsl2 with
		{
			S = Math.Min(hsl2.S, 0.25),
			L = (dark ? 0.88 : 0.2)
		});
		color = ColorMath.EnsureContrast(color, bg, 7.0);
		Rgba rgba2 = ColorMath.FromHsl(hsl2 with
		{
			S = Math.Min(hsl2.S, 0.2),
			L = (dark ? 0.48 : 0.52)
		});
		List<(Rgba Color, Hsl Hsl, double Weight)> vivid = (from c in source
			select (Color: c.Color, Hsl: ColorMath.ToHsl(c.Color), Weight: c.Weight) into c
			orderby c.Hsl.S * (1.0 - Math.Abs(c.Hsl.L - 0.55)) * (0.6 + 0.4 * Math.Sqrt(c.Weight)) descending
			select c).ToList();
		Hsl accentHsl = vivid[0].Hsl;
		Hsl h2 = vivid.Skip(1).FirstOrDefault(((Rgba Color, Hsl Hsl, double Weight) v) => ColorMath.HueDistance(v.Hsl.H, accentHsl.H) > 35.0).Hsl;
		if (h2.S < 0.05 && h2.L == 0.0)
		{
			h2 = accentHsl with
			{
				H = accentHsl.H + 40.0
			};
		}
		Rgba rgba3 = Vivid(accentHsl);
		Rgba rgba4 = Vivid(h2);
		return new Result(new Palette
		{
			Background = bg.ToHex(),
			Surface = rgba.ToHex(),
			Foreground = color.ToHex(),
			Muted = rgba2.ToHex(),
			Accent = rgba3.ToHex(),
			Accent2 = rgba4.ToHex(),
			Red = Semantic(355.0).ToHex(),
			Green = Semantic(115.0).ToHex(),
			Yellow = Semantic(45.0).ToHex()
		}, colorMode, source.Select<(Rgba, double), Rgba>(((Rgba Color, double Weight) c) => c.Color).ToList());
		Rgba Semantic(double hue)
		{
			Hsl? hsl3 = vivid.Where(((Rgba Color, Hsl Hsl, double Weight) v) => v.Hsl.S > 0.25 && ColorMath.HueDistance(v.Hsl.H, hue) < 28.0).Select<(Rgba, Hsl, double), Hsl?>((Func<(Rgba, Hsl, double), Hsl?>)(((Rgba Color, Hsl Hsl, double Weight) v) => v.Hsl)).FirstOrDefault();
			Hsl hsl4 = hsl3 ?? new Hsl(hue, 0.6, dark ? 0.7 : 0.45);
			return Vivid(hsl4 with
			{
				H = ((!hsl3.HasValue) ? hue : ((hsl4.H + hue) / 2.0))
			});
		}
		Rgba Vivid(Hsl h)
		{
			return ColorMath.EnsureContrast(ColorMath.FromHsl(h with
			{
				S = Math.Clamp(h.S, 0.45, 0.85),
				L = Math.Clamp(h.L, dark ? 0.62 : 0.4, dark ? 0.78 : 0.52)
			}), bg, 3.2);
		}
	}

	private static List<(Rgba Color, double Weight)> KMeans(IReadOnlyList<Rgba> pixels, int k)
	{
		IReadOnlyList<Rgba> pixels2 = pixels;
		Random rnd = new Random(42);
		List<Rgba> sample = ((pixels2.Count > 6000) ? (from _ in Enumerable.Range(0, 6000)
			select pixels2[rnd.Next(pixels2.Count)]).ToList() : pixels2.ToList());
		List<(double, double, double)> list = new List<(double, double, double)> { ToVec(sample[rnd.Next(sample.Count)]) };
		double[] array = new double[sample.Count];
		while (list.Count < k)
		{
			double num = 0.0;
			int j;
			for (j = 0; j < sample.Count; j++)
			{
				array[j] = list.Min<(double, double, double)>(((double R, double G, double B) c) => Dist(ToVec(sample[j]), c));
				num += array[j];
			}
			if (num <= 0.0)
			{
				break;
			}
			double num2 = rnd.NextDouble() * num;
			int l;
			for (l = 0; l < sample.Count - 1; l++)
			{
				num2 -= array[l];
				if (num2 <= 0.0)
				{
					break;
				}
			}
			list.Add(ToVec(sample[l]));
		}
		int[] array2 = new int[sample.Count];
		for (int m = 0; m < 15; m++)
		{
			for (int n = 0; n < sample.Count; n++)
			{
				(double, double, double) a = ToVec(sample[n]);
				int num3 = 0;
				double num4 = double.MaxValue;
				for (int num5 = 0; num5 < list.Count; num5++)
				{
					double num6 = Dist(a, list[num5]);
					if (num6 < num4)
					{
						int num7 = num5;
						num4 = num6;
						num3 = num7;
					}
				}
				array2[n] = num3;
			}
			(double, double, double, int)[] array3 = new(double, double, double, int)[list.Count];
			for (int num8 = 0; num8 < sample.Count; num8++)
			{
				(double, double, double, int) tuple = array3[array2[num8]];
				array3[array2[num8]] = (tuple.Item1 + (double)(int)sample[num8].R, tuple.Item2 + (double)(int)sample[num8].G, tuple.Item3 + (double)(int)sample[num8].B, tuple.Item4 + 1);
			}
			for (int num9 = 0; num9 < list.Count; num9++)
			{
				if (array3[num9].Item4 > 0)
				{
					list[num9] = (array3[num9].Item1 / (double)array3[num9].Item4, array3[num9].Item2 / (double)array3[num9].Item4, array3[num9].Item3 / (double)array3[num9].Item4);
				}
			}
		}
		int[] counts = new int[list.Count];
		int[] array4 = array2;
		foreach (int num11 in array4)
		{
			counts[num11]++;
		}
		return (from c in list.Select<(double, double, double), (Rgba, double)>(((double R, double G, double B) c, int i) => (new Rgba((byte)Math.Round(c.R), (byte)Math.Round(c.G), (byte)Math.Round(c.B)), (double)counts[i] / (double)sample.Count))
			where c.Item2 > 0.0
			select c).ToList();
	}

	private static (double R, double G, double B) ToVec(Rgba c)
	{
		return (R: (int)c.R, G: (int)c.G, B: (int)c.B);
	}

	private static double Dist((double R, double G, double B) a, (double R, double G, double B) b)
	{
		double num = a.R - b.R;
		double num2 = a.G - b.G;
		double num3 = a.B - b.B;
		return num * num + num2 * num2 + num3 * num3;
	}
}
