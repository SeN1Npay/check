using Metamorph.App.Infrastructure;

namespace Metamorph.App.Bar;

// Батарея — дешёвый системный вызов раз в 10 с, общий опрос датчиков ради неё не держим.
public sealed class BatteryModule : LabeledModule
{
	public override int IntervalTicks => 20;

	public BatteryModule(BarContext context)
		: base(context, "bat")
	{
	}

	public override void Update()
	{
		(int Percent, bool Charging)? battery = SystemStats.Battery();
		if (!battery.HasValue)
		{
			SetVisible(visible: false);
			return;
		}
		SetVisible(visible: true);
		(int percent, bool charging) = battery.Value;
		string text = (charging ? "+" : "");
		SetValue($"{percent}%{text}", (percent <= 15 && !charging) ? Context.Alert : null);
	}
}
