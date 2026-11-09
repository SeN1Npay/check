using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Metamorph.Core.Assistant;
using Metamorph.Core.Mascot;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class AppSettingsStore
{
	private readonly AppPaths _paths;

	private AppSettings? _cached;

	public AppSettings Current => _cached ?? (_cached = Load());

	public event EventHandler? Changed;

	public AppSettingsStore(AppPaths paths)
	{
		_paths = paths;
	}

	public AppSettings Load()
	{
		AppSettings appSettings;
		try
		{
			appSettings = (File.Exists(_paths.SettingsFile) ? (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsFile), ThemeJson.Options) ?? new AppSettings()) : new AppSettings());
		}
		catch (Exception ex)
		{
			Log.Warn("settings.json повреждён, используются настройки по умолчанию: " + ex.Message);
			appSettings = new AppSettings();
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>(appSettings.Hotkeys ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in HotkeyActions.Defaults())
		{
			dictionary.TryAdd(key, value);
		}
		appSettings.Hotkeys = dictionary;
		AppSettings appSettings2 = appSettings;
		if (appSettings2.Launcher == null)
		{
			LauncherSettings launcherSettings2 = (appSettings2.Launcher = new LauncherSettings());
		}
		appSettings2 = appSettings;
		if (appSettings2.Schedule == null)
		{
			ScheduleSettings scheduleSettings2 = (appSettings2.Schedule = new ScheduleSettings());
		}
		appSettings2 = appSettings;
		if (appSettings2.Updates == null)
		{
			UpdateSettings updateSettings2 = (appSettings2.Updates = new UpdateSettings());
		}
		appSettings2 = appSettings;
		if (appSettings2.Gallery == null)
		{
			GallerySettings gallerySettings2 = (appSettings2.Gallery = new GallerySettings());
		}
		appSettings2 = appSettings;
		if (appSettings2.Wallpaper == null)
		{
			WallpaperPrefs wallpaperPrefs2 = (appSettings2.Wallpaper = new WallpaperPrefs());
		}
		appSettings2 = appSettings;
		if (appSettings2.Mascot == null)
		{
			MascotSettings mascotSettings2 = (appSettings2.Mascot = new MascotSettings());
		}
		appSettings2 = appSettings;
		AssistantSettings assistantSettings2;
		if (appSettings2.Assistant == null)
		{
			assistantSettings2 = (appSettings2.Assistant = new AssistantSettings());
		}
		assistantSettings2 = appSettings.Assistant;
		if (assistantSettings2.Llm == null)
		{
			LlmSettings llmSettings2 = (assistantSettings2.Llm = new LlmSettings());
		}
		assistantSettings2 = appSettings.Assistant;
		if (assistantSettings2.Telegram == null)
		{
			TelegramSettings telegramSettings2 = (assistantSettings2.Telegram = new TelegramSettings());
		}
		_cached = appSettings;
		return appSettings;
	}

	public void Save(AppSettings settings)
	{
		Directory.CreateDirectory(_paths.Root);
		string text = _paths.SettingsFile + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(settings, ThemeJson.Options));
		File.Move(text, _paths.SettingsFile, overwrite: true);
		_cached = settings;
		this.Changed?.Invoke(this, EventArgs.Empty);
	}
}
