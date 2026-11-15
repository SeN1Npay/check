using System;
using System.Collections.Generic;

namespace Metamorph.Core.Games;

public enum PlatformerPower
{
	Small,
	Big,
	Fire
}

public enum PlatformerPhase
{
	Playing,
	Dying,
	Clearing,
	Cleared,
	GameOver
}

public enum PlatformerClearStage
{
	Slide,
	Walk,
	Tally,
	Done
}

public enum PlatformerEnemyKind
{
	Slime,
	Spiky,
	Bat,
	Snail,
	Boss
}

public enum PlatformerEnemyState
{
	Walk,
	Squished,
	Shell,
	ShellMoving,
	Knocked,
	Gone
}

public enum PlatformerItemKind
{
	SugarStar,
	FireCandy
}

public enum PlatformerEventKind
{
	Jump,
	Land,
	Coin,
	BlockCoin,
	PowerUpAppear,
	PowerUp,
	Stomp,
	Kick,
	Knock,
	Hurt,
	Death,
	BlockBump,
	BlockBreak,
	Bonk,
	Throw,
	CandyPop,
	ExtraLife,
	Checkpoint,
	HurryUp,
	BossHit,
	BossDefeated,
	Goal,
	TimeBonus,
	LevelClear,
	GameOver,
	Respawn
}

[Flags]
public enum PlatformerInput
{
	None = 0,
	Left = 1,
	Right = 2,
	Jump = 4,
	Run = 8,
	Fire = 0x10,
	Down = 0x20
}

// X, Y — мировые координаты (пиксели уровня, y вниз), Value — очки/счётчик по смыслу события.
public readonly record struct PlatformerEvent(PlatformerEventKind Kind, double X = 0.0, double Y = 0.0, int Value = 0);

public struct PlatformerEnemy
{
	public PlatformerEnemyKind Kind;

	public PlatformerEnemyState State;

	// Левый верхний угол.
	public double X;

	public double Y;

	public double W;

	public double H;

	public double Vx;

	public double Vy;

	public double BaseY;

	public double Phase;

	public double Timer;

	public double Flash;

	public int Hp;

	public int Chain;

	public bool Active;

	public bool OnGround;
}

public struct PlatformerItem
{
	public PlatformerItemKind Kind;

	public double X;

	public double Y;

	public double Vx;

	public double Vy;

	public double BaseY;

	public double Emerge;

	public bool Alive;
}

public struct PlatformerCandy
{
	public double X;

	public double Y;

	public double Vx;

	public double Vy;

	public double Life;

	public bool Alive;
}

public struct PlatformerPlatform
{
	public double X;

	public double Y;

	public double W;

	public double X0;

	public double Y0;

	public double Dx;

	public double Dy;

	public double Offset;

	public bool Vertical;
}

public struct PlatformerFireBar
{
	public double Cx;

	public double Cy;

	public double Angle;

	public double Speed;

	public int Count;
}

public struct PlatformerBump
{
	public int Col;

	public int Row;

	public double Time;
}

// «Мета-платформер»: чистая логика, фиксированный шаг 120 Гц, полностью детерминирован (без ГСЧ).
public sealed class PlatformerEngine
{
	public const double Step = 1.0 / 120.0;

	public const double T = PlatformerLevel.TileSize;

	public const double WalkSpeed = 90.0;

	public const double RunSpeed = 150.0;

	public const double GroundAccel = 480.0;

	public const double RunAccel = 400.0;

	public const double AirAccel = 360.0;

	public const double Friction = 560.0;

	public const double AirDrag = 120.0;

	public const double SkidDecel = 1200.0;

	public const double JumpVelocity = 300.0;

	public const double JumpRunBonus = 30.0;

	public const double GravityHold = 650.0;

	public const double GravityCut = 1900.0;

	public const double GravityFall = 1400.0;

	// Вершина прыжка: пока держишь прыжок и скорость мала — гравитация слабее («зависание»).
	public const double ApexThreshold = 45.0;

	public const double ApexScale = 0.5;

	public const double TerminalVelocity = 360.0;

	public const double CoyoteTime = 0.1;

	public const double JumpBufferTime = 0.12;

	public const double StompBounce = 200.0;

	public const double StompBounceHigh = 330.0;

	public const double CornerNudge = 5.0;

	public const double InvincibleTime = 2.0;

	public const double SmallW = 10.0;

	public const double SmallH = 14.0;

	public const double BigW = 12.0;

	public const double BigH = 26.0;

	public const double ShellSpeed = 210.0;

	public const double ShellRevive = 7.0;

	public const double CandySpeed = 200.0;

	public const double CandyBounce = 170.0;

	public const double PlatformPeriod = 4.0;

	public const double BumpTime = 0.2;

	public const double LookAhead = 28.0;

	public const int StartLives = 3;

	public const int CoinsPerLife = 100;

	private const double Eps = 0.001;

	private static readonly int[] ComboScores = { 100, 200, 400, 500, 800, 1000, 2000, 4000, 5000, 8000 };

	private readonly Dictionary<int, int> _brickCoins = new Dictionary<int, int>();

	private readonly PlatformerItem[] _items = new PlatformerItem[8];

	private readonly PlatformerCandy[] _candies = new PlatformerCandy[2];

	private readonly PlatformerBump[] _bumps = new PlatformerBump[8];

	private PlatformerLevel _level;

	private PlatformerTile[] _tiles = Array.Empty<PlatformerTile>();

	private int _w;

	private int _h;

	private PlatformerEnemy[] _enemies = Array.Empty<PlatformerEnemy>();

	private int _enemyCount;

	private PlatformerPlatform[] _platforms = Array.Empty<PlatformerPlatform>();

	private int _platformCount;

	private PlatformerFireBar[] _bars = Array.Empty<PlatformerFireBar>();

	private int _barCount;

	private double _acc;

	private PlatformerInput _input;

	private PlatformerInput _prevInput;

	private double _mx;

	private double _my;

	private double _mw = SmallW;

	private double _mh = SmallH;

	private double _coyote;

	private double _jumpBuffer;

	private bool _jumping;

	private int _ride = -1;

	private int _combo;

	private bool _hurried;

	private bool _deathPopped;

	private int _tallyLeft;

	private double _look;

	private int _startScore;

	private int _startCoins;

	private PlatformerPower _startPower;

	private double _viewWidth = 384.0;

	private double _viewHeight = 240.0;

	public PlatformerLevel Level => _level;

	public int Width => _w;

	public int Height => _h;

	public ReadOnlySpan<PlatformerTile> Tiles => _tiles;

	public ReadOnlySpan<PlatformerEnemy> Enemies => new ReadOnlySpan<PlatformerEnemy>(_enemies, 0, _enemyCount);

	public ReadOnlySpan<PlatformerItem> Items => _items;

	public ReadOnlySpan<PlatformerCandy> Candies => _candies;

	public ReadOnlySpan<PlatformerPlatform> Platforms => new ReadOnlySpan<PlatformerPlatform>(_platforms, 0, _platformCount);

