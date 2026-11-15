using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.Core.Wallpaper;

public sealed class WallpaperLibrary
{
	public const long CopyLimitBytes = 314572800L;

	private readonly string _folder;

	private string LinksFile => Path.Combine(_folder, "links.json");

	public string Folder => _folder;

	public WallpaperLibrary(string folder)
	{
		_folder = folder;
		Directory.CreateDirectory(folder);
	}

	public static WallpaperMedia MediaOf(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return WallpaperMedia.Unsupported;
		}
		if (Directory.Exists(path))
		{
			return WallpaperMedia.Folder;
		}
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text == ".gif")
		{
			return WallpaperMedia.Gif;
		}
		if (WallpaperService.ImageExtensions.Contains(text))
		{
			return WallpaperMedia.Image;
		}
		if (WallpaperService.VideoExtensions.Contains(text))
		{
			return WallpaperMedia.Video;
		}
		return WallpaperMedia.Unsupported;
	}

	public static bool IsSupported(string? path)
	{
		return MediaOf(path) != WallpaperMedia.Unsupported;
	}

	public IReadOnlyList<string> List()
	{
		IEnumerable<(string Path, DateTime Time)> source = from f in Directory.EnumerateFiles(_folder)
			where !f.Equals(LinksFile, StringComparison.OrdinalIgnoreCase) && IsSupported(f)
			select (Path: f, Time: File.GetLastWriteTimeUtc(f));
		return (from f in (from l in ReadLinks()
				where File.Exists(l) || Directory.Exists(l)
				select (Path: l, Time: DateTime.MaxValue)).Concat<(string Path, DateTime Time)>(source.OrderByDescending<(string Path, DateTime Time), DateTime>(((string Path, DateTime Time) f) => f.Time))
			select f.Path).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public string Add(string source, bool? copy = null)
	{
		WallpaperMedia wallpaperMedia = MediaOf(source);
		if (wallpaperMedia == WallpaperMedia.Unsupported)
		{
			throw new NotSupportedException(Loc.F("Не поддерживается как обои: {0}", Path.GetFileName(source)));
		}
		string full = Path.GetFullPath(source);
		if (IsInside(full))
		{
			return full;
		}
		bool flag = copy ?? (wallpaperMedia != WallpaperMedia.Folder && new FileInfo(full).Length <= 314572800);
		if (wallpaperMedia == WallpaperMedia.Folder || !flag)
		{
			List<string> list = ReadLinks();
			list.RemoveAll((string l) => l.Equals(full, StringComparison.OrdinalIgnoreCase));
			list.Insert(0, full);
			WriteLinks(list);
			return full;
		}
		long size = new FileInfo(full).Length;
		string text = Directory.EnumerateFiles(_folder).FirstOrDefault((string f) => Path.GetFileName(f).Equals(Path.GetFileName(full), StringComparison.OrdinalIgnoreCase) && new FileInfo(f).Length == size);
		if (text != null)
		{
			File.SetLastWriteTimeUtc(text, DateTime.UtcNow);
			return text;
		}
		string text2 = UniquePath(Path.GetFileName(full));
		File.Copy(full, text2);
		File.SetLastWriteTimeUtc(text2, DateTime.UtcNow);
		return text2;
	}

	public void Remove(string path)
	{
		string full = Path.GetFullPath(path);
		if (IsInside(full))
		{
			if (File.Exists(full))
			{
				File.Delete(full);
			}
			return;
		}
		List<string> list = ReadLinks();
		if (list.RemoveAll((string l) => l.Equals(full, StringComparison.OrdinalIgnoreCase)) > 0)
		{
			WriteLinks(list);
		}
	}

	public bool IsInside(string path)
	{
		return string.Equals(Path.GetDirectoryName(Path.GetFullPath(path))?.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(_folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
	}

	private string UniquePath(string name)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(name);
		string extension = Path.GetExtension(name);
		string text = Path.Combine(_folder, name);
		int num = 2;
		while (File.Exists(text))
		{
			text = Path.Combine(_folder, $"{fileNameWithoutExtension} ({num}){extension}");
			num++;
		}
		return text;
	}

	private List<string> ReadLinks()
	{
		try
		{
			return File.Exists(LinksFile) ? (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LinksFile)) ?? new List<string>()) : new List<string>();
		}
		catch
		{
			return new List<string>();
		}
	}

	private void WriteLinks(List<string> links)
	{
		File.WriteAllText(LinksFile, JsonSerializer.Serialize(links));
	}
}
