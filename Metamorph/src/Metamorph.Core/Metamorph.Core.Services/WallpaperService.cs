using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Wallpaper;

namespace Metamorph.Core.Services;

public sealed class WallpaperService
{
	private const int MaxGeneratedWidth = 3840;

	private readonly AppPaths _paths;

	public static readonly string[] ImageExtensions = new string[7] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif", ".gif" };

	public static readonly string[] VideoExtensions = new string[7] { ".mp4", ".wmv", ".avi", ".mov", ".mkv", ".webm", ".m4v" };

	public WallpaperService(AppPaths paths)
	{
		_paths = paths;
	}

	public void Apply(ThemeProfile profile, string? resolvedImagePath)
	{
		string resolvedImagePath2 = resolvedImagePath;
		Apply(profile, (string? _) => resolvedImagePath2);
	}

	public void Apply(ThemeProfile profile, Func<string?, string?> resolve)
	{
		WallpaperSettings wallpaper = profile.Wallpaper;
		switch (wallpaper.Kind)
		{
		case WallpaperKind.Keep:
			break;
		case WallpaperKind.Image:
		{
			string text = resolve(wallpaper.Path);
			if (text == null || !File.Exists(text))
			{
				throw new FileNotFoundException(Loc.F("Файл обоев не найден: {0}", text ?? wallpaper.Path));
			}
			SetWallpaper(text, wallpaper.Style);
			break;
		}
		case WallpaperKind.Solid:
		{
			Rgba rgba = Rgba.Parse(wallpaper.From);
			SetDesktopColor(rgba);
			SetWallpaper(Generate(profile.Id, rgba, rgba, GradientDirection.Vertical, wallpaper.Style), wallpaper.Style);
			break;
		}
		case WallpaperKind.Gradient:
			ApplyGradient(profile.Id, wallpaper.From, wallpaper.To, wallpaper.Direction, wallpaper.Style);
			break;
		case WallpaperKind.Video:
		case WallpaperKind.Live:
		{
			string text2 = resolve(wallpaper.Path);
			if (wallpaper.EffectiveKind == WallpaperKind.Image && text2 != null && File.Exists(text2))
			{
				SetWallpaper(text2, wallpaper.Style);
				break;
			}
			string text3 = ((text2 == null) ? null : PosterPath(text2));
			if (text3 != null && File.Exists(text3))
			{
				SetWallpaper(text3, wallpaper.Style);
			}
			else
			{
				ApplyGradient(profile.Id, wallpaper.From, wallpaper.To, wallpaper.Direction, wallpaper.Style);
			}
			break;
		}
		case WallpaperKind.Slideshow:
		{
			IReadOnlyList<string> readOnlyList = SlideshowImages(resolve(wallpaper.Folder));
			if (readOnlyList.Count == 0)
			{
				throw new FileNotFoundException(Loc.F("В папке слайд-шоу нет картинок: {0}", wallpaper.Folder));
			}
			SetWallpaper(readOnlyList[wallpaper.Shuffle ? Random.Shared.Next(readOnlyList.Count) : 0], wallpaper.Style);
			break;
		}
		case WallpaperKind.TimeOfDay:
		{
			WallpaperScheduleEntry wallpaperScheduleEntry = ActiveScheduleEntry(wallpaper, DateTime.Now.TimeOfDay);
			if (wallpaperScheduleEntry != null)
			{
				ApplyScheduleEntry(profile, wallpaperScheduleEntry, resolve(wallpaperScheduleEntry.Path));
			}
			break;
		}
		}
	}

	public static bool IsAnimated(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (!(text == ".gif"))
		{
			return VideoExtensions.Contains(text);
		}
		return true;
	}

