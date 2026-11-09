using System;
using Metamorph.Core.Colors;

namespace Metamorph.Core.Effects;

public readonly record struct Tint(Rgba Color, double Alpha);

// Тонировка по времени суток (Alpha — при силе 1, приложение умножает на Strength).
public static class TimeTint
{
	private static readonly TimeSpan DefaultSunrise = TimeSpan.FromHours(7.0);

	private static readonly TimeSpan DefaultSunset = TimeSpan.FromHours(19.5);

	public static readonly Tint Night = new Tint(new Rgba(26, 38, 92), 0.45);

	public static readonly Tint Dawn = new Tint(new Rgba(255, 138, 92), 0.28);

	public static readonly Tint Morning = new Tint(new Rgba(255, 196, 140), 0.14);

	public static readonly Tint Day = new Tint(new Rgba(255, 244, 230), 0.0);

	public static readonly Tint Golden = new Tint(new Rgba(255, 170, 64), 0.26);

	public static readonly Tint Dusk = new Tint(new Rgba(170, 80, 140), 0.3);

	private static readonly Tint[] Keys = new Tint[8] { Night, Dawn, Morning, Day, Day, Golden, Dusk, Night };

	public static Tint For(DateTime local, TimeSpan? sunrise = null, TimeSpan? sunset = null)
	{
		double rise = (sunrise ?? DefaultSunrise).TotalHours;
		double set = (sunset ?? DefaultSunset).TotalHours;
		if (!(set > rise + 2.0))
		{
			rise = DefaultSunrise.TotalHours;
			set = DefaultSunset.TotalHours;
		}
		double t = local.TimeOfDay.TotalHours;
		Span<double> at = stackalloc double[8]
		{
			rise - 1.25,
			rise,
			rise + 1.5,
			rise + 3.0,
			set - 2.0,
			set - 0.4,
			set + 0.5,
			set + 1.5
		};
		Tint[] keys = Keys;
		// Перед первым ключом и после последнего — ночь.
		if (t <= at[0] || t >= at[7])
		{
			return Night;
		}
		for (int i = 0; i < 7; i++)
		{
			if (t < at[i + 1])
			{
				double f = (t - at[i]) / (at[i + 1] - at[i]);
				return Lerp(keys[i], keys[i + 1], SmoothStep(f));
			}
		}
		return Night;
	}

	public static Tint Lerp(Tint a, Tint b, double f)
	{
		f = Math.Clamp(f, 0.0, 1.0);
		// Цвет при нулевой альфе не важен — берём цвет второго ключа, чтобы не было серого перехода.
		Rgba ca = a.Alpha <= 0.0 ? b.Color : a.Color;
		Rgba cb = b.Alpha <= 0.0 ? a.Color : b.Color;
		return new Tint(new Rgba(L(ca.R, cb.R, f), L(ca.G, cb.G, f), L(ca.B, cb.B, f)), a.Alpha + (b.Alpha - a.Alpha) * f);
	}

	private static byte L(byte a, byte b, double f)
	{
		return (byte)Math.Round(a + (b - a) * f);
	}

	private static double SmoothStep(double f)
	{
		f = Math.Clamp(f, 0.0, 1.0);
		return f * f * (3.0 - 2.0 * f);
	}
}
