using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Metamorph.App.Mascot;
using Metamorph.Core.Games;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// Экран выбора игры: карточки, рекорды, машущая Мета. Анимация Меты — лёгкий таймер, только пока окно активно.
internal sealed class HubPage : GamePage
{
	private sealed record Card(string Id, Border Root, ScaleTransform Scale);

	private readonly GameWindow _window;

	private readonly Card[] _cards;

	private readonly MascotCanvas _meta = new MascotCanvas
	{
		Width = 220.0,
		Height = 260.0,
		IsHitTestVisible = false
	};

	private readonly MascotAnimator _anim = new MascotAnimator(5);

	private readonly MascotColors _colors;

	private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render)
	{
		Interval = TimeSpan.FromMilliseconds(33.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private double _last;

	private double _nextClip;

	private int _selected;

	public override string Id => "hub";

	public override string Title => Loc.T("Игры с Метой");

	public HubPage(App app, GamePalette pal, GameWindow window)
		: base(app, pal)
	{
		_window = window;
		_colors = MascotColors.From(app.Services.Settings.Current.Mascot, pal.Source);
		Background = new RadialGradientBrush(GamePalette.WithAlpha(pal.Accent, 0.16), Colors.Transparent)
		{
			Center = new Point(0.5, 0.35),
			GradientOrigin = new Point(0.5, 0.35),
			RadiusX = 0.7,
			RadiusY = 0.8
		};
		RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		RowDefinitions.Add(new RowDefinition());
		RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		StackPanel header = new StackPanel
		{
			Margin = new Thickness(0.0, 28.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		header.Children.Add(Text(Loc.T("Во что поиграем?"), 34.0, pal.Fg, FontWeights.Black));
		header.Children.Add(Text(Loc.T("Мета-тян уже размялась и ждёт тебя"), 15.0, pal.Muted, FontWeights.Normal));
		SetRow(header, 0);
		Children.Add(header);
		StackPanel row = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_cards = new Card[3]
		{
			BuildCard(GameHub.Dino, Loc.T("Мета-раннер"), Loc.T("Беги, прыгай и пригибайся! Кристаллы, тыквы, летучие мыши — и день сменяет ночь."), 0),
			BuildCard(GameHub.Tetris, Loc.T("Тетрис"), Loc.T("Классика с SRS, запасом и T-спинами. Мета болеет за тебя рядом со стаканом."), 1),
			BuildCard(GameHub.Platformer, Loc.T("Мета-платформер"), Loc.T("Прыгай по блокам, топчи слаймиков и добегай до флага! Четыре мира и бесконечный режим."), 2)
		};
		row.Children.Add(_meta);
		foreach (Card c in _cards)
		{
			row.Children.Add(c.Root);
		}
		Viewbox fit = new Viewbox
		{
			Child = row,
			StretchDirection = StretchDirection.DownOnly,
			Margin = new Thickness(12.0, 8.0, 12.0, 8.0)
		};
		SetRow(fit, 1);
		Children.Add(fit);
		TextBlock hint = Text(Loc.T("← → — выбрать · Enter — играть · Esc — закрыть"), 13.0, pal.Muted, FontWeights.Normal);
		hint.Margin = new Thickness(0.0, 0.0, 0.0, 22.0);
		SetRow(hint, 2);
		Children.Add(hint);
		string? last = GameHub.Scores.LastGame;
		_selected = last == GameHub.Tetris ? 1 : (last == GameHub.Platformer || last == GameHub.PlatformerEndless ? 2 : 0);
		Select(_selected, animate: false);
		_anim.Facing = 1.0;
		_anim.Play(MascotClip.Wave, restart: true);
		_nextClip = 3.5;
		_timer.Tick += delegate
		{
			Frame();
		};
	}

	public override void OnShown()
	{
		StartTimer();
	}

	public override void OnActivated()
	{
		StartTimer();
	}

	public override void OnDeactivated()
	{
		_timer.Stop();
	}

	public override void Close()
	{
		_timer.Stop();
	}

	public override bool OnKey(Key key, bool down, bool repeat)
	{
		if (!down)
		{
			return key is Key.Left or Key.Right or Key.Enter or Key.Space;
		}
		switch (key)
		{
		case Key.Left:
		case Key.A:
			Select(Math.Max(0, _selected - 1), animate: true);
			return true;
		case Key.Right:
		case Key.D:
			Select(Math.Min(_cards.Length - 1, _selected + 1), animate: true);
			return true;
		case Key.Tab:
			Select((_selected + 1) % _cards.Length, animate: true);
			return true;
		case Key.Enter:
		case Key.Space:
			if (!repeat)
			{
				Play(_cards[_selected].Id);
			}
			return true;
		case Key.D1:
			Play(GameHub.Dino);
			return true;
		case Key.D2:
			Play(GameHub.Tetris);
			return true;
		case Key.D3:
			Play(GameHub.Platformer);
			return true;
		default:
			return false;
		}
	}

	private void StartTimer()
	{
		_last = _clock.Elapsed.TotalSeconds;
		Frame();
		_timer.Start();
	}

	private void Play(string id)
	{
		_timer.Stop();
		_window.Navigate(id, force: true);
	}

	private void Select(int index, bool animate)
	{
		_selected = index;
		for (int i = 0; i < _cards.Length; i++)
		{
			Card c = _cards[i];
			bool on = i == index;
			c.Root.BorderBrush = on ? GamePalette.Solid(Pal.Accent, 0.95) : GamePalette.Solid(Pal.Fg, 0.08);
			c.Root.Background = on ? GamePalette.Vertical((GamePalette.Mix(Pal.Surface, Pal.Accent, 0.16), 0.0), (GamePalette.Mix(Pal.Surface, Pal.Bg, 0.4), 1.0)) : GamePalette.Vertical((GamePalette.Mix(Pal.Surface, Pal.Bg, 0.25), 0.0), (GamePalette.Mix(Pal.Surface, Pal.Bg, 0.6), 1.0));
			double target = on ? 1.04 : 0.97;
			if (animate)
			{
				DoubleAnimation a = new DoubleAnimation(target, TimeSpan.FromMilliseconds(180.0))
				{
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				c.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
				c.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
			}
			else
			{
				c.Scale.ScaleX = c.Scale.ScaleY = target;
			}
		}
		if (animate)
		{
			_anim.Jiggle(0.6);
		}
	}

	private void Frame()
	{
		double now = _clock.Elapsed.TotalSeconds;
		double dt = Math.Clamp(now - _last, 0.0, 0.1);
		_last = now;
		_nextClip -= dt;
		if (_nextClip <= 0.0)
		{
			MascotClip clip = _anim.Clip == MascotClip.Idle ? (_clock.Elapsed.TotalSeconds % 3.0 < 1.5 ? MascotClip.Wave : MascotClip.Happy) : MascotClip.Idle;
			_anim.Play(clip, restart: true);
			_nextClip = clip == MascotClip.Idle ? 4.0 : 2.6;
		}
		_anim.LookX = 0.4 + 0.3 * _selected;
		_anim.LookY = 0.15;
		MascotPose pose = _anim.Update(dt);
		double s = 0.98;
		_meta.SetShadow(new Point(110.0, 238.0), 84.0, 1.0);
		_meta.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(s, s).Then(Affine.Translate(110.0, 238.0))));
	}

	private Card BuildCard(string id, string title, string description, int index)
	{
		Grid art = new Grid
		{
			Width = 280.0,
			Height = 160.0,
			ClipToBounds = true
		};
		art.Children.Add(new DrawSurface((dc, w, h) =>
		{
			if (id == GameHub.Dino)
			{
				DrawRunnerArt(dc, w, h);
			}
			else if (id == GameHub.Platformer)
			{
				PlatformerArt.DrawCard(dc, Pal, w, h);
			}
			else
			{
				DrawTetrisArt(dc, w, h);
			}
		}));
		MascotCanvas mini = new MascotCanvas
		{
			IsHitTestVisible = false
		};
		MascotAnimator a = new MascotAnimator(3)
		{
			Facing = 1.0
		};
		a.Play(id == GameHub.Tetris ? MascotClip.Happy : MascotClip.Jump, restart: true);
		MascotPose pose = a.Update(0.05);
		for (int i = 0; i < 6; i++)
		{
			pose = a.Update(0.05);
		}
		pose.Fx.Clear();
		if (id == GameHub.Dino)
		{
			mini.SetShadow(new Point(74.0, 138.0), 30.0, 0.7);
			mini.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(0.44, 0.44).Then(Affine.Translate(74.0, 112.0))));
		}
		else if (id == GameHub.Platformer)
		{
			mini.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(0.3, 0.3).Then(Affine.Translate(78.0, 98.0))));
		}
		else
		{
			mini.SetItems(MascotRig.Render(pose, _colors, Affine.Scale(0.5, 0.5).Then(Affine.Translate(222.0, 150.0))));
		}
		art.Children.Add(mini);
		Border artFrame = new Border
		{
			CornerRadius = new CornerRadius(14.0),
			Child = art,
			ClipToBounds = true
		};
		artFrame.Loaded += delegate
		{
			RectangleGeometry g = new RectangleGeometry(new Rect(0.0, 0.0, 280.0, 160.0), 14.0, 14.0);
			g.Freeze();
			art.Clip = g;
		};
		StackPanel body = new StackPanel
		{
			Margin = new Thickness(16.0)
		};
		body.Children.Add(artFrame);
		TextBlock t = Text(title, 22.0, Pal.Fg, FontWeights.Bold);
		t.HorizontalAlignment = HorizontalAlignment.Left;
		t.Margin = new Thickness(2.0, 14.0, 0.0, 4.0);
		body.Children.Add(t);
		TextBlock d = Text(description, 13.0, Pal.Muted, FontWeights.Normal);
		d.HorizontalAlignment = HorizontalAlignment.Left;
		d.TextAlignment = TextAlignment.Left;
		d.TextWrapping = TextWrapping.Wrap;
		d.Width = 276.0;
		d.Height = 40.0;
		d.Margin = new Thickness(2.0, 0.0, 0.0, 10.0);
		body.Children.Add(d);
		DockPanel bottom = new DockPanel
		{
			LastChildFill = false
		};
		GameRecord rec = GameHub.Scores.Get(id);
		TextBlock best = Text(rec.Best > 0 ? ("🏆 " + Loc.F("Рекорд: {0}", rec.Best)) : Loc.T("Рекорда пока нет"), 13.0, rec.Best > 0 ? Pal.Yellow : Pal.Muted, FontWeights.SemiBold);
		best.VerticalAlignment = VerticalAlignment.Center;
		DockPanel.SetDock(best, System.Windows.Controls.Dock.Left);
		bottom.Children.Add(best);
		Color ink = GamePalette.Luma(Pal.Accent) > 0.6 ? GamePalette.Darken(Pal.Bg, 0.3) : Colors.White;
		Border play = new Border
		{
			CornerRadius = new CornerRadius(12.0),
			Background = GamePalette.Vertical((GamePalette.Lighten(Pal.Accent, 0.12), 0.0), (Pal.Accent, 1.0)),
			Padding = new Thickness(18.0, 7.0, 18.0, 8.0),
			Cursor = Cursors.Hand,
			Child = Text(Loc.T("Играть") + "  ▶", 14.0, ink, FontWeights.Bold)
		};
		DockPanel.SetDock(play, System.Windows.Controls.Dock.Right);
		bottom.Children.Add(play);
		body.Children.Add(bottom);
		ScaleTransform scale = new ScaleTransform(1.0, 1.0);
		Border root = new Border
		{
			CornerRadius = new CornerRadius(22.0),
			BorderThickness = new Thickness(2.0),
			Margin = new Thickness(14.0, 0.0, 14.0, 0.0),
			Child = body,
			Cursor = Cursors.Hand,
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = scale
		};
		root.MouseEnter += delegate
		{
			if (_selected != index)
			{
				Select(index, animate: true);
			}
		};
		root.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Play(id);
		};
		return new Card(id, root, scale);
	}

	private void DrawRunnerArt(DrawingContext dc, double w, double h)
	{
		dc.DrawRectangle(GamePalette.Vertical((GamePalette.Mix(Pal.Bg, Pal.Accent2, 0.45), 0.0), (GamePalette.Mix(Pal.Bg, Pal.Yellow, 0.18), 1.0)), null, new Rect(0.0, 0.0, w, h));
		dc.DrawEllipse(GamePalette.Solid(Pal.Yellow, 0.9), null, new Point(w * 0.8, h * 0.28), 18.0, 18.0);
		dc.DrawEllipse(GamePalette.Solid(Pal.Yellow, 0.18), null, new Point(w * 0.8, h * 0.28), 30.0, 30.0);
		StreamGeometry hills = new StreamGeometry();
		using (StreamGeometryContext c = hills.Open())
		{
			c.BeginFigure(new Point(0.0, h), isFilled: true, isClosed: true);
			c.LineTo(new Point(0.0, h * 0.62), isStroked: false, isSmoothJoin: true);
			c.BezierTo(new Point(w * 0.2, h * 0.42), new Point(w * 0.35, h * 0.7), new Point(w * 0.55, h * 0.55), isStroked: false, isSmoothJoin: true);
			c.BezierTo(new Point(w * 0.75, h * 0.4), new Point(w * 0.9, h * 0.66), new Point(w, h * 0.58), isStroked: false, isSmoothJoin: true);
			c.LineTo(new Point(w, h), isStroked: false, isSmoothJoin: true);
		}
		hills.Freeze();
		dc.DrawGeometry(GamePalette.Solid(GamePalette.Mix(Pal.Bg, Pal.Accent, 0.35), 0.85), null, hills);
		double gy = h * 0.86;
		dc.DrawRectangle(GamePalette.Solid(GamePalette.Mix(Pal.Surface, Pal.Accent, 0.2)), null, new Rect(0.0, gy, w, h - gy));
		dc.DrawLine(GamePalette.Pen(Pal.Accent, 2.0), new Point(0.0, gy), new Point(w, gy));
		RunnerArt art = new RunnerArt(Pal);
		TransformPool pool = new TransformPool();
		art.Crystal(dc, pool, 150.0, gy, 1.0, 1.0, 0);
		art.Crystal(dc, pool, 168.0, gy, 0.75, 0.7, 1);
		art.Pumpkin(dc, pool, 226.0, gy, 0.9);
		art.Bat(dc, pool, 190.0, 58.0, 1.0, 0);
	}

	private void DrawTetrisArt(DrawingContext dc, double w, double h)
	{
		dc.DrawRectangle(GamePalette.Vertical((GamePalette.Mix(Pal.Bg, Pal.Accent, 0.22), 0.0), (GamePalette.Darken(Pal.Bg, 0.2), 1.0)), null, new Rect(0.0, 0.0, w, h));
		double c = 18.0;
		double ox = 26.0;
		double oy = h - c * 6 - 8.0;
		dc.DrawRoundedRectangle(GamePalette.Solid(Pal.Surface, 0.5), GamePalette.Pen(Pal.Fg, 1.0, 0.12), new Rect(ox - 4.0, oy - 4.0, c * 8 + 8.0, c * 6 + 8.0), 8.0, 8.0);
		CellStyle[] styles = TetrisArt.Styles(Pal);
		string[] rows = { "........", "...2....", "..222...", "6.....11", "66.4.311", "6544433." };
		for (int y = 0; y < rows.Length; y++)
		{
			for (int x = 0; x < rows[y].Length; x++)
			{
				char ch = rows[y][x];
				if (ch != '.')
				{
					styles[ch - '0'].Draw(dc, ox + x * c, oy + y * c, c);
				}
			}
		}
	}

	private TextBlock Text(string text, double size, Color color, FontWeight weight)
	{
		return new TextBlock
		{
			Text = text,
			FontSize = size,
			FontWeight = weight,
			FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
			Foreground = GamePalette.Solid(color),
			HorizontalAlignment = HorizontalAlignment.Center,
			TextAlignment = TextAlignment.Center
		};
	}
}
