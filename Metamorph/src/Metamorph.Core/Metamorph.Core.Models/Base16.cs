using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Metamorph.Core.Colors;

namespace Metamorph.Core.Models;

public static class Base16
{
	private static readonly Regex ColorLine = new Regex("^\\s*(base0[0-9A-Fa-f])\\s*:\\s*[\"']?#?([0-9A-Fa-f]{6})[\"']?", RegexOptions.Multiline);

	private static readonly Regex NameLine = new Regex("^\\s*(?:scheme|name)\\s*:\\s*[\"']?(.+?)[\"']?\\s*$", RegexOptions.Multiline);

	private static readonly Regex AuthorLine = new Regex("^\\s*author\\s*:\\s*[\"']?(.+?)[\"']?\\s*$", RegexOptions.Multiline);

	public static ThemeProfile Import(string yaml, string? fallbackName = null)
	{
		Dictionary<string, string> colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (Match item in ColorLine.Matches(yaml))
		{
			colors[item.Groups[1].Value.ToLowerInvariant()] = "#" + item.Groups[2].Value.ToUpperInvariant();
		}
		List<string> list = (from i in Enumerable.Range(0, 16)
			select $"base0{i:x}" into k
			where !colors.ContainsKey(k)
			select k).ToList();
		if (list.Count > 0)
		{
			throw new FormatException("В схеме base16 нет цветов: " + string.Join(", ", list));
		}
		Match match2 = NameLine.Match(yaml);
		string name = ((match2 != null && match2.Success) ? match2.Groups[1].Value.Trim() : (fallbackName ?? "Base16"));
		Match match3 = AuthorLine.Match(yaml);
		string author = ((match3 != null && match3.Success) ? match3.Groups[1].Value.Trim() : "");
		ColorMode colorMode = ((Rgba.Parse(colors["base00"]).Luminance > 0.5) ? ColorMode.Light : ColorMode.Dark);
		return new ThemeProfile
		{
			Id = Slug(name),
			Name = name,
			Author = author,
			Description = "Импортировано из схемы base16",
			Mode = colorMode,
			Palette = new Palette
			{
				Background = colors["base00"],
				Surface = colors["base01"],
				Foreground = colors["base05"],
				Muted = colors["base03"],
				Accent = colors["base0e"],
				Accent2 = colors["base0d"],
				Red = colors["base08"],
				Green = colors["base0b"],
				Yellow = colors["base0a"]
			},
			Wallpaper = 
			{
				Kind = WallpaperKind.Gradient,
				From = colors["base00"],
				To = colors["base01"]
			},
			System = 
			{
				AccentOnTaskbar = (colorMode == ColorMode.Dark)
			},
			Dock = 
			{
				Enabled = true
			},
			Taskbar = 
			{
				Mode = TaskbarMode.Hidden
			},
			Bar = 
			{
				Right = new List<string> { "tray", "layout", "cpu", "ram", "battery" }
			}
		};
	}

	public static string Export(ThemeProfile profile)
	{
		Palette palette = profile.Palette;
		TerminalPalette terminalPalette = TerminalPalette.From(palette, profile.Mode);
		Rgba rgba = C(palette.Background);
		Rgba rgba2 = C(palette.Foreground);
		Rgba[] array = new Rgba[16]
		{
			rgba,
			C(palette.Surface),
			C(palette.Surface).Mix(C(palette.Muted), 0.35),
			C(palette.Muted),
			C(palette.Muted).Mix(rgba2, 0.5),
			rgba2,
			rgba2.Mix((profile.Mode == ColorMode.Dark) ? new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Rgba(0, 0, 0), 0.3),
			rgba2.Mix((profile.Mode == ColorMode.Dark) ? new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Rgba(0, 0, 0), 0.6),
			C(palette.Red),
			C(palette.Red).Mix(C(palette.Yellow), 0.5),
			C(palette.Yellow),
			C(palette.Green),
			terminalPalette.Cyan,
			C(palette.Accent2),
			C(palette.Accent),
			C(palette.Red).Darken(0.3)
		};
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("scheme: \"");
		handler.AppendFormatted(profile.Name.Replace("\"", "'"));
		handler.AppendLiteral("\"");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("author: \"");
		handler.AppendFormatted(string.IsNullOrWhiteSpace(profile.Author) ? "Metamorph" : profile.Author.Replace("\"", "'"));
		handler.AppendLiteral("\"");
		stringBuilder4.AppendLine(ref handler);
		for (int i = 0; i < 16; i++)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, stringBuilder2);
			handler.AppendLiteral("base0");
			handler.AppendFormatted(i, "X");
			handler.AppendLiteral(": \"");
			handler.AppendFormatted(array[i].ToHex().TrimStart('#'));
			handler.AppendLiteral("\"");
			stringBuilder5.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
		static Rgba C(string hex)
		{
			return Rgba.Parse(hex).WithAlpha(byte.MaxValue);
		}
	}

	public static string Slug(string name)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = name.ToLowerInvariant();
		foreach (char c in text)
		{
			bool flag;
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'a':
			case 'b':
			case 'c':
			case 'd':
			case 'e':
			case 'f':
			case 'g':
			case 'h':
			case 'i':
			case 'j':
			case 'k':
			case 'l':
			case 'm':
			case 'n':
			case 'o':
			case 'p':
			case 'q':
			case 'r':
			case 's':
			case 't':
			case 'u':
			case 'v':
			case 'w':
			case 'x':
			case 'y':
			case 'z':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				stringBuilder.Append(c);
			}
			else if (stringBuilder.Length > 0)
			{
				if (stringBuilder[stringBuilder.Length - 1] != '-')
				{
					stringBuilder.Append('-');
				}
			}
		}
		string text2 = stringBuilder.ToString().Trim('-');
		if (text2.Length != 0)
		{
			return text2;
		}
		return "theme-" + DateTime.Now.ToString("yyyyMMddHHmmss");
	}
}
