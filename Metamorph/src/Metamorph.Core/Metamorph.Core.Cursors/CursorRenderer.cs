using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Colors;

namespace Metamorph.Core.Cursors;

public static class CursorRenderer
{
	private sealed class Shape
	{
		public readonly List<(List<(double X, double Y)> Points, bool Add)> Parts = new List<(List<(double, double)>, bool)>();

		public double HotX;

		public double HotY;

		public Shape Poly(bool add, params (double, double)[] pts)
		{
			Parts.Add((pts.ToList(), add));
			return this;
		}

		public Shape Rect(double l, double t, double r, double b, double radius = 0.0, bool add = true)
		{
			if (radius <= 0.0)
			{
				return Poly(add, (l, t), (r, t), (r, b), (l, b));
			}
			List<(double, double)> pts = new List<(double, double)>();
			Arc(r - radius, t + radius, 270.0);
			Arc(r - radius, b - radius, 0.0);
			Arc(l + radius, b - radius, 90.0);
			Arc(l + radius, t + radius, 180.0);
			Parts.Add((pts, add));
			return this;
			void Arc(double cx, double cy, double from)
			{
				for (int i = 0; i <= 6; i++)
				{
					double num = (from + (double)(i * 15)) * Math.PI / 180.0;
					pts.Add((cx + radius * Math.Cos(num), cy + radius * Math.Sin(num)));
				}
			}
		}

		public Shape Circle(double cx, double cy, double radius, bool add = true)
		{
			List<(double, double)> list = new List<(double, double)>();
			for (int i = 0; i < 40; i++)
			{
				double num = (double)i * Math.PI * 2.0 / 40.0;
				list.Add((cx + radius * Math.Cos(num), cy + radius * Math.Sin(num)));
			}
			Parts.Add((list, add));
			return this;
		}

		public Shape Ring(double cx, double cy, double outer, double inner)
		{
			return Circle(cx, cy, outer).Circle(cx, cy, inner, add: false);
		}

		public Shape Transform(Func<(double X, double Y), (double X, double Y)> f)
		{
			Shape shape = new Shape();
			foreach (var (source, item) in Parts)
			{
				shape.Parts.Add((source.Select<(double, double), (double, double)>(f).ToList(), item));
			}
			(shape.HotX, shape.HotY) = f((HotX, HotY));
			return shape;
		}

		public Shape Rotate(double degrees, double scale = 1.0)
		{
			double num = degrees * Math.PI / 180.0;
			double num2 = Math.Cos(num);
			double num3 = Math.Sin(num);
			double sin = num3;
			double cos = num2;
			return Transform(delegate((double X, double Y) p)
			{
				double num4 = (p.X - 16.0) * scale;
				double num5 = (p.Y - 16.0) * scale;
				double num6 = num4;
				return (X: 16.0 + num6 * cos - num5 * sin, Y: 16.0 + num6 * sin + num5 * cos);
			});
		}

		public Shape Merge(Shape other)
		{
			Parts.AddRange(other.Parts);
			return this;
		}

		public Shape Hot(double x, double y)
		{
			HotX = x;
			HotY = y;
			return this;
		}
	}

	public static readonly IReadOnlyList<string> Names = new string[17]
	{
		"Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
		"SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person"
	};

	private const int Ss = 4;

	private const double Grid = 32.0;

	private static Shape Arrow()
	{
		return new Shape().Poly(true, (3.0, 2.0), (3.0, 25.0), (8.5, 19.5), (12.5, 28.5), (16.0, 27.0), (12.0, 18.5), (19.5, 18.5)).Hot(3.0, 2.0);
	}

	private static Shape DoubleArrow()
	{
		return new Shape().Poly(true, (16.0, 2.0), (23.5, 9.5), (18.0, 9.5), (18.0, 22.5), (23.5, 22.5), (16.0, 30.0), (8.5, 22.5), (14.0, 22.5), (14.0, 9.5), (8.5, 9.5)).Hot(16.0, 16.0);
	}

