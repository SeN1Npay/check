using System;
using System.Diagnostics;
using System.Threading;

namespace Metamorph.Core.Services;

public static class ExplorerService
{
	public static void Restart()
	{
		Process[] processesByName = Process.GetProcessesByName("explorer");
		foreach (Process process in processesByName)
		{
			try
			{
				process.Kill();
				process.WaitForExit(5000);
			}
			catch (Exception ex)
			{
				Log.Warn("Не удалось завершить explorer.exe: " + ex.Message);
			}
			finally
			{
				process.Dispose();
			}
		}
		Thread.Sleep(1500);
		if (Process.GetProcessesByName("explorer").Length == 0)
		{
			Process.Start(new ProcessStartInfo("explorer.exe")
			{
				UseShellExecute = true
			});
		}
	}
}
