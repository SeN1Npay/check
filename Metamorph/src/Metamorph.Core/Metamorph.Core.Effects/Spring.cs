using System;

namespace Metamorph.Core.Effects;

// Критически демпфированная пружина (без перелёта), точное решение — устойчива при любом dt.
public struct Spring
{
	public double Value;

	public double Velocity;

	public void Step(double target, double omega, double dt)
	{
		if (!(dt > 0.0))
		{
			return;
		}
		double delta = Value - target;
		double temp = (Velocity + omega * delta) * dt;
		double e = Math.Exp(-omega * dt);
		Velocity = (Velocity - omega * temp) * e;
		Value = target + (delta + temp) * e;
	}

	public bool IsSettled(double target, double epsilon = 0.05)
	{
		if (Math.Abs(Value - target) < epsilon)
		{
			return Math.Abs(Velocity) < epsilon;
		}
		return false;
	}
}

public static class Parallax
{
	// Жёсткость пружины: ~0.35 с до цели.
	public const double Omega = 9.0;

	// Сдвиг обоев против курсора: курсор в центре — 0, у края — ±strength.
	public static (double X, double Y) Target(double cursorX, double cursorY, double width, double height, double strength)
	{
		if (!(width > 0.0) || !(height > 0.0) || !(strength > 0.0) || !double.IsFinite(cursorX) || !double.IsFinite(cursorY))
		{
			return (0.0, 0.0);
		}
		double nx = Math.Clamp(cursorX / width * 2.0 - 1.0, -1.0, 1.0);
		double ny = Math.Clamp(cursorY / height * 2.0 - 1.0, -1.0, 1.0);
		return (-nx * strength, -ny * strength);
	}

	// Масштаб, при котором сдвиг на ±strength не открывает края ни по ширине, ни по высоте.
	public static double OverscanScale(double strength, double width, double height)
	{
		if (!(strength > 0.0) || !(width > 0.0) || !(height > 0.0))
		{
			return 1.0;
		}
		return 1.0 + 2.0 * strength / Math.Min(width, height);
	}
}
