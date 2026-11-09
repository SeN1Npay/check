using System;
using System.Collections.Generic;
using System.IO;

namespace Metamorph.Core.Desktop;

// Правило автосортировки полки: ".png,.jpg", "folders", "shortcuts" (можно смешивать через запятую).
public sealed class ShelfRule
{
	private static readonly HashSet<string> ShortcutExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url", ".appref-ms" };

	private readonly HashSet<string> _extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public bool Folders { get; private set; }

	public bool Shortcuts { get; private set; }

	public IReadOnlyCollection<string> Extensions => _extensions;

	public bool IsEmpty
	{
		get
		{
			if (!Folders && !Shortcuts)
			{
				return _extensions.Count == 0;
			}
			return false;
		}
	}

	public static ShelfRule? Parse(string? rule)
	{
		if (string.IsNullOrWhiteSpace(rule))
		{
			return null;
		}
		ShelfRule result = new ShelfRule();
		foreach (string raw in rule.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string token = raw.Trim().TrimStart('*');
			if (token.Equals("folders", StringComparison.OrdinalIgnoreCase))
			{
				result.Folders = true;
			}
			else if (token.Equals("shortcuts", StringComparison.OrdinalIgnoreCase))
			{
				result.Shortcuts = true;
			}
			else if (token.Trim('.').Length > 0)
			{
				result._extensions.Add(token.StartsWith('.') ? token : ("." + token));
			}
		}
		return result.IsEmpty ? null : result;
	}

	public bool Matches(string path, bool isDirectory)
	{
		string name = Path.GetFileName(path.TrimEnd('\\', '/'));
		if (name.Length == 0 || ShelfFiles.IsSystemName(name))
		{
			return false;
		}
		if (isDirectory)
		{
			return Folders;
		}
		string ext = Path.GetExtension(name);
		if (Shortcuts && ShortcutExtensions.Contains(ext))
		{
			return true;
		}
		return ext.Length > 0 && _extensions.Contains(ext);
	}
}
