using System;
using System.Collections.Generic;

namespace Metamorph.Core.Mascot;

public sealed class MascotAnimator
{
	private sealed class Particle
	{
		public FxKind Kind;

		public double X;

		public double Y;

		public double Vx;

		public double Vy;

		public double Life;

		public double MaxLife;

		public double Rotation;

		public double Spin;

		public double Scale;

		public double Grow;
	}

	private const double BlendTime = 0.22;

	private readonly Random _rnd;

	private readonly List<Particle> _particles = new List<Particle>();

	private readonly Spring _tailL = new Spring(70.0, 5.5);

	private readonly Spring _tailR = new Spring(70.0, 5.5);

	private readonly Spring _ahoge = new Spring(160.0, 6.0);

	private readonly Spring _bangs = new Spring(200.0, 14.0);

	private readonly Spring _skirt = new Spring(120.0, 8.0);

	private readonly Spring _squash = new Spring(260.0, 13.0);

	private readonly Spring _sway = new Spring(60.0, 7.0);

	private MascotPose _from = new MascotPose();

	private MascotPose _last = new MascotPose();

	private double _blend = 1.0;

	private double _time;

	private double _nextBlink = 2.5;

	private double _blinkT = -1.0;

	private double _spawn;

	private double _talkT;

	private double _talkValue;

	private double _prevY;

	private double _lookX;

	private double _lookY;

	private double _turn;

	private bool _doubleBlink;

	private double _lastDt;

	public MascotClip Clip { get; private set; }

	public double ClipTime { get; private set; }

	public double LookX { get; set; }

	public double LookY { get; set; }

	public double Facing { get; set; } = 1.0;


	public double VelocityX { get; set; }

	public double VelocityY { get; set; }

	public double Bass { get; set; }

	public bool Talking { get; set; }

	public EyeStyle? EyesOverride { get; set; }

	public MouthStyle? MouthOverride { get; set; }

	public double BlushBoost { get; set; }

	public MascotPose Current => _last;

	public MascotAnimator(int seed = 7)
	{
		_rnd = new Random(seed);
	}

	public void Play(MascotClip clip, bool restart = false)
	{
		if (clip != Clip || restart)
		{
			_from = _last.Clone();
			_blend = 0.0;
			Clip = clip;
			ClipTime = 0.0;
			_spawn = 0.0;
			if (clip == MascotClip.Surprised)
			{
				_ahoge.Velocity -= 900.0;
			}
		}
	}

	public void Impact(double strength)
	{
		_squash.Velocity += Math.Clamp(strength, 0.0, 1.5) * 4.5;
	}

	public void Jiggle(double strength = 1.0)
	{
		_ahoge.Velocity += 700.0 * strength;
		_tailL.Velocity += 220.0 * strength;
		_tailR.Velocity -= 220.0 * strength;
	}

