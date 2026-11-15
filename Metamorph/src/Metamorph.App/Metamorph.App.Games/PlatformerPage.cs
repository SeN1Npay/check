using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.App.Mascot;
using Metamorph.Core.Games;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// «Мета-платформер»: фон, тайлы (без сглаживания — пиксельная чёткость), объекты, Мета и HUD — отдельные слои.
// Размер клетки — целое число физических пикселей, камера снапится к пикселю, поэтому тайлы не «плывут».
internal sealed class PlatformerPage : GamePage
{
	private enum State
	{
		Menu,
		Intro,
		Playing,
		Paused,
		Clear,
		Victory,
		Over
	}

	private enum Fx : byte
	{
		Shard,
		Sparkle,
		Puff,
		Dust,
		CoinPop,
		Ember
	}

	private struct Particle
	{
		public Fx Kind;

		public double X;

		public double Y;

		public double Vx;

		public double Vy;

		public double Life;

		public double Max;

		public double Size;

		public double Rot;
	}

	private struct Popup
	{
		public double X;

		public double Y;

		public double Life;

		public int Value;
	}

	private const double Rows = 15.0;

	private const double MetaSmall = 0.105;

	private const double MetaBig = 0.165;

	private const int EndlessCard = 4;

	private static readonly string[] DeathLines = { "Ой-ой… звёздочки…", "Ай! Не заметила…", "Ну вот… Давай ещё!" };

	private readonly GameLoop _loop;

	private readonly DrawSurface _back;

	private readonly DrawSurface _tiles;

	private readonly DrawSurface _ents;

	private readonly DrawSurface _hud;

	private readonly MascotCanvas _meta = new MascotCanvas
	{
		IsHitTestVisible = false
	};

	private readonly MascotCanvas _head = new MascotCanvas
	{
		IsHitTestVisible = false,
		Width = 36.0,
		Height = 32.0,
		HorizontalAlignment = HorizontalAlignment.Left,
		VerticalAlignment = VerticalAlignment.Top,
		Margin = new Thickness(18.0, 12.0, 0.0, 0.0),
		ClipToBounds = true
	};

	private readonly MascotAnimator _anim = new MascotAnimator(11);

	private readonly MascotColors _colors;

	private readonly MascotColors _fireColors;

	private readonly PlatformerArt _art;

	private readonly TransformPool _poolBack = new TransformPool();

	private readonly TransformPool _poolTiles = new TransformPool();

	private readonly TransformPool _poolEnts = new TransformPool();

	private readonly TransformPool _poolHud = new TransformPool();

	private readonly Particle[] _fx = new Particle[220];

	private readonly Popup[] _popups = new Popup[12];

	private readonly TextSlot[] _popupText = new TextSlot[12];

	private readonly HashSet<Key> _held = new HashSet<Key>();

	private readonly Random _rnd = new Random();

	private readonly Geometry _meadowFar = PlatformerArt.Hills(PlatformerArt.FarPeriod, 5, 50.0, 95.0, 3);

	private readonly Geometry _meadowMid = PlatformerArt.Hills(PlatformerArt.MidPeriod, 4, 18.0, 46.0, 9);

	private readonly Geometry _caveTop = PlatformerArt.Stalactites(PlatformerArt.FarPeriod, 14, 70.0, fromTop: true, 13);

	private readonly Geometry _caveBottom = PlatformerArt.Stalactites(PlatformerArt.FarPeriod, 11, 50.0, fromTop: false, 17);

	private readonly Geometry _caveCrystals = PlatformerArt.Crystals(PlatformerArt.MidPeriod, 5, 21);

	private readonly Geometry _skyIslands = PlatformerArt.Islands(PlatformerArt.FarPeriod, 4, 27);

	private readonly Geometry _skySea = PlatformerArt.Hills(PlatformerArt.MidPeriod, 7, 6.0, 26.0, 31);

	private readonly Geometry _castleWall = PlatformerArt.CastleWall(PlatformerArt.MidPeriod, 37);

	private readonly Geometry _castleWindows = PlatformerArt.Windows(PlatformerArt.MidPeriod, 3, -96.0, 41);

	private readonly Geometry _cloud = PlatformerArt.Cloud();

	private readonly double[] _clouds = { 80, 40, 330, 70, 560, 30, 760, 58 };

	private readonly double[] _crystalGlows;

	private readonly Brush _sun;

	private readonly Brush _sunGlow;

	private readonly Brush _shard;

	private readonly Brush _sparkle;

	private readonly Brush _puff;

	private readonly Brush _dust;

	private readonly Brush _ember;

	private readonly Brush _dim;

	private readonly Brush _introBg;

	private readonly Brush _panel;

	private readonly Pen _panelEdge;

	private readonly Brush _hudPanel;

	private readonly Brush _fg;

	private readonly Brush _muted;

	private readonly Brush _accent;

	private readonly Brush _gold;

	private readonly Brush _danger;

	private readonly Brush _bubbleFill;

	private readonly Pen _bubbleEdge;

	private readonly Brush _bubbleInk;

	private readonly Brush _cardBg;

	private readonly Brush _cardSel;

	private readonly Pen _cardEdge;

	private readonly Pen _cardEdgeSel;

	private readonly Brush _locked;

	private readonly TextSlot _scoreText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _coinText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _livesText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _timeText = new TextSlot(TextSlot.Mono);

	private readonly TextSlot _worldText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _titleText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _lineText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _line2Text = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _hintText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _recordText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _bubbleText = new TextSlot(TextSlot.Ui);

	private readonly TextSlot[] _cardId = new TextSlot[5];

	private readonly TextSlot[] _cardName = new TextSlot[5];

	private readonly string[] _cardIds;

	private readonly string[] _cardNames;

	private PlatformerEngine? _engine;

	private State _state = State.Menu;

	private double _time;

	private double _stateAt;

	private double _idleUntil;

	private int _menuIndex;

	private int _levelIndex;

	private bool _endless;

	private int _stage;

	private ulong _seed;

	private string _worldLabel = "";

	private string _levelTitle = "";

	private string _levelName = "";

	private string _livesLine = "";

	private string _menuBest = "";

	private string _scoreLine = "";

	private int _scoreLineValue = -1;

	private string? _bubbleLine;

	private double _bubbleUntil;

	private double _shake;

	private double _growT = 9.0;

	private PlatformerPower _shownPower;

	private double _runDust;

	private double _emberT;

	private int _timeBonus;

	private bool _record;

	private int _best;

	private int _finalScore;

	private double _ppd = 1.0;

	private double _k = 2.0;

	private double _ts = 32.0;

	private double _camX;

	private double _camY;

	public override string Id => GameHub.Platformer;

	public override string Title => Loc.T("Мета-платформер");

