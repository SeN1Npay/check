using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Models;

namespace Metamorph.Core.Desktop;

// Прямоугольник в DIP (без зависимости от WPF, чтобы раскладку можно было тестировать).
public readonly record struct CardRect(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;

	public double Bottom => Y + Height;

	public double CenterX => X + Width / 2.0;

	public double CenterY => Y + Height / 2.0;

	public CardRect Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

	public bool Intersects(CardRect o) => X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;

	public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

// Результат привязки: итоговый прямоугольник и линии-направляющие (если привязка сработала по краю/центру).
public readonly record struct CardSnapResult(CardRect Rect, double? GuideX, double? GuideY);

public static class CardLayout
{
	// Зазор между соседними карточками и отступ от краёв экрана.
	public const double Gap = 16.0;

	public const double EdgeMargin = 24.0;

	public const double Threshold = 10.0;

	public static (double Width, double Height) DefaultSize(string? kind, string? clockStyle = null)
	{
		return (kind ?? "").ToLowerInvariant() switch
		{
			"clock" => (clockStyle ?? "").ToLowerInvariant() == "stacked" ? (260.0, 280.0) : (380.0, 190.0),
			"calendar" => (300.0, 312.0),
			"weather" => (300.0, 150.0),
			"media" => (380.0, 136.0),
			"system" => (360.0, 170.0),
			"pomodoro" => (230.0, 260.0),
			"note" => (270.0, 230.0),
			"todo" => (290.0, 290.0),
			"quote" => (380.0, 150.0),
			_ => (300.0, 160.0),
		};
	}

	// Размер карточки в DIP с учётом масштаба; явные Width/Height из настроек важнее умолчаний.
	public static (double Width, double Height) SizeOf(CardSettings card, double scale = 1.0, string? clockStyle = null)
	{
		(double w, double h) = DefaultSize(card.Kind, clockStyle);
		double s = Math.Clamp(double.IsFinite(scale) ? scale : 1.0, 0.5, 3.0);
		double width = card.Width is double cw && double.IsFinite(cw) && cw >= 60.0 ? cw : w;
		double height = card.Height is double ch && double.IsFinite(ch) && ch >= 40.0 ? ch : h;
		return (width * s, height * s);
	}

	public static List<CardSettings> Preset(string? name)
	{
		switch ((name ?? "").Trim().ToLowerInvariant())
		{
		case "minimal":
			return new List<CardSettings>
			{
				Card("clock", 0.04, 0.07),
				Card("weather", 0.04, 0.28),
				Card("quote", 0.04, 0.80)
			};
		case "clock":
		{
			CardSettings clock = Card("clock", 0.3646, 0.30, 520.0, 240.0);
			clock.Options["seconds"] = "true";
			return new List<CardSettings>
			{
				clock,
				Card("quote", 0.40, 0.62)
			};
		}
		default:
			return new List<CardSettings>
			{
				Card("clock", 0.03, 0.05),
				Card("calendar", 0.03, 0.27),
				Card("weather", 0.03, 0.58),
				Card("pomodoro", 0.62, 0.05),
				Card("system", 0.79, 0.05),
				Card("media", 0.79, 0.23),
				Card("todo", 0.79, 0.38),
				Card("quote", 0.40, 0.82)
			};
		}
	}

	private static CardSettings Card(string kind, double x, double y, double? w = null, double? h = null)
	{
		return new CardSettings
		{
			Kind = kind,
			X = x,
			Y = y,
			Width = w,
			Height = h
		};
	}

	public static CardRect Clamp(CardRect r, CardRect workArea)
	{
		double x = Math.Min(r.X, workArea.Right - r.Width);
		double y = Math.Min(r.Y, workArea.Bottom - r.Height);
		return r with
		{
			X = Math.Max(workArea.X, x),
			Y = Math.Max(workArea.Y, y)
		};
	}

	public static CardRect ToRect(CardSettings card, CardRect workArea, double width, double height)
	{
		double fx = double.IsFinite(card.X) ? Math.Clamp(card.X, 0.0, 1.0) : 0.0;
		double fy = double.IsFinite(card.Y) ? Math.Clamp(card.Y, 0.0, 1.0) : 0.0;
		return Clamp(new CardRect(workArea.X + fx * workArea.Width, workArea.Y + fy * workArea.Height, width, height), workArea);
	}

	public static (double X, double Y) ToFraction(CardRect rect, CardRect workArea)
	{
		double x = workArea.Width > 0.0 ? (rect.X - workArea.X) / workArea.Width : 0.0;
		double y = workArea.Height > 0.0 ? (rect.Y - workArea.Y) / workArea.Height : 0.0;
		return (Math.Round(Math.Clamp(x, 0.0, 1.0), 5), Math.Round(Math.Clamp(y, 0.0, 1.0), 5));
	}

	public static CardSnapResult Snap(CardRect rect, IReadOnlyList<CardRect> others, CardRect workArea, double grid, double threshold = Threshold)
	{
		(double x, double? gx) = SnapAxis(rect.X, rect.Width, workArea.X, workArea.Width, others.Select(o => (o.X, o.Width)), grid, threshold);
		(double y, double? gy) = SnapAxis(rect.Y, rect.Height, workArea.Y, workArea.Height, others.Select(o => (o.Y, o.Height)), grid, threshold);
		CardRect snapped = Clamp(rect with { X = x, Y = y }, workArea);
		// Если зажали в края — направляющая уже не соответствует положению.
		if (Math.Abs(snapped.X - x) > 0.5)
		{
			gx = null;
		}
		if (Math.Abs(snapped.Y - y) > 0.5)
		{
			gy = null;
		}
		return new CardSnapResult(snapped, gx, gy);
	}

	// Одна ось: кандидаты (позиция начала, линия направляющей); ближайший в пределах порога, иначе сетка.
	private static (double Pos, double? Guide) SnapAxis(double pos, double size, double areaStart, double areaSize, IEnumerable<(double Start, double Size)> others, double grid, double threshold)
	{
		List<(double Pos, double Guide)> candidates = new List<(double, double)>
		{
			(areaStart + EdgeMargin, areaStart + EdgeMargin),
			(areaStart + areaSize - EdgeMargin - size, areaStart + areaSize - EdgeMargin),
			(areaStart + areaSize / 2.0 - size / 2.0, areaStart + areaSize / 2.0)
		};
		foreach ((double start, double osize) in others)
		{
			double end = start + osize;
			candidates.Add((start, start));
			candidates.Add((end - size, end));
			candidates.Add((end + Gap, end + Gap));
			candidates.Add((start - Gap - size, start - Gap));
			candidates.Add((start + osize / 2.0 - size / 2.0, start + osize / 2.0));
		}
		double best = double.MaxValue;
		(double, double?) result = (pos, null);
		foreach ((double cpos, double guide) in candidates)
		{
			double d = Math.Abs(cpos - pos);
			if (d <= threshold && d < best)
			{
				best = d;
				result = (cpos, guide);
			}
		}
		if (best != double.MaxValue)
		{
			return result;
		}
		if (grid >= 2.0)
		{
			return (areaStart + Math.Round((pos - areaStart) / grid) * grid, null);
		}
		return (pos, null);
	}

	// Свободное место для новой карточки: обход сетки сверху вниз, слева направо.
	public static CardRect FindFreeSpot(double width, double height, IReadOnlyList<CardRect> others, CardRect workArea, double grid)
	{
		double step = Math.Max(grid, 8.0) * 2.0;
		for (double y = workArea.Y + EdgeMargin; y + height <= workArea.Bottom - EdgeMargin; y += step)
		{
			for (double x = workArea.X + EdgeMargin; x + width <= workArea.Right - EdgeMargin; x += step)
			{
				CardRect candidate = new CardRect(x, y, width, height);
				CardRect padded = new CardRect(x - Gap / 2.0, y - Gap / 2.0, width + Gap, height + Gap);
				if (!others.Any(o => o.Intersects(padded)))
				{
					return candidate;
				}
			}
		}
		return Clamp(new CardRect(workArea.CenterX - width / 2.0, workArea.CenterY - height / 2.0, width, height), workArea);
	}
}
