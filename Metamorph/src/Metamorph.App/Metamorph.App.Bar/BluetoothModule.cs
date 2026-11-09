using System;
using System.Linq;
using System.Threading.Tasks;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;
using Windows.Devices.Radios;

namespace Metamorph.App.Bar;

public sealed class BluetoothModule : LabeledModule
{
	private Radio? _radio;

	private bool _busy;

	private bool _searched;

	public override int IntervalTicks => 10;

	public BluetoothModule(BarContext context)
		: base(context, "bt")
	{
		SetVisible(visible: false);
		Clickable(delegate
		{
			ToggleAsync();
		}, delegate
		{
			Settings.Open("ms-settings:bluetooth");
		});
	}

	public override void Update()
	{
		if (!_busy)
		{
			_busy = true;
			RefreshAsync();
		}
	}

	private async Task RefreshAsync()
	{
		try
		{
			if ((object)_radio == null && !_searched)
			{
				_searched = true;
				_radio = (await Radio.GetRadiosAsync()).FirstOrDefault((Radio r) => r.Kind == RadioKind.Bluetooth);
			}
			SetVisible((object)_radio != null);
			if ((object)_radio != null)
			{
				bool flag = _radio.State == RadioState.On;
				SetValue(flag ? Loc.T("вкл") : Loc.T("выкл"), flag ? null : Context.Muted);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Модуль bluetooth", ex);
			SetVisible(visible: false);
		}
		finally
		{
			_busy = false;
		}
	}

	private async Task ToggleAsync()
	{
		if ((object)_radio == null)
		{
			return;
		}
		try
		{
			if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed)
			{
				Settings.Open("ms-settings:bluetooth");
				return;
			}
			await _radio.SetStateAsync((_radio.State != RadioState.On) ? RadioState.On : RadioState.Off);
		}
		catch (Exception ex)
		{
			Log.Error("Bluetooth: переключение", ex);
			Settings.Open("ms-settings:bluetooth");
		}
		Update();
	}
}
