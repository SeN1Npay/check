using System;
using ManagedShell.WindowsTray;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal static class TrayHost
{
	private static TrayService? _trayService;

	private static NotificationArea? _area;

	private static bool _failed;

	public static NotificationArea? GetOrCreate()
	{
		if (_area != null || _failed)
		{
			return _area;
		}
		try
		{
			_trayService = new TrayService();
			ExplorerTrayService explorerTrayService = new ExplorerTrayService();
			_area = new NotificationArea(NotificationArea.DEFAULT_PINNED, _trayService, explorerTrayService);
			_area.Initialize();
			if (_area.IsFailed)
			{
				Log.Warn("Трей: ManagedShell не смог запустить область уведомлений.");
			}
			else
			{
				Log.Info("Трей: область уведомлений запущена.");
			}
		}
		catch (Exception ex)
		{
			Log.Error("Трей недоступен", ex);
			_failed = true;
			_area = null;
		}
		return _area;
	}

	public static void Shutdown()
	{
		try
		{
			_area?.Dispose();
		}
		catch (Exception ex)
		{
			Log.Error("Остановка трея", ex);
		}
		_area = null;
		_trayService = null;
	}
}
