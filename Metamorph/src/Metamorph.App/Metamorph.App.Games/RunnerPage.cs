using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.App.Mascot;
using Metamorph.Core.Games;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// «Мета-раннер»: мир рисуется одним элементом, Мета — MascotCanvas поверх, HUD — отдельным слоем.
internal sealed class RunnerPage : GamePage
{
	private enum State
	{
		Ready,
		Running,
		Paused,
		Over
	}

	private struct Dust
	{
		public double X;

		public double Y;

		public double Vx;

		public double Vy;

		public double Life;

		public double Max;

		public double Size;
	}

	// Мировые единицы на высоту экрана.
	private const double WorldHeight = 380.0;

	private const double MetaScale = 0.42;

	private const double FarPeriod = 1800.0;

	private const double CityPeriod = 1400.0;

	private const double CloudPeriod = 2400.0;

	private const double GroundPeriod = 640.0;

	private const double StarPeriod = 2200.0;

	private const double DayLength = 700.0;

	private static readonly Geometry MoonGeo = RunnerArt.Freeze(new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(0, 0), 22, 22), new EllipseGeometry(new Point(9, -6), 19, 19)));

	private static readonly string[] OverLines = { "Ай! Не заметила…", "Кристаллы коварны!", "Ой-ой… звёздочки…", "Ну вот… Давай ещё!" };

	private readonly DinoEngine _engine = new DinoEngine(GameRandom.NewSeed());

	private readonly GameLoop _loop;

	private readonly DrawSurface _world;

	private readonly DrawSurface _hud;

	private readonly MascotCanvas _meta = new MascotCanvas
	{
		IsHitTestVisible = false
	};

	private readonly MascotAnimator _anim = new MascotAnimator(9);

	private readonly MascotColors _colors;

	private readonly RunnerArt _art;

	private readonly TransformPool _pool = new TransformPool();

	private readonly Dust[] _dust = new Dust[64];

	private readonly Random _fx = new Random();

	private readonly Brush _skyDay;

	private readonly Brush _skyNight;

	private readonly Brush _sun;

	private readonly Brush _sunGlow;

	private readonly Brush _moon;

	private readonly Brush _stars;

	private readonly Brush _cloudDay;

	private readonly Brush _cloudNight;

	private readonly Brush _farDay;

	private readonly Brush _farNight;

	private readonly Brush _cityDay;

	private readonly Brush _cityNight;

	private readonly Brush _windows;

	private readonly Brush _ground;

	private readonly Brush _groundNight;

	private readonly Pen _groundLine;

	private readonly Brush _groundDetail;

	private readonly Brush _dustBrush;

	private readonly Brush _flash;

	private readonly Brush _dim;

	private readonly Brush _panel;

	private readonly Pen _panelEdge;

	private readonly Brush _fg;

	private readonly Brush _muted;

	private readonly Brush _accent;

	private readonly Brush _gold;

	private readonly Brush _bubbleFill;

	private readonly Pen _bubbleEdge;

	private readonly Brush _bubbleText;

	private readonly Geometry _far;

	private readonly Geometry _city;

	private readonly Geometry _cityWindows;

	private readonly Geometry _cloud;

	private readonly Geometry _groundDetails;

	private readonly Geometry _starField;

	private readonly double[] _clouds = { 120, 200, 640, 260, 980, 170, 1450, 240, 1900, 190 };

	private readonly TextSlot _scoreText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _hiText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _plusText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _titleText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _lineText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _hintText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _recordText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _bubble = new TextSlot(TextSlot.Ui);

	private State _state = State.Ready;

	private double _time;

	private double _idleUntil;

	private double _overAt;

	private double _milestoneT = 99.0;

	private int _milestoneValue;

	private string? _bubbleLine;

	private double _bubbleUntil;

	private double _nearMissCooldown;

	private double _runDust;

	private double _shownY;

	private bool _record;

	private int _best;

	private double _ppd = 1.0;

	private double _k = 1.0;

	private double _gy;

	private double _px;

	public override string Id => GameHub.Dino;

	public override string Title => Loc.T("Мета-раннер");

	public RunnerPage(App app, GamePalette pal)
		: base(app, pal)
	{
		_colors = MascotColors.From(app.Services.Settings.Current.Mascot, pal.Source);
		_art = new RunnerArt(pal);
		_loop = new GameLoop(Tick);
		_world = new DrawSurface(DrawWorld);
		_hud = new DrawSurface(DrawHud);
		Children.Add(_world);
		Children.Add(_meta);
		Children.Add(_hud);
		ClipToBounds = true;
		Color dayTop = GamePalette.Mix(pal.Bg, pal.Accent2, 0.42);
		Color dayBottom = GamePalette.Lighten(GamePalette.Mix(pal.Bg, GamePalette.Mix(pal.Yellow, pal.Accent, 0.4), 0.24), 0.04);
		Color nightTop = GamePalette.Darken(GamePalette.Mix(pal.Bg, pal.Accent, 0.12), 0.6);
		Color nightBottom = GamePalette.Mix(GamePalette.Darken(pal.Bg, 0.3), pal.Accent, 0.2);
		_skyDay = GamePalette.Vertical((dayTop, 0.0), (dayBottom, 0.8));
		_skyNight = GamePalette.Vertical((nightTop, 0.0), (nightBottom, 0.8));
		_sun = GamePalette.Solid(GamePalette.Vivid(pal.Yellow));
		_sunGlow = new RadialGradientBrush(GamePalette.WithAlpha(pal.Yellow, 0.35), GamePalette.WithAlpha(pal.Yellow, 0.0));
		_sunGlow.Freeze();
		_moon = GamePalette.Solid(GamePalette.Lighten(pal.Fg, 0.4));
		_stars = GamePalette.Solid(GamePalette.Lighten(pal.Fg, 0.5), 0.9);
		_cloudDay = GamePalette.Solid(Colors.White, 0.72);
		_cloudNight = GamePalette.Solid(pal.Fg, 0.12);
		Color far = GamePalette.Darken(GamePalette.Mix(dayBottom, pal.Accent, 0.3), 0.12);
		_farDay = GamePalette.Solid(far, 0.85);
		_farNight = GamePalette.Solid(GamePalette.Darken(GamePalette.Mix(nightBottom, pal.Accent, 0.22), 0.2), 0.9);
		Color city = GamePalette.Mix(GamePalette.Mix(pal.Bg, pal.Surface, 0.6), pal.Accent2, 0.18);
		_cityDay = GamePalette.Solid(city);
		_cityNight = GamePalette.Solid(GamePalette.Darken(city, 0.45));
		_windows = GamePalette.Solid(GamePalette.Vivid(pal.Yellow), 0.9);
		Color ground = GamePalette.Mix(pal.Surface, pal.Accent, 0.14);
		_ground = GamePalette.Vertical((ground, 0.0), (GamePalette.Darken(pal.Surface, 0.35), 1.0));
		_groundNight = GamePalette.Solid(Colors.Black, 0.35);
		_groundLine = GamePalette.Pen(GamePalette.Vivid(pal.Accent), 2.5);
		_groundDetail = GamePalette.Solid(GamePalette.Mix(ground, pal.Fg, 0.25), 0.55);
		_dustBrush = GamePalette.Solid(GamePalette.Mix(pal.Fg, ground, 0.35), 0.7);
		_flash = GamePalette.Solid(pal.Accent, 0.16);
		_dim = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.4), 0.55);
		_panel = GamePalette.Solid(GamePalette.Mix(pal.Surface, pal.Bg, 0.3), 0.92);
		_panelEdge = GamePalette.Pen(pal.Accent, 1.5, 0.6);
		_fg = GamePalette.Solid(pal.Fg);
		_muted = GamePalette.Solid(pal.Muted);
		_accent = GamePalette.Solid(GamePalette.Vivid(pal.Accent));
		_gold = GamePalette.Solid(GamePalette.Vivid(pal.Yellow));
		_bubbleFill = GamePalette.Solid(GamePalette.Lighten(pal.Surface, 0.08), 0.96);
		_bubbleEdge = GamePalette.Pen(pal.Accent, 1.5, 0.7);
		_bubbleText = GamePalette.Solid(pal.Fg);
		_far = BuildHills(FarPeriod, 7, 70.0, 150.0, 11);
		(_city, _cityWindows) = BuildCity(CityPeriod, 23);
		_cloud = RunnerArt.Group(RunnerArt.Ellipse(0, 0, 34, 14), RunnerArt.Ellipse(-22, 4, 20, 11), RunnerArt.Ellipse(24, 3, 22, 12), RunnerArt.Ellipse(4, -10, 20, 13));
		_groundDetails = BuildGroundDetails(31);
		_starField = BuildStars(47);
		_engine.Event += OnEngineEvent;
		_best = GameHub.Scores.Best(GameHub.Dino);
		_anim.Facing = 1.0;
		_anim.Play(MascotClip.Wave, restart: true);
		Loaded += delegate
		{
			_ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		};
		SizeChanged += delegate
		{
			Render(0.0);
		};
	}

	public override void OnShown()
	{
		GameHub.Scores.Touch(GameHub.Dino);
		Wake(4.0);
	}

	public override void OnActivated()
	{
		if (_state is State.Ready or State.Over)
		{
			Wake(3.0);
		}
	}

	public override void OnDeactivated()
	{
		ReleaseKeys();
		if (_state == State.Running)
		{
			Pause();
		}
		else
		{
			_loop.Stop();
		}
	}

	public override void Close()
	{
		_loop.Stop();
		_engine.Event -= OnEngineEvent;
	}

	public override bool OnEscape()
	{
		if (_state == State.Running)
		{
			Pause();
			return true;
		}
		return false;
	}

	public override bool OnKey(Key key, bool down, bool repeat)
	{
		switch (key)
		{
		case Key.Space:
		case Key.Up:
		case Key.W:
			if (down && !repeat)
			{
				switch (_state)
				{
				case State.Ready:
					Start();
					_engine.SetJump(true);
					break;
				case State.Paused:
					Resume();
					break;
				case State.Over:
					if (_time - _overAt > 0.45)
					{
						Start();
					}
					break;
				default:
					_engine.SetJump(true);
					break;
				}
			}
			else if (!down)
			{
				_engine.SetJump(false);
			}
			return true;
		case Key.Down:
		case Key.S:
			if (!repeat)
			{
				_engine.SetDuck(down);
			}
			return true;
		case Key.P:
			if (down && !repeat)
			{
				if (_state == State.Running)
				{
					Pause();
				}
				else if (_state == State.Paused)
				{
					Resume();
				}
			}
			return true;
		case Key.R:
			if (down && !repeat && _state != State.Running)
			{
				Start();
			}
			return true;
		default:
			return false;
		}
	}

	private void Start()
	{
		_engine.Reset(GameRandom.NewSeed());
		Array.Clear(_dust);
		_state = State.Running;
		_record = false;
		_bubbleLine = null;
		_milestoneT = 99.0;
		_best = GameHub.Scores.Best(GameHub.Dino);
		_anim.Play(MascotClip.Walk, restart: true);
		_loop.Start();
	}

	private void Pause()
	{
		ReleaseKeys();
		_state = State.Paused;
		_loop.Stop();
		Render(0.0);
	}

	private void Resume()
	{
		_state = State.Running;
		_loop.Start();
	}

	private void ReleaseKeys()
	{
		_engine.SetJump(false);
		_engine.SetDuck(false);
	}

	// Вне игры кадры крутятся ещё немного (анимация Меты), потом цикл засыпает.
	private void Wake(double seconds)
	{
		_idleUntil = _time + seconds;
		_loop.Start();
	}

	private void Tick(double dt)
	{
		_time += dt;
		if (_state == State.Running)
		{
			_engine.Update(dt);
		}
		Render(dt);
		if (_state != State.Running && _time > _idleUntil)
		{
			_loop.Stop();
		}
	}

	private void OnEngineEvent(DinoEvent e)
	{
		switch (e.Kind)
		{
		case DinoEventKind.Jump:
			_anim.Play(MascotClip.Jump, restart: true);
			Puff(5, 60.0);
			break;
		case DinoEventKind.Land:
			_anim.Impact(Math.Min(1.2, e.Value / 800.0));
			Puff(9, 110.0);
			break;
		case DinoEventKind.NearMiss:
			if (_time > _nearMissCooldown)
			{
				_nearMissCooldown = _time + 8.0;
				Say(Loc.T("Фух, чуть-чуть!"), 1.6);
				_anim.Jiggle(0.8);
			}
			break;
		case DinoEventKind.Milestone:
			_milestoneT = 0.0;
			_milestoneValue = e.Value;
			break;
		case DinoEventKind.GameOver:
			_state = State.Over;
			_overAt = _time;
			_anim.Play(MascotClip.Dizzy, restart: true);
			_anim.Impact(1.0);
			_record = GameHub.Submit(App, GameHub.Dino, e.Value);
			Say(_record ? Loc.T("Новый рекорд! Ура-а!") : Loc.T(OverLines[_fx.Next(OverLines.Length)]), 3.5);
			if (_record)
			{
				_best = e.Value;
			}
			Wake(3.0);
			break;
		}
	}

	private void Say(string line, double seconds)
	{
		_bubbleLine = line;
		_bubbleUntil = _time + seconds;
	}

	private void Puff(int count, double power)
	{
		for (int n = 0; n < count; n++)
		{
			for (int i = 0; i < _dust.Length; i++)
			{
				if (_dust[i].Life <= 0.0)
				{
					double life = 0.35 + _fx.NextDouble() * 0.3;
					_dust[i] = new Dust
					{
						X = -6.0 + _fx.NextDouble() * 18.0,
						Y = 1.0,
						Vx = -_engine.Speed * 0.35 - _fx.NextDouble() * power,
						Vy = 20.0 + _fx.NextDouble() * power * 0.6,
						Life = life,
						Max = life,
						Size = 3.0 + _fx.NextDouble() * 4.0
					};
					break;
				}
			}
		}
	}

	private void Render(double dt)
	{
		double w = ActualWidth;
		double h = ActualHeight;
		if (w < 4.0 || h < 4.0)
		{
			return;
		}
		_k = h / WorldHeight;
		_gy = Math.Round(h * 0.8);
		_px = Math.Round(Math.Min(w * 0.16, 180.0));
		UpdateDust(dt);
		UpdateMeta(dt);
		_milestoneT += dt;
		_world.InvalidateVisual();
		_hud.InvalidateVisual();
	}

	private void UpdateDust(double dt)
	{
		if (_state == State.Running && _engine.OnGround)
		{
			_runDust -= dt;
			if (_runDust <= 0.0)
			{
				_runDust = 0.11;
				Puff(1, 30.0);
			}
		}
		for (int i = 0; i < _dust.Length; i++)
		{
			ref Dust d = ref _dust[i];
			if (d.Life > 0.0)
			{
				d.Life -= dt;
				d.X += d.Vx * dt;
				d.Y += d.Vy * dt;
				d.Vy -= 160.0 * dt;
				if (d.Y < 0.0)
				{
					d.Y = 0.0;
				}
			}
		}
	}

	private void UpdateMeta(double dt)
	{
		double speedT = (_engine.Speed - DinoEngine.StartSpeed) / (DinoEngine.MaxSpeed - DinoEngine.StartSpeed);
		double rate = 1.0;
		double sx = 1.0;
		double sy = 1.0;
		switch (_state)
		{
		case State.Ready:
			if (_anim.Clip == MascotClip.Wave && _anim.ClipTime > 2.4)
			{
				_anim.Play(MascotClip.Idle);
			}
			_anim.LookX = 0.8;
			_shownY = 0.0;
			break;
		case State.Running:
			if (_engine.OnGround)
			{
				_anim.Play(MascotClip.Walk);
				rate = 1.7 + 1.6 * speedT;
				if (_engine.Ducking)
				{
					sx = 1.16;
					sy = 0.6;
				}
			}
			else
			{
				_anim.Play(MascotClip.Jump);
				_anim.VelocityY = -_engine.PlayerVy * 0.8;
				if (_engine.DuckHeld)
				{
					sy = 0.86;
				}
			}
			_anim.LookX = 0.9;
			_anim.LookY = _engine.OnGround ? 0.15 : -0.2;
			_anim.VelocityX = 240.0 + 300.0 * speedT;
			_shownY = _engine.PlayerY;
			break;
		case State.Over:
			_anim.VelocityX = 0.0;
			_anim.VelocityY = 0.0;
			_shownY = Math.Max(0.0, _shownY - 600.0 * dt);
			break;
		}
		MascotPose pose = _anim.Update(dt * rate);
		double ms = _k * MetaScale;
		double feet = _gy - _shownY * _k;
		double lift = Math.Clamp(_shownY / 140.0, 0.0, 1.0);
		_meta.SetShadow(new Point(_px, _gy + 3.0), 70.0 * ms * (1.0 - 0.5 * lift), 1.0 - 0.6 * lift);
		_meta.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(ms * sx, ms * sy).Then(Affine.Translate(_px, feet))));
	}

	private double Night()
	{
		double p = _engine.Distance * DinoEngine.ScorePerUnit / DayLength;
		int seg = (int)Math.Floor(p);
		double frac = p - seg;
		double t = Math.Clamp((frac - 0.86) / 0.14, 0.0, 1.0);
		t = t * t * (3.0 - 2.0 * t);
		return (seg & 1) == 0 ? t : 1.0 - t;
	}

	private void DrawWorld(DrawingContext dc, double w, double h)
	{
		_pool.Reset();
		double k = _k;
		double gy = _gy;
		double dist = _engine.Distance;
		double night = Night();
		Rect sky = new Rect(0.0, 0.0, w, gy + 1.0);
		dc.DrawRectangle(_skyDay, null, sky);
		if (night > 0.01)
		{
			dc.PushOpacity(night);
			dc.DrawRectangle(_skyNight, null, sky);
			Layer(dc, _starField, _stars, StarPeriod, 0.02, w, gy - 120.0 * k);
			dc.PushTransform(_pool.Next(k / 1.6, w * 0.72, gy * 0.24));
			dc.DrawGeometry(_moon, null, MoonGeo);
			dc.Pop();
			dc.Pop();
		}
		if (night < 0.99)
		{
			dc.PushOpacity(1.0 - night);
			Point sun = new Point(w * 0.8, gy * 0.26);
			dc.DrawEllipse(_sunGlow, null, sun, 70.0 * k / 1.6, 70.0 * k / 1.6);
			dc.DrawEllipse(_sun, null, sun, 26.0 * k / 1.6, 26.0 * k / 1.6);
			dc.Pop();
		}
		Brush cloud = night > 0.5 ? _cloudNight : _cloudDay;
		double cloudShift = (dist * 0.12 + _time * 6.0) % CloudPeriod;
		for (int copy = 0; copy < 3; copy++)
		{
			double baseX = (copy * CloudPeriod - cloudShift) * k;
			if (baseX > w)
			{
				break;
			}
			for (int i = 0; i < _clouds.Length; i += 2)
			{
				double cx = baseX + _clouds[i] * k;
				if (cx < -80.0 * k || cx > w + 80.0 * k)
				{
					continue;
				}
				dc.PushTransform(_pool.Next(k * (0.8 + 0.1 * (i % 3)), cx, gy - _clouds[i + 1] * k));
				dc.DrawGeometry(cloud, null, _cloud);
				dc.Pop();
			}
		}
		Layer(dc, _far, _farDay, FarPeriod, 0.12, w, gy);
		if (night > 0.01)
		{
			dc.PushOpacity(night);
			Layer(dc, _far, _farNight, FarPeriod, 0.12, w, gy);
			dc.Pop();
		}
		Layer(dc, _city, _cityDay, CityPeriod, 0.32, w, gy);
		if (night > 0.01)
		{
			dc.PushOpacity(night);
			Layer(dc, _city, _cityNight, CityPeriod, 0.32, w, gy);
			Layer(dc, _cityWindows, _windows, CityPeriod, 0.32, w, gy);
			dc.Pop();
		}
		Rect ground = new Rect(0.0, gy, w, h - gy);
		dc.DrawRectangle(_ground, null, ground);
		Layer(dc, _groundDetails, _groundDetail, GroundPeriod, 1.0, w, gy);
		if (night > 0.01)
		{
			dc.PushOpacity(night);
			dc.DrawRectangle(_groundNight, null, ground);
			dc.Pop();
		}
		dc.DrawLine(_groundLine, new Point(0.0, gy), new Point(w, gy));
		ReadOnlySpan<DinoObstacle> obstacles = _engine.Obstacles;
		for (int i = 0; i < obstacles.Length; i++)
		{
			ref readonly DinoObstacle o = ref obstacles[i];
			double sx = _px + o.X * k;
			if (sx > w + 40.0 || sx + o.W * k < -40.0)
			{
				continue;
			}
			_art.Obstacle(dc, _pool, in o, sx, gy - o.Y * k, k, _time);
		}
		for (int i = 0; i < _dust.Length; i++)
		{
			ref Dust d = ref _dust[i];
			if (d.Life > 0.0)
			{
				double a = d.Life / d.Max;
				dc.PushOpacity(a * 0.8);
				double r = d.Size * (1.6 - 0.6 * a) * k * 0.6;
				dc.DrawEllipse(_dustBrush, null, new Point(_px + d.X * k, gy - d.Y * k - r * 0.5), r, r * 0.8);
				dc.Pop();
			}
		}
		if (_milestoneT < 0.35)
		{
			dc.PushOpacity(1.0 - _milestoneT / 0.35);
			dc.DrawRectangle(_flash, null, new Rect(0.0, 0.0, w, h));
			dc.Pop();
		}
	}

	private void Layer(DrawingContext dc, Geometry g, Brush brush, double period, double parallax, double w, double baseY)
	{
		double shift = _engine.Distance * parallax % period;
		for (int copy = 0; copy < 4; copy++)
		{
			double x = (copy * period - shift) * _k;
			if (x > w)
			{
				break;
			}
			dc.PushTransform(_pool.Next(_k, x, baseY));
			dc.DrawGeometry(brush, null, g);
			dc.Pop();
		}
	}

	private void DrawHud(DrawingContext dc, double w, double h)
	{
		double ppd = _ppd;
		double size = Math.Clamp(h / 28.0, 16.0, 26.0);
		bool blink = _milestoneT < 0.9 && ((int)(_milestoneT * 8.0) & 1) == 1;
		FormattedText score = _scoreText.Number(_engine.Score, "00000", size, _fg, ppd);
		FormattedText hi = _hiText.Number(_best, "'HI '00000", size, _muted, ppd);
		double x = w - 24.0 - score.Width;
		if (!blink)
		{
			dc.DrawText(score, new Point(x, 18.0));
		}
		dc.DrawText(hi, new Point(x - hi.Width - size, 18.0));
		if (_milestoneT < 1.1)
		{
			FormattedText plus = _plusText.Number(_milestoneValue, "'+'0", size * 1.1, _accent, ppd);
			dc.PushOpacity(1.0 - _milestoneT / 1.1);
			dc.DrawText(plus, new Point(w - 24.0 - plus.Width, 18.0 + size * 1.4 + 16.0 * _milestoneT));
			dc.Pop();
		}
		if (_bubbleLine != null && _time < _bubbleUntil)
		{
			FormattedText line = _bubble.Get(_bubbleLine, 15.0, _bubbleText, ppd, 240.0);
			double top = _gy - (_shownY + 210.0 * MetaScale) * _k;
			GameDraw.Bubble(dc, line, _px + 10.0, top, _bubbleFill, _bubbleEdge, Math.Clamp((_bubbleUntil - _time) * 3.0, 0.0, 1.0), 10.0, w - 10.0);
		}
		switch (_state)
		{
		case State.Ready:
			Panel(dc, w, h, Loc.T("Мета-раннер"), Loc.T("Пробел или ↑ — старт"), Loc.T("Пробел/↑ — прыжок (держи — выше) · ↓ — пригнуться"), false);
			break;
		case State.Paused:
			dc.DrawRectangle(_dim, null, new Rect(0.0, 0.0, w, h));
			Panel(dc, w, h, Loc.T("Пауза"), Loc.F("Счёт: {0}", _engine.Score), Loc.T("P / Пробел — продолжить · Esc — в меню"), false);
			break;
		case State.Over:
		{
			double a = Math.Clamp((_time - _overAt) / 0.3, 0.0, 1.0);
			dc.PushOpacity(a);
			Panel(dc, w, h, Loc.T("Игра окончена"), Loc.F("Счёт: {0}", _engine.Score), Loc.T("Пробел / R — ещё раз · Esc — в меню"), _record);
			dc.Pop();
			break;
		}
		}
	}

	private void Panel(DrawingContext dc, double w, double h, string title, string line, string hint, bool record)
	{
		double ppd = _ppd;
		FormattedText t = _titleText.Get(title, 30.0, _fg, ppd);
		FormattedText l = _lineText.Get(line, 17.0, _muted, ppd);
		FormattedText hn = _hintText.Get(hint, 13.0, _muted, ppd);
		FormattedText? rec = record ? _recordText.Get("🏆 " + Loc.T("Новый рекорд!"), 18.0, _gold, ppd) : null;
		double pw = Math.Max(Math.Max(t.Width, l.Width), hn.Width) + 56.0;
		double ph = t.Height + l.Height + hn.Height + 44.0 + (rec != null ? rec.Height + 6.0 : 0.0);
		double px = (w - pw) / 2.0;
		double py = Math.Max(20.0, h * 0.3 - ph / 2.0);
		dc.DrawRoundedRectangle(_panel, _panelEdge, new Rect(px, py, pw, ph), 18.0, 18.0);
		double y = py + 16.0;
		dc.DrawText(t, new Point((w - t.Width) / 2.0, y));
		y += t.Height + 4.0;
		if (rec != null)
		{
			dc.DrawText(rec, new Point((w - rec.Width) / 2.0, y));
			y += rec.Height + 6.0;
		}
		dc.DrawText(l, new Point((w - l.Width) / 2.0, y));
		y += l.Height + 10.0;
		dc.DrawText(hn, new Point((w - hn.Width) / 2.0, y));
	}

	private static Geometry BuildHills(double period, int bumps, double minH, double maxH, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		double[] heights = new double[bumps];
		for (int i = 0; i < bumps; i++)
		{
			heights[i] = r.Range(minH, maxH);
		}
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext c = g.Open())
		{
			double step = period / bumps;
			// Начало и конец на одной высоте — слой бесшовно повторяется.
			double start = -heights[0] * 0.55;
			c.BeginFigure(new Point(0.0, 40.0), isFilled: true, isClosed: true);
			c.LineTo(new Point(0.0, start), isStroked: false, isSmoothJoin: true);
			for (int i = 0; i < bumps; i++)
			{
				double x0 = i * step;
				double peak = -heights[i];
				double end = i == bumps - 1 ? start : -heights[(i + 1) % bumps] * 0.55;
				c.BezierTo(new Point(x0 + step * 0.25, peak), new Point(x0 + step * 0.55, peak), new Point(x0 + step, end), isStroked: false, isSmoothJoin: true);
			}
			c.LineTo(new Point(period, 40.0), isStroked: false, isSmoothJoin: true);
		}
		g.Freeze();
		return g;
	}

	private static (Geometry City, Geometry Windows) BuildCity(double period, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		StreamGeometry city = new StreamGeometry();
		GeometryGroup windows = new GeometryGroup();
		using (StreamGeometryContext c = city.Open())
		{
			double x = 0.0;
			while (x < period - 30.0)
			{
				double bw = Math.Min(r.Range(34.0, 80.0), period - x);
				double bh = r.Range(40.0, 120.0);
				c.BeginFigure(new Point(x, 0.0), isFilled: true, isClosed: true);
				c.LineTo(new Point(x, -bh), isStroked: false, isSmoothJoin: false);
				if (r.NextDouble() < 0.3)
				{
					c.LineTo(new Point(x + bw / 2.0, -bh - 16.0), isStroked: false, isSmoothJoin: false);
				}
				c.LineTo(new Point(x + bw, -bh), isStroked: false, isSmoothJoin: false);
				c.LineTo(new Point(x + bw, 0.0), isStroked: false, isSmoothJoin: false);
				for (double wy = -bh + 10.0; wy < -12.0; wy += 14.0)
				{
					for (double wx = x + 7.0; wx < x + bw - 10.0; wx += 12.0)
					{
						if (r.NextDouble() < 0.38)
						{
							windows.Children.Add(new RectangleGeometry(new Rect(wx, wy, 5.0, 6.0)));
						}
					}
				}
				x += bw + r.Range(10.0, 70.0);
			}
		}
		city.Freeze();
		windows.Freeze();
		return (city, windows);
	}

	private static Geometry BuildGroundDetails(int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int i = 0; i < 26; i++)
		{
			double x = r.Range(0.0, GroundPeriod - 10.0);
			double y = r.Range(6.0, 48.0);
			if (r.NextDouble() < 0.5)
			{
				g.Children.Add(new RectangleGeometry(new Rect(x, y, r.Range(8.0, 26.0), 1.6)));
			}
			else
			{
				double s = r.Range(1.2, 3.2);
				g.Children.Add(new EllipseGeometry(new Point(x, y), s * 1.4, s));
			}
		}
		g.Freeze();
		return g;
	}

	private static Geometry BuildStars(int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int i = 0; i < 70; i++)
		{
			double s = r.NextDouble() < 0.15 ? 1.8 : 1.0;
			g.Children.Add(new EllipseGeometry(new Point(r.Range(0.0, StarPeriod), r.Range(-220.0, 110.0)), s, s));
		}
		g.Freeze();
		return g;
	}
}
