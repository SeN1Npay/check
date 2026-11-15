using System;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;

namespace Metamorph.Core.Mascot;

public sealed record MascotColors
{
	public string Skin { get; init; } = "#FFE6D8";


	public string SkinShade { get; init; } = "#F6C7B6";


	public string Blush { get; init; } = "#FF8FA6";


	public string Hair { get; init; } = "#F4A7C8";


	public string HairShade { get; init; } = "#D97FA8";


	public string HairLight { get; init; } = "#FFE0EE";


	public string Eyes { get; init; } = "#7C6CF2";


	public string EyesDark { get; init; } = "#3B2A8C";


	public string EyesLight { get; init; } = "#B9B0FF";


	public string Outfit { get; init; } = "#3B3F63";


	public string OutfitShade { get; init; } = "#2A2D4A";


	public string Ribbon { get; init; } = "#FF5C8A";


	public string White { get; init; } = "#FFFFFF";


	public string Line { get; init; } = "#4A3045";


	public string Shoes { get; init; } = "#3A2B3E";


	public string Mouth { get; init; } = "#A8384F";


	public string Tongue { get; init; } = "#FF8C9E";


	public static MascotColors Build(string hair, string eyes, string outfit, string? ribbon = null)
	{
		Rgba rgba = Parse(hair, new Rgba(244, 167, 200));
		Rgba c = Parse(eyes, new Rgba(124, 108, 242));
		Rgba c2 = Parse(outfit, new Rgba(59, 63, 99));
		Rgba rgba2 = ((ribbon == null) ? AccentFor(rgba) : Parse(ribbon, new Rgba(byte.MaxValue, 92, 138)));
		return new MascotColors
		{
			Hair = rgba.ToHex(),
			HairShade = Shade(rgba, 0.22).ToHex(),
			HairLight = rgba.Lighten(0.6).ToHex(),
			Eyes = c.ToHex(),
			EyesDark = Shade(c, 0.5).ToHex(),
			EyesLight = c.Lighten(0.45).ToHex(),
			Outfit = c2.ToHex(),
			OutfitShade = Shade(c2, 0.28).ToHex(),
			Ribbon = rgba2.ToHex(),
			Line = Shade(rgba.Mix(new Rgba(74, 48, 69), 0.75), 0.15).ToHex()
		};
	}

	public static MascotColors From(MascotSettings s, Palette? palette)
	{
		if (!s.ThemeColors || palette == null)
		{
			return Build(s.Hair, s.Eyes, s.Outfit);
		}
		Rgba rgba = Parse(palette.Accent, new Rgba(244, 167, 200));
		Rgba rgba2 = Parse(palette.Accent2, new Rgba(124, 108, 242));
		Rgba rgba3 = Parse(palette.Background, new Rgba(30, 30, 46));
		Rgba rgba4 = Parse(palette.Red, new Rgba(byte.MaxValue, 92, 138));
		Rgba c = rgba.Mix(new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.18);
		Rgba rgba5 = ((rgba3.Luminance > 0.5) ? rgba2.Mix(new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.25) : rgba3.Mix(rgba2, 0.35).Lighten(0.06));
		Rgba rgba6 = ((ColorMath.HueDistance(ColorMath.ToHsl(rgba4).H, ColorMath.ToHsl(c).H) < 25.0) ? rgba2 : rgba4);
		return Build(c.ToHex(), rgba2.ToHex(), rgba5.ToHex(), rgba6.ToHex());
	}

	private static Rgba AccentFor(Rgba hair)
	{
		Hsl hsl = ColorMath.ToHsl(hair);
		return ColorMath.FromHsl(hsl with
		{
			H = (hsl.H + 330.0) % 360.0,
			S = Math.Max(0.65, hsl.S),
			L = 0.62
		});
	}

	private static Rgba Shade(Rgba c, double amount)
	{
		return c.Darken(amount).Mix(new Rgba(90, 60, 140), amount * 0.25);
	}

	private static Rgba Parse(string? hex, Rgba fallback)
	{
		if (!Rgba.TryParse(hex, out var color))
		{
			return fallback;
		}
		return color with
		{
			A = byte.MaxValue
		};
	}
}
