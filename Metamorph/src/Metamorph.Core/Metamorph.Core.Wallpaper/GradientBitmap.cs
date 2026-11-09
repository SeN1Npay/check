using System;
using System.Buffers.Binary;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;

namespace Metamorph.Core.Wallpaper;

public static class GradientBitmap
{
	private const int HeaderSize = 54;

	private static readonly int[] Bayer4 = new int[16]
	{
		0, 8, 2, 10, 12, 4, 14, 6, 3, 11,
		1, 9, 15, 7, 13, 5
	};

	public static byte[] Create(int width, int height, Rgba from, Rgba to, GradientDirection direction)
	{
		if (width <= 0)
		{
			throw new ArgumentOutOfRangeException("width");
		}
		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException("height");
		}
		int num = (width * 3 + 3) & -4;
		int num2 = num * height;
		byte[] array = new byte[54 + num2];
		Span<byte> span = array.AsSpan();
		array[0] = 66;
		array[1] = 77;
		ref Span<byte> reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(2, reference.Length - 2), array.Length);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(10, reference.Length - 10), 54);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(14, reference.Length - 14), 40);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(18, reference.Length - 18), width);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(22, reference.Length - 22), height);
		reference = ref span;
		BinaryPrimitives.WriteInt16LittleEndian(reference.Slice(26, reference.Length - 26), 1);
		reference = ref span;
		BinaryPrimitives.WriteInt16LittleEndian(reference.Slice(28, reference.Length - 28), 24);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(34, reference.Length - 34), num2);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(38, reference.Length - 38), 2835);
		reference = ref span;
		BinaryPrimitives.WriteInt32LittleEndian(reference.Slice(42, reference.Length - 42), 2835);
		double num3 = (double)(width - 1) / 2.0;
		double num4 = (double)(height - 1) / 2.0;
		double num5 = Math.Max(1.0, Math.Sqrt(num3 * num3 + num4 * num4));
		double num6 = Math.Max(1, width - 1);
		double num7 = Math.Max(1, height - 1);
		for (int i = 0; i < height; i++)
		{
			int num8 = 54 + (height - 1 - i) * num;
			for (int j = 0; j < width; j++)
			{
				double t = direction switch
				{
					GradientDirection.Horizontal => (double)j / num6, 
					GradientDirection.Diagonal => ((double)j / num6 + (double)i / num7) / 2.0, 
					GradientDirection.Radial => Math.Sqrt(((double)j - num3) * ((double)j - num3) + ((double)i - num4) * ((double)i - num4)) / num5, 
					_ => (double)i / num7, 
				};
				double dither = ((double)Bayer4[((i & 3) << 2) | (j & 3)] + 0.5) / 16.0 - 0.5;
				int num9 = num8 + j * 3;
				array[num9] = Channel(from.B, to.B, t, dither);
				array[num9 + 1] = Channel(from.G, to.G, t, dither);
				array[num9 + 2] = Channel(from.R, to.R, t, dither);
			}
		}
		return array;
	}

	public static byte[] CreateSolid(int width, int height, Rgba color)
	{
		return Create(width, height, color, color, GradientDirection.Vertical);
	}

	private static byte Channel(byte a, byte b, double t, double dither)
	{
		return (byte)Math.Clamp((int)Math.Floor((double)(int)a + (double)(b - a) * t + 0.5 + dither), 0, 255);
	}
}
