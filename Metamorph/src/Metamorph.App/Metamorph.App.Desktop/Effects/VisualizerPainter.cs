using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Effects;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop.Effects;

// Рисует визуализатор в DrawingContext. Ресурсы (градиенты, перья) готовятся при смене цветов/размера; кадр без выделений.
internal sealed class VisualizerPainter
{
	private readonly DesktopVisualizerSettings _s;

	private readonly int _bars;

	private Brush _fill = Brushes.White;

	private Brush _peak = Brushes.White;

	private Pen _line = new Pen(Brushes.White, 1.0);

	private Pen _glow = new Pen(Brushes.White, 1.0);

	private Pen _ringPen = new Pen(Brushes.White, 1.0);

	private Pen _ringBase = new Pen(Brushes.White, 1.0);

	private double _left;

	private double _width;

	private double _baseline;

	private double _height;

	private double _barWidth;

	private double _slot;

	private Point _center;

	private double _radius;

	private bool _middle;

	public VisualizerBands Bands { get; }

	public VisualizerPainter(DesktopVisualizerSettings settings)
	{
		_s = settings;
		_bars = Math.Clamp(settings.Bars, 8, 160);
		int bands = settings.Style switch
		{
			VisualizerStyle.Mirror => (_bars + 1) / 2,
			VisualizerStyle.Ring => (_bars + 1) / 2,
			_ => _bars,
		};
		Bands = new VisualizerBands(bands);
	}

