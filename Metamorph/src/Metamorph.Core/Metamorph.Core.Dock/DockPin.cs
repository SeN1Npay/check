namespace Metamorph.Core.Dock;

public sealed class DockPin
{
	public string Path { get; set; } = "";


	public string? Arguments { get; set; }

	public string Name { get; set; } = "";


	public string? Target { get; set; }

	public string? AppId { get; set; }
}
