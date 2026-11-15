using System;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class DesktopIcons
{
	private const int ToggleDesktopIcons = 29698;

	public static void Apply(bool? hide, SystemSnapshot? original)
	{
		bool flag;
		if (hide.HasValue)
		{
			bool valueOrDefault = hide.GetValueOrDefault();
			Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideIcons", valueOrDefault);
			flag = valueOrDefault;
		}
		else
		{
			RegistryValueSnapshot registryValueSnapshot = original?.Find("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideIcons");
			if (registryValueSnapshot == null)
			{
				return;
			}
			SettingsBackup.RestoreValue(registryValueSnapshot);
			int? dword = registryValueSnapshot.Dword;
			flag = dword.HasValue && dword.GetValueOrDefault() == 1;
		}
		SetVisible(!flag);
	}

	public static bool? AreVisible()
	{
		nint num = FindFolderView();
		if (num != IntPtr.Zero)
		{
			return Win32.IsWindowVisible(num);
		}
		return null;
	}

	public static void SetVisible(bool visible)
	{
		bool? flag = AreVisible();
		if (flag.HasValue && flag != visible)
		{
			nint num = FindDefView();
			if (num != IntPtr.Zero)
			{
				Win32.SendMessageTimeout(num, 273, new IntPtr(29698), IntPtr.Zero, 2u, 1000u, out var _);
			}
		}
	}

	public static nint FindDefView()
	{
		nint num = Win32.FindWindowEx(Win32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
		if (num != IntPtr.Zero)
		{
			return num;
		}
		nint found = IntPtr.Zero;
		Win32.EnumWindows(delegate(nint hwnd, nint _)
		{
			if (Win32.ClassOf(hwnd) != "WorkerW")
			{
				return true;
			}
			nint num2 = Win32.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
			if (num2 == IntPtr.Zero)
			{
				return true;
			}
			found = num2;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	private static nint FindFolderView()
	{
		nint num = FindDefView();
		if (num != IntPtr.Zero)
		{
			return Win32.FindWindowEx(num, IntPtr.Zero, "SysListView32", null);
		}
		return IntPtr.Zero;
	}
}
