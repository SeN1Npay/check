using System;
using System.Collections.Generic;

namespace Metamorph.Core.Colors;

public static class AccentPalette
{
	public const int BaseIndex = 3;

	public const int StartMenuIndex = 4;

	public static Rgba[] Build(Rgba accent)
	{
		accent = accent.WithAlpha(byte.MaxValue);
		return new Rgba[8]
		{
			accent.Lighten(0.6),
			accent.Lighten(0.4),
			accent.Lighten(0.2),
			accent,
			accent.Darken(0.25),
			accent.Darken(0.45),
			accent.Darken(0.65),
			Complement(accent)
		};
	}

	public static byte[] ToRegistryBytes(IReadOnlyList<Rgba> colors)
	{
		if (colors.Count != 8)
		{
			throw new ArgumentException("AccentPalette должна содержать 8 цветов.", "colors");
		}
		byte[] array = new byte[32];
		for (int i = 0; i < 8; i++)
		{
			array[i * 4] = colors[i].R;
			array[i * 4 + 1] = colors[i].G;
			array[i * 4 + 2] = colors[i].B;
			array[i * 4 + 3] = 0;
		}
		return array;
	}

	public static Rgba Complement(Rgba c)
	{
		return new Rgba((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B));
	}
}
