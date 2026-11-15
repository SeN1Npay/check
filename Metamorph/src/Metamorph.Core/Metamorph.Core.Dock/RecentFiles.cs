using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Metamorph.Core.Services;

namespace Metamorph.Core.Dock;

public static class RecentFiles
{
	private static readonly Dictionary<string, string?> AssocCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static (DateTime At, List<(string Target, DateTime Time)> Items)? _recent;

	public static string RecentFolder => Environment.GetFolderPath(Environment.SpecialFolder.Recent);

	public static IReadOnlyList<string> ForApp(string exePath, int limit = 8)
	{
		if (string.IsNullOrWhiteSpace(exePath))
		{
			return Array.Empty<string>();
		}
		string exe = Path.GetFileName(exePath);
		return (from r in Recent()
			where Path.HasExtension(r.Target) && string.Equals(Path.GetFileName(AssociatedExe(Path.GetExtension(r.Target)) ?? ""), exe, StringComparison.OrdinalIgnoreCase)
			select r.Target).Distinct<string>(StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
	}

	private static List<(string Target, DateTime Time)> Recent()
	{
		(DateTime, List<(string, DateTime)>)? recent = _recent;
		if (recent.HasValue)
		{
			(DateTime, List<(string, DateTime)>) valueOrDefault = recent.GetValueOrDefault();
			if (DateTime.UtcNow - valueOrDefault.Item1 < TimeSpan.FromSeconds(30.0))
			{
				return valueOrDefault.Item2;
			}
		}
		List<(string, DateTime)> list = new List<(string, DateTime)>();
		try
		{
			foreach (FileInfo item2 in (from f in new DirectoryInfo(RecentFolder).EnumerateFiles("*.lnk")
				orderby f.LastWriteTimeUtc descending
				select f).Take(150))
			{
				string item = ShellIcons.TryResolveShortcut(item2.FullName).Target;
				if (item != null && File.Exists(item))
				{
					list.Add((item, item2.LastWriteTimeUtc));
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Недавние файлы: " + ex.Message);
		}
		_recent = (DateTime.UtcNow, list);
		return list;
	}

	public static string? AssociatedExe(string extension)
	{
		lock (AssocCache)
		{
			if (AssocCache.TryGetValue(extension, out string value))
			{
				return value;
			}
			uint size = 1024u;
			StringBuilder stringBuilder = new StringBuilder((int)size);
			string text = ((AssocQueryString(64u, 2, extension, null, stringBuilder, ref size) == 0) ? stringBuilder.ToString() : null);
			AssocCache[extension] = text;
			return text;
		}
	}

	[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern int AssocQueryString(uint flags, int str, string assoc, string? extra, StringBuilder? output, ref uint size);
}
