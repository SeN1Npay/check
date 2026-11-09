using System.Collections.Generic;
using Metamorph.Core.Assistant;
using Metamorph.Core.Mascot;

namespace Metamorph.Core.Services;

public sealed class AppSettings
{
	public string Language { get; set; } = "auto";


	public Dictionary<string, string> Hotkeys { get; set; } = HotkeyActions.Defaults();


	public LauncherSettings Launcher { get; set; } = new LauncherSettings();


	public ScheduleSettings Schedule { get; set; } = new ScheduleSettings();


	public UpdateSettings Updates { get; set; } = new UpdateSettings();


	public GallerySettings Gallery { get; set; } = new GallerySettings();


	public WallpaperPrefs Wallpaper { get; set; } = new WallpaperPrefs();


	public MascotSettings Mascot { get; set; } = new MascotSettings();


	public AssistantSettings Assistant { get; set; } = new AssistantSettings();

}