	public ReadOnlySpan<PlatformerFireBar> FireBars => new ReadOnlySpan<PlatformerFireBar>(_bars, 0, _barCount);

	public ReadOnlySpan<PlatformerBump> Bumps => _bumps;

	public double MetaLeft => _mx;

	public double MetaTop => _my;

	public double MetaWidth => _mw;

	public double MetaHeight => _mh;

	public double MetaX => _mx + _mw / 2.0;

	public double MetaBottom => _my + _mh;

	public double MetaVx { get; private set; }

	public double MetaVy { get; private set; }

	public bool OnGround { get; private set; }

	public int Facing { get; private set; } = 1;

	public bool Ducking { get; private set; }

	public bool Skidding { get; private set; }

	public PlatformerPower Power { get; private set; }

	public double Invincible { get; private set; }

	public int Score { get; private set; }

	public int Coins { get; private set; }

	public int Lives { get; private set; }

	public double TimeLeft { get; private set; }

	public PlatformerPhase Phase { get; private set; }

	public double PhaseTime { get; private set; }

	public PlatformerClearStage ClearStage { get; private set; }

	public bool BossDefeated { get; private set; }

	public bool CheckpointReached { get; private set; }

	public int Combo => _combo;

	// Время текущей попытки уровня (платформы, анимации).
	public double LevelTime { get; private set; }

	public double CameraX { get; private set; }

	public double CameraY { get; private set; }

	// Размер видимой области в пикселях уровня; при смене камера сразу зажимается в границы.
	public double ViewWidth
	{
		get => _viewWidth;
		set
		{
			if (value > 0.0 && value != _viewWidth)
			{
				_viewWidth = value;
				ClampCamera();
			}
		}
	}

	public double ViewHeight
	{
		get => _viewHeight;
		set
		{
			if (value > 0.0 && value != _viewHeight)
			{
				_viewHeight = value;
				ClampCamera();
			}
		}
	}

	public event Action<PlatformerEvent>? Event;

	public PlatformerEngine(PlatformerLevel level, int lives = StartLives)
	{
		_level = level;
		Lives = lives;
		LoadLevel(level, keepStats: false);
	}

	public static bool Has(PlatformerInput set, PlatformerInput flag)
	{
		return (set & flag) != 0;
	}

	// keepStats: true — переход на следующий уровень с очками, монетами и силой.
	public void LoadLevel(PlatformerLevel level, bool keepStats)
	{
		_level = level;
		if (!keepStats)
		{
			Score = 0;
			Coins = 0;
			Power = PlatformerPower.Small;
		}
		_startScore = Score;
		_startCoins = Coins;
		_startPower = Power;
		CheckpointReached = false;
		Build(atCheckpoint: false);
	}

	// R: уровень заново без потери жизни, но очки и сила — как на входе в уровень.
	public void RestartLevel()
	{
		if (Phase == PlatformerPhase.GameOver)
		{
			return;
		}
		Score = _startScore;
		Coins = _startCoins;
		Power = _startPower;
		CheckpointReached = false;
		Build(atCheckpoint: false);
	}

	// Для тестов и отладки.
	public void SetPower(PlatformerPower power)
	{
		Power = power;
		ApplySize();
	}

	public void SetInput(PlatformerInput input)
	{
		_input = input;
	}

	public PlatformerTile TileAt(int col, int row)
	{
		if (col < 0 || col >= _w)
		{
			return PlatformerTile.Hard;
		}
		if (row < 0 || row >= _h)
		{
			return PlatformerTile.Empty;
		}
		return _tiles[row * _w + col];
	}

	public void Update(double dt)
	{
		if (!double.IsFinite(dt))
		{
			return;
		}
		_acc += Math.Clamp(dt, 0.0, 0.25);
		while (_acc >= Step)
		{
			_acc -= Step;
			if (Phase is PlatformerPhase.Cleared or PlatformerPhase.GameOver)
			{
				_acc = 0.0;
				break;
			}
			Tick();
		}
	}

	private void Build(bool atCheckpoint)
	{
		_tiles = _level.CopyTiles();
		_w = _level.Width;
		_h = _level.Height;
		_brickCoins.Clear();
		for (int i = 0; i < _tiles.Length; i++)
		{
			if (_tiles[i] == PlatformerTile.BrickCoins)
			{
				_brickCoins[i] = PlatformerLevel.BrickCoinCount;
			}
		}
		int enemies = 0;
		int platforms = 0;
		int bars = 0;
		foreach (PlatformerSpawn s in _level.Spawns)
		{
			switch (s.Kind)
			{
			case PlatformerSpawnKind.PlatformH:
			case PlatformerSpawnKind.PlatformV:
				platforms++;
				break;
			case PlatformerSpawnKind.FireBar:
				bars++;
				break;
			default:
				enemies++;
				break;
			}
		}
		if (_enemies.Length < enemies)
		{
			_enemies = new PlatformerEnemy[enemies];
		}
		if (_platforms.Length < platforms)
		{
			_platforms = new PlatformerPlatform[platforms];
		}
		if (_bars.Length < bars)
		{
			_bars = new PlatformerFireBar[bars];
		}
		_enemyCount = _platformCount = _barCount = 0;
		foreach (PlatformerSpawn s in _level.Spawns)
		{
			switch (s.Kind)
			{
			case PlatformerSpawnKind.PlatformH:
			case PlatformerSpawnKind.PlatformV:
				_platforms[_platformCount++] = new PlatformerPlatform
				{
					X = s.Col * T,
					Y = s.Row * T,
					X0 = s.Col * T,
					Y0 = s.Row * T,
					W = s.Length * T,
					Vertical = s.Kind == PlatformerSpawnKind.PlatformV,
					Offset = (s.Col % 5) * 0.4
				};
				break;
			case PlatformerSpawnKind.FireBar:
				_bars[_barCount++] = new PlatformerFireBar
				{
					Cx = s.Col * T + T / 2.0,
					Cy = s.Row * T + T / 2.0,
					Angle = s.Col * 0.37,
					Speed = ((s.Col + s.Row) & 1) == 0 ? 1.9 : -1.9,
					Count = 6
				};
				break;
			default:
				_enemies[_enemyCount++] = MakeEnemy(s);
				break;
			}
		}
		Array.Clear(_items);
		Array.Clear(_candies);
		Array.Clear(_bumps);
		BossDefeated = false;
		int col = _level.StartCol;
		int row = _level.StartRow;
		if (atCheckpoint && _level.CheckpointCol >= 0)
		{
			col = _level.CheckpointCol;
			row = _level.CheckpointRow;
		}
		Ducking = false;
		Skidding = false;
		_mw = Power == PlatformerPower.Small ? SmallW : BigW;
		_mh = Power == PlatformerPower.Small ? SmallH : BigH;
		_mx = col * T + T / 2.0 - _mw / 2.0;
		_my = (row + 1) * T - _mh;
		MetaVx = MetaVy = 0.0;
		OnGround = true;
		Facing = 1;
		Invincible = 0.0;
		_coyote = CoyoteTime;
		_jumpBuffer = 0.0;
		_jumping = false;
		_ride = -1;
		_combo = 0;
		_hurried = false;
		_deathPopped = false;
		_input = _prevInput = PlatformerInput.None;
		_acc = 0.0;
		TimeLeft = _level.TimeLimit;
		LevelTime = 0.0;
		Phase = PlatformerPhase.Playing;
		PhaseTime = 0.0;
		ClearStage = PlatformerClearStage.Slide;
		if (!CanStand())
		{
			Ducking = Power != PlatformerPower.Small;
			ApplySize();
		}
		SnapCamera();
	}

