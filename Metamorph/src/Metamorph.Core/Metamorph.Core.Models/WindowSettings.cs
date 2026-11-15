namespace Metamorph.Core.Models;

public sealed class WindowSettings
{
	public ActiveBorderSettings ActiveBorder { get; set; } = new ActiveBorderSettings();


	public TilingSettings Tiling { get; set; } = new TilingSettings();

}
