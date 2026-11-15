using System;
using System.Collections.Generic;
using Metamorph.Core.Native;

namespace Metamorph.Core.Windows;

public static class WindowMover
{
	public static IntRect ToRect(NativeMethods.RECT r)
	{
		return new IntRect(r.Left, r.Top, r.Right, r.Bottom);
	}

	public static IntRect VisibleBounds(nint hwnd)
	{
		if (!Win32.TryGetFrameBounds(hwnd, out var rect))
		{
			return default(IntRect);
		}
		return ToRect(rect);
	}

	public static IntRect WorkArea(nint hwnd)
	{
		return ToRect(Win32.WorkAreaOf(hwnd));
	}

	public static void MoveVisible(nint hwnd, IntRect target)
	{
		if (Win32.IsZoomed(hwnd) || Win32.IsIconic(hwnd))
		{
			Win32.ShowWindow(hwnd, 9);
		}
		Win32.GetWindowRect(hwnd, out var rect);
		IntRect intRect = VisibleBounds(hwnd);
		int num = intRect.Left - rect.Left;
		int num2 = intRect.Top - rect.Top;
		int num3 = rect.Right - intRect.Right;
		int num4 = rect.Bottom - intRect.Bottom;
		Win32.SetWindowPos(hwnd, IntPtr.Zero, target.Left - num, target.Top - num2, target.Width + num + num3, target.Height + num2 + num4, 532u);
	}

	public static bool IsTileable(nint hwnd)
	{
		if (hwnd == IntPtr.Zero || !Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd) || Win32.IsCloaked(hwnd))
		{
			return false;
		}
		long windowLong = NativeMethods.GetWindowLong(hwnd, -16);
		long windowLong2 = NativeMethods.GetWindowLong(hwnd, -20);
		if ((windowLong & 0x40000000) != 0L || (windowLong & 0x40000) == 0L)
		{
			return false;
		}
		if ((windowLong2 & 0x80) != 0L || (windowLong2 & 0x8000000) != 0L)
		{
			return false;
		}
		if (Win32.GetAncestor(hwnd, 3u) != hwnd && Win32.GetParent(hwnd) != IntPtr.Zero)
		{
			return false;
		}
		bool flag;
		switch (Win32.ClassOf(hwnd))
		{
		case "Shell_TrayWnd":
		case "Shell_SecondaryTrayWnd":
		case "Progman":
		case "WorkerW":
		case "Windows.UI.Core.CoreWindow":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		return !flag;
	}

	public static List<nint> TileableOnSameMonitor(nint reference)
	{
		nint monitor = Win32.MonitorFromWindow(reference, 2u);
		List<nint> list = new List<nint>();
		Win32.EnumWindows(delegate(nint hwnd, nint _)
		{
			if (IsTileable(hwnd) && Win32.MonitorFromWindow(hwnd, 2u) == monitor)
			{
				list.Add(hwnd);
			}
			return true;
		}, IntPtr.Zero);
		return list;
	}
}
