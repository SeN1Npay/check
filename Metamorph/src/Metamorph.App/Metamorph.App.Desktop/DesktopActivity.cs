using System;
using System.Drawing;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Desktop;

// Общий «пульт» для всего, что рисуется на рабочем столе: когда можно анимировать, а когда надо замереть.
internal static class DesktopActivity
{
	private static bool _initialized;

	private static bool _locked;

	private static bool _remote;

	private static bool _energySaver;

	// Экран заблокирован / сеанс отключён.
	public static bool Locked
	{
		get
		{
			Init();
			return _locked;
		}
	}

	// Удалённый рабочий стол: анимации идут по сети кадрами — выключаем.
	public static bool Remote
	{
		get
		{
			Init();
			return _remote;
		}
	}

	// Экономия заряда Windows.
	public static bool EnergySaver
	{
		get
		{
			Init();
			return _energySaver;
		}
	}

	public static bool CanAnimate => !Locked && !Remote && !EnergySaver;

	// Срабатывает на UI-потоке подписчика не гарантированно — подписчики сами маршалят через Dispatcher.
	public static event Action? Changed;

	// Монитор закрыт развёрнутым или полноэкранным окном.
	public static bool IsCovered(Display display)
	{
		Point? point = DesktopLayer.CoveredMonitor(maximized: true);
		if (point.HasValue)
		{
			return display.Contains(point.Value.X, point.Value.Y);
		}
		return false;
	}

	public static bool ShouldAnimate(Display display)
	{
		if (CanAnimate)
		{
			return !IsCovered(display);
		}
		return false;
	}

	private static readonly object InitGate = new object();

	// Первое обращение может прийти и с UI-потока, и с потока обоев одновременно.
	private static void Init()
	{
		if (_initialized)
		{
			return;
		}
		lock (InitGate)
		{
			if (_initialized)
			{
				return;
			}
			InitCore();
			_initialized = true;
		}
	}

	private static void InitCore()
	{
		try
		{
			_remote = System.Windows.Forms.SystemInformation.TerminalServerSession;
			SystemEvents.SessionSwitch += OnSessionSwitch;
		}
		catch (Exception ex)
		{
			Log.Error("Рабочий стол: события сеанса", ex);
		}
		try
		{
			_energySaver = global::Windows.System.Power.PowerManager.EnergySaverStatus == global::Windows.System.Power.EnergySaverStatus.On;
			global::Windows.System.Power.PowerManager.EnergySaverStatusChanged += delegate
			{
				Set(ref _energySaver, global::Windows.System.Power.PowerManager.EnergySaverStatus == global::Windows.System.Power.EnergySaverStatus.On, "energysaver");
			};
		}
		catch (Exception ex2)
		{
			Log.Error("Рабочий стол: экономия заряда", ex2);
		}
	}

	private static void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
	{
		switch (e.Reason)
		{
		case SessionSwitchReason.SessionLock:
		case SessionSwitchReason.ConsoleDisconnect:
			Set(ref _locked, value: true, "lock");
			break;
		case SessionSwitchReason.SessionUnlock:
		case SessionSwitchReason.ConsoleConnect:
			Set(ref _locked, value: false, "lock");
			break;
		case SessionSwitchReason.RemoteConnect:
			Set(ref _remote, value: true, "remote");
			break;
		case SessionSwitchReason.RemoteDisconnect:
			Set(ref _remote, System.Windows.Forms.SystemInformation.TerminalServerSession, "remote");
			break;
		}
	}

	private static void Set(ref bool field, bool value, string what)
	{
		if (field == value)
		{
			return;
		}
		field = value;
		EventTrace.Mark("desktop.activity", $"{what}={value}");
		try
		{
			DesktopActivity.Changed?.Invoke();
		}
		catch (Exception ex)
		{
			Log.Error("Рабочий стол: подписчик активности", ex);
		}
	}
}
