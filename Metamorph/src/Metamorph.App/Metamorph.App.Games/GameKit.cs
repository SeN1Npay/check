using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Games;

// Цвета игр из палитры активной темы. Всё заморожено и создаётся один раз на окно.
internal sealed class GamePalette
{
	public Color Bg { get; }

	public Color Surface { get; }

	public Color Fg { get; }

	public Color Muted { get; }

	public Color Accent { get; }

	public Color Accent2 { get; }

	public Color Red { get; }

	public Color Green { get; }

	public Color Yellow { get; }

	public bool IsDark { get; }

	// Цвета фигур в порядке TetrominoKind: I, O, T, S, Z, J, L.
	public Color[] Pieces { get; }

	public Palette Source { get; }

	public GamePalette(Palette p)
	{
		Source = p;
		Bg = BrushFactory.ToColor(p.Background);
		Surface = BrushFactory.ToColor(p.Surface);
		Fg = BrushFactory.ToColor(p.Foreground);
		Muted = BrushFactory.ToColor(p.Muted);
		Accent = BrushFactory.ToColor(p.Accent);
		Accent2 = BrushFactory.ToColor(p.Accent2);
		Red = BrushFactory.ToColor(p.Red);
		Green = BrushFactory.ToColor(p.Green);
		Yellow = BrushFactory.ToColor(p.Yellow);
		IsDark = Luma(Bg) < 0.5;
		Color orange = Vivid(Mix(Yellow, Red, 0.45));
		Color blue = Vivid(HueShift(Accent2, 38.0));
		Pieces = new Color[7]
		{
			Vivid(Accent2),
			Vivid(Yellow),
			Vivid(Accent),
			Vivid(Green),
			Vivid(Red),
			Distinct(blue, Accent2, Accent),
			Distinct(orange, Yellow, Red)
		};
	}

	public static double Luma(Color c)
	{
		return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
	}

	public static Color Mix(Color a, Color b, double t)
	{
		t = Math.Clamp(t, 0.0, 1.0);
		return Color.FromArgb((byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
	}

	public static Color Lighten(Color c, double t)
	{
		return Mix(c, Color.FromArgb(c.A, 255, 255, 255), t);
	}

	public static Color Darken(Color c, double t)
	{
		return Mix(c, Color.FromArgb(c.A, 0, 0, 0), t);
	}

	public static Color WithAlpha(Color c, double a)
	{
		return Color.FromArgb((byte)Math.Round(255.0 * Math.Clamp(a, 0.0, 1.0)), c.R, c.G, c.B);
	}

	public static Color HueShift(Color c, double degrees)
	{
		Hsl hsl = ColorMath.ToHsl(new Rgba(c.R, c.G, c.B));
		Rgba r = ColorMath.FromHsl(hsl with { H = hsl.H + degrees });
		return Color.FromRgb(r.R, r.G, r.B);
	}

	// Слишком серые/тёмные цвета темы плохо читаются как фигуры — подтягиваем насыщенность и яркость.
	public static Color Vivid(Color c)
	{
		Hsl hsl = ColorMath.ToHsl(new Rgba(c.R, c.G, c.B));
		Rgba r = ColorMath.FromHsl(new Hsl(hsl.H, Math.Max(hsl.S, 0.45), Math.Clamp(hsl.L, 0.42, 0.72)));
		return Color.FromRgb(r.R, r.G, r.B);
	}

	private static Color Distinct(Color c, Color a, Color b)
	{
		double Hue(Color x) => ColorMath.ToHsl(new Rgba(x.R, x.G, x.B)).H;
		double h = Hue(c);
		if (ColorMath.HueDistance(h, Hue(a)) < 22.0 || ColorMath.HueDistance(h, Hue(b)) < 22.0)
		{
			c = HueShift(c, 35.0);
		}
		return Vivid(c);
	}

	public static SolidColorBrush Solid(Color c, double opacity = 1.0)
	{
		SolidColorBrush b = new SolidColorBrush(Color.FromArgb((byte)Math.Round(c.A * Math.Clamp(opacity, 0.0, 1.0)), c.R, c.G, c.B));
		b.Freeze();
		return b;
	}

	public static LinearGradientBrush Vertical(params (Color Color, double Offset)[] stops)
	{
		GradientStopCollection list = new GradientStopCollection(stops.Length);
		foreach ((Color color, double offset) in stops)
		{
			list.Add(new GradientStop(color, offset));
		}
		LinearGradientBrush b = new LinearGradientBrush(list, new Point(0.5, 0.0), new Point(0.5, 1.0));
		b.Freeze();
		return b;
	}

	public static Pen Pen(Color c, double width, double opacity = 1.0)
	{
		Pen p = new Pen(Solid(c, opacity), width)
		{
			LineJoin = PenLineJoin.Round,
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		};
		p.Freeze();
		return p;
	}
}

// Кадровый цикл на CompositionTarget.Rendering: подписка только пока идёт игра.
internal sealed class GameLoop
{
	private readonly Action<double> _tick;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private double _last;

	private TimeSpan _lastRender;

	public bool Running { get; private set; }

	public double Now => _clock.Elapsed.TotalSeconds;

	public GameLoop(Action<double> tick)
	{
		_tick = tick;
	}

	public void Start()
	{
		if (!Running)
		{
			Running = true;
			_last = Now;
			CompositionTarget.Rendering += OnRendering;
		}
	}

	public void Stop()
	{
		if (Running)
		{
			Running = false;
			CompositionTarget.Rendering -= OnRendering;
		}
	}

	private void OnRendering(object? sender, EventArgs e)
	{
		// Rendering может прийти несколько раз за кадр — считаем только новый кадр.
		if (e is RenderingEventArgs r)
		{
			if (r.RenderingTime == _lastRender)
			{
				return;
			}
			_lastRender = r.RenderingTime;
		}
		double now = Now;
		double dt = Math.Clamp(now - _last, 0.0, 0.1);
		_last = now;
		try
		{
			_tick(dt);
		}
		catch (Exception ex)
		{
			Log.Error("Игры: ошибка кадра", ex);
			Stop();
		}
	}
}

// Холст, который рисует делегатом владельца.
internal sealed class DrawSurface : FrameworkElement
{
	private readonly Action<DrawingContext, double, double> _draw;

	public DrawSurface(Action<DrawingContext, double, double> draw)
	{
		_draw = draw;
		IsHitTestVisible = false;
		SnapsToDevicePixels = true;
	}

	protected override void OnRender(DrawingContext dc)
	{
		double w = ActualWidth;
		double h = ActualHeight;
		if (w < 4.0 || h < 4.0)
		{
			return;
		}
		try
		{
			_draw(dc, w, h);
		}
		catch (Exception ex)
		{
			Log.Error("Игры: ошибка отрисовки", ex);
		}
	}
}

// Кэш FormattedText: пересоздаётся только при смене текста/размера/кисти.
internal sealed class TextSlot
{
	public static readonly Typeface Ui = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

	public static readonly Typeface Heavy = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

	public static readonly Typeface Mono = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

	private readonly Typeface _face;

	private string? _text;

	private long _number = long.MinValue;

	private string? _format;

	private double _size;

	private Brush? _brush;

	private double _ppd;

	private double _maxWidth;

	private FormattedText? _ft;

	public TextSlot(Typeface face)
	{
		_face = face;
	}

	public FormattedText Get(string text, double size, Brush brush, double ppd, double maxWidth = 0.0)
	{
		if (_ft == null || !ReferenceEquals(text, _text) && text != _text || size != _size || brush != _brush || ppd != _ppd || maxWidth != _maxWidth)
		{
			_text = text;
			_number = long.MinValue;
			Build(text, size, brush, ppd, maxWidth);
		}
		return _ft!;
	}

	public FormattedText Number(long value, string format, double size, Brush brush, double ppd)
	{
		if (_ft == null || value != _number || format != _format || size != _size || brush != _brush || ppd != _ppd)
		{
			_number = value;
			_format = format;
			_text = null;
			Build(value.ToString(format, CultureInfo.InvariantCulture), size, brush, ppd, 0.0);
		}
		return _ft!;
	}

	private void Build(string text, double size, Brush brush, double ppd, double maxWidth)
	{
		_size = size;
		_brush = brush;
		_ppd = ppd;
		_maxWidth = maxWidth;
		_ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, _face, Math.Max(1.0, size), brush, ppd);
		if (maxWidth > 0.0)
		{
			_ft.MaxTextWidth = maxWidth;
			_ft.TextAlignment = TextAlignment.Center;
		}
	}
}

// Пул трансформаций: у каждого объекта кадра своя MatrixTransform, новые создаются только при росте пула.
internal sealed class TransformPool
{
	private readonly List<MatrixTransform> _items = new List<MatrixTransform>();

