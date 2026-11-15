using System;
using System.Collections.Generic;

namespace Metamorph.Core.Games;

public enum TetrisEventKind
{
	Spawn,
	Move,
	Rotate,
	Hold,
	HardDrop,
	Lock,
	LineClear,
	LevelUp,
	GameOver
}

public enum TSpinKind
{
	None,
	Mini,
	Full
}

// Value: HardDrop — пройдено клеток; LineClear — число линий (Extra — TSpinKind); LevelUp — новый уровень.
public readonly record struct TetrisEvent(TetrisEventKind Kind, int Value = 0, int Extra = 0);

public readonly record struct TetrisClear(int Lines, TSpinKind TSpin, bool BackToBack, int Combo, long Points);

// Тетрис по гайдлайну: SRS, 7-bag, hold, задержка фиксации с ограничением сбросов, DAS/ARR.
// Фиксированный шаг 1/240 с — одинаковый ввод даёт одинаковую партию.
public sealed class TetrisEngine
{
	public const int Width = 10;

	public const int VisibleHeight = 20;

	public const int HiddenRows = 2;

	public const int Height = VisibleHeight + HiddenRows;

	public const int NextCount = 5;

	public const double Step = 1.0 / 240.0;

	public const double LockDelay = 0.5;

	public const int MaxLockResets = 15;

	public const double SoftDropFactor = 20.0;

	private readonly byte[] _cells = new byte[Width * Height];

	private readonly List<TetrominoKind> _queue = new List<TetrominoKind>(16);

	private readonly int[] _drop = new int[Height];

	private readonly int[] _cleared = new int[4];

	private SevenBag _bag;

	private int _startLevel;

	private double _acc;

	private double _fall;

	private double _lockTimer;

	private int _resets;

	private int _lowestY;

	private bool _lastWasRotation;

	private int _lastKick;

	private bool _leftHeld;

	private bool _rightHeld;

	private int _dir;

	private double _dasTimer;

	private double _arrTimer;

	private bool _charged;

	private bool _softHeld;

	private bool _b2b;

	public double Das { get; set; } = 0.160;

	public double Arr { get; set; } = 0.030;

	public TetrominoKind Current { get; private set; }

	public int Rotation { get; private set; }

	public int X { get; private set; }

	public int Y { get; private set; }

	public TetrominoKind? HoldPiece { get; private set; }

	public bool CanHold { get; private set; } = true;

	public long Score { get; private set; }

	public int Lines { get; private set; }

	public int Level { get; private set; }

	public int Combo { get; private set; } = -1;

	public bool BackToBackActive => _b2b;

	public bool IsGameOver { get; private set; }

	public int PiecesPlaced { get; private set; }

	public int LockResets => _resets;

	public TetrisClear LastClear { get; private set; }

	// Верхняя занятая строка стакана (Height — пусто).
	public int StackTop { get; private set; } = Height;

	public double LockProgress => Math.Clamp(_lockTimer / LockDelay, 0.0, 1.0);

	public bool Grounded => !IsGameOver && !Fits(Current, Rotation, X, Y + 1);

	// На сколько строк сдвинулась вниз каждая итоговая строка при последней очистке — для анимации осыпания.
	public ReadOnlySpan<int> LastDrop => _drop;

	public ReadOnlySpan<int> LastClearedRows => new ReadOnlySpan<int>(_cleared, 0, LastClear.Lines);

	public int GhostY
	{
		get
		{
			int y = Y;
			while (Fits(Current, Rotation, X, y + 1))
			{
				y++;
			}
			return y;
		}
	}

	public event Action<TetrisEvent>? Event;

	public TetrisEngine(ulong seed, int startLevel = 1)
	{
		_bag = new SevenBag(new GameRandom(seed));
		Reset(seed, startLevel);
	}

	public void Reset(ulong seed, int startLevel = 1)
	{
		Array.Clear(_cells);
		Array.Clear(_drop);
		_queue.Clear();
		_bag = new SevenBag(new GameRandom(seed));
		_startLevel = Math.Clamp(startLevel, 1, 30);
		Level = _startLevel;
		Score = 0;
		Lines = 0;
		Combo = -1;
		_b2b = false;
		IsGameOver = false;
		PiecesPlaced = 0;
		HoldPiece = null;
		CanHold = true;
		LastClear = default;
		StackTop = Height;
		_acc = 0.0;
		_leftHeld = _rightHeld = _softHeld = false;
		_dir = 0;
		Fill();
		Spawn(TakeNext());
	}

