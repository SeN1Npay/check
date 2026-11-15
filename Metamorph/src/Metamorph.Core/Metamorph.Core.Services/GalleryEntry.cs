using System.Collections.Generic;

namespace Metamorph.Core.Services;

public sealed class GalleryEntry
{
	public string Id { get; set; } = "";


	public string Name { get; set; } = "";


	public string Author { get; set; } = "";


	public string Description { get; set; } = "";


	public string Url { get; set; } = "";


	public string? Sha256 { get; set; }

	public List<string> Preview { get; set; } = new List<string>();

}