	public string? PosterPath(string mediaPath)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(mediaPath);
			if (!fileInfo.Exists)
			{
				return null;
			}
			string s = $"{fileInfo.FullName.ToLowerInvariant()}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
			string text = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).Substring(0, 16).ToLowerInvariant();
			return Path.Combine(_paths.Wallpapers, "posters", text + ".png");
		}
		catch
		{
			return null;
		}
	}

	public static bool IsStaticImage(string path)
	{
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text != null)
		{
			int length = text.Length;
			if (length != 4)
			{
				if (length == 5)
				{
					switch (text[2])
					{
					case 'p':
						break;
					case 'e':
						goto IL_00c8;
					case 'f':
						goto IL_00d7;
					case 'i':
						goto IL_00e6;
					default:
						goto IL_00f7;
					}
					if (text == ".jpeg")
					{
						goto IL_00f3;
					}
				}
			}
			else
			{
				char c = text[1];
				if ((uint)c <= 106u)
				{
					if (c != 'b')
					{
						if (c == 'j' && text == ".jpg")
						{
							goto IL_00f3;
						}
					}
					else if (text == ".bmp")
					{
						goto IL_00f3;
					}
				}
				else if (c != 'p')
				{
					if (c == 't' && text == ".tif")
					{
						goto IL_00f3;
					}
				}
				else if (text == ".png")
				{
					goto IL_00f3;
				}
			}
		}
		goto IL_00f7;
		IL_00f7:
		return false;
		IL_00d7:
		if (text == ".jfif")
		{
			goto IL_00f3;
		}
		goto IL_00f7;
		IL_00c8:
		if (text == ".webp")
		{
			goto IL_00f3;
		}
		goto IL_00f7;
		IL_00f3:
		return true;
		IL_00e6:
		if (text == ".tiff")
		{
			goto IL_00f3;
		}
		goto IL_00f7;
	}

	public void ApplyGradient(string id, string from, string to, GradientDirection direction, WallpaperStyle style)
	{
		Rgba rgba = Rgba.Parse(from);
		SetDesktopColor(rgba);
		SetWallpaper(Generate(id, rgba, Rgba.Parse(to), direction, style), style);
	}

	public void ApplyScheduleEntry(ThemeProfile profile, WallpaperScheduleEntry entry, string? resolvedPath)
	{
		WallpaperSettings wallpaper = profile.Wallpaper;
		if (resolvedPath != null && File.Exists(resolvedPath))
		{
			SetWallpaper(resolvedPath, wallpaper.Style);
		}
		else
		{
			ApplyGradient(profile.Id, entry.From ?? wallpaper.From, entry.To ?? entry.From ?? wallpaper.To, wallpaper.Direction, wallpaper.Style);
		}
	}

	public static WallpaperScheduleEntry? ActiveScheduleEntry(WallpaperSettings w, TimeSpan now)
	{
		if (w.Schedule.Count == 0)
		{
			return null;
		}
		int num = TimeOfDayParser.ActiveIndex(w.Schedule.Select((WallpaperScheduleEntry e) => e.At).ToList(), now);
		if (num < 0 || num >= w.Schedule.Count)
		{
			return null;
		}
		return w.Schedule[num];
	}

	public static IReadOnlyList<string> SlideshowImages(string? folder)
	{
		if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
		{
			return Array.Empty<string>();
		}
		return (from f in Directory.EnumerateFiles(folder)
			where ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())
			select f).OrderBy<string, string>((string f) => f, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public string Generate(string themeId, Rgba from, Rgba to, GradientDirection direction, WallpaperStyle style)
	{
		(int Width, int Height) tuple = ScreenSize(style == WallpaperStyle.Span);
		int item = tuple.Width;
		int item2 = tuple.Height;
		string path = $"{themeId}-{from.ToHex().TrimStart('#')}-{to.ToHex().TrimStart('#')}-{direction}-{item}x{item2}.bmp".ToLowerInvariant();
		string text = Path.Combine(_paths.Wallpapers, path);
		if (!File.Exists(text))
		{
			File.WriteAllBytes(text, GradientBitmap.Create(item, item2, from, to, direction));
		}
		return text;
	}

	public static void SetWallpaper(string path, WallpaperStyle style)
	{
		var (text, text2) = style switch
		{
			WallpaperStyle.Fit => ("6", "0"), 
			WallpaperStyle.Stretch => ("2", "0"), 
			WallpaperStyle.Tile => ("0", "1"), 
			WallpaperStyle.Center => ("0", "0"), 
			WallpaperStyle.Span => ("22", "0"), 
			_ => ("10", "0"), 
		};
		int num;
		if (string.Equals(Reg.Get("Control Panel\\Desktop", "WallpaperStyle") as string, text, StringComparison.Ordinal))
		{
			num = ((!string.Equals(Reg.Get("Control Panel\\Desktop", "TileWallpaper") as string, text2, StringComparison.Ordinal)) ? 1 : 0);
			if (num == 0)
			{
				goto IL_00f7;
			}
		}
		else
		{
			num = 1;
		}
		Reg.SetString("Control Panel\\Desktop", "WallpaperStyle", text);
		Reg.SetString("Control Panel\\Desktop", "TileWallpaper", text2);
		goto IL_00f7;
		IL_00f7:
		if (num == 0 && IsCurrentWallpaper(path))
		{
			EventTrace.Mark("wallpaper.spi.skipped", Path.GetFileName(path));
		}
		else
		{
			SetWallpaperRaw(path);
		}
	}

	public static bool IsCurrentWallpaper(string path)
	{
		try
		{
			string currentWallpaper = GetCurrentWallpaper();
			if (currentWallpaper.Length == 0 || !string.Equals(Path.GetFullPath(currentWallpaper), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			string path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
			return !File.Exists(path2) || File.GetLastWriteTimeUtc(path) <= File.GetLastWriteTimeUtc(path2);
		}
		catch
		{
			return false;
		}
	}

	public static void SetWallpaperRaw(string path)
	{
		EventTrace.Mark("wallpaper.spi", Path.GetFileName(path));
		if (!NativeMethods.SystemParametersInfo(20u, 0u, path, 3u))
		{
			throw new Win32Exception();
		}
	}

	public static string GetCurrentWallpaper()
	{
		StringBuilder stringBuilder = new StringBuilder(1024);
		if (!NativeMethods.SystemParametersInfo(115u, (uint)stringBuilder.Capacity, stringBuilder, 0u))
		{
			return "";
		}
		return stringBuilder.ToString();
	}

	public static void SetDesktopColor(Rgba color)
	{
		int num = color.ToColorRef();
		if (NativeMethods.GetSysColor(1) == num)
		{
			EventTrace.Mark("syscolor.skipped");
			return;
		}
		EventTrace.Mark("syscolor.set", color.ToHex());
		NativeMethods.SetSysColors(1, new int[1] { 1 }, new int[1] { num });
		Reg.SetString("Control Panel\\Colors", "Background", color.ToRegistryTriplet());
	}

	private static (int Width, int Height) ScreenSize(bool virtualScreen)
	{
		int num = NativeMethods.GetSystemMetrics(virtualScreen ? 78 : 0);
		int num2 = NativeMethods.GetSystemMetrics((!virtualScreen) ? 1 : 79);
		if (num <= 0 || num2 <= 0)
		{
			num = 1920;
			num2 = 1080;
		}
		if (num > 3840)
		{
			num2 = (int)Math.Round((double)num2 * (3840.0 / (double)num));
			num = 3840;
		}
		return (Width: num, Height: num2);
	}
}
