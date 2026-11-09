using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Metamorph.Core.Localization;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Games;

internal abstract class GamePage : Grid
{
	protected readonly App App;

	protected readonly GamePalette Pal;

	protected GamePage(App app, GamePalette pal)
	{
		App = app;
		Pal = pal;
		Focusable = false;
	}

	public abstract string Id { get; }

	public abstract string Title { get; }

	public virtual void OnShown()
	{
	}

	// Окно потеряло фокус — игра встаёт на паузу.
	public virtual void OnDeactivated()
	{
	}

	public virtual void OnActivated()
	{
	}

	public abstract bool OnKey(Key key, bool down, bool repeat);

	// true — Esc обработан страницей (пауза); false — уйти назад.
	public virtual bool OnEscape()
	{
		return false;
	}

	public virtual void Close()
	{
	}
}

// Окно игр: без рамки, скруглённое, свой заголовок; страницы — хаб, раннер, тетрис.
internal sealed class GameWindow : Window
{
	private const double Radius = 18.0;

	private readonly App _app;

	private readonly GamePalette _pal;

	private readonly Border _host = new Border();

	private readonly TextBlock _title = new TextBlock();

	private readonly Border _back;

	private readonly Grid _root = new Grid();

	private GamePage? _page;

	public GameWindow(App app, GamePalette pal)
	{
		_app = app;
		_pal = pal;
		Title = Loc.T("Игры с Метой") + " — Metamorph";
		Width = 1000.0;
		Height = 720.0;
		MinWidth = 720.0;
		MinHeight = 540.0;
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		ResizeMode = ResizeMode.CanResizeWithGrip;
		ShowInTaskbar = true;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		UseLayoutRounding = true;
		try
		{
			Icon = Application.Current?.MainWindow?.Icon;
		}
		catch
		{
		}
		Border frame = new Border
		{
			CornerRadius = new CornerRadius(Radius),
			Background = GamePalette.Vertical((pal.Bg, 0.0), (GamePalette.Mix(pal.Bg, pal.Accent, 0.07), 1.0)),
			BorderBrush = GamePalette.Solid(pal.Accent, 0.35),
			BorderThickness = new Thickness(1.0),
			Child = _root
		};
		_root.SizeChanged += delegate
		{
			RectangleGeometry clip = new RectangleGeometry(new Rect(0.0, 0.0, _root.ActualWidth, _root.ActualHeight), Radius - 1.0, Radius - 1.0);
			clip.Freeze();
			_root.Clip = clip;
		};
		_root.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(44.0)
		});
		_root.RowDefinitions.Add(new RowDefinition());
		_back = ChromeButton("‹", Loc.T("В меню"), delegate
		{
			Navigate(null, force: true);
		}, danger: false);
		Border close = ChromeButton("✕", Loc.T("Закрыть"), Close, danger: true);
		DockPanel bar = new DockPanel
		{
			Background = GamePalette.Solid(pal.Surface, 0.35),
			LastChildFill = true
		};
		DockPanel.SetDock(close, Dock.Right);
		bar.Children.Add(close);
		DockPanel.SetDock(_back, Dock.Left);
		bar.Children.Add(_back);
		TextBlock glyph = new TextBlock
		{
			Text = "🎮",
			FontSize = 16.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(12.0, 0.0, 8.0, 0.0)
		};
		DockPanel.SetDock(glyph, Dock.Left);
		bar.Children.Add(glyph);
		_title.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
		_title.FontSize = 14.0;
		_title.FontWeight = FontWeights.SemiBold;
		_title.Foreground = GamePalette.Solid(pal.Fg);
		_title.VerticalAlignment = VerticalAlignment.Center;
		bar.Children.Add(_title);
		bar.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
				return;
			}
			try
			{
				DragMove();
			}
			catch (InvalidOperationException)
			{
			}
		};
		Grid.SetRow(bar, 0);
		_root.Children.Add(bar);
		Grid.SetRow(_host, 1);
		_root.Children.Add(_host);
		Content = frame;
		SourceInitialized += delegate
		{
			try
			{
				WindowEffects.UseDarkTitleBar(new WindowInteropHelper(this).Handle, pal.IsDark);
			}
			catch (Exception ex)
			{
				Log.Warn("Игры: тёмный заголовок недоступен: " + ex.Message);
			}
		};
		Deactivated += delegate
		{
			_page?.OnDeactivated();
		};
		Activated += delegate
		{
			_page?.OnActivated();
		};
		StateChanged += delegate
		{
			if (WindowState == WindowState.Minimized)
			{
				_page?.OnDeactivated();
			}
		};
		Closed += delegate
		{
			_page?.Close();
			_page = null;
			_host.Child = null;
		};
		PreviewKeyDown += delegate(object _, KeyEventArgs e)
		{
			OnKey(e, down: true);
		};
		PreviewKeyUp += delegate(object _, KeyEventArgs e)
		{
			OnKey(e, down: false);
		};
	}

	public string? CurrentId => _page?.Id;

	// force=false: если окно уже открыто без конкретной игры — не прерываем текущую партию.
	public void Navigate(string? id, bool force = false)
	{
		if (_page != null && !force && (id == null || id == _page.Id))
		{
			return;
		}
		GamePage page;
		try
		{
			page = id switch
			{
				GameHub.Dino => new RunnerPage(_app, _pal),
				GameHub.Tetris => new TetrisPage(_app, _pal),
				_ => new HubPage(_app, _pal, this)
			};
		}
		catch (Exception ex)
		{
			Log.Error("Игры: не удалось создать страницу", ex);
			return;
		}
		_page?.Close();
		_page = page;
		_host.Child = page;
		_title.Text = page.Title;
		_back.Visibility = page is HubPage ? Visibility.Collapsed : Visibility.Visible;
		page.OnShown();
		Keyboard.Focus(this);
	}

	private void OnKey(KeyEventArgs e, bool down)
	{
		if (_page == null)
		{
			return;
		}
		Key key = e.Key == Key.System ? e.SystemKey : e.Key;
		if (key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
		{
			return;
		}
		if (key == Key.Escape)
		{
			e.Handled = true;
			if (!down || e.IsRepeat || _page.OnEscape())
			{
				return;
			}
			if (_page is HubPage)
			{
				Close();
			}
			else
			{
				Navigate(null, force: true);
			}
			return;
		}
		if (_page.OnKey(key, down, e.IsRepeat))
		{
			e.Handled = true;
		}
	}

	private Border ChromeButton(string glyph, string tip, Action click, bool danger)
	{
		Brush hover = danger ? GamePalette.Solid(_pal.Red, 0.85) : GamePalette.Solid(_pal.Fg, 0.12);
		Border b = new Border
		{
			Width = 46.0,
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			ToolTip = tip,
			Child = new TextBlock
			{
				Text = glyph,
				FontSize = glyph == "‹" ? 22.0 : 13.0,
				Foreground = GamePalette.Solid(_pal.Fg, 0.85),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, glyph == "‹" ? -3.0 : 0.0, 0.0, 0.0)
			}
		};
		b.MouseEnter += delegate
		{
			b.Background = hover;
		};
		b.MouseLeave += delegate
		{
			b.Background = Brushes.Transparent;
		};
		b.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
		};
		b.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			try
			{
				click();
			}
			catch (Exception ex)
			{
				Log.Error("Игры: кнопка заголовка", ex);
			}
		};
		return b;
	}
}
