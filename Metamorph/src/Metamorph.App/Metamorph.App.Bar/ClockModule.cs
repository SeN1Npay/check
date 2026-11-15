using System;
using System.Globalization;

namespace Metamorph.App.Bar;

public sealed class ClockModule : LabeledModule
{
	public override int IntervalTicks => 1;

	public ClockModule(BarContext context) : base(context, "")
	{
		BarContext context2 = context;
		ClockModule clockModule = this;
		Clickable(delegate
		{
			CalendarPopup.Toggle(clockModule.View, context2);
		});
	}

	public override void Update()
	{
		string value;
		try
		{
			value = DateTime.Now.ToString(Context.ClockFormat, CultureInfo.CurrentCulture);
		}
		catch (FormatException)
		{
			value = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
		}
		SetValue(value);
	}
}
