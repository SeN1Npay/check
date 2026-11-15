using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.App.Mascot;
using Metamorph.Core.Games;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// Тетрис: стакан, запас, очередь, статистика и Мета-болельщица справа.
internal sealed class TetrisPage : GamePage
{
	private enum State
	{
		Running,
		Paused,
		Over
	}

	private struct Spark
	{
		public double X;

		public double Y;

		public double Vx;

		public double Vy;

		public double Life;

		public double Max;

		public int Color;

		public double Size;
	}

	private const int Hidden = TetrisEngine.HiddenRows;

	private static readonly string[] OverLines = { "Голова кругом… Ещё разок?", "Ну вот… Давай ещё!" };

	private static readonly string[] TetrisLines = { "ТЕТРИС! Ты супер!", "Четыре линии! Вау!", "Вот это да!" };

	private readonly TetrisEngine _engine = new TetrisEngine(GameRandom.NewSeed());

	private readonly GameLoop _loop;

	private readonly DrawSurface _board;

	private readonly DrawSurface _hud;

	private readonly MascotCanvas _meta = new MascotCanvas
	{
		IsHitTestVisible = false
	};

	private readonly MascotAnimator _anim = new MascotAnimator(13);

	private readonly MascotColors _colors;

	private readonly CellStyle[] _styles;

	private readonly Random _fx = new Random();

	private readonly Spark[] _sparks = new Spark[160];

	private readonly int[] _drop = new int[TetrisEngine.Height];

	private readonly int[] _clearedRows = new int[4];

	private readonly (int X, int Top, int Bottom)[] _trail = new (int, int, int)[4];

	private readonly (double X0, double Y0, double X1, double Y1)[] _bounds = new (double, double, double, double)[7];

	private readonly Brush _well;

	private readonly Pen _wellEdge;

	private readonly Pen _grid;

	private readonly Brush _panel;

	private readonly Pen _panelEdge;

	private readonly Brush _flash;

	private readonly Pen _glow;

	private readonly Brush _lockShine;

	private readonly Brush _dim;

	private readonly Brush _fg;

	private readonly Brush _muted;

	private readonly Brush _accent;

	private readonly Brush _gold;

	private readonly Brush _danger;

	private readonly Brush _bubbleFill;

	private readonly Pen _bubbleEdge;

	private readonly TextSlot _holdLabel = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _nextLabel = new TextSlot(TextSlot.Ui);

	private readonly TextSlot[] _statLabels = { new TextSlot(TextSlot.Ui), new TextSlot(TextSlot.Ui), new TextSlot(TextSlot.Ui), new TextSlot(TextSlot.Ui) };

	private readonly TextSlot[] _statValues = { new TextSlot(TextSlot.Mono), new TextSlot(TextSlot.Mono), new TextSlot(TextSlot.Mono), new TextSlot(TextSlot.Mono) };

	private readonly TextSlot _hint = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _label = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _subLabel = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _levelText = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _bubble = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _title = new TextSlot(TextSlot.Heavy);

	private readonly TextSlot _line = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _hint2 = new TextSlot(TextSlot.Ui);

	private readonly TextSlot _record = new TextSlot(TextSlot.Heavy);

	private readonly TranslateTransform _gridShift = new TranslateTransform();

	private Geometry? _gridGeo;

	private double _gridCell;

	private State _state = State.Running;

	private double _time;

	private double _idleUntil;

	private double _overAt;

	private double _clearT = 99.0;

	private int _clearedCount;

	private double _shakeT = 99.0;

	private double _shakeAmp;

	private double _trailT = 99.0;

	private int _trailCount;

	private int _trailKind;

	private double _levelT = 99.0;

	private int _levelValue;

	private string? _labelLine;

	private string? _subLine;

	private double _labelT = 99.0;

	private string? _bubbleLine;

	private double _bubbleUntil;

	private double _clipUntil;

	private double _nearTopCooldown;

	private bool _isRecord;

	private int _best;

	private double _ppd = 1.0;

	// Раскладка (пересчитывается на каждый кадр из размеров, без выделений).
	private double _cell;

	private double _ox;

