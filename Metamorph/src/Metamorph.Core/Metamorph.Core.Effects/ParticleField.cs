using System;
using Metamorph.Core.Models;

namespace Metamorph.Core.Effects;

public struct Particle
{
	public float X;

	public float Y;

	public float Vx;

	public float Vy;

	// Снег/звёзды/светлячки — радиус; лепестки/листья/пузыри — размер; дождь — длина штриха.
	public float Size;

	// Градусы, для лепестков и листьев.
	public float Angle;

	public float Spin;

	// Фаза покачивания/мерцания/переворота.
	public float Phase;

	public float PhaseSpeed;

	// 0..1, итоговая непрозрачность с учётом мерцания.
	public float Alpha;

	public float BaseAlpha;

	// 0 — дальний план, 1 — ближний.
	public float Depth;

	// Индекс цвета в палитре вида частиц.
	public byte Variant;
}

public struct ShootingStar
{
	public bool Active;

	public float X;

	public float Y;

	public float Vx;

	public float Vy;

	public float Life;

	public float Duration;

	// 0..1: появление → затухание.
	public float Alpha => Duration <= 0f ? 0f : MathF.Sin(MathF.PI * Math.Clamp(Life / Duration, 0f, 1f));
}

// Детерминированная симуляция частиц: фиксированный пул, шаг без выделений памяти.
public sealed class ParticleField
{
	public const int MaxParticles = 400;

	public const float RepelRadius = 110f;

	private readonly Particle[] _items = new Particle[MaxParticles];

	private uint _rng;

	private float _shootingTimer;

	private ShootingStar _shooting;

	public ParticleKind Kind { get; private set; }

	public float Width { get; private set; }

	public float Height { get; private set; }

	public int Count { get; private set; }

	public float Speed { get; private set; } = 1f;

	public double Time { get; private set; }

	public Particle[] Items => _items;

	public ref readonly ShootingStar Shooting => ref _shooting;

	// Сколько цветов использует вид (Variant < VariantCount).
	public static int VariantCount(ParticleKind kind)
	{
		return kind switch
		{
			ParticleKind.Sakura => 3,
			ParticleKind.Leaves => 3,
			ParticleKind.Fireflies => 2,
			ParticleKind.Stars => 2,
			_ => 1,
		};
	}

	public ParticleField(int seed = 20240917)
	{
		_rng = (uint)(seed == 0 ? 1 : seed);
	}

	// Частиц на миллион DIP² при плотности 1.
	public static double BasePerMegapixel(ParticleKind kind)
	{
		return kind switch
		{
			ParticleKind.Snow => 90.0,
			ParticleKind.Sakura => 28.0,
			ParticleKind.Stars => 110.0,
			ParticleKind.Fireflies => 22.0,
			ParticleKind.Leaves => 16.0,
			ParticleKind.Rain => 150.0,
			ParticleKind.Bubbles => 24.0,
			_ => 50.0,
		};
	}

	public static int CountFor(ParticleKind kind, double width, double height, double density)
	{
		if (!(width > 0.0) || !(height > 0.0))
		{
			return 0;
		}
		double d = Math.Clamp(double.IsFinite(density) ? density : 1.0, 0.1, 3.0);
		double n = width * height / 1000000.0 * BasePerMegapixel(kind) * d;
		return (int)Math.Clamp(Math.Round(n), 1.0, MaxParticles);
	}

	public void Configure(ParticleKind kind, float width, float height, int count, double speed)
	{
		Kind = kind;
		Width = Math.Max(1f, width);
		Height = Math.Max(1f, height);
		Count = Math.Clamp(count, 0, MaxParticles);
		Speed = (float)Math.Clamp(double.IsFinite(speed) ? speed : 1.0, 0.1, 4.0);
		Time = 0.0;
		_shooting = default;
		_shootingTimer = Range(4f, 10f);
		for (int i = 0; i < Count; i++)
		{
			Spawn(ref _items[i], initial: true);
		}
	}

