using System.Collections.Generic;
using System.Threading.Tasks;
using Metamorph.Core.Mascot;

namespace Metamorph.Core.Assistant;

public interface IAssistantHost
{
	string MascotName { get; }

	IReadOnlyList<string> ThemeNames { get; }

	string? ActiveTheme { get; }

	IReadOnlyList<string> WallpaperNames { get; }

	int? Volume { get; }

	FocusTimer Timer { get; }

	Task<string?> ApplyThemeAsync(string query);

	Task<string?> SurpriseThemeAsync();

	Task NextThemeAsync();

	void ToggleDayNight();

	Task NextWallpaperAsync();

	bool ToggleWallpaperPause();

	Task<string?> SetWallpaperAsync(string query);

	Task MediaAsync(MediaCommand command);

	Task<string?> NowPlayingAsync();

	int? SetVolume(int? level, int delta);

	void SetMute(bool mute);

	string? OpenApp(string query);

	void OpenUrl(string url);

	void Mascot(MascotAction action);

	void HideMascot();

	void ShowIsland();

	void ShowLauncher();

	// game: null — окно выбора, "dino" | "tetris" — сразу игра.
	void PlayGame(string? game);
}