	public TetrominoKind PeekNext(int index)
	{
		Fill();
		return _queue[index];
	}

	// 0 — пусто, иначе (int)TetrominoKind + 1.
	public int Cell(int x, int y)
	{
		if ((uint)x >= Width || (uint)y >= Height)
		{
			return 0;
		}
		return _cells[y * Width + x];
	}

	public void SetCell(int x, int y, TetrominoKind? kind)
	{
		_cells[y * Width + x] = (byte)(kind.HasValue ? (int)kind.Value + 1 : 0);
		RecomputeStackTop();
	}

	// Заменить текущую фигуру (тесты, отладка).
	public void SpawnPiece(TetrominoKind kind)
	{
		IsGameOver = false;
		Spawn(kind);
	}

	public static double GravityInterval(int level)
	{
		int l = Math.Clamp(level, 1, 20);
		return Math.Pow(0.8 - (l - 1) * 0.007, l - 1);
	}

	public void Update(double dt)
	{
		if (IsGameOver || !double.IsFinite(dt))
		{
			return;
		}
		_acc += Math.Clamp(dt, 0.0, 0.25);
		while (_acc >= Step && !IsGameOver)
		{
			_acc -= Step;
			Tick();
		}
	}

	public void SetLeft(bool down)
	{
		SetHorizontal(-1, down);
	}

	public void SetRight(bool down)
	{
		SetHorizontal(1, down);
	}

	public void SetSoftDrop(bool down)
	{
		_softHeld = down;
		if (down)
		{
			_fall = Math.Max(_fall, GravityInterval(Level) / SoftDropFactor);
		}
	}

	public bool TryShift(int dx)
	{
		if (IsGameOver || !Fits(Current, Rotation, X + dx, Y))
		{
			return false;
		}
		X += dx;
		_lastWasRotation = false;
		MoveReset();
		Event?.Invoke(new TetrisEvent(TetrisEventKind.Move, dx));
		return true;
	}

	public bool TryRotate(bool clockwise)
	{
		if (IsGameOver)
		{
			return false;
		}
		int to = (Rotation + (clockwise ? 1 : 3)) & 3;
		int count = Tetromino.KickCount(Current);
		for (int i = 0; i < count; i++)
		{
			(int kx, int ky) = Tetromino.Kick(Current, Rotation, clockwise, i);
			if (Fits(Current, to, X + kx, Y + ky))
			{
				Rotation = to;
				X += kx;
				Y += ky;
				_lastWasRotation = true;
				_lastKick = i;
				MoveReset();
				CheckLowest();
				Event?.Invoke(new TetrisEvent(TetrisEventKind.Rotate, clockwise ? 1 : -1, i));
				return true;
			}
		}
		return false;
	}

	public bool Hold()
	{
		if (IsGameOver || !CanHold)
		{
			return false;
		}
		TetrominoKind current = Current;
		TetrominoKind? held = HoldPiece;
		HoldPiece = current;
		CanHold = false;
		Spawn(held ?? TakeNext());
		Event?.Invoke(new TetrisEvent(TetrisEventKind.Hold, (int)current));
		return true;
	}

	public int HardDrop()
	{
		if (IsGameOver)
		{
			return 0;
		}
		int d = 0;
		while (Fits(Current, Rotation, X, Y + 1))
		{
			Y++;
			d++;
		}
		if (d > 0)
		{
			_lastWasRotation = false;
		}
		Score += 2 * d;
		Event?.Invoke(new TetrisEvent(TetrisEventKind.HardDrop, d));
		Lock();
		return d;
	}

	private void Tick()
	{
		AutoShift();
		if (IsGameOver)
		{
			return;
		}
		double interval = GravityInterval(Level);
		if (_softHeld)
		{
			interval /= SoftDropFactor;
		}
		_fall += Step;
		while (_fall >= interval)
		{
			_fall -= interval;
			if (!MoveDown())
			{
				_fall = 0.0;
				break;
			}
			if (_softHeld)
			{
				Score++;
			}
		}
		if (!Fits(Current, Rotation, X, Y + 1))
		{
			_lockTimer += Step;
			if (_lockTimer >= LockDelay - 1e-9)
			{
				Lock();
			}
		}
	}