	private static PlatformerEnemy MakeEnemy(PlatformerSpawn s)
	{
		PlatformerEnemyKind kind = s.Kind switch
		{
			PlatformerSpawnKind.Spiky => PlatformerEnemyKind.Spiky,
			PlatformerSpawnKind.Bat => PlatformerEnemyKind.Bat,
			PlatformerSpawnKind.Snail => PlatformerEnemyKind.Snail,
			PlatformerSpawnKind.Boss => PlatformerEnemyKind.Boss,
			_ => PlatformerEnemyKind.Slime
		};
		(double w, double h) = EnemySize(kind, PlatformerEnemyState.Walk);
		double x = s.Col * T + T / 2.0 - w / 2.0;
		double y = kind == PlatformerEnemyKind.Bat ? s.Row * T + (T - h) / 2.0 : (s.Row + 1) * T - h;
		return new PlatformerEnemy
		{
			Kind = kind,
			State = PlatformerEnemyState.Walk,
			X = x,
			Y = y,
			W = w,
			H = h,
			Vx = -EnemySpeed(kind),
			BaseY = y,
			Phase = s.Col * 0.7,
			Timer = kind == PlatformerEnemyKind.Boss ? 1.6 : 0.0,
			Hp = kind == PlatformerEnemyKind.Boss ? 3 : 1
		};
	}

	public static (double W, double H) EnemySize(PlatformerEnemyKind kind, PlatformerEnemyState state)
	{
		if (kind == PlatformerEnemyKind.Snail && state is PlatformerEnemyState.Shell or PlatformerEnemyState.ShellMoving)
		{
			return (14.0, 12.0);
		}
		return kind switch
		{
			PlatformerEnemyKind.Spiky => (14.0, 13.0),
			PlatformerEnemyKind.Bat => (14.0, 10.0),
			PlatformerEnemyKind.Snail => (14.0, 14.0),
			PlatformerEnemyKind.Boss => (32.0, 26.0),
			_ => (14.0, 11.0)
		};
	}

	private static double EnemySpeed(PlatformerEnemyKind kind)
	{
		return kind switch
		{
			PlatformerEnemyKind.Spiky => 24.0,
			PlatformerEnemyKind.Bat => 30.0,
			PlatformerEnemyKind.Snail => 22.0,
			PlatformerEnemyKind.Boss => 34.0,
			_ => 28.0
		};
	}

	private void Emit(PlatformerEventKind kind, double x = 0.0, double y = 0.0, int value = 0)
	{
		Event?.Invoke(new PlatformerEvent(kind, x, y, value));
	}

	private void Tick()
	{
		PhaseTime += Step;
		bool jumpPressed = Has(_input, PlatformerInput.Jump) && !Has(_prevInput, PlatformerInput.Jump);
		bool firePressed = Has(_input, PlatformerInput.Fire) && !Has(_prevInput, PlatformerInput.Fire);
		_prevInput = _input;
		switch (Phase)
		{
		case PlatformerPhase.Playing:
			LevelTime += Step;
			if (jumpPressed)
			{
				_jumpBuffer = JumpBufferTime;
			}
			if (firePressed)
			{
				Throw();
			}
			TickPlaying();
			break;
		case PlatformerPhase.Dying:
			TickDying();
			break;
		case PlatformerPhase.Clearing:
			LevelTime += Step;
			TickClearing();
			break;
		}
		for (int i = 0; i < _bumps.Length; i++)
		{
			if (_bumps[i].Time > 0.0)
			{
				_bumps[i].Time += Step;
				if (_bumps[i].Time >= BumpTime)
				{
					_bumps[i].Time = 0.0;
				}
			}
		}
		UpdateCamera();
	}

	private void TickPlaying()
	{
		if (Invincible > 0.0)
		{
			Invincible = Math.Max(0.0, Invincible - Step);
		}
		UpdatePlatforms();
		UpdateMeta();
		if (Phase != PlatformerPhase.Playing)
		{
			return;
		}
		CheckTiles();
		if (Phase != PlatformerPhase.Playing)
		{
			return;
		}
		UpdateEnemies();
		UpdateItems();
		UpdateCandies();
		UpdateFireBars();
		if (Phase != PlatformerPhase.Playing)
		{
			return;
		}
		TimeLeft -= Step;
		if (!_hurried && TimeLeft <= 100.0)
		{
			_hurried = true;
			Emit(PlatformerEventKind.HurryUp);
		}
		if (TimeLeft <= 0.0)
		{
			TimeLeft = 0.0;
			Die();
		}
	}

	// ── Мета ──────────────────────────────────────────────────────────────

