using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Metamorph.Core.Localization;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public static class PrivacyUsage
{
	public const string ConsentStore = "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore";

	public const string Microphone = "microphone";

	public const string Camera = "webcam";

	public static string DisplayName(string key, bool packaged)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return "?";
		}
		if (!packaged)
		{
			string text;
			int num = (text = key.Replace('#', '\\')).LastIndexOf('\\') + 1;
			string text2 = text.Substring(num, text.Length - num);
			int num2 = text2.LastIndexOf('.');
			string text3 = ((num2 > 0) ? text2.Substring(0, num2) : text2);
			if (text3.Length != 0)
			{
				return Pretty(text3);
			}
			return key;
		}
		string text4 = key;
		int num3 = text4.IndexOf('_');
		if (num3 > 0)
		{
			text4 = text4.Substring(0, num3);
		}
		int num4 = text4.LastIndexOf('.');
		if (num4 >= 0 && num4 < text4.Length - 1)
		{
			string text = text4;
			int num = num4 + 1;
			text4 = text.Substring(num, text.Length - num);
		}
		return Pretty(text4);
	}

	private static string Pretty(string name)
	{
		switch (name.ToLowerInvariant())
		{
		case "windowscamera":
			return Loc.T("Камера");
		case "soundrecorder":
		case "windowssoundrecorder":
			return Loc.T("Звукозапись");
		case "msteams":
		case "ms-teams":
		case "teams":
			return "Teams";
		case "discord":
			return "Discord";
		case "zoom":
			return "Zoom";
		case "telegram":
			return "Telegram";
		case "obs32":
		case "obs64":
		case "obs":
			return "OBS";
		case "chrome":
			return "Chrome";
		case "msedge":
			return "Edge";
		case "firefox":
			return "Firefox";
		case "skype":
			return "Skype";
		default:
		{
			string result;
			if (name.Length <= 0)
			{
				result = name;
			}
			else
			{
				char reference = char.ToUpperInvariant(name[0]);
				result = string.Concat(new ReadOnlySpan<char>(ref reference), name.Substring(1, name.Length - 1));
			}
			return result;
		}
		}
	}

	public static IReadOnlyList<string> InUse(IEnumerable<CapabilityEntry> entries, Func<string, bool>? ignore = null)
	{
		Func<string, bool> ignore2 = ignore;
		return (from e in entries
			where e.InUse
			select DisplayName(e.Key, e.Packaged) into n
			where ignore2 == null || !ignore2(n)
			select n).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy<string, string>((string n) => n, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static IReadOnlyList<CapabilityEntry> Read(string capability)
	{
		List<CapabilityEntry> list = new List<CapabilityEntry>();
		if (!OperatingSystem.IsWindows())
		{
			return list;
		}
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\" + capability);
			if (registryKey == null)
			{
				return list;
			}
			string[] subKeyNames = registryKey.GetSubKeyNames();
			foreach (string text in subKeyNames)
			{
				if (text.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
				{
					using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
					if (registryKey2 == null)
					{
						continue;
					}
					string[] subKeyNames2 = registryKey2.GetSubKeyNames();
					foreach (string name in subKeyNames2)
					{
						CapabilityEntry capabilityEntry = ReadEntry(registryKey2, name, packaged: false);
						if ((object)capabilityEntry != null)
						{
							list.Add(capabilityEntry);
						}
					}
				}
				else
				{
					CapabilityEntry capabilityEntry2 = ReadEntry(registryKey, text, packaged: true);
					if ((object)capabilityEntry2 != null)
					{
						list.Add(capabilityEntry2);
					}
				}
			}
		}
		catch (Exception ex) when (((ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException) ? 1 : 0) != 0)
		{
			Log.Warn("Индикаторы приватности: " + ex.Message);
		}
		return list;
	}

	private static CapabilityEntry? ReadEntry(RegistryKey parent, string name, bool packaged)
	{
		if (!OperatingSystem.IsWindows())
		{
			return null;
		}
		using RegistryKey registryKey = parent.OpenSubKey(name);
		if (registryKey == null)
		{
			return null;
		}
		return new CapabilityEntry(name, packaged, ToLong(registryKey.GetValue("LastUsedTimeStart")), ToLong(registryKey.GetValue("LastUsedTimeStop")));
	}

	private static long ToLong(object? value)
	{
		if (!(value is long result))
		{
			if (!(value is int num))
			{
				if (value is byte[] array && array.Length >= 8)
				{
					return BitConverter.ToInt64(array, 0);
				}
				return 0L;
			}
			return (uint)num;
		}
		return result;
	}

	public static PrivacySnapshot Snapshot(Func<string, bool>? ignore = null)
	{
		return new PrivacySnapshot(InUse(Read("microphone"), ignore), InUse(Read("webcam"), ignore));
	}
}
