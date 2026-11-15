using System;
using System.Text;
using Metamorph.Core.Native;

namespace Metamorph.Core.Dock;

public static class WindowActions
{
	public static void Activate(nint hwnd)
	{
		if (ShellNative.IsIconic(hwnd))
		{
			ShellNative.ShowWindowAsync(hwnd, 9);
		}
		nint num = ShellNative.GetLastActivePopup(hwnd);
		if (num == IntPtr.Zero || !NativeMethods.IsWindowVisible(num))
		{
			num = hwnd;
		}
		if (!ShellNative.SetForegroundWindow(num))
		{
			ShellNative.SwitchToThisWindow(num, altTab: true);
		}
	}

	public static void Minimize(nint hwnd)
	{
		ShellNative.PostMessage(hwnd, 274, new IntPtr(61472), IntPtr.Zero);
	}

	public static void Close(nint hwnd)
	{
		ShellNative.PostMessage(hwnd, 274, new IntPtr(61536), IntPtr.Zero);
	}

	public static nint GetWindowIcon(nint hwnd)
	{
		int[] array = new int[3] { 1, 2, 0 };
		foreach (int value in array)
		{
			if (ShellNative.SendMessageTimeout(hwnd, 127, new IntPtr(value), IntPtr.Zero, 2u, 60u, out var result) != IntPtr.Zero && result != IntPtr.Zero)
			{
				return result;
			}
		}
		nint classLongPtr = ShellNative.GetClassLongPtr(hwnd, -14);
		if (classLongPtr == IntPtr.Zero)
		{
			return ShellNative.GetClassLongPtr(hwnd, -34);
		}
		return classLongPtr;
	}

	public static void OpenStartMenu()
	{
		ShellNative.keybd_event(91, 0, 0u, UIntPtr.Zero);
		ShellNative.keybd_event(91, 0, 2u, UIntPtr.Zero);
	}

	public static bool IsFullscreen(nint hwnd)
	{
		if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd))
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(64);
		ShellNative.GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);
		bool flag;
		switch (stringBuilder.ToString())
		{
		case "Progman":
		case "WorkerW":
		case "Shell_TrayWnd":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return false;
		}
		if (!ShellNative.GetWindowRect(hwnd, out var rect))
		{
			return false;
		}
		nint monitor = ShellNative.MonitorFromWindow(hwnd, 2u);
		ShellNative.MONITORINFO info = ShellNative.MONITORINFO.Create();
		if (!ShellNative.GetMonitorInfo(monitor, ref info))
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
}
