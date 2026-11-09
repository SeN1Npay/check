using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metamorph.Core.Colors;
using Metamorph.Core.Cursors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public sealed class CursorService
{
	private readonly AppPaths _paths;

	private readonly SettingsBackup _backup;

	public CursorService(AppPaths paths, SettingsBackup backup)
	{
		_paths = paths;
		_backup = backup;
	}

	public static IEnumerable<TrackedValue> TrackedValues()
	{
		return CursorRenderer.Names.Select((string n) => new TrackedValue("Control Panel\\Cursors", n, RegistryValueKind.ExpandString)).Append(new TrackedValue("Control Panel\\Cursors", "", RegistryValueKind.String)).Append(new TrackedValue("Control Panel\\Cursors", "Scheme Source", RegistryValueKind.DWord))
			.Append(new TrackedValue("Control Panel\\Cursors", "CursorBaseSize", RegistryValueKind.DWord));
	}

	public void Apply(ThemeProfile profile)
	{
		CursorSettings cursors = profile.Cursors;
		if (cursors.Style == CursorStyle.Keep)
		{
			RestoreOriginal(_backup.LoadOriginal());
			return;
		}
		_backup.EnsureCaptured(TrackedValues());
		Dictionary<string, string> dictionary = ((cursors.Style == CursorStyle.Accent) ? Generate(profile) : FromFolder(cursors.Path));
		SystemSnapshot systemSnapshot = _backup.LoadOriginal();
		foreach (string name in CursorRenderer.Names)
		{
			if (dictionary.TryGetValue(name, out var value))
			{
				Reg.SetExpandString("Control Panel\\Cursors", name, value);
				continue;
			}
			RegistryValueSnapshot registryValueSnapshot = systemSnapshot?.Find("Control Panel\\Cursors", name);
			if (registryValueSnapshot != null)
			{
				SettingsBackup.RestoreValue(registryValueSnapshot);
			}
		}
		Reg.SetString("Control Panel\\Cursors", "", "Metamorph");
		Reg.SetDword("Control Panel\\Cursors", "Scheme Source", 1u);
		Reload();
	}

	public static void RestoreOriginal(SystemSnapshot? original)
	{
		if (original != null)
		{
			HashSet<string> names = (from t in TrackedValues()
				select t.Name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (SettingsBackup.RestoreValues(original, (RegistryValueSnapshot v) => string.Equals(v.Key, "Control Panel\\Cursors", StringComparison.OrdinalIgnoreCase) && names.Contains(v.Name)) > 0)
			{
				Reload();
			}
		}
	}

	public static void Reload()
	{
		Win32.SystemParametersInfo(87u, 0u, IntPtr.Zero, 3u);
	}

	public Dictionary<string, string> Generate(ThemeProfile profile)
	{
		int num = Math.Clamp(profile.Cursors.Size, 32, 128);
		Rgba rgba = Rgba.Parse(profile.Palette.Accent).WithAlpha(byte.MaxValue);
		Rgba outline = Rgba.Parse((profile.Mode == ColorMode.Dark) ? profile.Palette.Background : "#FFFFFF").WithAlpha(byte.MaxValue);
		if (profile.Mode == ColorMode.Light)
		{
			Rgba rgba2 = Rgba.Parse(profile.Palette.Background).WithAlpha(byte.MaxValue).Darken(0.85)
				.Mix(rgba, 0.35);
			outline = Rgba.Parse("#FFFFFF");
			rgba = rgba2;
		}
		string text = Path.Combine(_paths.Generated, "cursors", $"{Safe(profile.Id)}-{rgba.ToHex().TrimStart('#')}-{num}");
		Directory.CreateDirectory(text);
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string name in CursorRenderer.Names)
		{
			string text2 = Path.Combine(text, name + ".cur");
			if (!File.Exists(text2))
			{
				bool flag = ((name == "Wait" || name == "AppStarting") ? true : false);
				Rgba fill = (flag ? Rgba.Parse(profile.Palette.Accent2).WithAlpha(byte.MaxValue) : rgba);
				CursorImage image = ((name == "AppStarting") ? CursorRenderer.Render(name, num, rgba, outline) : CursorRenderer.Render(name, num, fill, outline));
				File.WriteAllBytes(text2, CurWriter.Write(image));
			}
			dictionary[name] = text2;
		}
		return dictionary;
	}

	private static Dictionary<string, string> FromFolder(string? folder)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(folder))
		{
			return dictionary;
		}
		string dir = Environment.ExpandEnvironmentVariables(folder);
		if (!Directory.Exists(dir))
		{
			throw new DirectoryNotFoundException(Loc.F("Папка курсоров не найдена: {0}", dir));
		}
		foreach (string name in CursorRenderer.Names)
		{
			string text = new string[2] { ".cur", ".ani" }.Select((string ext) => Path.Combine(dir, name + ext)).FirstOrDefault(File.Exists);
			if (text != null)
			{
				dictionary[name] = text;
			}
		}
		return dictionary;
	}

	private static string Safe(string id)
	{
		return string.Concat(id.Select((char ch) => (!char.IsLetterOrDigit(ch) && ch != '-') ? '_' : ch));
	}
}
