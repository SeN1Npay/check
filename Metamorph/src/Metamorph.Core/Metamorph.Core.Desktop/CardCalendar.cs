using System;

namespace Metamorph.Core.Desktop;

public static class CardCalendar
{
	// 6 недель × 7 дней, начиная с недели, в которую попадает 1-е число (с учётом первого дня недели).
	public static DateTime[] Month(int year, int month, DayOfWeek firstDay)
	{
		DateTime first = new DateTime(year, month, 1);
		int shift = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
		DateTime start = first.AddDays(-shift);
		DateTime[] days = new DateTime[42];
		for (int i = 0; i < days.Length; i++)
		{
			days[i] = start.AddDays(i);
		}
		return days;
	}

	public static DayOfWeek[] WeekOrder(DayOfWeek firstDay)
	{
		DayOfWeek[] order = new DayOfWeek[7];
		for (int i = 0; i < 7; i++)
		{
			order[i] = (DayOfWeek)(((int)firstDay + i) % 7);
		}
		return order;
	}

	public static bool IsWeekend(DayOfWeek day)
	{
		return day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
	}
}
