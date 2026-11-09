using System.Collections.Generic;

namespace Metamorph.Core.Models;

public sealed class BarSettings
{
	public bool Enabled { get; set; } = true;


	public BarPosition Position { get; set; }

	public int Height { get; set; } = 28;


	public double Opacity { get; set; } = 0.85;


	public bool Blur { get; set; } = true;


	public string FontFamily { get; set; } = "Cascadia Mono, Consolas";


	public double FontSize { get; set; } = 13.0;


	public string ClockFormat { get; set; } = "ddd dd MMM  HH:mm";


	public List<string> Left { get; set; } = new List<string> { "launcher", "window" };


	public List<string> Center { get; set; } = new List<string> { "clock" };


	public List<string> Right { get; set; } = new List<string> { "layout", "cpu", "ram", "battery" };


	public ScreenMode Screens { get; set; }

	public BarStyle Style { get; set; }

	// Панель не резервирует место и выезжает, когда курсор упирается в край экрана.
	public bool AutoHide { get; set; }

	// Плавное появление панели и подсветка модулей при наведении.
	public bool Animations { get; set; } = true;

	public WeatherSettings Weather { get; set; } = new WeatherSettings();


	public List<ScriptModuleSettings> Scripts { get; set; } = new List<ScriptModuleSettings>();

}
