using System;
using System.IO;
using System.Text.Json;
using Metamorph.Core.Services;

namespace Metamorph.Core.Games;

// Открытые уровни «Мета-платформера»: %AppData%\Metamorph\platformer.json. Битый файл — открыт только первый.
public sealed class PlatformerProgress
{
	private sealed class Data
	{
		public int Unlocked { get; set; } = 1;

		public int BestStage { get; set; }
	}

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private Data _data;

	public string Path { get; }

	public int Unlocked => Math.Clamp(_data.Unlocked, 1, PlatformerLevels.Count);

	// Самый дальний этап бесконечного режима.
	public int BestStage => Math.Max(0, _data.BestStage);

	public PlatformerProgress(string path)
	{
		Path = path;
		_data = Load(path);
	}

	public bool IsUnlocked(int index)
	{
		return index >= 0 && index < Unlocked;
	}

	// Пройден уровень index — открывает следующий. true — открыт новый.
	public bool Complete(int index)
	{
		int next = Math.Min(PlatformerLevels.Count, index + 2);
		if (next <= Unlocked)
		{
			return false;
		}
		_data.Unlocked = next;
		Save();
		return true;
	}

	public void ReachStage(int stage)
	{
		if (stage > _data.BestStage)
		{
			_data.BestStage = stage;
			Save();
		}
	}

	private void Save()
	{
		try
		{
			string? dir = System.IO.Path.GetDirectoryName(Path);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			string tmp = Path + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Json));
			File.Move(tmp, Path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Error("Игры: не удалось сохранить прогресс платформера", ex);
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
			return JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Json) ?? new Data();
		}
		catch (Exception ex)
		{
			Log.Warn("Игры: файл прогресса платформера повреждён, начинаю с нуля: " + ex.Message);
			return new Data();
		}
	}
}
