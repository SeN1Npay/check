using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Metamorph.App.Games;
using Metamorph.App.Infrastructure;
using Metamorph.App.Launcher;
using Metamorph.Core.Assistant;
using Metamorph.Core.Launcher;
using Metamorph.Core.Mascot;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Assistant;

internal sealed class AssistantHost : IAssistantHost
{
	private static readonly (string[] Names, string Target)[] Aliases = new(string[], string)[8]
	{
		(new string[2] { "notepad", "блокнот" }, "notepad.exe"),
		(new string[3] { "calculator", "calc", "калькулятор" }, "calc.exe"),
		(new string[6] { "explorer", "files", "проводник", "файлы", "мой компьютер", "этот компьютер" }, "explorer.exe"),
		(new string[4] { "settings", "параметры", "настройки windows", "настройки виндовс" }, "ms-settings:"),
		(new string[2] { "task manager", "диспетчер задач" }, "taskmgr.exe"),
		(new string[3] { "paint", "пэинт", "паинт" }, "mspaint.exe"),
		(new string[5] { "terminal", "cmd", "командная строка", "терминал", "консоль" }, "cmd.exe"),
		(new string[3] { "browser", "браузер", "интернет" }, "https://www.google.com")
	};

	private readonly App _app;

	private readonly CoreAudio _audio = new CoreAudio();

	public string MascotName => _app.Services.Settings.Current.Mascot.Name;

	public IReadOnlyList<string> ThemeNames => (from p in _app.Services.Profiles.LoadAll()
		select p.Name).ToList();

	public string? ActiveTheme => _app.Session.Active?.Name;

	public IReadOnlyList<string> WallpaperNames => (from f in _app.Library.List()
		select Path.GetFileNameWithoutExtension(f.TrimEnd('\\', '/'))).ToList();

	public int? Volume
	{
		get
		{
			(float, bool)? tuple = _audio.Read();
			if (!tuple.HasValue)
			{
				return null;
			}
			return (int)Math.Round(tuple.GetValueOrDefault().Item1 * 100f);
		}
	}

	public FocusTimer Timer { get; }

	public AssistantHost(App app, FocusTimer timer)
	{
		_app = app;
		Timer = timer;
	}

	public async Task<string?> ApplyThemeAsync(string query)
	{
		string query2 = query;
		IReadOnlyList<ThemeProfile> readOnlyList = _app.Services.Profiles.LoadAll();
		string latin = IntentParser.ToLatin(query2);
		ThemeProfile match = readOnlyList.FirstOrDefault((ThemeProfile p) => p.Name.Equals(query2, StringComparison.OrdinalIgnoreCase) || p.Id.Equals(query2, StringComparison.OrdinalIgnoreCase)) ?? readOnlyList.FirstOrDefault((ThemeProfile p) => p.Name.Equals(latin, StringComparison.OrdinalIgnoreCase) || p.Id.Equals(latin.Replace(' ', '-'), StringComparison.OrdinalIgnoreCase)) ?? FuzzyMatcher.Filter(readOnlyList, query2, (ThemeProfile p) => p.Name, 1).FirstOrDefault() ?? FuzzyMatcher.Filter(readOnlyList, latin, (ThemeProfile p) => p.Name + " " + p.Id, 1).FirstOrDefault();
		if (match == null)
		{
			return null;
		}
		await _app.ApplyThemeAsync(match);
		return match.Name;
	}

	public async Task<string?> SurpriseThemeAsync()
	{
		return (await _app.SurpriseThemeAsync())?.Name;
	}

	public async Task NextThemeAsync()
	{
		IReadOnlyList<ThemeProfile> readOnlyList = _app.Services.Profiles.LoadAll();
		if (readOnlyList.Count != 0)
		{
			ThemeProfile a = _app.Session.Active;
			int num = ((a != null) ? readOnlyList.ToList().FindIndex((ThemeProfile p) => p.Id == a.Id) : (-1));
			await _app.ApplyThemeAsync(readOnlyList[(num + 1 + readOnlyList.Count) % readOnlyList.Count]);
		}
	}

	public void ToggleDayNight()
	{
		_app.ToggleDayNight();
	}

	public Task NextWallpaperAsync()
	{
		_app.NextWallpaper();
		return Task.CompletedTask;
	}

