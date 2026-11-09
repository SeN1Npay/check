using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Desktop;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public class BarWindow : Window, IComponentConnector
{
	private enum Side
	{
		Left,
		Center,
		Right
	}

	private readonly ThemeProfile _profile;

	private readonly List<BarModule> _modules = new List<BarModule>();

	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(500.0)
	};

	private nint _hwnd;

	private HwndSource? _source;

	private long _tick;

	private readonly Display _display;

	private readonly BarStyle _style;

	private AppBarHost? _appBar;

	private const double FloatingGap = 6.0;

	private readonly double _floatGap;

	private readonly System.Windows.Media.Color _hoverColor;

	private readonly TranslateTransform _slide = new TranslateTransform();

	private readonly DispatcherTimer _edgeTimer = new DispatcherTimer(DispatcherPriority.Input)
	{
		Interval = TimeSpan.FromMilliseconds(70.0)
	};

	// Опрос полноэкранных окон: уведомлению Проводника одному верить нельзя.
	private int _coveredStreak;

	private bool _suspended;

	private bool _revealed = true;

	private DateTime _edgeSince = DateTime.MaxValue;

	private DateTime _leftAt = DateTime.MaxValue;

	internal Border Root;

	internal StackPanel LeftPanel;

	internal StackPanel CenterPanel;

	internal StackPanel RightPanel;

	private bool _contentLoaded;

	public event EventHandler? ExplorerRestarted;

	public BarWindow(ThemeProfile profile, Display display, Action openMainWindow, MetamorphServices? services = null)
	{
		InitializeComponent();
		_profile = profile;
		_display = display;
		BarSettings bar = profile.Bar;
		Palette palette = profile.Palette;
		base.FontFamily = new System.Windows.Media.FontFamily(bar.FontFamily + ", Consolas");
		base.FontSize = bar.FontSize;
		base.Foreground = BrushFactory.Solid(palette.Foreground);
		Rect boundsDip = display.BoundsDip;
		bool top = bar.Position == BarPosition.Top;
		_floatGap = (bar.Style == BarStyle.Floating) ? FloatingGap : 0.0;
		base.Width = boundsDip.Width;
		base.Height = bar.Height + _floatGap;
		base.Left = boundsDip.Left;
		base.Top = (top ? boundsDip.Top : (boundsDip.Bottom - base.Height));
		BarContext context = BarContext.For(profile, services, openMainWindow, display.Primary);
		_style = bar.Style;
		switch (bar.Style)
		{
		case BarStyle.Islands:
			Root.Background = System.Windows.Media.Brushes.Transparent;
			Root.Padding = new Thickness(6.0, 3.0, 6.0, 3.0);
			break;
		case BarStyle.Powerline:
			Root.Background = BrushFactory.Solid(palette.Background, bar.Opacity);
			Root.Padding = new Thickness(0.0);
			break;
		case BarStyle.Floating:
			Root.Background = BrushFactory.Solid(palette.Background, bar.Opacity);
			Root.BorderBrush = BrushFactory.Solid(palette.Accent, 0.35);
			Root.BorderThickness = new Thickness(1.0);
			Root.CornerRadius = new CornerRadius(Math.Min(12.0, bar.Height / 2.0));
			Root.Margin = top ? new Thickness(_floatGap, _floatGap, _floatGap, 0.0) : new Thickness(_floatGap, 0.0, _floatGap, _floatGap);
			Root.Padding = new Thickness(6.0, 0.0, 6.0, 0.0);
			break;
		default:
			Root.Background = BrushFactory.Solid(palette.Background, bar.Opacity);
			Root.BorderBrush = BrushFactory.Solid(palette.Muted, 0.5);
			Root.BorderThickness = (top ? new Thickness(0.0, 0.0, 0.0, 1.0) : new Thickness(0.0, 1.0, 0.0, 0.0));
			break;
		}
		_hoverColor = BrushFactory.ToColor(palette.Surface, 0.75);
		Root.RenderTransform = _slide;
		if (bar.AutoHide || bar.Animations)
		{
			// Стартуем невидимыми, чтобы не мигнуть кадром до анимации появления.
			Root.Opacity = 0.0;
			_slide.Y = (top ? (0.0 - base.Height) : base.Height) * 0.6;
		}
		AddModules(LeftPanel, bar.Left, context, Side.Left);
		AddModules(CenterPanel, bar.Center, context, Side.Center);
		AddModules(RightPanel, bar.Right, context, Side.Right);
		_timer.Tick += delegate
		{
			CheckFullscreen();
			if (!_suspended)
			{
				UpdateModules(force: false);
			}
		};
		_edgeTimer.Tick += delegate
		{
			AutoHideTick();
		};
		base.Loaded += delegate
		{
			UpdateModules(force: true);
			_timer.Start();
			if (bar.AutoHide)
			{
				SetRevealed(reveal: false, animate: false);
				_edgeTimer.Start();
			}
			else if (bar.Animations)
			{
				AnimateReveal(reveal: true);
			}
		};
	}

	private void AddModules(Panel panel, IEnumerable<string> names, BarContext context, Side side)
	{
		List<FrameworkElement> list = new List<FrameworkElement>();
		foreach (string name in names)
		{
			BarModule barModule;
			try
			{
				barModule = BarModuleFactory.CreateFor(name, context);
			}
			catch (Exception ex)
			{
				Log.Error("Модуль панели " + name, ex);
				barModule = null;
			}
			if (barModule != null)
			{
				_modules.Add(barModule);
				list.Add((_style == BarStyle.Powerline || !_profile.Bar.Animations) ? barModule.View : WithHover(barModule.View));
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		switch (_style)
		{
		case BarStyle.Islands:
		{
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			foreach (FrameworkElement item in list)
			{
				stackPanel.Children.Add(item);
			}
			panel.Children.Add(new Border
			{
				Child = stackPanel,
				Background = BrushFactory.Solid(_profile.Palette.Background, _profile.Bar.Opacity),
				BorderBrush = BrushFactory.Solid(_profile.Palette.Surface, 0.9),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(Math.Max(4.0, (double)(_profile.Bar.Height - 6) / 2.0)),
				Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
				Margin = new Thickness(3.0, 0.0, 3.0, 0.0)
			});
			return;
		}
		case BarStyle.Powerline:
			AddPowerline(panel, list, side);
			return;
		}
		foreach (FrameworkElement item2 in list)
		{
			panel.Children.Add(item2);
		}
	}

	private void AddPowerline(Panel panel, List<FrameworkElement> views, Side side)
	{
		Palette palette = _profile.Palette;
		System.Windows.Media.Color color = BrushFactory.ToColor(palette.Surface);
		System.Windows.Media.Color color2 = BrushFactory.ToColor(palette.Background);
		System.Windows.Media.Color color3 = System.Windows.Media.Color.FromRgb((byte)((color.R + color2.R) / 2), (byte)((color.G + color2.G) / 2), (byte)((color.B + color2.B) / 2));
		System.Windows.Media.Color[] array = new System.Windows.Media.Color[2] { color, color3 };
		int h = _profile.Bar.Height;
		double arrow = (double)h / 2.0;
		for (int i = 0; i < views.Count; i++)
		{
			System.Windows.Media.Color color4 = array[i % 2];
			Border element = new Border
			{
				Background = new SolidColorBrush(color4),
				Child = views[i],
				Height = h,
				Padding = new Thickness(4.0, 0.0, 4.0, 0.0)
			};
			if (side == Side.Right)
			{
				panel.Children.Add(Arrow(color4, pointRight: false));
			}
			if (side == Side.Center && i == 0)
			{
				panel.Children.Add(Arrow(color4, pointRight: false));
			}
			panel.Children.Add(element);
			if (side == Side.Left)
			{
				panel.Children.Add(Arrow(color4, pointRight: true));
			}
			if (side == Side.Center && i == views.Count - 1)
			{
				panel.Children.Add(Arrow(color4, pointRight: true));
			}
		}
		Path Arrow(System.Windows.Media.Color fill, bool pointRight)
		{
			Geometry data = (pointRight ? Geometry.Parse($"M0,0 L{arrow},{(double)h / 2.0} L0,{h} Z") : Geometry.Parse($"M{arrow},0 L0,{(double)h / 2.0} L{arrow},{h} Z"));
			return new Path
			{
				Data = data,
				Fill = new SolidColorBrush(fill),
				Width = arrow,
				Height = h,
				Stretch = Stretch.None
			};
		}
	}

	private FrameworkElement WithHover(FrameworkElement view)
	{
		System.Windows.Media.Color on = _hoverColor;
		System.Windows.Media.Color off = System.Windows.Media.Color.FromArgb(0, on.R, on.G, on.B);
		SolidColorBrush background = new SolidColorBrush(off);
		Border border = new Border
		{
			Child = view,
			Background = background,
			CornerRadius = new CornerRadius(6.0),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(1.0, 2.0, 1.0, 2.0)
		};
		Duration duration = new Duration(TimeSpan.FromMilliseconds(140.0));
		border.MouseEnter += delegate
		{
			background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(on, duration));
		};
		border.MouseLeave += delegate
		{
			background.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(off, duration));
		};
		return border;
	}

	private void UpdateModules(bool force)
	{
		_tick++;
		foreach (BarModule module in _modules)
		{
			if (force || _tick % module.IntervalTicks == 0L)
			{
				try
				{
					module.Update();
				}
				catch (Exception ex)
				{
					Log.Error("Модуль панели " + module.GetType().Name, ex);
				}
			}
		}
	}

	private void CheckFullscreen()
	{
		if (_appBar == null)
		{
			return;
		}
		System.Drawing.Point? point = DesktopLayer.CoveredMonitor(maximized: false);
		bool covered = point.HasValue && _display.Contains(point.Value.X, point.Value.Y);
		if (covered == _appBar.FullscreenApp)
		{
			_coveredStreak = 0;
			return;
		}
		// Два замера подряд — чтобы мгновенные переключения окон не дёргали панель.
		if (++_coveredStreak >= 2)
		{
			_coveredStreak = 0;
			EventTrace.Mark("bar.fullscreen.poll", $"{_display.Name} {covered}");
			_appBar.SetFullscreen(covered);
		}
	}

	private void UpdateSuspension()
	{
		bool suspend = (_appBar?.FullscreenApp ?? false) || (_profile.Bar.AutoHide && !_revealed);
		if (suspend == _suspended)
		{
			return;
		}
		_suspended = suspend;
		foreach (BarModule module in _modules)
		{
			try
			{
				module.Suspend(suspend);
			}
			catch (Exception ex)
			{
				Log.Error("Модуль панели " + module.GetType().Name, ex);
			}
		}
		if (!suspend)
		{
			UpdateModules(force: true);
		}
	}

	private void AutoHideTick()
	{
		if (!Win32.GetCursorPos(out var pt))
		{
			return;
		}
		if (_appBar != null && _appBar.FullscreenApp)
		{
			if (_revealed)
			{
				SetRevealed(reveal: false, animate: false);
			}
			return;
		}
		System.Drawing.Rectangle bounds = _display.Bounds;
		bool top = _profile.Bar.Position == BarPosition.Top;
		bool atEdge = pt.X >= bounds.Left && pt.X < bounds.Right && (top ? (pt.Y <= bounds.Top + 1) : (pt.Y >= bounds.Bottom - 2));
		DateTime now = DateTime.UtcNow;
		if (!_revealed)
		{
			if (!atEdge)
			{
				_edgeSince = DateTime.MaxValue;
				return;
			}
			if (_edgeSince == DateTime.MaxValue)
			{
				_edgeSince = now;
			}
			// Небольшая задержка: курсор, пролетающий к кнопкам окна у кромки, панель не вызывает.
			if (now - _edgeSince >= TimeSpan.FromMilliseconds(220.0))
			{
				SetRevealed(reveal: true, animate: true);
			}
			return;
		}
		bool inside = Win32.GetWindowRect(_hwnd, out var rect) && pt.X >= rect.Left && pt.X < rect.Right && pt.Y >= rect.Top - 8 && pt.Y < rect.Bottom + 8;
		if (inside || atEdge)
		{
			_leftAt = DateTime.MaxValue;
		}
		else if (_leftAt == DateTime.MaxValue)
		{
			_leftAt = now;
		}
		else if (now - _leftAt >= TimeSpan.FromMilliseconds(800.0))
		{
			SetRevealed(reveal: false, animate: true);
		}
	}

	private void SetRevealed(bool reveal, bool animate)
	{
		_revealed = reveal;
		_edgeSince = DateTime.MaxValue;
		_leftAt = DateTime.MaxValue;
		if (reveal && _hwnd != IntPtr.Zero)
		{
			NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 19u);
		}
		Root.IsHitTestVisible = reveal;
		AnimateReveal(reveal, animate);
		UpdateSuspension();
	}

	private void AnimateReveal(bool reveal, bool animate = true)
	{
		double height = (base.ActualHeight > 0.0) ? base.ActualHeight : base.Height;
		double hiddenY = (_profile.Bar.Position == BarPosition.Top) ? (0.0 - height) : height;
		double toY = reveal ? 0.0 : hiddenY;
		double toOpacity = reveal ? 1.0 : 0.0;
		if (!animate)
		{
			_slide.BeginAnimation(TranslateTransform.YProperty, null);
			Root.BeginAnimation(UIElement.OpacityProperty, null);
			_slide.Y = toY;
			Root.Opacity = toOpacity;
			return;
		}
		Duration duration = new Duration(TimeSpan.FromMilliseconds(reveal ? 260.0 : 180.0));
		IEasingFunction ease = new CubicEase
		{
			EasingMode = (reveal ? EasingMode.EaseOut : EasingMode.EaseIn)
		};
		_slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(toY, duration)
		{
			EasingFunction = ease
		});
		Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(toOpacity, duration));
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		_source = HwndSource.FromHwnd(_hwnd);
		_source?.AddHook(WndProc);
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		bool autoHide = _profile.Bar.AutoHide;
		// Blur рисуется на весь прямоугольник окна, даже если панель спрятана, — с автоскрытием его не включаем.
		if (_profile.Bar.Blur && _profile.Bar.Style == BarStyle.Solid && !autoHide)
		{
			WindowEffects.EnableBlurBehind(_hwnd);
		}
		_appBar = new AppBarHost(_hwnd, "bar:" + _display.Name, delegate
		{
			PositionAppBar();
			return true;
		});
		_appBar.Enabled = !autoHide;
		_appBar.FullscreenChanged += delegate(bool fullscreen)
		{
			EventTrace.Mark("bar.zorder", fullscreen ? "bottom" : "topmost");
			NativeMethods.SetWindowPos(_hwnd, fullscreen ? NativeMethods.HWND_BOTTOM : NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, 19u);
			if (autoHide && fullscreen && _revealed)
			{
				SetRevealed(reveal: false, animate: false);
			}
			UpdateSuspension();
		};
		if (!autoHide)
		{
			_appBar.Register();
		}
		PositionAppBar();
	}

	private void PositionAppBar()
	{
		if (_hwnd != IntPtr.Zero && _appBar != null)
		{
			bool flag = _profile.Bar.Position == BarPosition.Top;
			double dpiScaleY = VisualTreeHelper.GetDpi(this).DpiScaleY;
			int num = (int)Math.Round(((double)_profile.Bar.Height + _floatGap) * dpiScaleY);
			System.Drawing.Rectangle bounds = _display.Bounds;
			NativeMethods.RECT rECT = default(NativeMethods.RECT);
			rECT.Left = bounds.Left;
			rECT.Right = bounds.Right;
			rECT.Top = (flag ? bounds.Top : (bounds.Bottom - num));
			rECT.Bottom = (flag ? (bounds.Top + num) : bounds.Bottom);
			NativeMethods.RECT desired = rECT;
			NativeMethods.RECT rc = _appBar.Reserve(flag ? 1u : 3u, desired);
			AppBarHost.MoveIfNeeded(_hwnd, rc, !_appBar.FullscreenApp);
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		try
		{
			if (_display.Primary)
			{
				BroadcastTrace.Inspect(msg, wParam, lParam);
			}
			if (_appBar != null && _appBar.HandleMessage(msg, wParam, lParam, _display, delegate
			{
				this.ExplorerRestarted?.Invoke(this, EventArgs.Empty);
			}))
			{
				handled = true;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Панель: сообщение окна", ex);
		}
		return IntPtr.Zero;
	}

	protected override void OnClosed(EventArgs e)
	{
		_timer.Stop();
		_edgeTimer.Stop();
		foreach (BarModule module in _modules)
		{
			try
			{
				module.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("Закрытие модуля панели", ex);
			}
		}
		_appBar?.Remove();
		_source?.RemoveHook(WndProc);
		base.OnClosed(e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Metamorph;component/bar/barwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			Root = (Border)target;
			break;
		case 2:
			LeftPanel = (StackPanel)target;
			break;
		case 3:
			CenterPanel = (StackPanel)target;
			break;
		case 4:
			RightPanel = (StackPanel)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
