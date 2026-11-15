namespace Metamorph.Core.Models;

public sealed class ScriptModuleSettings
{
	public string Name { get; set; } = "";


	public string Command { get; set; } = "";


	public string? Label { get; set; }

	public int IntervalSeconds { get; set; } = 10;


	public string? OnClick { get; set; }
}
