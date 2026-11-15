using System;
using System.IO;

namespace Metamorph.Core.Services;

public sealed class AppPaths
{
	public string Root { get; }

	public string BuiltInThemes { get; }

	public string UserThemes => Path.Combine(Root, "themes");

	public string Wallpapers => Path.Combine(Root, "wallpapers");

	public string WallpaperLibrary => Path.Combine(Root, "wallpapers", "library");

	public string BackupDir => Path.Combine(Root, "backup");

	public string BackupFile => Path.Combine(BackupDir, "original-settings.json");

	public string StateFile => Path.Combine(Root, "state.json");

	public string DockFile => Path.Combine(Root, "dock.json");

	public string LogFile => Path.Combine(Root, "metamorph.log");

	public string SettingsFile => Path.Combine(Root, "settings.json");

	public string Generated => Path.Combine(Root, "generated");

	public string Updates => Path.Combine(Root, "updates");

	public AppPaths(string? root = null, string? builtInThemes = null)
	{
		Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Metamorph");
		BuiltInThemes = builtInThemes ?? Path.Combine(AppContext.BaseDirectory, "themes");
		Directory.CreateDirectory(UserThemes);
		Directory.CreateDirectory(Wallpapers);
		Directory.CreateDirectory(BackupDir);
	}
}
