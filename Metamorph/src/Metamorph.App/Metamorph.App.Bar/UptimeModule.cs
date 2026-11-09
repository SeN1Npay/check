using Metamorph.App.Infrastructure;

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
		SetValue(SystemStats.Uptime());
	}
}
