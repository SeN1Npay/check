using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Metamorph.Core.Native;

public static class Win32
{
	public delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time);

	public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

	public struct DWM_THUMBNAIL_PROPERTIES
	{
		public int dwFlags;

		public NativeMethods.RECT rcDestination;

		public NativeMethods.RECT rcSource;

		public byte opacity;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fVisible;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fSourceClientAreaOnly;
	}

	public struct SIZE
	{
		public int cx;

		public int cy;
	}

	public struct POINT
	{
		public int X;

		public int Y;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct MONITORINFO
	{
		public int cbSize;

		public NativeMethods.RECT rcMonitor;

		public NativeMethods.RECT rcWork;

		public uint dwFlags;

		public static MONITORINFO Create()
		{
			MONITORINFO result = default(MONITORINFO);
			result.cbSize = Marshal.SizeOf<MONITORINFO>();
			return result;
		}
	}

	public struct LASTINPUTINFO
	{
		public uint cbSize;

		public uint dwTime;
	}

	public const int WM_COMMAND = 273;

	public const int WM_HOTKEY = 786;

	public const int WM_THEMECHANGED = 794;

	public const int WM_CLOSE = 16;

	public const int WM_SYSCOMMAND = 274;

	public const int SC_CLOSE = 61536;

	public const int SC_MINIMIZE = 61472;

	public const int SC_RESTORE = 61728;

	public const int SW_RESTORE = 9;

	public const int SW_MAXIMIZE = 3;

	public const int SW_SHOWNOACTIVATE = 4;

	public const uint SPI_SETCURSORS = 87u;

	public const uint SPI_GETWORKAREA = 48u;

	public const uint MOD_ALT = 1u;

	public const uint MOD_CONTROL = 2u;

	public const uint MOD_SHIFT = 4u;

	public const uint MOD_WIN = 8u;

	public const uint MOD_NOREPEAT = 16384u;

	public const uint EVENT_SYSTEM_FOREGROUND = 3u;

	public const uint EVENT_SYSTEM_MOVESIZESTART = 10u;

	public const uint EVENT_SYSTEM_MOVESIZEEND = 11u;

	public const uint EVENT_SYSTEM_MINIMIZESTART = 22u;

	public const uint EVENT_SYSTEM_MINIMIZEEND = 23u;

	public const uint EVENT_OBJECT_DESTROY = 32769u;

	public const uint EVENT_OBJECT_SHOW = 32770u;

	public const uint EVENT_OBJECT_HIDE = 32771u;

	public const uint EVENT_OBJECT_LOCATIONCHANGE = 32779u;

	public const uint EVENT_OBJECT_CLOAKED = 32791u;

	public const uint EVENT_OBJECT_UNCLOAKED = 32792u;

	public const uint WINEVENT_OUTOFCONTEXT = 0u;

	public const uint WINEVENT_SKIPOWNPROCESS = 2u;

	public const int OBJID_WINDOW = 0;

	public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

	public const int DWMWA_CLOAKED = 14;

	public const uint GA_ROOT = 2u;

	public const uint GA_ROOTOWNER = 3u;

	public const int GWL_STYLE = -16;

	public const long WS_CHILD = 1073741824L;

	public const long WS_VISIBLE = 268435456L;

	public const long WS_MAXIMIZEBOX = 65536L;

	public const long WS_THICKFRAME = 262144L;

	public const long WS_EX_TRANSPARENT = 32L;

	public const long WS_EX_LAYERED = 524288L;

	public const long WS_EX_TOPMOST = 8L;

	public const uint SWP_NOZORDER = 4u;

	public const uint SWP_NOOWNERZORDER = 512u;

	public const uint SWP_FRAMECHANGED = 32u;

	public const uint SWP_ASYNCWINDOWPOS = 16384u;

	public const uint SWP_HIDEWINDOW = 128u;

	public const uint MONITOR_DEFAULTTONEAREST = 2u;

	public const uint RDW_INVALIDATE = 1u;

	public const uint RDW_ERASE = 4u;

	public const uint RDW_ALLCHILDREN = 128u;

	public const uint RDW_UPDATENOW = 256u;

	public const uint RDW_FRAME = 1024u;

	public const int DWM_TNP_RECTDESTINATION = 1;

	public const int DWM_TNP_RECTSOURCE = 2;

	public const int DWM_TNP_OPACITY = 4;

	public const int DWM_TNP_VISIBLE = 8;

	public const int DWM_TNP_SOURCECLIENTAREAONLY = 16;

	public const byte VK_CONTROL = 17;

	public const byte VK_MENU = 18;

	public const byte VK_LWIN = 91;

	public const byte VK_LEFT = 37;

	public const byte VK_RIGHT = 39;

	public const byte VK_D = 68;

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	public static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc proc, uint pid, uint thread, uint flags);

	[DllImport("user32.dll")]
	public static extern bool UnhookWinEvent(nint hook);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool SystemParametersInfo(uint action, uint param, nint pv, uint winIni);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern nint SendMessageTimeout(nint hWnd, int msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

	[DllImport("user32.dll")]
	public static extern nint GetAncestor(nint hwnd, uint flags);

	[DllImport("user32.dll")]
	public static extern nint GetParent(nint hwnd);

	[DllImport("user32.dll")]
	public static extern nint SetParent(nint child, nint parent);

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(nint hwnd, int cmd);

	[DllImport("user32.dll")]
	public static extern bool IsZoomed(nint hwnd);

	[DllImport("user32.dll")]
	public static extern bool IsIconic(nint hwnd);

	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);

	[DllImport("user32.dll")]
	public static extern bool IsWindow(nint hwnd);

	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(nint hwnd, out NativeMethods.RECT rect);

	[DllImport("user32.dll")]
	public static extern bool GetClientRect(nint hwnd, out NativeMethods.RECT rect);

	[DllImport("user32.dll")]
	public static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	public static extern bool MoveWindow(nint hwnd, int x, int y, int w, int h, bool repaint);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetClassName(nint hwnd, StringBuilder name, int max);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);

	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumWindowsProc proc, nint lParam);

	[DllImport("user32.dll")]
	public static extern bool EnumChildWindows(nint parent, EnumWindowsProc proc, nint lParam);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

	[DllImport("user32.dll")]
	public static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(nint hwnd);

	[DllImport("user32.dll")]
	public static extern nint MonitorFromWindow(nint hwnd, uint flags);

	[DllImport("user32.dll")]
	public static extern nint MonitorFromPoint(POINT pt, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

	[DllImport("user32.dll")]
	public static extern bool GetCursorPos(out POINT pt);

	[DllImport("user32.dll")]
	public static extern nint CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

	[DllImport("user32.dll")]
	public static extern int SetWindowRgn(nint hwnd, nint rgn, bool redraw);

	[DllImport("user32.dll")]
	public static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

	[DllImport("user32.dll")]
	public static extern bool LockWorkStation();

	[DllImport("user32.dll")]
	public static extern bool ExitWindowsEx(uint flags, uint reason);

	[DllImport("powrprof.dll")]
	public static extern bool SetSuspendState(bool hibernate, bool force, bool disableWakeEvent);

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(nint hwnd, int attr, out NativeMethods.RECT value, int size);

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);

	[DllImport("dwmapi.dll")]
	public static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

	[DllImport("dwmapi.dll")]
	public static extern int DwmUnregisterThumbnail(nint thumbnail);

	[DllImport("dwmapi.dll")]
	public static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

	[DllImport("dwmapi.dll")]
	public static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out SIZE size);

	public static string ClassOf(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		if (GetClassName(hwnd, stringBuilder, stringBuilder.Capacity) <= 0)
		{
			return "";
		}
		return stringBuilder.ToString();
	}

	public static bool IsCloaked(nint hwnd)
	{
		if (DwmGetWindowAttribute(hwnd, 14, out int value, 4) == 0)
		{
			return value != 0;
		}
		return false;
	}

	public static bool TryGetFrameBounds(nint hwnd, out NativeMethods.RECT rect)
	{
		if (DwmGetWindowAttribute(hwnd, 9, out rect, Marshal.SizeOf<NativeMethods.RECT>()) == 0)
		{
			return true;
		}
		return GetWindowRect(hwnd, out rect);
	}

	public static NativeMethods.RECT WorkAreaOf(nint hwnd)
	{
		MONITORINFO info = MONITORINFO.Create();
		GetMonitorInfo(MonitorFromWindow(hwnd, 2u), ref info);
		return info.rcWork;
	}

	public static void SendChord(params byte[] keys)
	{
		foreach (byte vk in keys)
		{
			keybd_event(vk, 0, IsExtended(vk) ? 1u : 0u, UIntPtr.Zero);
		}
		for (int num = keys.Length - 1; num >= 0; num--)
		{
			keybd_event(keys[num], 0, 2u | (IsExtended(keys[num]) ? 1u : 0u), UIntPtr.Zero);
		}
	}

	private static bool IsExtended(byte vk)
	{
		if ((uint)(vk - 37) <= 3u || (uint)(vk - 91) <= 1u)
		{
			return true;
		}
		return false;
	}

	[DllImport("user32.dll")]
	public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

	public static double IdleSeconds()
	{
		try
		{
			LASTINPUTINFO lASTINPUTINFO = default(LASTINPUTINFO);
			lASTINPUTINFO.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>();
			LASTINPUTINFO info = lASTINPUTINFO;
			if (!GetLastInputInfo(ref info))
			{
				return 0.0;
			}
			return (double)((uint)Environment.TickCount - info.dwTime) / 1000.0;
		}
		catch
		{
			return 0.0;
		}
	}
}
