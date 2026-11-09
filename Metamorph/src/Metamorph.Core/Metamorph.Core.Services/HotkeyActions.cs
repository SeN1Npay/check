using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Services;

public static class HotkeyActions
{
	public const string Launcher = "launcher";

	public const string ToggleDock = "toggleDock";

	public const string ToggleBar = "toggleBar";

	public const string NextTheme = "nextTheme";

	public const string DayNight = "dayNight";

	public const string ShowMain = "showMain";

	public const string SnapLeft = "snapLeft";

	public const string SnapRight = "snapRight";

	public const string SnapUp = "snapUp";

	public const string SnapDown = "snapDown";

	public const string Maximize = "maximize";

	public const string Center = "center";

	public const string TileAll = "tileAll";

	public const string ShowIsland = "showIsland";

	public const string NextWallpaper = "nextWallpaper";

	public const string PauseWallpaper = "pauseWallpaper";

	public const string Assistant = "assistant";

	public const string SurpriseTheme = "surpriseTheme";

	public static readonly IReadOnlyList<string> All = Defaults().Keys.ToList();

	public static Dictionary<string, string> Defaults()
	{
		return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["launcher"] = "Alt+Space",
			["toggleDock"] = "Ctrl+Alt+D",
			["toggleBar"] = "Ctrl+Alt+B",
			["nextTheme"] = "Ctrl+Alt+T",
			["dayNight"] = "Ctrl+Alt+N",
			["showMain"] = "Ctrl+Alt+M",
			["snapLeft"] = "Win+Alt+Left",
			["snapRight"] = "Win+Alt+Right",
			["snapUp"] = "Win+Alt+Up",
			["snapDown"] = "Win+Alt+Down",
			["maximize"] = "Win+Alt+F",
			["center"] = "Win+Alt+C",
			["tileAll"] = "Win+Alt+Enter",
			["showIsland"] = "Ctrl+Alt+Space",
			["nextWallpaper"] = "Ctrl+Alt+W",
			["pauseWallpaper"] = "Ctrl+Alt+P",
			["assistant"] = "Ctrl+Alt+A",
			["surpriseTheme"] = "Ctrl+Alt+R"
		};
	}
}
