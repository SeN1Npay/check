namespace Metamorph.Core.Services;

public sealed class ScheduleSettings
{
	public bool Enabled { get; set; }

	public string DayThemeId { get; set; } = "catppuccin-latte";


	public string NightThemeId { get; set; } = "catppuccin-mocha";


	public string DayStart { get; set; } = "07:00";


	public string NightStart { get; set; } = "20:00";


	public bool FollowSun { get; set; }

	public double? Latitude { get; set; }

	public double? Longitude { get; set; }
}
