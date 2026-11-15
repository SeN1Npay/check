using Metamorph.Core.Dock;

namespace Metamorph.Core.Services;

public sealed class MetamorphServices
{
	public AppSettingsStore Settings { get; }

	public UpdateService Updates { get; }

	public GalleryService Gallery { get; }

	public WeatherService Weather { get; }

	public DockPinStore DockPins { get; }

	public AppPaths Paths { get; }

	public ProfileStore Profiles { get; }

	public SettingsBackup Backup { get; }

	public WallpaperService Wallpapers { get; }

	public AppStateStore State { get; }

	public ThemeEngine Engine { get; }

	public MetamorphServices(AppPaths? paths = null)
	{
		Paths = paths ?? new AppPaths();
		Profiles = new ProfileStore(Paths);
		Backup = new SettingsBackup(Paths);
		Wallpapers = new WallpaperService(Paths);
		State = new AppStateStore(Paths);
		Engine = new ThemeEngine(Paths, Profiles, Backup, Wallpapers, State);
		Updates = new UpdateService(Paths);
		Gallery = new GalleryService(Paths, Profiles);
		Weather = new WeatherService();
		DockPins = new DockPinStore(Paths);
		Settings = new AppSettingsStore(Paths);
	}
}
