using System;

namespace Metamorph.Core.Desktop;

public readonly record struct ShelfRect(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;

	public double Bottom => Y + Height;
}

// Геометрия полок в DIP: привязка к сетке и удержание внутри рабочей области монитора.
public static class ShelfLayout
{
	public const double Grid = 16.0;

	public const double MinWidth = 128.0;

	public const double MinHeight = 80.0;

	public static double Snap(double value, double origin, double grid = Grid)
	{
		if (grid <= 0.0)
		{
			return value;
		}
		return origin + Math.Round((value - origin) / grid) * grid;
	}

	public static ShelfRect Fit(ShelfRect shelf, ShelfRect work, bool snap = true, double grid = Grid)
	{
		double width = (snap ? Snap(shelf.Width, 0.0, grid) : shelf.Width);
		double height = (snap ? Snap(shelf.Height, 0.0, grid) : shelf.Height);
		width = Math.Max(Math.Min(MinWidth, work.Width), Math.Min(width, work.Width));
		height = Math.Max(Math.Min(MinHeight, work.Height), Math.Min(height, work.Height));
		double x = (snap ? Snap(shelf.X, work.X, grid) : shelf.X);
		double y = (snap ? Snap(shelf.Y, work.Y, grid) : shelf.Y);
		x = Math.Clamp(x, work.X, Math.Max(work.X, work.Right - width));
		y = Math.Clamp(y, work.Y, Math.Max(work.Y, work.Bottom - height));
		return new ShelfRect(x, y, width, height);
	}

	// В настройках X/Y — доли рабочей области (переносимо между разрешениями).
	public static ShelfRect FromFractions(double fx, double fy, double width, double height, ShelfRect work)
	{
		double x = work.X + Math.Clamp(double.IsFinite(fx) ? fx : 0.0, 0.0, 1.0) * work.Width;
		double y = work.Y + Math.Clamp(double.IsFinite(fy) ? fy : 0.0, 0.0, 1.0) * work.Height;
		double w = (double.IsFinite(width) && width > 0.0) ? width : 320.0;
		double h = (double.IsFinite(height) && height > 0.0) ? height : 220.0;
		return Fit(new ShelfRect(x, y, w, h), work, snap: false);
	}

	public static (double X, double Y) ToFractions(ShelfRect shelf, ShelfRect work)
	{
		double fx = (work.Width > 0.0) ? ((shelf.X - work.X) / work.Width) : 0.0;
		double fy = (work.Height > 0.0) ? ((shelf.Y - work.Y) / work.Height) : 0.0;
		return (Math.Clamp(fx, 0.0, 1.0), Math.Clamp(fy, 0.0, 1.0));
	}
}