	public bool ToggleWallpaperPause()
	{
		_app.ToggleWallpaperPause();
		return _app.Session.IsWallpaperPaused;
	}

	public async Task<string?> SetWallpaperAsync(string query)
	{
		string query2 = query;
		IReadOnlyList<string> readOnlyList = _app.Library.List();
		string file = readOnlyList.FirstOrDefault((string f) => Path.GetFileNameWithoutExtension(f.TrimEnd('\\', '/')).Equals(query2, StringComparison.OrdinalIgnoreCase)) ?? FuzzyMatcher.Filter(readOnlyList, query2, (string f) => Path.GetFileNameWithoutExtension(f.TrimEnd('\\', '/')), 1).FirstOrDefault();
		if (file == null)
		{
			return null;
		}
		WallpaperChoice wallpaperOverride = _app.WallpaperOverride;
		await _app.ApplyWallpaperChoiceAsync(new WallpaperChoice
		{
			Path = file,
			Muted = (wallpaperOverride?.Muted ?? true),
			Style = (wallpaperOverride?.Style ?? WallpaperStyle.Fill)
		});
		return Path.GetFileNameWithoutExtension(file.TrimEnd('\\', '/'));
	}

	public Task MediaAsync(MediaCommand command)
	{
		return command switch
		{
			MediaCommand.Next => MediaInfo.NextAsync(), 
			MediaCommand.Previous => MediaInfo.PreviousAsync(), 
			_ => MediaInfo.PlayPauseAsync(), 
		};
	}

	public async Task<string?> NowPlayingAsync()
	{
		MediaNow mediaNow = await MediaInfo.GetAsync();
		return ((object)mediaNow == null) ? null : MediaInfo.Format(mediaNow, 80);
	}

	public int? SetVolume(int? level, int delta)
	{
		(float, bool)? tuple = _audio.Read();
		if (tuple.HasValue)
		{
			(float, bool) valueOrDefault = tuple.GetValueOrDefault();
			int num = Math.Clamp(level ?? ((int)Math.Round(valueOrDefault.Item1 * 100f) + delta), 0, 100);
			_audio.SetLevel((float)num / 100f);
			if (valueOrDefault.Item2 && num > 0)
			{
				_audio.ToggleMute();
			}
			return num;
		}
		return null;
	}

	public void SetMute(bool mute)
	{
		(float, bool)? tuple = _audio.Read();
		if (tuple.HasValue && tuple.GetValueOrDefault().Item2 != mute)
		{
			_audio.ToggleMute();
		}
	}

	public string? OpenApp(string query)
	{
		string query2 = query;
		string value = query2.Trim().ToLowerInvariant();
		IReadOnlyList<AppEntry> current = AppCatalog.Current;
		if (current.Count == 0)
		{
			AppCatalog.RefreshAsync();
		}
		AppEntry appEntry = current.FirstOrDefault((AppEntry a) => a.Name.Equals(query2, StringComparison.OrdinalIgnoreCase)) ?? FuzzyMatcher.Filter(current, query2, (AppEntry a) => a.Name, 1).FirstOrDefault();
		if ((object)appEntry != null)
		{
			appEntry.Launch();
			return appEntry.Name;
		}
		(string[], string)[] aliases = Aliases;
		for (int i = 0; i < aliases.Length; i++)
		{
			var (source, fileName) = aliases[i];
			if (source.Contains(value))
			{
				Process.Start(new ProcessStartInfo(fileName)
				{
					UseShellExecute = true
				});
				return query2;
			}
		}
		return null;
	}

	public void OpenUrl(string url)
	{
		if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
	}

	public void Mascot(MascotAction action)
	{
		_app.MascotBrain?.Command(action);
	}

	public void HideMascot()
	{
		if (_app.IsMascotVisible)
		{
			_app.ToggleMascot();
		}
	}

	public void ShowIsland()
	{
		_app.Session.SummonIsland();
	}

	public void ShowLauncher()
	{
		_app.ShowLauncher();
	}

	public void PlayGame(string? game)
	{
		_app.Dispatcher.BeginInvoke((Action)delegate
		{
			try
			{
				GameHub.Show(_app, game);
			}
			catch (Exception ex)
			{
				Log.Error("Игры: не удалось открыть из помощника", ex);
			}
		});
	}
}
