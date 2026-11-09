using System;
using System.Collections.Generic;
using System.Drawing;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal static class DesktopLayer
{
	private const int SpawnWorkerW = 1324;

	private const uint SMTO_NORMAL = 0u;

	private static readonly HashSet<string> ShellClasses = new HashSet<string>(StringComparer.Ordinal)
	{
		"WorkerW", "Progman", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "MultitaskingViewFrame", "XamlExplorerHostIslandWindow", "Windows.UI.Core.CoreWindow", "ForegroundStaging", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
		"#32768", "Metamorph.Wallpaper"
	};

	public static nint FindWorkerW()
	{
		nint num = Win32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
		if (num == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		EventTrace.Mark("workerw.spawn");
		Win32.SendMessageTimeout(num, 1324, IntPtr.Zero, IntPtr.Zero, 0u, 1000u, out var _);
		nint workerW = IntPtr.Zero;
		Win32.EnumWindows(delegate(nint top, nint _)
		{
			if (Win32.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
			{
				workerW = Win32.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
			}
			return true;
		}, IntPtr.Zero);
		return workerW;
	}

	public static Point ParentOrigin(nint workerW)
	{
		if (!Win32.GetWindowRect(workerW, out var rect))
		{
			return Point.Empty;
		}
		return new Point(rect.Left, rect.Top);
	}

	private static bool IsShellWindow(nint hwnd)
	{
		string item = Win32.ClassOf(hwnd);
		if (ShellClasses.Contains(item))
		{
			return true;
		}
		try
		{
			Win32.GetWindowThreadProcessId(hwnd, out var pid);
			if (pid == (uint)Environment.ProcessId)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsFullscreenAppActive()
	{
		nint foregroundWindow = Win32.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero || IsShellWindow(foregroundWindow))
		{
			return false;
		}
		return CoversMonitor(foregroundWindow);
	}

	private static bool CoversMonitor(nint hwnd)
	{
		if (!Win32.GetWindowRect(hwnd, out var rect))
		{
			return false;
		}
		nint monitor = Win32.MonitorFromWindow(hwnd, 2u);
		Win32.MONITORINFO info = Win32.MONITORINFO.Create();
		if (!Win32.GetMonitorInfo(monitor, ref info))
		{
			return false;
		}
		NativeMethods.RECT rcMonitor = info.rcMonitor;
		if (rect.Left <= rcMonitor.Left && rect.Top <= rcMonitor.Top && rect.Right >= rcMonitor.Right)
		{
			return rect.Bottom >= rcMonitor.Bottom;
		}
		return false;
	}

	public static Point? CoveredMonitor(bool maximized)
	{
		nint foregroundWindow = Win32.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero || IsShellWindow(foregroundWindow))
		{
			return null;
		}
		if (!CoversMonitor(foregroundWindow) && (!maximized || !Win32.IsZoomed(foregroundWindow) || !Win32.IsWindowVisible(foregroundWindow) || Win32.IsIconic(foregroundWindow)))
		{
			return null;
		}
		nint monitor = Win32.MonitorFromWindow(foregroundWindow, 2u);
		Win32.MONITORINFO info = Win32.MONITORINFO.Create();
		if (!Win32.GetMonitorInfo(monitor, ref info))
		{
			return null;
		}
		NativeMethods.RECT rcMonitor = info.rcMonitor;
		return new Point((rcMonitor.Left + rcMonitor.Right) / 2, (rcMonitor.Top + rcMonitor.Bottom) / 2);
	}

	// Видимое окно переднего плана (не оболочка и не Metamorph), пересекающее область в физических пикселях.
	public static bool ForegroundIntersects(Rectangle area)
	{
		nint fg = Win32.GetForegroundWindow();
		if (fg == IntPtr.Zero || IsShellWindow(fg) || !Win32.IsWindowVisible(fg) || Win32.IsIconic(fg) || Win32.IsCloaked(fg))
		{
			return false;
		}
		if (!Win32.TryGetFrameBounds(fg, out var r) && !Win32.GetWindowRect(fg, out r))
		{
			return false;
		}
		return r.Left < area.Right && r.Right > area.Left && r.Top < area.Bottom && r.Bottom > area.Top;
	}

	public static bool IsDesktopForeground()
	{
		string text = Win32.ClassOf(Win32.GetForegroundWindow());
		if (text == "WorkerW" || text == "Progman")
		{
			return true;
		}
		return false;
	}
}
