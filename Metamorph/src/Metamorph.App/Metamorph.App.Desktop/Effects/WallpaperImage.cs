using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Effects;

// Текущие системные обои: файл, стиль и цвет фона — чтобы подложка рисовала их сама (параллакс/частицы).
internal sealed record WallpaperInfo(string? Path, WallpaperStyle Style, Color Background, DateTime Stamp)
{
	public static WallpaperInfo Read()
	{
		string path = null;
		DateTime stamp = default;
		try
		{
			string current = WallpaperService.GetCurrentWallpaper();
			if (!string.IsNullOrWhiteSpace(current))
			{
				if (!File.Exists(current))
				{
					// Исходный файл удалён — Windows показывает свою перекодированную копию.
					string transcoded = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
					current = File.Exists(transcoded) ? transcoded : null;
				}
				if (current != null)
				{
					path = current;
					stamp = File.GetLastWriteTimeUtc(current);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("Эффекты: путь обоев", ex);
		}
		return new WallpaperInfo(path, ReadStyle(), ReadBackground(), stamp);
	}

	private static WallpaperStyle ReadStyle()
	{
		try
		{
			string style = Reg.Get("Control Panel\\Desktop", "WallpaperStyle") as string;
			string tile = Reg.Get("Control Panel\\Desktop", "TileWallpaper") as string;
			return style switch
			{
				"6" => WallpaperStyle.Fit,
				"2" => WallpaperStyle.Stretch,
				"22" => WallpaperStyle.Span,
				"0" => (tile == "1") ? WallpaperStyle.Tile : WallpaperStyle.Center,
				_ => WallpaperStyle.Fill,
			};
		}
		catch
		{
			return WallpaperStyle.Fill;
		}
	}

	private static Color ReadBackground()
	{
		try
		{
			if (Reg.Get("Control Panel\\Colors", "Background") is string text)
			{
				string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 3 && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
				{
					return Color.FromRgb(r, g, b);
				}
			}
		}
		catch
		{
		}
		return Colors.Black;
	}
}

internal sealed record LoadedWallpaper(BitmapSource? Bitmap, int OriginalWidth, int OriginalHeight);

internal static class WallpaperImage
{
	// Декодирует сразу под размер монитора (DecodePixelWidth) и замораживает — безопасно передавать между потоками.
	public static LoadedWallpaper Load(WallpaperInfo info, int targetWidth, int targetHeight, double overscan)
	{
		if (string.IsNullOrEmpty(info.Path))
		{
			return new LoadedWallpaper(null, 0, 0);
		}
		byte[] data = File.ReadAllBytes(info.Path);
		int iw;
		int ih;
		using (MemoryStream probe = new MemoryStream(data, writable: false))
		{
			BitmapFrame frame = BitmapFrame.Create(probe, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
			iw = frame.PixelWidth;
			ih = frame.PixelHeight;
		}
		if (iw <= 0 || ih <= 0)
		{
			return new LoadedWallpaper(null, 0, 0);
		}
		double tw = Math.Max(1, targetWidth) * overscan;
		double th = Math.Max(1, targetHeight) * overscan;
		double s = info.Style switch
		{
			WallpaperStyle.Fit => Math.Min(tw / iw, th / ih),
			WallpaperStyle.Stretch => Math.Max(tw / iw, th / ih),
			WallpaperStyle.Center => 1.0,
			WallpaperStyle.Tile => 1.0,
			_ => Math.Max(tw / iw, th / ih),
		};
		BitmapImage image = new BitmapImage();
		image.BeginInit();
		image.CacheOption = BitmapCacheOption.OnLoad;
		image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
		image.StreamSource = new MemoryStream(data, writable: false);
		if (s < 1.0)
		{
			image.DecodePixelWidth = Math.Max(1, (int)Math.Ceiling(iw * s));
		}
		image.EndInit();
		image.Freeze();
		return new LoadedWallpaper(image, iw, ih);
	}

	// Маленькая копия для поиска акцента.
	public static List<Rgba> SamplePixels(string path, int width = 64)
	{
		List<Rgba> pixels = new List<Rgba>();
		byte[] data = File.ReadAllBytes(path);
		BitmapImage image = new BitmapImage();
		image.BeginInit();
		image.CacheOption = BitmapCacheOption.OnLoad;
		image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
		image.StreamSource = new MemoryStream(data, writable: false);
		image.DecodePixelWidth = width;
		image.EndInit();
		image.Freeze();
		FormatConvertedBitmap bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0.0);
		int w = bgra.PixelWidth;
		int h = bgra.PixelHeight;
		if (w <= 0 || h <= 0)
		{
			return pixels;
		}
		byte[] buffer = new byte[w * h * 4];
		bgra.CopyPixels(buffer, w * 4, 0);
		for (int i = 0; i + 3 < buffer.Length; i += 4)
		{
			if (buffer[i + 3] >= 128)
			{
				pixels.Add(new Rgba(buffer[i + 2], buffer[i + 1], buffer[i]));
			}
		}
		return pixels;
	}
}
