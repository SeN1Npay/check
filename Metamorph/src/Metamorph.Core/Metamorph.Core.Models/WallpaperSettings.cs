using System;
using System.Collections.Generic;
using System.IO;
using Metamorph.Core.Services;

namespace Metamorph.Core.Models;

public sealed class WallpaperSettings
{
	public WallpaperKind Kind { get; set; } = WallpaperKind.Gradient;


	public string? Path { get; set; }

	public string From { get; set; } = "#1E1E2E";


	public string To { get; set; } = "#313244";


	public GradientDirection Direction { get; set; } = GradientDirection.Diagonal;


	public WallpaperStyle Style { get; set; }

	public string? Folder { get; set; }

	public int IntervalMinutes { get; set; } = 30;


	public bool Shuffle { get; set; } = true;


	public List<WallpaperScheduleEntry> Schedule { get; set; } = new List<WallpaperScheduleEntry>();


	public bool Muted { get; set; } = true;


	public WallpaperKind EffectiveKind
	{
		get
		{
			if (Kind != WallpaperKind.Live)
			{
				return Kind;
			}
			string text = (string.IsNullOrWhiteSpace(Path) ? "" : System.IO.Path.GetExtension(Path).ToLowerInvariant());
			if (text == ".gif" || Array.IndexOf(WallpaperService.VideoExtensions, text) >= 0)
			{
				return WallpaperKind.Video;
			}
			if (text.Length > 0 && WallpaperService.IsStaticImage(Path))
			{
				return WallpaperKind.Image;
			}
			return WallpaperKind.Gradient;
		}
	}
}