	private void UpdateMeta()
	{
		bool left = Has(_input, PlatformerInput.Left);
		bool right = Has(_input, PlatformerInput.Right);
		bool jump = Has(_input, PlatformerInput.Jump);
		bool run = Has(_input, PlatformerInput.Run);
		bool down = Has(_input, PlatformerInput.Down);
		if (OnGround)
		{
			if (down)
			{
				Ducking = true;
			}
			else if (Ducking && CanStand())
			{
				Ducking = false;
			}
		}
		else if (!down && Ducking && CanStand())
		{
			Ducking = false;
		}
		ApplySize();
		int dir = (right ? 1 : 0) - (left ? 1 : 0);
		if (Ducking && OnGround)
		{
			dir = 0;
		}
		double vx = MetaVx;
		double max = run ? RunSpeed : WalkSpeed;
		Skidding = false;
		if (dir != 0)
		{
			Facing = dir;
			if (vx * dir < 0.0)
			{
				double decel = OnGround ? SkidDecel : AirAccel * 1.5;
				vx += dir * decel * Step;
				Skidding = OnGround && Math.Abs(vx) > 40.0;
			}
			else if (Math.Abs(vx) < max)
			{
				double accel = OnGround ? (run ? RunAccel : GroundAccel) : AirAccel;
				vx += dir * accel * Step;
				if (Math.Abs(vx) > max)
				{
					vx = dir * max;
				}
			}
			else if (OnGround)
			{
				vx = Approach(vx, dir * max, Friction * Step);
			}
		}
		else
		{
			vx = Approach(vx, 0.0, (OnGround ? Friction : AirDrag) * Step);
		}
		MetaVx = vx;
		if (_jumpBuffer > 0.0)
		{
			_jumpBuffer -= Step;
		}
		if (OnGround)
		{
			_coyote = CoyoteTime;
		}
		else if (_coyote > 0.0)
		{
			_coyote -= Step;
		}
		double vy = MetaVy;
		if (_jumpBuffer > 0.0 && _coyote > 0.0)
		{
			vy = -(JumpVelocity + JumpRunBonus * Math.Min(1.0, Math.Abs(vx) / RunSpeed));
			OnGround = false;
			_coyote = 0.0;
			_jumpBuffer = 0.0;
			_jumping = true;
			_ride = -1;
			Emit(PlatformerEventKind.Jump, MetaX, MetaBottom);
		}
		if (!jump)
		{
			_jumping = false;
		}
		double g = vy < 0.0 ? (_jumping ? GravityHold : GravityCut) : GravityFall;
		if (_jumping && Math.Abs(vy) < ApexThreshold)
		{
			g *= ApexScale;
		}
		vy = Math.Min(vy + g * Step, TerminalVelocity);
		if (_ride >= 0 && _ride < _platformCount)
		{
			ref PlatformerPlatform p = ref _platforms[_ride];
			MoveX(ref _mx, _my, _mw, _mh, p.Dx);
			if (vy >= 0.0)
			{
				_my = p.Y - _mh;
			}
		}
		if (MoveX(ref _mx, _my, _mw, _mh, vx * Step))
		{
			MetaVx = 0.0;
		}
		bool wasGround = OnGround;
		OnGround = false;
		double oldBottom = _my + _mh;
		_ride = -1;
		if (vy < 0.0)
		{
			MoveMetaUp(ref vy);
		}
		else if (MoveDown(ref _my, _mx, _mw, _mh, vy * Step))
		{
			OnGround = true;
		}
		else
		{
			for (int i = 0; i < _platformCount; i++)
			{
				ref PlatformerPlatform p = ref _platforms[i];
				double nb = _my + _mh;
				if (_mx + _mw > p.X + 1.0 && _mx < p.X + p.W - 1.0 && oldBottom <= Math.Max(p.Y, p.Y - p.Dy) + 1.0 && nb >= p.Y)
				{
					_my = p.Y - _mh;
					OnGround = true;
					_ride = i;
					break;
				}
			}
		}
		if (OnGround)
		{
			if (!wasGround)
			{
				Emit(PlatformerEventKind.Land, MetaX, MetaBottom, (int)vy);
				_combo = 0;
			}
			vy = 0.0;
			_jumping = false;
		}
		MetaVy = vy;
	}

	private void MoveMetaUp(ref double vy)
	{
		double ny = _my + vy * Step;
		int r = Floor(ny / T);
		if ((r + 1) * T > _my + Eps)
		{
			_my = ny;
			return;
		}
		int c0 = Floor(_mx / T);
		int c1 = Floor((_mx + _mw - Eps) / T);
		bool s0 = PlatformerLevel.IsSolid(TileAt(c0, r));
		bool s1 = PlatformerLevel.IsSolid(TileAt(c1, r));
		if (!s0 && !s1)
		{
			_my = ny;
			return;
		}
		// Срезание угла: задели край блока на пару пикселей — сдвигаем, а не бьёмся головой.
		if (c0 != c1)
		{
			if (s0 && !s1 && (c0 + 1) * T - _mx <= CornerNudge)
			{
				_mx = (c0 + 1) * T;
				_my = ny;
				return;
			}
			if (s1 && !s0 && _mx + _mw - c1 * T <= CornerNudge)
			{
				_mx = c1 * T - _mw;
				_my = ny;
				return;
			}
		}
		_my = (r + 1) * T;
		vy = 0.0;
		_jumping = false;
		int cc = Floor(MetaX / T);
		if (!PlatformerLevel.IsSolid(TileAt(cc, r)))
		{
			cc = s0 ? c0 : c1;
		}
		HitBlock(cc, r);
	}

	private bool CanStand()
	{
		if (Power == PlatformerPower.Small)
		{
			return true;
		}
		double bottom = _my + _mh;
		double top = bottom - BigH;
		int c0 = Floor(_mx / T);
		int c1 = Floor((_mx + _mw - Eps) / T);
		int r0 = Floor(top / T);
		int r1 = Floor((bottom - Eps) / T);
		for (int c = c0; c <= c1; c++)
		{
			for (int r = r0; r <= r1; r++)
			{
				if (PlatformerLevel.IsSolid(TileAt(c, r)))
				{
					return false;
				}
			}
		}
		return true;
	}

	private void ApplySize()
	{
		double w = Power == PlatformerPower.Small ? SmallW : BigW;
		double h = Power == PlatformerPower.Small ? SmallH : (Ducking ? SmallH : BigH);
		if (w != _mw || h != _mh)
		{
			double cx = _mx + _mw / 2.0;
			double bottom = _my + _mh;
			_mw = w;
			_mh = h;
			_mx = cx - w / 2.0;
			_my = bottom - h;
		}
	}

	private void Throw()
	{
		if (Power != PlatformerPower.Fire || Phase != PlatformerPhase.Playing)
		{
			return;
		}
		for (int i = 0; i < _candies.Length; i++)
		{
			if (!_candies[i].Alive)
			{
				_candies[i] = new PlatformerCandy
				{
					X = MetaX + Facing * 6.0 - 3.0,
					Y = _my + 8.0,
					Vx = Facing * CandySpeed,
					Vy = 40.0,
					Life = 3.0,
					Alive = true
				};
				Emit(PlatformerEventKind.Throw, MetaX, _my + 8.0);
				return;
			}
		}
	}

	private void Hurt()
	{
		if (Invincible > 0.0 || Phase != PlatformerPhase.Playing)
		{
			return;
		}
		if (Power != PlatformerPower.Small)
		{
			Power = PlatformerPower.Small;
			Ducking = false;
			ApplySize();
			Invincible = InvincibleTime;
			Emit(PlatformerEventKind.Hurt, MetaX, _my);
			return;
		}
		Die();
	}

	private void Die()
	{
		if (Phase != PlatformerPhase.Playing)
		{
			return;
		}
		Phase = PlatformerPhase.Dying;
		PhaseTime = 0.0;
		Lives = Math.Max(0, Lives - 1);
		MetaVx = MetaVy = 0.0;
		_deathPopped = false;
		Power = PlatformerPower.Small;
		Ducking = false;
		ApplySize();
		Emit(PlatformerEventKind.Death, MetaX, _my, Lives);
	}

	private void TickDying()
	{
		if (PhaseTime >= 0.5)
		{
			if (!_deathPopped)
			{
				_deathPopped = true;
				MetaVy = -280.0;
			}
			MetaVy = Math.Min(MetaVy + GravityFall * Step, 500.0);
			_my += MetaVy * Step;
		}
		if (PhaseTime >= 2.6)
		{
			if (Lives > 0)
			{
				Build(CheckpointReached);
				Emit(PlatformerEventKind.Respawn, MetaX, MetaBottom, Lives);
			}
			else
			{
				Phase = PlatformerPhase.GameOver;
				PhaseTime = 0.0;
				Emit(PlatformerEventKind.GameOver, MetaX, MetaBottom, Score);
			}
		}
	}

