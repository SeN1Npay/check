using System;
using Metamorph.App.Infrastructure;

namespace Metamorph.App.Bar;

public sealed class RamModule : LabeledModule
{
	private IDisposable? _lease = SensorHub.Shared.Acquire();

	public override int IntervalTicks => 4;

	public RamModule(BarContext context)
		: base(context, "ram")
	{
	}

	public override void Update()
	{
		SensorSnapshot s = SensorHub.Shared.Current;
		if (s.RamUsedGb.HasValue)
		{
			SetValue($"{s.RamUsedGb.Value:0.0}G", (s.RamLoad.GetValueOrDefault() >= 90) ? Context.Alert : null);
		}
	}

	public override void Suspend(bool suspended)
	{
		if (suspended)
		{
			_lease?.Dispose();
			_lease = null;
		}
		else
		{
			_lease ??= SensorHub.Shared.Acquire();
		}
	}

	public override void Dispose()
	{
		_lease?.Dispose();
		_lease = null;
	}
}
