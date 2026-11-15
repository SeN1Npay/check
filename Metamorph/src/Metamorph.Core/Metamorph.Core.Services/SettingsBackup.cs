using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Metamorph.Core.Colors;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public sealed class SettingsBackup
{
	public static readonly TrackedValue[] Tracked = new TrackedValue[25]
	{
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "ColorPrevalence", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "AccentPalette", RegistryValueKind.Binary),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "AccentColorMenu", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "StartColorMenu", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\DWM", "AccentColor", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\DWM", "ColorizationColor", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\DWM", "ColorizationAfterglow", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\DWM", "ColorPrevalence", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\DWM", "AccentColorInactive", RegistryValueKind.DWord),
		new TrackedValue("Control Panel\\Desktop", "Wallpaper", RegistryValueKind.String),
		new TrackedValue("Control Panel\\Desktop", "WallpaperStyle", RegistryValueKind.String),
		new TrackedValue("Control Panel\\Desktop", "TileWallpaper", RegistryValueKind.String),
		new TrackedValue("Control Panel\\Colors", "Background", RegistryValueKind.String),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Search", "SearchboxTaskbarMode", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowTaskViewButton", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowCortanaButton", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarSmallIcons", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "TaskbarGlomLevel", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "MMTaskbarGlomLevel", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideIcons", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\\People", "PeopleBand", RegistryValueKind.DWord),
		new TrackedValue("Software\\Microsoft\\Windows\\CurrentVersion\\Feeds", "ShellFeedsTaskbarViewMode", RegistryValueKind.DWord)
	};

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly AppPaths _paths;

	private readonly object _sync = new object();

	public bool HasOriginal => File.Exists(_paths.BackupFile);

	public SettingsBackup(AppPaths paths)
	{
		_paths = paths;
	}

	public void EnsureOriginalSaved()
	{
		lock (_sync)
		{
			if (!HasOriginal)
			{
				Save(Capture());
				Log.Info("Сохранена резервная копия исходных настроек: " + _paths.BackupFile);
				return;
			}
		}
		EnsureCaptured(Tracked);
	}

	public void EnsureCaptured(IEnumerable<TrackedValue> values)
	{
		lock (_sync)
		{
			SystemSnapshot systemSnapshot = LoadOriginal() ?? Capture();
			int num = 0;
			foreach (TrackedValue value in values)
			{
				if (systemSnapshot.Find(value.Key, value.Name) == null)
				{
					systemSnapshot.Values.Add(CaptureValue(value));
					num++;
				}
			}
			if (num > 0 || !HasOriginal)
			{
				Save(systemSnapshot);
			}
		}
	}

	public void EnsureExtra(string name, Func<string?> current)
	{
		lock (_sync)
		{
			SystemSnapshot systemSnapshot = LoadOriginal() ?? Capture();
			if (!systemSnapshot.Extras.ContainsKey(name))
			{
				systemSnapshot.Extras[name] = current();
				Save(systemSnapshot);
			}
		}
	}

	public bool TryGetExtra(string name, out string? value)
	{
		value = null;
		return LoadOriginal()?.Extras.TryGetValue(name, out value) ?? false;
	}

	public SystemSnapshot Capture()
	{
		SystemSnapshot systemSnapshot = new SystemSnapshot
		{
			CreatedUtc = DateTime.UtcNow,
			MachineName = Environment.MachineName,
			WallpaperPath = WallpaperService.GetCurrentWallpaper(),
			TaskbarAutoHide = TaskbarController.GetAutoHide(),
			WallpaperCopy = CopyCurrentWallpaper()
		};
		TrackedValue[] tracked = Tracked;
		foreach (TrackedValue t in tracked)
		{
			systemSnapshot.Values.Add(CaptureValue(t));
		}
		return systemSnapshot;
	}

	public static RegistryValueSnapshot CaptureValue(TrackedValue t)
	{
		object obj = Reg.Get(t.Key, t.Name);
		RegistryValueKind registryValueKind = Reg.GetKind(t.Key, t.Name) ?? t.Kind;
		RegistryValueSnapshot registryValueSnapshot = new RegistryValueSnapshot
		{
			Key = t.Key,
			Name = t.Name,
			Kind = registryValueKind.ToString(),
			Exists = (obj != null)
		};
		if (!(obj is int value))
		{
			if (!(obj is byte[] inArray))
			{
				if (obj is string text)
				{
					registryValueSnapshot.Text = text;
				}
			}
			else
			{
				registryValueSnapshot.Binary = Convert.ToBase64String(inArray);
			}
		}
		else
		{
			registryValueSnapshot.Dword = value;
		}
		return registryValueSnapshot;
	}

	public SystemSnapshot? LoadOriginal()
	{
		if (!HasOriginal)
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<SystemSnapshot>(File.ReadAllText(_paths.BackupFile));
		}
		catch (Exception ex)
		{
			Log.Error("Резервная копия повреждена", ex);
			return null;
		}
	}

	private void Save(SystemSnapshot snapshot)
	{
		Directory.CreateDirectory(_paths.BackupDir);
		string text = _paths.BackupFile + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(snapshot, JsonOptions));
		File.Move(text, _paths.BackupFile, overwrite: true);
	}

	public void DeleteOriginal()
	{
		if (HasOriginal)
		{
			File.Delete(_paths.BackupFile);
		}
	}

	public static void RestoreRegistry(SystemSnapshot snapshot)
	{
		RestoreValues(snapshot, (RegistryValueSnapshot _) => true);
	}

	public static int RestoreValues(SystemSnapshot snapshot, Func<RegistryValueSnapshot, bool> filter)
	{
		int num = 0;
		foreach (RegistryValueSnapshot item in snapshot.Values.Where(filter))
		{
			RestoreValue(item);
			num++;
		}
		return num;
	}

	public static void RestoreValue(RegistryValueSnapshot v)
	{
		if (!v.Exists)
		{
			Reg.Delete(v.Key, v.Name);
			return;
		}
		int? dword = v.Dword;
		if (dword.HasValue)
		{
			int valueOrDefault = dword.GetValueOrDefault();
			Reg.SetDword(v.Key, v.Name, (uint)valueOrDefault);
			return;
		}
		string binary = v.Binary;
		if (binary != null)
		{
			Reg.SetBinary(v.Key, v.Name, Convert.FromBase64String(binary));
			return;
		}
		string text = v.Text;
		if (text != null)
		{
			if (v.Kind == "ExpandString")
			{
				Reg.SetExpandString(v.Key, v.Name, text);
			}
			else
			{
				Reg.SetString(v.Key, v.Name, text);
			}
		}
	}

	public static void RestoreWallpaper(SystemSnapshot snapshot)
	{
		if (Rgba.TryParseTriplet(snapshot.Find("Control Panel\\Colors", "Background")?.Text, out var color))
		{
			WallpaperService.SetDesktopColor(color);
		}
		string wallpaperPath = snapshot.WallpaperPath;
		object obj;
		if (string.IsNullOrEmpty(wallpaperPath) || !File.Exists(wallpaperPath) || IsTranscoded(wallpaperPath))
		{
			string wallpaperCopy = snapshot.WallpaperCopy;
			obj = ((wallpaperCopy != null && File.Exists(wallpaperCopy)) ? wallpaperCopy : wallpaperPath);
		}
		else
		{
			obj = wallpaperPath;
		}
		if (obj == null)
		{
			obj = "";
		}
		WallpaperService.SetWallpaperRaw((string)obj);
	}

	private string? CopyCurrentWallpaper()
	{
		try
		{
			string currentWallpaper = WallpaperService.GetCurrentWallpaper();
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
			string text2 = (File.Exists(currentWallpaper) ? currentWallpaper : (File.Exists(text) ? text : null));
			if (text2 == null)
			{
				return null;
			}
			string text3 = Path.GetExtension(text2);
			if (string.IsNullOrEmpty(text3))
			{
				text3 = ".jpg";
			}
			string text4 = Path.Combine(_paths.BackupDir, "original-wallpaper" + text3);
			File.Copy(text2, text4, overwrite: true);
			return text4;
		}
		catch (Exception ex)
		{
			Log.Warn("Не удалось скопировать исходные обои: " + ex.Message);
			return null;
		}
	}

	private static bool IsTranscoded(string path)
	{
		return Path.GetFileName(path).StartsWith("TranscodedWallpaper", StringComparison.OrdinalIgnoreCase);
	}
}