	private void StartClear()
	{
		double poleBottom = (_level.GoalRow + 1) * T;
		double poleTop = PoleTop * T;
		double frac = Math.Clamp((poleBottom - MetaBottom) / Math.Max(T, poleBottom - poleTop), 0.0, 1.0);
		int bonus = frac >= 0.9 ? 5000 : frac >= 0.65 ? 2000 : frac >= 0.4 ? 800 : frac >= 0.2 ? 400 : 100;
		Score += bonus;
		Phase = PlatformerPhase.Clearing;
		PhaseTime = 0.0;
		ClearStage = PlatformerClearStage.Slide;
		MetaVx = MetaVy = 0.0;
		Ducking = false;
		ApplySize();
		Facing = 1;
		_mx = _level.GoalCol * T + 7.0 - _mw;
		Emit(PlatformerEventKind.Goal, MetaX, _my, bonus);
	}

	// Верхняя клетка флагштока.
	public int PoleTop => Math.Max(1, _level.GoalRow - 8);

	private void TickClearing()
	{
		switch (ClearStage)
		{
		case PlatformerClearStage.Slide:
			if (MoveDown(ref _my, _mx, _mw, _mh, 110.0 * Step) || _my > _h * T)
			{
				OnGround = true;
				ClearStage = PlatformerClearStage.Walk;
				PhaseTime = 0.0;
			}
			break;
		case PlatformerClearStage.Walk:
			if (PhaseTime > 0.35)
			{
				MetaVx = WalkSpeed * 0.8;
				MoveX(ref _mx, _my, _mw, _mh, MetaVx * Step);
				MetaVy = Math.Min(MetaVy + GravityFall * Step, TerminalVelocity);
				if (MoveDown(ref _my, _mx, _mw, _mh, MetaVy * Step))
				{
					MetaVy = 0.0;
					OnGround = true;
				}
				else
				{
					OnGround = false;
				}
			}
			if (PhaseTime > 1.9)
			{
				MetaVx = 0.0;
				ClearStage = PlatformerClearStage.Tally;
				_tallyLeft = (int)Math.Ceiling(TimeLeft);
				TimeLeft = _tallyLeft;
				Emit(PlatformerEventKind.TimeBonus, MetaX, _my, _tallyLeft * 50);
			}
			break;
		case PlatformerClearStage.Tally:
			if (_tallyLeft > 0)
			{
				int n = Math.Min(_tallyLeft, 2);
				_tallyLeft -= n;
				TimeLeft = _tallyLeft;
				Score += n * 50;
			}
			else if (PhaseTime > 0.4)
			{
				ClearStage = PlatformerClearStage.Done;
				Phase = PlatformerPhase.Cleared;
				PhaseTime = 0.0;
				Emit(PlatformerEventKind.LevelClear, MetaX, _my, Score);
			}
			break;
		}
	}

	private void CheckTiles()
	{
		double l = _mx + 1.0;
		double t = _my + 1.0;
		double r = _mx + _mw - 1.0;
		double b = _my + _mh - 1.0;
		int c0 = Floor(l / T);
		int c1 = Floor(r / T);
		int r0 = Floor(t / T);
		int r1 = Floor(b / T);
		for (int row = r0; row <= r1; row++)
		{
			for (int col = c0; col <= c1; col++)
			{
				switch (TileAt(col, row))
				{
				case PlatformerTile.Coin:
					_tiles[row * _w + col] = PlatformerTile.Empty;
					AddCoin(col * T + T / 2.0, row * T + T / 2.0, fromBlock: false);
					break;
				case PlatformerTile.Spikes:
					if (b > row * T + 8.0)
					{
						Hurt();
					}
					break;
				case PlatformerTile.Lava:
					if (b > row * T + 4.0)
					{
						Die();
						return;
					}
					break;
				}
			}
		}
		if (_level.CheckpointCol >= 0 && !CheckpointReached && MetaX >= _level.CheckpointCol * T)
		{
			CheckpointReached = true;
			Emit(PlatformerEventKind.Checkpoint, _level.CheckpointCol * T + T / 2.0, (_level.CheckpointRow + 1) * T);
		}
		if (_mx + _mw >= _level.GoalCol * T + 6.0 && _my < (_level.GoalRow + 1) * T)
		{
			StartClear();
			return;
		}
		if (_my > _h * T + 8.0)
		{
			Die();
		}
	}

	private void AddCoin(double x, double y, bool fromBlock)
	{
		Coins++;
		Score += 200;
		Emit(fromBlock ? PlatformerEventKind.BlockCoin : PlatformerEventKind.Coin, x, y, Coins);
		if (Coins >= CoinsPerLife)
		{
			Coins -= CoinsPerLife;
			Lives++;
			Emit(PlatformerEventKind.ExtraLife, x, y, Lives);
		}
	}

	private void HitBlock(int col, int row)
	{
		if (col < 0 || col >= _w || row < 0 || row >= _h)
		{
			return;
		}
		int idx = row * _w + col;
		double bx = col * T + T / 2.0;
		double by = row * T;
		bool bumped = true;
		switch (_tiles[idx])
		{
		case PlatformerTile.Question:
			_tiles[idx] = PlatformerTile.Used;
			AddCoin(bx, by, fromBlock: true);
			AddBump(col, row);
			break;
		case PlatformerTile.QuestionPower:
		{
			_tiles[idx] = PlatformerTile.Used;
			SpawnItem(Power == PlatformerPower.Small ? PlatformerItemKind.SugarStar : PlatformerItemKind.FireCandy, col, row);
			AddBump(col, row);
			Emit(PlatformerEventKind.PowerUpAppear, bx, by);
			break;
		}
		case PlatformerTile.BrickCoins:
		{
			int left = _brickCoins.TryGetValue(idx, out int n) ? n - 1 : 0;
			_brickCoins[idx] = left;
			if (left <= 0)
			{
				_tiles[idx] = PlatformerTile.Used;
			}
			AddCoin(bx, by, fromBlock: true);
			AddBump(col, row);
			break;
		}
		case PlatformerTile.Brick:
			if (Power != PlatformerPower.Small)
			{
				_tiles[idx] = PlatformerTile.Empty;
				Score += 50;
				Emit(PlatformerEventKind.BlockBreak, bx, by + T / 2.0, 50);
			}
			else
			{
				AddBump(col, row);
			}
			break;
		default:
			bumped = false;
			Emit(PlatformerEventKind.Bonk, bx, by + T);
			break;
		}
		if (!bumped)
		{
			return;
		}
		// Удар снизу сбивает врагов и подбрасывает монету, стоящих на блоке.
		for (int i = 0; i < _enemyCount; i++)
		{
			ref PlatformerEnemy e = ref _enemies[i];
			if (e.Active && e.State is PlatformerEnemyState.Walk or PlatformerEnemyState.Shell or PlatformerEnemyState.ShellMoving && e.Kind != PlatformerEnemyKind.Boss && Math.Abs(e.Y + e.H - row * T) < 4.0 && e.X + e.W > col * T && e.X < col * T + T)
			{
				Knock(ref e, 100);
			}
		}
		if (TileAt(col, row - 1) == PlatformerTile.Coin)
		{
			_tiles[idx - _w] = PlatformerTile.Empty;
			AddCoin(bx, by - T / 2.0, fromBlock: true);
		}
		for (int i = 0; i < _items.Length; i++)
		{
			ref PlatformerItem it = ref _items[i];
			if (it.Alive && it.Emerge >= 1.0 && Math.Abs(it.Y + 12.0 - row * T) < 3.0 && it.X + 12.0 > col * T && it.X < col * T + T)
			{
				it.Vy = -200.0;
				it.Vx = it.X + 6.0 < bx ? -Math.Abs(it.Vx) : Math.Abs(it.Vx);
			}
		}
	}