	private int _index;

	public void Reset()
	{
		_index = 0;
	}

	public MatrixTransform Next(double m11, double m12, double m21, double m22, double dx, double dy)
	{
		if (_index == _items.Count)
		{
			_items.Add(new MatrixTransform());
		}
		MatrixTransform t = _items[_index++];
		t.Matrix = new Matrix(m11, m12, m21, m22, dx, dy);
		return t;
	}

	public MatrixTransform Next(double scale, double dx, double dy)
	{
		return Next(scale, 0.0, 0.0, scale, dx, dy);
	}
}

internal static class GameDraw
{
	// Облачко реплики над точкой (tipX, tipY) — хвостик смотрит вниз.
	public static void Bubble(DrawingContext dc, FormattedText text, double tipX, double tipY, Brush fill, Pen edge, double opacity, double minX, double maxX)
	{
		if (opacity <= 0.01)
		{
			return;
		}
		double padX = 12.0;
		double padY = 8.0;
		double w = text.Width + padX * 2.0;
		double h = text.Height + padY * 2.0;
		double x = Math.Clamp(tipX - w / 2.0, minX, Math.Max(minX, maxX - w));
		double y = tipY - h - 10.0;
		dc.PushOpacity(opacity);
		dc.DrawRoundedRectangle(fill, edge, new Rect(x, y, w, h), 12.0, 12.0);
		double tx = Math.Clamp(tipX, x + 16.0, x + w - 16.0);
		dc.DrawGeometry(fill, null, Tail(tx, y + h - 1.0));
		dc.DrawText(text, new Point(x + padX + (text.MaxTextWidth > 0.0 ? (text.Width - text.MaxTextWidth) / 2.0 : 0.0), y + padY));
		dc.Pop();
	}

	private static readonly Dictionary<(int, int), Geometry> Tails = new Dictionary<(int, int), Geometry>();

	private static Geometry Tail(double x, double y)
	{
		(int, int) key = ((int)Math.Round(x), (int)Math.Round(y));
		if (Tails.TryGetValue(key, out Geometry? g))
		{
			return g;
		}
		if (Tails.Count > 256)
		{
			Tails.Clear();
		}
		StreamGeometry s = new StreamGeometry();
		using (StreamGeometryContext c = s.Open())
		{
			c.BeginFigure(new Point(key.Item1 - 8, key.Item2), isFilled: true, isClosed: true);
			c.LineTo(new Point(key.Item1 + 2, key.Item2 + 11), isStroked: false, isSmoothJoin: false);
			c.LineTo(new Point(key.Item1 + 8, key.Item2), isStroked: false, isSmoothJoin: false);
		}
		s.Freeze();
		Tails[key] = s;
		return s;
	}
}
