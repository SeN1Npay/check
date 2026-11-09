using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Games;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// Готовые замороженные геометрии и кисти раннера. Локальные координаты: x вправо от левого края, y вверх отрицательный (0 — земля).
internal sealed class RunnerArt
{
	private static readonly Geometry CrystalGeo = Poly((0, 0), (3, -20), (10, -38), (17, -21), (20, 0));

	private static readonly Geometry CrystalLight = Poly((10, -36), (4.5, -20), (8, -6), (10, -20));

	private static readonly Geometry CrystalShade = Poly((10, -20), (17, -21), (20, 0), (11, 0));

	private static readonly Geometry PumpkinBody = Ellipse(20, -15, 20, 15);

	private static readonly Geometry PumpkinRibs = Group(Ellipse(12, -15, 9, 14.5), Ellipse(28, -15, 9, 14.5), Line(20, -29, 20, -1));

	private static readonly Geometry PumpkinStem = Poly((18, -27), (19, -34), (23, -34), (22, -27));

	private static readonly Geometry PumpkinLeaf = Poly((22, -31), (29, -36), (27, -30));

	private static readonly Geometry PumpkinFace = Group(Poly((10, -19), (14, -24), (17, -18)), Poly((23, -18), (26, -24), (30, -19)), Poly((10, -11), (14, -8), (17, -11), (20, -7), (23, -11), (26, -8), (30, -11), (27, -5), (13, -5)));

	private static readonly Geometry BlockGeo = Freeze(new RectangleGeometry(new Rect(0, -46, 34, 46), 6, 6));

	private static readonly Geometry BlockInner = Freeze(new RectangleGeometry(new Rect(5, -41, 24, 36), 4, 4));

	private static readonly Geometry BlockStar = ParseFrozen(MascotRig.StarPath(17, -23, 9, 3.6));

	private static readonly Geometry BatBody = Group(Ellipse(20, -12, 7, 8), Poly((14.5, -17), (15, -24), (18.5, -19)), Poly((25.5, -17), (25, -24), (21.5, -19)));

	private static readonly Geometry[] BatWings =
	{
		Group(Path("M14,-13 C10,-22 4,-24 0,-22 C3,-19 3,-16 2,-14 C5,-15 7,-13 7,-10 C9,-12 12,-12 14,-10 Z"), Path("M26,-13 C30,-22 36,-24 40,-22 C37,-19 37,-16 38,-14 C35,-15 33,-13 33,-10 C31,-12 28,-12 26,-10 Z")),
		Group(Path("M14,-12 C10,-6 4,-3 0,-4 C3,-7 2,-9 3,-11 C6,-9 8,-11 8,-13 C10,-12 12,-13 14,-14 Z"), Path("M26,-12 C30,-6 36,-3 40,-4 C37,-7 38,-9 37,-11 C34,-9 32,-11 32,-13 C30,-12 28,-13 26,-14 Z"))
	};

	private static readonly Geometry BatEyes = Group(Ellipse(17.6, -13, 1.5, 1.5), Ellipse(22.4, -13, 1.5, 1.5));

	private static readonly Geometry BirdBody = Group(Ellipse(22, -12, 13, 7), Ellipse(11, -15, 6, 6), Poly((34, -13), (44, -18), (42, -9), (33, -10)));

	private static readonly Geometry BirdBeak = Poly((6, -17), (0, -14.5), (6, -12.5));

	private static readonly Geometry BirdEye = Ellipse(9.5, -16, 1.4, 1.4);

	private static readonly Geometry[] BirdWings =
	{
		Poly((17, -15), (27, -15), (30, -26), (22, -24)),
		Poly((17, -11), (27, -11), (26, -1), (20, -3))
	};

	private readonly Brush[] _crystal = new Brush[2];

	private readonly Pen[] _crystalEdge = new Pen[2];

	private readonly Brush _light;

	private readonly Brush _shade;

	private readonly Brush _pumpkin;

	private readonly Pen _pumpkinRib;

	private readonly Brush _stem;

	private readonly Brush _face;

	private readonly Brush _block;

	private readonly Pen _blockEdge;

	private readonly Pen _blockInner;

