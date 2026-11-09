using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Services;

public sealed class UpdateService
{
	private static readonly HttpClient Http = CreateClient();

	private readonly AppPaths _paths;

	public UpdateService(AppPaths paths)
	{
		_paths = paths;
	}

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(60.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Metamorph-Updater/1.0");
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
		return httpClient;
	}

	public static bool IsValidRepository(string? repo)
	{
		if (!string.IsNullOrWhiteSpace(repo))
		{
			return Regex.IsMatch(repo.Trim(), "^[\\w.-]+/[\\w.-]+$");
		}
		return false;
	}

	public async Task<ReleaseInfo?> GetLatestAsync(string repository, CancellationToken ct = default(CancellationToken))
	{
		if (!IsValidRepository(repository))
		{
			return null;
		}
		return ParseRelease(await Http.GetStringAsync("https://api.github.com/repos/" + repository.Trim() + "/releases/latest", ct));
	}

	public static ReleaseInfo? ParseRelease(string json)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement rootElement = jsonDocument.RootElement;
		JsonElement value;
		string text = (rootElement.TryGetProperty("tag_name", out value) ? (value.GetString() ?? "") : "");
		if (!TryParseVersion(text, out Version version))
		{
			return null;
		}
		List<ReleaseAsset> list = new List<ReleaseAsset>();
		if (rootElement.TryGetProperty("assets", out var value2))
		{
			foreach (JsonElement item in value2.EnumerateArray())
			{
				list.Add(new ReleaseAsset(item.GetProperty("name").GetString() ?? "", item.GetProperty("browser_download_url").GetString() ?? "", item.TryGetProperty("size", out var value3) ? value3.GetInt64() : 0));
			}
		}
		JsonElement value4;
		JsonElement value5;
		return new ReleaseInfo(text, version, rootElement.TryGetProperty("body", out value4) ? (value4.GetString() ?? "") : "", rootElement.TryGetProperty("html_url", out value5) ? (value5.GetString() ?? "") : "", list);
	}

	public static bool TryParseVersion(string? text, out Version version)
	{
		version = new Version(0, 0);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = text.Trim().TrimStart('v', 'V');
		int num = text2.IndexOfAny(new char[3] { '-', '+', ' ' });
		if (num > 0)
		{
			text2 = text2.Substring(0, num);
		}
		if (!Version.TryParse(text2.Contains('.') ? text2 : (text2 + ".0"), out Version result))
		{
			return false;
		}
		version = new Version(result.Major, result.Minor, Math.Max(0, result.Build));
		return true;
	}

	public static bool IsNewer(Version candidate, Version current)
	{
		return candidate > new Version(current.Major, current.Minor, Math.Max(0, current.Build));
	}

	public static ReleaseAsset? PickAsset(ReleaseInfo release, bool standalone)
	{
		List<ReleaseAsset> source = release.Assets.Where((ReleaseAsset a) => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !a.Name.Contains("source", StringComparison.OrdinalIgnoreCase)).ToList();
		return source.FirstOrDefault((ReleaseAsset a) => a.Name.Contains("standalone", StringComparison.OrdinalIgnoreCase) == standalone) ?? source.FirstOrDefault();
	}

	public static string? FindHash(string sums, string fileName)
	{
		string[] array = sums.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Trim().Split(new char[3] { ' ', '\t', '*' }, StringSplitOptions.RemoveEmptyEntries);
			if (array2.Length >= 2 && string.Equals(array2[^1], fileName, StringComparison.OrdinalIgnoreCase))
			{
				return array2[0].ToLowerInvariant();
			}
		}
		return null;
	}

	public async Task<string> DownloadAsync(ReleaseInfo release, ReleaseAsset asset, IProgress<double>? progress = null, CancellationToken ct = default(CancellationToken))
	{
		Directory.CreateDirectory(_paths.Updates);
		string zipPath = Path.Combine(_paths.Updates, asset.Name);
		using (HttpResponseMessage response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct))
		{
			response.EnsureSuccessStatusCode();
			long total = response.Content.Headers.ContentLength ?? asset.Size;
			Stream input = await response.Content.ReadAsStreamAsync(ct);
			try
			{
				FileStream output = File.Create(zipPath);
				try
				{
					byte[] buffer = new byte[81920];
					long done = 0L;
					while (true)
					{
						int num;
						int read = (num = await input.ReadAsync(buffer, ct));
						if (num <= 0)
						{
							break;
						}
						await output.WriteAsync(buffer.AsMemory(0, read), ct);
						done += read;
						if (total > 0)
						{
							progress?.Report((double)done / (double)total);
						}
					}
				}
				finally
				{
					if (output != null)
					{
						await output.DisposeAsync();
					}
				}
			}
			finally
			{
				if (input != null)
				{
					await input.DisposeAsync();
				}
			}
		}
		ReleaseAsset releaseAsset = release.Assets.FirstOrDefault((ReleaseAsset a) => a.Name.StartsWith("SHA256SUMS", StringComparison.OrdinalIgnoreCase));
		if ((object)releaseAsset != null)
		{
			string? a2 = FindHash(await Http.GetStringAsync(releaseAsset.Url, ct), asset.Name) ?? throw new InvalidDataException("В SHA256SUMS нет хэша архива");
			string b = Sha256(zipPath);
			if (!string.Equals(a2, b, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Контрольная сумма архива не совпала — обновление отменено");
			}
		}
		string text = Path.Combine(_paths.Updates, release.Version.ToString());
		if (Directory.Exists(text))
		{
			Directory.Delete(text, recursive: true);
		}
		ZipFile.ExtractToDirectory(zipPath, text);
		string[] fileSystemEntries = Directory.GetFileSystemEntries(text);
		if (fileSystemEntries.Length == 1 && Directory.Exists(fileSystemEntries[0]))
		{
			text = fileSystemEntries[0];
		}
		if (!File.Exists(Path.Combine(text, "Metamorph.exe")))
		{
			throw new InvalidDataException("В архиве нет Metamorph.exe");
		}
		return text;
	}

	public static string Sha256(string file)
	{
		using FileStream source = File.OpenRead(file);
		return Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
	}

	public static void ScheduleReplace(string newFilesDir, string installDir, int processId)
	{
		string text = Path.Combine(installDir, "Metamorph.exe");
		string text2 = Path.Combine(Path.GetTempPath(), "metamorph-update.cmd");
		File.WriteAllText(text2, string.Join("\r\n", "@echo off", ":wait", $"tasklist /FI \"PID eq {processId}\" | find \"{processId}\" > nul && (ping 127.0.0.1 -n 2 > nul & goto wait)", $"robocopy \"{newFilesDir.TrimEnd('\\')}\" \"{installDir.TrimEnd('\\')}\" /E /NFL /NDL /NJH /NJS /R:3 /W:1 > nul", "start \"\" \"" + text + "\" --minimized", "del \"%~f0\""));
		Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + text2 + "\"")
		{
			CreateNoWindow = true,
			UseShellExecute = false
		})?.Dispose();
	}
}