	public MascotPose Update(double dt)
	{
		dt = Math.Clamp(double.IsFinite(dt) ? dt : 0.0, 0.0, 0.1);
		_time += dt;
		ClipTime += dt;
		_blend = Math.Min(1.0, _blend + dt / 0.22);
		MascotPose mascotPose = ClipPose(Clip, ClipTime);
		MascotPose mascotPose2 = ((_blend >= 1.0) ? mascotPose : Lerp(_from, mascotPose, Ease(_blend)));
		mascotPose2.Fx = mascotPose.Fx;
		double num = Math.Min(1.0, dt * 7.0);
		_lookX += (Math.Clamp(LookX, -1.0, 1.0) - _lookX) * num;
		_lookY += (Math.Clamp(LookY, -1.0, 1.0) - _lookY) * num;
		mascotPose2.LookX = ((Clip == MascotClip.Think) ? mascotPose2.LookX : _lookX);
		mascotPose2.LookY = ((Clip == MascotClip.Think) ? mascotPose2.LookY : _lookY);
		double num2 = ((Clip == MascotClip.Walk) ? ((double)Math.Sign(Facing) * 0.85) : Math.Clamp(_lookX * 0.45, -0.45, 0.45));
		MascotClip clip = Clip;
		if ((uint)(clip - 4) <= 3u)
		{
			num2 = 0.0;
		}
		_turn += (num2 - _turn) * Math.Min(1.0, dt * 5.0);
		mascotPose2.Turn = _turn;
		EyeStyle eyes = mascotPose2.Eyes;
		bool flag = ((eyes == EyeStyle.Normal || (uint)(eyes - 3) <= 1u) ? true : false);
		if (flag && Clip != MascotClip.Held)
		{
			_nextBlink -= dt;
			if (_nextBlink <= 0.0 && _blinkT < 0.0)
			{
				_blinkT = 0.0;
				_doubleBlink = _rnd.NextDouble() < 0.2;
			}
			if (_blinkT >= 0.0)
			{
				_blinkT += dt;
				double num3 = _blinkT / 0.16;
				mascotPose2.EyeOpen = Math.Min(mascotPose2.EyeOpen, (num3 < 0.5) ? (1.0 - num3 * 2.0) : ((num3 - 0.5) * 2.0));
				if (_blinkT >= 0.16)
				{
					_blinkT = -1.0;
					_nextBlink = (_doubleBlink ? 0.12 : (2.0 + _rnd.NextDouble() * 3.5));
					_doubleBlink = false;
				}
			}
		}
		EyeStyle? eyesOverride = EyesOverride;
		if (eyesOverride.HasValue)
		{
			EyeStyle valueOrDefault = eyesOverride.GetValueOrDefault();
			mascotPose2.Eyes = valueOrDefault;
		}
		MouthStyle? mouthOverride = MouthOverride;
		if (mouthOverride.HasValue)
		{
			MouthStyle valueOrDefault2 = mouthOverride.GetValueOrDefault();
			mascotPose2.Mouth = valueOrDefault2;
		}
		mascotPose2.Blush = Math.Clamp(mascotPose2.Blush + BlushBoost, 0.0, 1.0);
		if (Talking)
		{
			_talkT -= dt;
			if (_talkT <= 0.0)
			{
				_talkT = 0.07 + _rnd.NextDouble() * 0.07;
				_talkValue = ((_rnd.NextDouble() < 0.18) ? 0.05 : (0.25 + _rnd.NextDouble() * 0.65));
			}
			mascotPose2.MouthOpen += (_talkValue - mascotPose2.MouthOpen) * Math.Min(1.0, dt * 30.0);
		}
		double num4 = ((dt > 0.0) ? ((mascotPose2.Y - _prevY) / dt) : 0.0);
		_prevY = mascotPose2.Y;
		double num5 = Math.Clamp(VelocityX, -1500.0, 1500.0);
		double num6 = Math.Clamp(VelocityY + num4 * 0.8, -1500.0, 1500.0);
		double num7 = Math.Clamp(num5 * 0.04, -45.0, 45.0);
		double num8 = Math.Clamp(num6 * 0.025, -12.0, 40.0);
		double num9 = 2.2 * Math.Sin(_time * 1.3);
		mascotPose2.TailL = _tailL.Update(num7 + num8 + num9 + mascotPose2.TailL, dt);
		mascotPose2.TailR = _tailR.Update(num7 - num8 + num9 * 0.8 + mascotPose2.TailR, dt);
		mascotPose2.Ahoge = _ahoge.Update(Math.Clamp(num5 * 0.02 + num6 * 0.03, -35.0, 35.0) + 5.0 * Math.Sin(_time * 2.1) + mascotPose2.Ahoge, dt);
		mascotPose2.Bangs = _bangs.Update(Math.Clamp(num5 * 0.006, -4.0, 4.0), dt);
		mascotPose2.Skirt = _skirt.Update(Math.Clamp((0.0 - num5) * 0.02, -10.0, 10.0) + mascotPose2.Skirt, dt);
		MascotPose mascotPose3;
		double rotation = (mascotPose3 = mascotPose2).Rotation;
		Spring sway = _sway;
		clip = Clip;
		flag = (uint)(clip - 6) <= 1u;
		mascotPose3.Rotation = rotation + sway.Update(flag ? 0.0 : Math.Clamp(num5 * 0.004, -6.0, 6.0), dt);
		double num10 = _squash.Update(0.0, dt);
		mascotPose2.ScaleY *= 1.0 - Math.Clamp(num10 * 0.12, -0.12, 0.25);
		mascotPose2.ScaleX *= 1.0 + Math.Clamp(num10 * 0.08, -0.08, 0.18);
		UpdateParticles(dt, mascotPose2);
		_last = mascotPose2;
		return mascotPose2;
	}

