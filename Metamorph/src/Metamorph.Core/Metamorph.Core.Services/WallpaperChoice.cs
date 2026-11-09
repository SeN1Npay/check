using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class WallpaperChoice
{
	public string? Path { get; set; }

	public bool Muted { get; set; } = true;


	public WallpaperStyle Style { get; set; }

	public WallpaperChoice Copy()
	{
		return (WallpaperChoice)MemberwiseClone();
	}
}
