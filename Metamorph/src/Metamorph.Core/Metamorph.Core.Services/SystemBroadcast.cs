using System;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class SystemBroadcast
{
	public static void SettingsChanged()
	{
		string[] array = new string[2] { "ImmersiveColorSet", "WindowsThemeElement" };
		nint lpdwResult;
		foreach (string lParam in array)
		{
			NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, 26, IntPtr.Zero, lParam, 2u, 300u, out lpdwResult);
		}
		NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, 21, IntPtr.Zero, null, 2u, 300u, out lpdwResult);
	}
}
