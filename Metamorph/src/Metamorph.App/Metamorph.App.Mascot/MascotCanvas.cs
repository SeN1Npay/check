using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Mascot;

internal sealed class MascotCanvas : FrameworkElement
{
	private const int CacheLimit = 6000;

	private static readonly Dictionary<string, Geometry> Geometries = new Dictionary<string, Geometry>();

	private static readonly Dictionary<string, Brush> Brushes = new Dictionary<string, Brush>();

	private static readonly Dictionary<(string, double), Pen> Pens = new Dictionary<(string, double), Pen>();

	private List<DrawItem>? _items;

	private Point _shadow;

	private double _shadowWidth;

	private double _shadowAlpha;

	public void SetItems(List<DrawItem> items)
	{
		_items = items;
		InvalidateVisual();
	}

	public void SetShadow(Point center, double width, double alpha)
	{
		_shadow = center;
		_shadowWidth = width;
		_shadowAlpha = Math.Clamp(alpha, 0.0, 1.0);
	}

	protected override void OnRender(DrawingContext dc)
	{
		if (_shadowAlpha > 0.02 && _shadowWidth > 1.0)
		{
			dc.PushOpacity(0.28 * _shadowAlpha);
			dc.DrawEllipse(Brush("#000000"), null, _shadow, _shadowWidth / 2.0, _shadowWidth / 9.0);
			dc.Pop();
		}
		if (_items == null)
		{
			return;
		}
		foreach (DrawItem item in _items)
		{
			Geometry geometry = Geo(item.Path);
			if (geometry != null)
			{
				Affine transform = item.Transform;
				dc.PushTransform(new MatrixTransform(transform.A, transform.B, transform.C, transform.D, transform.E, transform.F));
				bool num = item.Opacity < 0.999;
				if (num)
				{
					dc.PushOpacity(Math.Clamp(item.Opacity, 0.0, 1.0));
				}
				dc.DrawGeometry((item.Fill == null) ? null : Brush(item.Fill), (item.Stroke == null) ? null : Pen(item.Stroke, item.StrokeWidth), geometry);
				if (num)
				{
					dc.Pop();
				}
				dc.Pop();
			}
		}
	}

	private static Geometry? Geo(string path)
	{
		if (Geometries.TryGetValue(path, out Geometry value))
		{
			return value;
		}
		if (Geometries.Count > 6000)
		{
			Geometries.Clear();
		}
		try
		{
			value = Geometry.Parse("F1 " + path);
			value.Freeze();
		}
		catch
		{
			value = null;
		}
		Geometries[path] = value;
		return value;
	}

	private static Brush Brush(string color)
	{
		if (Brushes.TryGetValue(color, out Brush value))
		{
			return value;
		}
		try
		{
			value = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
		}
		catch
		{
			value = new SolidColorBrush(Colors.HotPink);
		}
		value.Freeze();
		if (Brushes.Count > 400)
		{
			Brushes.Clear();
		}
		return Brushes[color] = value;
	}

	private static Pen Pen(string color, double width)
	{
		(string, double) key = (color, Math.Round(width, 2));
		if (Pens.TryGetValue(key, out Pen value))
		{
			return value;
		}
		value = new Pen(Brush(color), width)
		{
			LineJoin = PenLineJoin.Round,
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		};
		value.Freeze();
		if (Pens.Count > 400)
		{
			Pens.Clear();
		}
		return Pens[key] = value;
	}
}
