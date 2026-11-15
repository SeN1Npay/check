using System;
using System.IO;
using System.Text.Json;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class AppStateStore
{
	private readonly AppPaths _paths;

	public AppStateStore(AppPaths paths)
	{
		_paths = paths;
	}

	public AppState Load()
	{
		try
		{
			return File.Exists(_paths.StateFile) ? (JsonSerializer.Deserialize<AppState>(File.ReadAllText(_paths.StateFile), ThemeJson.Options) ?? new AppState()) : new AppState();
		}
		catch (Exception ex)
		{
			Log.Warn("state.json повреждён и будет пересоздан: " + ex.Message);
			return new AppState();
		}
	}

	public void Save(AppState state)
	{
		File.WriteAllText(_paths.StateFile, JsonSerializer.Serialize(state, ThemeJson.Options));
	}
}
