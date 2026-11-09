using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.Core.Desktop;

// Пользовательская раскладка карточек по id темы (cards.json). Файлы тем не трогаем — они неизменяемые.
public sealed class CardLayoutStore
{
	private sealed class Data
	{
		public Dictionary<string, List<CardSettings>> Themes { get; set; } = new Dictionary<string, List<CardSettings>>();
	}

	private readonly string _file;

	public string FilePath => _file;

	public CardLayoutStore(string file)
	{
		_file = file;
	}

	public CardLayoutStore(AppPaths paths)
		: this(Path.Combine(paths.Root, "cards.json"))
	{
	}

	private static string Key(string? themeId)
	{
		return string.IsNullOrWhiteSpace(themeId) ? "_" : themeId.Trim();
	}

	public List<CardSettings>? Load(string? themeId)
	{
		Data data = Read();
		if (data.Themes.TryGetValue(Key(themeId), out List<CardSettings>? list) && list != null)
		{
			return Sanitize(list);
		}
		return null;
	}

	public void Save(string? themeId, IEnumerable<CardSettings> items)
	{
		Data data = Read();
		data.Themes[Key(themeId)] = Sanitize(items);
		Write(data);
	}

	public void Reset(string? themeId)
	{
		Data data = Read();
		if (data.Themes.Remove(Key(themeId)))
		{
			Write(data);
		}
	}

	// Пользовательская раскладка → Items темы (custom или непустой список) → пресет.
	public static List<CardSettings> Effective(CardsSettings settings, List<CardSettings>? user)
	{
		if (user != null)
		{
			return Sanitize(user);
		}
		if (string.Equals(settings.Preset, "custom", StringComparison.OrdinalIgnoreCase) || settings.Items.Count > 0)
		{
			return Sanitize(settings.Items);
		}
		return CardLayout.Preset(settings.Preset);
	}

	public List<CardSettings> Effective(CardsSettings settings, string? themeId)
	{
		return Effective(settings, Load(themeId));
	}

	// Глубокая копия без неизвестных видов и мусорных координат.
	public static List<CardSettings> Sanitize(IEnumerable<CardSettings?> items)
	{
		List<CardSettings> list = new List<CardSettings>();
		foreach (CardSettings? item in items)
		{
			string kind = (item?.Kind ?? "").Trim().ToLowerInvariant();
			if (item == null || !CardsSettings.Known.Contains(kind))
			{
				continue;
			}
			list.Add(new CardSettings
			{
				Kind = kind,
				X = double.IsFinite(item.X) ? Math.Clamp(item.X, 0.0, 1.0) : 0.0,
				Y = double.IsFinite(item.Y) ? Math.Clamp(item.Y, 0.0, 1.0) : 0.0,
				Width = item.Width is double w && double.IsFinite(w) && w > 0.0 ? w : null,
				Height = item.Height is double h && double.IsFinite(h) && h > 0.0 ? h : null,
				Monitor = string.IsNullOrWhiteSpace(item.Monitor) ? null : item.Monitor,
				Options = item.Options == null ? new Dictionary<string, string>() : new Dictionary<string, string>(item.Options)
			});
		}
		return list;
	}

	private Data Read()
	{
		if (!File.Exists(_file))
		{
			return new Data();
		}
		try
		{
			Data? data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_file), ThemeJson.Options);
			if (data?.Themes == null)
			{
				return new Data();
			}
			data.Themes = new Dictionary<string, List<CardSettings>>(data.Themes.Where(p => p.Value != null), StringComparer.Ordinal);
			return data;
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: не удалось прочитать cards.json", ex);
			return new Data();
		}
	}

	private void Write(Data data)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);
			string tmp = _file + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(data, ThemeJson.Options));
			File.Move(tmp, _file, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: не удалось сохранить cards.json", ex);
		}
	}
}
