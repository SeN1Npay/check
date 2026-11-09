using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Metamorph.App.Desktop.Cards;

// Кольцо прогресса (0..1): дорожка + дуга с закруглёнными концами.
internal sealed class CardRing : FrameworkElement
{
	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CardRing), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

	private Pen? _trackPen;

	private Pen? _fillPen;

	private Brush _fill;

	private readonly Brush _track;

	private readonly double _thickness;

	public double Value
	{
		get => (double)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public CardRing(double size, double thickness, Brush track, Brush fill)
	{
		Width = size;
		Height = size;
		_thickness = thickness;
		_track = track;
		_fill = fill;
	}

	public void SetFill(Brush fill)
	{
		if (!ReferenceEquals(fill, _fill))
		{
			_fill = fill;
			_fillPen = null;
			InvalidateVisual();
		}
	}

	// Плавный переход; без анимации — когда рабочий стол неактивен или при переходе через ноль.
	public void Animate(double value, bool animate, double seconds = 0.6, bool linear = false)
	{
		value = Math.Clamp(double.IsFinite(value) ? value : 0.0, 0.0, 1.0);
		if (!animate || !Enabled)
		{
			BeginAnimation(ValueProperty, null);
			Value = value;
			return;
		}
		DoubleAnimation anim = new DoubleAnimation(value, TimeSpan.FromSeconds(seconds));
		if (!linear)
		{
			anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
		}
		anim.Freeze();
		BeginAnimation(ValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
	}

	protected override void OnRender(DrawingContext dc)
	{
		double size = Math.Min(ActualWidth, ActualHeight);
		if (size <= _thickness * 2.0)
		{
			return;
		}
		_trackPen ??= Frozen(new Pen(_track, _thickness));
		_fillPen ??= Frozen(new Pen(_fill, _thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
		double r = (size - _thickness) / 2.0;
		Point c = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
		dc.DrawEllipse(null, _trackPen, c, r, r);
		double v = Math.Clamp(Value, 0.0, 1.0);
		if (v <= 0.001)
		{
			return;
		}
		if (v >= 0.999)
		{
			dc.DrawEllipse(null, _fillPen, c, r, r);
			return;
		}
		double angle = v * 2.0 * Math.PI - Math.PI / 2.0;
		Point start = new Point(c.X, c.Y - r);
		Point end = new Point(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext ctx = g.Open())
		{
			ctx.BeginFigure(start, isFilled: false, isClosed: false);
			ctx.ArcTo(end, new Size(r, r), 0.0, v > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
		}
		g.Freeze();
		dc.DrawGeometry(null, _fillPen, g);
	}

	private static Pen Frozen(Pen p)
	{
		p.Freeze();
		return p;
	}
}

internal static class CardUi
{
	public static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null, FontFamily? font = null)
	{
		TextBlock t = new TextBlock
		{
			Text = text,
			FontSize = size,
			Foreground = brush,
			FontWeight = weight ?? FontWeights.Normal,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		if (font != null)
		{
			t.FontFamily = font;
		}
		return t;
	}

	// Круглая/скруглённая кнопка-иконка с подсветкой при наведении.
	public static Border IconButton(CardTheme theme, string glyph, Action click, double size = 32.0, bool primary = false, string? tooltip = null)
	{
		TextBlock icon = new TextBlock
		{
			Text = glyph,
			FontFamily = theme.Icons,
			FontSize = size * 0.42,
			Foreground = primary ? theme.OnAccent : theme.Foreground,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Border b = new Border
		{
			Width = size,
			Height = size,
			CornerRadius = new CornerRadius(size / 2.0),
			Background = primary ? theme.AccentGradient : Brushes.Transparent,
			Child = icon,
			Cursor = Cursors.Hand,
			ToolTip = tooltip
		};
		b.MouseEnter += delegate
		{
			if (primary)
			{
				b.Opacity = 0.88;
			}
			else
			{
				b.Background = theme.Hover;
			}
		};
		b.MouseLeave += delegate
		{
			b.Opacity = 1.0;
			if (!primary)
			{
				b.Background = Brushes.Transparent;
			}
		};
		b.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			try
			{
				click();
			}
			catch (Exception ex)
			{
				Metamorph.Core.Services.Log.Error("Карточки: кнопка", ex);
			}
		};
		return b;
	}

	public static void SetGlyph(Border button, string glyph)
	{
		if (button.Child is TextBlock t)
		{
			t.Text = glyph;
		}
	}
}

// Глифы Segoe MDL2 / Fluent Icons.
internal static class CardGlyphs
{
	public const string Play = "\uE768";

	public const string Pause = "\uE769";

	public const string Previous = "\uE892";

	public const string Next = "\uE893";

	public const string Reset = "\uE72C";

	public const string Close = "\uE711";

	public const string Check = "\uE73E";

	public const string Left = "\uE76B";

	public const string Right = "\uE76C";

	public const string Music = "\uE8D6";
}
