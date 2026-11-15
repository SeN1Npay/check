using System;
using System.Collections.Generic;
using System.Globalization;

namespace Metamorph.Core.Models;

public static class TimeOfDayParser
{
	public static bool TryParse(string? text, out TimeSpan time)
	{
		time = default(TimeSpan);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (!TimeSpan.TryParseExact(text.Trim(), new string[2] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var result))
		{
			return false;
		}
		if (result < TimeSpan.Zero || result >= TimeSpan.FromDays(1.0))
		{
			return false;
		}
		time = result;
		return true;
	}

	public static int ActiveIndex(IReadOnlyList<string> starts, TimeSpan now)
	{
		int num = -1;
		TimeSpan timeSpan = TimeSpan.MinValue;
		int result = -1;
		TimeSpan timeSpan2 = TimeSpan.MinValue;
		for (int i = 0; i < starts.Count; i++)
		{
			if (TryParse(starts[i], out var time))
			{
				if (time > timeSpan2)
				{
					int num2 = i;
					timeSpan2 = time;
					result = num2;
				}
				if (time <= now && time > timeSpan)
				{
					int num3 = i;
					timeSpan = time;
					num = num3;
				}
			}
		}
		if (num < 0)
		{
			return result;
		}
		return num;
	}

	public static TimeSpan UntilNextChange(IReadOnlyList<string> starts, TimeSpan now)
	{
		TimeSpan timeSpan = TimeSpan.FromDays(1.0);
		foreach (string start in starts)
		{
			if (TryParse(start, out var time))
			{
				TimeSpan timeSpan2 = time - now;
				if (timeSpan2 <= TimeSpan.Zero)
				{
					timeSpan2 += TimeSpan.FromDays(1.0);
				}
				if (timeSpan2 < timeSpan)
				{
					timeSpan = timeSpan2;
				}
			}
		}
		return timeSpan;
	}
}