	public PlatformerPage(App app, GamePalette pal)
		: base(app, pal)
	{
		_colors = MascotColors.From(app.Services.Settings.Current.Mascot, pal.Source);
		_fireColors = _colors with
		{
			Outfit = "#E5483A",
			OutfitShade = "#B02F26"
		};
		_art = new PlatformerArt(pal);
		_loop = new GameLoop(Tick);
		_back = new DrawSurface(DrawBack);
		_tiles = new DrawSurface(DrawTiles);
		_ents = new DrawSurface(DrawEnts);
		_hud = new DrawSurface(DrawHud);
		RenderOptions.SetEdgeMode(_tiles, EdgeMode.Aliased);
		Children.Add(_back);
		Children.Add(_tiles);
		Children.Add(_ents);
		Children.Add(_meta);
		Children.Add(_hud);
		Children.Add(_head);
		ClipToBounds = true;
		for (int i = 0; i < _popupText.Length; i++)
		{
			_popupText[i] = new TextSlot(TextSlot.Heavy);
		}
		for (int i = 0; i < 5; i++)
		{
			_cardId[i] = new TextSlot(TextSlot.Heavy);
			_cardName[i] = new TextSlot(TextSlot.Ui);
		}
		_cardIds = new string[5] { "1-1", "1-2", "1-3", "1-4", "∞" };
		_cardNames = new string[5];
		for (int i = 0; i < 4; i++)
		{
			_cardNames[i] = Loc.T(PlatformerLevels.All[i].Name);
		}
		_cardNames[EndlessCard] = Loc.T("Бесконечный режим");
		GameRandom gr = new GameRandom(99);
		_crystalGlows = new double[10];
		for (int i = 0; i < 5; i++)
		{
			_crystalGlows[i * 2] = (i + 0.5) * PlatformerArt.MidPeriod / 5.0 + gr.Range(-20.0, 20.0);
			_crystalGlows[i * 2 + 1] = gr.Range(0.8, 1.4);
		}
		_sun = GamePalette.Solid(GamePalette.Lighten(GamePalette.Vivid(pal.Yellow), 0.2));
		_sunGlow = new RadialGradientBrush(GamePalette.WithAlpha(pal.Yellow, 0.45), GamePalette.WithAlpha(pal.Yellow, 0.0)).AsFrozen();
		_shard = GamePalette.Solid(Color.FromRgb(0xC8, 0x6A, 0x44));
		_sparkle = GamePalette.Solid(Color.FromRgb(0xFF, 0xF6, 0xC8));
		_puff = GamePalette.Solid(Colors.White, 0.85);
		_dust = GamePalette.Solid(GamePalette.Mix(Color.FromRgb(0xE8, 0xD8, 0xC0), pal.Fg, 0.2), 0.8);
		_ember = GamePalette.Solid(Color.FromRgb(0xFF, 0xA0, 0x3A));
		_dim = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.4), 0.55);
		_introBg = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.55), 0.94);
		_panel = GamePalette.Solid(GamePalette.Mix(pal.Surface, pal.Bg, 0.3), 0.93);
		_panelEdge = GamePalette.Pen(pal.Accent, 1.5, 0.6);
		_hudPanel = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.35), 0.45);
		_fg = GamePalette.Solid(Colors.White);
		_muted = GamePalette.Solid(GamePalette.Lighten(pal.Muted, 0.35));
		_accent = GamePalette.Solid(GamePalette.Vivid(pal.Accent));
		_gold = GamePalette.Solid(GamePalette.Vivid(pal.Yellow));
		_danger = GamePalette.Solid(GamePalette.Vivid(pal.Red));
		_bubbleFill = GamePalette.Solid(GamePalette.Lighten(pal.Surface, 0.08), 0.96);
		_bubbleEdge = GamePalette.Pen(pal.Accent, 1.5, 0.7);
		_bubbleInk = GamePalette.Solid(pal.Fg);
		_cardBg = GamePalette.Solid(GamePalette.Mix(pal.Surface, pal.Bg, 0.35), 0.92);
		_cardSel = GamePalette.Solid(GamePalette.Mix(pal.Surface, pal.Accent, 0.22), 0.96);
		_cardEdge = GamePalette.Pen(pal.Fg, 1.0, 0.12);
		_cardEdgeSel = GamePalette.Pen(GamePalette.Vivid(pal.Accent), 2.5);
		_locked = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.5), 0.6);
		_menuIndex = Math.Clamp(GameHub.Progress.Unlocked - 1, 0, 3);
		ShowMenu();
		SetHeadIcon();
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
		GameHub.Scores.Touch(GameHub.Platformer);
		Wake(10.0);
	}

	public override void OnActivated()
	{
		if (_state is State.Menu or State.Over or State.Victory)
		{
			Wake(6.0);
		}
	}

	public override void OnDeactivated()
	{
		_held.Clear();
		if (_state is State.Playing or State.Intro)
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
		SubmitQuit();
		SetEngine(null);
	}

	public override bool OnEscape()
	{
		switch (_state)
		{
		case State.Playing:
		case State.Intro:
			Pause();
			return true;
		case State.Menu:
			return false;
		default:
			SubmitQuit();
			ShowMenu();
			return true;
		}
	}

	public override bool OnKey(Key key, bool down, bool repeat)
	{
		bool game = IsGameKey(key);
		if (down)
		{
			_held.Add(key);
		}
		else
		{
			_held.Remove(key);
		}
		if (!down || repeat)
		{
			return game;
		}
		bool confirm = key is Key.Enter or Key.Space;
		switch (_state)
		{
		case State.Menu:
			Wake(10.0);
			switch (key)
			{
			case Key.Left:
			case Key.A:
				SelectCard(Math.Max(0, _menuIndex - 1));
				return true;
			case Key.Right:
			case Key.D:
			case Key.Tab:
				SelectCard(Math.Min(EndlessCard, _menuIndex + 1));
				return true;
			case Key.D1:
			case Key.D2:
			case Key.D3:
			case Key.D4:
			case Key.D5:
				SelectCard(key - Key.D1);
				StartSelected();
				return true;
			case Key.Enter:
			case Key.Space:
				StartSelected();
				return true;
			}
			return game;
		case State.Intro:
			if (confirm)
			{
				SetState(State.Playing);
			}
			return true;
		case State.Playing:
			if (key == Key.P)
			{
				Pause();
			}
			else if (key == Key.R)
			{
				Restart();
			}
			return game || key is Key.P or Key.R;
		case State.Paused:
			if (key is Key.P or Key.Space or Key.Enter)
			{
				Resume();
			}
			else if (key == Key.R)
			{
				Restart();
			}
			return true;
		case State.Clear:
			if (confirm && _engine?.Phase == PlatformerPhase.Cleared)
			{
				NextLevel();
			}
			return true;
		case State.Over:
		case State.Victory:
			if ((confirm || key == Key.R) && _time - _stateAt > 0.6)
			{
				if (_endless)
				{
					StartEndless();
				}
				else
				{
					StartCampaign(_state == State.Victory ? 0 : _levelIndex);
				}
			}
			return true;
		}
		return game;
	}

	private static bool IsGameKey(Key key)
	{
		return key is Key.Left or Key.Right or Key.Up or Key.Down or Key.A or Key.D or Key.W or Key.S or Key.Space or Key.LeftShift or Key.RightShift or Key.X or Key.LeftCtrl or Key.RightCtrl or Key.Z or Key.Enter;
	}

	private PlatformerInput ComposeInput()
	{
		PlatformerInput input = PlatformerInput.None;
		if (_held.Contains(Key.Left) || _held.Contains(Key.A))
		{
			input |= PlatformerInput.Left;
		}
		if (_held.Contains(Key.Right) || _held.Contains(Key.D))
		{
			input |= PlatformerInput.Right;
		}
		if (_held.Contains(Key.Space) || _held.Contains(Key.Up) || _held.Contains(Key.W))
		{
			input |= PlatformerInput.Jump;
		}
		if (_held.Contains(Key.LeftShift) || _held.Contains(Key.RightShift) || _held.Contains(Key.X))
		{
			input |= PlatformerInput.Run;
		}
		if (_held.Contains(Key.LeftCtrl) || _held.Contains(Key.RightCtrl) || _held.Contains(Key.Z))
		{
			input |= PlatformerInput.Fire;
		}
		if (_held.Contains(Key.Down) || _held.Contains(Key.S))
		{
			input |= PlatformerInput.Down;
		}
		return input;
	}

	// ── Ход игры ──────────────────────────────────────────────────────────

	private void SetEngine(PlatformerEngine? engine)
	{
		if (_engine != null)
		{
			_engine.Event -= OnEngineEvent;
		}
		_engine = engine;
		if (_engine != null)
		{
			_engine.Event += OnEngineEvent;
		}
	}

	private void SetState(State state)
	{
		_state = state;
		_stateAt = _time;
		_head.Visibility = state == State.Menu ? Visibility.Collapsed : Visibility.Visible;
	}

	private void ShowMenu()
	{
		SetState(State.Menu);
		_held.Clear();
		SelectCard(_menuIndex);
		_anim.Play(MascotClip.Wave, restart: true);
		Wake(10.0);
	}

	private void SelectCard(int index)
	{
		_menuIndex = Math.Clamp(index, 0, EndlessCard);
		PlatformerLevel level = _menuIndex == EndlessCard ? PlatformerGenerator.Generate(7, 0) : PlatformerLevels.All[_menuIndex];
		SetEngine(new PlatformerEngine(level));
		_menuBest = _menuIndex == EndlessCard ? Loc.F("Лучший счёт: {0}", GameHub.Scores.Best(GameHub.PlatformerEndless)) + "  ·  " + Loc.F("Лучший этап: {0}", GameHub.Progress.BestStage) : Loc.F("Лучший счёт: {0}", GameHub.Scores.Best(GameHub.Platformer));
		_shownPower = PlatformerPower.Small;
		_anim.Jiggle(0.6);
		Array.Clear(_fx);
		Array.Clear(_popups);
		Render(0.0);
	}

	private void StartSelected()
	{
		if (_menuIndex == EndlessCard)
		{
			StartEndless();
		}
		else if (GameHub.Progress.IsUnlocked(_menuIndex))
		{
			StartCampaign(_menuIndex);
		}
		else
		{
			_anim.Play(MascotClip.Annoyed, restart: true);
			_shake = 4.0;
		}
	}

	private void StartCampaign(int index)
	{
		_endless = false;
		_levelIndex = Math.Clamp(index, 0, PlatformerLevels.Count - 1);
		SetEngine(new PlatformerEngine(PlatformerLevels.All[_levelIndex]));
		_best = GameHub.Scores.Best(GameHub.Platformer);
		BeginLevel();
	}

	private void StartEndless()
	{
		_endless = true;
		_stage = 0;
		_seed = GameRandom.NewSeed();
		SetEngine(new PlatformerEngine(PlatformerGenerator.Generate(_seed, _stage)));
		_best = GameHub.Scores.Best(GameHub.PlatformerEndless);
		BeginLevel();
	}

	private void BeginLevel()
	{
		PlatformerEngine e = _engine!;
		_record = false;
		_timeBonus = 0;
		_bubbleLine = null;
		_shownPower = e.Power;
		_growT = 9.0;
		Array.Clear(_fx);
		Array.Clear(_popups);
		_held.Clear();
		_levelName = Loc.T(e.Level.Name);
		_levelTitle = _endless ? Loc.F("Мир {0}", "∞-" + (_stage + 1)) : Loc.F("Мир {0}", e.Level.Id);
		_worldLabel = _endless ? "∞-" + (_stage + 1) : Loc.F("Мир {0}", e.Level.Id);
		_livesLine = "Мета × " + e.Lives;
		_anim.Play(MascotClip.Idle, restart: true);
		SetState(State.Intro);
		_loop.Start();
	}

	private void NextLevel()
	{
		PlatformerEngine e = _engine!;
		if (_endless)
		{
			_stage++;
			GameHub.Progress.ReachStage(_stage + 1);
			e.LoadLevel(PlatformerGenerator.Generate(_seed, _stage), keepStats: true);
			BeginLevel();
			return;
		}
		GameHub.Progress.Complete(_levelIndex);
		if (_levelIndex >= PlatformerLevels.Count - 1)
		{
			Finish(State.Victory);
			return;
		}
		_levelIndex++;
		e.LoadLevel(PlatformerLevels.All[_levelIndex], keepStats: true);
		BeginLevel();
	}

	private void Finish(State state)
	{
		PlatformerEngine e = _engine!;
		_finalScore = e.Score;
		string id = _endless ? GameHub.PlatformerEndless : GameHub.Platformer;
		_record = GameHub.Submit(App, id, _finalScore);
		_best = GameHub.Scores.Best(id);
		_menuBest = Loc.F("Лучший счёт: {0}", _best);
		if (_endless)
		{
			GameHub.Progress.ReachStage(_stage + 1);
		}
		SetState(state);
		_anim.Play(state == State.Victory ? MascotClip.Dance : MascotClip.Dizzy, restart: true);
		if (_record)
		{
			Say(Loc.T("Новый рекорд! Ура-а!"), 3.5);
		}
		Wake(5.0);
	}

	// Выход посреди партии — результат всё равно попадает в рекорды.
	private void SubmitQuit()
	{
		if (_engine != null && _state is State.Intro or State.Playing or State.Paused or State.Clear && _engine.Score > 0)
		{
			GameHub.Submit(App, _endless ? GameHub.PlatformerEndless : GameHub.Platformer, _engine.Score);
		}
	}

	private void Pause()
	{
		_held.Clear();
		SetState(State.Paused);
		_loop.Stop();
		Render(0.0);
	}

	private void Resume()
	{
		SetState(State.Playing);
		_loop.Start();
	}

	private void Restart()
	{
		_engine?.RestartLevel();
		BeginLevel();
		SetState(State.Playing);
	}

	private void Wake(double seconds)
	{
		_idleUntil = _time + seconds;
		_loop.Start();
	}

	private void Tick(double dt)
	{
		_time += dt;
		PlatformerEngine? e = _engine;
		switch (_state)
		{
		case State.Intro:
			if (_time - _stateAt > 2.3)
			{
				SetState(State.Playing);
			}
			break;
		case State.Playing:
			if (e != null)
			{
				e.SetInput(ComposeInput());
				e.Update(dt);
				if (e.Phase == PlatformerPhase.Cleared || (e.Phase == PlatformerPhase.Clearing && e.ClearStage == PlatformerClearStage.Tally))
				{
					SetState(State.Clear);
				}
			}
			break;
		case State.Clear:
			if (e != null)
			{
				e.Update(dt);
				if (e.Phase == PlatformerPhase.Cleared && _time - _stateAt > 7.0)
				{
					NextLevel();
				}
			}
			break;
		}
		Render(dt);
		bool live = _state is State.Intro or State.Playing or State.Clear;
		if (!live && _time > _idleUntil)
		{
			_loop.Stop();
		}
	}

	private void OnEngineEvent(PlatformerEvent ev)
	{
		switch (ev.Kind)
		{
		case PlatformerEventKind.Jump:
			_anim.Play(MascotClip.Jump, restart: true);
			Burst(Fx.Dust, ev.X, ev.Y, 4, 40.0);
			break;
		case PlatformerEventKind.Land:
			_anim.Impact(Math.Min(1.0, ev.Value / 380.0));
			if (ev.Value > 160)
			{
				Burst(Fx.Dust, ev.X, ev.Y, 6, 60.0);
			}
			break;
		case PlatformerEventKind.Coin:
			Burst(Fx.Sparkle, ev.X, ev.Y, 5, 50.0);
			break;
		case PlatformerEventKind.BlockCoin:
			Spawn(Fx.CoinPop, ev.X, ev.Y - 8.0, 0.0, -300.0, 0.55, 1.0);
			PopupAt(ev.X, ev.Y - 30.0, 200);
			break;
		case PlatformerEventKind.PowerUpAppear:
			Burst(Fx.Sparkle, ev.X, ev.Y, 6, 40.0);
			break;
		case PlatformerEventKind.PowerUp:
			_growT = 0.0;
			Burst(Fx.Sparkle, _engine!.MetaX, _engine.MetaTop, 14, 90.0);
			PopupAt(ev.X, ev.Y - 10.0, 1000);
			_anim.Play(MascotClip.Happy, restart: true);
			Say(Loc.T(ev.Value == (int)PlatformerPower.Fire ? "Огненный леденец! Ловите!" : "Сахарная звезда! Я выросла~"), 2.2);
			break;
		case PlatformerEventKind.Stomp:
			Burst(Fx.Puff, ev.X, ev.Y, 5, 50.0);
			PopupAt(ev.X, ev.Y - 8.0, ev.Value);
			_anim.Jiggle(0.8);
			break;
		case PlatformerEventKind.Kick:
			Burst(Fx.Puff, ev.X, ev.Y + 6.0, 4, 60.0);
			PopupAt(ev.X, ev.Y - 8.0, ev.Value);
			break;
		case PlatformerEventKind.Knock:
			Burst(Fx.Sparkle, ev.X, ev.Y, 6, 70.0);
			if (ev.Value > 0)
			{
				PopupAt(ev.X, ev.Y - 8.0, ev.Value);
			}
			break;
		case PlatformerEventKind.Hurt:
			_shake = Math.Max(_shake, 3.0);
			_growT = 0.0;
			_anim.Play(MascotClip.Surprised, restart: true);
			Say(Loc.T("Ай-ай!"), 1.2);
			break;
		case PlatformerEventKind.Death:
			_shake = Math.Max(_shake, 5.0);
			_anim.Play(MascotClip.Dizzy, restart: true);
			_anim.Impact(1.0);
			Say(Loc.T(_engine?.TimeLeft <= 0.0 ? "Время вышло!" : DeathLines[_rnd.Next(DeathLines.Length)]), 2.2);
			break;
		case PlatformerEventKind.BlockBreak:
			for (int i = 0; i < 4; i++)
			{
				Spawn(Fx.Shard, ev.X + ((i & 1) == 0 ? -4.0 : 4.0), ev.Y + (i < 2 ? -4.0 : 4.0), (i & 1) == 0 ? -70.0 : 70.0, i < 2 ? -330.0 : -220.0, 1.1, 4.0);
			}
			_shake = Math.Max(_shake, 1.5);
			break;
		case PlatformerEventKind.Bonk:
			Burst(Fx.Dust, ev.X, ev.Y, 2, 20.0);
			break;
		case PlatformerEventKind.Throw:
			Burst(Fx.Sparkle, ev.X, ev.Y, 3, 40.0);
			break;
		case PlatformerEventKind.CandyPop:
			Burst(Fx.Puff, ev.X, ev.Y, 3, 30.0);
			break;
		case PlatformerEventKind.ExtraLife:
			PopupAt(ev.X, ev.Y - 16.0, -1);
			Say(Loc.T("Ещё жизнь!"), 1.6);
			break;
		case PlatformerEventKind.Checkpoint:
			Burst(Fx.Sparkle, ev.X, ev.Y - 20.0, 10, 60.0);
			Say(Loc.T("Чекпоинт!"), 1.4);
			break;
		case PlatformerEventKind.HurryUp:
			Say(Loc.T("Скорее, время!"), 2.0);
			break;
		case PlatformerEventKind.BossHit:
			_shake = 7.0;
			Burst(Fx.Puff, ev.X, ev.Y, 8, 80.0);
			PopupAt(ev.X, ev.Y - 10.0, ev.Value);
			Say(Loc.T("Получай, большой слайм!"), 1.6);
			break;
		case PlatformerEventKind.BossDefeated:
			_shake = 12.0;
			Burst(Fx.Sparkle, ev.X, ev.Y, 24, 160.0);
			PopupAt(ev.X, ev.Y - 10.0, ev.Value);
			_anim.Play(MascotClip.Happy, restart: true);
			Say(Loc.T("Победа! Ворота открыты!"), 2.6);
			break;
		case PlatformerEventKind.Goal:
			PopupAt(ev.X, ev.Y - 8.0, ev.Value);
			Burst(Fx.Sparkle, ev.X, ev.Y, 12, 90.0);
			Say(Loc.T("Флаг мой!"), 2.0);
			break;
		case PlatformerEventKind.TimeBonus:
			_timeBonus = ev.Value;
			break;
		case PlatformerEventKind.LevelClear:
			_anim.Play(MascotClip.Happy, restart: true);
			break;
		case PlatformerEventKind.GameOver:
			Finish(State.Over);
			break;
		case PlatformerEventKind.Respawn:
			_shownPower = PlatformerPower.Small;
			BeginLevel();
			break;
		}
	}

	private void Say(string line, double seconds)
	{
		_bubbleLine = line;
		_bubbleUntil = _time + seconds;
	}

	// ── Частицы (мировые координаты) ─────────────────────────────────────

	private void Spawn(Fx kind, double x, double y, double vx, double vy, double life, double size)
	{
		for (int i = 0; i < _fx.Length; i++)
		{
			if (_fx[i].Life <= 0.0)
			{
				_fx[i] = new Particle
				{
					Kind = kind,
					X = x,
					Y = y,
					Vx = vx,
					Vy = vy,
					Life = life,
					Max = life,
					Size = size,
					Rot = _rnd.NextDouble() * 6.28
				};
				return;
			}
		}
	}

	private void Burst(Fx kind, double x, double y, int count, double power)
	{
		for (int n = 0; n < count; n++)
		{
			double a = _rnd.NextDouble() * Math.PI * 2.0;
			double p = power * (0.4 + 0.6 * _rnd.NextDouble());
			double vy = Math.Sin(a) * p;
			if (kind == Fx.Dust)
			{
				vy = -Math.Abs(vy) * 0.4 - 10.0;
			}
			Spawn(kind, x, y, Math.Cos(a) * p, vy, kind == Fx.Puff ? 0.35 : 0.45 + _rnd.NextDouble() * 0.35, kind == Fx.Puff ? 3.0 + _rnd.NextDouble() * 3.0 : 1.5 + _rnd.NextDouble() * 2.0);
		}
	}

	private void PopupAt(double x, double y, int value)
	{
		int slot = 0;
		double least = double.MaxValue;
		for (int i = 0; i < _popups.Length; i++)
		{
			if (_popups[i].Life < least)
			{
				least = _popups[i].Life;
				slot = i;
			}
		}
		_popups[slot] = new Popup
		{
			X = x,
			Y = y,
			Life = 0.9,
			Value = value
		};
	}

	private void UpdateParticles(double dt)
	{
		PlatformerEngine? e = _engine;
		if (e != null && _state is State.Playing && e.Phase == PlatformerPhase.Playing)
		{
			if (e.OnGround && Math.Abs(e.MetaVx) > 110.0)
			{
				_runDust -= dt;
				if (_runDust <= 0.0)
				{
					_runDust = 0.09;
					Spawn(Fx.Dust, e.MetaX - e.Facing * 4.0, e.MetaBottom, -e.Facing * 20.0, -14.0, 0.4, 1.6);
				}
			}
			if (e.Power == PlatformerPower.Fire && _rnd.NextDouble() < dt * 6.0)
			{
				Spawn(Fx.Sparkle, e.MetaX + (_rnd.NextDouble() - 0.5) * 14.0, e.MetaTop + _rnd.NextDouble() * e.MetaHeight, 0.0, -20.0, 0.5, 1.4);
			}
		}
		if (e != null && e.Level.World == PlatformerWorld.Castle && _state != State.Menu)
		{
			_emberT -= dt;
			if (_emberT <= 0.0)
			{
				_emberT = 0.12;
				Spawn(Fx.Ember, e.CameraX + _rnd.NextDouble() * e.ViewWidth, e.Height * 16.0 - 4.0, (_rnd.NextDouble() - 0.5) * 20.0, -30.0 - _rnd.NextDouble() * 40.0, 2.5, 1.2 + _rnd.NextDouble());
			}
		}
		for (int i = 0; i < _fx.Length; i++)
		{
			ref Particle p = ref _fx[i];
			if (p.Life <= 0.0)
			{
				continue;
			}
			p.Life -= dt;
			p.X += p.Vx * dt;
			p.Y += p.Vy * dt;
			p.Rot += dt * 8.0;
			switch (p.Kind)
			{
			case Fx.Shard:
			case Fx.CoinPop:
				p.Vy += 900.0 * dt;
				break;
			case Fx.Sparkle:
			case Fx.Puff:
				p.Vx *= 1.0 - Math.Min(1.0, dt * 5.0);
				p.Vy *= 1.0 - Math.Min(1.0, dt * 5.0);
				break;
			case Fx.Dust:
				p.Vy += 30.0 * dt;
				break;
			case Fx.Ember:
				p.Vx += Math.Sin(_time * 3.0 + i) * 20.0 * dt;
				break;
			}
		}
		for (int i = 0; i < _popups.Length; i++)
		{
			if (_popups[i].Life > 0.0)
			{
				_popups[i].Life -= dt;
				_popups[i].Y -= 26.0 * dt;
			}
		}
	}

	// ── Кадр ──────────────────────────────────────────────────────────────

	private void Render(double dt)
	{
		double w = ActualWidth;
		double h = ActualHeight;
		if (w < 4.0 || h < 4.0)
		{
			return;
		}
		double ppd = _ppd > 0.0 ? _ppd : 1.0;
		_ts = Math.Max(8.0, Math.Floor(h * ppd / Rows)) / ppd;
		_k = _ts / 16.0;
		PlatformerEngine? e = _engine;
		if (e != null)
		{
			e.ViewWidth = w / _k;
			e.ViewHeight = h / _k;
			_shake = Math.Max(0.0, _shake - dt * 18.0);
			double sx = _shake > 0.2 ? Math.Sin(_time * 63.0) * _shake : 0.0;
			double sy = _shake > 0.2 ? Math.Cos(_time * 49.0) * _shake * 0.6 : 0.0;
			_camX = Math.Round((e.CameraX * _k + sx) * ppd) / ppd;
			_camY = Math.Round((e.CameraY * _k + sy) * ppd) / ppd;
		}
		UpdateParticles(dt);
		UpdateMeta(dt);
		_back.InvalidateVisual();
		_tiles.InvalidateVisual();
		_ents.InvalidateVisual();
		_hud.InvalidateVisual();
	}

	private void UpdateMeta(double dt)
	{
		PlatformerEngine? e = _engine;
		if (e == null)
		{
			_meta.Visibility = Visibility.Collapsed;
			return;
		}
		_meta.Visibility = Visibility.Visible;
		double rate = 1.0;
		double sx = 1.0;
		double sy = 1.0;
		if (_state == State.Menu || _state == State.Intro)
		{
			if (_anim.Clip == MascotClip.Wave && _anim.ClipTime > 2.4)
			{
				_anim.Play(MascotClip.Idle);
			}
			if (_anim.Clip == MascotClip.Annoyed && _anim.ClipTime > 1.2)
			{
				_anim.Play(MascotClip.Idle);
			}
			_anim.VelocityX = 0.0;
			_anim.VelocityY = 0.0;
		}
		else
		{
			switch (e.Phase)
			{
			case PlatformerPhase.Playing:
				if (_anim.Clip is MascotClip.Happy or MascotClip.Surprised && _anim.ClipTime < 0.7)
				{
					break;
				}
				if (e.OnGround)
				{
					if (e.Ducking)
					{
						_anim.Play(MascotClip.Idle);
						sx = 1.12;
						sy = 0.72;
					}
					else if (Math.Abs(e.MetaVx) > 4.0)
					{
						_anim.Play(MascotClip.Walk);
						rate = 0.8 + 2.0 * Math.Abs(e.MetaVx) / PlatformerEngine.RunSpeed;
					}
					else
					{
						_anim.Play(MascotClip.Idle);
					}
				}
				else
				{
					_anim.Play(e.MetaVy < 0.0 ? MascotClip.Jump : MascotClip.Fall);
				}
				break;
			case PlatformerPhase.Dying:
			case PlatformerPhase.GameOver:
				_anim.Play(MascotClip.Dizzy);
				break;
			case PlatformerPhase.Clearing:
				if (e.ClearStage == PlatformerClearStage.Slide)
				{
					_anim.Play(MascotClip.Held);
				}
				else if (e.ClearStage == PlatformerClearStage.Walk && Math.Abs(e.MetaVx) > 1.0)
				{
					_anim.Play(MascotClip.Walk);
					rate = 1.4;
				}
				else
				{
					_anim.Play(MascotClip.Happy);
				}
				break;
			case PlatformerPhase.Cleared:
				_anim.Play(_state == State.Victory ? MascotClip.Dance : MascotClip.Happy);
				break;
			}
			_anim.VelocityX = e.MetaVx * 2.0;
			_anim.VelocityY = e.MetaVy * 1.2;
		}
		_anim.Facing = e.Facing;
		_anim.LookX = e.Facing * 0.7;
		_anim.LookY = e.OnGround ? 0.1 : (e.MetaVy < 0.0 ? -0.4 : 0.3);
		MascotPose pose = _anim.Update(dt * rate);
		if (e.Power != _shownPower && _growT > 0.65)
		{
			_shownPower = e.Power;
		}
		_growT += dt;
		bool big = e.Power != PlatformerPower.Small;
		bool wasBig = _shownPower != PlatformerPower.Small;
		double scale = big ? MetaBig : MetaSmall;
		if (_growT < 0.65 && big != wasBig)
		{
			// Мигание между размерами при росте/уменьшении.
			scale = ((int)(_growT * 14.0) & 1) == 0 ? (wasBig ? MetaBig : MetaSmall) : scale;
		}
		double ms = _k * scale;
		double fx = e.MetaX * _k - _camX;
		double fy = e.MetaBottom * _k - _camY + _k * 0.5;
		_meta.Opacity = e.Invincible > 0.0 && ((int)(_time * 18.0) & 1) == 0 ? 0.25 : 1.0;
		_meta.SetShadow(default, 0.0, 0.0);
		_meta.SetItems(MascotRig.Render(pose, e.Power == PlatformerPower.Fire ? _fireColors : _colors, Affine.Scale(ms * sx, ms * sy).Then(Affine.Translate(fx, fy))));
	}

	private void SetHeadIcon()
	{
		MascotAnimator a = new MascotAnimator(5)
		{
			Facing = 1.0
		};
		a.Play(MascotClip.Happy, restart: true);
		MascotPose pose = a.Update(0.05);
		for (int i = 0; i < 4; i++)
		{
			pose = a.Update(0.05);
		}
		pose.Fx.Clear();
		_head.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(0.25, 0.25).Then(Affine.Translate(18.0, 58.0))));
	}

	// ── Слои ──────────────────────────────────────────────────────────────

	private PlatformerWorld World => _engine?.Level.World ?? PlatformerWorld.Meadow;

	private void Layer(DrawingContext dc, TransformPool pool, Geometry g, Brush brush, double period, double parallax, double w, double baseY, double drift = 0.0)
	{
		double worldX = (_engine?.CameraX ?? 0.0) * parallax + drift;
		double shift = (worldX % period + period) % period;
		for (int copy = 0; copy < 8; copy++)
		{
			double x = (copy * period - shift) * _k;
			if (x > w)
			{
				break;
			}
			dc.PushTransform(pool.Next(_k, x, baseY));
			dc.DrawGeometry(brush, null, g);
			dc.Pop();
		}
	}

	private void DrawBack(DrawingContext dc, double w, double h)
	{
		TransformPool pool = _poolBack;
		pool.Reset();
		PlatformerTheme th = _art.Theme(World);
		dc.DrawRectangle(th.Sky, null, new Rect(0.0, 0.0, w, h));
		double k = _k;
		switch (World)
		{
		case PlatformerWorld.Meadow:
			Sun(dc, w, h);
			Clouds(dc, pool, th, w, h, 0.08, 1.0);
			Layer(dc, pool, _meadowFar, th.Far, PlatformerArt.FarPeriod, 0.18, w, h - 36.0 * k);
			Layer(dc, pool, _meadowMid, th.Mid, PlatformerArt.MidPeriod, 0.4, w, h - 30.0 * k);
			break;
		case PlatformerWorld.Cave:
		{
			Layer(dc, pool, _caveTop, th.Far, PlatformerArt.FarPeriod, 0.2, w, 30.0 * k);
			Layer(dc, pool, _caveBottom, th.Far, PlatformerArt.FarPeriod, 0.2, w, h - 30.0 * k);
			double shift = ((_engine?.CameraX ?? 0.0) * 0.45 % PlatformerArt.MidPeriod + PlatformerArt.MidPeriod) % PlatformerArt.MidPeriod;
			for (int copy = 0; copy < 6; copy++)
			{
				double baseX = (copy * PlatformerArt.MidPeriod - shift) * k;
				if (baseX > w)
				{
					break;
				}
				for (int i = 0; i < _crystalGlows.Length; i += 2)
				{
					double gx = baseX + _crystalGlows[i] * k;
					if (gx < -60.0 * k || gx > w + 60.0 * k)
					{
						continue;
					}
					double pulse = 0.75 + 0.25 * Math.Sin(_time * 1.7 + i);
					double r = 34.0 * k * _crystalGlows[i + 1] * pulse;
					dc.DrawEllipse(th.Glow, null, new Point(gx, h - 40.0 * k), r, r);
				}
			}
			Layer(dc, pool, _caveCrystals, th.MidDetail, PlatformerArt.MidPeriod, 0.45, w, h - 28.0 * k);
			break;
		}
		case PlatformerWorld.Sky:
			Sun(dc, w, h);
			Layer(dc, pool, _skyIslands, th.Far, PlatformerArt.FarPeriod, 0.15, w, h - 20.0 * k);
			Clouds(dc, pool, th, w, h, 0.25, 1.3);
			Layer(dc, pool, _skySea, th.Mid, PlatformerArt.MidPeriod, 0.35, w, h - 8.0 * k, _time * 3.0);
			break;
		case PlatformerWorld.Castle:
		{
			Layer(dc, pool, _castleWall, th.Mid, PlatformerArt.MidPeriod, 0.3, w, h - 32.0 * k);
			dc.PushOpacity(0.7 + 0.3 * Math.Sin(_time * 2.3));
			Layer(dc, pool, _castleWindows, th.MidDetail, PlatformerArt.MidPeriod, 0.3, w, h - 32.0 * k);
			dc.Pop();
			double glow = h * (0.32 + 0.03 * Math.Sin(_time * 1.6));
			dc.DrawRectangle(_art.LavaGlow, null, new Rect(0.0, h - glow, w, glow));
			break;
		}
		}
	}

	private void Sun(DrawingContext dc, double w, double h)
	{
		Point c = new Point(w * 0.82, h * 0.2);
		double r = 20.0 * _k;
		dc.DrawEllipse(_sunGlow, null, c, r * 2.6, r * 2.6);
		dc.DrawEllipse(_sun, null, c, r, r);
	}

	private void Clouds(DrawingContext dc, TransformPool pool, PlatformerTheme th, double w, double h, double parallax, double size)
	{
		double shift = (((_engine?.CameraX ?? 0.0) * parallax + _time * 5.0) % PlatformerArt.CloudPeriod + PlatformerArt.CloudPeriod) % PlatformerArt.CloudPeriod;
		for (int copy = 0; copy < 6; copy++)
		{
			double baseX = (copy * PlatformerArt.CloudPeriod - shift) * _k;
			if (baseX > w)
			{
				break;
			}
			for (int i = 0; i < _clouds.Length; i += 2)
			{
				double cx = baseX + _clouds[i] * _k;
				if (cx < -60.0 * _k || cx > w + 60.0 * _k)
				{
					continue;
				}
				dc.PushTransform(pool.Next(_k * size * (0.6 + 0.1 * (i % 3)), cx, _clouds[i + 1] * _k));
				dc.DrawGeometry(th.Cloud, null, _cloud);
				dc.Pop();
			}
		}
	}

	private void DrawTiles(DrawingContext dc, double w, double h)
	{
		PlatformerEngine? e = _engine;
		if (e == null)
		{
			return;
		}
		TransformPool pool = _poolTiles;
		pool.Reset();
		PlatformerWorld world = e.Level.World;
		PlatformerTheme th = _art.Theme(world);
		double ts = _ts;
		int c0 = Math.Max(0, (int)Math.Floor(_camX / ts) - 1);
		int c1 = Math.Min(e.Width - 1, (int)Math.Ceiling((_camX + w) / ts));
		int r0 = Math.Max(0, (int)Math.Floor(_camY / ts) - 1);
		int r1 = Math.Min(e.Height - 1, (int)Math.Ceiling((_camY + h) / ts));
		ReadOnlySpan<PlatformerTile> tiles = e.Tiles;
		int width = e.Width;
		for (int r = r0; r <= r1; r++)
		{
			double y = r * ts - _camY;
			for (int c = c0; c <= c1; c++)
			{
				PlatformerTile t = tiles[r * width + c];
				if (t == PlatformerTile.Empty || t == PlatformerTile.Coin)
				{
					continue;
				}
				double x = c * ts - _camX;
				double bump = e.BumpOffset(c, r);
				double by = bump != 0.0 ? Math.Round(bump * _k * _ppd) / _ppd : 0.0;
				PlatformerTile above = r > 0 ? tiles[(r - 1) * width + c] : PlatformerTile.Empty;
				bool open = !PlatformerLevel.IsSolid(above) || above == PlatformerTile.Door;
				_art.Tile(dc, pool, th, world, t, x, y + by, ts, c, r, open, above != PlatformerTile.Lava, _time);
			}
		}
	}

	private void DrawEnts(DrawingContext dc, double w, double h)
	{
		PlatformerEngine? e = _engine;
		if (e == null)
		{
			return;
		}
		TransformPool pool = _poolEnts;
		pool.Reset();
		double k = _k;
		double ts = _ts;
		PlatformerWorld world = e.Level.World;
		PlatformerTheme th = _art.Theme(world);
		int c0 = Math.Max(0, (int)Math.Floor(_camX / ts) - 1);
		int c1 = Math.Min(e.Width - 1, (int)Math.Ceiling((_camX + w) / ts));
		ReadOnlySpan<PlatformerTile> tiles = e.Tiles;
		int width = e.Width;
		for (int r = 0; r < e.Height; r++)
		{
			for (int c = c0; c <= c1; c++)
			{
				PlatformerTile t = tiles[r * width + c];
				if (t == PlatformerTile.Coin)
				{
					_art.Coin(dc, (c + 0.5) * ts - _camX, (r + 0.5) * ts - _camY, k, _time * 4.0 + c * 0.45);
				}
				else if (t == PlatformerTile.Lava && (r == 0 || tiles[(r - 1) * width + c] != PlatformerTile.Lava))
				{
					dc.DrawRectangle(_art.LavaGlow, null, new Rect(c * ts - _camX, r * ts - _camY - ts * 1.5, ts, ts * 1.5));
				}
			}
		}
		Level(dc, pool, e, k);
		ReadOnlySpan<PlatformerPlatform> platforms = e.Platforms;
		for (int i = 0; i < platforms.Length; i++)
		{
			ref readonly PlatformerPlatform p = ref platforms[i];
			double px = Math.Round((p.X * k - _camX) * _ppd) / _ppd;
			double py = Math.Round((p.Y * k - _camY) * _ppd) / _ppd;
			int segs = (int)Math.Round(p.W / 16.0);
			for (int s = 0; s < segs; s++)
			{
				_art.Plank(dc, th, world, px + s * ts, py, ts, s);
			}
		}
		ReadOnlySpan<PlatformerItem> items = e.Items;
		for (int i = 0; i < items.Length; i++)
		{
			ref readonly PlatformerItem it = ref items[i];
			if (it.Alive)
			{
				_art.Item(dc, pool, in it, (it.X + 6.0) * k - _camX, (it.Y + 12.0) * k - _camY, k, _time);
			}
		}
		ReadOnlySpan<PlatformerEnemy> enemies = e.Enemies;
		for (int i = 0; i < enemies.Length; i++)
		{
			ref readonly PlatformerEnemy en = ref enemies[i];
			if (en.State == PlatformerEnemyState.Gone || !en.Active && en.X * k - _camX > w + 40.0)
			{
				continue;
			}
			double sx = (en.X + en.W / 2.0) * k - _camX;
			if (sx < -60.0 * k || sx > w + 60.0 * k)
			{
				continue;
			}
			_art.Enemy(dc, pool, in en, sx, (en.Y + en.H) * k - _camY, k, _time);
		}
		ReadOnlySpan<PlatformerCandy> candies = e.Candies;
		for (int i = 0; i < candies.Length; i++)
		{
			if (candies[i].Alive)
			{
				_art.Candy(dc, pool, (candies[i].X + 3.0) * k - _camX, (candies[i].Y + 3.0) * k - _camY, k, _time * 14.0 * Math.Sign(candies[i].Vx));
			}
		}
		ReadOnlySpan<PlatformerFireBar> bars = e.FireBars;
		for (int i = 0; i < bars.Length; i++)
		{
			ref readonly PlatformerFireBar b = ref bars[i];
			double cos = Math.Cos(b.Angle);
			double sin = Math.Sin(b.Angle);
			for (int n = 1; n < b.Count; n++)
			{
				_art.FireBall(dc, (b.Cx + cos * n * 8.0) * k - _camX, (b.Cy + sin * n * 8.0) * k - _camY, k);
			}
		}
	}

	// Флаг/кристалл финиша и чекпоинт.
	private void Level(DrawingContext dc, TransformPool pool, PlatformerEngine e, double k)
	{
		PlatformerLevel level = e.Level;
		double gx = (level.GoalCol * 16.0 + 8.0) * k - _camX;
		double bottom = (level.GoalRow + 1) * 16.0 * k - _camY;
		if (level.World == PlatformerWorld.Castle)
		{
			_art.Gem(dc, pool, gx, bottom, k, _time);
		}
		else
		{
			double top = e.PoleTop * 16.0 * k - _camY;
			double flagY = top + 4.0 * k;
			if (e.Phase == PlatformerPhase.Clearing && e.ClearStage == PlatformerClearStage.Slide)
			{
				flagY = Math.Clamp(e.MetaTop * k - _camY, flagY, bottom - 12.0 * k);
			}
			else if (e.Phase is PlatformerPhase.Clearing or PlatformerPhase.Cleared)
			{
				flagY = bottom - 12.0 * k;
			}
			_art.Flag(dc, pool, gx, top, bottom, flagY, k, _time);
		}
		if (level.CheckpointCol >= 0)
		{
			_art.Checkpoint(dc, pool, (level.CheckpointCol * 16.0 + 6.0) * k - _camX, (level.CheckpointRow + 1) * 16.0 * k - _camY, k, e.CheckpointReached, _time);
		}
	}

	private void DrawHud(DrawingContext dc, double w, double h)
	{
		TransformPool pool = _poolHud;
		pool.Reset();
		double k = _k;
		PlatformerEngine? e = _engine;
		for (int i = 0; i < _fx.Length; i++)
		{
			ref Particle p = ref _fx[i];
			if (p.Life <= 0.0)
			{
				continue;
			}
			double a = Math.Clamp(p.Life / p.Max, 0.0, 1.0);
			double x = p.X * k - _camX;
			double y = p.Y * k - _camY;
			if (x < -20.0 || x > w + 20.0 || y < -20.0 || y > h + 20.0)
			{
				continue;
			}
			switch (p.Kind)
			{
			case Fx.Shard:
			{
				double s = p.Size * k;
				double c = Math.Cos(p.Rot) * s;
				double sn = Math.Sin(p.Rot) * s;
				dc.PushTransform(pool.Next(c, sn, -sn, c, x, y));
				dc.DrawRectangle(_shard, null, new Rect(-0.5, -0.5, 1.0, 1.0));
				dc.Pop();
				break;
			}
			case Fx.Sparkle:
			{
				double s = p.Size * k * (0.5 + 0.5 * Math.Sin(p.Rot * 2.0) * a + 0.5 * a);
				dc.PushOpacity(a);
				dc.DrawRectangle(_sparkle, null, new Rect(x - s * 0.2, y - s, s * 0.4, s * 2.0));
				dc.DrawRectangle(_sparkle, null, new Rect(x - s, y - s * 0.2, s * 2.0, s * 0.4));
				dc.Pop();
				break;
			}
			case Fx.Puff:
			{
				double r = p.Size * k * (1.6 - 0.6 * a);
				dc.PushOpacity(a * 0.9);
				dc.DrawEllipse(_puff, null, new Point(x, y), r, r);
				dc.Pop();
				break;
			}
			case Fx.Dust:
			{
				double r = p.Size * k * (1.5 - 0.5 * a);
				dc.PushOpacity(a * 0.7);
				dc.DrawEllipse(_dust, null, new Point(x, y - r * 0.4), r, r * 0.8);
				dc.Pop();
				break;
			}
			case Fx.CoinPop:
				_art.Coin(dc, x, y, k, p.Rot * 2.0);
				break;
			case Fx.Ember:
				dc.PushOpacity(a);
				dc.DrawRectangle(_ember, null, new Rect(x, y, p.Size * k, p.Size * k));
				dc.Pop();
				break;
			}
		}
		double ppd = _ppd;
		for (int i = 0; i < _popups.Length; i++)
		{
			ref Popup p = ref _popups[i];
			if (p.Life <= 0.0)
			{
				continue;
			}
			FormattedText t = p.Value < 0 ? _popupText[i].Get("1UP", 7.0 * k, _accent, ppd) : _popupText[i].Number(p.Value, "0", 6.0 * k, _fg, ppd);
			dc.PushOpacity(Math.Clamp(p.Life * 2.5, 0.0, 1.0));
			dc.DrawText(t, new Point(p.X * k - _camX - t.Width / 2.0, p.Y * k - _camY - t.Height / 2.0));
			dc.Pop();
		}
		if (e != null && _bubbleLine != null && _time < _bubbleUntil && _state != State.Menu)
		{
			FormattedText line = _bubbleText.Get(_bubbleLine, 15.0, _bubbleInk, ppd, 240.0);
			double top = (e.MetaTop - 8.0) * k - _camY;
			GameDraw.Bubble(dc, line, e.MetaX * k - _camX, Math.Max(top, line.Height + 40.0), _bubbleFill, _bubbleEdge, Math.Clamp((_bubbleUntil - _time) * 3.0, 0.0, 1.0), 10.0, w - 10.0);
		}
		if (e != null && _state != State.Menu)
		{
			DrawStatus(dc, e, w, ppd);
		}
		switch (_state)
		{
		case State.Menu:
			DrawMenu(dc, w, h);
			break;
		case State.Intro:
		{
			double a = Math.Clamp(Math.Min((_time - _stateAt) * 5.0, (2.3 - (_time - _stateAt)) * 4.0), 0.0, 1.0);
			dc.PushOpacity(a);
			dc.DrawRectangle(_introBg, null, new Rect(0.0, 0.0, w, h));
			FormattedText title = _titleText.Get(_levelTitle, 40.0, _fg, ppd);
			FormattedText name = _lineText.Get(_levelName, 20.0, _accent, ppd);
			FormattedText lives = _line2Text.Get(_livesLine, 17.0, _muted, ppd);
			double y = h * 0.36;
			dc.DrawText(title, new Point((w - title.Width) / 2.0, y));
			dc.DrawText(name, new Point((w - name.Width) / 2.0, y + title.Height + 4.0));
			dc.DrawText(lives, new Point((w - lives.Width) / 2.0, y + title.Height + name.Height + 18.0));
			FormattedText hint = _hintText.Get(Loc.T("←/→ — ход · Пробел — прыжок · Shift — бег · Ctrl — леденец · ↓ — присесть"), 13.0, _muted, ppd);
			dc.DrawText(hint, new Point((w - hint.Width) / 2.0, h - hint.Height - 28.0));
			dc.Pop();
			break;
		}
		case State.Paused:
			dc.DrawRectangle(_dim, null, new Rect(0.0, 0.0, w, h));
			Panel(dc, w, h, Loc.T("Пауза"), Loc.F("Мир {0} · {1}", e?.Level.Id ?? "", _levelName), Loc.F("Счёт: {0}", e?.Score ?? 0), Loc.T("P / Пробел — продолжить · R — заново · Esc — в меню"), false);
			break;
		case State.Clear:
			if (e != null)
			{
				double a = Math.Clamp((_time - _stateAt) / 0.3, 0.0, 1.0);
				dc.PushOpacity(a);
				Panel(dc, w, h, Loc.T("Уровень пройден!"), ScoreLine(e.Score), Loc.F("Бонус за время: {0}", _timeBonus), e.Phase == PlatformerPhase.Cleared ? Loc.T("Enter — дальше · Esc — в меню") : " ", false);
				dc.Pop();
			}
			break;
		case State.Over:
		case State.Victory:
		{
			double a = Math.Clamp((_time - _stateAt) / 0.3, 0.0, 1.0);
			dc.PushOpacity(a);
			Panel(dc, w, h, Loc.T(_state == State.Victory ? "Все миры пройдены!" : "Игра окончена"), ScoreLine(_finalScore), _menuBest, Loc.T("Enter / R — заново · Esc — в меню"), _record);
			dc.Pop();
			break;
		}
		}
	}

	private string ScoreLine(int score)
	{
		if (score != _scoreLineValue)
		{
			_scoreLineValue = score;
			_scoreLine = Loc.F("Счёт: {0}", score);
		}
		return _scoreLine;
	}

	private void DrawStatus(DrawingContext dc, PlatformerEngine e, double w, double ppd)
	{
		double size = Math.Clamp(_ts * 0.62, 14.0, 26.0);
		dc.DrawRoundedRectangle(_hudPanel, null, new Rect(10.0, 8.0, w - 20.0, size * 1.9), 12.0, 12.0);
		double y = 8.0 + (size * 1.9 - size * 1.3) / 2.0;
		FormattedText lives = _livesText.Number(e.Lives, "'×'0", size, _fg, ppd);
		dc.DrawText(lives, new Point(58.0, y));
		double cx = 58.0 + lives.Width + size * 1.4;
		_art.Coin(dc, cx, y + size * 0.65, size / 16.0, _time * 3.0);
		FormattedText coins = _coinText.Number(e.Coins, "'×'00", size, _fg, ppd);
		dc.DrawText(coins, new Point(cx + size * 0.6, y));
		FormattedText score = _scoreText.Number(e.Score, "000000", size, _fg, ppd);
		dc.DrawText(score, new Point((w - score.Width) / 2.0, y));
		bool hurry = e.TimeLeft < 100.0 && e.Phase == PlatformerPhase.Playing;
		FormattedText time = _timeText.Number((long)Math.Ceiling(e.TimeLeft), "'⏱ '000", size, hurry && ((int)(_time * 4.0) & 1) == 0 ? _danger : _fg, ppd);
		dc.DrawText(time, new Point(w - 24.0 - time.Width, y));
		FormattedText world = _worldText.Get(_worldLabel, size * 0.8, _muted, ppd);
		dc.DrawText(world, new Point(w - 24.0 - time.Width - size - world.Width, y + size * 0.15));
	}

	private void DrawMenu(DrawingContext dc, double w, double h)
	{
		double ppd = _ppd;
		dc.DrawRectangle(_dim, null, new Rect(0.0, 0.0, w, h));
		FormattedText title = _titleText.Get(Loc.T("Мета-платформер"), 36.0, _fg, ppd);
		FormattedText sub = _lineText.Get(Loc.T("Выбери уровень"), 16.0, _muted, ppd);
		double top = Math.Max(16.0, h * 0.12);
		dc.DrawText(title, new Point((w - title.Width) / 2.0, top));
		dc.DrawText(sub, new Point((w - sub.Width) / 2.0, top + title.Height));
		double cw = Math.Clamp((w - 80.0) / 5.0 - 12.0, 110.0, 170.0);
		double ch = cw * 0.86;
		double total = cw * 5.0 + 12.0 * 4.0;
		double x0 = (w - total) / 2.0;
		double cy = top + title.Height + sub.Height + 28.0;
		for (int i = 0; i < 5; i++)
		{
			bool sel = i == _menuIndex;
			bool locked = i < 4 && !GameHub.Progress.IsUnlocked(i);
			double lift = sel ? -6.0 : 0.0;
			Rect card = new Rect(x0 + i * (cw + 12.0), cy + lift, cw, ch);
			dc.DrawRoundedRectangle(sel ? _cardSel : _cardBg, sel ? _cardEdgeSel : _cardEdge, card, 14.0, 14.0);
			Rect art = new Rect(card.X + 8.0, card.Y + 8.0, card.Width - 16.0, card.Height * 0.5);
			PlatformerTheme th = _art.Theme(i == EndlessCard ? (PlatformerWorld)((int)(_time * 0.6) % 4) : (PlatformerWorld)i);
			dc.DrawRoundedRectangle(th.Sky, null, art, 8.0, 8.0);
			double ground = art.Bottom - art.Height * 0.28;
			dc.DrawRectangle(th.Body, null, new Rect(art.X, ground, art.Width, art.Bottom - ground));
			dc.DrawRectangle(th.Top, null, new Rect(art.X, ground, art.Width, art.Height * 0.09));
			_art.Coin(dc, art.X + art.Width * 0.7, art.Y + art.Height * 0.35, art.Height / 70.0, _time * 3.0 + i);
			FormattedText id = _cardId[i].Get(_cardIds[i], 22.0, locked ? _muted : _fg, ppd);
			dc.DrawText(id, new Point(card.X + 12.0, art.Bottom + 6.0));
			FormattedText name = _cardName[i].Get(locked ? "🔒 " + Loc.T("Закрыто") : _cardNames[i], 12.5, _muted, ppd, card.Width - 20.0);
			dc.DrawText(name, new Point(card.X + 10.0, art.Bottom + 8.0 + id.Height));
			if (locked)
			{
				dc.DrawRoundedRectangle(_locked, null, card, 14.0, 14.0);
			}
		}
		double by = cy + ch + 22.0;
		FormattedText bl = _line2Text.Get(_menuBest, 15.0, _gold, ppd);
		dc.DrawText(bl, new Point((w - bl.Width) / 2.0, by));
		FormattedText hint = _hintText.Get(Loc.T("← → — выбрать · Enter — играть · Esc — к играм"), 13.0, _muted, ppd);
		dc.DrawText(hint, new Point((w - hint.Width) / 2.0, h - hint.Height - 20.0));
	}

	private void Panel(DrawingContext dc, double w, double h, string title, string line, string line2, string hint, bool record)
	{
		double ppd = _ppd;
		FormattedText t = _titleText.Get(title, 30.0, _fg, ppd);
		FormattedText l = _lineText.Get(line, 17.0, _muted, ppd);
		FormattedText l2 = _line2Text.Get(line2, 17.0, _gold, ppd);
		FormattedText hn = _hintText.Get(hint, 13.0, _muted, ppd);
		FormattedText? rec = record ? _recordText.Get("🏆 " + Loc.T("Новый рекорд!"), 18.0, _gold, ppd) : null;
		double pw = Math.Max(Math.Max(Math.Max(t.Width, l.Width), l2.Width), hn.Width) + 56.0;
		double ph = t.Height + l.Height + l2.Height + hn.Height + 52.0 + (rec != null ? rec.Height + 6.0 : 0.0);
		double px = (w - pw) / 2.0;
		double py = Math.Max(20.0, h * 0.32 - ph / 2.0);
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
		y += l.Height + 4.0;
		dc.DrawText(l2, new Point((w - l2.Width) / 2.0, y));
		y += l2.Height + 12.0;
		dc.DrawText(hn, new Point((w - hn.Width) / 2.0, y));
	}
}
