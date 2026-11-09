using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class WeatherService
{
	private static readonly HttpClient Http;

	private readonly Dictionary<string, (DateTime At, WeatherNow Value)> _cache = new Dictionary<string, (DateTime, WeatherNow)>();

	static WeatherService()
	{
		Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(10.0)
		};
		Http.DefaultRequestHeaders.UserAgent.ParseAdd("Metamorph/1.0");
	}

	public async Task<WeatherNow?> GetAsync(WeatherSettings s, CancellationToken ct = default(CancellationToken))
	{
		string key = $"{s.City}|{s.Latitude}|{s.Longitude}|{s.Fahrenheit}";
		if (_cache.TryGetValue(key, out (DateTime, WeatherNow) value) && DateTime.UtcNow - value.Item1 < TimeSpan.FromMinutes(15.0))
		{
			return value.Item2;
		}
		string place = s.City;
		double? latitude = s.Latitude;
		double value2;
		double value3;
		if (latitude.HasValue)
		{
			double valueOrDefault = latitude.GetValueOrDefault();
			latitude = s.Longitude;
			if (latitude.HasValue)
			{
				double valueOrDefault2 = latitude.GetValueOrDefault();
				value2 = valueOrDefault2;
				value3 = valueOrDefault;
				goto IL_021e;
			}
		}
		if (string.IsNullOrWhiteSpace(s.City))
		{
			return null;
		}
		(double, double, string)? tuple = ParseGeocoding(await Http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(s.City) + "&count=1&language=ru&format=json", ct));
		if (!tuple.HasValue)
		{
			return null;
		}
		(value3, value2, place) = tuple.Value;
		goto IL_021e;
		IL_021e:
		IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
		string requestUri = string.Create(invariantCulture, $"https://api.open-meteo.com/v1/forecast?latitude={value3:0.####}&longitude={value2:0.####}&current=temperature_2m,weather_code,wind_speed_10m&timezone=auto{(s.Fahrenheit ? "&temperature_unit=fahrenheit" : "")}");
		WeatherNow weatherNow = ParseForecast(await Http.GetStringAsync(requestUri, ct), s.Fahrenheit, place);
		if ((object)weatherNow != null)
		{
			_cache[key] = (DateTime.UtcNow, weatherNow);
		}
		return weatherNow;
	}

	public static (double Lat, double Lon, string Name)? ParseGeocoding(string json)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		if (!jsonDocument.RootElement.TryGetProperty("results", out var value) || value.GetArrayLength() == 0)
		{
			return null;
		}
		JsonElement jsonElement = value[0];
		JsonElement value2;
		return (jsonElement.GetProperty("latitude").GetDouble(), jsonElement.GetProperty("longitude").GetDouble(), jsonElement.TryGetProperty("name", out value2) ? (value2.GetString() ?? "") : "");
	}

	public static WeatherNow? ParseForecast(string json, bool fahrenheit, string place)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		if (!jsonDocument.RootElement.TryGetProperty("current", out var value))
		{
			return null;
		}
		JsonElement value2;
		JsonElement value3;
		return new WeatherNow(value.GetProperty("temperature_2m").GetDouble(), value.TryGetProperty("weather_code", out value2) ? value2.GetInt32() : 0, value.TryGetProperty("wind_speed_10m", out value3) ? value3.GetDouble() : 0.0, fahrenheit, place);
	}

	public static string Icon(int code)
	{
		if (code >= 80)
		{
			if (code > 82)
			{
				if (code < 95)
				{
					if ((uint)(code - 85) <= 1u)
					{
						goto IL_0097;
					}
					goto IL_00a7;
				}
				return "⛈";
			}
		}
		else
		{
			switch (code)
			{
			case 0:
				return "☀";
			case 1:
			case 2:
				return "⛅";
			case 3:
				return "☁";
			case 45:
			case 48:
				return "\ud83c\udf2b";
			case 51:
			case 52:
			case 53:
			case 54:
			case 55:
			case 56:
			case 57:
				return "\ud83c\udf26";
			case 61:
			case 62:
			case 63:
			case 64:
			case 65:
			case 66:
			case 67:
				break;
			case 71:
			case 72:
			case 73:
			case 74:
			case 75:
			case 76:
			case 77:
				goto IL_0097;
			default:
				goto IL_00a7;
			}
		}
		return "\ud83c\udf27";
		IL_0097:
		return "❄";
		IL_00a7:
		return "•";
	}

	public static string Describe(int code)
	{
		string ru;
		if (code <= 77)
		{
			if (code <= 67)
			{
				if (code <= 57)
				{
					if (code < 51)
					{
						switch (code)
						{
						case 0:
							break;
						case 1:
							goto IL_0069;
						case 2:
							goto IL_0071;
						case 3:
							goto IL_0079;
						case 45:
						case 48:
							goto IL_0081;
						default:
							goto IL_00b9;
						}
						ru = "ясно";
					}
					else
					{
						ru = "морось";
					}
				}
				else
				{
					if (code < 61)
					{
						goto IL_00b9;
					}
					ru = "дождь";
				}
			}
			else
			{
				if (code < 71)
				{
					goto IL_00b9;
				}
				ru = "снег";
			}
		}
		else if (code <= 82)
		{
			if (code < 80)
			{
				goto IL_00b9;
			}
			ru = "ливень";
		}
		else if (code < 95)
		{
			if ((uint)(code - 85) > 1u)
			{
				goto IL_00b9;
			}
			ru = "снегопад";
		}
		else
		{
			ru = "гроза";
		}
		goto IL_00bf;
		IL_0079:
		ru = "пасмурно";
		goto IL_00bf;
		IL_0071:
		ru = "переменная облачность";
		goto IL_00bf;
		IL_00b9:
		ru = "нет данных";
		goto IL_00bf;
		IL_0081:
		ru = "туман";
		goto IL_00bf;
		IL_00bf:
		return Loc.T(ru);
		IL_0069:
		ru = "преимущественно ясно";
		goto IL_00bf;
	}
}
