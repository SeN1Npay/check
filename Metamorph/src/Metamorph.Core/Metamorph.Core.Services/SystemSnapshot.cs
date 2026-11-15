using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Services;

public sealed class SystemSnapshot
{
	public int Version { get; set; } = 2;


	public DateTime CreatedUtc { get; set; }

	public string MachineName { get; set; } = "";


	public string WallpaperPath { get; set; } = "";


	public string? WallpaperCopy { get; set; }

	public bool TaskbarAutoHide { get; set; }

	public List<RegistryValueSnapshot> Values { get; set; } = new List<RegistryValueSnapshot>();


	public Dictionary<string, string?> Extras { get; set; } = new Dictionary<string, string>();


	public RegistryValueSnapshot? Find(string key, string name)
	{
		string key2 = key;
		string name2 = name;
		return Values.FirstOrDefault((RegistryValueSnapshot v) => string.Equals(v.Key, key2, StringComparison.OrdinalIgnoreCase) && string.Equals(v.Name, name2, StringComparison.OrdinalIgnoreCase));
	}
}