	private readonly Brush _blockStar;

	private readonly Brush _bat;

	private readonly Brush _batEyes;

	private readonly Brush _bird;

	private readonly Brush _beak;

	private readonly Brush _eye;

	public RunnerArt(GamePalette p)
	{
		Color c1 = GamePalette.Vivid(p.Accent);
		Color c2 = GamePalette.Vivid(p.Accent2);
		_crystal[0] = GamePalette.Vertical((GamePalette.Lighten(c1, 0.35), 0.0), (c1, 0.55), (GamePalette.Darken(c1, 0.2), 1.0));
		_crystal[1] = GamePalette.Vertical((GamePalette.Lighten(c2, 0.35), 0.0), (c2, 0.55), (GamePalette.Darken(c2, 0.2), 1.0));
		_crystalEdge[0] = GamePalette.Pen(GamePalette.Darken(c1, 0.45), 1.1);
		_crystalEdge[1] = GamePalette.Pen(GamePalette.Darken(c2, 0.45), 1.1);
		_light = GamePalette.Solid(Colors.White, 0.42);
		_shade = GamePalette.Solid(Colors.Black, 0.16);
		Color orange = GamePalette.Vivid(GamePalette.Mix(p.Yellow, p.Red, 0.45));
		_pumpkin = GamePalette.Vertical((GamePalette.Lighten(orange, 0.25), 0.0), (orange, 0.5), (GamePalette.Darken(orange, 0.25), 1.0));
		_pumpkinRib = GamePalette.Pen(GamePalette.Darken(orange, 0.35), 1.2);
		_stem = GamePalette.Solid(GamePalette.Vivid(GamePalette.Darken(p.Green, 0.15)));
		_face = GamePalette.Solid(GamePalette.Darken(orange, 0.7));
		Color block = GamePalette.Vivid(GamePalette.Mix(p.Accent2, p.Accent, 0.35));
		_block = GamePalette.Vertical((GamePalette.Lighten(block, 0.25), 0.0), (block, 0.4), (GamePalette.Darken(block, 0.3), 1.0));
		_blockEdge = GamePalette.Pen(GamePalette.Darken(block, 0.5), 1.2);
		_blockInner = GamePalette.Pen(GamePalette.Lighten(block, 0.45), 1.2, 0.7);
		_blockStar = GamePalette.Solid(GamePalette.Lighten(p.Yellow, 0.2), 0.95);
		_bat = GamePalette.Solid(GamePalette.Darken(GamePalette.Mix(p.Muted, p.Accent, 0.35), 0.35));
		_batEyes = GamePalette.Solid(GamePalette.Vivid(p.Yellow));
		Color bird = GamePalette.Vivid(GamePalette.Mix(p.Accent2, p.Red, 0.2));
		_bird = GamePalette.Vertical((GamePalette.Lighten(bird, 0.15), 0.0), (GamePalette.Darken(bird, 0.2), 1.0));
		_beak = GamePalette.Solid(GamePalette.Vivid(p.Yellow));
		_eye = GamePalette.Solid(GamePalette.Darken(p.Bg, 0.5));
	}

	public void Obstacle(DrawingContext dc, TransformPool pool, in DinoObstacle o, double sx, double sy, double k, double time)
	{
		switch (o.Kind)
		{
		case DinoObstacleKind.CrystalSmall:
		case DinoObstacleKind.CrystalLarge:
		{
			(double w, double h) = DinoEngine.Size(o.Kind);
			for (int i = 0; i < o.Count; i++)
			{
				double hv = ((o.Variant >> i) & 1) == 0 ? 1.0 : 0.86;
				Crystal(dc, pool, sx + i * (w + 6.0) * k, sy, k * w / 20.0, k * h / 38.0 * hv, (o.Variant >> (i + 3)) & 1);
			}
			break;
		}
		case DinoObstacleKind.Pumpkin:
			Pumpkin(dc, pool, sx, sy, k);
			break;
		case DinoObstacleKind.Block:
			Block(dc, pool, sx, sy, k, time + o.Variant);
			break;
		case DinoObstacleKind.Bat:
			Bat(dc, pool, sx, sy, k, (int)(time * 9.0 + o.Variant) & 1);
			break;
		default:
			Bird(dc, pool, sx, sy, k, (int)(time * 7.0 + o.Variant) & 1);
			break;
		}
	}

