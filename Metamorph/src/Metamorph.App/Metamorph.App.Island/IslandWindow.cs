using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Audio;
using Metamorph.App.Desktop;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Assistant;
using Metamorph.Core.Audio;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Windows.Networking.Connectivity;

namespace Metamorph.App.Island;

internal sealed class IslandWindow : Window
{
	private enum Mode
	{
		None,
		Idle,
		Compact,
		Expanded,
		Activity
	}

	private const string Mdl2 = "Segoe MDL2 Assets";

	private const double ExpandedMediaHeight = 164.0;

	private const double ExpandedIdleHeight = 118.0;

	private const int MiniBars = 5;

	private const int BigBars = 7;

	private readonly ThemeProfile _profile;

	private readonly IslandSettings _s;

	private readonly Display _display;

	private readonly Action _openMainWindow;

	private readonly CoreAudio _audio = new CoreAudio();

	private readonly DispatcherTimer _poll = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(250.0)
	};

	private readonly DispatcherTimer _hoverTimer = new DispatcherTimer();

	private readonly DispatcherTimer _edgeTimer = new DispatcherTimer(DispatcherPriority.Input)
	{
		Interval = TimeSpan.FromMilliseconds(60.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private readonly System.Windows.Media.FontFamily _font;

	private readonly System.Windows.Media.Brush _fg;

	private readonly System.Windows.Media.Brush _muted;

	private readonly System.Windows.Media.Brush _accent;

	private readonly System.Windows.Media.Brush _accent2;

	private readonly System.Windows.Media.Brush _surface;

	private readonly System.Windows.Media.Brush _alert;

	private readonly System.Windows.Media.Brush _green;

	private readonly Border _pill;

	private readonly Grid _stage;

	private readonly Grid _idleView;

	private readonly Grid _compactView;

	private readonly Grid _mediaView;

	private readonly Grid _infoView;

	private readonly Grid _activityView;

	private readonly TextBlock _idleClock = new TextBlock();

	private readonly Border _compactArt = new Border();

	private readonly Border _bigArt = new Border();

	private readonly TextBlock _compactTitle = new TextBlock();

	private readonly TextBlock _title = new TextBlock();

	private readonly TextBlock _artist = new TextBlock();

	private readonly TextBlock _source = new TextBlock();

	private readonly TextBlock _elapsed = new TextBlock();

	private readonly TextBlock _total = new TextBlock();

	private readonly TextBlock _playGlyph = new TextBlock();

	private readonly ColumnDefinition _progressDone = new ColumnDefinition();

	private readonly ColumnDefinition _progressLeft = new ColumnDefinition();

	private readonly Grid _progressRow = new Grid();

	private readonly ScaleTransform[] _miniBars = new ScaleTransform[5];

	private readonly ScaleTransform[] _bigBars = new ScaleTransform[7];

	private readonly TextBlock _infoTime = new TextBlock();

	private readonly TextBlock _infoDate = new TextBlock();

	private readonly TextBlock _infoStatus = new TextBlock();

	private readonly TextBlock _activityGlyph = new TextBlock();

	private readonly TextBlock _activityLabel = new TextBlock();

	private readonly TextBlock _activityValue = new TextBlock();

	private readonly Grid _activityBar = new Grid();

	private readonly ColumnDefinition _activityDone = new ColumnDefinition();

	private readonly ColumnDefinition _activityLeft = new ColumnDefinition();

	private Mode _mode;

	private FrameworkElement? _currentView;

	private MediaDetails? _media;

	private DateTime _mediaFetched;

	private bool _mediaBusy;

	private bool _mediaInitialized;

	private ImageSource? _art;

	private bool _hover;

	private bool _hoverWanted;

	private bool _pinned;

	private bool _fullscreenHidden;

	private bool _closed;

	private DateTime _trackPopupUntil;

	private DateTime _activityUntil;

	private (float Level, bool Muted)? _lastVolume;

	private bool? _lastCaps;

	private (nint Hwnd, int Lang)? _lastLayout;

	private System.Windows.Forms.PowerLineStatus? _lastLine;

	private int _lastBatteryBucket = 100;

	private IDisposable? _audioLease;

	private readonly float[] _raw = new float[48];

	private readonly SpectrumSmoother _smooth = new SpectrumSmoother(7)
	{
		Attack = 28.0,
		Decay = 9.0
	};

	private bool _rendering;

	private double _lastRender;

	private int _tick;

	private readonly string? _announce;

	private nint _hwnd;

	private Display _current;

	private DateTime _summonUntil;

	private DateTime _edgeSince = DateTime.MaxValue;

	private DateTime _lastTopmost;

	private bool _summonActive;

	private bool _pendingSummon;

	private bool _loaded;

	private readonly StackPanel _privacyDots = new StackPanel
	{
		Orientation = System.Windows.Controls.Orientation.Horizontal,
		HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
		IsHitTestVisible = false
	};

	private readonly Ellipse _micDot = new Ellipse
	{
		Width = 6.0,
		Height = 6.0,
		Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
		Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 159, 10))
	};

	private readonly Ellipse _camDot = new Ellipse
	{
		Width = 6.0,
		Height = 6.0,
		Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
		Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 209, 88))
	};

	private PrivacySnapshot _privacy = PrivacySnapshot.Empty;

	private bool _privacyRead;

	private (bool Online, bool Wifi, string Name)? _lastNet;

	private DispatcherTimer? _clipTimer;

	private bool _clipListening;

	private const int WmClipboardUpdate = 797;

	private TextBlock? _playButton;

	private DateTime _animatingUntil;

	private int _fullscreenStreak;

	// Активное окно залезает под пилюлю — в покое прячемся, чтобы не закрывать вкладки и заголовок.
	private bool _dodging;

	private int _dodgeStreak;

	private bool _dodgeHidden;

	private bool Summoned => DateTime.UtcNow < _summonUntil;

	private bool Notch => _s.Style == IslandStyle.Notch;

	private double ActivityWidth => Math.Max(_s.Width, 270);

	private bool ShowPrivacy => Has("privacy");

	private double DotsWidth => ShowPrivacy ? (((_privacy.Microphone.Count > 0) ? 10 : 0) + ((_privacy.Camera.Count > 0) ? 10 : 0)) : 0;

	private bool IsAnimating => DateTime.UtcNow < _animatingUntil;

	internal static FocusTimer? FocusSource { get; set; }

	public IslandWindow(ThemeProfile profile, Display display, Action openMainWindow, string? announce)
	{
		_profile = profile;
		_s = profile.Island;
		_display = display;
		_current = display;
		_openMainWindow = openMainWindow;
		_announce = announce;
		Palette palette = profile.Palette;
		_fg = BrushFactory.Solid(palette.Foreground);
		_muted = BrushFactory.Solid(palette.Muted);
		_accent = BrushFactory.Solid(palette.Accent);
		_accent2 = BrushFactory.Solid(palette.Accent2);
		_surface = BrushFactory.Solid(palette.Surface);
		_alert = BrushFactory.Solid(palette.Red);
		_green = BrushFactory.Solid(palette.Green);
		_font = new System.Windows.Media.FontFamily(profile.Bar.FontFamily + ", Segoe UI");
		base.Title = "Metamorph Island";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = System.Windows.Media.Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.Focusable = false;
		base.FontFamily = _font;
		base.Foreground = _fg;
		base.Width = Math.Max(Math.Max(_s.ExpandedWidth, _s.Width), 300) + 48;
		base.Height = 204.0;
		_idleView = BuildIdle();
		_compactView = BuildCompact();
		_mediaView = BuildMedia();
		_infoView = BuildInfo();
		_activityView = BuildActivity();
		_stage = new Grid
		{
			Children = 
			{
				(UIElement)_idleView,
				(UIElement)_compactView,
				(UIElement)_mediaView,
				(UIElement)_infoView,
				(UIElement)_activityView
			}
		};
		foreach (FrameworkElement child in _stage.Children)
		{
			child.Opacity = 0.0;
			child.IsHitTestVisible = false;
			child.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
			child.VerticalAlignment = VerticalAlignment.Center;
		}
		_privacyDots.Children.Add(_micDot);
		_privacyDots.Children.Add(_camDot);
		_privacyDots.Visibility = Visibility.Collapsed;
		_stage.Children.Add(_privacyDots);
		System.Windows.Media.Color color;
		try
		{
			color = BrushFactory.ToColor(_s.Background ?? palette.Background, Math.Clamp(_s.Opacity, 0.2, 1.0));
		}
		catch
		{
			color = System.Windows.Media.Color.FromArgb(240, 0, 0, 0);
		}
		_pill = new Border
		{
			Background = new SolidColorBrush(color),
			BorderBrush = BrushFactory.Solid(palette.Surface, 0.9),
			BorderThickness = ((_s.Style == IslandStyle.Notch) ? new Thickness(1.0, 0.0, 1.0, 1.0) : new Thickness(1.0)),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Width = 60.0,
			Height = _s.Height,
			Child = _stage,
			Cursor = System.Windows.Input.Cursors.Hand,
			Effect = new DropShadowEffect
			{
				BlurRadius = 18.0,
				ShadowDepth = 3.0,
				Direction = 270.0,
				Opacity = 0.45,
				Color = Colors.Black
			}
		};
		_pill.SizeChanged += delegate
		{
			UpdateShape();
		};
		_pill.MouseEnter += delegate
		{
			Hover(inside: true);
		};
		_pill.MouseLeave += delegate
		{
			Hover(inside: false);
		};
		_pill.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			_pinned = !_pinned;
			Resolve();
			e.Handled = true;
		};
		_pill.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			OpenMenu();
			e.Handled = true;
		};
		_pill.MouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			Safe(delegate
			{
				(float, bool)? tuple = _audio.Read();
				if (tuple.HasValue)
				{
					(float, bool) valueOrDefault = tuple.GetValueOrDefault();
					_audio.SetLevel(Math.Clamp(valueOrDefault.Item1 + (float)Math.Sign(e.Delta) * 0.05f, 0f, 1f));
				}
				CheckVolume(force: true);
			});
			e.Handled = true;
		};
		base.Content = new Grid
		{
			Children = { (UIElement)_pill },
			Margin = new Thickness(0.0, 0.0, 0.0, 0.0)
		};
		_hoverTimer.Tick += delegate
		{
			_hoverTimer.Stop();
			_hover = _hoverWanted && _s.ExpandOnHover;
			Resolve();
		};
		_poll.Tick += delegate
		{
			Safe(Tick);
		};
		_edgeTimer.Tick += delegate
		{
			Safe(EdgeTick);
		};
		base.SourceInitialized += delegate
		{
			_hwnd = new WindowInteropHelper(this).Handle;
			WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
			if (Has("clipboard"))
			{
				HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
				_clipListening = NativeMethods.AddClipboardFormatListener(_hwnd);
			}
		};
		base.Loaded += delegate
		{
			Safe(OnLoaded);
		};
		base.Closed += delegate
		{
			Shutdown();
		};
	}

	private void OnLoaded()
	{
		Reposition();
		UpdateClock();
		_lastVolume = _audio.Read();
		_lastCaps = System.Windows.Forms.Control.IsKeyLocked(Keys.Capital);
		PowerStatus powerStatus = SystemInformation.PowerStatus;
		_lastLine = powerStatus.PowerLineStatus;
		_lastBatteryBucket = BatteryBucket((int)Math.Round(powerStatus.BatteryLifePercent * 100f));
		if (_announce != null && Has("theme"))
		{
			ShowActivity("\ue790", "Segoe MDL2 Assets", Loc.T("Тема"), _announce, null, _accent);
		}
		_loaded = true;
		Resolve();
		_poll.Start();
		_edgeTimer.Start();
		RefreshMediaAsync();
		if (_pendingSummon)
		{
			Summon(toggle: false);
		}
	}

	private void Shutdown()
	{
		_closed = true;
		_poll.Stop();
		_edgeTimer.Stop();
		_hoverTimer.Stop();
		StopRendering();
		_audioLease?.Dispose();
		_audioLease = null;
		_clipTimer?.Stop();
		if (_clipListening && _hwnd != IntPtr.Zero)
		{
			NativeMethods.RemoveClipboardFormatListener(_hwnd);
		}
		_clipListening = false;
	}

	private static void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error("Остров", ex);
		}
	}

	private bool Has(string activity)
	{
		string activity2 = activity;
		return _s.Activities.Any((string a) => a.Trim().Equals(activity2, StringComparison.OrdinalIgnoreCase));
	}

	private TextBlock Text(TextBlock t, double size, System.Windows.Media.Brush brush, FontWeight? weight = null)
	{
		t.FontSize = size;
		t.Foreground = brush;
		t.FontWeight = weight ?? FontWeights.Normal;
		t.TextTrimming = TextTrimming.CharacterEllipsis;
		t.VerticalAlignment = VerticalAlignment.Center;
		return t;
	}

	private TextBlock Glyph(string glyph, double size, Action? click = null, System.Windows.Media.Brush? brush = null)
	{
		System.Windows.Media.Brush brush2 = brush;
		Action click2 = click;
		TextBlock t = new TextBlock
		{
			Text = glyph,
			FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
			FontSize = size,
			Foreground = (brush2 ?? _fg),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Margin = new Thickness(14.0, 0.0, 14.0, 0.0),
			Background = System.Windows.Media.Brushes.Transparent
		};
		if (click2 != null)
		{
			t.Cursor = System.Windows.Input.Cursors.Hand;
			t.MouseEnter += delegate
			{
				t.Foreground = _accent;
			};
			t.MouseLeave += delegate
			{
				t.Foreground = brush2 ?? _fg;
			};
			t.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
			{
				Safe(click2);
				e.Handled = true;
			};
		}
		return t;
	}

	private StackPanel Bars(ScaleTransform[] target, double height, double width, double gap)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
			Height = height
		};
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(((SolidColorBrush)_accent).Color, ((SolidColorBrush)_accent2).Color, 90.0);
		linearGradientBrush.Freeze();
		for (int i = 0; i < target.Length; i++)
		{
			target[i] = new ScaleTransform(1.0, 0.15);
			stackPanel.Children.Add(new System.Windows.Shapes.Rectangle
			{
				Width = width,
				Height = height,
				RadiusX = width / 2.0,
				RadiusY = width / 2.0,
				Fill = linearGradientBrush,
				Margin = new Thickness((i == 0) ? 0.0 : gap, 0.0, 0.0, 0.0),
				RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
				RenderTransform = target[i]
			});
		}
		return stackPanel;
	}

	private Border Art(Border b, double size, double radius)
	{
		double width = (b.Height = size);
		b.Width = width;
		b.CornerRadius = new CornerRadius(radius);
		b.Background = _surface;
		b.VerticalAlignment = VerticalAlignment.Center;
		b.Child = new TextBlock
		{
			Text = "\ue8d6",
			FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
			FontSize = size * 0.42,
			Foreground = _accent,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		return b;
	}

	private Grid BuildIdle()
	{
		Text(_idleClock, 13.0, _fg, FontWeights.SemiBold);
		_idleClock.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		Grid grid = new Grid
		{
			Height = _s.Height
		};
		if (_s.Idle == IslandIdle.Clock)
		{
			grid.Children.Add(_idleClock);
		}
		return grid;
	}

	private Grid BuildCompact()
	{
		int height = _s.Height;
		Grid obj = new Grid
		{
			Width = _s.Width,
			Height = height,
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		Border border = Art(_compactArt, height - 12, 6.0);
		border.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		if (!_s.AlbumArt)
		{
			border.Visibility = Visibility.Collapsed;
		}
		Text(_compactTitle, 12.5, _fg);
		_compactTitle.Margin = new Thickness(10.0, 0.0, 8.0, 0.0);
		StackPanel stackPanel = Bars(_miniBars, (double)height * 0.5, 3.0, 2.5);
		stackPanel.Margin = new Thickness(0.0, 0.0, 14.0, 0.0);
		if (!_s.Visualizer)
		{
			stackPanel.Visibility = Visibility.Collapsed;
		}
		Grid.SetColumn(_compactTitle, 1);
		Grid.SetColumn(stackPanel, 2);
		obj.Children.Add(border);
		obj.Children.Add(_compactTitle);
		obj.Children.Add(stackPanel);
		return obj;
	}

	private Grid BuildMedia()
	{
		Grid obj = new Grid
		{
			Width = _s.ExpandedWidth,
			Height = 164.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(16.0, 14.0, 16.0, 10.0)
		};
		obj.Children.Add(stackPanel);
		DockPanel dockPanel = new DockPanel
		{
			Height = 64.0,
			LastChildFill = true
		};
		Border border = Art(_bigArt, 64.0, 12.0);
		if (!_s.AlbumArt)
		{
			border.Visibility = Visibility.Collapsed;
		}
		DockPanel.SetDock(border, System.Windows.Controls.Dock.Left);
		dockPanel.Children.Add(border);
		StackPanel stackPanel2 = Bars(_bigBars, 30.0, 4.0, 3.0);
		stackPanel2.Margin = new Thickness(10.0, 0.0, 2.0, 0.0);
		if (!_s.Visualizer)
		{
			stackPanel2.Visibility = Visibility.Collapsed;
		}
		DockPanel.SetDock(stackPanel2, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(_s.AlbumArt ? 14 : 0, 0.0, 0.0, 0.0)
		};
		stackPanel3.Children.Add(Text(_title, 15.0, _fg, FontWeights.SemiBold));
		stackPanel3.Children.Add(Text(_artist, 12.5, _muted));
		stackPanel3.Children.Add(Text(_source, 11.0, _muted));
		_artist.Margin = new Thickness(0.0, 2.0, 0.0, 0.0);
		stackPanel3.Background = System.Windows.Media.Brushes.Transparent;
		stackPanel3.Cursor = System.Windows.Input.Cursors.Hand;
		stackPanel3.ToolTip = Loc.T("Открыть плеер");
		stackPanel3.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			MediaDetails i = _media;
			if ((object)i != null)
			{
				Safe(delegate
				{
					MediaInfo.OpenPlayer(i.App);
				});
			}
		};
		dockPanel.Children.Add(stackPanel3);
		stackPanel.Children.Add(dockPanel);
		_progressRow.Margin = new Thickness(0.0, 14.0, 0.0, 0.0);
		_progressRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		_progressRow.ColumnDefinitions.Add(new ColumnDefinition());
		_progressRow.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Text(_elapsed, 11.0, _muted);
		Text(_total, 11.0, _muted);
		Grid grid = new Grid
		{
			Height = 5.0,
			Margin = new Thickness(10.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid seekArea = new Grid
		{
			Background = System.Windows.Media.Brushes.Transparent,
			Height = 16.0,
			Cursor = System.Windows.Input.Cursors.Hand,
			ToolTip = Loc.T("Клик — перемотка")
		};
		seekArea.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			Safe(delegate
			{
				MediaDetails media = _media;
				if ((object)media != null && !(media.Duration <= TimeSpan.Zero) && !(seekArea.ActualWidth <= 0.0))
				{
					double num = Math.Clamp(e.GetPosition(seekArea).X / seekArea.ActualWidth, 0.0, 1.0);
					TimeSpan target = TimeSpan.FromTicks((long)((double)media.Duration.Ticks * num));
					_media = media with
					{
						Position = target
					};
					_mediaFetched = DateTime.UtcNow;
					UpdateProgress();
					MediaCommand(() => MediaInfo.SeekAsync(target));
				}
			});
		};
		grid.ColumnDefinitions.Add(_progressDone);
		grid.ColumnDefinitions.Add(_progressLeft);
		Border element = new Border
		{
			Background = _surface,
			CornerRadius = new CornerRadius(2.5)
		};
		Grid.SetColumnSpan(element, 2);
		grid.Children.Add(element);
		grid.Children.Add(new Border
		{
			Background = _accent,
			CornerRadius = new CornerRadius(2.5)
		});
		seekArea.Children.Add(grid);
		Grid.SetColumn(seekArea, 1);
		Grid.SetColumn(_total, 2);
		_progressRow.Children.Add(_elapsed);
		_progressRow.Children.Add(seekArea);
		_progressRow.Children.Add(_total);
		stackPanel.Children.Add(_progressRow);
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel4.Children.Add(Glyph("\ue892", 18.0, delegate
		{
			MediaCommand(MediaInfo.PreviousAsync);
		}));
		_playGlyph.Text = "\ue768";
		TextBlock textBlock = Glyph("\ue768", 24.0, delegate
		{
			MediaCommand(MediaInfo.PlayPauseAsync);
		});
		stackPanel4.Children.Add(textBlock);
		stackPanel4.Children.Add(Glyph("\ue893", 18.0, delegate
		{
			MediaCommand(MediaInfo.NextAsync);
		}));
		_playButton = textBlock;
		stackPanel.Children.Add(stackPanel4);
		return obj;
	}

	private Grid BuildInfo()
	{
		Grid obj = new Grid
		{
			Width = Math.Max(270.0, (double)_s.ExpandedWidth * 0.72),
			Height = 118.0
		};
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Text(_infoTime, 34.0, _fg, FontWeights.Light).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		Text(_infoDate, 13.0, _muted).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		Text(_infoStatus, 12.0, _accent).HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		_infoStatus.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		stackPanel.Children.Add(_infoTime);
		stackPanel.Children.Add(_infoDate);
		stackPanel.Children.Add(_infoStatus);
		obj.Children.Add(stackPanel);
		return obj;
	}

	private Grid BuildActivity()
	{
		Grid obj = new Grid
		{
			Width = ActivityWidth,
			Height = _s.Height,
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = GridLength.Auto
				},
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		_activityGlyph.FontSize = 15.0;
		_activityGlyph.VerticalAlignment = VerticalAlignment.Center;
		_activityGlyph.Margin = new Thickness(14.0, 0.0, 10.0, 0.0);
		Text(_activityLabel, 12.5, _fg);
		Text(_activityValue, 12.5, _fg, FontWeights.SemiBold);
		_activityValue.Margin = new Thickness(8.0, 0.0, 14.0, 0.0);
		_activityBar.Height = 5.0;
		_activityBar.VerticalAlignment = VerticalAlignment.Center;
		_activityBar.ColumnDefinitions.Add(_activityDone);
		_activityBar.ColumnDefinitions.Add(_activityLeft);
		Border element = new Border
		{
			Background = _surface,
			CornerRadius = new CornerRadius(2.5)
		};
		Grid.SetColumnSpan(element, 2);
		_activityBar.Children.Add(element);
		_activityBar.Children.Add(new Border
		{
			Background = _accent,
			CornerRadius = new CornerRadius(2.5)
		});
		Grid.SetColumn(_activityLabel, 1);
		Grid.SetColumn(_activityBar, 1);
		Grid.SetColumn(_activityValue, 2);
		obj.Children.Add(_activityGlyph);
		obj.Children.Add(_activityLabel);
		obj.Children.Add(_activityBar);
		obj.Children.Add(_activityValue);
		return obj;
	}

	private Mode Target()
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow < _activityUntil)
		{
			return Mode.Activity;
		}
		if (_hover || _pinned || Summoned || utcNow < _trackPopupUntil)
		{
			return Mode.Expanded;
		}
		MediaDetails media = _media;
		if ((object)media != null && media.Playing)
		{
			return Mode.Compact;
		}
		return Mode.Idle;
	}

	private void Resolve()
	{
		if (_closed)
		{
			return;
		}
		Mode mode = Target();
		FrameworkElement frameworkElement = mode switch
		{
			Mode.Activity => _activityView, 
			Mode.Expanded => ((object)_media != null) ? _mediaView : _infoView, 
			Mode.Compact => _compactView, 
			_ => _idleView, 
		};
		System.Windows.Size size = SizeOf(mode, frameworkElement);
		UpdateDots(mode);
		bool num = mode != _mode || frameworkElement != _currentView;
		_mode = mode;
		if (num || (Math.Abs(_pill.Width - size.Width) > 0.5 && !IsAnimating))
		{
			AnimateSize(size);
		}
		if (frameworkElement != _currentView)
		{
			if (_currentView != null)
			{
				Fade(_currentView, 0.0, 0.0, 0.1);
			}
			Fade(frameworkElement, 1.0, 0.09, 0.2);
			_currentView = frameworkElement;
		}
		bool flag = Summoned || (_s.FullscreenActivities && (mode == Mode.Activity || DateTime.UtcNow < _trackPopupUntil));
		bool dodge = _dodging && !_pinned && (mode == Mode.Idle || mode == Mode.Compact);
		_dodgeHidden = dodge;
		bool flag2 = (_fullscreenHidden && !flag) || dodge;
		int num2;
		if (mode != Mode.Idle || _s.Idle != IslandIdle.Hidden)
		{
			num2 = ((!flag2) ? 1 : 0);
			if (num2 != 0 && (_fullscreenHidden || Summoned))
			{
				BringToTop();
			}
		}
		else
		{
			num2 = 0;
		}
		double num3 = ((num2 != 0) ? 1.0 : 0.0);
		if (Math.Abs(_pill.Opacity - num3) > 0.01 && _pill.Tag as double? != num3)
		{
			_pill.Tag = num3;
			_pill.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(num3, Duration(0.25)));
		}
		_pill.IsHitTestVisible = !flag2;
		UpdateAudio();
	}

	private System.Windows.Size SizeOf(Mode mode, FrameworkElement view)
	{
		if (mode == Mode.Idle)
		{
			return _s.Idle switch
			{
				IslandIdle.Clock => new System.Windows.Size(Math.Max(76.0, MeasureWidth(_idleClock) + 30.0) + DotsWidth * 2.0, _s.Height), 
				IslandIdle.Dot => new System.Windows.Size(64.0 + DotsWidth * 2.0, 10.0), 
				_ => new System.Windows.Size(44.0 + DotsWidth * 2.0, (_privacy.Any && ShowPrivacy) ? 10 : 8), 
			};
		}
		return new System.Windows.Size(view.Width, view.Height);
	}

	private static double MeasureWidth(TextBlock t)
	{
		t.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
		return t.DesiredSize.Width;
	}

	private Duration Duration(double seconds)
	{
		return new Duration(TimeSpan.FromSeconds(seconds / Math.Clamp(_s.AnimationSpeed, 0.25, 3.0)));
	}

	private void AnimateSize(System.Windows.Size size)
	{
		BackEase easingFunction = new BackEase
		{
			Amplitude = 0.32,
			EasingMode = EasingMode.EaseOut
		};
		Duration duration = Duration(0.42);
		_animatingUntil = DateTime.UtcNow + duration.TimeSpan;
		_pill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(size.Width, duration)
		{
			EasingFunction = easingFunction
		});
		_pill.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(size.Height, duration)
		{
			EasingFunction = easingFunction
		});
	}

	private void Fade(FrameworkElement view, double to, double delay, double seconds)
	{
		view.IsHitTestVisible = to > 0.0;
		view.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, Duration(seconds))
		{
			BeginTime = TimeSpan.FromSeconds(delay / Math.Clamp(_s.AnimationSpeed, 0.25, 3.0))
		});
	}

	private void UpdateShape()
	{
		double num = Math.Max(0.0, _pill.ActualWidth);
		double num2 = Math.Max(0.0, _pill.ActualHeight);
		double num3 = Math.Min(num2 / 2.0, 26.0);
		Rect rect = new Rect(0.0, 0.0, Math.Max(0.0, num - 2.0), Math.Max(0.0, num2 - 2.0));
		if (Notch)
		{
			_pill.CornerRadius = new CornerRadius(0.0, 0.0, num3, num3);
			_stage.Clip = new CombinedGeometry(GeometryCombineMode.Union, new RectangleGeometry(rect, Math.Max(0.0, num3 - 1.0), Math.Max(0.0, num3 - 1.0)), new RectangleGeometry(new Rect(0.0, 0.0, rect.Width, rect.Height / 2.0)));
		}
		else
		{
			_pill.CornerRadius = new CornerRadius(num3);
			_stage.Clip = new RectangleGeometry(rect, Math.Max(0.0, num3 - 1.0), Math.Max(0.0, num3 - 1.0));
		}
	}

	public void Summon(bool toggle)
	{
		if (!_loaded)
		{
			_pendingSummon = true;
		}
		else if (toggle && Summoned)
		{
			_summonUntil = DateTime.MinValue;
			_summonActive = false;
			Resolve();
		}
		else
		{
			Summon(TimeSpan.FromSeconds(6.0));
		}
	}

	private void Summon(TimeSpan duration)
	{
		_summonUntil = DateTime.UtcNow + duration;
		_summonActive = true;
		_lastTopmost = DateTime.MinValue;
		RefreshMediaAsync();
		Resolve();
	}

	private void EdgeTick()
	{
		if (!Win32.GetCursorPos(out var pt))
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		System.Drawing.Rectangle bounds = _current.Bounds;
		double scale = Display.Scale;
		double num = (double)Math.Max(Math.Max(_s.ExpandedWidth, _s.Width), 320) * scale / 2.0;
		double num2 = (double)bounds.X + (double)bounds.Width / 2.0;
		bool flag = _s.HotEdge && pt.Y <= bounds.Y + 1 && pt.X >= bounds.X && pt.X < bounds.Right && Math.Abs((double)pt.X - num2) <= num;
		if (flag)
		{
			if (_edgeSince == DateTime.MaxValue)
			{
				_edgeSince = utcNow;
			}
			if (!Summoned && utcNow - _edgeSince >= TimeSpan.FromMilliseconds(90.0))
			{
				Summon(TimeSpan.FromSeconds(1.2));
			}
		}
		else
		{
			_edgeSince = DateTime.MaxValue;
		}
		if (Summoned)
		{
			if (flag || OverPill(pt, scale))
			{
				DateTime dateTime = utcNow + TimeSpan.FromSeconds(0.9);
				if (dateTime > _summonUntil)
				{
					_summonUntil = dateTime;
				}
			}
		}
		else if (_summonActive)
		{
			_summonActive = false;
			Resolve();
		}
	}

	private bool OverPill(Win32.POINT pt, double scale)
	{
		double actualWidth = _pill.ActualWidth;
		double actualHeight = _pill.ActualHeight;
		if (actualWidth <= 0.0 || actualHeight <= 0.0)
		{
			return false;
		}
		double num = (base.Left + (base.ActualWidth - actualWidth) / 2.0) * scale;
		double num2 = base.Top * scale;
		if ((double)pt.X >= num - 14.0 && (double)pt.X <= num + actualWidth * scale + 14.0 && (double)pt.Y >= num2 - 14.0)
		{
			return (double)pt.Y <= num2 + actualHeight * scale + 14.0;
		}
		return false;
	}

	private void BringToTop()
	{
		if (_hwnd != IntPtr.Zero && !(DateTime.UtcNow - _lastTopmost < TimeSpan.FromSeconds(1.0)))
		{
			_lastTopmost = DateTime.UtcNow;
			NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 83u);
		}
	}

	private void Hover(bool inside)
	{
		_hoverWanted = inside;
		_hoverTimer.Stop();
		_hoverTimer.Interval = TimeSpan.FromMilliseconds(inside ? 140 : 420);
		_hoverTimer.Start();
	}

	private void Tick()
	{
		_tick++;
		CheckVolume();
		CheckCaps();
		CheckLayout();
		if (_tick % 2 == 0)
		{
			CheckDodge();
		}
		if (_tick % 4 == 0)
		{
			RefreshMediaAsync();
			CheckFullscreen();
			UpdateClock();
		}
		if (_tick % 8 == 0)
		{
			CheckBattery();
			CheckPrivacy();
			CheckNetwork();
			Reposition();
		}
		UpdateProgress();
		Resolve();
	}

	private void Reposition()
	{
		Display display = (_current = Display.All().FirstOrDefault((Display d) => d.Name == _display.Name) ?? _display);
		Rect rect = (_s.OverBar ? display.BoundsDip : display.WorkAreaDip);
		double num = rect.X + (rect.Width - base.Width) / 2.0;
		double num2 = rect.Y + (double)((!Notch) ? Math.Clamp(_s.Offset, 0, 200) : 0);
		if (Math.Abs(base.Left - num) > 0.5)
		{
			base.Left = num;
		}
		if (Math.Abs(base.Top - num2) > 0.5)
		{
			base.Top = num2;
		}
	}

	public void Announce(string glyph, string label, string value)
	{
		if (!_closed)
		{
			ShowActivity(glyph, "Segoe UI Emoji", label, value, null, _accent);
			_activityUntil = DateTime.UtcNow + TimeSpan.FromSeconds(5.0);
			Resolve();
		}
	}

	private void UpdateClock()
	{
		DateTime now = DateTime.Now;
		string text;
		try
		{
			text = now.ToString(_s.ClockFormat, Loc.Culture);
		}
		catch (FormatException)
		{
			text = now.ToString("HH:mm", Loc.Culture);
		}
		FocusTimer focusSource = FocusSource;
		string text2 = ((focusSource != null && focusSource.Running) ? (((focusSource.Phase == FocusPhase.Break) ? "☕ " : "⏱ ") + FocusTimer.Format(focusSource.Remaining) + (focusSource.Paused ? " ⏸" : "")) : null);
		if (text2 != null)
		{
			text = text2;
		}
		if (_idleClock.Text != text)
		{
			_idleClock.Text = text;
		}
		_infoTime.Text = now.ToString("HH:mm", Loc.Culture);
		string text3 = now.ToString("dddd, d MMMM", Loc.Culture);
		TextBlock infoDate = _infoDate;
		string text4;
		if (text3.Length <= 0)
		{
			text4 = text3;
		}
		else
		{
			char reference = char.ToUpper(text3[0], Loc.Culture);
			ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(ref reference);
			string text5 = text3;
			text4 = string.Concat(readOnlySpan, text5.Substring(1, text5.Length - 1));
		}
		infoDate.Text = text4;
		List<string> list = new List<string>();
		if (text2 != null)
		{
			list.Add(text2);
		}
		if (ShowPrivacy && _privacy.Microphone.Count > 0)
		{
			list.Add("\ud83c\udfa4 " + string.Join(", ", _privacy.Microphone));
		}
		if (ShowPrivacy && _privacy.Camera.Count > 0)
		{
			list.Add("\ud83d\udcf7 " + string.Join(", ", _privacy.Camera));
		}
		(float, bool)? lastVolume = _lastVolume;
		if (lastVolume.HasValue)
		{
			(float, bool) valueOrDefault = lastVolume.GetValueOrDefault();
			list.Add(valueOrDefault.Item2 ? Loc.T("звук выкл") : Loc.F("звук {0}%", Math.Round(valueOrDefault.Item1 * 100f)));
		}
		PowerStatus powerStatus = SystemInformation.PowerStatus;
		if (!powerStatus.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) && powerStatus.BatteryLifePercent <= 1f)
		{
			list.Add(Loc.F("батарея {0}%", Math.Round(powerStatus.BatteryLifePercent * 100f)) + ((powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online) ? " ⚡" : ""));
		}
		_infoStatus.Text = string.Join("  •  ", list);
	}

	private void CheckDodge()
	{
		bool dodge = false;
		if (_s.DodgeWindows && _hwnd != IntPtr.Zero)
		{
			double scale = Display.Scale;
			double halfWidth = Math.Max(_s.Width, _pill.ActualWidth) / 2.0 + 8.0;
			double center = base.Left + base.ActualWidth / 2.0;
			double bottom = base.Top + Math.Max(_s.Height, _pill.ActualHeight) + 4.0;
			System.Drawing.Rectangle area = System.Drawing.Rectangle.FromLTRB((int)((center - halfWidth) * scale), (int)(base.Top * scale), (int)((center + halfWidth) * scale), (int)(bottom * scale));
			dodge = DesktopLayer.ForegroundIntersects(area);
		}
		if (dodge == _dodging)
		{
			_dodgeStreak = 0;
		}
		else if (dodge || ++_dodgeStreak >= 2)
		{
			// Прячемся сразу, а возвращаемся только после двух замеров — без мигания при переключении окон.
			_dodgeStreak = 0;
			_dodging = dodge;
		}
	}

	private void CheckFullscreen()
	{
		if (!_s.HideInFullscreen)
		{
			return;
		}
		System.Drawing.Point? point = (DesktopLayer.IsFullscreenAppActive() ? DesktopLayer.CoveredMonitor(maximized: false) : null);
		int num;
		if (point.HasValue)
		{
			System.Drawing.Point valueOrDefault = point.GetValueOrDefault();
			num = (_display.Contains(valueOrDefault.X, valueOrDefault.Y) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		if (flag != _fullscreenHidden)
		{
			if (++_fullscreenStreak >= 2)
			{
				_fullscreenStreak = 0;
				_fullscreenHidden = flag;
			}
		}
		else
		{
			_fullscreenStreak = 0;
		}
	}

	private void CheckVolume(bool force = false)
	{
		(float, bool)? lastVolume = _audio.Read();
		if (!lastVolume.HasValue)
		{
			return;
		}
		(float, bool)? lastVolume2 = _lastVolume;
		_lastVolume = lastVolume;
		if (!Has("volume"))
		{
			return;
		}
		if (!force)
		{
			if (!lastVolume2.HasValue)
			{
				return;
			}
			(float, bool) valueOrDefault = lastVolume2.GetValueOrDefault();
			if (Math.Abs(valueOrDefault.Item1 - lastVolume.Value.Item1) < 0.004f && valueOrDefault.Item2 == lastVolume.Value.Item2)
			{
				return;
			}
		}
		float num = (lastVolume.Value.Item2 ? 0f : lastVolume.Value.Item1);
		string glyph = ((lastVolume.Value.Item2 || (double)num <= 0.001) ? "\ue74f" : (((double)num < 0.34) ? "\ue993" : (((double)num < 0.67) ? "\ue994" : "\ue995")));
		ShowActivity(glyph, "Segoe MDL2 Assets", "", lastVolume.Value.Item2 ? Loc.T("выкл") : $"{Math.Round(num * 100f):0}%", num, _accent);
	}

	private void CheckCaps()
	{
		bool flag = System.Windows.Forms.Control.IsKeyLocked(Keys.Capital);
		bool? lastCaps = _lastCaps;
		_lastCaps = flag;
		if (lastCaps.HasValue && lastCaps != flag && Has("capslock"))
		{
			ShowActivity("⇪", "Segoe UI Symbol", "Caps Lock", flag ? Loc.T("вкл") : Loc.T("выкл"), null, flag ? _accent : _muted);
		}
	}

	private void CheckLayout()
	{
		nint foregroundWindow = NativeMethods.GetForegroundWindow();
		uint processId;
		nint keyboardLayout = NativeMethods.GetKeyboardLayout(NativeMethods.GetWindowThreadProcessId(foregroundWindow, out processId));
		if (keyboardLayout == IntPtr.Zero)
		{
			return;
		}
		int num = (int)(((IntPtr)keyboardLayout).ToInt64() & 0xFFFF);
		(nint, int)? lastLayout = _lastLayout;
		_lastLayout = (foregroundWindow, num);
		if (!lastLayout.HasValue)
		{
			return;
		}
		(nint, int) valueOrDefault = lastLayout.GetValueOrDefault();
		if (valueOrDefault.Item1 != foregroundWindow || valueOrDefault.Item2 == num || !Has("layout"))
		{
			return;
		}
		string value;
		string text;
		try
		{
			CultureInfo cultureInfo = CultureInfo.GetCultureInfo(num);
			value = cultureInfo.TwoLetterISOLanguageName.ToUpperInvariant();
			text = ((cultureInfo.Parent.NativeName.Length > 0) ? cultureInfo.Parent.NativeName : cultureInfo.NativeName);
			if (text.Length > 0)
			{
				char reference = char.ToUpper(text[0], cultureInfo);
				ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(ref reference);
				string text2 = text;
				text = string.Concat(readOnlySpan, text2.Substring(1, text2.Length - 1));
			}
		}
		catch (CultureNotFoundException)
		{
			value = num.ToString("X4");
			text = Loc.T("Раскладка");
		}
		ShowActivity("\ue765", "Segoe MDL2 Assets", text, value, null, _accent);
	}

	private static int BatteryBucket(int percent)
	{
		if (percent > 10)
		{
			if (percent > 20)
			{
				return 100;
			}
			return 20;
		}
		return 10;
	}

	private void CheckBattery()
	{
		PowerStatus powerStatus = SystemInformation.PowerStatus;
		if (powerStatus.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) || powerStatus.BatteryLifePercent > 1f)
		{
			return;
		}
		int num = (int)Math.Round(powerStatus.BatteryLifePercent * 100f);
		System.Windows.Forms.PowerLineStatus powerLineStatus = powerStatus.PowerLineStatus;
		System.Windows.Forms.PowerLineStatus? lastLine = _lastLine;
		_lastLine = powerLineStatus;
		int num2 = BatteryBucket(num);
		int lastBatteryBucket = _lastBatteryBucket;
		_lastBatteryBucket = num2;
		if (Has("battery"))
		{
			if (lastLine.HasValue && lastLine != powerLineStatus && powerLineStatus != System.Windows.Forms.PowerLineStatus.Unknown)
			{
				bool flag = powerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
				ShowActivity(flag ? "\ue945" : "\ue83f", "Segoe MDL2 Assets", flag ? Loc.T("Зарядка") : Loc.T("От батареи"), $"{num}%", (double)num / 100.0, flag ? _green : _accent);
			}
			else if (powerLineStatus == System.Windows.Forms.PowerLineStatus.Offline && num2 < lastBatteryBucket)
			{
				ShowActivity("\ue850", "Segoe MDL2 Assets", Loc.T("Низкий заряд"), $"{num}%", (double)num / 100.0, _alert);
			}
		}
	}

	private void UpdateDots(Mode mode)
	{
		bool flag = ShowPrivacy && _privacy.Any;
		_privacyDots.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		if (flag)
		{
			_micDot.Visibility = ((_privacy.Microphone.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
			_camDot.Visibility = ((_privacy.Camera.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
			bool flag2 = mode == Mode.Idle && _s.Idle != IslandIdle.Clock;
			double num = (flag2 ? 4.0 : 6.0);
			Ellipse micDot = _micDot;
			Ellipse micDot2 = _micDot;
			Ellipse camDot = _camDot;
			double num3 = (_camDot.Height = num);
			double num5 = (camDot.Width = num3);
			double width = (micDot2.Height = num5);
			micDot.Width = width;
			_privacyDots.VerticalAlignment = ((mode == Mode.Idle) ? VerticalAlignment.Center : VerticalAlignment.Top);
			_privacyDots.Margin = ((mode == Mode.Idle) ? new Thickness(0.0, 0.0, flag2 ? 6 : 10, 0.0) : new Thickness(0.0, Math.Max(3, _s.Height / 2 - 12), 12.0, 0.0));
		}
	}

	private static bool IsSelf(string app)
	{
		return app.Equals("Metamorph", StringComparison.OrdinalIgnoreCase);
	}

	private void CheckPrivacy()
	{
		if (!ShowPrivacy)
		{
			return;
		}
		PrivacySnapshot privacySnapshot = PrivacyUsage.Snapshot(IsSelf);
		PrivacySnapshot privacy = _privacy;
		bool flag = !_privacyRead;
		_privacyRead = true;
		if (!privacySnapshot.SameAs(privacy))
		{
			_privacy = privacySnapshot;
			var (readOnlyList, readOnlyList2) = privacySnapshot.StartedSince(privacy);
			if (!flag && readOnlyList2.Count > 0)
			{
				ShowActivity("\ue714", "Segoe MDL2 Assets", Loc.T("Камера"), string.Join(", ", readOnlyList2), null, BrushFactory.Solid("#30D158"));
			}
			else if (!flag && readOnlyList.Count > 0)
			{
				ShowActivity("\ue720", "Segoe MDL2 Assets", Loc.T("Микрофон"), string.Join(", ", readOnlyList), null, BrushFactory.Solid("#FF9F0A"));
			}
			else
			{
				Resolve();
			}
			UpdateClock();
		}
	}

	private static (bool Online, bool Wifi, string Name) ReadNetwork()
	{
		ConnectionProfile internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
		if ((object)internetConnectionProfile == null)
		{
			return (Online: false, Wifi: false, Name: "");
		}
		if (internetConnectionProfile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None)
		{
			return (Online: false, Wifi: false, Name: "");
		}
		bool isWlanConnectionProfile = internetConnectionProfile.IsWlanConnectionProfile;
		string text = ((!isWlanConnectionProfile) ? null : internetConnectionProfile.WlanConnectionProfileDetails?.GetConnectedSsid()) ?? internetConnectionProfile.ProfileName ?? "";
		if (text.Length > 24)
		{
			text = text.Substring(0, 23) + "…";
		}
		return (Online: true, Wifi: isWlanConnectionProfile, Name: text);
	}

	private void CheckNetwork()
	{
		if (!Has("network"))
		{
			return;
		}
		(bool, bool, string) value;
		try
		{
			value = ReadNetwork();
		}
		catch (Exception ex)
		{
			Log.Warn("Остров: сеть — " + ex.Message);
			return;
		}
		(bool, bool, string)? lastNet = _lastNet;
		_lastNet = value;
		if (!lastNet.HasValue)
		{
			return;
		}
		(bool, bool, string) valueOrDefault = lastNet.GetValueOrDefault();
		if (valueOrDefault.Item1 != value.Item1 || !(valueOrDefault.Item3 == value.Item3))
		{
			if (value.Item1)
			{
				ShowActivity(value.Item2 ? "\ue701" : "\ue839", "Segoe MDL2 Assets", Loc.T("Сеть подключена"), value.Item3, null, _green);
			}
			else
			{
				ShowActivity("\ue7ba", "Segoe MDL2 Assets", Loc.T("Нет сети"), "", null, _alert);
			}
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 797 && !_closed)
		{
			if (_clipTimer == null)
			{
				_clipTimer = new DispatcherTimer
				{
					Interval = TimeSpan.FromMilliseconds(250.0)
				};
				_clipTimer.Tick += delegate
				{
					_clipTimer.Stop();
					Safe(ShowClipboard);
				};
			}
			_clipTimer.Stop();
			_clipTimer.Start();
		}
		return IntPtr.Zero;
	}

	private void ShowClipboard()
	{
		System.Windows.IDataObject dataObject;
		try
		{
			dataObject = System.Windows.Clipboard.GetDataObject();
		}
		catch (COMException)
		{
			return;
		}
		if (dataObject == null || dataObject.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") || dataObject.GetDataPresent("Clipboard Viewer Ignore"))
		{
			return;
		}
		string value;
		if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop) && dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] array)
		{
			value = ((array.Length == 1) ? System.IO.Path.GetFileName(array[0]) : Loc.F("файлов: {0}", array.Length));
		}
		else if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
		{
			value = Loc.T("изображение");
		}
		else
		{
			if (!dataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText) || !(dataObject.GetData(System.Windows.DataFormats.UnicodeText) is string text))
			{
				return;
			}
			value = Loc.F("текст, {0} симв.", text.Length);
		}
		ShowActivity("\ue8c8", "Segoe MDL2 Assets", Loc.T("Скопировано"), value, null, _accent);
	}

	private void ShowActivity(string glyph, string glyphFont, string label, string value, double? level, System.Windows.Media.Brush? accent)
	{
		_activityGlyph.Text = glyph;
		_activityGlyph.FontFamily = new System.Windows.Media.FontFamily(glyphFont);
		_activityGlyph.Foreground = accent ?? _accent;
		_activityLabel.Text = label;
		_activityValue.Text = value;
		bool flag = level.HasValue && label.Length == 0;
		_activityBar.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_activityLabel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		if (level.HasValue)
		{
			double valueOrDefault = level.GetValueOrDefault();
			valueOrDefault = Math.Clamp(valueOrDefault, 0.0, 1.0);
			_activityDone.Width = new GridLength(valueOrDefault, GridUnitType.Star);
			_activityLeft.Width = new GridLength(1.0 - valueOrDefault, GridUnitType.Star);
		}
		_activityUntil = DateTime.UtcNow + TimeSpan.FromSeconds(1.8);
		Resolve();
	}

	private async Task RefreshMediaAsync()
	{
		if (_mediaBusy || _closed)
		{
			return;
		}
		_mediaBusy = true;
		try
		{
			MediaDetails i = await MediaInfo.GetDetailsAsync();
			if (_closed)
			{
				return;
			}
			MediaDetails previous = _media;
			_media = i;
			_mediaFetched = DateTime.UtcNow;
			bool changed = i?.Key != previous?.Key;
			if ((object)i == null)
			{
				_art = null;
			}
			else if (changed || (_art == null && _s.AlbumArt && _tick % 12 == 0))
			{
				if (changed)
				{
					_art = null;
				}
				if (_s.AlbumArt)
				{
					_art = Decode(await MediaInfo.GetThumbnailAsync()) ?? _art;
				}
				if (changed && _mediaInitialized && i.Playing && _s.TrackPopupSeconds > 0 && !_hover && !_pinned)
				{
					_trackPopupUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Clamp(_s.TrackPopupSeconds, 0, 30));
				}
			}
			if (!changed && (object)previous != null && (object)i != null && previous.Playing != i.Playing && _mediaInitialized && _s.TrackPopupSeconds > 0 && !_hover && !_pinned)
			{
				_trackPopupUntil = DateTime.UtcNow + TimeSpan.FromSeconds(1.6);
			}
			_mediaInitialized = true;
			UpdateMediaViews();
			Resolve();
		}
		catch (Exception ex)
		{
			Log.Error("Остров: музыка", ex);
		}
		finally
		{
			_mediaBusy = false;
		}
	}

	private static ImageSource? Decode(byte[]? bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.StreamSource = new MemoryStream(bytes);
			bitmapImage.DecodePixelWidth = 160;
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private void UpdateMediaViews()
	{
		MediaDetails media = _media;
		if ((object)media == null)
		{
			return;
		}
		_compactTitle.Text = ((media.Artist.Length > 0) ? (media.Title + "  ·  " + media.Artist) : media.Title);
		_title.Text = media.Title;
		_artist.Text = ((media.Album.Length > 0 && media.Artist.Length > 0) ? (media.Artist + " — " + media.Album) : ((media.Artist.Length > 0) ? media.Artist : media.Album));
		_source.Text = AppName(media.App);
		if (_playButton != null)
		{
			_playButton.Text = (media.Playing ? "\ue769" : "\ue768");
		}
		Border[] array = new Border[2] { _compactArt, _bigArt };
		foreach (Border border in array)
		{
			if (_art != null)
			{
				border.Background = new ImageBrush(_art)
				{
					Stretch = Stretch.UniformToFill
				};
				if (border.Child != null)
				{
					border.Child.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				border.Background = _surface;
				if (border.Child != null)
				{
					border.Child.Visibility = Visibility.Visible;
				}
			}
		}
		UpdateProgress();
	}

	internal static string AppName(string aumid)
	{
		if (string.IsNullOrWhiteSpace(aumid))
		{
			return "";
		}
		string text = aumid;
		int num = text.IndexOf('!');
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		int num2 = text.IndexOf('_');
		if (num2 > 0)
		{
			text = text.Substring(0, num2);
		}
		string text2;
		if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			text2 = text;
			text = text2.Substring(0, text2.Length - 4);
		}
		int num3 = text.LastIndexOf('.');
		if (num3 >= 0 && num3 < text.Length - 1)
		{
			text2 = text;
			int num4 = num3 + 1;
			text = text2.Substring(num4, text2.Length - num4);
		}
		if (text.Length <= 0)
		{
			return text;
		}
		char reference = char.ToUpperInvariant(text[0]);
		ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(ref reference);
		text2 = text;
		return string.Concat(readOnlySpan, text2.Substring(1, text2.Length - 1));
	}

	private void UpdateProgress()
	{
		MediaDetails media = _media;
		if ((object)media == null || media.Duration <= TimeSpan.Zero)
		{
			_progressRow.Visibility = Visibility.Hidden;
			return;
		}
		_progressRow.Visibility = Visibility.Visible;
		TimeSpan timeSpan = media.Position + (media.Playing ? (DateTime.UtcNow - _mediaFetched) : TimeSpan.Zero);
		if (timeSpan > media.Duration)
		{
			timeSpan = media.Duration;
		}
		double num = Math.Clamp(timeSpan.TotalSeconds / media.Duration.TotalSeconds, 0.0, 1.0);
		_progressDone.Width = new GridLength(num, GridUnitType.Star);
		_progressLeft.Width = new GridLength(1.0 - num, GridUnitType.Star);
		_elapsed.Text = Format(timeSpan);
		_total.Text = "-" + Format(media.Duration - timeSpan);
	}

	private static string Format(TimeSpan t)
	{
		if (!(t.TotalHours >= 1.0))
		{
			return t.ToString("m\\:ss", CultureInfo.InvariantCulture);
		}
		return t.ToString("h\\:mm\\:ss", CultureInfo.InvariantCulture);
	}

	private void MediaCommand(Func<Task> command)
	{
		Func<Task> command2 = command;
		Task.Run(async delegate
		{
			try
			{
				await command2();
			}
			catch (Exception ex)
			{
				Log.Error("Остров: команда плееру", ex);
			}
		});
		DispatcherTimer refresh = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(350.0)
		};
		refresh.Tick += delegate
		{
			refresh.Stop();
			RefreshMediaAsync();
		};
		refresh.Start();
	}

	private void UpdateAudio()
	{
		bool flag = _s.Visualizer && (_media?.Playing ?? false);
		if (flag)
		{
			Mode mode = _mode;
			bool flag2 = (uint)(mode - 2) <= 1u;
			flag = flag2;
		}
		bool flag3 = flag && !_fullscreenHidden && !_dodgeHidden;
		if (flag3 && _audioLease == null)
		{
			_audioLease = AudioSpectrum.Shared.Acquire();
		}
		if (!flag3 && _audioLease != null)
		{
			_audioLease.Dispose();
			_audioLease = null;
		}
		if (flag3 && !_rendering)
		{
			_rendering = true;
			CompositionTarget.Rendering += OnRendering;
		}
		else if (!flag3)
		{
			StopRendering();
		}
	}

	private void StopRendering()
	{
		if (!_rendering)
		{
			return;
		}
		_rendering = false;
		CompositionTarget.Rendering -= OnRendering;
		foreach (ScaleTransform item in _miniBars.Concat(_bigBars))
		{
			if (item != null)
			{
				item.ScaleY = 0.15;
			}
		}
	}

	private void OnRendering(object? sender, EventArgs e)
	{
		try
		{
			double now = _clock.Elapsed.TotalSeconds;
			double num = now - _lastRender;
			if (!(num < 1.0 / 30.0))
			{
				_lastRender = now;
				num = Math.Min(num, 0.1);
				float[] target = ((!AudioSpectrum.Shared.Read(_raw)) ? (from i in Enumerable.Range(0, 7)
					select (float)(0.25 + 0.18 * Math.Sin(now * 4.0 + (double)i * 1.3))).ToArray() : SpectrumAnalyzer.Resample(_raw.Take(_raw.Length * 2 / 3).ToArray(), 7));
				float[] array = _smooth.Update(target, num);
				for (int j = 0; j < 7; j++)
				{
					_bigBars[j].ScaleY = 0.15 + 0.85 * (double)array[j];
				}
				for (int k = 0; k < 5; k++)
				{
					_miniBars[k].ScaleY = 0.15 + 0.85 * (double)array[Math.Min(6, k + 1)];
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("Остров: визуализатор", ex);
			StopRendering();
		}
	}

	private void OpenMenu()
	{
		ContextMenu menu = new ContextMenu
		{
			Placement = PlacementMode.MousePoint,
			PlacementTarget = _pill
		};
		Add(_pinned ? "Свернуть" : "Развернуть", delegate
		{
			_pinned = !_pinned;
			Resolve();
		});
		FocusTimer focus = FocusSource;
		if (focus != null && focus.Running)
		{
			menu.Items.Add(new Separator());
			Add(focus.Paused ? "Продолжить таймер" : "Пауза таймера", delegate
			{
				if (focus.Paused)
				{
					focus.Resume();
				}
				else
				{
					focus.Pause();
				}
				UpdateClock();
				Resolve();
			});
			Add("Отменить таймер", delegate
			{
				focus.Cancel();
				UpdateClock();
				Resolve();
			});
			menu.Items.Add(new Separator());
		}
		Add("Открыть Metamorph", _openMainWindow);
		Add("Скрыть остров", base.Close);
		menu.IsOpen = true;
		void Add(string header, Action action)
		{
			Action action2 = action;
			MenuItem menuItem = new MenuItem
			{
				Header = Loc.T(header)
			};
			menuItem.Click += delegate
			{
				Safe(action2);
			};
			menu.Items.Add(menuItem);
		}
	}
}