	private static Shape Build(string name)
	{
		return name switch
		{
			"Arrow" => Arrow(), 
			"Help" => Arrow().Ring(25.0, 24.0, 5.0, 2.6), 
			"AppStarting" => Arrow().Ring(24.0, 24.0, 6.5, 3.5), 
			"Wait" => new Shape().Ring(16.0, 16.0, 12.0, 7.0).Hot(16.0, 16.0), 
			"Crosshair" => new Shape().Rect(15.0, 2.0, 17.0, 13.0).Rect(15.0, 19.0, 17.0, 30.0).Rect(2.0, 15.0, 13.0, 17.0)
				.Rect(19.0, 15.0, 30.0, 17.0)
				.Hot(16.0, 16.0), 
			"IBeam" => new Shape().Rect(15.0, 4.0, 17.0, 28.0).Rect(11.0, 3.0, 21.0, 5.2, 1.0).Rect(11.0, 26.8, 21.0, 29.0, 1.0)
				.Hot(16.0, 16.0), 
			"NWPen" => new Shape().Poly(true, (3.0, 3.0), (6.0, 11.0), (22.0, 27.0), (27.0, 22.0), (11.0, 6.0)).Hot(3.0, 3.0), 
			"No" => new Shape().Ring(16.0, 16.0, 12.0, 8.3).Merge(new Shape().Rect(14.3, 4.5, 17.7, 27.5).Rotate(-45.0)).Hot(16.0, 16.0), 
			"SizeNS" => DoubleArrow(), 
			"SizeWE" => DoubleArrow().Rotate(90.0), 
			"SizeNWSE" => DoubleArrow().Rotate(-45.0, 0.92), 
			"SizeNESW" => DoubleArrow().Rotate(45.0, 0.92), 
			"SizeAll" => DoubleArrow().Merge(DoubleArrow().Rotate(90.0)).Hot(16.0, 16.0), 
			"UpArrow" => new Shape().Poly(true, (16.0, 2.0), (25.0, 12.0), (19.0, 12.0), (19.0, 30.0), (13.0, 30.0), (13.0, 12.0), (7.0, 12.0)).Hot(16.0, 2.0), 
			"Hand" => new Shape().Rect(11.0, 2.0, 16.0, 19.0, 2.5).Rect(15.5, 10.5, 20.0, 19.0, 2.2).Rect(19.5, 11.5, 24.0, 20.0, 2.2)
				.Rect(8.0, 15.0, 25.0, 29.5, 4.0)
				.Poly(true, (8.5, 17.0), (4.5, 13.5), (2.8, 15.5), (8.0, 23.5))
				.Hot(13.5, 2.0), 
			"Pin" => Arrow().Circle(24.0, 23.0, 4.5), 
			"Person" => Arrow().Circle(24.0, 20.0, 3.0).Rect(19.5, 24.0, 28.5, 30.0, 2.5), 
			_ => Arrow(), 
		};
	}

