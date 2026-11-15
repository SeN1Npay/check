using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Launcher;

public static class FuzzyMatcher
{
	public static double? Score(string query, string candidate)
	{
		query = query.Trim();
		if (query.Length == 0)
		{
			return 0.0;
		}
		if (string.IsNullOrEmpty(candidate))
		{
			return null;
		}
		string text = query.ToLowerInvariant();
		string text2 = candidate.ToLowerInvariant();
		if (text2 == text)
		{
			return 1000.0;
		}
		if (text2.StartsWith(text, StringComparison.Ordinal))
		{
			return 800.0 - (double)text2.Length * 0.5;
		}
		int num = IndexAtWordStart(text2, text);
		if (num >= 0)
		{
			return (double)(600 - num) - (double)text2.Length * 0.3;
		}
		int num2 = text2.IndexOf(text, StringComparison.Ordinal);
		if (num2 >= 0)
		{
			return (double)(400 - num2) - (double)text2.Length * 0.3;
		}
		double num3 = 0.0;
		int num4 = 0;
		int num5 = -2;
		string text3 = text;
		foreach (char c in text3)
		{
			if (c == ' ')
			{
				continue;
			}
			int num6 = -1;
			for (int j = num4; j < text2.Length; j++)
			{
				if (text2[j] == c)
				{
					num6 = j;
					break;
				}
			}
			if (num6 < 0)
			{
				return null;
			}
			num3 += 10.0;
			if (num6 == num5 + 1)
			{
				num3 += 15.0;
			}
			if (num6 == 0 || IsBoundary(text2[num6 - 1]))
			{
				num3 += 20.0;
			}
			num3 -= (double)Math.Min(num6 - num4, 10);
			num5 = num6;
			num4 = num6 + 1;
		}
		return num3 - (double)text2.Length * 0.2;
	}

	public static IEnumerable<T> Filter<T>(IEnumerable<T> items, string query, Func<T, string> text, int limit = 50)
	{
		string query2 = query;
		Func<T, string> text2 = text;
		return from x in (from x in items.Select((T item) => (Item: item, Score: Score(query2, text2(item)))).Where(delegate((T Item, double? Score) x)
				{
					double? item2 = x.Score;
					return item2.HasValue;
				})
				orderby x.Score descending
				select x).Take(limit)
			select x.Item;
	}

	private static bool IsBoundary(char ch)
	{
		switch (ch)
		{
		case ' ':
		case '(':
		case '-':
		case '.':
		case '/':
		case '\\':
		case '_':
			return true;
		default:
			return false;
		}
	}

	private static int IndexAtWordStart(string c, string q)
	{
		for (int i = 1; i < c.Length; i++)
		{
			if (IsBoundary(c[i - 1]) && string.CompareOrdinal(c, i, q, 0, q.Length) == 0)
			{
				return i;
			}
		}
		return -1;
	}
}
