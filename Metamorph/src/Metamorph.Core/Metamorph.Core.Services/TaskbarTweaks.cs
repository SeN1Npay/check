using System;
using Metamorph.Core.Models;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class TaskbarTweaks
{
	public static void Apply(TaskbarSettings t, SystemSnapshot? original)
	{
		SystemSnapshot original2 = original;
		Set("Software\\Microsoft\\Windows\\CurrentVersion\\Search", "SearchboxTaskbarMode", t.HideSearch, (bool hide) => (!hide) ? VisibleSearchMode(original2) : 0u, original2);
		Set("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowTaskViewButton", t.HideTaskView, (bool hide) => (!hide) ? 1u : 0u, original2);
		Set("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowCortanaButton", t.HideCortana, (bool hide) => (!hide) ? 1u : 0u, original2);
		Set("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\People", "PeopleBand", t.HidePeople, (bool hide) => (!hide) ? 1u : 0u, original2);
		Set("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarSmallIcons", t.SmallIcons, (bool small) => small ? 1u : 0u, original2);
		try
		{
			Set("Software\\Microsoft\\Windows\\CurrentVersion\\Feeds", "ShellFeedsTaskbarViewMode", t.HideNews, (bool hide) => hide ? 2u : 0u, original2);
		}
		catch (UnauthorizedAccessException)
		{
			Log.Warn("Новости и интересы: запись в реестр запрещена системой.");
		}
		TaskbarGrouping? grouping = t.Grouping;
		if (grouping.HasValue)
		{
			TaskbarGrouping valueOrDefault = grouping.GetValueOrDefault();
			uint value = GlomLevel(valueOrDefault);
			Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarGlomLevel", value);
			Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "MMTaskbarGlomLevel", value);
		}
		else
		{
			RestoreOne(original2, "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarGlomLevel");
			RestoreOne(original2, "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "MMTaskbarGlomLevel");
		}
		NotifyExplorer();
	}

	public static uint GlomLevel(TaskbarGrouping grouping)
	{
		return grouping switch
		{
			TaskbarGrouping.Always => 0u, 
			TaskbarGrouping.WhenFull => 1u, 
			_ => 2u, 
		};
	}

	public static void NotifyExplorer()
	{
		NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, 26, IntPtr.Zero, "TraySettings", 2u, 500u, out var lpdwResult);
		NativeMethods.SendMessageTimeout(NativeMethods.HWND_BROADCAST, 26, IntPtr.Zero, "Policy", 2u, 300u, out lpdwResult);
	}

	private static uint VisibleSearchMode(SystemSnapshot? original)
	{
		int? num = original?.Find("Software\\Microsoft\\Windows\\CurrentVersion\\Search", "SearchboxTaskbarMode")?.Dword;
		if (!num.HasValue || num.GetValueOrDefault() <= 0)
		{
			return 1u;
		}
		return (uint)num.Value;
	}

	private static void Set(string key, string name, bool? value, Func<bool, uint> map, SystemSnapshot? original)
	{
		if (value.HasValue)
		{
			bool valueOrDefault = value.GetValueOrDefault();
			Reg.SetDword(key, name, map(valueOrDefault));
		}
		else
		{
			RestoreOne(original, key, name);
		}
	}

	private static void RestoreOne(SystemSnapshot? original, string key, string name)
	{
		RegistryValueSnapshot registryValueSnapshot = original?.Find(key, name);
		if (registryValueSnapshot != null)
		{
			SettingsBackup.RestoreValue(registryValueSnapshot);
		}
	}
}
