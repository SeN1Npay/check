using System;

namespace Metamorph.App.Bar;

public sealed class UptimeModule : LabeledModule
{
	public override int IntervalTicks => 60;

	public UptimeModule(BarContext context)
		: base(context, "up")
	{
	}

	public override void Update()
	{
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(Environment.TickCount64);
		SetValue((timeSpan.TotalDays >= 1.0) ? $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h" : $"{timeSpan.Hours}h {timeSpan.Minutes:00}m");
	}
}
