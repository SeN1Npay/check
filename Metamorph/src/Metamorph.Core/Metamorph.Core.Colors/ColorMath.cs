using System;

namespace Metamorph.Core.Colors;

public static class ColorMath
{
	public static Hsl ToHsl(Rgba c)
	{
		double num = (double)(int)c.R / 255.0;
		double num2 = (double)(int)c.G / 255.0;
		double num3 = (double)(int)c.B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = (num4 + num5) / 2.0;
		if (num4 - num5 < 1E-09)
		{
			return new Hsl(0.0, 0.0, num6);
		}
		double num7 = num4 - num5;
		double s = ((num6 > 0.5) ? (num7 / (2.0 - num4 - num5)) : (num7 / (num4 + num5)));
		double num8 = ((num4 == num) ? ((num2 - num3) / num7 + (double)((num2 < num3) ? 6 : 0)) : ((num4 != num2) ? ((num - num2) / num7 + 4.0) : ((num3 - num) / num7 + 2.0)));
		return new Hsl(num8 * 60.0, s, num6);
	}

	public static Rgba FromHsl(Hsl hsl, byte alpha = byte.MaxValue)
	{
		double num = (hsl.H % 360.0 + 360.0) % 360.0 / 360.0;
		double num2 = Math.Clamp(hsl.S, 0.0, 1.0);
		double num3 = Math.Clamp(hsl.L, 0.0, 1.0);
		if (num2 < 1E-09)
		{
			byte num4 = (byte)Math.Round(num3 * 255.0);
			return new Rgba(num4, num4, num4, alpha);
		}
		double q = ((num3 < 0.5) ? (num3 * (1.0 + num2)) : (num3 + num2 - num3 * num2));
		double p = 2.0 * num3 - q;
		return new Rgba((byte)Math.Round(Hue(num + 1.0 / 3.0) * 255.0), (byte)Math.Round(Hue(num) * 255.0), (byte)Math.Round(Hue(num - 1.0 / 3.0) * 255.0), alpha);
		double Hue(double t)
		{
			if (t < 0.0)
			{
				t += 1.0;
			}
			if (t > 1.0)
			{
				t -= 1.0;
			}
			if (t < 1.0 / 6.0)
			{
				return p + (q - p) * 6.0 * t;
			}
			if (t < 0.5)
			{
				return q;
			}
			if (t < 2.0 / 3.0)
			{
				return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
			}
			return p;
		}
	}

	public static double RelativeLuminance(Rgba c)
	{
		return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
		static double Ch(byte v)
		{
			double num = (double)(int)v / 255.0;
			if (!(num <= 0.03928))
			{
				return Math.Pow((num + 0.055) / 1.055, 2.4);
			}
			return num / 12.92;
		}
	}

	public static double Contrast(Rgba a, Rgba b)
	{
		double val = RelativeLuminance(a);
		double val2 = RelativeLuminance(b);
		return (Math.Max(val, val2) + 0.05) / (Math.Min(val, val2) + 0.05);
	}

	public static double HueDistance(double a, double b)
	{
		double num = Math.Abs(a - b) % 360.0;
		if (!(num > 180.0))
		{
			return num;
		}
		return 360.0 - num;
	}

	public static double DistanceSq(Rgba a, Rgba b)
	{
		double num = a.R - b.R;
		double num2 = a.G - b.G;
		double num3 = a.B - b.B;
		return num * num * 0.3 + num2 * num2 * 0.59 + num3 * num3 * 0.11;
	}

	public static Rgba EnsureContrast(Rgba color, Rgba background, double minContrast)
	{
		if (Contrast(color, background) >= minContrast)
		{
			return color;
		}
		Hsl hsl = ToHsl(color);
		bool flag = RelativeLuminance(background) < 0.4;
		for (int i = 0; i < 40; i++)
		{
			hsl = hsl with
			{
				L = Math.Clamp(hsl.L + (flag ? 0.025 : (-0.025)), 0.0, 1.0)
			};
			Rgba rgba = FromHsl(hsl, color.A);
			if (Contrast(rgba, background) >= minContrast)
			{
				return rgba;
			}
		}
		if (!flag)
		{
			return new Rgba(0, 0, 0, color.A);
		}
		return new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue, color.A);
	}
}
