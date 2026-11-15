namespace Metamorph.Core.Services;

public sealed record CapabilityEntry(string Key, bool Packaged, long Start, long Stop)
{
	public bool InUse
	{
		get
		{
			if (Start > 0)
			{
				return Stop == 0;
			}
			return false;
		}
	}
}
