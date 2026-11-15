using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Metamorph.Core.Services;

namespace Metamorph.Core.Games;

public sealed class GameRecord
{
	public int Best { get; set; }

	public int Plays { get; set; }

	public DateTime? LastPlayed { get; set; }
}

// Рекорды мини-игр: %AppData%\Metamorph\games.json. Битый или отсутствующий файл — просто пустые рекорды.
public sealed class GameScores
{
	private sealed class Data
	{
		public Dictionary<string, GameRecord> Games { get; set; } = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);

		public string? LastGame { get; set; }
	}

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	private readonly object _lock = new object();

	private Data _data;

	public string Path { get; }

	public string? LastGame
	{
		get
		{
			lock (_lock)
			{
				return _data.LastGame;
			}
		}
	}

	public GameScores(string path)
	{
		Path = path;
		_data = Load(path);
	}

	public int Best(string game)
	{
		lock (_lock)
		{
			return _data.Games.TryGetValue(game, out GameRecord? r) ? r.Best : 0;
		}
	}

	public GameRecord Get(string game)
	{
		lock (_lock)
		{
			return _data.Games.TryGetValue(game, out GameRecord? r) ? new GameRecord { Best = r.Best, Plays = r.Plays, LastPlayed = r.LastPlayed } : new GameRecord();
		}
	}

	// Записывает результат партии; true — новый рекорд (строго больше прежнего и больше нуля).
	public bool Submit(string game, int score, DateTime? when = null)
	{
		bool record;
		lock (_lock)
		{
			if (!_data.Games.TryGetValue(game, out GameRecord? r))
			{
				r = new GameRecord();
				_data.Games[game] = r;
			}
			record = score > r.Best && score > 0;
			if (record)
			{
				r.Best = score;
			}
			r.Plays++;
			r.LastPlayed = when ?? DateTime.Now;
			_data.LastGame = game;
		}
		Save();
		return record;
	}

	public void Touch(string game)
	{
		lock (_lock)
		{
			_data.LastGame = game;
		}
		Save();
	}

	public void Save()
	{
		try
		{
			string json;
			lock (_lock)
			{
				json = JsonSerializer.Serialize(_data, Json);
			}
			string? dir = System.IO.Path.GetDirectoryName(Path);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string tmp = Path + ".tmp";
			File.WriteAllText(tmp, json);
			File.Move(tmp, Path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Error("Игры: не удалось сохранить рекорды", ex);
		}
	}

	private static Data Load(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return new Data();
			}
			Data? data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Json);
			if (data?.Games == null)
			{
				return new Data();
			}
			var games = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);
			foreach (KeyValuePair<string, GameRecord> kv in data.Games)
			{
				if (kv.Value != null)
				{
					kv.Value.Best = Math.Max(0, kv.Value.Best);
					kv.Value.Plays = Math.Max(0, kv.Value.Plays);
					games[kv.Key] = kv.Value;
				}
			}
			data.Games = games;
			return data;
		}
		catch (Exception ex)
		{
			Log.Warn("Игры: файл рекордов повреждён, начинаю с нуля: " + ex.Message);
			return new Data();
		}
	}
}