	// surface и workArea — в локальных DIP подложки.
	public void Layout(Size surface, Rect workArea, EffectColors colors)
	{
		_height = Math.Clamp(double.IsFinite(_s.Height) ? _s.Height : 140.0, 30.0, surface.Height * 0.6);
		_middle = _s.Placement != VisualizerPlacement.Bottom;
		_width = surface.Width * (_middle ? 0.55 : 0.7);
		_left = (surface.Width - _width) / 2.0;
		_baseline = _middle ? (surface.Height / 2.0) : (Math.Min(surface.Height, workArea.Bottom) - 18.0);
		_slot = _width / _bars;
		_barWidth = Math.Max(1.5, _slot * 0.62);
		_center = new Point(surface.Width / 2.0, surface.Height / 2.0);
		_radius = Math.Clamp(_height * 0.75, 50.0, Math.Min(surface.Width, surface.Height) * 0.28);
		Color a = colors.Accent;
		Color b = _s.Gradient ? colors.Accent2 : colors.Accent;
		LinearGradientBrush horizontal = new LinearGradientBrush(a, b, new Point(_left, 0.0), new Point(_left + _width, 0.0))
		{
			MappingMode = BrushMappingMode.Absolute
		};
		horizontal.Freeze();
		_fill = horizontal;
		SolidColorBrush peak = new SolidColorBrush(EffectColors.Mix(b, Colors.White, 0.45));
		peak.Freeze();
		_peak = peak;
		_line = Freeze(new Pen(horizontal, 3.0)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round,
			LineJoin = PenLineJoin.Round
		});
		LinearGradientBrush glowBrush = horizontal.Clone();
		glowBrush.Opacity = 0.28;
		glowBrush.Freeze();
		_glow = Freeze(new Pen(glowBrush, 10.0)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round,
			LineJoin = PenLineJoin.Round
		});
		double outer = _radius + _height * 0.6;
		LinearGradientBrush vertical = new LinearGradientBrush(a, b, new Point(0.0, _center.Y - outer), new Point(0.0, _center.Y + outer))
		{
			MappingMode = BrushMappingMode.Absolute
		};
		vertical.Freeze();
		double thickness = Math.Clamp(Math.PI * 2.0 * _radius / _bars * 0.55, 1.5, 12.0);
		_ringPen = Freeze(new Pen(vertical, thickness)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		});
		LinearGradientBrush ringBaseBrush = vertical.Clone();
		ringBaseBrush.Opacity = 0.35;
		ringBaseBrush.Freeze();
		_ringBase = Freeze(new Pen(ringBaseBrush, 1.5));
	}

	private static Pen Freeze(Pen pen)
	{
		pen.Freeze();
		return pen;
	}

	public void Draw(DrawingContext dc, double fade)
	{
		double opacity = Math.Clamp(_s.Opacity, 0.05, 1.0) * fade;
		dc.PushOpacity(opacity);
		switch (_s.Style)
		{
		case VisualizerStyle.Mirror:
			DrawBars(dc, mirror: true);
			break;
		case VisualizerStyle.Wave:
			DrawWave(dc);
			break;
		case VisualizerStyle.Ring:
			DrawRing(dc);
			break;
		default:
			DrawBars(dc, mirror: false);
			break;
		}
		dc.Pop();
	}

	private float ValueAt(int bar, bool mirror, out float peak)
	{
		int i = bar;
		if (mirror)
		{
			// Низкие частоты в центре, расходятся к краям.
			int half = Bands.Count;
			int center = _bars / 2;
			i = (bar >= center) ? (bar - center) : (center - 1 - bar);
			i = Math.Clamp(i, 0, half - 1);
		}
		peak = Bands.Peaks[i];
		return Bands.Values[i];
	}

	private void DrawBars(DrawingContext dc, bool mirror)
	{
		double r = _barWidth / 2.0;
		for (int b = 0; b < _bars; b++)
		{
			float peak;
			float v = ValueAt(b, mirror, out peak);
			double x = _left + b * _slot + (_slot - _barWidth) / 2.0;
			double h = Math.Max(_barWidth, v * _height);
			double ph = peak * _height;
			if (_middle)
			{
				dc.DrawRoundedRectangle(_fill, null, new Rect(x, _baseline - h / 2.0, _barWidth, h), r, r);
				if (ph > _barWidth)
				{
					dc.DrawRoundedRectangle(_peak, null, new Rect(x, _baseline - ph / 2.0 - 5.0, _barWidth, 2.5), 1.25, 1.25);
					dc.DrawRoundedRectangle(_peak, null, new Rect(x, _baseline + ph / 2.0 + 2.5, _barWidth, 2.5), 1.25, 1.25);
				}
			}
			else
			{
				dc.DrawRoundedRectangle(_fill, null, new Rect(x, _baseline - h, _barWidth, h), r, r);
				if (ph > _barWidth)
				{
					dc.DrawRoundedRectangle(_peak, null, new Rect(x, _baseline - ph - 5.0, _barWidth, 2.5), 1.25, 1.25);
				}
			}
		}
	}

	private Point WavePoint(int i, double sign)
	{
		int n = Bands.Count;
		i = Math.Clamp(i, 0, n - 1);
		double x = _left + (n == 1 ? 0.5 : (double)i / (n - 1)) * _width;
		double h = Bands.Values[i] * _height * (_middle ? 0.5 : 1.0);
		return new Point(x, _baseline - sign * h);
	}

	private void DrawWave(DrawingContext dc)
	{
		DrawWaveLine(dc, _glow, 1.0);
		DrawWaveLine(dc, _line, 1.0);
		if (_middle)
		{
			DrawWaveLine(dc, _glow, -1.0);
			DrawWaveLine(dc, _line, -1.0);
		}
	}

	// Сглаженная кривая (Catmull-Rom) отрезками с круглыми концами — без StreamGeometry на кадр.
	private void DrawWaveLine(DrawingContext dc, Pen pen, double sign)
	{
		const int Sub = 4;
		int n = Bands.Count;
		Point prev = WavePoint(0, sign);
		for (int i = 0; i < n - 1; i++)
		{
			Point p0 = WavePoint(i - 1, sign);
			Point p1 = WavePoint(i, sign);
			Point p2 = WavePoint(i + 1, sign);
			Point p3 = WavePoint(i + 2, sign);
			for (int s = 1; s <= Sub; s++)
			{
				double t = (double)s / Sub;
				double t2 = t * t;
				double t3 = t2 * t;
				double y = 0.5 * (2.0 * p1.Y + (p2.Y - p0.Y) * t + (2.0 * p0.Y - 5.0 * p1.Y + 4.0 * p2.Y - p3.Y) * t2 + (3.0 * p1.Y - p0.Y - 3.0 * p2.Y + p3.Y) * t3);
				Point cur = new Point(p1.X + (p2.X - p1.X) * t, y);
				dc.DrawLine(pen, prev, cur);
				prev = cur;
			}
		}
	}

	private void DrawRing(DrawingContext dc)
	{
		dc.DrawEllipse(null, _ringBase, _center, _radius - 4.0, _radius - 4.0);
		double len = _height * 0.6;
		for (int b = 0; b < _bars; b++)
		{
			// Симметрично слева/справа: низкие частоты сверху.
			int half = Bands.Count;
			int i = (b <= _bars / 2) ? b : (_bars - b);
			i = Math.Clamp(i, 0, half - 1);
			double angle = -Math.PI / 2.0 + Math.PI * 2.0 * b / _bars;
			double cos = Math.Cos(angle);
			double sin = Math.Sin(angle);
			double l = Math.Max(2.0, Bands.Values[i] * len);
			Point from = new Point(_center.X + cos * _radius, _center.Y + sin * _radius);
			Point to = new Point(_center.X + cos * (_radius + l), _center.Y + sin * (_radius + l));
			dc.DrawLine(_ringPen, from, to);
			double pr = _radius + Bands.Peaks[i] * len + 6.0;
			if (Bands.Peaks[i] * len > 4.0)
			{
				dc.DrawEllipse(_peak, null, new Point(_center.X + cos * pr, _center.Y + sin * pr), 1.6, 1.6);
			}
		}
	}
}
