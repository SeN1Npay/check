using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.Core.Dock;

public sealed class DockPinStore
{
	private readonly string _file;

	public bool Exists => File.Exists(_file);

	public DockPinStore(AppPaths paths)
	{
		_file = paths.DockFile;
	}

	public List<DockPin> Load(Func<IEnumerable<DockPin>>? importDefaults = null)
	{
		if (File.Exists(_file))
		{
			try
			{
				return (JsonSerializer.Deserialize<DockData>(File.ReadAllText(_file), ThemeJson.Options)?.Pins ?? new List<DockPin>()).Where((DockPin p) => !string.IsNullOrWhiteSpace(p.Path)).ToList();
			}
			catch (Exception ex)
			{
				Log.Error("Не удалось прочитать dock.json", ex);
				return new List<DockPin>();
			}
		}
		List<DockPin> list;
		try
		{
			list = (from p in importDefaults?.Invoke()
				where !string.IsNullOrWhiteSpace(p.Path)
				select p).ToList() ?? new List<DockPin>();
		}
		catch (Exception ex2)
		{
			Log.Error("Импорт закреплений панели задач", ex2);
			list = new List<DockPin>();
		}
		Save(list);
		return list;
	}

	public void Save(IEnumerable<DockPin> pins)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_file));
			string contents = JsonSerializer.Serialize(new DockData
			{
				Pins = pins.ToList()
			}, ThemeJson.Options);
			string text = _file + ".tmp";
			File.WriteAllText(text, contents);
			File.Move(text, _file, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Error("Не удалось сохранить dock.json", ex);
		}
	}
}
