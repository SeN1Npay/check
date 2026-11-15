namespace Metamorph.Core.Services;

public sealed class RegistryValueSnapshot
{
	public string Key { get; set; } = "";


	public string Name { get; set; } = "";


	public string Kind { get; set; } = "";


	public bool Exists { get; set; }

	public int? Dword { get; set; }

	public string? Text { get; set; }

	public string? Binary { get; set; }
}
