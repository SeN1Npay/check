using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class AppThemers
{
	public const string SchemeName = "Metamorph";

	private const string BlockStart = "# >>> Metamorph oh-my-posh >>>";

	private const string BlockEnd = "# <<< Metamorph oh-my-posh <<<";

	private readonly AppPaths _paths;

	private readonly SettingsBackup _backup;

	public string OhMyPoshThemeFile => Path.Combine(_paths.Generated, "oh-my-posh", "metamorph.omp.json");

	public AppThemers(AppPaths paths, SettingsBackup backup)
	{
		_paths = paths;
		_backup = backup;
	}

	public List<string> Apply(ThemeProfile profile)
	{
		List<string> list = new List<string>();
		if (profile.Apps.WindowsTerminal)
		{
			list.Add(ApplyTerminal(profile));
		}
		else
		{
			RestoreTerminal();
		}
		if (profile.Apps.VsCode)
		{
			list.Add(ApplyVsCode(profile));
		}
		else
		{
			RestoreVsCode();
		}
		if (profile.Apps.OhMyPosh)
		{
			list.Add(ApplyOhMyPosh(profile));
		}
		else
		{
			RestoreOhMyPosh();
		}
		return list;
	}

	public void RestoreAll()
	{
		RestoreTerminal();
		RestoreVsCode();
		RestoreOhMyPosh();
	}

	public static IEnumerable<string> TerminalSettingsFiles()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return new string[3]
		{
			Path.Combine(folderPath, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
			Path.Combine(folderPath, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
			Path.Combine(folderPath, "Microsoft", "Windows Terminal", "settings.json")
		}.Where(File.Exists);
	}

	private string ApplyTerminal(ThemeProfile profile)
	{
		List<string> list = TerminalSettingsFiles().ToList();
		if (list.Count == 0)
		{
			return Loc.T("Windows Terminal не найден");
		}
		foreach (string item in list)
		{
			JsonObject root = JsoncFile.Load(item);
			JsonObject defaults = TerminalDefaults(root);
			_backup.EnsureExtra("wt|" + item, () => defaults["colorScheme"]?.ToJsonString());
			ApplyTerminalScheme(root, profile);
			JsoncFile.Save(item, root);
		}
		return Loc.F("Windows Terminal: схема «{0}»", "Metamorph");
	}

	public static void ApplyTerminalScheme(JsonObject root, ThemeProfile profile)
	{
		TerminalPalette terminalPalette = TerminalPalette.From(profile.Palette, profile.Mode);
		JsonArray jsonArray = (JsonArray)(root["schemes"] = (root["schemes"] as JsonArray) ?? new JsonArray());
		foreach (JsonObject item in (from s in jsonArray.OfType<JsonObject>()
			where (string?)s["name"] == "Metamorph"
			select s).ToList())
		{
			jsonArray.Remove(item);
		}
		jsonArray.Add(new JsonObject
		{
			["name"] = "Metamorph",
			["background"] = H(terminalPalette.Background),
			["foreground"] = H(terminalPalette.Foreground),
			["cursorColor"] = H(terminalPalette.Cursor),
			["selectionBackground"] = H(terminalPalette.Selection),
			["black"] = H(terminalPalette.Black),
			["red"] = H(terminalPalette.Red),
			["green"] = H(terminalPalette.Green),
			["yellow"] = H(terminalPalette.Yellow),
			["blue"] = H(terminalPalette.Blue),
			["purple"] = H(terminalPalette.Purple),
			["cyan"] = H(terminalPalette.Cyan),
			["white"] = H(terminalPalette.White),
			["brightBlack"] = H(terminalPalette.BrightBlack),
			["brightRed"] = H(terminalPalette.BrightRed),
			["brightGreen"] = H(terminalPalette.BrightGreen),
			["brightYellow"] = H(terminalPalette.BrightYellow),
			["brightBlue"] = H(terminalPalette.BrightBlue),
			["brightPurple"] = H(terminalPalette.BrightPurple),
			["brightCyan"] = H(terminalPalette.BrightCyan),
			["brightWhite"] = H(terminalPalette.BrightWhite)
		});
		TerminalDefaults(root)["colorScheme"] = "Metamorph";
		static string H(Rgba c)
		{
			return c.WithAlpha(byte.MaxValue).ToHex();
		}
	}

	private static JsonObject TerminalDefaults(JsonObject root)
	{
		if (root["profiles"] is JsonArray value)
		{
			root.Remove("profiles");
			root["profiles"] = new JsonObject
			{
				["defaults"] = new JsonObject(),
				["list"] = value
			};
		}
		return JsoncFile.Obj(JsoncFile.Obj(root, "profiles"), "defaults");
	}

	private void RestoreTerminal()
	{
		foreach (string item in TerminalSettingsFiles())
		{
			if (_backup.TryGetExtra("wt|" + item, out string value))
			{
				JsonObject root = JsoncFile.Load(item);
				RestoreTerminalScheme(root, value);
				JsoncFile.Save(item, root);
			}
		}
	}

	public static void RestoreTerminalScheme(JsonObject root, string? previousJson)
	{
		if (root["schemes"] is JsonArray jsonArray)
		{
			foreach (JsonObject item in (from s in jsonArray.OfType<JsonObject>()
				where (string?)s["name"] == "Metamorph"
				select s).ToList())
			{
				jsonArray.Remove(item);
			}
		}
		JsonObject jsonObject = TerminalDefaults(root);
		if (previousJson == null)
		{
			jsonObject.Remove("colorScheme");
		}
		else
		{
			jsonObject["colorScheme"] = JsonNode.Parse(previousJson);
		}
	}

	public static IEnumerable<string> VsCodeSettingsFiles()
	{
		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		return from n in new string[3] { "Code", "Code - Insiders", "VSCodium" }
			select Path.Combine(appData, n, "User", "settings.json") into f
			where Directory.Exists(Path.GetDirectoryName(f))
			select f;
	}

	private string ApplyVsCode(ThemeProfile profile)
	{
		List<string> list = VsCodeSettingsFiles().ToList();
		if (list.Count == 0)
		{
			return Loc.T("VS Code не найден");
		}
		foreach (string item in list)
		{
			JsonObject root = JsoncFile.Load(item);
			_backup.EnsureExtra("vscode-theme|" + item, () => root["workbench.colorTheme"]?.ToJsonString());
			_backup.EnsureExtra("vscode-colors|" + item, () => root["workbench.colorCustomizations"]?.ToJsonString());
			ApplyVsCodeColors(root, profile);
			JsoncFile.Save(item, root);
		}
		return Loc.T("VS Code: цвета темы");
	}

	public static void ApplyVsCodeColors(JsonObject root, ThemeProfile profile)
	{
		Palette palette = profile.Palette;
		TerminalPalette terminalPalette = TerminalPalette.From(palette, profile.Mode);
		JsonObject jsonObject = new JsonObject
		{
			["editor.background"] = H(palette.Background),
			["editor.foreground"] = H(palette.Foreground),
			["editorLineNumber.foreground"] = H(palette.Muted),
			["editorCursor.foreground"] = H(palette.Accent),
			["editor.selectionBackground"] = A(palette.Accent, 85),
			["sideBar.background"] = H(palette.Background),
			["sideBar.foreground"] = H(palette.Foreground),
			["activityBar.background"] = H(palette.Background),
			["activityBar.foreground"] = H(palette.Accent),
			["activityBarBadge.background"] = H(palette.Accent),
			["activityBarBadge.foreground"] = H(palette.Background),
			["titleBar.activeBackground"] = H(palette.Background),
			["titleBar.activeForeground"] = H(palette.Foreground),
			["statusBar.background"] = H(palette.Surface),
			["statusBar.foreground"] = H(palette.Foreground),
			["tab.activeBackground"] = H(palette.Surface),
			["tab.inactiveBackground"] = H(palette.Background),
			["tab.activeBorderTop"] = H(palette.Accent),
			["editorGroupHeader.tabsBackground"] = H(palette.Background),
			["panel.background"] = H(palette.Background),
			["terminal.background"] = H(palette.Background),
			["terminal.foreground"] = H(palette.Foreground),
			["focusBorder"] = H(palette.Accent),
			["button.background"] = H(palette.Accent),
			["button.foreground"] = H(palette.Background),
			["badge.background"] = H(palette.Accent),
			["list.activeSelectionBackground"] = A(palette.Accent, 64),
			["list.hoverBackground"] = A(palette.Surface, 170),
			["input.background"] = H(palette.Surface),
			["dropdown.background"] = H(palette.Surface),
			["errorForeground"] = H(palette.Red),
			["gitDecoration.modifiedResourceForeground"] = H(palette.Yellow),
			["gitDecoration.untrackedResourceForeground"] = H(palette.Green)
		};
		string[] array = new string[8] { "Black", "Red", "Green", "Yellow", "Blue", "Magenta", "Cyan", "White" };
		for (int i = 0; i < 16; i++)
		{
			jsonObject["terminal.ansi" + ((i >= 8) ? "Bright" : "") + array[i % 8]] = terminalPalette.Ansi[i].WithAlpha(byte.MaxValue).ToHex();
		}
		root["workbench.colorCustomizations"] = jsonObject;
		root["workbench.colorTheme"] = ((profile.Mode == ColorMode.Dark) ? "Default Dark Modern" : "Default Light Modern");
		static string A(string hex, byte alpha)
		{
			string text = Rgba.Parse(hex).WithAlpha(alpha).ToHex();
			if (text.Length != 9)
			{
				return text;
			}
			string text2 = text;
			return "#" + text2.Substring(3, text2.Length - 3) + text.Substring(1, 2);
		}
		static string H(string hex)
		{
			return Rgba.Parse(hex).WithAlpha(byte.MaxValue).ToHex();
		}
	}

	private void RestoreVsCode()
	{
		foreach (string item in VsCodeSettingsFiles().Where(File.Exists))
		{
			if (_backup.TryGetExtra("vscode-colors|" + item, out string value))
			{
				_backup.TryGetExtra("vscode-theme|" + item, out string value2);
				JsonObject root = JsoncFile.Load(item);
				SetOrRemove(root, "workbench.colorCustomizations", value);
				SetOrRemove(root, "workbench.colorTheme", value2);
				JsoncFile.Save(item, root);
			}
		}
	}

	private static void SetOrRemove(JsonObject root, string name, string? json)
	{
		if (json == null)
		{
			root.Remove(name);
		}
		else
		{
			root[name] = JsonNode.Parse(json);
		}
	}

	public static IEnumerable<string> PowerShellProfiles()
	{
		string docs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		yield return Path.Combine(docs, "PowerShell", "Microsoft.PowerShell_profile.ps1");
		yield return Path.Combine(docs, "WindowsPowerShell", "Microsoft.PowerShell_profile.ps1");
	}

	private string ApplyOhMyPosh(ThemeProfile profile)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(OhMyPoshThemeFile));
		File.WriteAllText(OhMyPoshThemeFile, BuildOhMyPoshTheme(profile).ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true
		}));
		foreach (string item in PowerShellProfiles())
		{
			string text = (File.Exists(item) ? File.ReadAllText(item) : "");
			string text2 = WithProfileBlock(text, OhMyPoshThemeFile);
			if (!(text2 == text))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(item));
				JsoncFile.BackupOnce(item);
				File.WriteAllText(item, text2);
			}
		}
		return Loc.T("oh-my-posh: тема и профиль PowerShell");
	}

	private void RestoreOhMyPosh()
	{
		foreach (string item in PowerShellProfiles().Where(File.Exists))
		{
			string text = File.ReadAllText(item);
			string text2 = WithoutProfileBlock(text);
			if (text2 != text)
			{
				File.WriteAllText(item, text2);
			}
		}
	}

	public static string WithProfileBlock(string profileText, string themeFile)
	{
		string text = string.Join(Environment.NewLine, "# >>> Metamorph oh-my-posh >>>", "if (Get-Command oh-my-posh -ErrorAction SilentlyContinue) {", "    oh-my-posh init pwsh --config '" + themeFile.Replace("'", "''") + "' | Invoke-Expression", "}", "# <<< Metamorph oh-my-posh <<<");
		string text2 = WithoutProfileBlock(profileText).TrimEnd();
		return ((text2.Length > 0) ? (text2 + Environment.NewLine + Environment.NewLine) : "") + text + Environment.NewLine;
	}

	public static string WithoutProfileBlock(string profileText)
	{
		string pattern = Regex.Escape("# >>> Metamorph oh-my-posh >>>") + "[\\s\\S]*?" + Regex.Escape("# <<< Metamorph oh-my-posh <<<") + "\\r?\\n?";
		string text = Regex.Replace(profileText, pattern, "");
		if (!(text == profileText))
		{
			return text.TrimEnd() + ((text.Trim().Length > 0) ? Environment.NewLine : "");
		}
		return profileText;
	}

	public static JsonObject BuildOhMyPoshTheme(ThemeProfile profile)
	{
		Palette p = profile.Palette;
		JsonObject jsonObject = Segment("os", H(p.Accent), " {{ .Icon }} ", null, "diamond");
		jsonObject.Remove("powerline_symbol");
		jsonObject["leading_diamond"] = "\ue0b6";
		JsonObject jsonObject2 = Segment("git", H(p.Green), " {{ .HEAD }}{{ if .Working.Changed }} *{{ end }} ", new JsonObject { ["fetch_status"] = true });
		jsonObject2["background_templates"] = new JsonArray("{{ if or (.Working.Changed) (.Staging.Changed) }}" + H(p.Yellow) + "{{ end }}");
		JsonObject jsonObject3 = new JsonObject();
		jsonObject3["$schema"] = "https://raw.githubusercontent.com/JanDeDobbeleer/oh-my-posh/main/themes/schema.json";
		jsonObject3["version"] = 2;
		jsonObject3["final_space"] = true;
		jsonObject3["blocks"] = new JsonArray(new JsonObject
		{
			["type"] = "prompt",
			["alignment"] = "left",
			["segments"] = new JsonArray(jsonObject, Segment("path", H(p.Accent2), " {{ .Path }} ", new JsonObject { ["style"] = "folder" }), jsonObject2, Segment("executiontime", H(p.Yellow), " {{ .FormattedMs }} ", new JsonObject { ["threshold"] = 3000 }), Segment("status", H(p.Red), " {{ if gt .Code 0 }}✗ {{ .Code }}{{ end }} "))
		}, new JsonObject
		{
			["type"] = "prompt",
			["alignment"] = "left",
			["newline"] = true,
			["segments"] = new JsonArray(new JsonObject
			{
				["type"] = "text",
				["style"] = "plain",
				["foreground"] = H(p.Accent),
				["template"] = "❯ "
			})
		});
		return jsonObject3;
		static string H(string hex)
		{
			return Rgba.Parse(hex).WithAlpha(byte.MaxValue).ToHex();
		}
		JsonObject Segment(string type, string bg, string template, JsonObject? props = null, string style = "powerline")
		{
			JsonObject jsonObject4 = new JsonObject
			{
				["type"] = type,
				["style"] = style,
				["powerline_symbol"] = "\ue0b0",
				["foreground"] = H(p.Background),
				["background"] = bg,
				["template"] = template
			};
			if (props != null)
			{
				jsonObject4["properties"] = props;
			}
			return jsonObject4;
		}
	}
}
