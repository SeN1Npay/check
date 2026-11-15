using System;
using System.Globalization;
using System.Linq;

namespace Metamorph.Core.Colors;

public readonly record struct Rgba(byte R, byte G, byte B, byte A = byte.MaxValue)
{
	public double Luminance => (0.2126 * (double)(int)R + 0.7152 * (double)(int)G + 0.0722 * (double)(int)B) / 255.0;

	public static Rgba Parse(string? hex)
	{
		if (!TryParse(hex, out var color))
		{
			throw new FormatException("Некорректный цвет '" + hex + "'. Ожидается #RRGGBB или #AARRGGBB.");
		}
		return color;
	}

	public static bool TryParse(string? hex, out Rgba color)
	{
		color = default(Rgba);
		if (string.IsNullOrWhiteSpace(hex))
		{
			return false;
		}
		string text = hex.Trim().TrimStart('#');
		if (text.Length == 3)
		{
			text = string.Concat(text.Select((char ch) => new string(ch, 2)));
		}
		int length = text.Length;
		if ((length != 6 && length != 8) || 1 == 0)
		{
			return false;
		}
		if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return false;
		}
		color = ((text.Length == 6) ? new Rgba((byte)(result >> 16), (byte)(result >> 8), (byte)result) : new Rgba((byte)(result >> 16), (byte)(result >> 8), (byte)result, (byte)(result >> 24)));
		return true;
	}

	public uint ToAbgr()
	{
		return (uint)((A << 24) | (B << 16) | (G << 8) | R);
	}

	public uint ToArgb()
	{
		return (uint)((A << 24) | (R << 16) | (G << 8) | B);
	}

	public int ToColorRef()
	{
		return (B << 16) | (G << 8) | R;
	}

	public static Rgba FromAbgr(uint v)
	{
		return new Rgba((byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24));
	}

	public static Rgba FromArgb(uint v)
	{
		return new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24));
	}

	public Rgba WithAlpha(byte alpha)
	{
		return this with
		{
			A = alpha
		};
	}

	public Rgba Mix(Rgba other, double t)
	{
		t = Math.Clamp(t, 0.0, 1.0);
		return new Rgba(Lerp(R, other.R), Lerp(G, other.G), Lerp(B, other.B), Lerp(A, other.A));
		byte Lerp(byte from, byte to)
		{
			return (byte)Math.Round((double)(int)from + (double)(to - from) * t);
		}
	}

	public Rgba Lighten(double t)
	{
		return Mix(new Rgba(byte.MaxValue, byte.MaxValue, byte.MaxValue, A), t);
	}

	public Rgba Darken(double t)
	{
		return Mix(new Rgba(0, 0, 0, A), t);
	}

	public string ToRegistryTriplet()
	{
		return $"{R} {G} {B}";
	}

	public static bool TryParseTriplet(string? text, out Rgba color)
	{
		color = default(Rgba);
		string[] array = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length != 3)
		{
			return false;
		}
		if (!byte.TryParse(array[0], out var result) || !byte.TryParse(array[1], out var result2) || !byte.TryParse(array[2], out var result3))
		{
			return false;
		}
		color = new Rgba(result, result2, result3);
		return true;
	}

	public string ToHex()
	{
		if (A == byte.MaxValue)
		{
			return $"#{R:X2}{G:X2}{B:X2}";
		}
		return $"#{A:X2}{R:X2}{G:X2}{B:X2}";
	}

	public override string ToString()
	{
		return ToHex();
	}
}
