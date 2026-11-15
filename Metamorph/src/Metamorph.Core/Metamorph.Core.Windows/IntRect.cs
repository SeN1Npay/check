using System;

namespace Metamorph.Core.Windows;

public readonly record struct IntRect(int Left, int Top, int Right, int Bottom)
{
	public int Width => Right - Left;

	public int Height => Bottom - Top;

	public static IntRect FromSize(int left, int top, int width, int height)
	{
		return new IntRect(left, top, left + width, top + height);
	}

	public bool Near(IntRect other, int tolerance = 12)
	{
		if (Math.Abs(Left - other.Left) <= tolerance && Math.Abs(Top - other.Top) <= tolerance && Math.Abs(Right - other.Right) <= tolerance)
		{
			return Math.Abs(Bottom - other.Bottom) <= tolerance;
		}
		return false;
	}
}
