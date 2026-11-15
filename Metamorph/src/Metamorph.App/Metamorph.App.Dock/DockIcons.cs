using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Dock;

namespace Metamorph.App.Dock;

internal static class DockIcons
{
	private static readonly Dictionary<string, ImageSource?> Cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

	public static int PixelSize(double iconSize, double maxScale, double dpiScale)
	{
		double num = iconSize * Math.Max(1.0, maxScale) * Math.Max(1.0, dpiScale);
		int[] array = new int[6] { 32, 48, 64, 96, 128, 256 };
		foreach (int num2 in array)
		{
			if ((double)num2 >= num)
			{
				return num2;
			}
		}
		return 256;
	}

	public static ImageSource? FromShell(string parsingName, int pixelSize, bool useCache = true)
	{
		string key = parsingName + "|" + pixelSize;
		if (useCache && Cache.TryGetValue(key, out ImageSource value))
		{
			return value;
		}
		IconPixels iconPixels = ShellIcons.TryGetIcon(parsingName, pixelSize);
		BitmapSource bitmapSource = (((object)iconPixels == null) ? null : ToBitmap(iconPixels));
		Cache[key] = bitmapSource;
		return bitmapSource;
	}

	public static BitmapSource ToBitmap(IconPixels p)
	{
		PixelFormat pixelFormat = (p.Premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32);
		BitmapSource bitmapSource = BitmapSource.Create(p.Width, p.Height, 96.0, 96.0, pixelFormat, null, p.Bgra, p.Stride);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	public static ImageSource? FromHIcon(nint hicon)
	{
		if (hicon == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(hicon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bitmapSource.Freeze();
			return bitmapSource;
		}
		catch
		{
			return null;
		}
	}

	public static ImageSource AppsButton(Color from, Color to, Color dots)
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		LinearGradientBrush brush = new LinearGradientBrush(from, to, new Point(0.0, 0.0), new Point(1.0, 1.0));
		drawingGroup.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0.0, 0.0, 100.0, 100.0), 24.0, 24.0)));
		SolidColorBrush brush2 = new SolidColorBrush(dots);
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				Rect rect = new Rect(24 + j * 20, 24 + i * 20, 12.0, 12.0);
				drawingGroup.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(rect, 3.5, 3.5)));
			}
		}
		DrawingImage drawingImage = new DrawingImage(drawingGroup);
		drawingImage.Freeze();
		return drawingImage;
	}

	public static ImageSource Placeholder(string name, Color background, Color foreground)
	{
		string textToFormat = (string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpper(CultureInfo.CurrentCulture));
		DrawingGroup drawingGroup = new DrawingGroup
		{
			Children = { (Drawing)new GeometryDrawing(new SolidColorBrush(background), null, new RectangleGeometry(new Rect(0.0, 0.0, 100.0, 100.0), 24.0, 24.0)) }
		};
		FormattedText formattedText = new FormattedText(textToFormat, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 56.0, new SolidColorBrush(foreground), 1.0);
		Geometry geometry = formattedText.BuildGeometry(new Point((100.0 - formattedText.Width) / 2.0, (100.0 - formattedText.Height) / 2.0));
		drawingGroup.Children.Add(new GeometryDrawing(new SolidColorBrush(foreground), null, geometry));
		DrawingImage drawingImage = new DrawingImage(drawingGroup);
		drawingImage.Freeze();
		return drawingImage;
	}

	public static ImageSource Trash(Color body, Color lid)
	{
		DrawingGroup obj = new DrawingGroup
		{
			Children = { (Drawing)new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0.0, 0.0, 100.0, 100.0))) }
		};
		SolidColorBrush brush = new SolidColorBrush(body);
		SolidColorBrush brush2 = new SolidColorBrush(lid);
		obj.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(18.0, 16.0, 64.0, 9.0), 4.0, 4.0)));
		obj.Children.Add(new GeometryDrawing(brush2, null, new RectangleGeometry(new Rect(40.0, 8.0, 20.0, 10.0), 3.0, 3.0)));
		Geometry geometry = Geometry.Parse("M 24,30 L 76,30 L 70,92 L 30,92 Z");
		obj.Children.Add(new GeometryDrawing(brush, null, geometry));
		DrawingImage drawingImage = new DrawingImage(obj);
		drawingImage.Freeze();
		return drawingImage;
	}
}
