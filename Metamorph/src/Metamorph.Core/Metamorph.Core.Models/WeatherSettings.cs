namespace Metamorph.Core.Models;

public sealed class WeatherSettings
{
	public string City { get; set; } = "";


	public double? Latitude { get; set; }

	public double? Longitude { get; set; }

	public bool Fahrenheit { get; set; }
}
