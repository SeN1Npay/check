using System;

namespace Metamorph.Core.Audio;

public static class Fft
{
	public static bool IsPowerOfTwo(int n)
	{
		if (n > 0)
		{
			return (n & (n - 1)) == 0;
		}
		return false;
	}

	public static void Transform(Span<double> re, Span<double> im)
	{
		int length = re.Length;
		if (length != im.Length)
		{
			throw new ArgumentException("re и im должны быть одной длины");
		}
		if (!IsPowerOfTwo(length))
		{
			throw new ArgumentException("Длина должна быть степенью двойки");
		}
		int i = 1;
		int num = 0;
		for (; i < length; i++)
		{
			int num2 = length >> 1;
			while ((num & num2) != 0)
			{
				num ^= num2;
				num2 >>= 1;
			}
			num ^= num2;
			if (i < num)
			{
				ref double reference = ref re[i];
				ref double reference2 = ref re[num];
				double num3 = re[num];
				double num4 = re[i];
				reference = num3;
				reference2 = num4;
				reference = ref im[i];
				ref double reference3 = ref im[num];
				num4 = im[num];
				num3 = im[i];
				reference = num4;
				reference3 = num3;
			}
		}
		for (int num5 = 2; num5 <= length; num5 <<= 1)
		{
			double num6 = Math.PI * -2.0 / (double)num5;
			double num7 = Math.Cos(num6);
			double num8 = Math.Sin(num6);
			int num9 = num5 >> 1;
			for (int j = 0; j < length; j += num5)
			{
				double num10 = 1.0;
				double num11 = 0.0;
				for (int k = 0; k < num9; k++)
				{
					int num12 = j + k;
					int index = num12 + num9;
					double num13 = re[index] * num10 - im[index] * num11;
					double num14 = re[index] * num11 + im[index] * num10;
					re[index] = re[num12] - num13;
					im[index] = im[num12] - num14;
					re[num12] += num13;
					im[num12] += num14;
					double num15 = num10 * num7 - num11 * num8;
					num11 = num10 * num8 + num11 * num7;
					num10 = num15;
				}
			}
		}
	}
}
