using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Xunit;

namespace Metamorph.Tests;

public class WeatherLocateTests
{
	[Fact]
	public void ParsesIpLocation()
	{
		var r = WeatherService.ParseIpLocation("{\"success\":true,\"latitude\":55.7558,\"longitude\":37.6173,\"city\":\"Москва\"}");
		Assert.NotNull(r);
		Assert.Equal(55.7558, r!.Value.Lat, 4);
		Assert.Equal(37.6173, r.Value.Lon, 4);
		Assert.Equal("Москва", r.Value.Name);
	}

	[Theory]
	[InlineData("{\"success\":false,\"message\":\"Reserved range\"}")]
	[InlineData("{\"success\":true,\"city\":\"X\"}")]
	[InlineData("[]")]
	public void RejectsBadIpLocation(string json)
	{
		Assert.Null(WeatherService.ParseIpLocation(json));
	}

	[Fact]
	public void AutoLocateOnByDefaultAndOldThemesKeepIt()
	{
		Assert.True(new WeatherSettings().AutoLocate);
		ThemeProfile p = ThemeJson.Deserialize("{\"id\":\"x\",\"name\":\"x\",\"bar\":{\"weather\":{\"city\":\"\"}}}")!;
		Assert.True(p.Bar.Weather.AutoLocate);
	}
}
