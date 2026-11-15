using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class VirtualDesktops
{
	private const string Key = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops";

	public static VirtualDesktopInfo Read()
	{
		List<Guid> list = ParseIds(Reg.Get("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops", "VirtualDesktopIDs") as byte[]);
		byte[] array = (Reg.Get("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops", "CurrentVirtualDesktop") as byte[]) ?? (Reg.Get($"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\{SessionId()}\\VirtualDesktops", "CurrentVirtualDesktop") as byte[]);
		if (list.Count == 0)
		{
			return new VirtualDesktopInfo(1, 0, new string[1] { "1" });
		}
		int val = ((array != null && array.Length == 16) ? list.IndexOf(new Guid(array)) : 0);
		List<string> names = list.Select((Guid id, int i) => (!(Reg.Get($"{"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops"}\\Desktops\\{id:B}", "Name") is string { Length: >0 } text)) ? (i + 1).ToString() : text).ToList();
		return new VirtualDesktopInfo(list.Count, Math.Max(0, val), names);
	}

	private static int SessionId()
	{
		using Process process = Process.GetCurrentProcess();
		return process.SessionId;
	}

	public static List<Guid> ParseIds(byte[]? data)
	{
		List<Guid> list = new List<Guid>();
		if (data == null)
		{
			return list;
		}
		for (int i = 0; i + 16 <= data.Length; i += 16)
		{
			list.Add(new Guid(data.AsSpan(i, 16)));
		}
		return list;
	}

	public static int StepsTo(int current, int target)
	{
		return target - current;
	}

	public static void SwitchTo(int target)
	{
		VirtualDesktopInfo virtualDesktopInfo = Read();
		int num = StepsTo(virtualDesktopInfo.Current, Math.Clamp(target, 0, virtualDesktopInfo.Count - 1));
		byte b = (byte)((num > 0) ? 39 : 37);
		for (int i = 0; i < Math.Abs(num); i++)
		{
			Win32.SendChord(17, 91, b);
			Thread.Sleep(60);
		}
	}

	public static void Next()
	{
		Win32.SendChord(17, 91, 39);
	}

	public static void Previous()
	{
		Win32.SendChord(17, 91, 37);
	}

	public static void Create()
	{
		Win32.SendChord(17, 91, 68);
	}
}
