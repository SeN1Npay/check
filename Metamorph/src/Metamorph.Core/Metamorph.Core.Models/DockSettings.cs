namespace Metamorph.Core.Models;

public sealed class DockSettings
{
	public bool Enabled { get; set; }

	public DockPosition Position { get; set; }

	public DockBehavior Behavior { get; set; } = DockBehavior.DodgeWindows;


	public DockStyle Style { get; set; }

	public int IconSize { get; set; } = 48;


	public DockHoverEffect HoverEffect { get; set; }

	public double Magnification { get; set; } = 1.8;


	public double MagnifyRange { get; set; } = 2.5;


	public int Spacing { get; set; } = 8;


	public int Padding { get; set; } = 9;


	public int Margin { get; set; } = 8;


	public int CornerRadius { get; set; } = 18;


	public double Opacity { get; set; } = 0.78;


	public string? Background { get; set; }

	public string? BorderColor { get; set; }

	public DockIndicator Indicator { get; set; }

	public bool BounceOnLaunch { get; set; } = true;


	public bool BounceOnAttention { get; set; } = true;


	public bool ShowLabels { get; set; } = true;


	public bool ShowAppsButton { get; set; } = true;


	public bool ShowTrash { get; set; } = true;


	public bool ShowRunningApps { get; set; } = true;


	public double AnimationSpeed { get; set; } = 1.0;


	public ScreenMode Screens { get; set; }

	public bool Blur { get; set; } = true;


	public bool Previews { get; set; } = true;


	public bool ShowRecentFiles { get; set; } = true;


	public bool ShowBadges { get; set; } = true;

}