	private static double Ease(double t)
	{
		return t * t * (3.0 - 2.0 * t);
	}

	private MascotPose ClipPose(MascotClip clip, double t)
	{
		MascotPose mascotPose = new MascotPose();
		double num = Math.Sin(_time * 2.2);
		mascotPose.Breath = 1.0 + 0.013 * num;
		mascotPose.HeadY = 0.7 * num;
		mascotPose.ArmL = 5.0 + 1.5 * num;
		mascotPose.ArmR = 5.0 + 1.5 * num;
		mascotPose.Head = 2.0 * Math.Sin(_time * 0.7);
		switch (clip)
		{
		case MascotClip.Walk:
		{
			double num5 = t * (Math.PI * 2.0) * 1.15;
			double num6 = Math.Sin(num5);
			mascotPose.LiftL = Math.Max(0.0, num6) * 5.0;
			mascotPose.LiftR = Math.Max(0.0, 0.0 - num6) * 5.0;
			mascotPose.Y = (0.0 - Math.Abs(num6)) * 2.6;
			mascotPose.Rotation = 3.2 * num6;
			mascotPose.ArmL = 8.0 + 11.0 * num6;
			mascotPose.ArmR = 8.0 - 11.0 * num6;
			mascotPose.Head = Facing * 3.0 + 1.5 * Math.Sin(num5 * 2.0);
			mascotPose.Skirt = 4.0 * num6;
			break;
		}
		case MascotClip.Jump:
			mascotPose.ArmL = (mascotPose.ArmR = 70.0 + 12.0 * Math.Sin(t * 14.0));
			mascotPose.LiftL = (mascotPose.LiftR = 3.0);
			mascotPose.Eyes = EyeStyle.Happy;
			mascotPose.Mouth = MouthStyle.Grin;
			mascotPose.Blush = 0.6;
			if (t < 0.12)
			{
				mascotPose.ScaleY = 1.0 - 1.2 * t;
				mascotPose.ScaleX = 1.0 + 0.8 * t;
			}
			else
			{
				mascotPose.ScaleY = 1.0 + Math.Clamp((0.0 - VelocityY) / 2500.0, -0.08, 0.1);
			}
			break;
		case MascotClip.Wave:
			mascotPose.ArmR = 150.0 + 20.0 * Math.Sin(t * (Math.PI * 2.0) * 2.3);
			mascotPose.ArmL = 6.0;
			mascotPose.Head = -5.0 + 2.0 * Math.Sin(t * (Math.PI * 2.0) * 1.15);
			mascotPose.Y = (0.0 - Math.Abs(Math.Sin(t * (Math.PI * 2.0) * 1.15))) * 1.5;
			mascotPose.Eyes = ((t < 1.2) ? EyeStyle.Happy : EyeStyle.Normal);
			mascotPose.Mouth = MouthStyle.Open;
			mascotPose.MouthOpen = 0.45;
			mascotPose.Blush = 0.6;
			break;
		case MascotClip.Dance:
		{
			double num7 = 1.0 + 0.15 * Bass;
			double num8 = t * (Math.PI * 2.0) * num7;
			double num9 = Math.Sin(num8);
			double num10 = Math.Abs(Math.Sin(num8));
			double num11 = Math.Clamp(Bass, 0.0, 1.0);
			mascotPose.Y = (0.0 - (2.5 + 6.0 * num11)) * num10;
			mascotPose.Rotation = 7.0 * num9;
			mascotPose.Head = 8.0 * Math.Sin(num8 + 0.6);
			mascotPose.Skirt = 7.0 * num9;
			mascotPose.LiftL = Math.Max(0.0, num9) * 4.5;
			mascotPose.LiftR = Math.Max(0.0, 0.0 - num9) * 4.5;
			switch ((int)(t / 2.4) % 3)
			{
			case 0:
				mascotPose.ArmL = 125.0 + 28.0 * num9;
				mascotPose.ArmR = 125.0 - 28.0 * num9;
				break;
			case 1:
				mascotPose.ArmL = 55.0 + 20.0 * num10;
				mascotPose.ArmR = 55.0 + 20.0 * num10;
				break;
			default:
				mascotPose.ArmR = 140.0 + 30.0 * Math.Sin(num8 * 2.0);
				mascotPose.ArmL = 12.0;
				break;
			}
			mascotPose.Eyes = (((int)(t / 3.1) % 4 != 3) ? EyeStyle.Happy : EyeStyle.Wink);
			mascotPose.Mouth = (((int)(t / 1.7) % 2 == 0) ? MouthStyle.Grin : MouthStyle.Cat);
			mascotPose.Blush = 0.65;
			Spawn(0.55, FxKind.Note, () => ((double X, double Y, double Vx, double Vy, double Life))(X: (_rnd.NextDouble() < 0.5) ? (-38) : 38, Y: -150.0, Vx: (_rnd.NextDouble() - 0.5) * 30.0, Vy: -45.0, Life: 2.2));
			break;
		}
		case MascotClip.Sleep:
			mascotPose.Sit = 1.0;
			mascotPose.Head = 13.0 + 2.0 * Math.Sin(_time * 0.9);
			mascotPose.Breath = 1.0 + 0.03 * Math.Sin(_time * 1.4);
			mascotPose.HeadY = 1.2 * Math.Sin(_time * 1.4);
			mascotPose.ArmL = (mascotPose.ArmR = -5.0);
			mascotPose.Eyes = EyeStyle.Closed;
			mascotPose.Mouth = ((Math.Sin(_time * 1.4) > 0.3) ? MouthStyle.O : MouthStyle.Flat);
			mascotPose.Blush = 0.45;
			Spawn(1.3, FxKind.Z, () => (X: 22.0, Y: -150.0, Vx: 14.0, Vy: -22.0, Life: 2.8));
			break;
		case MascotClip.Held:
			mascotPose.ArmL = 130.0 + 12.0 * Math.Sin(t * 9.0);
			mascotPose.ArmR = 130.0 + 12.0 * Math.Sin(t * 9.0 + 1.7);
			mascotPose.LiftL = 2.0 + 2.5 * Math.Sin(t * 10.0);
			mascotPose.LiftR = 2.0 + 2.5 * Math.Sin(t * 10.0 + Math.PI);
			mascotPose.Rotation = Math.Clamp((0.0 - VelocityX) * 0.03, -28.0, 28.0);
			mascotPose.Eyes = ((Math.Abs(VelocityX) > 1400.0) ? EyeStyle.Dizzy : EyeStyle.Wide);
			mascotPose.Mouth = MouthStyle.O;
			mascotPose.Blush = 0.5;
			Spawn(0.9, FxKind.Sweat, () => (X: 34.0, Y: -138.0, Vx: 12.0, Vy: 30.0, Life: 0.9));
			break;
		case MascotClip.Fall:
			mascotPose.ArmL = (mascotPose.ArmR = 150.0 + 10.0 * Math.Sin(t * 12.0));
			mascotPose.LiftL = (mascotPose.LiftR = 2.0);
			mascotPose.Rotation = Math.Clamp(VelocityX * 0.035, -30.0, 30.0);
			mascotPose.ScaleY = 1.0 + Math.Clamp(VelocityY / 6000.0, 0.0, 0.08);
			mascotPose.Eyes = EyeStyle.Wide;
			mascotPose.Mouth = MouthStyle.O;
			break;
		case MascotClip.Sit:
			mascotPose.Sit = 1.0;
			mascotPose.ArmL = (mascotPose.ArmR = -6.0);
			mascotPose.Head = 4.0 * Math.Sin(_time * 0.5);
			mascotPose.LiftL = 1.5 + 1.5 * Math.Sin(_time * 3.0);
			mascotPose.LiftR = 1.5 + 1.5 * Math.Sin(_time * 3.0 + Math.PI);
			mascotPose.Mouth = (((int)(_time / 6.0) % 2 != 0) ? MouthStyle.Cat : MouthStyle.Smile);
			break;
		case MascotClip.Think:
			mascotPose.Head = 9.0;
			mascotPose.LookX = -0.8;
			mascotPose.LookY = -0.9;
			mascotPose.ArmR = 28.0;
			mascotPose.Mouth = MouthStyle.Pout;
			mascotPose.Fx.Add(new MascotFx(FxKind.Dots, 30.0, -172.0, 1.1, 0.0, Math.Min(1.0, t * 3.0)));
			break;
		case MascotClip.Happy:
		{
			double num3 = Math.Abs(Math.Sin(t * (Math.PI * 2.0) * 1.6));
			mascotPose.Y = (0.0 - num3) * 6.0;
			mascotPose.ArmL = (mascotPose.ArmR = 70.0 + 15.0 * num3);
			mascotPose.Eyes = EyeStyle.Happy;
			mascotPose.Mouth = MouthStyle.Grin;
			mascotPose.Blush = 0.85;
			Spawn(0.3, FxKind.Sparkle, () => (X: (_rnd.NextDouble() - 0.5) * 120.0, Y: -120.0 - _rnd.NextDouble() * 50.0, Vx: 0.0, Vy: -20.0, Life: 0.8));
			break;
		}
		case MascotClip.Surprised:
			mascotPose.Y = ((t < 0.35) ? (-7.0 * Math.Sin(t / 0.35 * Math.PI)) : 0.0);
			mascotPose.ArmL = (mascotPose.ArmR = 48.0);
			mascotPose.Eyes = EyeStyle.Wide;
			mascotPose.Mouth = MouthStyle.O;
			mascotPose.Blush = 0.5;
			if (t < 0.6)
			{
				mascotPose.Fx.Add(new MascotFx(FxKind.Sparkle, 36.0, -160.0, 1.2 + 0.4 * Math.Sin(t * 20.0), t * 90.0, 1.0 - t / 0.6));
			}
			break;
		case MascotClip.Stretch:
		{
			double num4 = ((t < 0.8) ? Ease(t / 0.8) : ((t < 1.8) ? 1.0 : Math.Max(0.0, 1.0 - (t - 1.8) / 0.6)));
			mascotPose.ArmL = (mascotPose.ArmR = 5.0 + 118.0 * num4);
			mascotPose.Rotation = 2.5 * num4 * Math.Sin(t * 2.4);
			mascotPose.LiftL = (mascotPose.LiftR = 2.5 * num4);
			mascotPose.ScaleY = 1.0 + 0.05 * num4;
			mascotPose.Head = -4.0 * num4;
			mascotPose.Eyes = ((num4 > 0.5) ? EyeStyle.Closed : EyeStyle.Normal);
			mascotPose.Mouth = ((num4 > 0.5) ? MouthStyle.Open : MouthStyle.Smile);
			mascotPose.MouthOpen = ((num4 > 0.5) ? 0.85 : 0.0);
			break;
		}
		case MascotClip.Dizzy:
		{
			mascotPose.Rotation = 6.0 * Math.Sin(t * 3.0);
			mascotPose.Head = 8.0 * Math.Sin(t * 3.0 + 1.0);
			mascotPose.Eyes = EyeStyle.Dizzy;
			mascotPose.Mouth = MouthStyle.Pout;
			for (int i = 0; i < 3; i++)
			{
				double num2 = t * 4.0 + (double)i * (Math.PI * 2.0) / 3.0;
				mascotPose.Fx.Add(new MascotFx(FxKind.Star, Math.Cos(num2) * 26.0, -160.0 + Math.Sin(num2) * 7.0, 0.8, num2 * 40.0, (Math.Sin(num2) > -0.2) ? 1.0 : 0.5));
			}
			break;
		}
		case MascotClip.Love:
			mascotPose.Eyes = EyeStyle.Heart;
			mascotPose.Blush = 1.0;
			mascotPose.Mouth = MouthStyle.Grin;
			mascotPose.Rotation = 4.0 * Math.Sin(t * 3.0);
			mascotPose.ArmL = (mascotPose.ArmR = 20.0);
			Spawn(0.45, FxKind.Heart, () => (X: (_rnd.NextDouble() - 0.5) * 80.0, Y: -150.0, Vx: (_rnd.NextDouble() - 0.5) * 20.0, Vy: -35.0, Life: 1.8));
			break;
		case MascotClip.Annoyed:
			mascotPose.Mouth = MouthStyle.Pout;
			mascotPose.Eyes = EyeStyle.Normal;
			mascotPose.EyeOpen = 0.62;
			mascotPose.Head = -6.0;
			mascotPose.ArmL = (mascotPose.ArmR = -8.0);
			mascotPose.Blush = 0.9;
			mascotPose.Fx.Add(new MascotFx(FxKind.Sweat, 34.0, -142.0, 1.0, 0.0, 1.0));
			break;
		}
		return mascotPose;
	}

