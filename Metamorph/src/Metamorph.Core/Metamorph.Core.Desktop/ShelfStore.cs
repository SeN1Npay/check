using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Metamorph.Core.Services;

namespace Metamorph.Core.Desktop;

public sealed class ShelfState
{
	public double X { get; set; }

	public double Y { get; set; }

	public double Width { get; set; }

	public double Height { get; set; }

	public bool Collapsed { get; set; }
}

// Положение полок, которое пользователь поменял мышью: shelves.json, по id темы → по названию полки.
// Файлы тем при этом не трогаем.
public sealed class ShelfStore
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly Dictionary<string, Dictionary<string, ShelfState>> _themes;

	public string FilePath { get; }

	private ShelfStore(string path, Dictionary<string, Dictionary<string, ShelfState>> themes)
	{
		FilePath = path;
		_themes = themes;
	}

	public static ShelfStore Load(string path)
	{
		Dictionary<string, Dictionary<string, ShelfState>> themes = null;
		try
		{
			if (File.Exists(path))
			{
				themes = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ShelfState>>>(File.ReadAllText(path), Options);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Полки: не читается " + path + " (" + ex.Message + ") — начинаю с чистого листа");
		}
		return new ShelfStore(path, themes ?? new Dictionary<string, Dictionary<string, ShelfState>>());
	}

	public static string Key(string? title, int index)
	{
		if (!string.IsNullOrWhiteSpace(title))
		{
			return title.Trim();
		}
		return "#" + index;
	}

	public ShelfState? Get(string themeId, string key)
	{
		if (_themes.TryGetValue(themeId ?? "", out Dictionary<string, ShelfState> shelves) && shelves.TryGetValue(key, out ShelfState state))
		{
			return state;
		}
		return null;
	}

	public void Set(string themeId, string key, ShelfState state)
	{
		themeId ??= "";
		if (!_themes.TryGetValue(themeId, out Dictionary<string, ShelfState> shelves))
		{
			shelves = (_themes[themeId] = new Dictionary<string, ShelfState>());
		}
		shelves[key] = state;
	}

	public void Save()
	{
		string? dir = Path.GetDirectoryName(FilePath);
		if (!string.IsNullOrEmpty(dir))
		{
			Directory.CreateDirectory(dir);
		}
		string temp = FilePath + ".tmp";
		File.WriteAllText(temp, JsonSerializer.Serialize(_themes, Options));
		File.Move(temp, FilePath, overwrite: true);
	}
}
