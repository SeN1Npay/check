namespace Metamorph.Core.Services;

public sealed class WallpaperPrefs
{
	public WallpaperChoice? Override { get; set; }

	public bool KeepOnThemeChange { get; set; } = true;


	public int RotateMinutes { get; set; }

	public bool PauseWhenCovered { get; set; } = true;


	public bool PauseOnBattery { get; set; } = true;


	public double Volume { get; set; } = 0.5;

}
