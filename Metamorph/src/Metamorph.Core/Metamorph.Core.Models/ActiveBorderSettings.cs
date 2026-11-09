namespace Metamorph.Core.Models;

public sealed class ActiveBorderSettings
{
	public bool Enabled { get; set; }

	public int Width { get; set; } = 3;


	public string? Color { get; set; }

	public bool Gradient { get; set; } = true;


	public int Radius { get; set; }
}
