using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public static class TilingIntegration
{
	public static string KomorebiConfig
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("KOMOREBI_CONFIG_HOME");
			return Path.Combine((environmentVariable != null && environmentVariable.Length > 0) ? environmentVariable : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "komorebi.json");
		}
	}

	public static string GlazeConfig => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".glzr", "glazewm", "config.yaml");

	public static string Apply(ThemeProfile profile)
	{
		TilingSettings tiling = profile.Windows.Tiling;
		switch (tiling.Engine)
		{
		case TilingEngine.Komorebi:
		{
			JsonObject root = JsoncFile.Load(KomorebiConfig);
			ApplyKomorebi(root, profile);
			JsoncFile.Save(KomorebiConfig, root);
			if (IsRunning("komorebi"))
			{
				Run("komorebic", "reload-configuration");
			}
			else if (tiling.StartEngine)
			{
				Run("komorebic", "start");
			}
			return Loc.T("komorebi: цвета рамок и отступы");
		}
		case TilingEngine.GlazeWM:
			if (!File.Exists(GlazeConfig))
			{
				return Loc.T("GlazeWM: config.yaml не найден (запустите GlazeWM один раз)");
			}
			JsoncFile.BackupOnce(GlazeConfig);
			File.WriteAllText(GlazeConfig, ApplyGlaze(File.ReadAllText(GlazeConfig), profile));
			if (IsRunning("glazewm"))
			{
				Run("glazewm", "command wm-reload-config");
			}
			else if (tiling.StartEngine)
			{
				Run("glazewm", "start");
			}
			return Loc.T("GlazeWM: цвета рамки и отступы");
		default:
			return "";
		}
	}

	public static void RestoreConfigs()
	{
		string[] array = new string[2] { KomorebiConfig, GlazeConfig };
		foreach (string text in array)
		{
			string text2 = text + ".metamorph.bak";
			if (File.Exists(text2))
			{
				File.Move(text2, text, overwrite: true);
			}
		}
	}

	public static void ApplyKomorebi(JsonObject root, ThemeProfile profile)
	{
		Palette palette = profile.Palette;
		int gaps = profile.Windows.Tiling.Gaps;
		root["default_workspace_padding"] = gaps;
		root["default_container_padding"] = gaps;
		root["border"] = true;
		root["border_width"] = Math.Max(1, profile.Windows.ActiveBorder.Width);
		JsonObject jsonObject = (JsonObject)(root["border_colours"] = new JsonObject
		{
			["single"] = H(profile.Windows.ActiveBorder.Color ?? palette.Accent),
			["stack"] = H(palette.Accent2),
			["monocle"] = H(palette.Yellow),
			["unfocused"] = H(palette.Muted)
		});
		if (root.ContainsKey("active_window_border_colours"))
		{
			root["active_window_border_colours"] = jsonObject.DeepClone();
		}
		static string H(string hex)
		{
			return Rgba.Parse(hex).WithAlpha(byte.MaxValue).ToHex();
		}
	}

	public static string ApplyGlaze(string yaml, ThemeProfile profile)
	{
		int gaps = profile.Windows.Tiling.Gaps;
		string text = Rgba.Parse(profile.Windows.ActiveBorder.Color ?? profile.Palette.Accent).WithAlpha(byte.MaxValue).ToHex()
			.ToLowerInvariant();
		return Regex.Replace(Regex.Replace(Regex.Replace(yaml, "(inner_gap:\\s*)['\"]?[^'\"\\r\\n]*['\"]?", $"$1'{gaps}px'"), "(outer_gap:\\s*\\r?\\n(?:\\s+(?:top|right|bottom|left):.*\\r?\\n?)+)", (Match m) => Regex.Replace(m.Value, "((?:top|right|bottom|left):\\s*)['\"]?[^'\"\\r\\n]*['\"]?", $"$1'{gaps}px'")), "(focused_window:[\\s\\S]*?color:\\s*)['\"]?#?[0-9A-Fa-f]{3,8}['\"]?", "$1'" + text + "'");
	}

	private static bool IsRunning(string process)
	{
		Process[] processesByName = Process.GetProcessesByName(process);
		Process[] array = processesByName;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Dispose();
		}
		return processesByName.Length != 0;
	}

	private static void Run(string exe, string args)
	{
		try
		{
			using (Process.Start(new ProcessStartInfo(exe, args)
			{
				UseShellExecute = false,
				CreateNoWindow = true
			}))
			{
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"Не удалось запустить {exe} {args}: {ex.Message}");
		}
	}
}
