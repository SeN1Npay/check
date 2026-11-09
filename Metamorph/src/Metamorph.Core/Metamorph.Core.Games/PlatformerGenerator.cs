using System;

namespace Metamorph.Core.Games;

// Бесконечный режим: уровень из случайных кусков по зерну. Каждый вариант проверяется PlatformerReach;
// если не проходится — пробуем следующее зерно, в крайнем случае — простой ровный уровень.
public static class PlatformerGenerator
{
	public const int Rows = 15;

	private const int MinTop = 9;

	private const int MaxTop = 13;

	public static PlatformerLevel Generate(ulong seed, int stage)
	{
		stage = Math.Max(0, stage);
		for (int attempt = 0; attempt < 24; attempt++)
		{
			PlatformerLevel level = Build(seed + (ulong)attempt * 0x9E3779B97F4A7C15UL, stage, plain: false);
			if (PlatformerReach.CanFinish(level))
			{
				return level;
			}
		}
		return Build(seed, stage, plain: true);
	}

	private sealed class Grid
	{
		public readonly char[,] Cells;

		public readonly int Width;

		public readonly char Ground;

		public readonly bool Lava;

		public Grid(int width, PlatformerWorld world)
		{
			Width = width;
			Cells = new char[Rows, width];
			for (int r = 0; r < Rows; r++)
			{
				for (int c = 0; c < width; c++)
				{
					Cells[r, c] = '.';
				}
			}
			Ground = world == PlatformerWorld.Castle ? 'X' : '#';
			Lava = world == PlatformerWorld.Castle;
			if (world is PlatformerWorld.Cave or PlatformerWorld.Castle)
			{
				for (int c = 0; c < width; c++)
				{
					Cells[0, c] = Cells[1, c] = Ground;
				}
			}
		}

		public void Column(int c, int top)
		{
			for (int r = top; r < Rows; r++)
			{
				Cells[r, c] = Ground;
			}
		}

		public void Gap(int c)
		{
			if (Lava)
			{
				Cells[Rows - 2, c] = Cells[Rows - 1, c] = '~';
			}
		}

		public void Set(int c, int r, char ch)
		{
			if (c >= 0 && c < Width && r >= 2 && r < Rows)
			{
				Cells[r, c] = ch;
			}
		}

		public string[] ToRows()
		{
			string[] rows = new string[Rows];
			char[] line = new char[Width];
			for (int r = 0; r < Rows; r++)
			{
				for (int c = 0; c < Width; c++)
				{
					line[c] = Cells[r, c];
				}
				rows[r] = new string(line);
			}
			return rows;
		}
	}

