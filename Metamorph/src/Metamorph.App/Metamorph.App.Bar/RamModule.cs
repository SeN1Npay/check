using Metamorph.Core.Native;

namespace Metamorph.App.Bar;

public sealed class RamModule : LabeledModule
{
	public override int IntervalTicks => 4;

	public RamModule(BarContext context)
		: base(context, "ram")
	{
	}

	public override void Update()
	{
		NativeMethods.MEMORYSTATUSEX buffer = NativeMethods.MEMORYSTATUSEX.Create();
		if (NativeMethods.GlobalMemoryStatusEx(ref buffer))
		{
			double value = (double)(buffer.ullTotalPhys - buffer.ullAvailPhys) / 1024.0 / 1024.0 / 1024.0;
			SetValue($"{value:0.0}G", (buffer.dwMemoryLoad >= 90) ? Context.Alert : null);
		}
	}
}