	public void Crystal(DrawingContext dc, TransformPool pool, double x, double y, double kx, double ky, int tint)
	{
		dc.PushTransform(pool.Next(kx, 0, 0, ky, x, y));
		dc.DrawGeometry(_crystal[tint & 1], _crystalEdge[tint & 1], CrystalGeo);
		dc.DrawGeometry(_shade, null, CrystalShade);
		dc.DrawGeometry(_light, null, CrystalLight);
		dc.Pop();
	}

	public void Pumpkin(DrawingContext dc, TransformPool pool, double x, double y, double k)
	{
		dc.PushTransform(pool.Next(k, x, y));
		dc.DrawGeometry(_stem, null, PumpkinStem);
		dc.DrawGeometry(_stem, null, PumpkinLeaf);
		dc.DrawGeometry(_pumpkin, _pumpkinRib, PumpkinBody);
		dc.DrawGeometry(null, _pumpkinRib, PumpkinRibs);
		dc.DrawGeometry(_face, null, PumpkinFace);
		dc.Pop();
	}

	public void Block(DrawingContext dc, TransformPool pool, double x, double y, double k, double time)
	{
		dc.PushTransform(pool.Next(k, x, y));
		dc.DrawGeometry(_block, _blockEdge, BlockGeo);
		dc.DrawGeometry(null, _blockInner, BlockInner);
		dc.PushOpacity(0.75 + 0.25 * Math.Sin(time * 5.0));
		dc.DrawGeometry(_blockStar, null, BlockStar);
		dc.Pop();
		dc.Pop();
	}

	public void Bat(DrawingContext dc, TransformPool pool, double x, double y, double k, int frame)
	{
		dc.PushTransform(pool.Next(k, x, y));
		dc.DrawGeometry(_bat, null, BatWings[frame & 1]);
		dc.DrawGeometry(_bat, null, BatBody);
		dc.DrawGeometry(_batEyes, null, BatEyes);
		dc.Pop();
	}

	public void Bird(DrawingContext dc, TransformPool pool, double x, double y, double k, int frame)
	{
		dc.PushTransform(pool.Next(k, x, y));
		dc.DrawGeometry(_bird, null, BirdWings[(frame + 1) & 1]);
		dc.DrawGeometry(_bird, null, BirdBody);
		dc.DrawGeometry(_beak, null, BirdBeak);
		dc.DrawGeometry(_eye, null, BirdEye);
		dc.DrawGeometry(_bird, null, BirdWings[frame & 1]);
		dc.Pop();
	}

	public static Geometry Poly(params (double X, double Y)[] pts)
	{
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext c = g.Open())
		{
			c.BeginFigure(new Point(pts[0].X, pts[0].Y), isFilled: true, isClosed: true);
			for (int i = 1; i < pts.Length; i++)
			{
				c.LineTo(new Point(pts[i].X, pts[i].Y), isStroked: true, isSmoothJoin: true);
			}
		}
		g.Freeze();
		return g;
	}

	public static Geometry Ellipse(double cx, double cy, double rx, double ry)
	{
		return Freeze(new EllipseGeometry(new Point(cx, cy), rx, ry));
	}

	private static Geometry Line(double x1, double y1, double x2, double y2)
	{
		return Freeze(new LineGeometry(new Point(x1, y1), new Point(x2, y2)));
	}

	public static Geometry Group(params Geometry[] items)
	{
		GeometryGroup g = new GeometryGroup
		{
			FillRule = FillRule.Nonzero
		};
		foreach (Geometry item in items)
		{
			g.Children.Add(item);
		}
		g.Freeze();
		return g;
	}

	private static Geometry Path(string data)
	{
		return ParseFrozen(data);
	}

	private static Geometry ParseFrozen(string data)
	{
		Geometry g = Geometry.Parse(data);
		g.Freeze();
		return g;
	}

	public static Geometry Freeze(Geometry g)
	{
		g.Freeze();
		return g;
	}
}
