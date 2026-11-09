using System.Collections.Generic;

namespace Metamorph.Core.Services;

public sealed class GalleryIndex
{
	public int Version { get; set; } = 1;


	public List<GalleryEntry> Themes { get; set; } = new List<GalleryEntry>();

}
