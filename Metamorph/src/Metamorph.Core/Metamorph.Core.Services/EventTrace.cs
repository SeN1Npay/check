using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Metamorph.Core.Services;

public static class EventTrace
{
	public readonly record struct Entry(double Ms, string Category, string Detail);

	private const int Capacity = 4000;

	private static readonly object Sync = new object();

	private static readonly Entry[] Ring = new Entry[4000];

	private static readonly Dictionary<string, long> Counts = new Dictionary<string, long>(StringComparer.Ordinal);

	private static readonly Dictionary<string, Queue<double>> Recent = new Dictionary<string, Queue<double>>(StringComparer.Ordinal);

	private static readonly Dictionary<string, double> LastStormWarning = new Dictionary<string, double>(StringComparer.Ordinal);

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static int _next;

	private static long _total;

	public const double StormWindowSeconds = 3.0;

	public static int StormThreshold { get; set; } = 24;


	public static Func<double> Now { get; set; } = () => Clock.Elapsed.TotalMilliseconds;


	public static long Total
	{
		get
		{
			lock (Sync)
			{
				return _total;
			}
		}
	}

	public static event Action<string, int>? Storm;

	public static void Mark(string category, string detail = "")
	{
		int? num = null;
		lock (Sync)
		{
			double num2 = Now();
			Ring[_next] = new Entry(num2, category, (detail.Length > 200) ? detail.Substring(0, 200) : detail);
			_next = (_next + 1) % 4000;
			_total++;
			Counts[category] = (Counts.TryGetValue(category, out var value) ? (value + 1) : 1);
			if (!Recent.TryGetValue(category, out Queue<double> value2))
			{
				value2 = (Recent[category] = new Queue<double>());
			}
			value2.Enqueue(num2);
			while (value2.Count > 0 && num2 - value2.Peek() > 3000.0)
			{
				value2.Dequeue();
			}
			if (value2.Count >= StormThreshold && (!LastStormWarning.TryGetValue(category, out var value3) || num2 - value3 > 30000.0))
			{
				LastStormWarning[category] = num2;
				num = value2.Count;
			}
		}
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			Log.Warn($"Похоже на петлю событий: {category} ×{valueOrDefault} за {3.0:0} с ({detail})");
			try
			{
				EventTrace.Storm?.Invoke(category, valueOrDefault);
			}
			catch
			{
			}
		}
	}

	public static int Rate(string category, double seconds)
	{
		lock (Sync)
		{
			double num = Now();
			int num2 = 0;
			foreach (Entry item in Snapshot(locked: true))
			{
				if (item.Category == category && num - item.Ms <= seconds * 1000.0)
				{
					num2++;
				}
			}
			return num2;
		}
	}

	public static IReadOnlyDictionary<string, long> Counters()
	{
		lock (Sync)
		{
			return new Dictionary<string, long>(Counts, StringComparer.Ordinal);
		}
	}

	public static IReadOnlyList<Entry> Snapshot(int max = 4000)
	{
		lock (Sync)
		{
			List<Entry> list = Snapshot(locked: true);
			return (list.Count <= max) ? list : list.Skip(list.Count - max).ToList();
		}
	}

	private static List<Entry> Snapshot(bool locked)
	{
		int num = (int)Math.Min(_total, 4000L);
		List<Entry> list = new List<Entry>(num);
		int num2 = (_next - num + 4000) % 4000;
		for (int i = 0; i < num; i++)
		{
			list.Add(Ring[(num2 + i) % 4000]);
		}
		return list;
	}

	public static string Dump(int lastEvents = 300)
	{
		StringBuilder stringBuilder = new StringBuilder();
		double now = Now();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(31, 2, stringBuilder2);
		handler.AppendLiteral("Event trace: ");
		handler.AppendFormatted(Total);
		handler.AppendLiteral(" events, uptime ");
		handler.AppendFormatted(now / 1000.0, "0");
		handler.AppendLiteral(" s");
		stringBuilder3.AppendLine(ref handler);
		IReadOnlyList<Entry> readOnlyList = Snapshot();
		Dictionary<string, int> dictionary = (from e in readOnlyList
			where now - e.Ms <= 10000.0
			group e by e.Category).ToDictionary((IGrouping<string, Entry> g) => g.Key, (IGrouping<string, Entry> g) => g.Count());
		foreach (KeyValuePair<string, long> item in from kv in Counters()
			orderby kv.Value descending
			select kv)
		{
			item.Deconstruct(out var key, out var value);
			string text = key;
			long value2 = value;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted<string>(text, -32);
			handler.AppendLiteral(" total ");
			handler.AppendFormatted(value2, 7);
			handler.AppendLiteral("   last 10 s ");
			handler.AppendFormatted(dictionary.GetValueOrDefault(text), 5);
			stringBuilder4.AppendLine(ref handler);
		}
		stringBuilder.AppendLine();
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder2);
		handler.AppendLiteral("Last ");
		handler.AppendFormatted(Math.Min(lastEvents, readOnlyList.Count));
		handler.AppendLiteral(" events (seconds since start):");
		stringBuilder5.AppendLine(ref handler);
		foreach (Entry item2 in readOnlyList.Skip(Math.Max(0, readOnlyList.Count - lastEvents)))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted(item2.Ms / 1000.0, 10, "0.000");
			handler.AppendLiteral("  ");
			handler.AppendFormatted(item2.Category);
			handler.AppendLiteral("  ");
			handler.AppendFormatted(item2.Detail);
			stringBuilder6.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	public static void Reset()
	{
		lock (Sync)
		{
			Array.Clear(Ring);
			Counts.Clear();
			Recent.Clear();
			LastStormWarning.Clear();
			_next = 0;
			_total = 0L;
		}
	}
}
