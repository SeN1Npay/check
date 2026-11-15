using System.Collections.Generic;
using Metamorph.Core.Models;

namespace Metamorph.Core.Colors;

public sealed record TerminalPalette(Rgba Background, Rgba Foreground, Rgba Cursor, Rgba Selection, Rgba Black, Rgba Red, Rgba Green, Rgba Yellow, Rgba Blue, Rgba Purple, Rgba Cyan, Rgba White, Rgba BrightBlack, Rgba BrightRed, Rgba BrightGreen, Rgba BrightYellow, Rgba BrightBlue, Rgba BrightPurple, Rgba BrightCyan, Rgba BrightWhite)
{
	public IReadOnlyList<Rgba> Ansi => new Rgba[16]
	{
		Black, Red, Green, Yellow, Blue, Purple, Cyan, White, BrightBlack, BrightRed,
		BrightGreen, BrightYellow, BrightBlue, BrightPurple, BrightCyan, BrightWhite
	};

	public static TerminalPalette From(Palette p, ColorMode mode)
	{
		Rgba background = C(p.Background);
		Rgba rgba = C(p.Foreground);
		Rgba rgba2 = C(p.Accent);
		Rgba rgba3 = C(p.Accent2);
		Rgba rgba4 = C(p.Red);
		Rgba rgba5 = C(p.Green);
		Rgba rgba6 = C(p.Yellow);
		Rgba rgba7 = rgba3.Mix(rgba5, 0.5);
		bool dark = mode == ColorMode.Dark;
		return new TerminalPalette(background, rgba, rgba2, C(p.Surface).Mix(rgba2, 0.25), dark ? C(p.Surface) : rgba, rgba4, rgba5, rgba6, rgba3, rgba2, rgba7, dark ? rgba.Darken(0.12) : C(p.Surface), C(p.Muted), Bright(rgba4), Bright(rgba5), Bright(rgba6), Bright(rgba3), Bright(rgba2), Bright(rgba7), dark ? rgba : C(p.Muted).Darken(0.3));
		Rgba Bright(Rgba c)
		{
			if (!dark)
			{
				return c.Darken(0.15);
			}
			return c.Lighten(0.15);
		}
		static Rgba C(string hex)
		{
			return Rgba.Parse(hex).WithAlpha(byte.MaxValue);
		}
	}
}
