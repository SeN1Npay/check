using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Metamorph.Core.Mascot;

public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
	public static readonly Affine Identity = new Affine(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

	public static Affine Translate(double x, double y)
	{
		return new Affine(1.0, 0.0, 0.0, 1.0, x, y);
	}

	public static Affine Scale(double sx, double sy)
	{
		return new Affine(sx, 0.0, 0.0, sy, 0.0, 0.0);
	}

	public static Affine Rotate(double degrees)
	{
		var (num, num2) = Math.SinCos(degrees * Math.PI / 180.0);
		return new Affine(num2, num, 0.0 - num, num2, 0.0, 0.0);
	}

	public Affine Then(Affine next)
	{
		return new Affine(next.A * A + next.C * B, next.B * A + next.D * B, next.A * C + next.C * D, next.B * C + next.D * D, next.A * E + next.C * F + next.E, next.B * E + next.D * F + next.F);
	}

	public static Affine About(double px, double py, double degrees, double sx = 1.0, double sy = 1.0, double tx = 0.0, double ty = 0.0)
	{
		return Translate(0.0 - px, 0.0 - py).Then(Scale(sx, sy)).Then(Rotate(degrees)).Then(Translate(px + tx, py + ty));
	}

	public (double X, double Y) Apply(double x, double y)
	{
		return (X: A * x + C * y + E, Y: B * x + D * y + F);
	}

	public string ToSvg()
	{
		IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
		DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(13, 6, invariantCulture);
		handler.AppendLiteral("matrix(");
		handler.AppendFormatted(A, "0.####");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(B, "0.####");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(C, "0.####");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(D, "0.####");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(E, "0.##");
		handler.AppendLiteral(" ");
		handler.AppendFormatted(F, "0.##");
		handler.AppendLiteral(")");
		return string.Create(invariantCulture, ref handler);
	}
}
