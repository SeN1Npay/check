using System;

namespace Metamorph.Core.Wallpaper;

public sealed record WallpaperEngineItem(string Id, string Title, string Type, string Folder, string? File, string? Preview)
{
	public bool IsVideo
	{
		get
		{
			if (Type.Equals("video", StringComparison.OrdinalIgnoreCase))
			{
				return File != null;
			}
			return false;
		}
	}

	public string? UsablePath
	{
		get
		{
			if (!IsVideo)
			{
				return Preview;
			}
			return File;
		}
	}

	public bool PreviewOnly => !IsVideo;
}
