using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;

namespace Metamorph.App.Infrastructure;

internal static class BrushFactory
{
	public static Color ToColor(string hex, double opacity = 1.0)
	{
		Rgba color;
		Rgba rgba = (Rgba.TryParse(hex, out color) ? color : new Rgba(byte.MaxValue, 0, byte.MaxValue));
		return Color.FromArgb((byte)Math.Round((double)(int)rgba.A * Math.Clamp(opacity, 0.0, 1.0)), rgba.R, rgba.G, rgba.B);
	}

	public static SolidColorBrush Solid(string hex, double opacity = 1.0)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(ToColor(hex, opacity));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	public static Brush Wallpaper(ThemeProfile p, string? imagePath)
	{
		WallpaperSettings wallpaper = p.Wallpaper;
		int num;
		switch (wallpaper.Kind)
		{
		case WallpaperKind.Image:
			num = 0;
			goto IL_003d;
		case WallpaperKind.Live:
			num = 1;
			goto IL_003d;
		case WallpaperKind.Solid:
			return Solid(wallpaper.From);
		case WallpaperKind.Gradient:
		case WallpaperKind.Video:
			{
				object obj2 = wallpaper.Direction switch
				{
					GradientDirection.Horizontal => new LinearGradientBrush(ToColor(wallpaper.From), ToColor(wallpaper.To), new Point(0.0, 0.5), new Point(1.0, 0.5)), 
					GradientDirection.Diagonal => new LinearGradientBrush(ToColor(wallpaper.From), ToColor(wallpaper.To), new Point(0.0, 0.0), new Point(1.0, 1.0)), 
					GradientDirection.Radial => new RadialGradientBrush(ToColor(wallpaper.From), ToColor(wallpaper.To))
					{
						RadiusX = 0.75,
						RadiusY = 0.75
					}, 
					_ => new LinearGradientBrush(ToColor(wallpaper.From), ToColor(wallpaper.To), 90.0), 
				};
				((Freezable)obj2).Freeze();
				return (Brush)obj2;
			}
			IL_003d:
			if (imagePath == null || !File.Exists(imagePath))
			{
				if (num == 0)
				{
					break;
				}
				if (num == 1)
				{
					goto case WallpaperKind.Gradient;
				}
			}
			try
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(imagePath);
				bitmapImage.DecodePixelWidth = 640;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				ImageBrush imageBrush = new ImageBrush(bitmapImage);
				imageBrush.Stretch = Stretch.UniformToFill;
				imageBrush.Freeze();
				return imageBrush;
			}
			catch
			{
				return Solid(p.Palette.Background);
			}
		}
		return Solid(p.Palette.Background);
	}
}
