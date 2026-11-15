using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metamorph.Core.Models;

public static class ThemeJson
{
	public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		WriteIndented = true,
		Converters = { (JsonConverter)new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	public static ThemeProfile Deserialize(string json)
	{
		return JsonSerializer.Deserialize<ThemeProfile>(json, Options) ?? throw new InvalidDataException("Пустой файл темы.");
	}

	public static string Serialize(ThemeProfile profile)
	{
		return JsonSerializer.Serialize(profile, Options);
	}

	public static ThemeProfile Clone(ThemeProfile profile)
	{
		ThemeProfile themeProfile = Deserialize(Serialize(profile));
		themeProfile.SourcePath = profile.SourcePath;
		themeProfile.IsBuiltIn = profile.IsBuiltIn;
		return themeProfile;
	}
}
