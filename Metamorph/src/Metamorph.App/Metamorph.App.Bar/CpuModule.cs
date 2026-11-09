using System;
using Metamorph.App.Infrastructure;

namespace Metamorph.App.Bar;

public sealed class CpuModule : LabeledModule
{
	private IDisposable? _lease = SensorHub.Shared.Acquire();

	public CpuModule(BarContext context)
		: base(context, "cpu")
	{
		SetValue(" --%");
	}

	public override void Update()
	{
		double? cpu = SensorHub.Shared.Current.Cpu;
		if (cpu.HasValue)
		{
			double value = cpu.GetValueOrDefault();
			SetValue($"{value,3:0}%", (value >= 90.0) ? Context.Alert : null);
		}
	}

	// Панель скрыта — отпускаем общий опрос (если больше никому не нужен, он остановится).
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
