using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Metamorph.Core.Desktop;

// Файловые операции полок: никогда не перезаписываем, на одном томе — перенос, между томами — копия (оригинал остаётся).
public static class ShelfFiles
{
	public static bool IsSystemName(string name)
	{
		if (!name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
		{
			return name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static string SafeName(string? title)
	{
		string text = (title ?? "").Trim();
		char[] invalid = Path.GetInvalidFileNameChars().Concat(new char[7] { '<', '>', ':', '"', '|', '?', '*' }).ToArray();
		foreach (char c in invalid)
		{
			text = text.Replace(c, '_');
		}
		text = text.Trim(' ', '.');
		return (text.Length == 0) ? "shelf" : text;
	}

	public static string DefaultFolder(string root, string? title)
	{
		return Path.Combine(root, "shelves", SafeName(title));
	}

	// "a.txt" → "a (2).txt" → "a (3).txt"…; у папок расширение не отделяется.
	public static string UniqueName(string folder, string name, bool isDirectory, Func<string, bool> exists)
	{
		string candidate = Path.Combine(folder, name);
		if (!exists(candidate))
		{
			return candidate;
		}
		string stem = (isDirectory ? name : Path.GetFileNameWithoutExtension(name));
		string ext = (isDirectory ? "" : Path.GetExtension(name));
		if (stem.Length == 0)
		{
			stem = name;
			ext = "";
		}
		for (int i = 2; i < 10000; i++)
		{
			candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
			if (!exists(candidate))
			{
				return candidate;
			}
		}
		return Path.Combine(folder, $"{stem} ({Guid.NewGuid():N}){ext}");
	}

	public static bool SameVolume(string a, string b)
	{
		string ra = Path.GetPathRoot(Path.GetFullPath(a)) ?? "";
		string rb = Path.GetPathRoot(Path.GetFullPath(b)) ?? "";
		return string.Equals(ra.TrimEnd('\\', '/'), rb.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsInside(string path, string folder)
	{
		string p = Path.GetFullPath(path).TrimEnd('\\', '/');
		string f = Path.GetFullPath(folder).TrimEnd('\\', '/');
		if (!p.Equals(f, StringComparison.OrdinalIgnoreCase))
		{
			return p.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	// Переносит файл/папку в folder. null — переносить нечего (уже там, папка в самой себе, источник пропал).
	public static string? MoveInto(string source, string folder, bool? sameVolume = null)
	{
		string full = Path.GetFullPath(source).TrimEnd('\\', '/');
		bool isDirectory = Directory.Exists(full);
		if (!isDirectory && !File.Exists(full))
		{
			return null;
		}
		string parent = Path.GetDirectoryName(full) ?? "";
		if (string.Equals(parent.TrimEnd('\\', '/'), Path.GetFullPath(folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		if (isDirectory && IsInside(folder, full))
		{
			return null;
		}
		Directory.CreateDirectory(folder);
		string dest = UniqueName(folder, Path.GetFileName(full), isDirectory, (string p) => File.Exists(p) || Directory.Exists(p));
		if (sameVolume ?? SameVolume(full, folder))
		{
			if (isDirectory)
			{
				Directory.Move(full, dest);
			}
			else
			{
				File.Move(full, dest, overwrite: false);
			}
		}
		else if (isDirectory)
		{
			CopyDirectory(full, dest);
		}
		else
		{
			File.Copy(full, dest, overwrite: false);
		}
		return dest;
	}

	private static void CopyDirectory(string source, string dest)
	{
		Directory.CreateDirectory(dest);
		foreach (string file in Directory.EnumerateFiles(source))
		{
			File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: false);
		}
		foreach (string dir in Directory.EnumerateDirectories(source))
		{
			CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
		}
	}

	// Разложить рабочий стол по правилу. Возвращает перенесённые пути (новые), ошибки отдаёт в onError.
	public static List<string> SortInto(string desktop, string folder, ShelfRule rule, Action<string, Exception>? onError = null)
	{
		List<string> moved = new List<string>();
		if (!Directory.Exists(desktop))
		{
			return moved;
		}
		foreach (string entry in Directory.EnumerateFileSystemEntries(desktop).ToList())
		{
			try
			{
				FileAttributes attributes = File.GetAttributes(entry);
				if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
				{
					continue;
				}
				bool isDirectory = (attributes & FileAttributes.Directory) != 0;
				if (!rule.Matches(entry, isDirectory) || (isDirectory && IsInside(folder, entry)))
				{
					continue;
				}
				string? dest = MoveInto(entry, folder);
				if (dest != null)
				{
					moved.Add(dest);
				}
			}
			catch (Exception ex)
			{
				onError?.Invoke(entry, ex);
			}
		}
		return moved;
	}
}
