using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Metamorph.Core.Remote;

public static class McpArgs
{
	public static string? Str(this JsonObject args, string name)
	{
		JsonNode jsonNode = args[name];
		if (jsonNode == null)
		{
			return null;
		}
		if (jsonNode.GetValueKind() != JsonValueKind.String)
		{
			return jsonNode.ToJsonString(McpServer.Json);
		}
		return jsonNode.GetValue<string>();
	}

	public static int Int(this JsonObject args, string name, int fallback, int min = int.MinValue, int max = int.MaxValue)
	{
		JsonNode jsonNode = args[name];
		int value;
		try
		{
			JsonValueKind? jsonValueKind = jsonNode?.GetValueKind();
			if (!jsonValueKind.HasValue)
			{
				goto IL_0062;
			}
			JsonValueKind valueOrDefault = jsonValueKind.GetValueOrDefault();
			int num;
			if (valueOrDefault != JsonValueKind.String)
			{
				if (valueOrDefault != JsonValueKind.Number)
				{
					goto IL_0062;
				}
				num = (int)Math.Round(jsonNode.GetValue<double>());
			}
			else
			{
				if (!int.TryParse(jsonNode.GetValue<string>(), out var result))
				{
					goto IL_0062;
				}
				num = result;
			}
			goto IL_0064;
			IL_0062:
			num = fallback;
			goto IL_0064;
			IL_0064:
			value = num;
		}
		catch
		{
			value = fallback;
		}
		return Math.Clamp(value, min, max);
	}

	public static double Num(this JsonObject args, string name, double fallback, double min = double.MinValue, double max = double.MaxValue)
	{
		JsonNode jsonNode = args[name];
		double num2;
		try
		{
			JsonValueKind? jsonValueKind = jsonNode?.GetValueKind();
			if (!jsonValueKind.HasValue)
			{
				goto IL_0066;
			}
			JsonValueKind valueOrDefault = jsonValueKind.GetValueOrDefault();
			double num;
			if (valueOrDefault != JsonValueKind.String)
			{
				if (valueOrDefault != JsonValueKind.Number)
				{
					goto IL_0066;
				}
				num = jsonNode.GetValue<double>();
			}
			else
			{
				if (!double.TryParse(jsonNode.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					goto IL_0066;
				}
				num = result;
			}
			goto IL_0068;
			IL_0066:
			num = fallback;
			goto IL_0068;
			IL_0068:
			num2 = num;
		}
		catch
		{
			num2 = fallback;
		}
		if (!double.IsFinite(num2))
		{
			return fallback;
		}
		return Math.Clamp(num2, min, max);
	}

	public static bool Bool(this JsonObject args, string name, bool fallback)
	{
		JsonNode jsonNode = args[name];
		switch (jsonNode?.GetValueKind())
		{
		case JsonValueKind.True:
			return true;
		case JsonValueKind.False:
			return false;
		case JsonValueKind.String:
		{
			bool result;
			switch (jsonNode.GetValue<string>().Trim().ToLowerInvariant())
			{
			case "true":
			case "1":
			case "yes":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		default:
			return fallback;
		}
	}
}
