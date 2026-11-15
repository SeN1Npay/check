namespace Metamorph.Core.Models;

public sealed class WallpaperScheduleEntry
{
	public string At { get; set; } = "07:00";


	public string? Path { get; set; }

	public string? From { get; set; }

	public string? To { get; set; }
}