	// cursorX/cursorY в координатах поля; NaN — курсора нет.
	public void Step(float dt, float cursorX = float.NaN, float cursorY = float.NaN, bool repel = false)
	{
		if (!(dt > 0f))
		{
			return;
		}
		dt = Math.Min(dt, 0.1f);
		Time += dt;
		float sdt = dt * Speed;
		bool push = repel && Kind != ParticleKind.Stars && float.IsFinite(cursorX) && float.IsFinite(cursorY);
		Particle[] items = _items;
		for (int i = 0; i < Count; i++)
		{
			ref Particle p = ref items[i];
			switch (Kind)
			{
			case ParticleKind.Snow:
				p.Phase += p.PhaseSpeed * sdt;
				p.X += (p.Vx + MathF.Sin(p.Phase) * (8f + 14f * p.Depth)) * sdt;
				p.Y += p.Vy * sdt;
				break;
			case ParticleKind.Sakura:
				p.Phase += p.PhaseSpeed * sdt;
				p.Angle += p.Spin * sdt;
				p.X += (p.Vx + MathF.Sin(p.Phase * 0.7f) * 22f) * sdt;
				p.Y += (p.Vy + MathF.Cos(p.Phase) * 8f) * sdt;
				break;
			case ParticleKind.Leaves:
				p.Phase += p.PhaseSpeed * sdt;
				p.Angle += p.Spin * sdt;
				p.X += (p.Vx + MathF.Sin(p.Phase * 0.5f) * 45f) * sdt;
				p.Y += (p.Vy + MathF.Sin(p.Phase) * 12f) * sdt;
				break;
			case ParticleKind.Rain:
				p.X += p.Vx * sdt;
				p.Y += p.Vy * sdt;
				break;
			case ParticleKind.Bubbles:
				p.Phase += p.PhaseSpeed * sdt;
				p.X += MathF.Sin(p.Phase) * 18f * sdt;
				p.Y += p.Vy * sdt;
				break;
			case ParticleKind.Stars:
				p.Phase += p.PhaseSpeed * dt;
				p.Alpha = p.BaseAlpha * (0.55f + 0.45f * MathF.Sin(p.Phase));
				break;
			case ParticleKind.Fireflies:
			{
				p.Phase += p.PhaseSpeed * dt;
				p.Angle += (Next01() - 0.5f) * 240f * dt;
				float rad = p.Angle * (MathF.PI / 180f);
				float v = p.Spin;
				p.X += MathF.Cos(rad) * v * sdt;
				p.Y += MathF.Sin(rad) * v * sdt;
				float pulse = 0.5f + 0.5f * MathF.Sin(p.Phase);
				p.Alpha = p.BaseAlpha * (0.25f + 0.75f * pulse * pulse);
				break;
			}
			}
			if (push)
			{
				float dx = p.X - cursorX;
				float dy = p.Y - cursorY;
				float d2 = dx * dx + dy * dy;
				if (d2 < RepelRadius * RepelRadius && d2 > 0.0001f)
				{
					float d = MathF.Sqrt(d2);
					float k = (1f - d / RepelRadius) * 520f * dt;
					p.X += dx / d * k;
					p.Y += dy / d * k;
				}
			}
			Recycle(ref p);
		}
		if (Kind == ParticleKind.Stars)
		{
			StepShooting(dt);
		}
	}

	private void Recycle(ref Particle p)
	{
		float margin = p.Size * 2f + 20f;
		switch (Kind)
		{
		case ParticleKind.Bubbles:
			if (p.Y < -margin)
			{
				Spawn(ref p, initial: false);
			}
			else
			{
				WrapX(ref p, margin);
			}
			break;
		case ParticleKind.Fireflies:
			// Светлячки летают по кругу экрана — заворачиваем со всех сторон.
			if (p.X < -margin)
			{
				p.X += Width + 2f * margin;
			}
			else if (p.X > Width + margin)
			{
				p.X -= Width + 2f * margin;
			}
			if (p.Y < -margin)
			{
				p.Y += Height + 2f * margin;
			}
			else if (p.Y > Height + margin)
			{
				p.Y -= Height + 2f * margin;
			}
			break;
		case ParticleKind.Stars:
			break;
		default:
			if (p.Y > Height + margin)
			{
				Spawn(ref p, initial: false);
			}
			else
			{
				WrapX(ref p, margin);
			}
			break;
		}
	}

	private void WrapX(ref Particle p, float margin)
	{
		if (p.X < -margin)
		{
			p.X += Width + 2f * margin;
		}
		else if (p.X > Width + margin)
		{
			p.X -= Width + 2f * margin;
		}
	}

