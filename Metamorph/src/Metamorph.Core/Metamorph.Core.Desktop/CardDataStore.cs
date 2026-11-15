using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.Core.Desktop;

public sealed class CardTodo
{
	public string Text { get; set; } = "";

	public bool Done { get; set; }
}

// Содержимое заметок и списков дел (cards-data.json). Общее для всех тем — заметка переживает смену темы.
public sealed class CardDataStore
{
	private sealed class Data
	{
		public Dictionary<string, string> Notes { get; set; } = new Dictionary<string, string>();

		public Dictionary<string, List<CardTodo>> Todos { get; set; } = new Dictionary<string, List<CardTodo>>();
	}

	private readonly string _file;

	private Data? _data;

	public CardDataStore(string file)
	{
		_file = file;
	}

	public CardDataStore(AppPaths paths)
		: this(Path.Combine(paths.Root, "cards-data.json"))
	{
	}

	public static string KeyOf(CardSettings card)
	{
		return card.Options.TryGetValue("id", out string? id) && !string.IsNullOrWhiteSpace(id) ? id : "default";
	}

	public string GetNote(string key)
	{
		return Get().Notes.TryGetValue(key, out string? text) ? text ?? "" : "";
	}

	public void SetNote(string key, string text)
	{
		Get().Notes[key] = text ?? "";
		Write();
	}

	public List<CardTodo> GetTodos(string key)
	{
		if (Get().Todos.TryGetValue(key, out List<CardTodo>? list) && list != null)
		{
			return list.Where(t => t != null && !string.IsNullOrWhiteSpace(t.Text)).Select(t => new CardTodo
			{
				Text = t.Text,
				Done = t.Done
			}).ToList();
		}
		return new List<CardTodo>();
	}

	public void SetTodos(string key, IEnumerable<CardTodo> todos)
	{
		Get().Todos[key] = todos.Select(t => new CardTodo
		{
			Text = t.Text,
			Done = t.Done
		}).ToList();
		Write();
	}

	private Data Get()
	{
		if (_data != null)
		{
			return _data;
		}
		_data = new Data();
		if (File.Exists(_file))
		{
			try
			{
				Data? data = JsonSerializer.Deserialize<Data>(File.ReadAllText(_file), ThemeJson.Options);
				_data.Notes = data?.Notes ?? _data.Notes;
				_data.Todos = data?.Todos ?? _data.Todos;
			}
			catch (Exception ex)
			{
				Log.Error("Карточки: не удалось прочитать cards-data.json", ex);
				// Битый файл не затираем молча — откладываем копию рядом.
				try
				{
					File.Copy(_file, _file + ".bad", overwrite: true);
				}
				catch
				{
				}
			}
		}
		return _data;
	}

	private void Write()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);
			string tmp = _file + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(_data ?? new Data(), ThemeJson.Options));
			File.Move(tmp, _file, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: не удалось сохранить cards-data.json", ex);
		}
	}
}
