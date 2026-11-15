using System;
using System.Collections.Generic;

namespace Metamorph.Core.Games;

public enum PlatformerTile : byte
{
	Empty,
	Ground,
	Hard,
	Brick,
	BrickCoins,
	Question,
	QuestionPower,
	Used,
	OneWay,
	Spikes,
	Lava,
	Coin,
	Door,
	FirePivot
}

public enum PlatformerWorld
{
	Meadow,
	Cave,
	Sky,
	Castle
}

public enum PlatformerSpawnKind
{
	Slime,
	Spiky,
	Bat,
	Snail,
	Boss,
	PlatformH,
	PlatformV,
	FireBar
}

public readonly record struct PlatformerSpawn(PlatformerSpawnKind Kind, int Col, int Row, int Length = 1);

// Уровень «Мета-платформера» в компактном ASCII. Строки сверху вниз, клетка 16×16.
//   .  пусто                 #  земля                 X  камень (твёрдый)
//   B  кирпич                C  кирпич с монетами     ?  звёздный блок с монетой
//   P  звёздный блок с усилением (Сахарная звезда / Огненный леденец)
//   =  платформа «сквозь»    ^  шипы                  ~  лава
//   o  монета                D  ворота босса (исчезают после победы)
//   S  старт Меты            K  чекпоинт              E  финиш (флаг/кристалл; клетка над землёй)
//   s  слаймик               k  колючка               b  летучая мышь
//   n  улитка                G  большой слайм (босс)
//   -  платформа, ездит вправо на 4 клетки и обратно (длина — число символов подряд)
//   |  платформа, ездит вверх на 4 клетки и обратно (длина — число символов подряд)
//   *  основание огненной палки (твёрдый блок, палка крутится вокруг)
public sealed class PlatformerLevel
{
	public const int TileSize = 16;

	public const int PlatformTravel = 4;

	public const int BrickCoinCount = 8;

	private readonly PlatformerTile[] _tiles;

	public string Id { get; }

	public string Name { get; }

	public string NameEn { get; }

	public PlatformerWorld World { get; }

	public int Width { get; }

	public int Height { get; }

	public int StartCol { get; }

	public int StartRow { get; }

	public int CheckpointCol { get; }

	public int CheckpointRow { get; }

	public int GoalCol { get; }

	public int GoalRow { get; }

	public double TimeLimit { get; init; } = 300.0;

	public IReadOnlyList<PlatformerSpawn> Spawns { get; }

	public ReadOnlySpan<PlatformerTile> Tiles => _tiles;

	private PlatformerLevel(string id, string name, string nameEn, PlatformerWorld world, int width, int height, PlatformerTile[] tiles, List<PlatformerSpawn> spawns, (int C, int R) start, (int C, int R) checkpoint, (int C, int R) goal)
	{
		Id = id;
		Name = name;
		NameEn = nameEn;
		World = world;
		Width = width;
		Height = height;
		_tiles = tiles;
		Spawns = spawns;
		(StartCol, StartRow) = start;
		(CheckpointCol, CheckpointRow) = checkpoint;
		(GoalCol, GoalRow) = goal;
	}

	// Вне карты: слева/справа стены, сверху и снизу пусто (снизу — пропасть).
	public PlatformerTile this[int col, int row]
	{
		get
		{
			if (col < 0 || col >= Width)
			{
				return PlatformerTile.Hard;
			}
			if (row < 0 || row >= Height)
			{
				return PlatformerTile.Empty;
			}
			return _tiles[row * Width + col];
		}
	}

	public PlatformerTile[] CopyTiles()
	{
		return (PlatformerTile[])_tiles.Clone();
	}

	public static bool IsSolid(PlatformerTile t)
	{
		return t is PlatformerTile.Ground or PlatformerTile.Hard or PlatformerTile.Brick or PlatformerTile.BrickCoins or PlatformerTile.Question or PlatformerTile.QuestionPower or PlatformerTile.Used or PlatformerTile.Door or PlatformerTile.FirePivot;
	}

	public static bool IsHazard(PlatformerTile t)
	{
		return t is PlatformerTile.Spikes or PlatformerTile.Lava;
	}

