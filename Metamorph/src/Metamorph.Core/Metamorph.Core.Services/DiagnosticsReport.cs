using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Metamorph.Core.Services;

public static class DiagnosticsReport
{
	private static readonly string[] SecretWords = new string[7] { "token", "key", "secret", "password", "pass", "auth", "cookie" };

	public static string Redact(string json)
	{
		JsonNode jsonNode;
		try
		{
			JsonDocumentOptions documentOptions = new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			};
			jsonNode = JsonNode.Parse(json, null, documentOptions);
		}
		catch (JsonException)
		{
			return "(settings.json is corrupted and was not included)";
		}
		if (jsonNode == null)
		{
			return "null";
		}
		Walk(jsonNode);
		return jsonNode.ToJsonString(new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		});
	}

	private static bool IsSecret(string name)
	{
		string i = name.ToLowerInvariant();
		if (SecretWords.Any((string w) => i.Contains(w)) && !i.Contains("hotkey"))
		{
			return !i.Contains("keep");
		}
		return false;
	}

	private static void Walk(JsonNode node)
	{
		if (!(node is JsonObject jsonObject))
		{
			if (!(node is JsonArray jsonArray))
			{
				return;
			}
			{
				foreach (JsonNode item in jsonArray)
				{
					if (item != null)
					{
						Walk(item);
					}
				}
				return;
			}
		}
		foreach (var (text2, jsonNode2) in jsonObject.ToList())
		{
			if (jsonNode2 != null)
			{
				if (IsSecret(text2) && jsonNode2 is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && value.Length > 0)
				{
					jsonObject[text2] = "***";
				}
				else
				{
					Walk(jsonNode2);
				}
			}
		}
	}

	public static string Anonymize(string text)
	{
		string userName = Environment.UserName;
		if (string.IsNullOrEmpty(userName) || userName.Length < 2)
		{
			return text;
		}
		return text.Replace("\\" + userName + "\\", "\\<user>\\", StringComparison.OrdinalIgnoreCase).Replace("/" + userName + "/", "/<user>/", StringComparison.OrdinalIgnoreCase);
	}

	public static string SystemInfo(string version, IEnumerable<string>? extra = null)
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("Metamorph ");
		handler.AppendFormatted(version);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
		handler.AppendLiteral("Date: ");
		handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss zzz");
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(7, 2, stringBuilder2);
		handler.AppendLiteral("OS: ");
		handler.AppendFormatted(RuntimeInformation.OSDescription);
		handler.AppendLiteral(" (");
		handler.AppendFormatted(RuntimeInformation.OSArchitecture);
		handler.AppendLiteral(")");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder6 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
		handler.AppendLiteral("Windows version: ");
		handler.AppendFormatted(Environment.OSVersion.Version);
		stringBuilder6.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder7 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 2, stringBuilder2);
		handler.AppendLiteral(".NET: ");
		handler.AppendFormatted(RuntimeInformation.FrameworkDescription);
		handler.AppendLiteral(", process ");
		handler.AppendFormatted(RuntimeInformation.ProcessArchitecture);
		stringBuilder7.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder2);
		handler.AppendLiteral("CPUs: ");
		handler.AppendFormatted(Environment.ProcessorCount);
		handler.AppendLiteral(", 64-bit OS: ");
		handler.AppendFormatted(Environment.Is64BitOperatingSystem);
		stringBuilder8.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder9 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(29, 2, stringBuilder2);
		handler.AppendLiteral("UI culture: ");
		handler.AppendFormatted(CultureInfo.CurrentUICulture.Name);
		handler.AppendLiteral(", system uptime: ");
		handler.AppendFormatted(TimeSpan.FromMilliseconds(Environment.TickCount64), "d\\.hh\\:mm");
		stringBuilder9.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder10 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder2);
		handler.AppendLiteral("Process memory: ");
		handler.AppendFormatted(Environment.WorkingSet / 1048576);
		handler.AppendLiteral(" MB");
		stringBuilder10.AppendLine(ref handler);
		if (extra != null)
		{
			foreach (string item in extra)
			{
				stringBuilder.AppendLine(item);
			}
		}
		return stringBuilder.ToString();
	}

	public static void Create(string zipPath, string systemInfo, string? settingsJson, IEnumerable<(string Name, string Path)> files, IEnumerable<(string Name, string Content)>? texts = null)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
		if (File.Exists(zipPath))
		{
			File.Delete(zipPath);
		}
		using ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
		Add(zip, "system.txt", systemInfo);
		foreach (var (name, text) in texts ?? Array.Empty<(string, string)>())
		{
			Add(zip, name, Anonymize(text));
		}
		if (settingsJson != null)
		{
			Add(zip, "settings.json", Redact(settingsJson));
		}
		foreach (var (text2, path) in files)
		{
			try
			{
				if (!File.Exists(path))
				{
					continue;
				}
				using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				using StreamReader streamReader = new StreamReader(stream);
				string text3 = streamReader.ReadToEnd();
				if (text2.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
				{
					text3 = Redact(text3);
				}
				Add(zip, text2, Anonymize(text3));
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				Add(zip, text2 + ".error.txt", ex.Message);
			}
		}
	}

	private static void Add(ZipArchive zip, string name, string content)
	{
		using StreamWriter streamWriter = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		streamWriter.Write(content);
	}
}
