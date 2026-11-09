using System;
using System.Collections.Generic;

namespace Metamorph.Core.Windows;

public static class TilingLayout
{
	private enum Span
	{
		Start,
		End,
		Full
	}

	private static IntRect Cell(IntRect work, int gap, Span column, Span row)
	{
		IntRect intRect = new IntRect(work.Left + gap, work.Top + gap, work.Right - gap, work.Bottom - gap);
		int num = (intRect.Width - gap) / 2;
		int num2 = (intRect.Height - gap) / 2;
		var (left, right) = column switch
		{
			Span.Start => (intRect.Left, intRect.Left + num), 
			Span.End => (intRect.Right - num, intRect.Right), 
			_ => (intRect.Left, intRect.Right), 
		};
		var (top, bottom) = row switch
		{
			Span.Start => (intRect.Top, intRect.Top + num2), 
			Span.End => (intRect.Bottom - num2, intRect.Bottom), 
			_ => (intRect.Top, intRect.Bottom), 
		};
		return new IntRect(left, top, right, bottom);
	}

	public static IntRect Snap(IntRect current, IntRect work, int gap, SnapDirection direction)
	{
		var (column, row) = Classify(current, work, gap) ?? (Span.Full, Span.Full);
		switch (direction)
		{
		case SnapDirection.Left:
			column = Span.Start;
			break;
		case SnapDirection.Right:
			column = Span.End;
			break;
		case SnapDirection.Up:
			row = Span.Start;
			break;
		case SnapDirection.Down:
			row = Span.End;
			break;
		}
		return Cell(work, gap, column, row);
	}

	public static IntRect Maximize(IntRect work, int gap)
	{
		return Cell(work, gap, Span.Full, Span.Full);
	}

	public static IntRect Center(IntRect work, double widthShare = 0.62, double heightShare = 0.72)
	{
		int num = (int)((double)work.Width * widthShare);
		int num2 = (int)((double)work.Height * heightShare);
		return IntRect.FromSize(work.Left + (work.Width - num) / 2, work.Top + (work.Height - num2) / 2, num, num2);
	}

	public static IReadOnlyList<IntRect> MasterStack(IntRect work, int gap, int count, double ratio)
	{
		List<IntRect> list = new List<IntRect>();
		if (count <= 0)
		{
			return list;
		}
		IntRect item = new IntRect(work.Left + gap, work.Top + gap, work.Right - gap, work.Bottom - gap);
		if (count == 1)
		{
			list.Add(item);
			return list;
		}
		ratio = Math.Clamp(ratio, 0.2, 0.8);
		int num = (int)((double)(item.Width - gap) * ratio);
		list.Add(new IntRect(item.Left, item.Top, item.Left + num, item.Bottom));
		int left = item.Left + num + gap;
		int num2 = count - 1;
		int num3 = (item.Height - gap * (num2 - 1)) / num2;
		for (int i = 0; i < num2; i++)
		{
			int num4 = item.Top + i * (num3 + gap);
			int bottom = ((i == num2 - 1) ? item.Bottom : (num4 + num3));
			list.Add(new IntRect(left, num4, item.Right, bottom));
		}
		return list;
	}

	private static (Span Column, Span Row)? Classify(IntRect r, IntRect work, int gap)
	{
		Span[] array = new Span[3]
		{
			Span.Start,
			Span.End,
			Span.Full
		};
		foreach (Span span in array)
		{
			Span[] array2 = new Span[3]
			{
				Span.Start,
				Span.End,
				Span.Full
			};
			foreach (Span span2 in array2)
			{
				if (Cell(work, gap, span, span2).Near(r))
				{
					return (span, span2);
				}
			}
		}
		return null;
	}
}