	private void AutoShift()
	{
		if (_dir == 0)
		{
			return;
		}
		_dasTimer += Step;
		if (_dasTimer < Das)
		{
			return;
		}
		if (!_charged)
		{
			_charged = true;
			_arrTimer = Arr;
		}
		else
		{
			_arrTimer += Step;
		}
		if (Arr <= 0.0)
		{
			while (TryShift(_dir))
			{
			}
			return;
		}
		while (_arrTimer >= Arr)
		{
			_arrTimer -= Arr;
			if (!TryShift(_dir))
			{
				_arrTimer = 0.0;
				break;
			}
		}
	}

	private void SetHorizontal(int dir, bool down)
	{
		ref bool held = ref dir < 0 ? ref _leftHeld : ref _rightHeld;
		if (held == down)
		{
			return;
		}
		held = down;
		if (down)
		{
			_dir = dir;
			_dasTimer = 0.0;
			_arrTimer = 0.0;
			_charged = false;
			TryShift(dir);
		}
		else if (_dir == dir)
		{
			bool other = dir < 0 ? _rightHeld : _leftHeld;
			_dir = other ? -dir : 0;
			_dasTimer = 0.0;
			_arrTimer = 0.0;
			_charged = false;
		}
	}

	private bool MoveDown()
	{
		if (!Fits(Current, Rotation, X, Y + 1))
		{
			return false;
		}
		Y++;
		_lastWasRotation = false;
		_lockTimer = 0.0;
		CheckLowest();
		return true;
	}

	private void MoveReset()
	{
		bool grounded = !Fits(Current, Rotation, X, Y + 1);
		if ((grounded || _lockTimer > 0.0) && _resets < MaxLockResets)
		{
			_resets++;
			_lockTimer = 0.0;
		}
	}

	private void CheckLowest()
	{
		if (Y > _lowestY)
		{
			_lowestY = Y;
			_resets = 0;
		}
	}

	private void Spawn(TetrominoKind kind)
	{
		Current = kind;
		Rotation = 0;
		X = kind == TetrominoKind.O ? 4 : 3;
		Y = 0;
		_lockTimer = 0.0;
		_resets = 0;
		_fall = 0.0;
		_lastWasRotation = false;
		_lastKick = -1;
		if (!Fits(kind, 0, X, Y))
		{
			GameOver();
			return;
		}
		if (Fits(kind, 0, X, Y + 1))
		{
			Y++;
		}
		_lowestY = Y;
		Event?.Invoke(new TetrisEvent(TetrisEventKind.Spawn, (int)kind));
	}

	private void Lock()
	{
		bool allHidden = true;
		foreach ((int cx, int cy) in Tetromino.CellsOf(Current, Rotation))
		{
			int x = X + cx;
			int y = Y + cy;
			if (y >= 0)
			{
				_cells[y * Width + x] = (byte)((int)Current + 1);
			}
			if (y >= HiddenRows)
			{
				allHidden = false;
			}
		}
		PiecesPlaced++;
		TSpinKind tspin = DetectTSpin();
		int lines = ClearLines();
		AddScore(lines, tspin);
		RecomputeStackTop();
		Event?.Invoke(new TetrisEvent(TetrisEventKind.Lock, lines));
		if (lines > 0 || tspin != TSpinKind.None)
		{
			Event?.Invoke(new TetrisEvent(TetrisEventKind.LineClear, lines, (int)tspin));
		}
		int level = _startLevel + Lines / 10;
		if (level > Level)
		{
			Level = level;
			Event?.Invoke(new TetrisEvent(TetrisEventKind.LevelUp, level));
		}
		if (allHidden && lines == 0)
		{
			GameOver();
			return;
		}
		CanHold = true;
		Spawn(TakeNext());
	}

