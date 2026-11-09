using System;

namespace Metamorph.Core.Games;

// Детерминированный ГСЧ (xorshift64*): одинаковое зерно — одинаковая партия на любой версии .NET.
public sealed class GameRandom
{
	private ulong _state;

	public GameRandom(ulong seed)
	{
		_state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
		for (int i = 0; i < 4; i++)
		{
			NextULong();
		}
	}

	public ulong NextULong()
	{
		_state ^= _state >> 12;
		_state ^= _state << 25;
		_state ^= _state >> 27;
		return _state * 0x2545F4914F6CDD1DUL;
	}

	public int Next(int maxExclusive)
	{
		if (maxExclusive <= 1)
		{
			return 0;
		}
		return (int)(NextULong() % (ulong)maxExclusive);
	}

	public int Next(int min, int maxExclusive)
	{
		return min + Next(maxExclusive - min);
	}

	public double NextDouble()
	{
		return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
	}

	public double Range(double min, double max)
	{
		return min + (max - min) * NextDouble();
	}

	public static ulong NewSeed()
	{
		return (ulong)Environment.TickCount64 * 6364136223846793005UL ^ (ulong)Guid.NewGuid().GetHashCode();
	}
}
