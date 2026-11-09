using System;
using System.Runtime.InteropServices;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal static class BroadcastTrace
{
	private const int WM_DWMCOMPOSITIONCHANGED = 798;

	private const int WM_DWMCOLORIZATIONCOLORCHANGED = 800;

	public static void Inspect(int msg, nint wParam, nint lParam)
	{
		switch (msg)
		{
		case 26:
		{
			string value = null;
			try
			{
				if (lParam != IntPtr.Zero)
				{
					value = Marshal.PtrToStringUni(lParam);
				}
			}
			catch
			{
			}
			EventTrace.Mark("broadcast.settingchange", $"{((IntPtr)wParam).ToInt64()} {value}");
			break;
		}
		case 21:
			EventTrace.Mark("broadcast.syscolor");
			break;
		case 794:
			EventTrace.Mark("broadcast.themechanged");
			break;
		case 800:
			EventTrace.Mark("broadcast.dwmcolor");
			break;
		case 798:
			EventTrace.Mark("broadcast.dwmcomposition");
			break;
		case 126:
			EventTrace.Mark("broadcast.displaychange", $"{((IntPtr)lParam).ToInt64() & 0xFFFF}x{(((IntPtr)lParam).ToInt64() >> 16) & 0xFFFF}");
			break;
		}
	}
}
