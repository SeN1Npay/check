using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public static class ThemePackage
{
	public const string Extension = ".mmtheme";

	private const long MaxUnpackedBytes = 536870912L;

	public static void Export(ThemeProfile profile, ProfileStore store, string targetFile)
	{
		ProfileStore store2 = store;
		ThemeProfile profile2 = profile;
		ThemeProfile themeProfile = ThemeJson.Clone(profile2);
		string text = targetFile + ".tmp";
		ZipArchive zip = ZipFile.Open(text, ZipArchiveMode.Create);
		try
		{
			Dictionary<string, string> added = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			WallpaperSettings wallpaper = themeProfile.Wallpaper;
			WallpaperKind kind = wallpaper.Kind;
			if ((kind == WallpaperKind.Image || (uint)(kind - 6) <= 1u) ? true : false)
			{
				wallpaper.Path = AddFile(wallpaper.Path, "wallpaper");
			}
			if (wallpaper.Kind == WallpaperKind.Slideshow)
			{
				wallpaper.Folder = AddFolder(wallpaper.Folder, "slideshow", new string[5] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp" });
			}
			foreach (WallpaperScheduleEntry item in wallpaper.Schedule)
			{
				item.Path = AddFile(item.Path, "schedule");
			}
			if (themeProfile.Cursors.Style == CursorStyle.Custom)
			{
				themeProfile.Cursors.Path = AddFolder(themeProfile.Cursors.Path, "cursors", new string[2] { "*.cur", "*.ani" });
			}
			if (themeProfile.Sounds.Scheme == SoundScheme.Custom)
			{
				themeProfile.Sounds.Path = AddFolder(themeProfile.Sounds.Path, "sounds", new string[1] { "*.wav" });
			}
			using (StreamWriter streamWriter = new StreamWriter(zip.CreateEntry("theme.json", CompressionLevel.Optimal).Open()))
			{
				streamWriter.Write(ThemeJson.Serialize(themeProfile));
			}
			string? AddFile(string? path, string folder)
			{
				if (string.IsNullOrWhiteSpace(path))
				{
					return path;
				}
				string text2 = store2.ResolvePath(profile2, path);
				if (text2 == null || !File.Exists(text2))
				{
					return path;
				}
				text2 = Path.GetFullPath(text2);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text2);
				string extension = Path.GetExtension(text2);
				string text3 = $"assets/{folder}/{fileNameWithoutExtension}{extension}";
				int num = 2;
				string value;
				while (added.TryGetValue(text3, out value) && !string.Equals(value, text2, StringComparison.OrdinalIgnoreCase))
				{
					text3 = $"assets/{folder}/{fileNameWithoutExtension}-{num}{extension}";
					num++;
				}
				if (added.TryAdd(text3, text2))
				{
					zip.CreateEntryFromFile(text2, text3, CompressionLevel.Optimal);
				}
				return text3;
			}
		}
		finally
		{
			if (zip != null)
			{
				((IDisposable)zip).Dispose();
			}
		}
		File.Move(text, targetFile, overwrite: true);
		string? AddFolder(string? path, string folder, string[] patterns)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return path;
			}
			string full = store2.ResolvePath(profile2, path);
			if (full == null || !Directory.Exists(full))
			{
				return path;
			}
			foreach (string item2 in patterns.SelectMany((string p) => Directory.EnumerateFiles(full, p)).Distinct().Take(200))
			{
				zip.CreateEntryFromFile(item2, "assets/" + folder + "/" + Path.GetFileName(item2), CompressionLevel.Optimal);
			}
			return "assets/" + folder;
		}
	}

	public static ThemeProfile Import(string packageFile, AppPaths paths, ProfileStore store)
	{
		using ZipArchive zipArchive = ZipFile.OpenRead(packageFile);
		ZipArchiveEntry? obj = zipArchive.GetEntry("theme.json") ?? throw new InvalidDataException("В пакете нет theme.json");
		if (zipArchive.Entries.Sum((ZipArchiveEntry e) => e.Length) > 536870912)
		{
			throw new InvalidDataException("Пакет слишком большой");
		}
		ThemeProfile themeProfile;
		using (StreamReader streamReader = new StreamReader(obj.Open()))
		{
			themeProfile = ThemeJson.Deserialize(streamReader.ReadToEnd());
		}
		if (string.IsNullOrWhiteSpace(themeProfile.Id))
		{
			themeProfile.Id = Base16.Slug(Path.GetFileNameWithoutExtension(packageFile));
		}
		themeProfile.Id = Base16.Slug(themeProfile.Id);
		if ((from p in store.LoadAll()
			where p.IsBuiltIn
			select p.Id).ToHashSet<string>(StringComparer.OrdinalIgnoreCase).Contains(themeProfile.Id))
		{
			themeProfile.Id += "-imported";
		}
		string assetsDir = Path.Combine(paths.UserThemes, themeProfile.Id + ".assets");
		if (Directory.Exists(assetsDir))
		{
			Directory.Delete(assetsDir, recursive: true);
		}
		foreach (ZipArchiveEntry entry in zipArchive.Entries)
		{
			if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal) && !entry.FullName.EndsWith('/'))
			{
				string fullName = entry.FullName;
				int length = "assets/".Length;
				string path2 = fullName.Substring(length, fullName.Length - length).Replace('/', Path.DirectorySeparatorChar);
				string fullPath = Path.GetFullPath(Path.Combine(assetsDir, path2));
				ReadOnlySpan<char> readOnlySpan = Path.GetFullPath(assetsDir);
				char reference = Path.DirectorySeparatorChar;
				if (!fullPath.StartsWith(string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference)), StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException("Недопустимый путь в пакете: " + entry.FullName);
				}
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
				entry.ExtractToFile(fullPath, overwrite: true);
			}
		}
		themeProfile.Wallpaper.Path = Map(themeProfile.Wallpaper.Path);
		themeProfile.Wallpaper.Folder = Map(themeProfile.Wallpaper.Folder);
		foreach (WallpaperScheduleEntry item in themeProfile.Wallpaper.Schedule)
		{
			item.Path = Map(item.Path);
		}
		themeProfile.Cursors.Path = Map(themeProfile.Cursors.Path);
		themeProfile.Sounds.Path = Map(themeProfile.Sounds.Path);
		store.Save(themeProfile);
		return themeProfile;
		string? Map(string? path)
		{
			if (path == null || !path.StartsWith("assets/", StringComparison.Ordinal))
			{
				return path;
			}
			string path3 = assetsDir;
			int length2 = "assets/".Length;
			return Path.Combine(path3, path.Substring(length2, path.Length - length2).Replace('/', Path.DirectorySeparatorChar));
		}
	}
}
