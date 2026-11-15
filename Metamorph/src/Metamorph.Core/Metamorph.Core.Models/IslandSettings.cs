using System.Collections.Generic;

namespace Metamorph.Core.Models;

public sealed class IslandSettings
{
	public static readonly IReadOnlyList<string> KnownActivities = new string[8] { "volume", "layout", "capslock", "battery", "theme", "privacy", "network", "clipboard" };

	public bool Enabled { get; set; }

	public ScreenMode Screens { get; set; }

	public bool OverBar { get; set; }

	public int Offset { get; set; } = 6;


	public int Height { get; set; } = 34;


	public int Width { get; set; } = 250;


	public int ExpandedWidth { get; set; } = 400;


	public IslandIdle Idle { get; set; }

	public string? Background { get; set; }

	public double Opacity { get; set; } = 0.96;


	public bool AlbumArt { get; set; } = true;


	public bool Visualizer { get; set; } = true;


	public bool ExpandOnHover { get; set; } = true;


	public int TrackPopupSeconds { get; set; } = 3;


	public List<string> Activities { get; set; } = new List<string> { "volume", "layout", "capslock", "battery", "theme", "privacy", "network" };


	public double AnimationSpeed { get; set; } = 1.0;


	public bool HideInFullscreen { get; set; } = true;


	public string ClockFormat { get; set; } = "HH:mm";


	public bool HotEdge { get; set; } = true;


	public bool FullscreenActivities { get; set; } = true;

	// В покое и с музыкой остров прячется, если под ним активное окно (вкладки, заголовок); вызов — у верхней кромки.
	public bool DodgeWindows { get; set; } = true;


	public IslandStyle Style { get; set; }
}
