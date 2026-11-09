namespace Metamorph.Core.Services;

public sealed class LauncherSettings
{
	public bool Enabled { get; set; } = true;


	public string WebSearch { get; set; } = "https://duckduckgo.com/?q={0}";

}
