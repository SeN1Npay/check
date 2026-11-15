using System.Collections.Generic;

namespace Metamorph.Core.Models;

public sealed class WidgetSettings
{
	public static readonly IReadOnlyList<string> Known = new string[11]
	{
		"clock", "date", "cpu", "ram", "disk", "network", "uptime", "battery", "weather", "media",
		"hostname"
	};

	public bool Enabled { get; set; }

	public WidgetCorner Corner { get; set; } = WidgetCorner.TopRight;


	public List<string> Modules { get; set; } = new List<string> { "clock", "date", "cpu", "ram", "disk", "network", "uptime" };


	public string? FontFamily { get; set; }

	public double Scale { get; set; } = 1.0;


	public double Opacity { get; set; }
}
