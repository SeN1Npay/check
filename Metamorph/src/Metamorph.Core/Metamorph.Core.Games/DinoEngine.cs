using System;

namespace Metamorph.Core.Games;

public enum DinoObstacleKind
{
	CrystalSmall,
	CrystalLarge,
	Pumpkin,
	Block,
	Bat,
	Bird
}

public enum DinoEventKind
{
	Jump,
	Land,
	NearMiss,
	Milestone,
	GameOver
}

public readonly record struct DinoEvent(DinoEventKind Kind, int Value = 0);

public struct DinoObstacle
{
	public DinoObstacleKind Kind;

	// Левый нижний угол в мировых единицах (y вверх, земля — 0); X относительно Меты.
	public double X;

	public double Y;

	public double W;

	public double H;

	public int Count;

	public int Variant;

	public double MinClearance;

	public bool Passed;

	public readonly bool Flying => Kind >= DinoObstacleKind.Bat;
}

// «Мета-раннер»: бег с прыжками в духе динозаврика из Chrome. Фиксированный шаг 120 Гц, детерминированный ГСЧ.
public sealed class DinoEngine
{
	public const double Step = 1.0 / 120.0;

	public const double Gravity = 2600.0;

	public const double JumpVelocity = 820.0;

	// Отпустил прыжок на подъёме — гравитация сильнее (низкий прыжок).
	public const double JumpCutMultiplier = 2.6;

	public const double FastFallMultiplier = 3.2;

	public const double CoyoteTime = 0.08;

	public const double JumpBufferTime = 0.12;

	public const double StartSpeed = 380.0;

	public const double MaxSpeed = 820.0;

	public const double Acceleration = 5.0;

	public const double SpawnX = 1100.0;

	public const double ScorePerUnit = 0.025;

	public const double StandWidth = 30.0;

	public const double StandHeight = 74.0;

	public const double DuckWidth = 46.0;

	public const double DuckHeight = 40.0;

	public const double NearMissDistance = 14.0;

	public const double FlyingFromScore = 250.0;

	private const int Capacity = 24;

	private readonly DinoObstacle[] _obstacles = new DinoObstacle[Capacity];

	private int _count;

	private GameRandom _rnd;

	private double _acc;

	private bool _jumpHeld;

	private bool _duckHeld;

	private double _jumpBuffer;

	private double _coyote;

	private bool _jumped;

	private double _gap;

	private int _lastMilestone;

	public bool AutoSpawn { get; set; } = true;

	public double PlayerY { get; private set; }

	public double PlayerVy { get; private set; }

	public bool OnGround { get; private set; } = true;

	public bool Ducking => _duckHeld && OnGround;

	public bool DuckHeld => _duckHeld;

	public double Speed { get; private set; }

	public double Distance { get; private set; }

	public double Time { get; private set; }

	public int Score => (int)(Distance * ScorePerUnit);

	public bool IsGameOver { get; private set; }

	public double AirTime { get; private set; }

	public ReadOnlySpan<DinoObstacle> Obstacles => new ReadOnlySpan<DinoObstacle>(_obstacles, 0, _count);

	public event Action<DinoEvent>? Event;

	public DinoEngine(ulong seed)
	{
		_rnd = new GameRandom(seed);
		Reset(seed);
	}

	public void Reset(ulong seed)
	{
		_rnd = new GameRandom(seed);
		_count = 0;
		_acc = 0.0;
		_jumpHeld = _duckHeld = false;
		_jumpBuffer = 0.0;
		_coyote = CoyoteTime;
		_jumped = false;
		PlayerY = 0.0;
		PlayerVy = 0.0;
		OnGround = true;
		Speed = StartSpeed;
		Distance = 0.0;
		Time = 0.0;
		AirTime = 0.0;
		IsGameOver = false;
		_lastMilestone = 0;
		_gap = 500.0;
	}

	public (double Left, double Bottom, double Right, double Top) PlayerBox
	{
		get
		{
			if (Ducking)
			{
				return (-DuckWidth / 2.0 + 4.0, PlayerY, DuckWidth / 2.0 + 4.0, PlayerY + DuckHeight);
			}
			double h = _duckHeld ? StandHeight * 0.8 : StandHeight;
			return (-StandWidth / 2.0, PlayerY, StandWidth / 2.0, PlayerY + h);
		}
	}

	public void SetJump(bool down)
	{
		if (down && !_jumpHeld)
		{
			_jumpBuffer = JumpBufferTime;
		}
		_jumpHeld = down;
	}

