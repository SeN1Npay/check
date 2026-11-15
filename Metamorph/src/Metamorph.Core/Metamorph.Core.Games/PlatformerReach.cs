using System;
using System.Collections.Generic;

namespace Metamorph.Core.Games;

// Грубая проверка проходимости: BFS по клеткам, где можно стоять, с консервативной дальностью прыжка.
// Кирпичи считаются твёрдыми (маленькая Мета их не ломает), ворота босса — проходимыми.
public static class PlatformerReach
{
	public const int MaxUp = 4;

	public const int MaxDx = 9;

	// Сколько клеток по горизонтали покрывает прыжок с подъёмом dy (dy < 0 — спуск).
	public static int Reach(int dy)
	{
		if (dy >= 4)
		{
			return 3;
		}
		if (dy == 3)
		{
			return 4;
		}
		if (dy == 2)
		{
			return 5;
		}
		if (dy >= 0)
		{
			return 6;
		}
		return Math.Min(MaxDx, 6 + -dy / 2);
	}

	public static bool CanFinish(PlatformerLevel level)
	{
		return Explore(level, out _);
	}

	// reached — отметки достижимых клеток (индекс r * Width + c).
	public static bool Explore(PlatformerLevel level, out bool[] reached)
	{
		int w = level.Width;
		int h = level.Height;
		bool[] support = new bool[w * h];
		foreach (PlatformerSpawn s in level.Spawns)
		{
			if (s.Kind == PlatformerSpawnKind.PlatformH)
			{
				for (int c = s.Col; c < s.Col + s.Length + PlatformerLevel.PlatformTravel && c < w; c++)
				{
					support[s.Row * w + c] = true;
				}
			}
			else if (s.Kind == PlatformerSpawnKind.PlatformV)
			{
				for (int r = Math.Max(0, s.Row - PlatformerLevel.PlatformTravel); r <= s.Row; r++)
				{
					for (int c = s.Col; c < s.Col + s.Length && c < w; c++)
					{
						support[r * w + c] = true;
					}
				}
			}
		}
		bool Passable(int c, int r)
		{
			if (c < 0 || c >= w || r >= h)
			{
				return false;
			}
			if (r < 0)
			{
				return true;
			}
			PlatformerTile t = level[c, r];
			if (t == PlatformerTile.Door)
			{
				return true;
			}
			return !PlatformerLevel.IsSolid(t) && !PlatformerLevel.IsHazard(t);
		}
		bool Standable(int c, int r)
		{
			if (c < 0 || c >= w || r < 0 || r >= h - 1 || !Passable(c, r))
			{
				return false;
			}
			PlatformerTile below = level[c, r + 1];
			return support[(r + 1) * w + c] || below == PlatformerTile.OneWay || (PlatformerLevel.IsSolid(below) && below != PlatformerTile.Door);
		}
		bool Segment(int c0, int c1, int r0, int r1)
		{
			for (int c = Math.Min(c0, c1); c <= Math.Max(c0, c1); c++)
			{
				for (int r = Math.Min(r0, r1); r <= Math.Max(r0, r1); r++)
				{
					if (!Passable(c, r))
					{
						return false;
					}
				}
			}
			return true;
		}
		bool Path(int c, int r, int c2, int r2)
		{
			int top = Math.Min(r, r2);
			for (int apex = top - 1; apex <= top; apex++)
			{
				if (Segment(c, c, apex, r) && Segment(c, c2, apex, apex) && Segment(c2, c2, apex, r2))
				{
					return true;
				}
			}
			return false;
		}
		reached = new bool[w * h];
		if (!Standable(level.StartCol, level.StartRow))
		{
			return false;
		}
		Queue<int> queue = new Queue<int>();
		reached[level.StartRow * w + level.StartCol] = true;
		queue.Enqueue(level.StartRow * w + level.StartCol);
		bool finish = false;
		while (queue.Count > 0)
		{
			int i = queue.Dequeue();
			int c = i % w;
			int r = i / w;
			if (Math.Abs(c - level.GoalCol) <= 1)
			{
				finish = true;
			}
			for (int r2 = Math.Max(0, r - MaxUp); r2 < h - 1; r2++)
			{
				int reach = Reach(r - r2);
				for (int c2 = Math.Max(0, c - reach); c2 <= Math.Min(w - 1, c + reach); c2++)
				{
					int j = r2 * w + c2;
					if (!reached[j] && Standable(c2, r2) && Path(c, r, c2, r2))
					{
						reached[j] = true;
						queue.Enqueue(j);
					}
				}
			}
		}
		return finish;
	}
}
