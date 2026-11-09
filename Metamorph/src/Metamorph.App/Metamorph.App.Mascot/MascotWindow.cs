using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Audio;
using Metamorph.App.Desktop;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Mascot;

internal sealed class MascotWindow : Window
{
	private const double BubbleSpace = 140.0;

	private readonly App _app;

	private readonly Grid _root = new Grid();

	private readonly MascotCanvas _canvas = new MascotCanvas();

	private readonly Grid _bubbleHost = new Grid
	{
		VerticalAlignment = VerticalAlignment.Top,
		IsHitTestVisible = true
	};

	private readonly Border _bubble;

	private readonly TextBlock _bubbleText;

	private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render)
	{
		Interval = TimeSpan.FromMilliseconds(33.0)
	};

	private readonly DispatcherTimer _slow = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private readonly float[] _bands = new float[48];

	private MascotColors _colors = new MascotColors();

	private Palette? _palette;

	private IDisposable? _spectrum;

	private nint _hwnd;

	private double _last;

	private double _feetX;

	private double _feetY;

	private double _w;

	private double _h;

	private double _scaleShown;

	private bool _mediaPlaying;

	private bool _hiddenForFullscreen;

	private bool _pressed;

	private bool _dragging;

	private bool _mediaBusy;

	private Point _down;

	private string? _trackKey;

	private int _ticks;

	private int _fullscreenStreak;

	public MascotBrain Brain { get; }

	public double PositionFraction
	{
		get
		{
			Rect workAreaDip = Display.PrimaryDisplay.WorkAreaDip;
			if (!(workAreaDip.Width > 0.0))
			{
				return 0.8;
			}
			return Math.Clamp((Brain.X - workAreaDip.Left) / workAreaDip.Width, 0.0, 1.0);
		}
	}

	public MascotWindow(App app, MascotSettings settings, Palette? palette)
	{
		_app = app;
		Brain = new MascotBrain(settings);
		_palette = palette;
		_colors = MascotColors.From(Brain.Settings, palette);
		base.Title = "Metamorph — " + Brain.Settings.Name;
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.Topmost = true;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.Focusable = false;
		base.SnapsToDevicePixels = true;
		_bubbleText = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			MaxWidth = 250.0,
			FontSize = 13.0,
			LineHeight = 18.0,
			Foreground = Res("TextBrush", Brushes.White)
		};
		_bubble = new Border
		{
			Child = _bubbleText,
			Padding = new Thickness(12.0, 7.0, 12.0, 8.0),
			CornerRadius = new CornerRadius(14.0),
			Background = Res("CardBrush", new SolidColorBrush(Color.FromRgb(40, 40, 58))),
			BorderBrush = Res("AccentBrush", Brushes.HotPink),
			BorderThickness = new Thickness(1.5),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(6.0, 0.0, 6.0, 9.0),
			Opacity = 0.0,
			Visibility = Visibility.Collapsed,
			Cursor = Cursors.Hand,
			Effect = new DropShadowEffect
			{
				BlurRadius = 10.0,
				ShadowDepth = 2.0,
				Opacity = 0.35
			}
		};
		Path path = new Path
		{
			Data = Geometry.Parse("M0,0 L14,0 L5,10 Z"),
			Fill = _bubble.Background,
			Stroke = _bubble.BorderBrush,
			StrokeThickness = 1.5,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			IsHitTestVisible = false
		};
		path.SetBinding(UIElement.VisibilityProperty, new Binding("Visibility")
		{
			Source = _bubble
		});
		path.SetBinding(UIElement.OpacityProperty, new Binding("Opacity")
		{
			Source = _bubble
		});
		_bubbleHost.Children.Add(_bubble);
		_bubbleHost.Children.Add(path);
		_bubble.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			Brain.Say(null);
			e.Handled = true;
		};
		_root.Children.Add(_canvas);
		_root.Children.Add(_bubbleHost);
		base.Content = _root;
		Layout();
		_canvas.MouseLeftButtonDown += OnDown;
		_canvas.MouseMove += OnMove;
		_canvas.MouseLeftButtonUp += OnUp;
		_canvas.LostMouseCapture += delegate
		{
			EndDrag();
		};
		_canvas.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			ShowMenu();
			e.Handled = true;
		};
		_canvas.Cursor = Cursors.Hand;
		Brain.SpeechChanged += OnSpeech;
		_timer.Tick += delegate
		{
			Frame();
		};
		_slow.Tick += delegate
		{
			SlowTick();
		};
		base.SourceInitialized += delegate
		{
			_hwnd = new WindowInteropHelper(this).Handle;
			WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		};
		SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
		base.Closed += delegate
		{
			SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
			_timer.Stop();
			_slow.Stop();
			_spectrum?.Dispose();
			_spectrum = null;
		};
	}

	public void Start(bool hello, bool drop)
	{
		Rect rect = UpdateWorld(Display.PrimaryDisplay);
		double? position = Brain.Settings.Position;
		double num;
		if (position.HasValue)
		{
			double valueOrDefault = position.GetValueOrDefault();
			num = rect.Left + valueOrDefault * rect.Width;
		}
		else
		{
			num = rect.Left + rect.Width * 0.82;
		}
		double x = num;
		if (drop)
		{
			Brain.Drop(x, rect.Top + rect.Height * 0.25);
		}
		else
		{
			Brain.Place(x);
		}
		_last = _clock.Elapsed.TotalSeconds;
		Frame();
		Show();
		_timer.Start();
		_slow.Start();
		if (hello)
		{
			DispatcherTimer t = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1.5)
			};
			t.Tick += delegate
			{
				t.Stop();
				Brain.Notify(MascotEvent.Hello);
			};
			t.Start();
		}
	}

	public void ApplySettings(MascotSettings s)
	{
		Brain.Settings = MascotSettings.Sanitize(s);
		_colors = MascotColors.From(Brain.Settings, _palette);
		if (!Brain.Settings.Dance)
		{
			_spectrum?.Dispose();
			_spectrum = null;
		}
		if (Math.Abs(_scaleShown - Brain.Settings.Scale) > 0.001)
		{
			Layout();
			UpdateWorld(CurrentDisplay());
		}
		if (!Brain.Settings.HideInFullscreen && _hiddenForFullscreen)
		{
			SetFullscreenHidden(hide: false);
		}
	}

	public void SetPalette(Palette? palette)
	{
		_palette = palette;
		_colors = MascotColors.From(Brain.Settings, palette);
		Brush brush = Res("AccentBrush", Brushes.HotPink);
		_bubble.BorderBrush = brush;
		_bubble.Background = Res("CardBrush", _bubble.Background);
		_bubbleText.Foreground = Res("TextBrush", Brushes.White);
		foreach (Path item in _bubbleHost.Children.OfType<Path>())
		{
			item.Fill = _bubble.Background;
			item.Stroke = brush;
		}
	}

	private void Layout()
	{
		double unitScale = Brain.UnitScale;
		_scaleShown = Brain.Settings.Scale;
		_w = Math.Max(330.0 * unitScale, 290.0);
		_h = 290.0 * unitScale + 140.0;
		_feetX = _w / 2.0;
		_feetY = _h - 24.0 * unitScale;
		base.Width = _w;
		base.Height = _h;
		_bubbleHost.Height = Math.Max(10.0, _feetY - 196.0 * unitScale);
	}

	private void Frame()
	{
		try
		{
			double totalSeconds = _clock.Elapsed.TotalSeconds;
			double dt = Math.Clamp(totalSeconds - _last, 0.0, 0.1);
			_last = totalSeconds;
			Point point = CursorDip();
			if (_dragging)
			{
				Brain.DragTo(point.X, point.Y, totalSeconds);
			}
			bool musicPlaying = false;
			double num = 0.0;
			if (_mediaPlaying && Brain.Settings.Dance)
			{
				if (_spectrum != null && AudioSpectrum.Shared.Read(_bands))
				{
					for (int i = 0; i < 6; i++)
					{
						num += (double)_bands[i];
					}
					num = Math.Clamp(num / 6.0 * 1.6, 0.0, 1.0);
					musicPlaying = true;
				}
				else
				{
					musicPlaying = true;
					num = 0.45 + 0.35 * Math.Max(0.0, Math.Sin(totalSeconds * 7.5));
				}
			}
			MascotInput mascotInput = default(MascotInput);
			mascotInput.CursorX = point.X;
			mascotInput.CursorY = point.Y;
			mascotInput.UserIdleSeconds = Win32.IdleSeconds();
			mascotInput.MusicPlaying = musicPlaying;
			mascotInput.Bass = num;
			mascotInput.Now = DateTime.Now;
			MascotInput input = mascotInput;
			MascotPose p = Brain.Update(dt, in input);
			double unitScale = Brain.UnitScale;
			Affine view = Affine.Scale(unitScale, unitScale).Then(Affine.Translate(_feetX, _feetY));
			double num2 = Brain.Ground - Brain.Y;
			double num3 = _feetY + num2;
			double alpha = ((num3 < _h - 1.0) ? (1.0 - Math.Clamp(num2 / (90.0 * unitScale), 0.0, 1.0)) : 0.0);
			_canvas.SetShadow(new Point(_feetX, num3 - 1.0), 92.0 * unitScale * (1.0 - 0.4 * Math.Clamp(num2 / (90.0 * unitScale), 0.0, 1.0)), alpha);
			_canvas.SetItems(MascotRig.Render(p, _colors, view));
			double num4 = Math.Round(Brain.X - _feetX, 1);
			double num5 = Math.Round(Brain.Y - _feetY, 1);
			if (Math.Abs(num4 - base.Left) > 0.05 || Math.Abs(num5 - base.Top) > 0.05)
			{
				base.Left = num4;
				base.Top = num5;
			}
			int num6 = ((Brain.State == MascotState.Sleep && Brain.Speech == null) ? 66 : 33);
			if ((int)_timer.Interval.TotalMilliseconds != num6)
			{
				_timer.Interval = TimeSpan.FromMilliseconds(num6);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Маскот: кадр", ex);
		}
	}

	private void SlowTick()
	{
		try
		{
			_ticks++;
			bool flag = Brain.Settings.HideInFullscreen && DesktopLayer.IsFullscreenAppActive();
			if (flag != _hiddenForFullscreen)
			{
				if (++_fullscreenStreak >= 2)
				{
					_fullscreenStreak = 0;
					SetFullscreenHidden(flag);
				}
			}
			else
			{
				_fullscreenStreak = 0;
			}
			if (_ticks % 2 == 0)
			{
				PollMediaAsync();
			}
			if (_ticks % 5 == 0 && !_dragging && Brain.State != MascotState.Held)
			{
				UpdateWorld(CurrentDisplay());
				if (_hwnd != IntPtr.Zero && !_hiddenForFullscreen)
				{
					NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 19u);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("Маскот: таймер", ex);
		}
	}

	private void SetFullscreenHidden(bool hide)
	{
		_hiddenForFullscreen = hide;
		if (hide)
		{
			_timer.Stop();
			if (_dragging)
			{
				EndDrag();
			}
			Hide();
		}
		else
		{
			_last = _clock.Elapsed.TotalSeconds;
			Show();
			_timer.Start();
		}
	}

	private async Task PollMediaAsync()
	{
		if (_mediaBusy)
		{
			return;
		}
		_mediaBusy = true;
		try
		{
			MediaNow mediaNow = await MediaInfo.GetAsync();
			bool flag = mediaNow?.Playing ?? false;
			if (flag != _mediaPlaying)
			{
				_mediaPlaying = flag;
				if (flag && Brain.Settings.Dance)
				{
					if (_spectrum == null)
					{
						_spectrum = AudioSpectrum.Shared.Acquire();
					}
				}
				else
				{
					_spectrum?.Dispose();
					_spectrum = null;
				}
			}
			if ((object)mediaNow != null && flag)
			{
				string text = mediaNow.App + "|" + mediaNow.Artist + "|" + mediaNow.Title;
				if (_trackKey != null && text != _trackKey)
				{
					Brain.Notify(MascotEvent.TrackChanged, mediaNow.Title);
				}
				_trackKey = text;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Маскот: медиа", ex);
		}
		finally
		{
			_mediaBusy = false;
		}
	}

	private Display CurrentDisplay()
	{
		double scale = Display.Scale;
		int x = (int)(Brain.X * scale);
		int y = (int)((Brain.Y - Brain.Height / 2.0) * scale);
		return Display.All().FirstOrDefault((Display d) => d.Contains(x, y)) ?? Display.PrimaryDisplay;
	}

	private Rect UpdateWorld(Display display)
	{
		Rect workAreaDip = display.WorkAreaDip;
		Brain.SetWorld(workAreaDip.Left, workAreaDip.Top, workAreaDip.Right, workAreaDip.Bottom);
		return workAreaDip;
	}

	private void OnDisplayChanged(object? sender, EventArgs e)
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			try
			{
				UpdateWorld(CurrentDisplay());
			}
			catch (Exception ex)
			{
				Log.Error("Маскот: мониторы", ex);
			}
		});
	}

	private static Point CursorDip()
	{
		Win32.GetCursorPos(out var pt);
		double scale = Display.Scale;
		return new Point((double)pt.X / scale, (double)pt.Y / scale);
	}

	private void OnDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (e.ClickCount >= 2)
		{
			_pressed = false;
			_app.ShowAssistant();
			return;
		}
		_pressed = true;
		_dragging = false;
		_down = CursorDip();
		_canvas.CaptureMouse();
	}

	private void OnMove(object sender, MouseEventArgs e)
	{
		if (_pressed && !_dragging)
		{
			Point point = CursorDip();
			if (!(Math.Abs(point.X - _down.X) + Math.Abs(point.Y - _down.Y) < 6.0))
			{
				_dragging = true;
				Brain.Grab(_down.X, _down.Y, _clock.Elapsed.TotalSeconds);
				Brain.DragTo(point.X, point.Y, _clock.Elapsed.TotalSeconds);
			}
		}
	}

	private void OnUp(object sender, MouseButtonEventArgs e)
	{
		if (_pressed)
		{
			e.Handled = true;
			_pressed = false;
			if (_dragging)
			{
				EndDrag();
				return;
			}
			_canvas.ReleaseMouseCapture();
			double num = (e.GetPosition(_canvas).Y - _feetY) / Brain.UnitScale;
			Brain.Poke(num < -105.0);
		}
	}

	private void EndDrag()
	{
		if (_dragging)
		{
			_dragging = false;
			_pressed = false;
			Point p = CursorDip();
			double s = Display.Scale;
			Display display = Display.All().FirstOrDefault((Display d) => d.Contains((int)(p.X * s), (int)(p.Y * s))) ?? CurrentDisplay();
			Brain.DragTo(p.X, p.Y, _clock.Elapsed.TotalSeconds);
			UpdateWorld(display);
			Brain.Release(_clock.Elapsed.TotalSeconds);
			if (_canvas.IsMouseCaptured)
			{
				_canvas.ReleaseMouseCapture();
			}
		}
	}

	private void ShowMenu()
	{
		ContextMenu menu = new ContextMenu
		{
			Placement = PlacementMode.MousePoint,
			PlacementTarget = _canvas
		};
		Add(Loc.T("Поговорить…"), _app.ShowAssistant);
		menu.Items.Add(new Separator());
		Add(Loc.T("Помаши"), delegate
		{
			Brain.Command(MascotAction.Wave);
		});
		Add(Loc.T("Потанцуй"), delegate
		{
			Brain.Command(MascotAction.Dance);
		});
		Add(Loc.T("Подпрыгни"), delegate
		{
			Brain.Command(MascotAction.Jump);
		});
		Add(Loc.T("Посиди"), delegate
		{
			Brain.Command(MascotAction.Sit);
		});
		Add(Loc.T("Поспи"), delegate
		{
			Brain.Command(MascotAction.Sleep);
		});
		menu.Items.Add(new Separator());
		Add(Loc.T("Настройки Мета-тян…"), _app.ShowMascotTab);
		Add(Loc.T("Спрятать"), _app.ToggleMascot);
		menu.IsOpen = true;
		void Add(string text, Action action)
		{
			Action action2 = action;
			MenuItem menuItem = new MenuItem
			{
				Header = text
			};
			menuItem.Click += delegate
			{
				try
				{
					action2();
				}
				catch (Exception ex)
				{
					Log.Error("Маскот: меню", ex);
				}
			};
			menu.Items.Add(menuItem);
		}
	}

	private void OnSpeech(string? text)
	{
		if (text == null)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220.0));
			doubleAnimation.Completed += delegate
			{
				if (Brain.Speech == null)
				{
					_bubble.Visibility = Visibility.Collapsed;
				}
			};
			_bubble.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			return;
		}
		_bubbleText.Text = text;
		_bubble.Visibility = Visibility.Visible;
		_bubble.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160.0)));
		ScaleTransform scaleTransform = new ScaleTransform(0.85, 0.85);
		_bubble.RenderTransformOrigin = new Point(0.5, 1.0);
		_bubble.RenderTransform = scaleTransform;
		DoubleAnimation animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = new BackEase
			{
				Amplitude = 0.4,
				EasingMode = EasingMode.EaseOut
			}
		};
		scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
	}

	private static Brush Res(string key, Brush fallback)
	{
		return (Application.Current?.TryFindResource(key) as Brush) ?? fallback;
	}
}
