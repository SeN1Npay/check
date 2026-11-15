using System;

namespace Metamorph.Core.Mascot;

public sealed class Spring
{
	public double Value;

	public double Velocity;

	public double Stiffness = 120.0;

	public double Damping = 9.0;

	public Spring()
	{
	}

	public Spring(double stiffness, double damping)
	{
		Stiffness = stiffness;
		Damping = damping;
	}

	public double Update(double target, double dt)
	{
		int num = Math.Max(1, (int)Math.Ceiling(dt / 0.008));
		double num2 = dt / (double)num;
		for (int i = 0; i < num; i++)
		{
			double num3 = (0.0 - Stiffness) * (Value - target) - Damping * Velocity;
			Velocity += num3 * num2;
			Value += Velocity * num2;
		}
		if (!double.IsFinite(Value) || !double.IsFinite(Velocity))
		{
			Value = (Velocity = 0.0);
		}
		return Value;
	}
}
