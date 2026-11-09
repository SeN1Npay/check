using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Dock;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal static class PosterMaker
{
	public static string? Ensure(WallpaperService wallpapers, string mediaPath)
	{
		try
		{
			if (!WallpaperService.IsAnimated(mediaPath) || !File.Exists(mediaPath))
			{
				return null;
			}
			string text = wallpapers.PosterPath(mediaPath);
			if (text == null)
			{
				return null;
			}
			if (File.Exists(text))
			{
				return text;
			}
			BitmapSource bitmapSource = (GifAnimation.IsGif(mediaPath) ? GifAnimation.FirstFrame(mediaPath) : Thumbnail(mediaPath));
			if (bitmapSource == null)
			{
				return null;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			string text2 = text + ".tmp";
			using (FileStream stream = File.Create(text2))
			{
				PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
				pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
				pngBitmapEncoder.Save(stream);
			}
			File.Move(text2, text, overwrite: true);
			EventTrace.Mark("wallpaper.poster", Path.GetFileName(mediaPath));
			return text;
		}
		catch (Exception ex)
		{
			Log.Warn("Постер обоев " + mediaPath + ": " + ex.Message);
			return null;
		}
	}

	private static BitmapSource? Thumbnail(string path)
	{
		int size = Math.Clamp((from d in Display.All()
			select d.Bounds.Width).DefaultIfEmpty(1920).Max(), 640, 2560);
		IconPixels iconPixels = ShellIcons.TryGetThumbnail(path, size);
		if ((object)iconPixels == null)
		{
			return null;
		}
		BitmapSource bitmapSource = BitmapSource.Create(iconPixels.Width, iconPixels.Height, 96.0, 96.0, iconPixels.Premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32, null, iconPixels.Bgra, iconPixels.Stride);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	public static ImageSource? Load(string? posterPath)
	{
		try
		{
			if (posterPath == null || !File.Exists(posterPath))
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.UriSource = new Uri(posterPath, UriKind.Absolute);
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	public static ImageSource? Tile(string path, int width)
	{
		try
		{
			if (Directory.Exists(path))
			{
				return null;
			}
			if (GifAnimation.IsGif(path))
			{
				return Scale(GifAnimation.FirstFrame(path), width);
			}
			if (WallpaperService.VideoExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
			{
				IconPixels iconPixels = ShellIcons.TryGetThumbnail(path, width);
				if ((object)iconPixels == null)
				{
					return null;
				}
				BitmapSource bitmapSource = BitmapSource.Create(iconPixels.Width, iconPixels.Height, 96.0, 96.0, iconPixels.Premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32, null, iconPixels.Bgra, iconPixels.Stride);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = width;
			bitmapImage.UriSource = new Uri(path, UriKind.Absolute);
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? Scale(BitmapSource? source, int width)
	{
		if (source == null || source.PixelWidth <= width)
		{
			return source;
		}
		double num = (double)width / (double)source.PixelWidth;
		TransformedBitmap transformedBitmap = new TransformedBitmap(source, new ScaleTransform(num, num));
		transformedBitmap.Freeze();
		return transformedBitmap;
	}
}
