using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop.Effects;

// Статичные обои, нарисованные самой подложкой (чтобы их мог двигать параллакс и под частицами был фон).
// Рисуется один раз при смене размера — покадровой стоимости нет.
internal sealed class StaticWallpaperContent : FrameworkElement, IAnimatedContent
{
	private readonly BitmapSource? _bitmap;

	private readonly WallpaperStyle _style;

	private readonly SolidColorBrush _background;

	private readonly Rectangle _display;

	private readonly Rectangle _virtual;

	private readonly int _originalWidth;

	private readonly int _originalHeight;

	private readonly double _scale;

	private readonly ImageBrush? _tile;

	public FrameworkElement Element => this;

	public bool Paused { get; set; }

	public StaticWallpaperContent(LoadedWallpaper image, WallpaperInfo info, Rectangle display, Rectangle virtualScreen, double scale)
	{
		_bitmap = image.Bitmap;
		_style = info.Style;
		_originalWidth = image.OriginalWidth;
		_originalHeight = image.OriginalHeight;
		_display = display;
		_virtual = virtualScreen;
		_scale = scale <= 0.0 ? 1.0 : scale;
		_background = new SolidColorBrush(info.Background);
		_background.Freeze();
		IsHitTestVisible = false;
		if (_bitmap != null && _style == WallpaperStyle.Tile)
		{
			_tile = new ImageBrush(_bitmap)
			{
				TileMode = TileMode.Tile,
				ViewportUnits = BrushMappingMode.Absolute,
				Viewport = new Rect(0.0, 0.0, _originalWidth / _scale, _originalHeight / _scale),
				Stretch = Stretch.Fill
			};
			_tile.Freeze();
		}
	}

	public void WhenReady(Action ready)
	{
		ready();
	}

	protected override void OnRender(DrawingContext dc)
	{
		double w = ActualWidth;
		double h = ActualHeight;
		Rect bounds = new Rect(0.0, 0.0, w, h);
		dc.DrawRectangle(_background, null, bounds);
		if (_bitmap == null || _originalWidth <= 0 || _originalHeight <= 0 || w <= 0.0 || h <= 0.0)
		{
			return;
		}
		if (_tile != null)
		{
			dc.DrawRectangle(_tile, null, bounds);
			return;
		}
		double iw = _originalWidth / _scale;
		double ih = _originalHeight / _scale;
		Rect area = bounds;
		if (_style == WallpaperStyle.Span)
		{
			// Одна картинка на все мониторы: рисуем свою часть общего прямоугольника.
			area = new Rect((_virtual.X - _display.X) / _scale, (_virtual.Y - _display.Y) / _scale, _virtual.Width / _scale, _virtual.Height / _scale);
		}
		Rect target;
		switch (_style)
		{
		case WallpaperStyle.Stretch:
			target = area;
			break;
		case WallpaperStyle.Center:
			target = new Rect(area.X + (area.Width - iw) / 2.0, area.Y + (area.Height - ih) / 2.0, iw, ih);
			break;
		default:
		{
			double s = (_style == WallpaperStyle.Fit) ? Math.Min(area.Width / iw, area.Height / ih) : Math.Max(area.Width / iw, area.Height / ih);
			double tw = iw * s;
			double th = ih * s;
			target = new Rect(area.X + (area.Width - tw) / 2.0, area.Y + (area.Height - th) / 2.0, tw, th);
			break;
		}
		}
		dc.DrawImage(_bitmap, target);
	}

	public void Dispose()
	{
	}
}