	private void AddScore(int lines, TSpinKind tspin)
	{
		int basePoints = tspin switch
		{
			TSpinKind.Full => lines switch
			{
				0 => 400,
				1 => 800,
				2 => 1200,
				_ => 1600
			},
			TSpinKind.Mini => lines switch
			{
				0 => 100,
				1 => 200,
				_ => 400
			},
			_ => lines switch
			{
				0 => 0,
				1 => 100,
				2 => 300,
				3 => 500,
				_ => 800
			}
		};
		bool b2b = false;
		if (lines > 0)
		{
			bool difficult = lines == 4 || tspin != TSpinKind.None;
			if (difficult)
			{
				b2b = _b2b;
				_b2b = true;
			}
			else
			{
				_b2b = false;
			}
		}
		long points = (long)basePoints * Level;
		if (b2b)
		{
			points = points * 3 / 2;
		}
		if (lines > 0)
		{
			Combo++;
			if (Combo > 0)
			{
				points += 50L * Combo * Level;
			}
		}
		else
		{
			Combo = -1;
		}
		Score += points;
		Lines += lines;
		LastClear = new TetrisClear(lines, tspin, b2b, Math.Max(Combo, 0), points);
	}

	private TSpinKind DetectTSpin()
	{
		if (Current != TetrominoKind.T || !_lastWasRotation)
		{
			return TSpinKind.None;
		}
		int cx = X + 1;
		int cy = Y + 1;
		bool tl = Occupied(cx - 1, cy - 1);
		bool tr = Occupied(cx + 1, cy - 1);
		bool bl = Occupied(cx - 1, cy + 1);
		bool br = Occupied(cx + 1, cy + 1);
		int count = (tl ? 1 : 0) + (tr ? 1 : 0) + (bl ? 1 : 0) + (br ? 1 : 0);
		if (count < 3)
		{
			return TSpinKind.None;
		}
		bool front = Rotation switch
		{
			0 => tl && tr,
			1 => tr && br,
			2 => bl && br,
			_ => tl && bl
		};
		return front || _lastKick == 4 ? TSpinKind.Full : TSpinKind.Mini;
	}

	private bool Occupied(int x, int y)
	{
		if (x < 0 || x >= Width || y >= Height)
		{
			return true;
		}
		return y >= 0 && _cells[y * Width + x] != 0;
	}

	private int ClearLines()
	{
		int count = 0;
		int write = Height - 1;
		for (int read = Height - 1; read >= 0; read--)
		{
			bool full = true;
			for (int x = 0; x < Width; x++)
			{
				if (_cells[read * Width + x] == 0)
				{
					full = false;
					break;
				}
			}
			if (full)
			{
				if (count < _cleared.Length)
				{
					_cleared[count] = read;
				}
				count++;
				continue;
			}
			if (write != read)
			{
				Array.Copy(_cells, read * Width, _cells, write * Width, Width);
			}
			_drop[write] = write - read;
			write--;
		}
		for (int y = write; y >= 0; y--)
		{
			Array.Clear(_cells, y * Width, Width);
			_drop[y] = 0;
		}
		return count;
	}

	private void RecomputeStackTop()
	{
		for (int i = 0; i < _cells.Length; i++)
		{
			if (_cells[i] != 0)
			{
				StackTop = i / Width;
				return;
			}
		}
		StackTop = Height;
	}

	private void GameOver()
	{
		if (!IsGameOver)
		{
			IsGameOver = true;
			_dir = 0;
			Event?.Invoke(new TetrisEvent(TetrisEventKind.GameOver, (int)Math.Min(Score, int.MaxValue)));
		}
	}

	private void Fill()
	{
		while (_queue.Count < NextCount + 1)
		{
			_queue.Add(_bag.Next());
		}
	}

	private TetrominoKind TakeNext()
	{
		Fill();
		TetrominoKind k = _queue[0];
		_queue.RemoveAt(0);
		Fill();
		return k;
	}

	public bool Fits(TetrominoKind kind, int rotation, int x, int y)
	{
		foreach ((int cx, int cy) in Tetromino.CellsOf(kind, rotation))
		{
			int bx = x + cx;
			int by = y + cy;
			if (bx < 0 || bx >= Width || by >= Height)
			{
				return false;
			}
			if (by >= 0 && _cells[by * Width + bx] != 0)
			{
				return false;
			}
		}
		return true;
	}
}