	private void AddBump(int col, int row)
	{
		int slot = 0;
		double oldest = -1.0;
		for (int i = 0; i < _bumps.Length; i++)
		{
			if (_bumps[i].Time <= 0.0)
			{
				slot = i;
				break;
			}
			if (_bumps[i].Time > oldest)
			{
				oldest = _bumps[i].Time;
				slot = i;
			}
		}
		_bumps[slot] = new PlatformerBump
		{
			Col = col,
			Row = row,
			Time = Step * 0.5
		};
		Emit(PlatformerEventKind.BlockBump, col * T + T / 2.0, row * T + T / 2.0);
	}

	// Смещение блока вверх при «подпрыгивании», в пикселях (0 — покой).
	public double BumpOffset(int col, int row)
	{
		for (int i = 0; i < _bumps.Length; i++)
		{
			ref PlatformerBump b = ref _bumps[i];
			if (b.Time > 0.0 && b.Col == col && b.Row == row)
			{
				return -6.0 * Math.Sin(Math.PI * b.Time / BumpTime);
			}
		}
		return 0.0;
	}

	// ── Предметы, леденцы, платформы, огненные палки ─────────────────────

	private void SpawnItem(PlatformerItemKind kind, int col, int row)
	{
		int slot = 0;
		for (int i = 0; i < _items.Length; i++)
		{
			if (!_items[i].Alive)
			{
				slot = i;
				break;
			}
		}
		_items[slot] = new PlatformerItem
		{
			Kind = kind,
			X = col * T + 2.0,
			Y = row * T,
			BaseY = row * T,
			Alive = true
		};
	}

	private void UpdateItems()
	{
		for (int i = 0; i < _items.Length; i++)
		{
			ref PlatformerItem it = ref _items[i];
			if (!it.Alive)
			{
				continue;
			}
			if (it.Emerge < 1.0)
			{
				it.Emerge = Math.Min(1.0, it.Emerge + Step / 0.6);
				it.Y = it.BaseY - 12.0 * it.Emerge;
				if (it.Emerge >= 1.0 && it.Kind == PlatformerItemKind.SugarStar)
				{
					it.Vx = 48.0;
				}
			}
			else if (it.Kind == PlatformerItemKind.SugarStar || it.Vy != 0.0)
			{
				it.Vy = Math.Min(it.Vy + GravityFall * Step, TerminalVelocity);
				if (MoveX(ref it.X, it.Y, 12.0, 12.0, it.Vx * Step))
				{
					it.Vx = -it.Vx;
				}
				if (it.Vy < 0.0)
				{
					it.Y += it.Vy * Step;
				}
				else if (MoveDown(ref it.Y, it.X, 12.0, 12.0, it.Vy * Step))
				{
					it.Vy = 0.0;
				}
				if (it.Y > _h * T + 16.0)
				{
					it.Alive = false;
					continue;
				}
			}
			if (it.Emerge > 0.5 && Overlap(_mx, _my, _mw, _mh, it.X, it.Y, 12.0, 12.0))
			{
				it.Alive = false;
				Collect(it.Kind, it.X + 6.0, it.Y + 6.0);
			}
		}
	}

	private void Collect(PlatformerItemKind kind, double x, double y)
	{
		Score += 1000;
		PlatformerPower before = Power;
		if (kind == PlatformerItemKind.FireCandy)
		{
			Power = PlatformerPower.Fire;
		}
		else if (Power == PlatformerPower.Small)
		{
			Power = PlatformerPower.Big;
		}
		if (before == PlatformerPower.Small && Power != PlatformerPower.Small)
		{
			ApplySize();
			if (!CanStand())
			{
				Ducking = true;
				ApplySize();
			}
		}
		Emit(PlatformerEventKind.PowerUp, x, y, (int)Power);
	}

	private void UpdateCandies()
	{
		for (int i = 0; i < _candies.Length; i++)
		{
			ref PlatformerCandy c = ref _candies[i];
			if (!c.Alive)
			{
				continue;
			}
			c.Life -= Step;
			c.Vy = Math.Min(c.Vy + 900.0 * Step, 300.0);
			bool pop = MoveX(ref c.X, c.Y, 6.0, 6.0, c.Vx * Step);
			if (c.Vy < 0.0)
			{
				c.Y += c.Vy * Step;
			}
			else if (MoveDown(ref c.Y, c.X, 6.0, 6.0, c.Vy * Step))
			{
				c.Vy = -CandyBounce;
			}
			if (c.Life <= 0.0 || c.Y > _h * T || c.X < CameraX - 24.0 || c.X > CameraX + ViewWidth + 24.0)
			{
				c.Alive = false;
				continue;
			}
			for (int j = 0; j < _enemyCount && !pop; j++)
			{
				ref PlatformerEnemy e = ref _enemies[j];
				if (e.Active && e.State is PlatformerEnemyState.Walk or PlatformerEnemyState.Shell or PlatformerEnemyState.ShellMoving && Overlap(c.X, c.Y, 6.0, 6.0, e.X, e.Y, e.W, e.H))
				{
					pop = true;
					if (e.Kind != PlatformerEnemyKind.Boss)
					{
						Knock(ref e, 200);
					}
				}
			}
			if (pop)
			{
				c.Alive = false;
				Emit(PlatformerEventKind.CandyPop, c.X + 3.0, c.Y + 3.0);
			}
		}
	}

	private void UpdatePlatforms()
	{
		for (int i = 0; i < _platformCount; i++)
		{
			ref PlatformerPlatform p = ref _platforms[i];
			double s = 0.5 - 0.5 * Math.Cos((LevelTime / PlatformPeriod + p.Offset) * Math.PI * 2.0);
			double d = s * PlatformerLevel.PlatformTravel * T;
			double nx = p.Vertical ? p.X0 : p.X0 + d;
			double ny = p.Vertical ? p.Y0 - d : p.Y0;
			p.Dx = nx - p.X;
			p.Dy = ny - p.Y;
			p.X = nx;
			p.Y = ny;
		}
	}