	private double _oy;

	private double _side;

	private double _gap;

	public override string Id => GameHub.Tetris;

	public override string Title => Loc.T("Тетрис");

	public TetrisPage(App app, GamePalette pal)
		: base(app, pal)
	{
		_colors = MascotColors.From(app.Services.Settings.Current.Mascot, pal.Source);
		_styles = TetrisArt.Styles(pal);
		_loop = new GameLoop(Tick);
		_board = new DrawSurface(DrawBoard);
		_hud = new DrawSurface(DrawHud);
		Children.Add(_board);
		Children.Add(_meta);
		Children.Add(_hud);
		ClipToBounds = true;
		Background = new RadialGradientBrush(GamePalette.WithAlpha(pal.Accent, 0.12), Colors.Transparent)
		{
			Center = new Point(0.5, 0.45),
			GradientOrigin = new Point(0.5, 0.45),
			RadiusX = 0.6,
			RadiusY = 0.7
		};
		_well = GamePalette.Vertical((GamePalette.WithAlpha(GamePalette.Darken(pal.Bg, 0.25), 0.85), 0.0), (GamePalette.WithAlpha(GamePalette.Mix(pal.Bg, pal.Accent, 0.1), 0.92), 1.0));
		_wellEdge = GamePalette.Pen(GamePalette.Mix(pal.Fg, pal.Accent, 0.4), 1.5, 0.35);
		_grid = GamePalette.Pen(pal.Fg, 1.0, 0.05);
		_panel = GamePalette.Solid(GamePalette.Mix(pal.Surface, pal.Bg, 0.35), 0.7);
		_panelEdge = GamePalette.Pen(pal.Fg, 1.0, 0.1);
		_flash = GamePalette.Solid(Colors.White, 0.9);
		_glow = GamePalette.Pen(GamePalette.Vivid(pal.Accent), 5.0, 0.8);
		_lockShine = GamePalette.Solid(Colors.White, 1.0);
		_dim = GamePalette.Solid(GamePalette.Darken(pal.Bg, 0.4), 0.6);
		_fg = GamePalette.Solid(pal.Fg);
		_muted = GamePalette.Solid(pal.Muted);
		_accent = GamePalette.Solid(GamePalette.Vivid(pal.Accent));
		_gold = GamePalette.Solid(GamePalette.Vivid(pal.Yellow));
		_danger = GamePalette.Solid(pal.Red, 0.18);
		_bubbleFill = GamePalette.Solid(GamePalette.Lighten(pal.Surface, 0.08), 0.96);
		_bubbleEdge = GamePalette.Pen(pal.Accent, 1.5, 0.7);
		for (int k = 0; k < 7; k++)
		{
			double x0 = 9.0;
			double y0 = 9.0;
			double x1 = -9.0;
			double y1 = -9.0;
			foreach ((int cx, int cy) in Tetromino.CellsOf((TetrominoKind)k, 0))
			{
				x0 = Math.Min(x0, cx);
				y0 = Math.Min(y0, cy);
				x1 = Math.Max(x1, cx + 1);
				y1 = Math.Max(y1, cy + 1);
			}
			_bounds[k] = (x0, y0, x1, y1);
		}
		_engine.Event += OnEngineEvent;
		_best = GameHub.Scores.Best(GameHub.Tetris);
		_anim.Facing = -1.0;
		_anim.Play(MascotClip.Wave, restart: true);
		_clipUntil = 2.0;
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
		GameHub.Scores.Touch(GameHub.Tetris);
		_loop.Start();
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

	public override void OnActivated()
	{
		if (_state == State.Over)
		{
			Wake(2.0);
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
		bool run = _state == State.Running;
		bool press = down && !repeat;
		switch (key)
		{
		case Key.Left:
			if (!repeat && (run || !down))
			{
				_engine.SetLeft(down);
			}
			return true;
		case Key.Right:
			if (!repeat && (run || !down))
			{
				_engine.SetRight(down);
			}
			return true;
		case Key.Down:
			if (!repeat && (run || !down))
			{
				_engine.SetSoftDrop(down);
			}
			return true;
		case Key.Space:
			if (press)
			{
				if (run)
				{
					_engine.HardDrop();
				}
				else if (_state == State.Paused)
				{
					Resume();
				}
				else if (_time - _overAt > 0.6)
				{
					Restart();
				}
			}
			return true;
		case Key.Up:
		case Key.X:
			if (press && run)
			{
				_engine.TryRotate(clockwise: true);
			}
			return true;
		case Key.Z:
		case Key.LeftCtrl:
		case Key.RightCtrl:
			if (press && run)
			{
				_engine.TryRotate(clockwise: false);
			}
			return true;
		case Key.C:
		case Key.LeftShift:
		case Key.RightShift:
			if (press && run)
			{
				_engine.Hold();
			}
			return true;
		case Key.P:
			if (press)
			{
				if (run)
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
			if (press)
			{
				Restart();
			}
			return true;
		default:
			return false;
		}
	}

	private void Restart()
	{
		ReleaseKeys();
		_engine.Reset(GameRandom.NewSeed());
		Array.Clear(_sparks);
		Array.Clear(_drop);
		_state = State.Running;
		_clearT = _shakeT = _trailT = _levelT = _labelT = 99.0;
		_bubbleLine = null;
		_isRecord = false;
		_best = GameHub.Scores.Best(GameHub.Tetris);
		_anim.Play(MascotClip.Happy, restart: true);
		_clipUntil = _time + 1.2;
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
		_engine.SetLeft(false);
		_engine.SetRight(false);
		_engine.SetSoftDrop(false);
	}

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

	private void OnEngineEvent(TetrisEvent e)
	{
		switch (e.Kind)
		{
		case TetrisEventKind.HardDrop:
			OnHardDrop(e.Value);
			break;
		case TetrisEventKind.Lock:
			if (_engine.StackTop < Hidden + 6 && _time > _nearTopCooldown && !_engine.IsGameOver)
			{
				_nearTopCooldown = _time + 12.0;
				React(MascotClip.Surprised, _fx.Next(2) == 0 ? Loc.T("Ой-ой, высоко!") : Loc.T("Осторожнее, стакан почти полон!"), 1.6);
			}
			break;
		case TetrisEventKind.LineClear:
			OnClear();
			break;
		case TetrisEventKind.LevelUp:
			_levelT = 0.0;
			_levelValue = e.Value;
			React(MascotClip.Dance, Loc.F("Уровень {0}! Танцуем!", e.Value), 2.6);
			break;
		case TetrisEventKind.GameOver:
		{
			_state = State.Over;
			_overAt = _time;
			ReleaseKeys();
			int score = (int)Math.Min(_engine.Score, int.MaxValue);
			_isRecord = GameHub.Submit(App, GameHub.Tetris, score);
			if (_isRecord)
			{
				_best = score;
			}
			_anim.Play(MascotClip.Dizzy, restart: true);
			_clipUntil = double.MaxValue;
			Say(_isRecord ? Loc.T("Новый рекорд! Ура-а!") : Loc.T(OverLines[_fx.Next(OverLines.Length)]), 4.0);
			Wake(3.0);
			break;
		}
		}
	}

	private void OnHardDrop(int distance)
	{
		_shakeT = 0.0;
		_shakeAmp = 0.06 + Math.Min(1.0, distance / 12.0) * 0.16;
		if (distance <= 0)
		{
			return;
		}
		_trailT = 0.0;
		_trailKind = (int)_engine.Current;
		_trailCount = 0;
		foreach ((int cx, int cy) in Tetromino.CellsOf(_engine.Current, _engine.Rotation))
		{
			int x = _engine.X + cx;
			int bottom = _engine.Y + cy;
			int top = bottom - distance;
			int found = -1;
			for (int i = 0; i < _trailCount; i++)
			{
				if (_trail[i].X == x)
				{
					found = i;
				}
			}
			if (found < 0)
			{
				_trail[_trailCount++] = (x, top, bottom);
			}
			else
			{
				_trail[found] = (x, Math.Min(_trail[found].Top, top), Math.Max(_trail[found].Bottom, bottom));
			}
		}
	}

	private void OnClear()
	{
		TetrisClear c = _engine.LastClear;
		if (c.Lines > 0)
		{
			_clearT = 0.0;
			_engine.LastDrop.CopyTo(_drop);
			_clearedCount = Math.Min(4, c.Lines);
			_engine.LastClearedRows.Slice(0, _clearedCount).CopyTo(_clearedRows);
			for (int i = 0; i < _clearedCount; i++)
			{
				Burst(_clearedRows[i], 12 + 4 * c.Lines);
			}
		}
		string main = c.TSpin switch
		{
			TSpinKind.Full => Loc.T("T-спин") + (c.Lines > 0 ? " " + Name(c.Lines) : ""),
			TSpinKind.Mini => Loc.T("T-спин мини") + (c.Lines > 0 ? " " + Name(c.Lines) : ""),
			_ => c.Lines == 4 ? Loc.T("ТЕТРИС!") : (c.Lines >= 2 ? Name(c.Lines) : "")
		};
		string sub = (c.BackToBack ? Loc.T("Подряд!") + "  " : "") + (c.Combo > 0 ? Loc.F("Комбо ×{0}", c.Combo) : "");
		if (main.Length > 0 || sub.Length > 0)
		{
			_labelLine = main;
			_subLine = sub.Trim();
			_labelT = 0.0;
		}
		if (c.TSpin != TSpinKind.None && c.Lines > 0)
		{
			React(MascotClip.Love, Loc.T("Т-спин?! Да ты профи!"), 2.2);
		}
		else if (c.Lines == 4)
		{
			React(MascotClip.Happy, Loc.T(TetrisLines[_fx.Next(TetrisLines.Length)]), 2.2);
		}
		else if (c.Lines > 0)
		{
			_anim.Jiggle(0.5 + 0.2 * c.Lines);
			_anim.Impact(0.25);
		}
		static string Name(int lines)
		{
			return lines switch
			{
				1 => Loc.T("Одинарная"),
				2 => Loc.T("Двойная"),
				3 => Loc.T("Тройная"),
				_ => Loc.T("ТЕТРИС!")
			};
		}
	}

	private void React(MascotClip clip, string line, double seconds)
	{
		if (_state == State.Over)
		{
			return;
		}
		_anim.Play(clip, restart: true);
		_clipUntil = _time + seconds;
		Say(line, seconds + 0.6);
	}

	private void Say(string line, double seconds)
	{
		_bubbleLine = line;
		_bubbleUntil = _time + seconds;
	}

	private void Burst(int row, int count)
	{
		for (int n = 0; n < count; n++)
		{
			for (int i = 0; i < _sparks.Length; i++)
			{
				if (_sparks[i].Life <= 0.0)
				{
					double life = 0.45 + _fx.NextDouble() * 0.4;
					_sparks[i] = new Spark
					{
						X = _fx.NextDouble() * TetrisEngine.Width,
						Y = row - Hidden + 0.5,
						Vx = (_fx.NextDouble() - 0.5) * 6.0,
						Vy = -2.0 - _fx.NextDouble() * 6.0,
						Life = life,
						Max = life,
						Color = _fx.Next(7),
						Size = 0.06 + _fx.NextDouble() * 0.1
					};
					break;
				}
			}
		}
	}

	private void Render(double dt)
	{
		_clearT += dt;
		_shakeT += dt;
		_trailT += dt;
		_levelT += dt;
		_labelT += dt;
		for (int i = 0; i < _sparks.Length; i++)
		{
			ref Spark s = ref _sparks[i];
			if (s.Life > 0.0)
			{
				s.Life -= dt;
				s.X += s.Vx * dt;
				s.Y += s.Vy * dt;
				s.Vy += 14.0 * dt;
			}
		}
		Layout(ActualWidth, ActualHeight);
		UpdateMeta(dt);
		_board.InvalidateVisual();
		_hud.InvalidateVisual();
	}

	private void Layout(double w, double h)
	{
		_cell = Math.Max(8.0, Math.Floor(Math.Min((h - 56.0) / TetrisEngine.VisibleHeight, (w - 48.0) / 23.0)));
		_side = Math.Round(_cell * 5.2);
		_gap = Math.Round(_cell * 0.8);
		double boardW = _cell * TetrisEngine.Width;
		double total = _side * 2.0 + _gap * 2.0 + boardW;
		_ox = Math.Round((w - total) / 2.0 + _side + _gap);
		_oy = Math.Round((h - _cell * TetrisEngine.VisibleHeight) / 2.0 + 6.0);
	}

	private void UpdateMeta(double dt)
	{
		if (_state != State.Over && _time > _clipUntil)
		{
			_anim.Play(MascotClip.Sit);
			_clipUntil = double.MaxValue;
		}
		double mx = _ox + _cell * TetrisEngine.Width + _gap + _side / 2.0;
		double feet = _oy + _cell * TetrisEngine.VisibleHeight;
		double scale = Math.Min(_cell * 6.4 / 200.0, (_side + _gap) / 150.0);
		double pieceX = _ox + (_engine.X + 1.5) * _cell;
		double pieceY = _oy + (_engine.Y - Hidden + 1) * _cell;
		_anim.LookX = Math.Clamp((pieceX - mx) / 260.0, -1.0, 1.0);
		_anim.LookY = Math.Clamp((pieceY - (feet - 150.0 * scale)) / 260.0, -1.0, 1.0);
		_anim.Bass = 0.6 + 0.4 * Math.Max(0.0, Math.Sin(_time * 7.0));
		MascotPose pose = _anim.Update(dt);
		_meta.SetShadow(new Point(mx, feet - 2.0), 80.0 * scale, 1.0);
		_meta.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(scale, scale).Then(Affine.Translate(mx, feet - 4.0))));
	}

	private void DrawBoard(DrawingContext dc, double w, double h)
	{
		double c = _cell;
		double bw = c * TetrisEngine.Width;
		double bh = c * TetrisEngine.VisibleHeight;
		double shake = _shakeT < 0.4 ? c * _shakeAmp * Math.Exp(-_shakeT * 14.0) * Math.Cos(_shakeT * 42.0) : 0.0;
		double ox = _ox;
		double oy = _oy + shake;
		double leftX = ox - _gap - _side;
		double rightX = ox + bw + _gap;
		Panel(dc, leftX, _oy, _side, c * 4.2, _holdLabel.Get(Loc.T("Запас"), 13.0, _muted, _ppd));
		if (_engine.HoldPiece is TetrominoKind held)
		{
			Mini(dc, held, leftX + _side / 2.0, _oy + c * 2.45, c * 0.78, _engine.CanHold ? 1.0 : 0.35);
		}
		Panel(dc, rightX, _oy, _side, c * 12.4, _nextLabel.Get(Loc.T("Далее"), 13.0, _muted, _ppd));
		double ny = _oy + c * 2.5;
		for (int i = 0; i < TetrisEngine.NextCount; i++)
		{
			double size = i == 0 ? c * 0.86 : c * 0.62;
			Mini(dc, _engine.PeekNext(i), rightX + _side / 2.0, ny, size, i == 0 ? 1.0 : 0.8);
			ny += i == 0 ? c * 2.6 : c * 1.95;
		}
		Stats(dc, leftX, _oy + c * 4.8, _side, c * 8.6);
		FormattedText hint = _hint.Get(Loc.T("←/→ ход · ↑/X поворот · Z влево · Пробел — сброс · C — запас"), 11.5, _muted, _ppd, _side);
		dc.DrawText(hint, new Point(leftX + (_side - hint.MaxTextWidth) / 2.0, _oy + c * 13.8));
		Rect well = new Rect(ox - 4.0, oy - 4.0, bw + 8.0, bh + 8.0);
		dc.DrawRoundedRectangle(_well, _wellEdge, well, 10.0, 10.0);
		if (_engine.StackTop < Hidden + 5 && _state == State.Running)
		{
			dc.PushOpacity(0.5 + 0.5 * Math.Sin(_time * 6.0));
			dc.DrawRoundedRectangle(_danger, null, new Rect(ox, oy, bw, c * 4.0), 6.0, 6.0);
			dc.Pop();
		}
		if (_gridGeo == null || _gridCell != c)
		{
			_gridGeo = BuildGrid(c);
			_gridCell = c;
		}
		_gridShift.X = ox;
		_gridShift.Y = oy;
		dc.PushTransform(_gridShift);
		dc.DrawGeometry(null, _grid, _gridGeo);
		dc.Pop();
		double collapse = Math.Clamp((_clearT - 0.1) / 0.16, 0.0, 1.0);
		double fall = 1.0 - (1.0 - Math.Pow(1.0 - collapse, 3.0));
		for (int y = Hidden; y < TetrisEngine.Height; y++)
		{
			double rowY = oy + (y - Hidden - _drop[y] * fall) * c;
			for (int x = 0; x < TetrisEngine.Width; x++)
			{
				int v = _engine.Cell(x, y);
				if (v != 0)
				{
					_styles[v - 1].Draw(dc, ox + x * c, rowY, c);
				}
			}
		}
		if (_clearT < 0.3)
		{
			double a = 1.0 - _clearT / 0.3;
			double grow = Math.Min(1.0, _clearT / 0.08);
			double fw = bw * (0.35 + 0.65 * grow);
			dc.PushOpacity(a);
			for (int i = 0; i < _clearedCount; i++)
			{
				double ry = oy + (_clearedRows[i] - Hidden) * c;
				dc.DrawRoundedRectangle(_flash, null, new Rect(ox + (bw - fw) / 2.0, ry + c * 0.08, fw, c * 0.84), c * 0.3, c * 0.3);
			}
			dc.Pop();
		}
		if (_state != State.Over)
		{
			CellStyle style = _styles[(int)_engine.Current];
			int ghostY = _engine.GhostY;
			if (ghostY != _engine.Y)
			{
				foreach ((int cx, int cy) in Tetromino.CellsOf(_engine.Current, _engine.Rotation))
				{
					int gy = ghostY + cy;
					if (gy >= Hidden)
					{
						style.Ghost(dc, ox + (_engine.X + cx) * c, oy + (gy - Hidden) * c, c);
					}
				}
			}
		}
		if (_trailT < 0.24)
		{
			dc.PushOpacity(1.0 - _trailT / 0.24);
			Brush trail = _styles[_trailKind].Trail;
			for (int i = 0; i < _trailCount; i++)
			{
				(int x, int top, int bottom) = _trail[i];
				double t0 = Math.Max(top, Hidden);
				if (bottom >= t0)
				{
					dc.DrawRectangle(trail, null, new Rect(ox + x * c + c * 0.12, oy + (t0 - Hidden) * c, c * 0.76, (bottom - t0 + 1) * c));
				}
			}
			dc.Pop();
		}
		if (_state != State.Over)
		{
			CellStyle style = _styles[(int)_engine.Current];
			double lockGlow = _engine.Grounded ? 0.08 + 0.22 * _engine.LockProgress : 0.0;
			foreach ((int cx, int cy) in Tetromino.CellsOf(_engine.Current, _engine.Rotation))
			{
				int py = _engine.Y + cy;
				if (py < Hidden - 1)
				{
					continue;
				}
				double sx = ox + (_engine.X + cx) * c;
				double sy = oy + (py - Hidden) * c;
				style.Draw(dc, sx, sy, c);
				if (lockGlow > 0.0)
				{
					dc.PushOpacity(lockGlow);
					dc.DrawRoundedRectangle(_lockShine, null, new Rect(sx + 1.0, sy + 1.0, c - 2.0, c - 2.0), c * 0.17, c * 0.17);
					dc.Pop();
				}
			}
		}
		for (int i = 0; i < _sparks.Length; i++)
		{
			ref Spark s = ref _sparks[i];
			if (s.Life > 0.0)
			{
				dc.PushOpacity(Math.Clamp(s.Life / s.Max, 0.0, 1.0));
				double r = s.Size * c;
				dc.DrawEllipse(_styles[s.Color].Spark, null, new Point(ox + s.X * c, oy + s.Y * c), r, r);
				dc.Pop();
			}
		}
		if (_levelT < 1.4)
		{
			dc.PushOpacity(1.0 - _levelT / 1.4);
			dc.DrawRoundedRectangle(null, _glow, well, 10.0, 10.0);
			dc.Pop();
		}
		if (_labelT < 1.5 && (_labelLine?.Length > 0 || _subLine?.Length > 0))
		{
			double a = _labelT < 1.0 ? 1.0 : 1.0 - (_labelT - 1.0) / 0.5;
			double rise = _labelT * c * 0.8;
			dc.PushOpacity(a);
			double y = oy + bh * 0.36 - rise;
			if (_labelLine?.Length > 0)
			{
				FormattedText t = _label.Get(_labelLine, Math.Max(16.0, c * 1.05), _labelLine == Loc.T("ТЕТРИС!") ? _gold : _fg, _ppd);
				dc.DrawText(t, new Point(ox + (bw - t.Width) / 2.0, y));
				y += t.Height;
			}
			if (_subLine?.Length > 0)
			{
				FormattedText s = _subLabel.Get(_subLine, Math.Max(12.0, c * 0.6), _accent, _ppd);
				dc.DrawText(s, new Point(ox + (bw - s.Width) / 2.0, y));
			}
			dc.Pop();
		}
		if (_levelT < 1.6)
		{
			double t = _levelT / 1.6;
			FormattedText word = _levelText.Get(Loc.F("Уровень {0}!", _levelValue), Math.Max(18.0, c * 1.2), _accent, _ppd);
			dc.PushOpacity(t < 0.75 ? 1.0 : 1.0 - (t - 0.75) / 0.25);
			dc.DrawText(word, new Point(ox + (bw - word.Width) / 2.0, oy + bh * 0.2 - t * c));
			dc.Pop();
		}
	}

	private void Panel(DrawingContext dc, double x, double y, double w, double h, FormattedText label)
	{
		dc.DrawRoundedRectangle(_panel, _panelEdge, new Rect(x, y, w, h), 12.0, 12.0);
		dc.DrawText(label, new Point(x + (w - label.Width) / 2.0, y + 8.0));
	}

	private void Stats(DrawingContext dc, double x, double y, double w, double h)
	{
		dc.DrawRoundedRectangle(_panel, _panelEdge, new Rect(x, y, w, h), 12.0, 12.0);
		double row = h / 4.0;
		double vs = Math.Max(13.0, Math.Min(_cell * 0.7, 22.0));
		for (int i = 0; i < 4; i++)
		{
			string label = i switch
			{
				0 => Loc.T("Счёт"),
				1 => Loc.T("Уровень"),
				2 => Loc.T("Линии"),
				_ => Loc.T("Рекорд")
			};
			long value = i switch
			{
				0 => _engine.Score,
				1 => _engine.Level,
				2 => _engine.Lines,
				_ => Math.Max(_best, 0)
			};
			FormattedText l = _statLabels[i].Get(label, 12.0, _muted, _ppd);
			FormattedText v = _statValues[i].Number(value, "#,0", vs, i == 3 ? _gold : _fg, _ppd);
			double ry = y + i * row + (row - l.Height - v.Height) / 2.0;
			dc.DrawText(l, new Point(x + 14.0, ry));
			dc.DrawText(v, new Point(x + 14.0, ry + l.Height));
		}
	}

	private void Mini(DrawingContext dc, TetrominoKind kind, double cx, double cy, double size, double opacity)
	{
		(double x0, double y0, double x1, double y1) = _bounds[(int)kind];
		double left = cx - (x1 - x0) * size / 2.0;
		double top = cy - (y1 - y0) * size / 2.0;
		CellStyle style = _styles[(int)kind];
		if (opacity < 0.999)
		{
			dc.PushOpacity(opacity);
		}
		foreach ((int px, int py) in Tetromino.CellsOf(kind, 0))
		{
			style.Draw(dc, left + (px - x0) * size, top + (py - y0) * size, size);
		}
		if (opacity < 0.999)
		{
			dc.Pop();
		}
	}

	private void DrawHud(DrawingContext dc, double w, double h)
	{
		if (_bubbleLine != null && _time < _bubbleUntil)
		{
			double scale = Math.Min(_cell * 6.4 / 200.0, (_side + _gap) / 150.0);
			double mx = _ox + _cell * TetrisEngine.Width + _gap + _side / 2.0;
			double headTop = _oy + _cell * TetrisEngine.VisibleHeight - 4.0 - 205.0 * scale;
			FormattedText t = _bubble.Get(_bubbleLine, 14.0, _fg, _ppd, Math.Max(120.0, _side + _gap * 2.0));
			GameDraw.Bubble(dc, t, mx, headTop, _bubbleFill, _bubbleEdge, Math.Clamp((_bubbleUntil - _time) * 3.0, 0.0, 1.0), _ox, w - 8.0);
		}
		if (_state == State.Paused)
		{
			dc.DrawRectangle(_dim, null, new Rect(0.0, 0.0, w, h));
			Overlay(dc, w, h, Loc.T("Пауза"), Loc.F("Счёт: {0}", _engine.Score), Loc.T("P / Пробел — продолжить · Esc — в меню"), false);
		}
		else if (_state == State.Over)
		{
			double a = Math.Clamp((_time - _overAt) / 0.35, 0.0, 1.0);
			dc.PushOpacity(a);
			dc.DrawRectangle(_dim, null, new Rect(_ox - 4.0, _oy - 4.0, _cell * TetrisEngine.Width + 8.0, _cell * TetrisEngine.VisibleHeight + 8.0));
			Overlay(dc, w, h, Loc.T("Игра окончена"), Loc.F("Счёт: {0}", _engine.Score), Loc.T("R — заново · Esc — в меню"), _isRecord);
			dc.Pop();
		}
	}

	private void Overlay(DrawingContext dc, double w, double h, string title, string line, string hint, bool record)
	{
		FormattedText t = _title.Get(title, Math.Max(20.0, _cell * 0.95), _fg, _ppd);
		FormattedText l = _line.Get(line, 16.0, _fg, _ppd);
		FormattedText hn = _hint2.Get(hint, 12.5, _muted, _ppd);
		FormattedText? rec = record ? _record.Get("🏆 " + Loc.T("Новый рекорд!"), 17.0, _gold, _ppd) : null;
		double bw = _cell * TetrisEngine.Width;
		double pw = Math.Min(bw - 12.0, Math.Max(Math.Max(t.Width, l.Width), hn.Width) + 36.0);
		double ph = t.Height + l.Height + hn.Height + 38.0 + (rec != null ? rec.Height + 6.0 : 0.0);
		double px = _ox + (bw - pw) / 2.0;
		double py = _oy + _cell * 7.0;
		dc.DrawRoundedRectangle(_bubbleFill, _bubbleEdge, new Rect(px, py, pw, ph), 16.0, 16.0);
		double cx = _ox + bw / 2.0;
		double y = py + 14.0;
		dc.DrawText(t, new Point(cx - t.Width / 2.0, y));
		y += t.Height + 4.0;
		if (rec != null)
		{
			dc.DrawText(rec, new Point(cx - rec.Width / 2.0, y));
			y += rec.Height + 6.0;
		}
		dc.DrawText(l, new Point(cx - l.Width / 2.0, y));
		y += l.Height + 8.0;
		dc.DrawText(hn, new Point(cx - hn.Width / 2.0, y));
	}

	private static Geometry BuildGrid(double c)
	{
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext s = g.Open())
		{
			for (int x = 1; x < TetrisEngine.Width; x++)
			{
				s.BeginFigure(new Point(x * c, 0.0), isFilled: false, isClosed: false);
				s.LineTo(new Point(x * c, TetrisEngine.VisibleHeight * c), isStroked: true, isSmoothJoin: false);
			}
			for (int y = 1; y < TetrisEngine.VisibleHeight; y++)
			{
				s.BeginFigure(new Point(0.0, y * c), isFilled: false, isClosed: false);
				s.LineTo(new Point(TetrisEngine.Width * c, y * c), isStroked: true, isSmoothJoin: false);
			}
		}
		g.Freeze();
		return g;
	}
}
