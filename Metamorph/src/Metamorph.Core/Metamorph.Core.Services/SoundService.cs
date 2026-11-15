using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public sealed class SoundService
{
	private readonly SettingsBackup _backup;

	public SoundService(SettingsBackup backup)
	{
		_backup = backup;
	}

	public static IEnumerable<(string CurrentKey, string Event)> Events()
	{
		foreach (string app in Reg.SubKeys("AppEvents\\Schemes\\Apps"))
		{
			foreach (string item in Reg.SubKeys("AppEvents\\Schemes\\Apps\\" + app))
			{
				yield return (CurrentKey: $"{"AppEvents\\Schemes\\Apps"}\\{app}\\{item}\\.Current", Event: item);
			}
		}
	}

	public static IEnumerable<TrackedValue> TrackedValues()
	{
		return (from e in Events()
			select new TrackedValue(e.CurrentKey, "", RegistryValueKind.ExpandString)).Append(new TrackedValue("AppEvents\\Schemes", "", RegistryValueKind.String));
	}

	public void Apply(SoundSettings s)
	{
		if (s.Scheme == SoundScheme.Keep)
		{
			RestoreOriginal(_backup.LoadOriginal());
			return;
		}
		_backup.EnsureCaptured(TrackedValues());
		SystemSnapshot systemSnapshot = _backup.LoadOriginal();
		Dictionary<string, string> dictionary = ((s.Scheme == SoundScheme.Custom) ? Wavs(s.Path) : new Dictionary<string, string>());
		foreach (var (key, key2) in Events())
		{
			if (s.Scheme == SoundScheme.Silent)
			{
				Reg.SetExpandString(key, "", "");
				continue;
			}
			if (dictionary.TryGetValue(key2, out var value))
			{
				Reg.SetExpandString(key, "", value);
				continue;
			}
			RegistryValueSnapshot registryValueSnapshot = systemSnapshot?.Find(key, "");
			if (registryValueSnapshot != null)
			{
				SettingsBackup.RestoreValue(registryValueSnapshot);
			}
		}
		Reg.SetString("AppEvents\\Schemes", "", (s.Scheme == SoundScheme.Silent) ? ".None" : ".Current");
	}

	public static void RestoreOriginal(SystemSnapshot? original)
	{
		if (original != null)
		{
			SettingsBackup.RestoreValues(original, (RegistryValueSnapshot v) => v.Key.StartsWith("AppEvents\\Schemes", StringComparison.OrdinalIgnoreCase));
		}
	}

	private static Dictionary<string, string> Wavs(string? folder)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(folder))
		{
			return dictionary;
		}
		string text = Environment.ExpandEnvironmentVariables(folder);
		if (!Directory.Exists(text))
		{
			throw new DirectoryNotFoundException(Loc.F("Папка звуков не найдена: {0}", text));
		}
		foreach (string item in Directory.EnumerateFiles(text, "*.wav"))
		{
			dictionary[Path.GetFileNameWithoutExtension(item)] = item;
		}
		return dictionary;
	}
}
