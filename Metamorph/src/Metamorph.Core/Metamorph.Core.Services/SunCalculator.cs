using System;

namespace Metamorph.Core.Services;

public static class SunCalculator
{
	public static (DateTime Sunrise, DateTime Sunset)? SunTimesUtc(DateOnly date, double latitude, double longitude)
	{
		DateTime? dateTime = Compute(date, latitude, longitude, sunrise: true);
		DateTime? dateTime2 = Compute(date, latitude, longitude, sunrise: false);
		if (dateTime.HasValue && dateTime2.HasValue)
		{
			return (dateTime.Value, dateTime2.Value);
		}
		return null;
	}

	public static (DateTime Sunrise, DateTime Sunset)? SunTimesLocal(DateOnly date, double latitude, double longitude, TimeZoneInfo? zone = null)
	{
		(DateTime, DateTime)? tuple = SunTimesUtc(date, latitude, longitude);
		if (!tuple.HasValue)
		{
			return null;
		}
		if (zone == null)
		{
			zone = TimeZoneInfo.Local;
		}
		return (TimeZoneInfo.ConvertTimeFromUtc(tuple.Value.Item1, zone), TimeZoneInfo.ConvertTimeFromUtc(tuple.Value.Item2, zone));
	}

	private static DateTime? Compute(DateOnly date, double lat, double lng, bool sunrise)
	{
		int dayOfYear = date.DayOfYear;
		double num = lng / 15.0;
		double num2 = (double)dayOfYear + ((double)(sunrise ? 6 : 18) - num) / 24.0;
		double num3 = 0.9856 * num2 - 3.289;
		double num4 = Norm(num3 + 1.916 * Sin(num3) + 0.02 * Sin(2.0 * num3) + 282.634);
		double num5 = Norm(Deg(Math.Atan(0.91764 * Tan(num4))));
		num5 += Math.Floor(num4 / 90.0) * 90.0 - Math.Floor(num5 / 90.0) * 90.0;
		num5 /= 15.0;
		double num6 = 0.39782 * Sin(num4);
		double num7 = Math.Cos(Math.Asin(num6));
		double num8 = (Cos(90.833) - num6 * Sin(lat)) / (num7 * Cos(lat));
		if (num8 > 1.0 || num8 < -1.0)
		{
			return null;
		}
		double value = (((sunrise ? (360.0 - Deg(Math.Acos(num8))) : Deg(Math.Acos(num8))) / 15.0 + num5 - 0.06571 * num2 - 6.622 - num) % 24.0 + 24.0) % 24.0;
		return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(value);
	}

	private static double Rad(double d)
	{
		return d * Math.PI / 180.0;
	}

	private static double Deg(double r)
	{
		return r * 180.0 / Math.PI;
	}

	private static double Sin(double d)
	{
		return Math.Sin(Rad(d));
	}

	private static double Cos(double d)
	{
		return Math.Cos(Rad(d));
	}

	private static double Tan(double d)
	{
		return Math.Tan(Rad(d));
	}

	private static double Norm(double d)
	{
		return (d % 360.0 + 360.0) % 360.0;
	}
}
