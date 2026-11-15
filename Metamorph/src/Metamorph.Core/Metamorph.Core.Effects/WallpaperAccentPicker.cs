using System;
using System.Collections.Generic;
using Metamorph.Core.Colors;

namespace Metamorph.Core.Effects;

// Яркий акцент из пикселей обоев (через PaletteExtractor) и второй акцент для градиентов.
public static class WallpaperAccentPicker
{
	public static Rgba? Pick(IReadOnlyList<Rgba> pixels)
	{
		if (pixels == null || pixels.Count == 0)
		{
			return null;
		}
		PaletteExtractor.Result result = PaletteExtractor.Extract(pixels);
		if (!Rgba.TryParse(result.Palette.Accent, out Rgba accent))
		{
			return null;
		}
		// Слишком тусклый акцент на частицах не виден — поднимаем насыщенность и яркость.
		Hsl hsl = ColorMath.ToHsl(accent);
		return ColorMath.FromHsl(hsl with
		{
			S = Math.Max(hsl.S, 0.55),
			L = Math.Clamp(hsl.L, 0.5, 0.7)
		});
	}

	public static Rgba Secondary(Rgba accent)
	{
		Hsl hsl = ColorMath.ToHsl(accent);
		return ColorMath.FromHsl(hsl with
		{
			H = (hsl.H + 40.0) % 360.0
		});
	}

	public static Rgba Mix(Rgba a, Rgba b, double f)
	{
		f = Math.Clamp(f, 0.0, 1.0);
		return new Rgba((byte)Math.Round(a.R + (b.R - a.R) * f), (byte)Math.Round(a.G + (b.G - a.G) * f), (byte)Math.Round(a.B + (b.B - a.B) * f));
	}
}
