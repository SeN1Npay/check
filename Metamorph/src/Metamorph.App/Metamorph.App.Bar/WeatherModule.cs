using System;
using System.Threading.Tasks;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class WeatherModule : LabeledModule
{
	private DateTime _loadedAt;

	private bool _busy;

	public override int IntervalTicks => 120;

	public WeatherModule(BarContext context)
		: base(context, "")
	{
		SetVisible(visible: false);
		Clickable(delegate
		{
			_loadedAt = default(DateTime);
			Update();
		});
	}

	public override void Update()
	{
		if (!_busy && Context.Services != null && !(DateTime.UtcNow - _loadedAt < TimeSpan.FromMinutes(15.0)))
		{
			_busy = true;
			RefreshAsync();
		}
	}

	private async Task RefreshAsync()
	{
		try
		{
			WeatherNow weatherNow = await Context.Services.Weather.GetAsync(Context.Profile.Bar.Weather);
			_loadedAt = DateTime.UtcNow;
			if ((object)weatherNow != null)
			{
				SetVisible(visible: true);
				SetValue(weatherNow.Icon + " " + weatherNow.TemperatureText);
				base.TextView.ToolTip = $"{weatherNow.Place}: {weatherNow.Description}, {Loc.T("ветер")} {weatherNow.WindSpeed:0} {Loc.T("км/ч")}";
			}
		}
		catch (Exception ex)
		{
			Log.Error("Модуль weather", ex);
			_loadedAt = DateTime.UtcNow - TimeSpan.FromMinutes(10.0);
		}
		finally
		{
			_busy = false;
		}
	}
}
