using System;
using System.Diagnostics;
using System.Windows;
using Metamorph.Core.Localization;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal static class PowerActions
{
	public static void Lock()
	{
		Win32.LockWorkStation();
	}

	public static void Sleep()
	{
		Win32.SetSuspendState(hibernate: false, force: false, disableWakeEvent: false);
	}

	public static void SignOut()
	{
		if (Confirm(Loc.T("Выйти из системы?")))
		{
			Win32.ExitWindowsEx(0u, 0u);
		}
	}

	public static void Restart()
	{
		if (Confirm(Loc.T("Перезагрузить компьютер?")))
		{
			Run("/r /t 0");
		}
	}

	public static void Shutdown()
	{
		if (Confirm(Loc.T("Выключить компьютер?")))
		{
			Run("/s /t 0");
		}
	}

	private static bool Confirm(string question)
	{
		return MessageBox.Show(question, "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
	}

	private static void Run(string args)
	{
		try
		{
			Process.Start(new ProcessStartInfo("shutdown.exe", args)
			{
				CreateNoWindow = true,
				UseShellExecute = false
			});
		}
		catch (Exception ex)
		{
			Log.Error("shutdown.exe", ex);
		}
	}
}
