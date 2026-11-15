namespace Metamorph.Core.Models;

public sealed class TilingSettings
{
	public bool Hotkeys { get; set; } = true;


	public int Gaps { get; set; } = 8;


	public double MasterRatio { get; set; } = 0.55;


	public TilingEngine Engine { get; set; }

	public bool StartEngine { get; set; }
}
