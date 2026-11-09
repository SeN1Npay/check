using System;
using System.Diagnostics;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

internal static class Settings
{
	public static void Open(string uri)
	{
		try
		{
			Process.Start(new ProcessStartInfo(uri)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Error("Открытие " + uri, ex);
		}
	}
}
