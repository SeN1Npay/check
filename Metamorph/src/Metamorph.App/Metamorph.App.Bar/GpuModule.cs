using System;
using Metamorph.App.Infrastructure;

namespace Metamorph.App.Bar;

public sealed class GpuModule : LabeledModule
{
	private IDisposable? _lease = SensorHub.Shared.Acquire(gpu: true);

	public override int IntervalTicks => 4;

	public GpuModule(BarContext context)
		: base(context, "gpu")
	{
		Clickable(delegate
		{
			Settings.Open("taskmgr.exe");
		});
	}

	public override void Update()
	{
		double? gpu = SensorHub.Shared.Current.Gpu;
		if (gpu.HasValue)
		{
			double value = gpu.GetValueOrDefault();
			SetValue($"{value:0}%", (value >= 90.0) ? Context.Alert : null);
			SetVisible(visible: true);
		}
		else if (!SensorHub.Shared.GpuAvailable)
		{
			SetVisible(visible: false);
		}
	}

	// Счётчик GPU дорогой: пока панель скрыта, отпускаем его.
	public override void Suspend(bool suspended)
	{
		if (suspended)
		{
			_lease?.Dispose();
			_lease = null;
		}
		else
		{
			_lease ??= SensorHub.Shared.Acquire(gpu: true);
		}
	}

	public override void Dispose()
	{
		_lease?.Dispose();
		_lease = null;
	}
}