	public static CursorImage Render(string name, int size, Rgba fill, Rgba outline, double outlineWidth = 1.6)
	{
		size = Math.Clamp(size, 16, 256);
		Shape shape = Build(name);
		double scale = (double)size / 32.0;
		int num = size * 4;
		bool[] array = new bool[num * num];
		foreach (var (source, value) in shape.Parts)
		{
			FillPolygon(array, num, source.Select<(double, double), (double, double)>(((double X, double Y) p) => (p.X * scale * 4.0, p.Y * scale * 4.0)).ToList(), value);
		}
		double[] array2 = DistanceToShape(array, num);
		double num2 = outlineWidth * scale * 4.0;
		byte[] array3 = new byte[size * size * 4];
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				int num3 = 0;
				int num4 = 0;
				for (int k = 0; k < 4; k++)
				{
					for (int l = 0; l < 4; l++)
					{
						int num5 = (i * 4 + k) * num + j * 4 + l;
						if (array[num5])
						{
							num3++;
						}
						if (array2[num5] <= num2)
						{
							num4++;
						}
					}
				}
				int num6 = 16;
				double num7 = (double)num3 / (double)num6;
				double num8 = (double)num4 / (double)num6 * ((double)(int)outline.A / 255.0);
				double num9 = num7 * ((double)(int)fill.A / 255.0);
				double num10 = num9 + num8 * (1.0 - num9);
				int num11 = (i * size + j) * 4;
				if (!(num10 <= 0.0))
				{
					array3[num11] = (byte)Math.Round(((double)(int)fill.B * num9 + (double)(int)outline.B * num8 * (1.0 - num9)) / num10);
					array3[num11 + 1] = (byte)Math.Round(((double)(int)fill.G * num9 + (double)(int)outline.G * num8 * (1.0 - num9)) / num10);
					array3[num11 + 2] = (byte)Math.Round(((double)(int)fill.R * num9 + (double)(int)outline.R * num8 * (1.0 - num9)) / num10);
					array3[num11 + 3] = (byte)Math.Round(num10 * 255.0);
				}
			}
		}
		int hotX = (int)Math.Clamp(Math.Round(shape.HotX * scale), 0.0, size - 1);
		int hotY = (int)Math.Clamp(Math.Round(shape.HotY * scale), 0.0, size - 1);
		return new CursorImage(size, array3, hotX, hotY);
	}

	private static void FillPolygon(bool[] mask, int size, List<(double X, double Y)> pts, bool value)
	{
		if (pts.Count < 3)
		{
			return;
		}
		List<double> list = new List<double>();
		int num = Math.Max(0, (int)Math.Floor(pts.Min(((double X, double Y) p) => p.Y)));
		int num2 = Math.Min(size - 1, (int)Math.Ceiling(pts.Max(((double X, double Y) p) => p.Y)));
		for (int i = num; i <= num2; i++)
		{
			double num3 = (double)i + 0.5;
			list.Clear();
			for (int j = 0; j < pts.Count; j++)
			{
				(double, double) tuple = pts[j];
				(double, double) tuple2 = pts[(j + 1) % pts.Count];
				if ((tuple.Item2 <= num3 && tuple2.Item2 > num3) || (tuple2.Item2 <= num3 && tuple.Item2 > num3))
				{
					list.Add(tuple.Item1 + (num3 - tuple.Item2) / (tuple2.Item2 - tuple.Item2) * (tuple2.Item1 - tuple.Item1));
				}
			}
			list.Sort();
			for (int k = 0; k + 1 < list.Count; k += 2)
			{
				int num4 = Math.Max(0, (int)Math.Ceiling(list[k] - 0.5));
				int num5 = Math.Min(size - 1, (int)Math.Floor(list[k + 1] - 0.5));
				for (int l = num4; l <= num5; l++)
				{
					mask[i * size + l] = value;
				}
			}
		}
	}

	private static double[] DistanceToShape(bool[] mask, int n)
	{
		int[] array = new int[n * n];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ((!mask[i]) ? 536870911 : 0);
		}
		for (int j = 0; j < n; j++)
		{
			for (int k = 0; k < n; k++)
			{
				int num = j * n + k;
				int num2 = array[num];
				if (k > 0)
				{
					num2 = Math.Min(num2, array[num - 1] + 3);
				}
				if (j > 0)
				{
					num2 = Math.Min(num2, array[num - n] + 3);
					if (k > 0)
					{
						num2 = Math.Min(num2, array[num - n - 1] + 4);
					}
					if (k < n - 1)
					{
						num2 = Math.Min(num2, array[num - n + 1] + 4);
					}
				}
				array[num] = num2;
			}
		}
		for (int num3 = n - 1; num3 >= 0; num3--)
		{
			for (int num4 = n - 1; num4 >= 0; num4--)
			{
				int num5 = num3 * n + num4;
				int num6 = array[num5];
				if (num4 < n - 1)
				{
					num6 = Math.Min(num6, array[num5 + 1] + 3);
				}
				if (num3 < n - 1)
				{
					num6 = Math.Min(num6, array[num5 + n] + 3);
					if (num4 < n - 1)
					{
						num6 = Math.Min(num6, array[num5 + n + 1] + 4);
					}
					if (num4 > 0)
					{
						num6 = Math.Min(num6, array[num5 + n - 1] + 4);
					}
				}
				array[num5] = num6;
			}
		}
		return array.Select((int v) => (double)v / 3.0).ToArray();
	}
}
