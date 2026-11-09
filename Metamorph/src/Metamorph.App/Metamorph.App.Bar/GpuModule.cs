using System;
using System.Threading.Tasks;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class GpuModule : LabeledModule
{
	private readonly GpuCounter _counter = new GpuCounter();

	private bool _busy;

	public override int IntervalTicks => 4;

	public GpuModule(BarContext context)
		: base(context, "gpu")
	{
		Clickable(delegate
		{
			Settings.Open("taskmgr.exe");
		});
	}

	public override async void Update()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		try
		{
			double? num = await Task.Run((Func<double?>)_counter.Read);
			if (num.HasValue)
			{
				double valueOrDefault = num.GetValueOrDefault();
				SetValue($"{valueOrDefault:0}%", (valueOrDefault >= 90.0) ? Context.Alert : null);
				SetVisible(visible: true);
			}
			else if (!_counter.Available)
			{
				SetVisible(visible: false);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Модуль gpu", ex);
		}
		finally
		{
			_busy = false;
		}
	}

	public override void Dispose()
	{
		_counter.Dispose();
	}
}