	public void SetDuck(bool down)
	{
		_duckHeld = down;
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

	// Ручное добавление препятствия (тесты, отладка).
	public void Spawn(DinoObstacleKind kind, double x, double y = 0.0, int count = 1)
	{
		if (_count >= Capacity)
		{
			return;
		}
		(double w, double h) = Size(kind);
		count = Math.Clamp(count, 1, 3);
		_obstacles[_count++] = new DinoObstacle
		{
			Kind = kind,
			X = x,
			Y = y,
			W = kind <= DinoObstacleKind.CrystalLarge ? w * count + 6.0 * (count - 1) : w,
			H = h,
			Count = count,
			Variant = _rnd.Next(1000),
			MinClearance = double.MaxValue
		};
	}

	public static (double W, double H) Size(DinoObstacleKind kind)
	{
		return kind switch
		{
			DinoObstacleKind.CrystalSmall => (20.0, 38.0),
			DinoObstacleKind.CrystalLarge => (28.0, 56.0),
			DinoObstacleKind.Pumpkin => (40.0, 34.0),
			DinoObstacleKind.Block => (34.0, 46.0),
			DinoObstacleKind.Bat => (40.0, 24.0),
			_ => (44.0, 26.0)
		};
	}

	private void Tick()
	{
		Time += Step;
		Speed = Math.Min(MaxSpeed, Speed + Acceleration * Step);
		double dx = Speed * Step;
		Distance += dx;
		Physics();
		Scroll(dx);
		if (AutoSpawn)
		{
			_gap -= dx;
			if (_gap <= 0.0)
			{
				SpawnRandom();
			}
		}
		Collide();
		int milestone = Score / 100;
		if (milestone > _lastMilestone && !IsGameOver)
		{
			_lastMilestone = milestone;
			Event?.Invoke(new DinoEvent(DinoEventKind.Milestone, milestone * 100));
		}
	}

	private void Physics()
	{
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
		bool canJump = OnGround || (_coyote > 0.0 && !_jumped);
		if (_jumpBuffer > 0.0 && canJump)
		{
			PlayerVy = JumpVelocity;
			OnGround = false;
			_jumped = true;
			_jumpBuffer = 0.0;
			_coyote = 0.0;
			AirTime = 0.0;
			Event?.Invoke(new DinoEvent(DinoEventKind.Jump));
		}
		if (OnGround)
		{
			return;
		}
		AirTime += Step;
		double g = Gravity;
		if (PlayerVy > 0.0 && !_jumpHeld)
		{
			g *= JumpCutMultiplier;
		}
		if (_duckHeld)
		{
			g *= FastFallMultiplier;
		}
		PlayerVy -= g * Step;
		PlayerY += PlayerVy * Step;
		if (PlayerY <= 0.0)
		{
			int impact = (int)Math.Abs(PlayerVy);
			PlayerY = 0.0;
			PlayerVy = 0.0;
			OnGround = true;
			_jumped = false;
			Event?.Invoke(new DinoEvent(DinoEventKind.Land, impact));
		}
	}

	private void Scroll(double dx)
	{
		int write = 0;
		for (int i = 0; i < _count; i++)
		{
			ref DinoObstacle o = ref _obstacles[i];
			o.X -= dx * (o.Kind == DinoObstacleKind.Bird ? 1.08 : 1.0);
			if (o.X + o.W < -400.0)
			{
				continue;
			}
			if (write != i)
			{
				_obstacles[write] = o;
			}
			write++;
		}
		_count = write;
	}

	private void Collide()
	{
		var (pl, pb, pr, pt) = PlayerBox;
		for (int i = 0; i < _count; i++)
		{
			ref DinoObstacle o = ref _obstacles[i];
			// Прощающие хитбоксы: края препятствий «подрезаны».
			double inset = o.Flying ? 6.0 : 4.0;
			double ol = o.X + inset;
			double or = o.X + o.W - inset;
			double ob = o.Y + (o.Flying ? 4.0 : 0.0);
			double ot = o.Y + o.H - (o.Flying ? 5.0 : 4.0);
			bool overlapX = pr > ol && pl < or;
			if (overlapX)
			{
				bool overlapY = pt > ob && pb < ot;
				if (overlapY)
				{
					IsGameOver = true;
					Event?.Invoke(new DinoEvent(DinoEventKind.GameOver, Score));
					return;
				}
				double clearance = pb >= ot ? pb - ot : ob - pt;
				if (clearance < o.MinClearance)
				{
					o.MinClearance = clearance;
				}
			}
			else if (!o.Passed && or < pl)
			{
				o.Passed = true;
				if (o.MinClearance < NearMissDistance)
				{
					Event?.Invoke(new DinoEvent(DinoEventKind.NearMiss, (int)o.Kind));
				}
			}
		}
	}

	private void SpawnRandom()
	{
		double t = (Speed - StartSpeed) / (MaxSpeed - StartSpeed);
		bool flying = Score >= FlyingFromScore && _rnd.NextDouble() < 0.28;
		if (flying)
		{
			DinoObstacleKind kind = _rnd.NextDouble() < 0.5 ? DinoObstacleKind.Bat : DinoObstacleKind.Bird;
			// Низко — прыгать, средне — пригнуться, высоко — пролетит над головой.
			double y = _rnd.Next(3) switch
			{
				0 => 12.0,
				1 => 50.0,
				_ => 92.0
			};
			Spawn(kind, SpawnX, y);
		}
		else
		{
			double r = _rnd.NextDouble();
			if (r < 0.4)
			{
				Spawn(DinoObstacleKind.CrystalSmall, SpawnX, 0.0, 1 + _rnd.Next(t > 0.3 ? 3 : 2));
			}
			else if (r < 0.62)
			{
				Spawn(DinoObstacleKind.CrystalLarge, SpawnX, 0.0, 1 + _rnd.Next(t > 0.5 ? 3 : (t > 0.15 ? 2 : 1)));
			}
			else if (r < 0.82)
			{
				Spawn(DinoObstacleKind.Pumpkin, SpawnX);
			}
			else
			{
				Spawn(DinoObstacleKind.Block, SpawnX);
			}
		}
		double width = _count > 0 ? _obstacles[_count - 1].W : 0.0;
		_gap = width + Speed * _rnd.Range(0.85, 1.55) + 140.0;
	}
}
