using Metamorph.Core.Native;

namespace Metamorph.App.Bar;

public sealed class BatteryModule : LabeledModule
{
	private const byte NoBattery = 128;

	private const byte Unknown = byte.MaxValue;

	public override int IntervalTicks => 20;

	public BatteryModule(BarContext context)
		: base(context, "bat")
	{
	}

	public override void Update()
	{
		if (!NativeMethods.GetSystemPowerStatus(out var status) || status.BatteryFlag == 128 || status.BatteryFlag == byte.MaxValue || status.BatteryLifePercent > 100)
		{
			SetVisible(visible: false);
			return;
		}
		SetVisible(visible: true);
		string text = ((status.ACLineStatus == 1) ? "+" : "");
		SetValue($"{status.BatteryLifePercent}%{text}", (status.BatteryLifePercent <= 15 && text == "") ? Context.Alert : null);
	}
}
