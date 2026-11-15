using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Colors;
using Metamorph.Core.Effects;
using Metamorph.Core.Models;
using Metamorph.App.Infrastructure;

namespace Metamorph.App.Desktop.Effects;

// Цвета эффектов: из палитры темы, акцент можно заменить акцентом обоев.
internal sealed record EffectColors(Color Accent, Color Accent2, Color Foreground, Color Red, Color Yellow, Color Green)
{
	public static EffectColors From(Palette p)
	{
		return new EffectColors(BrushFactory.ToColor(p.Accent), BrushFactory.ToColor(p.Accent2), BrushFactory.ToColor(p.Foreground), BrushFactory.ToColor(p.Red), BrushFactory.ToColor(p.Yellow), BrushFactory.ToColor(p.Green));
	}

	public EffectColors WithAccent(Color accent)
	{
		Rgba second = WallpaperAccentPicker.Secondary(ToRgba(accent));
		return this with
		{
			Accent = accent,
			Accent2 = Color.FromRgb(second.R, second.G, second.B)
		};
	}

	public static Rgba ToRgba(Color c)
	{
		return new Rgba(c.R, c.G, c.B);
	}

	public static Color Mix(Color a, Color b, double f)
	{
		Rgba m = WallpaperAccentPicker.Mix(ToRgba(a), ToRgba(b), f);
		return Color.FromRgb(m.R, m.G, m.B);
	}
}

// Замороженные кисти/картинки для частиц: всё готовится один раз, в кадре только выбор из массивов.
internal sealed class ParticlePaint
{
	public const int Levels = 16;

	public const int AngleSteps = 36;

	private static readonly Color White = Colors.White;

	// [вариант][уровень альфы]
	public readonly Brush[][] Solid;

	public readonly Brush[][] Glow;

	public readonly Pen[][] Line;

	// [вариант][шаг угла] — лепестки и листья.
	public readonly ImageSource[][]? Shapes;

	public readonly Pen TrailRight;

	public readonly Pen TrailLeft;

	public readonly Brush Head;

	public ParticleKind Kind { get; }

	public ParticlePaint(ParticleKind kind, EffectColors c)
	{
		Kind = kind;
		Color[] variants = VariantColors(kind, c);
		Solid = new Brush[variants.Length][];
		Glow = new Brush[variants.Length][];
		Line = new Pen[variants.Length][];
		for (int v = 0; v < variants.Length; v++)
		{
			Solid[v] = new Brush[Levels];
			Glow[v] = new Brush[Levels];
			Line[v] = new Pen[Levels];
			for (int i = 0; i < Levels; i++)
			{
				double a = (double)(i + 1) / Levels;
				Color col = WithAlpha(variants[v], a);
				SolidColorBrush solid = new SolidColorBrush(col);
				solid.Freeze();
				Solid[v][i] = solid;
				RadialGradientBrush glow = new RadialGradientBrush
				{
					GradientStops = new GradientStopCollection
					{
						new GradientStop(col, 0.0),
						new GradientStop(WithAlpha(variants[v], a * 0.55), 0.3),
						new GradientStop(WithAlpha(variants[v], 0.0), 1.0)
					}
				};
				glow.Freeze();
				Glow[v][i] = glow;
				Pen pen = new Pen(solid, kind == ParticleKind.Bubbles ? 1.0 : 1.3)
				{
					StartLineCap = PenLineCap.Round,
					EndLineCap = PenLineCap.Round
				};
				pen.Freeze();
				Line[v][i] = pen;
			}
		}
		if (kind == ParticleKind.Sakura || kind == ParticleKind.Leaves)
		{
			Shapes = new ImageSource[variants.Length][];
			Geometry shape = kind == ParticleKind.Sakura ? Petal() : Leaf();
			Geometry vein = kind == ParticleKind.Leaves ? LeafVein() : null;
			for (int v = 0; v < variants.Length; v++)
			{
				Shapes[v] = new ImageSource[AngleSteps];
				Brush fill = Fill(kind, variants[v]);
				Pen veinPen = null;
				if (vein != null)
				{
					veinPen = new Pen(new SolidColorBrush(EffectColors.Mix(variants[v], Colors.Black, 0.35)), 0.05);
					veinPen.Freeze();
				}
				for (int s = 0; s < AngleSteps; s++)
				{
					Shapes[v][s] = Shape(shape, vein, fill, veinPen, 360.0 * s / AngleSteps);
				}
			}
		}
		TrailRight = TrailPen(new Point(0.0, 0.0), new Point(1.0, 1.0));
		TrailLeft = TrailPen(new Point(1.0, 0.0), new Point(0.0, 1.0));
		SolidColorBrush head = new SolidColorBrush(White);
		head.Freeze();
		Head = head;
	}

	public static int Level(float alpha)
	{
		int i = (int)(alpha * Levels) - 1;
		if (i < 0)
		{
			return 0;
		}
		if (i >= Levels)
		{
			return Levels - 1;
		}
		return i;
	}

	public static int AngleIndex(float degrees)
	{
		float a = degrees % 360f;
		if (a < 0f)
		{
			a += 360f;
		}
		int i = (int)(a / 360f * AngleSteps + 0.5f);
		return i >= AngleSteps ? 0 : i;
	}

