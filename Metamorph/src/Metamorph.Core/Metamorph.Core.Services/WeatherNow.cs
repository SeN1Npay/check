using System;

namespace Metamorph.Core.Services;

public sealed record WeatherNow(double Temperature, int Code, double WindSpeed, bool Fahrenheit, string Place)
{
	public string Icon => WeatherService.Icon(Code);

	public string Description => WeatherService.Describe(Code);

	public string TemperatureText => $"{Math.Round(Temperature):0}°{(Fahrenheit ? "F" : "C")}";
}
