using System;
using System.Collections.Generic;

namespace Metamorph.Core.Dock;

public static class DockLayout
{
	public static double Falloff(double distance, double range)
	{
		if (range <= 0.0)
		{
			return 0.0;
		}
		double num = Math.Abs(distance);
		if (!(num >= range))
		{
			return (Math.Cos(Math.PI * num / range) + 1.0) / 2.0;
		}
		return 0.0;
	}

	public static double[] RestCenters(IReadOnlyList<double> extents, double spacing, double center)
	{
		double[] array = new double[extents.Count];
		double num = TotalLength(extents, null, spacing);
		double num2 = center - num / 2.0;
		for (int i = 0; i < extents.Count; i++)
		{
			array[i] = num2 + extents[i] / 2.0;
			num2 += extents[i] + spacing;
		}
		return array;
	}

	public static double[] TargetScales(IReadOnlyList<double> restCenters, IReadOnlyList<bool> scalable, double? pointer, double magnification, double range)
	{
		double[] array = new double[restCenters.Count];
		int num;
		double num2;
		for (int i = 0; i < array.Length; array[num] = num2, i++)
		{
			num = i;
			if (pointer.HasValue)
			{
				double valueOrDefault = pointer.GetValueOrDefault();
				if (scalable[i])
				{
					num2 = 1.0 + (magnification - 1.0) * Falloff(restCenters[i] - valueOrDefault, range);
					continue;
				}
			}
			num2 = 1.0;
		}
		return array;
	}

	public static double TotalLength(IReadOnlyList<double> extents, IReadOnlyList<double>? scales, double spacing)
	{
		if (extents.Count == 0)
		{
			return 0.0;
		}
		double num = spacing * (double)(extents.Count - 1);
		for (int i = 0; i < extents.Count; i++)
		{
			num += extents[i] * (scales?[i] ?? 1.0);
		}
		return num;
	}

	public static double[] Arrange(IReadOnlyList<double> extents, IReadOnlyList<double> scales, double spacing, double center, out double total)
	{
		total = TotalLength(extents, scales, spacing);
		double[] array = new double[extents.Count];
		double num = center - total / 2.0;
		for (int i = 0; i < extents.Count; i++)
		{
			array[i] = num;
			num += extents[i] * scales[i] + spacing;
		}
		return array;
	}

	public static double[] ArrangeSlots(IReadOnlyList<double> slots, double center, out double total)
	{
		total = 0.0;
		foreach (double slot in slots)
		{
			total += slot;
		}
		double[] array = new double[slots.Count];
		double num = center - total / 2.0;
		for (int i = 0; i < slots.Count; i++)
		{
			array[i] = num;
			num += slots[i];
		}
		return array;
	}

	public static int IndexAt(IReadOnlyList<double> starts, IReadOnlyList<double> lengths, double spacing, double pos)
	{
		for (int i = 0; i < starts.Count; i++)
		{
			if (pos >= starts[i] - spacing / 2.0 && pos < starts[i] + lengths[i] + spacing / 2.0)
			{
				return i;
			}
		}
		return -1;
	}

	public static double Approach(double current, double target, double dtSeconds, double speed)
	{
		if (dtSeconds <= 0.0)
		{
			return current;
		}
		double num = target + (current - target) * Math.Exp((0.0 - speed) * dtSeconds);
		if (!(Math.Abs(num - target) < 0.0005))
		{
			return num;
		}
		return target;
	}

	public static double Bounce(double t, double period, int count)
	{
		if (t < 0.0 || period <= 0.0 || t >= period * (double)count)
		{
			return 0.0;
		}
		double num = t / period;
		int num2 = (int)num;
		double num3 = num - (double)num2;
		double num4 = Math.Max(0.35, 1.0 - 0.18 * (double)num2);
		return Math.Sin(Math.PI * num3) * num4;
	}

	public static int InsertIndex(IReadOnlyList<double> centers, double pos)
	{
		int num = 0;
		foreach (double center in centers)
		{
			if (pos > center)
			{
				num++;
			}
		}
		return num;
	}
}