	private void UpdateFireBars()
	{
		for (int i = 0; i < _barCount; i++)
		{
			ref PlatformerFireBar b = ref _bars[i];
			b.Angle += b.Speed * Step;
			double cos = Math.Cos(b.Angle);
			double sin = Math.Sin(b.Angle);
			for (int k = 1; k < b.Count; k++)
			{
				double fx = b.Cx + cos * k * 8.0;
				double fy = b.Cy + sin * k * 8.0;
				if (CircleHits(fx, fy, 3.5, _mx + 2.0, _my + 2.0, _mw - 4.0, _mh - 4.0))
				{
					Hurt();
					return;
				}
			}
		}
	}

	// ── Враги ─────────────────────────────────────────────────────────────

	private void UpdateEnemies()
	{
		double activateX = CameraX + ViewWidth + 24.0;
		for (int i = 0; i < _enemyCount; i++)
		{
			ref PlatformerEnemy e = ref _enemies[i];
			if (e.State == PlatformerEnemyState.Gone)
			{
				continue;
			}
			if (!e.Active)
			{
				if (e.X > activateX)
				{
					continue;
				}
				e.Active = true;
			}
			if (e.Flash > 0.0)
			{
				e.Flash -= Step;
			}
			switch (e.State)
			{
			case PlatformerEnemyState.Knocked:
				e.Vy = Math.Min(e.Vy + GravityFall * Step, 500.0);
				e.X += e.Vx * Step;
				e.Y += e.Vy * Step;
				if (e.Y > _h * T + 32.0)
				{
					e.State = PlatformerEnemyState.Gone;
				}
				continue;
			case PlatformerEnemyState.Squished:
				e.Timer -= Step;
				if (e.Timer <= 0.0)
				{
					e.State = PlatformerEnemyState.Gone;
				}
				continue;
			}
			MoveEnemy(ref e);
			if (e.State == PlatformerEnemyState.Gone)
			{
				continue;
			}
			if (e.State == PlatformerEnemyState.ShellMoving)
			{
				ShellSweep(i);
			}
			if (Phase == PlatformerPhase.Playing)
			{
				TouchEnemy(ref e);
				if (Phase != PlatformerPhase.Playing)
				{
					return;
				}
			}
		}
	}

	private void MoveEnemy(ref PlatformerEnemy e)
	{
		if (e.Kind == PlatformerEnemyKind.Bat)
		{
			e.Phase += Step * 2.6;
			if (MoveX(ref e.X, e.Y, e.W, e.H, e.Vx * Step))
			{
				e.Vx = -e.Vx;
			}
			e.Y = e.BaseY + 18.0 * Math.Sin(e.Phase);
			return;
		}
		if (e.State == PlatformerEnemyState.Shell)
		{
			e.Timer -= Step;
			if (e.Timer <= 0.0)
			{
				SetEnemyState(ref e, PlatformerEnemyState.Walk);
				e.Vx = (MetaX < e.X + e.W / 2.0 ? -1.0 : 1.0) * EnemySpeed(e.Kind);
			}
		}
		if (e.State == PlatformerEnemyState.ShellMoving && e.Timer > 0.0)
		{
			e.Timer -= Step;
		}
		if (e.Kind == PlatformerEnemyKind.Boss)
		{
			double dir = MetaX < e.X + e.W / 2.0 ? -1.0 : 1.0;
			if (e.OnGround)
			{
				e.Vx = dir * (EnemySpeed(e.Kind) + (3 - e.Hp) * 14.0);
				e.Timer -= Step;
				if (e.Timer <= 0.0)
				{
					e.Vy = -300.0;
					e.Timer = 1.8 - (3 - e.Hp) * 0.3;
					e.OnGround = false;
				}
			}
		}
		e.Vy = Math.Min(e.Vy + GravityFall * Step, TerminalVelocity);
		if (MoveX(ref e.X, e.Y, e.W, e.H, e.Vx * Step))
		{
			e.Vx = -e.Vx;
		}
		bool landed;
		if (e.Vy < 0.0)
		{
			e.Y += e.Vy * Step;
			landed = false;
		}
		else
		{
			landed = MoveDown(ref e.Y, e.X, e.W, e.H, e.Vy * Step);
		}
		e.OnGround = landed;
		if (landed)
		{
			e.Vy = 0.0;
			if (e.State == PlatformerEnemyState.Walk && e.Kind is PlatformerEnemyKind.Slime or PlatformerEnemyKind.Spiky)
			{
				double ax = e.Vx > 0.0 ? e.X + e.W + 1.0 : e.X - 1.0;
				PlatformerTile below = TileAt(Floor(ax / T), Floor((e.Y + e.H + 1.0) / T));
				if (!PlatformerLevel.IsSolid(below) && below != PlatformerTile.OneWay)
				{
					e.Vx = -e.Vx;
				}
			}
		}
		if (e.Y > _h * T + 32.0)
		{
			e.State = PlatformerEnemyState.Gone;
		}
	}

	private void ShellSweep(int self)
	{
		ref PlatformerEnemy s = ref _enemies[self];
		for (int j = 0; j < _enemyCount; j++)
		{
			if (j == self)
			{
				continue;
			}
			ref PlatformerEnemy o = ref _enemies[j];
			if (!o.Active || o.State is PlatformerEnemyState.Knocked or PlatformerEnemyState.Squished or PlatformerEnemyState.Gone || !Overlap(s.X, s.Y, s.W, s.H, o.X, o.Y, o.W, o.H))
			{
				continue;
			}
			if (o.Kind == PlatformerEnemyKind.Boss)
			{
				Knock(ref s, 0);
				return;
			}
			s.Chain++;
			Knock(ref o, ComboScores[Math.Min(s.Chain + 3, ComboScores.Length - 1)]);
		}
	}

	private void TouchEnemy(ref PlatformerEnemy e)
	{
		if (!Overlap(_mx + 1.0, _my + 1.0, _mw - 2.0, _mh - 2.0, e.X, e.Y, e.W, e.H))
		{
			return;
		}
		bool falling = MetaVy > 0.0 && MetaBottom - e.Y < (e.Kind == PlatformerEnemyKind.Boss ? 12.0 : 8.0);
		if (e.State == PlatformerEnemyState.Shell)
		{
			Kick(ref e);
			if (falling)
			{
				Bounce(e.Y);
			}
			return;
		}
		if (e.Kind == PlatformerEnemyKind.Spiky)
		{
			Hurt();
			return;
		}
		if (falling)
		{
			Stomp(ref e);
			return;
		}
		if (e.State == PlatformerEnemyState.ShellMoving && e.Timer > 0.0)
		{
			return;
		}
		if (e.Kind == PlatformerEnemyKind.Boss && e.Flash > 0.0)
		{
			return;
		}
		Hurt();
	}

	private void Bounce(double enemyTop)
	{
		bool held = Has(_input, PlatformerInput.Jump);
		_my = enemyTop - _mh;
		MetaVy = -(held ? StompBounceHigh : StompBounce);
		_jumping = held;
		OnGround = false;
		_coyote = 0.0;
	}

