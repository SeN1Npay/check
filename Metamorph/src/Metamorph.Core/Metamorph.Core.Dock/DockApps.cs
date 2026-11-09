using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metamorph.Core.Dock;

public static class DockApps
{
	public const string AppsFolderPrefix = "shell:AppsFolder\\";

	public static string TaskbarPinsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Internet Explorer\\Quick Launch\\User Pinned\\TaskBar");

	public static string KeyFor(string? appId, string? exePath)
	{
		if (string.IsNullOrWhiteSpace(appId))
		{
			return "exe:" + (exePath ?? "").Trim().ToLowerInvariant();
		}
		return "id:" + appId.Trim().ToLowerInvariant();
	}

	public static bool IsUwpHost(string? exePath)
	{
		return exePath?.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) ?? false;
	}

	public static bool Matches(DockPin pin, string? appId, string? exePath)
	{
		if (!string.IsNullOrWhiteSpace(pin.AppId) && !string.IsNullOrWhiteSpace(appId))
		{
			return string.Equals(pin.AppId.Trim(), appId.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		string text = pin.Target;
		if (string.IsNullOrWhiteSpace(text) && pin.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			text = pin.Path;
		}
		if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(exePath) && !IsUwpHost(exePath))
		{
			return PathsEqual(text, exePath);
		}
		return false;
	}

	public static bool PathsEqual(string a, string b)
	{
		try
		{
			return string.Equals(Path.GetFullPath(Environment.ExpandEnvironmentVariables(a)), Path.GetFullPath(Environment.ExpandEnvironmentVariables(b)), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
	}

	public static DockPin PinFor(string name, string? appId, string exePath)
	{
		if (IsUwpHost(exePath) && !string.IsNullOrWhiteSpace(appId))
		{
			return new DockPin
			{
				Path = "shell:AppsFolder\\" + appId,
				AppId = appId,
				Name = name
			};
		}
		return new DockPin
		{
			Path = exePath,
			Target = exePath,
			AppId = appId,
			Name = name
		};
	}

	public static IEnumerable<DockPin> ImportTaskbarPins(string folder, Func<string, (string? Target, string? AppId)> resolve)
	{
		if (!Directory.Exists(folder))
		{
			return Array.Empty<DockPin>();
		}
		List<DockPin> list = new List<DockPin>();
		foreach (FileInfo item in from f in new DirectoryInfo(folder).GetFiles("*.lnk")
			orderby f.CreationTimeUtc
			select f)
		{
			var (target, appId) = resolve(item.FullName);
			list.Add(new DockPin
			{
				Path = item.FullName,
				Name = Path.GetFileNameWithoutExtension(item.Name),
				Target = target,
				AppId = appId
			});
		}
		return list;
	}
}
