using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Metamorph.Core.Native;

public static class NativeMethods
{
	public enum AccentState
	{
		Disabled,
		EnableGradient,
		EnableTransparentGradient,
		EnableBlurBehind,
		EnableAcrylicBlurBehind
	}

	public struct AccentPolicy
	{
		public AccentState AccentState;

		public int AccentFlags;

		public uint GradientColor;

		public int AnimationId;
	}

	public struct WindowCompositionAttributeData
	{
		public int Attribute;

		public nint Data;

		public int SizeOfData;
	}

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public struct APPBARDATA
	{
		public int cbSize;

		public nint hWnd;

		public uint uCallbackMessage;

		public uint uEdge;

		public RECT rc;

		public nint lParam;

		public static APPBARDATA Create(nint hwnd)
		{
			APPBARDATA result = default(APPBARDATA);
			result.cbSize = Marshal.SizeOf<APPBARDATA>();
			result.hWnd = hwnd;
			return result;
		}
	}

	public struct MEMORYSTATUSEX
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;

		public static MEMORYSTATUSEX Create()
		{
			MEMORYSTATUSEX result = default(MEMORYSTATUSEX);
			result.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
			return result;
		}
	}

	public struct SYSTEM_POWER_STATUS
	{
		public byte ACLineStatus;

		public byte BatteryFlag;

		public byte BatteryLifePercent;

		public byte SystemStatusFlag;

		public int BatteryLifeTime;

		public int BatteryFullLifeTime;
	}

	public struct DWM_COLORIZATION_PARAMS
	{
		public uint clrColor;

		public uint clrAfterGlow;

		public uint nIntensity;

		public uint clrAfterGlowBalance;

		public uint clrBlurBalance;

		public uint clrGlassReflectionIntensity;

		public int fOpaque;
	}

	public const uint SPI_SETDESKWALLPAPER = 20u;

	public const uint SPI_GETDESKWALLPAPER = 115u;

	public const uint SPIF_UPDATEINIFILE = 1u;

	public const uint SPIF_SENDCHANGE = 2u;

	public const int WM_SETTINGCHANGE = 26;

	public const int WM_SYSCOLORCHANGE = 21;

	public const int WM_DISPLAYCHANGE = 126;

	public static readonly nint HWND_BROADCAST = new IntPtr(65535);

	public static readonly nint HWND_TOPMOST = new IntPtr(-1);

	public static readonly nint HWND_NOTOPMOST = new IntPtr(-2);

	public static readonly nint HWND_BOTTOM = new IntPtr(1);

	public const uint SMTO_ABORTIFHUNG = 2u;

	public const int SW_HIDE = 0;

	public const int SW_SHOWNA = 8;

	public const int COLOR_DESKTOP = 1;

	public const int SM_CXSCREEN = 0;

	public const int SM_CYSCREEN = 1;

	public const int SM_CXVIRTUALSCREEN = 78;

	public const int SM_CYVIRTUALSCREEN = 79;

	public const int GWL_EXSTYLE = -20;

	public const long WS_EX_TOOLWINDOW = 128L;

	public const long WS_EX_NOACTIVATE = 134217728L;

	public const uint SWP_NOSIZE = 1u;

	public const uint SWP_NOMOVE = 2u;

	public const uint SWP_NOACTIVATE = 16u;

	public const uint SWP_SHOWWINDOW = 64u;

	public const int WCA_ACCENT_POLICY = 19;

	public const uint ABM_NEW = 0u;

	public const uint ABM_REMOVE = 1u;

	public const uint ABM_QUERYPOS = 2u;

	public const uint ABM_SETPOS = 3u;

	public const uint ABM_GETSTATE = 4u;

	public const uint ABM_SETSTATE = 10u;

	public const uint ABE_LEFT = 0u;

	public const uint ABE_TOP = 1u;

	public const uint ABE_RIGHT = 2u;

	public const uint ABE_BOTTOM = 3u;

	public const int ABS_AUTOHIDE = 1;

	public const int ABS_ALWAYSONTOP = 2;

	public const int ABN_POSCHANGED = 1;

	public const int ABN_FULLSCREENAPP = 2;

	public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

	public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string? pvParam, uint fWinIni);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern nint SendMessageTimeout(nint hWnd, int msg, nint wParam, string? lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern nint FindWindow(string? lpClassName, string? lpWindowName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string? lpszClass, string? lpszWindow);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	public static extern nint GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool AddClipboardFormatListener(nint hwnd);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool RemoveClipboardFormatListener(nint hwnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	public static extern nint GetKeyboardLayout(uint idThread);

	[DllImport("user32.dll")]
	public static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetSysColors(int cElements, int[] lpaElements, int[] lpaRgbValues);

	[DllImport("user32.dll")]
	public static extern int GetSysColor(int nIndex);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern uint RegisterWindowMessage(string lpString);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
	private static extern int GetWindowLong32(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint value);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
	private static extern int SetWindowLong32(nint hWnd, int nIndex, int value);

	public static long GetWindowLong(nint hWnd, int index)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(hWnd, index);
		}
		return ((IntPtr)GetWindowLongPtr64(hWnd, index)).ToInt64();
	}

	public static void SetWindowLong(nint hWnd, int index, long value)
	{
		if (IntPtr.Size == 8)
		{
			SetWindowLongPtr64(hWnd, index, new IntPtr(value));
		}
		else
		{
			SetWindowLong32(hWnd, index, (int)value);
		}
	}

	[DllImport("user32.dll")]
	public static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

	[DllImport("shell32.dll")]
	public static extern nuint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

	[DllImport("dwmapi.dll")]
	public static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

	[DllImport("dwmapi.dll", EntryPoint = "#127")]
	public static extern int DwmGetColorizationParameters(out DWM_COLORIZATION_PARAMS parameters);

	[DllImport("dwmapi.dll", EntryPoint = "#131")]
	public static extern int DwmSetColorizationParameters(ref DWM_COLORIZATION_PARAMS parameters, uint unknown);
}
