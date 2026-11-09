using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Metamorph.Core.Wallpaper;

public static class WallpaperEngineImport
{
	public const string AppId = "431960";

	private static readonly Regex _PathEntryRegex = new Regex("\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static Regex PathEntry() => _PathEntryRegex;

	public static List<string> ParseLibraryFolders(string vdf)
	{
		List<string> list = new List<string>();
		foreach (Match item in PathEntry().Matches(vdf))
		{
			list.Add(item.Groups[1].Value.Replace("\\\\", "\\"));
		}
		foreach (Match item2 in Regex.Matches(vdf, "^\\s*\"\\d+\"\\s+\"([A-Za-z]:(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Multiline))
		{
			list.Add(item2.Groups[1].Value.Replace("\\\\", "\\"));
		}
		return list.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static WallpaperEngineItem? ParseProject(string folder, string json)
	{
		string folder2 = folder;
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
			JsonElement root = jsonDocument.RootElement;
			string text = Str("type") ?? "scene";
			string text2 = Str("title");
			string rel2 = Str("file");
			string rel3 = Str("preview");
			string fileName = Path.GetFileName(folder2.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			return new WallpaperEngineItem(fileName, string.IsNullOrWhiteSpace(text2) ? fileName : text2.Trim(), text.ToLowerInvariant(), folder2, Full(rel2), Full(rel3));
			string? Str(string name)
			{
				if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
				{
					return null;
				}
				return value.GetString();
			}
		}
		catch (JsonException)
		{
			return null;
		}
		string? Full(string? rel)
		{
			if (!string.IsNullOrWhiteSpace(rel))
			{
				return Path.GetFullPath(Path.Combine(folder2, rel.Replace('/', Path.DirectorySeparatorChar)));
			}
			return null;
		}
	}

	public static List<WallpaperEngineItem> Scan(IEnumerable<string> steamLibraries)
	{
		List<WallpaperEngineItem> list = new List<WallpaperEngineItem>();
		foreach (string item in steamLibraries.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			foreach (string item2 in new string[3]
			{
				Path.Combine(item, "steamapps", "workshop", "content", "431960"),
				Path.Combine(item, "steamapps", "common", "wallpaper_engine", "projects", "myprojects"),
				Path.Combine(item, "steamapps", "common", "wallpaper_engine", "projects", "defaultprojects")
			}.Where(Directory.Exists))
			{
				IEnumerable<string> enumerable;
				try
				{
					enumerable = Directory.EnumerateDirectories(item2).ToList();
				}
				catch
				{
					continue;
				}
				foreach (string item3 in enumerable)
				{
					string path = Path.Combine(item3, "project.json");
					if (!File.Exists(path))
					{
						continue;
					}
					try
					{
						WallpaperEngineItem wallpaperEngineItem = ParseProject(item3, File.ReadAllText(path));
						if ((object)wallpaperEngineItem != null && wallpaperEngineItem.IsVideo)
						{
							string file = wallpaperEngineItem.File;
							if (file != null && File.Exists(file))
							{
								list.Add(wallpaperEngineItem);
							}
						}
					}
					catch
					{
					}
				}
			}
		}
		return list.OrderBy<WallpaperEngineItem, string>((WallpaperEngineItem i) => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public static List<string> FindSteamLibraries()
	{
		List<string> list = new List<string>();
		try
		{
			if (OperatingSystem.IsWindows())
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
				if (registryKey?.GetValue("SteamPath") is string { Length: >0 } text)
				{
					list.Add(Path.GetFullPath(text.Replace('/', '\\')));
				}
			}
		}
		catch
		{
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
		if (folderPath.Length > 0)
		{
			list.Add(Path.Combine(folderPath, "Steam"));
		}
		List<string> list2 = new List<string>();
		foreach (string item in list.Where(Directory.Exists).Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			list2.Add(item);
			string path = Path.Combine(item, "steamapps", "libraryfolders.vdf");
			try
			{
				if (File.Exists(path))
				{
					list2.AddRange(ParseLibraryFolders(File.ReadAllText(path)));
				}
			}
			catch
			{
			}
		}
		return list2.Where(Directory.Exists).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}
}