	private void Stomp(ref PlatformerEnemy e)
	{
		double ex = e.X + e.W / 2.0;
		double ey = e.Y;
		Bounce(e.Y);
		if (e.Kind == PlatformerEnemyKind.Boss)
		{
			if (e.Flash > 0.0)
			{
				return;
			}
			e.Hp--;
			e.Flash = 1.0;
			if (e.Hp <= 0)
			{
				e.State = PlatformerEnemyState.Knocked;
				e.Vy = -220.0;
				e.Vx = 0.0;
				BossDefeated = true;
				for (int i = 0; i < _tiles.Length; i++)
				{
					if (_tiles[i] == PlatformerTile.Door)
					{
						_tiles[i] = PlatformerTile.Empty;
					}
				}
				Score += 5000;
				Emit(PlatformerEventKind.BossDefeated, ex, ey, 5000);
			}
			else
			{
				Score += 1000;
				Emit(PlatformerEventKind.BossHit, ex, ey, 1000);
			}
			return;
		}
		_combo++;
		bool life = _combo > ComboScores.Length;
		int points = life ? 0 : ComboScores[_combo - 1];
		switch (e.Kind)
		{
		case PlatformerEnemyKind.Snail:
			SetEnemyState(ref e, PlatformerEnemyState.Shell);
			e.Vx = 0.0;
			e.Timer = ShellRevive;
			e.Chain = 0;
			break;
		case PlatformerEnemyKind.Bat:
			e.State = PlatformerEnemyState.Knocked;
			e.Vy = 0.0;
			e.Vx = 0.0;
			break;
		default:
			e.State = PlatformerEnemyState.Squished;
			e.Timer = 0.5;
			break;
		}
		Score += points;
		Emit(PlatformerEventKind.Stomp, ex, ey, points);
		if (life)
		{
			Lives++;
			Emit(PlatformerEventKind.ExtraLife, ex, ey, Lives);
		}
	}

	private void Kick(ref PlatformerEnemy e)
	{
		double dir = MetaX < e.X + e.W / 2.0 ? 1.0 : -1.0;
		e.State = PlatformerEnemyState.ShellMoving;
		e.Vx = dir * ShellSpeed;
		e.Timer = 0.25;
		e.Chain = 0;
		Score += 400;
		Emit(PlatformerEventKind.Kick, e.X + e.W / 2.0, e.Y, 400);
	}

	private void Knock(ref PlatformerEnemy e, int points)
	{
		e.State = PlatformerEnemyState.Knocked;
		e.Vy = -180.0;
		e.Vx = e.Vx >= 0.0 ? 30.0 : -30.0;
		Score += points;
		Emit(PlatformerEventKind.Knock, e.X + e.W / 2.0, e.Y, points);
	}

	private static void SetEnemyState(ref PlatformerEnemy e, PlatformerEnemyState state)
	{
		e.State = state;
		(double w, double h) = EnemySize(e.Kind, state);
		double bottom = e.Y + e.H;
		double cx = e.X + e.W / 2.0;
		e.W = w;
		e.H = h;
		e.X = cx - w / 2.0;
		e.Y = bottom - h;
	}

	// ── Камера ────────────────────────────────────────────────────────────

	private void UpdateCamera()
	{
		if (Phase is PlatformerPhase.Playing or PlatformerPhase.Clearing)
		{
			_look += (Facing * LookAhead - _look) * Math.Min(1.0, 2.5 * Step);
			double focus = MetaX + _look;
			double lo = CameraX + ViewWidth * 0.40;
			double hi = CameraX + ViewWidth * 0.52;
			if (focus > hi)
			{
				CameraX += focus - hi;
			}
			else if (focus < lo)
			{
				CameraX -= lo - focus;
			}
			double fy = MetaBottom;
			double top = CameraY + ViewHeight * 0.3;
			double bottom = CameraY + ViewHeight * 0.75;
			if (fy < top)
			{
				CameraY -= top - fy;
			}
			else if (fy > bottom)
			{
				CameraY += fy - bottom;
			}
		}
		ClampCamera();
	}

	private void SnapCamera()
	{
		_look = Facing * LookAhead;
		CameraX = MetaX + _look - ViewWidth * 0.46;
		CameraY = MetaBottom - ViewHeight * 0.6;
		ClampCamera();
	}

	private void ClampCamera()
	{
		double lw = _w * T;
		double lh = _h * T;
		CameraX = lw <= ViewWidth ? (lw - ViewWidth) / 2.0 : Math.Clamp(CameraX, 0.0, lw - ViewWidth);
		CameraY = lh <= ViewHeight ? lh - ViewHeight : Math.Clamp(CameraY, 0.0, lh - ViewHeight);
	}

	// ── Столкновения с картой ─────────────────────────────────────────────

	// true — упёрлись в стену. Шаг дробится по 8 px, так что сквозь клетку не проскочить.
	private bool MoveX(ref double x, double y, double w, double h, double dx)
	{
		int r0 = Floor(y / T);
		int r1 = Floor((y + h - Eps) / T);
		while (dx != 0.0)
		{
			double d = Math.Clamp(dx, -8.0, 8.0);
			dx -= d;
			double nx = x + d;
			if (d > 0.0)
			{
				int c = Floor((nx + w - Eps) / T);
				if (c * T >= x + w - Eps && ColumnSolid(c, r0, r1))
				{
					x = c * T - w;
					return true;
				}
			}
			else
			{
				int c = Floor(nx / T);
				if ((c + 1) * T <= x + Eps && ColumnSolid(c, r0, r1))
				{
					x = (c + 1) * T;
					return true;
				}
			}
			x = nx;
		}
		return false;
	}

	// true — встали на твёрдую клетку или платформу «сквозь».
	private bool MoveDown(ref double y, double x, double w, double h, double dy)
	{
		int c0 = Floor(x / T);
		int c1 = Floor((x + w - Eps) / T);
		while (dy > 0.0)
		{
			double d = Math.Min(dy, 8.0);
			dy -= d;
			double bottom = y + h;
			double nb = bottom + d;
			int r = Floor(nb / T);
			if (r * T >= bottom - Eps)
			{
				for (int c = c0; c <= c1; c++)
				{
					PlatformerTile t = TileAt(c, r);
					if (PlatformerLevel.IsSolid(t) || t == PlatformerTile.OneWay)
					{
						y = r * T - h;
						return true;
					}
				}
			}
			y += d;
		}
		return false;
	}

	private bool ColumnSolid(int c, int r0, int r1)
	{
		for (int r = r0; r <= r1; r++)
		{
			if (PlatformerLevel.IsSolid(TileAt(c, r)))
			{
				return true;
			}
		}
		return false;
	}

	private static bool Overlap(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh)
	{
		return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
	}

	private static bool CircleHits(double cx, double cy, double r, double x, double y, double w, double h)
	{
		double nx = Math.Clamp(cx, x, x + w);
		double ny = Math.Clamp(cy, y, y + h);
		double dx = cx - nx;
		double dy = cy - ny;
		return dx * dx + dy * dy < r * r;
	}

	private static double Approach(double v, double target, double amount)
	{
		if (v < target)
		{
			return Math.Min(v + amount, target);
		}
		return Math.Max(v - amount, target);
	}

	private static int Floor(double v)
	{
		return (int)Math.Floor(v);
	}
}
