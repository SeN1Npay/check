using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Models;

public static class BarModules
{
	public static readonly IReadOnlyList<string> Known = new string[23]
	{
		"launcher", "window", "clock", "cpu", "ram", "battery", "layout", "uptime", "tray", "workspaces",
		"media", "volume", "network", "bluetooth", "power", "weather", "disk", "cava", "gpu", "mic",
		"timer", "screenshot", "wallpaper"
	};

	public const string ScriptPrefix = "script:";

	public static bool IsKnown(string name)
	{
		return Known.Contains(name.Trim().ToLowerInvariant());
	}

	public static bool IsScript(string name, out string scriptName)
	{
		string text = name.Trim();
		if (text.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			int length = "script:".Length;
			scriptName = text2.Substring(length, text2.Length - length).Trim();
			return scriptName.Length > 0;
		}
		scriptName = "";
		return false;
	}
}