	private void StepShooting(float dt)
	{
		if (_shooting.Active)
		{
			_shooting.Life += dt;
			_shooting.X += _shooting.Vx * dt;
			_shooting.Y += _shooting.Vy * dt;
			if (_shooting.Life >= _shooting.Duration)
			{
				_shooting.Active = false;
				_shootingTimer = Range(6f, 18f);
			}
			return;
		}
		_shootingTimer -= dt;
		if (_shootingTimer <= 0f)
		{
			float dir = Next01() < 0.5f ? -1f : 1f;
			float speed = Range(700f, 1100f);
			_shooting = new ShootingStar
			{
				Active = true,
				X = Range(Width * 0.15f, Width * 0.85f),
				Y = Range(0f, Height * 0.35f),
				Vx = dir * speed * 0.87f,
				Vy = speed * 0.5f,
				Life = 0f,
				Duration = Range(0.6f, 1.1f)
			};
		}
	}

	private void Spawn(ref Particle p, bool initial)
	{
		float d = Next01();
		p.Depth = d;
		p.Angle = Range(0f, 360f);
		p.Phase = Range(0f, MathF.PI * 2f);
		p.Variant = (byte)Math.Min(VariantCount(Kind) - 1, (int)(Next01() * VariantCount(Kind)));
		p.X = Range(0f, Width);
		switch (Kind)
		{
		case ParticleKind.Snow:
			p.Size = 1.2f + 3.3f * d;
			p.Vx = Range(-6f, 6f);
			p.Vy = 18f + 46f * d;
			p.PhaseSpeed = Range(0.6f, 1.6f);
			p.BaseAlpha = 0.35f + 0.6f * d;
			p.Y = initial ? Range(-20f, Height) : Range(-40f, -10f);
			break;
		case ParticleKind.Sakura:
			p.Size = 7f + 8f * d;
			p.Vx = Range(8f, 28f);
			p.Vy = 22f + 34f * d;
			p.Spin = Range(-110f, 110f);
			p.PhaseSpeed = Range(1.5f, 3.2f);
			p.BaseAlpha = 0.65f + 0.35f * d;
			p.Y = initial ? Range(-30f, Height) : Range(-60f, -20f);
			break;
		case ParticleKind.Leaves:
			p.Size = 10f + 9f * d;
			p.Vx = Range(-10f, 25f);
			p.Vy = 30f + 40f * d;
			p.Spin = Range(-190f, 190f);
			p.PhaseSpeed = Range(1.0f, 2.4f);
			p.BaseAlpha = 0.75f + 0.25f * d;
			p.Y = initial ? Range(-40f, Height) : Range(-80f, -30f);
			break;
		case ParticleKind.Rain:
			p.Size = 10f + 16f * d;
			p.Vy = 600f + 420f * d;
			p.Vx = -0.14f * p.Vy;
			p.BaseAlpha = 0.18f + 0.32f * d;
			p.X = Range(0f, Width * 1.15f);
			p.Y = initial ? Range(-40f, Height) : Range(-120f, -20f);
			break;
		case ParticleKind.Bubbles:
			p.Size = 4f + 13f * d;
			p.Vy = -(14f + 30f * d);
			p.PhaseSpeed = Range(1.2f, 2.6f);
			p.BaseAlpha = 0.25f + 0.4f * d;
			p.Y = initial ? Range(0f, Height + 20f) : Height + Range(10f, 60f);
			break;
		case ParticleKind.Stars:
			p.Size = 0.6f + 1.6f * d * d;
			p.PhaseSpeed = Range(0.5f, 2.6f);
			p.BaseAlpha = 0.35f + 0.65f * d;
			p.Y = Range(0f, Height);
			break;
		case ParticleKind.Fireflies:
			p.Size = 1.6f + 2.2f * d;
			p.Spin = Range(10f, 28f);
			p.PhaseSpeed = Range(1.2f, 3.5f);
			p.BaseAlpha = 0.6f + 0.4f * d;
			p.Y = Range(Height * 0.2f, Height);
			break;
		}
		p.Alpha = p.BaseAlpha;
	}

	private float Next01()
	{
		// xorshift32
		uint x = _rng;
		x ^= x << 13;
		x ^= x >> 17;
		x ^= x << 5;
		_rng = x;
		return (x >> 8) * (1f / 16777216f);
	}

	private float Range(float min, float max)
	{
		return min + (max - min) * Next01();
	}
}
