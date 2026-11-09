using System;
using System.Collections.Generic;
using Metamorph.Core.Models;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class TaskbarController
{
	private const string PrimaryClass = "Shell_TrayWnd";

	private const string SecondaryClass = "Shell_SecondaryTrayWnd";

	public static void Apply(TaskbarMode mode)
	{
		switch (mode)
		{
		case TaskbarMode.Normal:
			SetAutoHide(autoHide: false);
			SetVisible(visible: true);
			break;
		case TaskbarMode.AutoHide:
			SetAutoHide(autoHide: true);
			SetVisible(visible: true);
			break;
		case TaskbarMode.Hidden:
			SetAutoHide(autoHide: true);
			SetVisible(visible: false);
			break;
		}
	}

	public static bool GetAutoHide()
	{
		NativeMethods.APPBARDATA pData = NativeMethods.APPBARDATA.Create(FindExplorerTaskbar());
		return (((UIntPtr)NativeMethods.SHAppBarMessage(4u, ref pData)).ToUInt64() & 1) != 0;
	}

	public static void SetAutoHide(bool autoHide)
	{
		NativeMethods.APPBARDATA pData = NativeMethods.APPBARDATA.Create(FindExplorerTaskbar());
		pData.lParam = new IntPtr(autoHide ? 1 : 2);
		NativeMethods.SHAppBarMessage(10u, ref pData);
	}

	public static bool IsVisible()
	{
		nint num = FindExplorerTaskbar();
		if (num != IntPtr.Zero)
		{
			return NativeMethods.IsWindowVisible(num);
		}
		return false;
	}

	public static void SetVisible(bool visible)
	{
		foreach (nint item in EnumerateTaskbars())
		{
			NativeMethods.ShowWindow(item, visible ? 8 : 0);
		}
	}

	public static void SafeShow()
	{
		try
		{
			SetVisible(visible: true);
		}
		catch
		{
		}
	}

	public static nint FindExplorerTaskbar()
	{
		uint processId = (uint)Environment.ProcessId;
		nint num = IntPtr.Zero;
		while ((num = NativeMethods.FindWindowEx(IntPtr.Zero, num, "Shell_TrayWnd", null)) != IntPtr.Zero)
		{
			NativeMethods.GetWindowThreadProcessId(num, out var processId2);
			if (processId2 != processId)
			{
				return num;
			}
		}
		return IntPtr.Zero;
	}

	public static NativeMethods.RECT? GetVisibleTaskbarRect()
	{
		nint num = FindExplorerTaskbar();
		if (num == IntPtr.Zero || !NativeMethods.IsWindowVisible(num) || GetAutoHide())
		{
			return null;
		}
		if (!ShellNative.GetWindowRect(num, out var rect))
		{
			return null;
		}
		return rect;
	}

	private static IEnumerable<nint> EnumerateTaskbars()
	{
		nint num = FindExplorerTaskbar();
		if (num != IntPtr.Zero)
		{
			yield return num;
		}
		nint secondary = IntPtr.Zero;
		while (true)
		{
			nint num2;
			secondary = (num2 = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null));
			if (num2 != IntPtr.Zero)
			{
				yield return secondary;
				continue;
			}
			break;
		}
	}
}
