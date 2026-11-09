using System;
using System.Windows;
using System.Windows.Media;

namespace Metamorph.App.Games;

// «Стеклянная» клетка: вертикальный градиент, тёмная кромка и светлый блик сверху. Кисти общие для всех клеток цвета.
internal sealed class CellStyle
{
	public readonly Brush Fill;

	public readonly Pen Edge;

	public readonly Brush Shine;

	public readonly Brush GhostFill;

	public readonly Pen GhostEdge;

	public readonly Brush Trail;

	public readonly Brush Spark;

	public readonly Brush Flat;

	public CellStyle(Color c)
	{
		Fill = GamePalette.Vertical((GamePalette.Lighten(c, 0.3), 0.0), (c, 0.45), (GamePalette.Darken(c, 0.2), 1.0));
		Edge = GamePalette.Pen(GamePalette.Darken(c, 0.42), 1.0);
		Shine = GamePalette.Solid(Colors.White, 0.5);
		GhostFill = GamePalette.Solid(c, 0.12);
		GhostEdge = GamePalette.Pen(c, 1.6, 0.75);
		Trail = GamePalette.Vertical((GamePalette.WithAlpha(c, 0.0), 0.0), (GamePalette.WithAlpha(c, 0.42), 1.0));
		Spark = GamePalette.Solid(GamePalette.Lighten(c, 0.35));
		Flat = GamePalette.Solid(c);
	}

	public void Draw(DrawingContext dc, double x, double y, double size)
	{
		double r = size * 0.17;
		Rect box = new Rect(x + 1.0, y + 1.0, size - 2.0, size - 2.0);
		dc.DrawRoundedRectangle(Fill, Edge, box, r, r);
		double sh = Math.Max(1.0, Math.Round(size * 0.07));
		dc.DrawRoundedRectangle(Shine, null, new Rect(box.X + size * 0.14, box.Y + 1.5, Math.Max(1.0, box.Width - size * 0.28), sh), sh / 2.0, sh / 2.0);
	}

	public void Ghost(DrawingContext dc, double x, double y, double size)
	{
		double r = size * 0.17;
		dc.DrawRoundedRectangle(GhostFill, GhostEdge, new Rect(x + 2.0, y + 2.0, size - 4.0, size - 4.0), r, r);
	}
}

internal static class TetrisArt
{
	public static CellStyle[] Styles(GamePalette p)
	{
		CellStyle[] styles = new CellStyle[7];
		for (int i = 0; i < styles.Length; i++)
		{
			styles[i] = new CellStyle(p.Pieces[i]);
		}
		return styles;
	}
}
