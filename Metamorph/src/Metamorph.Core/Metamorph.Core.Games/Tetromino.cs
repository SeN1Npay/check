using System;

namespace Metamorph.Core.Games;

public enum TetrominoKind : byte
{
	I,
	O,
	T,
	S,
	Z,
	J,
	L
}

// Формы и таблицы SRS. Координаты: x вправо, y вниз, клетки внутри рамки N×N (I — 4, O — 2, остальные — 3).
public static class Tetromino
{
	public const int Count = 7;

	private static readonly (int X, int Y)[][] Spawn =
	{
		new[] { (0, 1), (1, 1), (2, 1), (3, 1) },
		new[] { (0, 0), (1, 0), (0, 1), (1, 1) },
		new[] { (1, 0), (0, 1), (1, 1), (2, 1) },
		new[] { (1, 0), (2, 0), (0, 1), (1, 1) },
		new[] { (0, 0), (1, 0), (1, 1), (2, 1) },
		new[] { (0, 0), (0, 1), (1, 1), (2, 1) },
		new[] { (2, 0), (0, 1), (1, 1), (2, 1) }
	};

	// [kind][rotation][cell]
	private static readonly (int X, int Y)[][][] Cells = BuildCells();

	// Таблицы смещений SRS из вики (y вверх), [from][0 = по часовой, 1 = против][тест].
	private static readonly (int X, int Y)[][][] KicksJlstz =
	{
		new[]
		{
			new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) },
			new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) }
		},
		new[]
		{
			new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) },
			new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) }
		},
		new[]
		{
			new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) },
			new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) }
		},
		new[]
		{
			new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) },
			new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) }
		}
	};

	private static readonly (int X, int Y)[][][] KicksI =
	{
		new[]
		{
			new[] { (0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2) },
			new[] { (0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1) }
		},
		new[]
		{
			new[] { (0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1) },
			new[] { (0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2) }
		},
		new[]
		{
			new[] { (0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2) },
			new[] { (0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1) }
		},
		new[]
		{
			new[] { (0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1) },
			new[] { (0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2) }
		}
	};

	private static readonly (int X, int Y)[] NoKick = { (0, 0) };

	public static int BoxSize(TetrominoKind kind)
	{
		return kind switch
		{
			TetrominoKind.I => 4,
			TetrominoKind.O => 2,
			_ => 3
		};
	}

	public static ReadOnlySpan<(int X, int Y)> CellsOf(TetrominoKind kind, int rotation)
	{
		return Cells[(int)kind][rotation & 3];
	}

	// Смещения уже в системе «y вниз».
	public static (int X, int Y) Kick(TetrominoKind kind, int from, bool clockwise, int test)
	{
		(int X, int Y) k = KickTable(kind, from, clockwise)[test];
		return (k.X, -k.Y);
	}

	public static int KickCount(TetrominoKind kind)
	{
		return kind == TetrominoKind.O ? 1 : 5;
	}

	private static (int X, int Y)[] KickTable(TetrominoKind kind, int from, bool clockwise)
	{
		return kind switch
		{
			TetrominoKind.O => NoKick,
			TetrominoKind.I => KicksI[from & 3][clockwise ? 0 : 1],
			_ => KicksJlstz[from & 3][clockwise ? 0 : 1]
		};
	}

	private static (int X, int Y)[][][] BuildCells()
	{
		var all = new (int X, int Y)[Count][][];
		for (int k = 0; k < Count; k++)
		{
			int n = BoxSize((TetrominoKind)k);
			all[k] = new (int X, int Y)[4][];
			all[k][0] = Spawn[k];
			for (int r = 1; r < 4; r++)
			{
				var prev = all[k][r - 1];
				var next = new (int X, int Y)[4];
				for (int i = 0; i < 4; i++)
				{
					// Поворот по часовой внутри рамки: (x, y) → (n−1−y, x).
					next[i] = (n - 1 - prev[i].Y, prev[i].X);
				}
				all[k][r] = next;
			}
		}
		return all;
	}
}
