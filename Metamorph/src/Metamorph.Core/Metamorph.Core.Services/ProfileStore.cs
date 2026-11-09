using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class ProfileStore
{
	private readonly AppPaths _paths;

	private readonly List<string> _errors = new List<string>();

	public IReadOnlyList<string> LastErrors => _errors;

	public ProfileStore(AppPaths paths)
	{
		_paths = paths;
	}

	public IReadOnlyList<ThemeProfile> LoadAll()
	{
		_errors.Clear();
		return (from g in LoadDirectory(_paths.BuiltInThemes, builtIn: true).Concat(LoadDirectory(_paths.UserThemes, builtIn: false)).GroupBy<ThemeProfile, string>((ThemeProfile p) => p.Id, StringComparer.OrdinalIgnoreCase)
			select g.Last() into p
			orderby (!p.IsBuiltIn) ? 1 : 0
			select p).ThenBy<ThemeProfile, string>((ThemeProfile p) => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public ThemeProfile? Find(string id)
	{
		string id2 = id;
		return LoadAll().FirstOrDefault((ThemeProfile p) => string.Equals(p.Id, id2, StringComparison.OrdinalIgnoreCase));
	}

	public void Save(ThemeProfile profile)
	{
		IReadOnlyList<string> readOnlyList = ThemeValidator.Validate(profile);
		if (readOnlyList.Count > 0)
		{
			throw new InvalidDataException(string.Join(Environment.NewLine, readOnlyList));
		}
		string text = Path.Combine(_paths.UserThemes, profile.Id + ".json");
		File.WriteAllText(text, ThemeJson.Serialize(profile));
		profile.SourcePath = text;
		profile.IsBuiltIn = false;
	}

	public ThemeProfile CreateCopy(ThemeProfile source, string? name = null)
	{
		ThemeProfile themeProfile = ThemeJson.Clone(source);
		themeProfile.Wallpaper.Path = ResolvePath(source, source.Wallpaper.Path);
		themeProfile.Wallpaper.Folder = ResolvePath(source, source.Wallpaper.Folder);
		foreach (WallpaperScheduleEntry item in themeProfile.Wallpaper.Schedule)
		{
			item.Path = ResolvePath(source, item.Path);
		}
		themeProfile.Cursors.Path = ResolvePath(source, source.Cursors.Path);
		themeProfile.Sounds.Path = ResolvePath(source, source.Sounds.Path);
		themeProfile.Id = UniqueId(BaseId(source.Id) + "-custom");
		themeProfile.Name = name ?? Loc.F("{0} (моя)", source.Name);
		themeProfile.Author = Environment.UserName;
		Save(themeProfile);
		return themeProfile;
	}

	public string? ResolveWallpaperPath(ThemeProfile profile)
	{
		return ResolvePath(profile, profile.Wallpaper.Path);
	}

	public string? ResolvePath(ThemeProfile profile, string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		string text = Environment.ExpandEnvironmentVariables(raw);
		if (Path.IsPathRooted(text))
		{
			return text;
		}
		string sourcePath = profile.SourcePath;
		return Path.GetFullPath(Path.Combine((sourcePath != null) ? Path.GetDirectoryName(sourcePath) : _paths.BuiltInThemes, text));
	}

	private string UniqueId(string baseId)
	{
		HashSet<string> hashSet = (from p in LoadAll()
			select p.Id).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!hashSet.Contains(baseId))
		{
			return baseId;
		}
		int num = 2;
		string text;
		while (true)
		{
			text = $"{baseId}-{num}";
			if (!hashSet.Contains(text))
			{
				break;
			}
			num++;
		}
		return text;
	}

	private static string BaseId(string id)
	{
		int num = id.IndexOf("-custom", StringComparison.OrdinalIgnoreCase);
		if (num <= 0)
		{
			return id;
		}
		return id.Substring(0, num);
	}

	private IEnumerable<ThemeProfile> LoadDirectory(string dir, bool builtIn)
	{
		if (!Directory.Exists(dir))
		{
			yield break;
		}
		foreach (string item in from f in Directory.EnumerateFiles(dir, "*.json")
			orderby f
			select f)
		{
			ThemeProfile themeProfile = null;
			try
			{
				themeProfile = ThemeJson.Deserialize(File.ReadAllText(item));
				if (string.IsNullOrWhiteSpace(themeProfile.Id))
				{
					themeProfile.Id = Path.GetFileNameWithoutExtension(item).ToLowerInvariant();
				}
				themeProfile.SourcePath = item;
				themeProfile.IsBuiltIn = builtIn;
				IReadOnlyList<string> readOnlyList = ThemeValidator.Validate(themeProfile);
				if (readOnlyList.Count > 0)
				{
					_errors.Add(Path.GetFileName(item) + ": " + string.Join("; ", readOnlyList));
					themeProfile = null;
				}
			}
			catch (Exception ex)
			{
				_errors.Add(Path.GetFileName(item) + ": " + ex.Message);
			}
			if (themeProfile != null)
			{
				yield return themeProfile;
			}
		}
	}
}
