using System.Collections.Generic;

namespace Metamorph.Core.Dock;

public sealed class DockData
{
	public int Version { get; set; } = 1;


	public List<DockPin> Pins { get; set; } = new List<DockPin>();

}