	private void Spawn(double every, FxKind kind, Func<(double X, double Y, double Vx, double Vy, double Life)> make)
	{
		_spawn -= _lastDt;
		if (!(_spawn > 0.0))
		{
			_spawn = every;
			var (x, y, vx, vy, maxLife) = make();
			_particles.Add(new Particle
			{
				Kind = kind,
				X = x,
				Y = y,
				Vx = vx,
				Vy = vy,
				Life = 0.0,
				MaxLife = maxLife,
				Rotation = (_rnd.NextDouble() - 0.5) * 30.0,
				Spin = (_rnd.NextDouble() - 0.5) * 40.0,
				Scale = ((kind == FxKind.Z) ? 0.7 : 1.0),
				Grow = ((kind == FxKind.Z) ? 0.35 : 0.0)
			});
		}
	}

	private void UpdateParticles(double dt, MascotPose pose)
	{
		_lastDt = dt;
		for (int num = _particles.Count - 1; num >= 0; num--)
		{
			Particle particle = _particles[num];
			particle.Life += dt;
			if (particle.Life >= particle.MaxLife)
			{
				_particles.RemoveAt(num);
			}
			else
			{
				particle.X += particle.Vx * dt + Math.Sin(particle.Life * 3.0 + (double)num) * 6.0 * dt;
				particle.Y += particle.Vy * dt;
				particle.Rotation += particle.Spin * dt;
				particle.Scale += particle.Grow * dt;
				double num2 = particle.Life / particle.MaxLife;
				double alpha = ((num2 < 0.15) ? (num2 / 0.15) : ((num2 > 0.7) ? ((1.0 - num2) / 0.3) : 1.0));
				pose.Fx.Add(new MascotFx(particle.Kind, particle.X, particle.Y, particle.Scale, particle.Rotation, alpha));
			}
		}
		if (_particles.Count > 40)
		{
			_particles.RemoveRange(0, _particles.Count - 40);
		}
	}

