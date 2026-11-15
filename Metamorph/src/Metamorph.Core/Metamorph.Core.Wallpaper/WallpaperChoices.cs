using System;
using System.Collections.Generic;
using System.IO;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.Core.Wallpaper;

public static class WallpaperChoices
{
	public static WallpaperSettings? Resolve(WallpaperChoice? choice, ThemeProfile theme)
	{
		if (choice == null || string.IsNullOrWhiteSpace(choice.Path))
		{
			return null;
		}
		string path = choice.Path;
		WallpaperStyle style = choice.Style;
		bool flag = ((style == WallpaperStyle.Tile || style == WallpaperStyle.Span) ? true : false);
		WallpaperStyle style2 = ((!flag) ? choice.Style : WallpaperStyle.Fill);
		switch (WallpaperLibrary.MediaOf(path))
		{
		case WallpaperMedia.Folder:
			return new WallpaperSettings
			{
				Kind = WallpaperKind.Slideshow,
				Folder = path,
				IntervalMinutes = 15,
				Shuffle = true,
				Style = style2,
				From = theme.Palette.Background,
				To = theme.Palette.Surface
			};
		case WallpaperMedia.Gif:
		case WallpaperMedia.Video:
			return new WallpaperSettings
			{
				Kind = WallpaperKind.Video,
				Path = path,
				Muted = choice.Muted,
				Style = style2,
				From = theme.Palette.Background,
				To = theme.Palette.Surface
			};
		case WallpaperMedia.Image:
			return new WallpaperSettings
			{
				Kind = WallpaperKind.Image,
				Path = path,
				Style = choice.Style,
				From = theme.Palette.Background,
				To = theme.Palette.Surface
			};
		default:
			return null;
		}
	}

	public static ThemeProfile Effective(ThemeProfile theme, WallpaperPrefs? prefs)
	{
		WallpaperChoice wallpaperChoice = prefs?.Override;
		if (wallpaperChoice == null)
		{
			return theme;
		}
		WallpaperSettings wallpaperSettings = Resolve(wallpaperChoice, theme);
		if (wallpaperSettings == null)
		{
			return theme;
		}
		ThemeProfile themeProfile = ThemeJson.Clone(theme);
		themeProfile.SourcePath = theme.SourcePath;
		themeProfile.Wallpaper = wallpaperSettings;
		return themeProfile;
	}

	public static string Describe(WallpaperChoice? choice)
	{
		if (choice == null || string.IsNullOrWhiteSpace(choice.Path))
		{
			return Loc.T("обои темы");
		}
		return Path.GetFileName(choice.Path.TrimEnd('\\', '/'));
	}

	public static WallpaperChoice? Next(WallpaperChoice? current, IReadOnlyList<string> library)
	{
		if (library.Count == 0)
		{
			return current;
		}
		string text = current?.Path;
		int num = ((text != null) ? IndexOf(library, text) : (-1));
		WallpaperChoice obj = current?.Copy() ?? new WallpaperChoice();
		obj.Path = library[(num + 1) % library.Count];
		return obj;
	}

	private static int IndexOf(IReadOnlyList<string> list, string path)
	{
		for (int i = 0; i < list.Count; i++)
		{
			if (string.Equals(list[i], path, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}
}
