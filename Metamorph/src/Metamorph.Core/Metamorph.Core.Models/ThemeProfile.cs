using System.Text.Json.Serialization;

namespace Metamorph.Core.Models;

public sealed class ThemeProfile
{
	public string Id { get; set; } = "";


	public string Name { get; set; } = "";


	public string Description { get; set; } = "";


	public string Author { get; set; } = "";


	public ColorMode Mode { get; set; }

	public Palette Palette { get; set; } = new Palette();


	public WallpaperSettings Wallpaper { get; set; } = new WallpaperSettings();


	public SystemSettings System { get; set; } = new SystemSettings();


	public TaskbarSettings Taskbar { get; set; } = new TaskbarSettings();


	public BarSettings Bar { get; set; } = new BarSettings();


	public DockSettings Dock { get; set; } = new DockSettings();


	public WindowSettings Windows { get; set; } = new WindowSettings();


	public DesktopSettings Desktop { get; set; } = new DesktopSettings();


	public AppsSettings Apps { get; set; } = new AppsSettings();


	public CursorSettings Cursors { get; set; } = new CursorSettings();


	public SoundSettings Sounds { get; set; } = new SoundSettings();


	public IslandSettings Island { get; set; } = new IslandSettings();


	[JsonIgnore]
	public string? SourcePath { get; set; }

	[JsonIgnore]
	public bool IsBuiltIn { get; set; }
}