	private static MascotPose Lerp(MascotPose a, MascotPose b, double t)
	{
		MascotPose mascotPose = b.Clone();
		mascotPose.X = L(a.X, b.X);
		mascotPose.Y = L(a.Y, b.Y);
		mascotPose.Rotation = L(a.Rotation, b.Rotation);
		mascotPose.ScaleX = L(a.ScaleX, b.ScaleX);
		mascotPose.ScaleY = L(a.ScaleY, b.ScaleY);
		mascotPose.Body = L(a.Body, b.Body);
		mascotPose.Breath = L(a.Breath, b.Breath);
		mascotPose.Head = L(a.Head, b.Head);
		mascotPose.HeadY = L(a.HeadY, b.HeadY);
		mascotPose.ArmL = L(a.ArmL, b.ArmL);
		mascotPose.ArmR = L(a.ArmR, b.ArmR);
		mascotPose.LiftL = L(a.LiftL, b.LiftL);
		mascotPose.LiftR = L(a.LiftR, b.LiftR);
		mascotPose.Sit = L(a.Sit, b.Sit);
		mascotPose.Skirt = L(a.Skirt, b.Skirt);
		mascotPose.EyeOpen = L(a.EyeOpen, b.EyeOpen);
		mascotPose.MouthOpen = L(a.MouthOpen, b.MouthOpen);
		mascotPose.Blush = L(a.Blush, b.Blush);
		mascotPose.LookX = L(a.LookX, b.LookX);
		mascotPose.LookY = L(a.LookY, b.LookY);
		if (t < 0.5)
		{
			mascotPose.Eyes = a.Eyes;
			mascotPose.Mouth = a.Mouth;
		}
		return mascotPose;
		double L(double x, double y)
		{
			return x + (y - x) * t;
		}
	}
}
