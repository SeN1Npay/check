using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class GalleryService
{
	private static readonly HttpClient Http;

	private static readonly JsonSerializerOptions Options;

	private readonly AppPaths _paths;

	private readonly ProfileStore _store;

	static GalleryService()
	{
		Http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30.0)
		};
		Options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};
		Http.DefaultRequestHeaders.UserAgent.ParseAdd("Metamorph/1.0");
	}

	public GalleryService(AppPaths paths, ProfileStore store)
	{
		_paths = paths;
		_store = store;
	}

	public static string? ResolveIndexUrl(AppSettings settings)
	{
		if (!string.IsNullOrWhiteSpace(settings.Gallery.IndexUrl))
		{
			return settings.Gallery.IndexUrl.Trim();
		}
		if (!UpdateService.IsValidRepository(settings.Updates.Repository))
		{
			return null;
		}
		return "https://raw.githubusercontent.com/" + settings.Updates.Repository.Trim() + "/main/gallery/index.json";
	}

	public static GalleryIndex Parse(string json)
	{
		return JsonSerializer.Deserialize<GalleryIndex>(json, Options) ?? new GalleryIndex();
	}

	public static string ResolveUrl(string indexUrl, string entryUrl)
	{
		if (!Uri.TryCreate(entryUrl, UriKind.Absolute, out Uri result))
		{
			return new Uri(new Uri(indexUrl), entryUrl).ToString();
		}
		return result.ToString();
	}

	public async Task<GalleryIndex> LoadAsync(string indexUrl, CancellationToken ct = default(CancellationToken))
	{
		if (File.Exists(indexUrl))
		{
			return Parse(await File.ReadAllTextAsync(indexUrl, ct));
		}
		return Parse(await Http.GetStringAsync(indexUrl, ct));
	}

	public async Task<ThemeProfile> InstallAsync(string indexUrl, GalleryEntry entry, CancellationToken ct = default(CancellationToken))
	{
		string text = Path.Combine(_paths.Root, "downloads");
		Directory.CreateDirectory(text);
		string file = Path.Combine(text, Base16Safe(entry.Id) + ".mmtheme");
		if (File.Exists(indexUrl))
		{
			File.Copy(Path.Combine(Path.GetDirectoryName(indexUrl), entry.Url), file, overwrite: true);
		}
		else
		{
			string path = file;
			await File.WriteAllBytesAsync(path, await Http.GetByteArrayAsync(ResolveUrl(indexUrl, entry.Url), ct), ct);
		}
		if (!string.IsNullOrWhiteSpace(entry.Sha256) && !string.Equals(UpdateService.Sha256(file), entry.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Контрольная сумма пакета темы не совпала");
		}
		return ThemePackage.Import(file, _paths, _store);
	}

	private static string Base16Safe(string id)
	{
		return Base16.Slug(string.IsNullOrWhiteSpace(id) ? "theme" : id);
	}
}