	private static PlatformerLevel Build(ulong seed, int stage, bool plain)
	{
		GameRandom rnd = new GameRandom(seed ^ ((ulong)stage * 0xD1B54A32D192ED03UL));
		PlatformerWorld world = (PlatformerWorld)(stage % 4);
		double diff = Math.Min(1.0, stage * 0.12);
		int width = 150 + Math.Min(90, stage * 10);
		Grid g = new Grid(width, world);
		int top = 13;
		int x = 0;
		for (; x < 12; x++)
		{
			g.Column(x, top);
		}
		g.Set(2, top - 1, 'S');
		bool checkpoint = false;
		int end = width - 22;
		while (x < end)
		{
			if (!checkpoint && x >= width / 2)
			{
				checkpoint = true;
				g.Column(x, top);
				g.Set(x, top - 1, 'K');
				x++;
				continue;
			}
			int pick = plain ? 0 : rnd.Next(9);
			switch (pick)
			{
			case 0:
				x = Flat(g, rnd, x, top, rnd.Next(4, 9), diff);
				break;
			case 1:
			case 2:
			{
				int next = Math.Clamp(top + rnd.Next(-2, 2), MinTop, MaxTop);
				int dy = top - next;
				int len = rnd.Next(2, 3 + (diff > 0.5 ? 2 : 1));
				len = Math.Min(len, PlatformerReach.Reach(dy) - 2);
				len = Math.Max(1, len);
				for (int i = 0; i < len && x < end; i++, x++)
				{
					g.Gap(x);
				}
				top = next;
				x = Flat(g, rnd, x, top, rnd.Next(3, 6), diff * 0.5);
				break;
			}
			case 3:
				top = Math.Clamp(top + (rnd.Next(2) == 0 ? -1 : 1) * rnd.Next(1, 3), MinTop, MaxTop);
				x = Flat(g, rnd, x, top, rnd.Next(3, 6), diff);
				break;
			case 4:
			{
				int start = x;
				x = Flat(g, rnd, x, top, 8, 0.0);
				int row = top - 4;
				for (int c = start + 1; c < start + 6; c++)
				{
					double r = rnd.NextDouble();
					g.Set(c, row, r < 0.12 ? 'P' : r < 0.45 ? '?' : r < 0.52 ? 'C' : 'B');
					if (rnd.NextDouble() < 0.4)
					{
						g.Set(c, row - 3, 'o');
					}
				}
				if (rnd.NextDouble() < 0.4 + diff * 0.4)
				{
					PutEnemy(g, start + 7, top, Enemy(rnd, diff));
				}
				break;
			}
			case 5:
			{
				int start = x;
				x = Flat(g, rnd, x, top, 7, 0.0);
				int h = rnd.Next(2, 4);
				for (int r = top - h; r < top; r++)
				{
					g.Set(start + 3, r, 'X');
					g.Set(start + 4, r, 'X');
				}
				g.Set(start + 3, top - h - 2, 'o');
				g.Set(start + 4, top - h - 2, 'o');
				break;
			}
			case 6:
			{
				int start = x;
				x = Flat(g, rnd, x, top, 7, 0.0);
				int len = 1 + (rnd.NextDouble() < diff ? 1 : 0);
				for (int i = 0; i < len; i++)
				{
					g.Set(start + 3 + i, top - 1, '^');
				}
				break;
			}
			case 7:
			{
				int start = x;
				x = Flat(g, rnd, x, top, 8, 0.0);
				int row = top - 3;
				for (int c = start + 2; c < start + 6; c++)
				{
					g.Set(c, row, '=');
					g.Set(c, row - 2, 'o');
				}
				if (rnd.NextDouble() < 0.5)
				{
					PutEnemy(g, start + 6, top, Enemy(rnd, diff));
				}
				break;
			}
			default:
			{
				// Лесенка вверх-вниз.
				int start = x;
				x = Flat(g, rnd, x, top, 9, 0.0);
				for (int i = 0; i < 3; i++)
				{
					for (int r = top - 1 - i; r < top; r++)
					{
						g.Set(start + 1 + i, r, 'X');
						g.Set(start + 7 - i, r, 'X');
					}
				}
				g.Set(start + 4, top - 1, rnd.NextDouble() < 0.5 ? 's' : 'o');
				break;
			}
			}
		}
		for (; x < width; x++)
		{
			g.Column(x, top);
		}
		g.Set(width - 10, top - 1, 'E');
		string id = "∞-" + (stage + 1);
		return PlatformerLevel.Parse(id, world, "Бесконечный путь", "Endless Road", g.ToRows());
	}

	private static int Flat(Grid g, GameRandom rnd, int x, int top, int len, double enemyChance)
	{
		int start = x;
		for (int i = 0; i < len && x < g.Width; i++, x++)
		{
			g.Column(x, top);
		}
		if (len >= 4 && rnd.NextDouble() < enemyChance + 0.25)
		{
			PutEnemy(g, start + len / 2, top, Enemy(rnd, enemyChance));
		}
		if (rnd.NextDouble() < 0.35)
		{
			for (int c = start + 1; c < start + len - 1; c++)
			{
				g.Set(c, top - 3, 'o');
			}
		}
		return x;
	}

	private static void PutEnemy(Grid g, int c, int top, char kind)
	{
		g.Set(c, kind == 'b' ? top - 4 : top - 1, kind);
	}

	private static char Enemy(GameRandom rnd, double diff)
	{
		double r = rnd.NextDouble();
		if (diff > 0.2 && r < 0.18)
		{
			return 'k';
		}
		if (diff > 0.3 && r < 0.32)
		{
			return 'b';
		}
		return r < 0.7 ? 's' : 'n';
	}
}