	public static PlatformerLevel Parse(string id, PlatformerWorld world, string name, string nameEn, IReadOnlyList<string> rows)
	{
		if (rows == null || rows.Count < 4)
		{
			throw new FormatException("Уровень " + id + ": слишком мало строк");
		}
		int width = rows[0].Length;
		int height = rows.Count;
		for (int r = 0; r < height; r++)
		{
			if (rows[r].Length != width)
			{
				throw new FormatException($"Уровень {id}: строка {r} длиной {rows[r].Length}, ожидалось {width}");
			}
		}
		PlatformerTile[] tiles = new PlatformerTile[width * height];
		List<PlatformerSpawn> spawns = new List<PlatformerSpawn>();
		(int, int) start = (-1, -1);
		(int, int) checkpoint = (-1, -1);
		(int, int) goal = (-1, -1);
		for (int r = 0; r < height; r++)
		{
			string row = rows[r];
			for (int c = 0; c < width; c++)
			{
				char ch = row[c];
				PlatformerTile t = PlatformerTile.Empty;
				switch (ch)
				{
				case '.':
				case ' ':
					break;
				case '#':
					t = PlatformerTile.Ground;
					break;
				case 'X':
					t = PlatformerTile.Hard;
					break;
				case 'B':
					t = PlatformerTile.Brick;
					break;
				case 'C':
					t = PlatformerTile.BrickCoins;
					break;
				case '?':
					t = PlatformerTile.Question;
					break;
				case 'P':
					t = PlatformerTile.QuestionPower;
					break;
				case '=':
					t = PlatformerTile.OneWay;
					break;
				case '^':
					t = PlatformerTile.Spikes;
					break;
				case '~':
					t = PlatformerTile.Lava;
					break;
				case 'o':
					t = PlatformerTile.Coin;
					break;
				case 'D':
					t = PlatformerTile.Door;
					break;
				case '*':
					t = PlatformerTile.FirePivot;
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.FireBar, c, r));
					break;
				case 'S':
					if (start.Item1 >= 0)
					{
						throw new FormatException("Уровень " + id + ": два старта");
					}
					start = (c, r);
					break;
				case 'K':
					checkpoint = (c, r);
					break;
				case 'E':
					if (goal.Item1 >= 0)
					{
						throw new FormatException("Уровень " + id + ": два финиша");
					}
					goal = (c, r);
					break;
				case 's':
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.Slime, c, r));
					break;
				case 'k':
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.Spiky, c, r));
					break;
				case 'b':
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.Bat, c, r));
					break;
				case 'n':
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.Snail, c, r));
					break;
				case 'G':
					spawns.Add(new PlatformerSpawn(PlatformerSpawnKind.Boss, c, r));
					break;
				case '-':
				case '|':
					if (c == 0 || row[c - 1] != ch)
					{
						int len = 1;
						while (c + len < width && row[c + len] == ch)
						{
							len++;
						}
						spawns.Add(new PlatformerSpawn(ch == '-' ? PlatformerSpawnKind.PlatformH : PlatformerSpawnKind.PlatformV, c, r, len));
					}
					break;
				default:
					throw new FormatException($"Уровень {id}: неизвестный символ '{ch}' в ({c},{r})");
				}
				tiles[r * width + c] = t;
			}
		}
		if (start.Item1 < 0)
		{
			throw new FormatException("Уровень " + id + ": нет старта S");
		}
		if (goal.Item1 < 0)
		{
			throw new FormatException("Уровень " + id + ": нет финиша E");
		}
		return new PlatformerLevel(id, name, nameEn, world, width, height, tiles, spawns, start, checkpoint, goal);
	}

	// Склеивает куски уровня по горизонтали; в каждом куске строки обязаны быть одной длины.
	public static string[] Join(params string[][] parts)
	{
		int height = parts[0].Length;
		string[] result = new string[height];
		for (int r = 0; r < height; r++)
		{
			result[r] = "";
		}
		for (int p = 0; p < parts.Length; p++)
		{
			string[] part = parts[p];
			if (part.Length != height)
			{
				throw new FormatException($"Кусок {p}: {part.Length} строк вместо {height}");
			}
			for (int r = 0; r < height; r++)
			{
				if (part[r].Length != part[0].Length)
				{
					throw new FormatException($"Кусок {p}, строка {r}: длина {part[r].Length} вместо {part[0].Length}");
				}
				result[r] += part[r];
			}
		}
		return result;
	}
}
