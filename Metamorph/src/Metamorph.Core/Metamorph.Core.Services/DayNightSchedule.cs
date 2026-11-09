using System;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public static class DayNightSchedule
{
	public static (TimeSpan Day, TimeSpan Night) Boundaries(ScheduleSettings s, DateTime now)
	{
		if (s.FollowSun)
		{
			double? latitude = s.Latitude;
			if (latitude.HasValue)
			{
				double valueOrDefault = latitude.GetValueOrDefault();
				latitude = s.Longitude;
				if (latitude.HasValue)
				{
					double valueOrDefault2 = latitude.GetValueOrDefault();
					(DateTime, DateTime)? tuple = SunCalculator.SunTimesLocal(DateOnly.FromDateTime(now), valueOrDefault, valueOrDefault2);
					if (tuple.HasValue)
					{
						(DateTime, DateTime) valueOrDefault3 = tuple.GetValueOrDefault();
						return (Day: valueOrDefault3.Item1.TimeOfDay, Night: valueOrDefault3.Item2.TimeOfDay);
					}
				}
			}
		}
		TimeSpan time;
		TimeSpan item = (TimeOfDayParser.TryParse(s.DayStart, out time) ? time : TimeSpan.FromHours(7.0));
		TimeSpan time2;
		TimeSpan item2 = (TimeOfDayParser.TryParse(s.NightStart, out time2) ? time2 : TimeSpan.FromHours(20.0));
		return (Day: item, Night: item2);
	}

	public static bool IsDay(ScheduleSettings s, DateTime now)
	{
		(TimeSpan Day, TimeSpan Night) tuple = Boundaries(s, now);
		TimeSpan item = tuple.Day;
		TimeSpan item2 = tuple.Night;
		TimeSpan timeOfDay = now.TimeOfDay;
		if (!(item <= item2))
		{
			if (!(timeOfDay >= item))
			{
				return timeOfDay < item2;
			}
			return true;
		}
		if (timeOfDay >= item)
		{
			return timeOfDay < item2;
		}
		return false;
	}

	public static string ThemeFor(ScheduleSettings s, DateTime now)
	{
		if (!IsDay(s, now))
		{
			return s.NightThemeId;
		}
		return s.DayThemeId;
	}

	public static TimeSpan UntilNextChange(ScheduleSettings s, DateTime now)
	{
		(TimeSpan Day, TimeSpan Night) tuple = Boundaries(s, now);
		TimeSpan item = tuple.Day;
		TimeSpan item2 = tuple.Night;
		TimeSpan timeSpan = (IsDay(s, now) ? item2 : item) - now.TimeOfDay;
		if (timeSpan <= TimeSpan.Zero)
		{
			timeSpan += TimeSpan.FromDays(1.0);
		}
		return timeSpan;
	}
}
