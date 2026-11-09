using System;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;
using Windows.Networking.Connectivity;

namespace Metamorph.App.Bar;

public sealed class NetworkModule : LabeledModule
{
	private readonly SystemStats _stats = new SystemStats();

	private string _name = "";

	private string _kind = "net";

	private bool _online = true;

	private long _tick;

	public override int IntervalTicks => 4;

	public NetworkModule(BarContext context)
		: base(context, "")
	{
		Clickable(delegate
		{
			Settings.Open("ms-settings:network");
		}, delegate
		{
			Settings.Open("ms-settings:network-status");
		});
	}

	public override void Update()
	{
		if (_tick++ % 5 == 0L)
		{
			ReadProfile();
		}
		if (!_online)
		{
			SetValue(Loc.T("нет сети"), Context.Alert);
			return;
		}
		(double, double)? tuple = _stats.Network();
		object obj;
		if (tuple.HasValue)
		{
			(double, double) valueOrDefault = tuple.GetValueOrDefault();
			obj = " ↓" + SystemStats.Rate(valueOrDefault.Item1) + " ↑" + SystemStats.Rate(valueOrDefault.Item2);
		}
		else
		{
			obj = "";
		}
		string text = (string)obj;
		SetValue((_kind + " " + _name + text).Trim());
		base.TextView.ToolTip = _name;
	}

	private void ReadProfile()
	{
		try
		{
			ConnectionProfile internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
			if ((object)internetConnectionProfile == null)
			{
				_online = false;
				return;
			}
			NetworkConnectivityLevel networkConnectivityLevel = internetConnectionProfile.GetNetworkConnectivityLevel();
			_online = networkConnectivityLevel != NetworkConnectivityLevel.None;
			_kind = (internetConnectionProfile.IsWlanConnectionProfile ? "wifi" : (internetConnectionProfile.IsWwanConnectionProfile ? "lte" : "eth"));
			string text = ((!internetConnectionProfile.IsWlanConnectionProfile) ? null : internetConnectionProfile.WlanConnectionProfileDetails?.GetConnectedSsid());
			_name = text ?? internetConnectionProfile.ProfileName ?? "";
			if (_name.Length > 18)
			{
				_name = _name.Substring(0, 17) + "…";
			}
			if (networkConnectivityLevel == NetworkConnectivityLevel.LocalAccess || networkConnectivityLevel == NetworkConnectivityLevel.ConstrainedInternetAccess)
			{
				_name += " (!)";
			}
		}
		catch (Exception ex)
		{
			Log.Error("Модуль network", ex);
			_online = true;
			_kind = "net";
		}
	}
}