	private static Color[] VariantColors(ParticleKind kind, EffectColors c)
	{
		Color sakura = Color.FromRgb(255, 183, 197);
		return kind switch
		{
			ParticleKind.Snow => new Color[1] { EffectColors.Mix(c.Foreground, White, 0.6) },
			ParticleKind.Sakura => new Color[3]
			{
				sakura,
				Color.FromRgb(255, 222, 230),
				EffectColors.Mix(sakura, c.Accent, 0.45)
			},
			ParticleKind.Stars => new Color[2]
			{
				EffectColors.Mix(c.Foreground, White, 0.75),
				EffectColors.Mix(White, c.Accent, 0.45)
			},
			ParticleKind.Fireflies => new Color[2]
			{
				EffectColors.Mix(c.Yellow, Color.FromRgb(255, 226, 110), 0.5),
				c.Accent2
			},
			ParticleKind.Leaves => new Color[3]
			{
				EffectColors.Mix(c.Red, Color.FromRgb(196, 64, 40), 0.4),
				EffectColors.Mix(c.Yellow, Color.FromRgb(230, 170, 40), 0.4),
				EffectColors.Mix(c.Green, Color.FromRgb(110, 150, 50), 0.4)
			},
			ParticleKind.Rain => new Color[1] { EffectColors.Mix(c.Foreground, c.Accent2, 0.25) },
			ParticleKind.Bubbles => new Color[1] { EffectColors.Mix(c.Foreground, c.Accent, 0.3) },
			_ => new Color[1] { c.Foreground },
		};
	}

	private static Color WithAlpha(Color c, double a)
	{
		return Color.FromArgb((byte)Math.Round(255.0 * Math.Clamp(a, 0.0, 1.0)), c.R, c.G, c.B);
	}

	private static Brush Fill(ParticleKind kind, Color c)
	{
		RadialGradientBrush brush = new RadialGradientBrush
		{
			GradientOrigin = new Point(0.4, 0.3),
			GradientStops = new GradientStopCollection
			{
				new GradientStop(EffectColors.Mix(c, White, kind == ParticleKind.Sakura ? 0.45 : 0.15), 0.0),
				new GradientStop(WithAlpha(c, 0.95), 1.0)
			}
		};
		brush.Freeze();
		return brush;
	}

	// Картинка фиксированного квадрата (−0.75..0.75), чтобы поворот не менял масштаб при DrawImage.
	private static ImageSource Shape(Geometry shape, Geometry? vein, Brush fill, Pen? veinPen, double angle)
	{
		DrawingGroup rotated = new DrawingGroup
		{
			Transform = new RotateTransform(angle)
		};
		rotated.Children.Add(new GeometryDrawing(fill, null, shape));
		if (vein != null && veinPen != null)
		{
			rotated.Children.Add(new GeometryDrawing(null, veinPen, vein));
		}
		DrawingGroup root = new DrawingGroup();
		root.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(-0.75, -0.75, 1.5, 1.5))));
		root.Children.Add(rotated);
		DrawingImage image = new DrawingImage(root);
		image.Freeze();
		return image;
	}

	private static Geometry Petal()
	{
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext ctx = g.Open())
		{
			// Лепесток сакуры с выемкой на конце.
			ctx.BeginFigure(new Point(0.0, 0.5), isFilled: true, isClosed: true);
			ctx.BezierTo(new Point(-0.42, 0.25), new Point(-0.46, -0.3), new Point(-0.14, -0.47), isStroked: true, isSmoothJoin: true);
			ctx.LineTo(new Point(0.0, -0.36), isStroked: true, isSmoothJoin: false);
			ctx.LineTo(new Point(0.14, -0.47), isStroked: true, isSmoothJoin: false);
			ctx.BezierTo(new Point(0.46, -0.3), new Point(0.42, 0.25), new Point(0.0, 0.5), isStroked: true, isSmoothJoin: true);
		}
		g.Freeze();
		return g;
	}

	private static Geometry Leaf()
	{
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext ctx = g.Open())
		{
			ctx.BeginFigure(new Point(0.0, -0.5), isFilled: true, isClosed: true);
			ctx.BezierTo(new Point(0.38, -0.28), new Point(0.34, 0.28), new Point(0.0, 0.48), isStroked: true, isSmoothJoin: true);
			ctx.BezierTo(new Point(-0.34, 0.28), new Point(-0.38, -0.28), new Point(0.0, -0.5), isStroked: true, isSmoothJoin: true);
		}
		g.Freeze();
		return g;
	}

	private static Geometry LeafVein()
	{
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext ctx = g.Open())
		{
			ctx.BeginFigure(new Point(0.0, -0.42), isFilled: false, isClosed: false);
			ctx.LineTo(new Point(0.0, 0.62), isStroked: true, isSmoothJoin: false);
			ctx.BeginFigure(new Point(0.0, 0.0), isFilled: false, isClosed: false);
			ctx.LineTo(new Point(0.18, -0.16), isStroked: true, isSmoothJoin: false);
			ctx.BeginFigure(new Point(0.0, 0.0), isFilled: false, isClosed: false);
			ctx.LineTo(new Point(-0.18, -0.16), isStroked: true, isSmoothJoin: false);
			ctx.BeginFigure(new Point(0.0, 0.22), isFilled: false, isClosed: false);
			ctx.LineTo(new Point(0.16, 0.06), isStroked: true, isSmoothJoin: false);
			ctx.BeginFigure(new Point(0.0, 0.22), isFilled: false, isClosed: false);
			ctx.LineTo(new Point(-0.16, 0.06), isStroked: true, isSmoothJoin: false);
		}
		g.Freeze();
		return g;
	}

	private static Pen TrailPen(Point start, Point end)
	{
		LinearGradientBrush brush = new LinearGradientBrush
		{
			StartPoint = start,
			EndPoint = end,
			GradientStops = new GradientStopCollection
			{
				new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0),
				new GradientStop(Color.FromArgb(230, 255, 255, 255), 1.0)
			}
		};
		brush.Freeze();
		Pen pen = new Pen(brush, 1.6)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		};
		pen.Freeze();
		return pen;
	}
}
