using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Desktop;

// Горячие углы экрана. API используется SessionController — сигнатуры не менять.
// Опрос GetCursorPos раз в 80 мс (дёшево); логика углов и взвода — HotCornerTracker в Core.
internal sealed class HotCorners : IDisposable
{
	private readonly HotCornersSettings _settings;

	private readonly Action<string> _run;

	private readonly Color _accent;

	private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Background)
	{
		Interval = TimeSpan.FromMilliseconds(80.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private HotCornerTracker? _tracker;

	private Dispatcher? _dispatcher;

	private bool _started;

	// run — выполнить действие из HotCornersSettings.Actions (обрабатывает App.RunDesktopAction).
	// accent — цвет «волны» при срабатывании (палитра темы), по умолчанию цвет окон Windows.
	public HotCorners(HotCornersSettings settings, Action<string> run, string? accent = null)
	{
		_settings = settings;
		_run = run;
		_accent = (string.IsNullOrWhiteSpace(accent) ? SystemParameters.WindowGlassColor : BrushFactory.ToColor(accent));
		_timer.Tick += delegate
		{
			Poll();
		};
	}

	public void Start()
	{
		if (_started || !_settings.Enabled)
		{
			return;
		}
		List<HotCorner> active = new List<HotCorner>();
		foreach (HotCorner corner in Enum.GetValues<HotCorner>())
		{
			if (IsAction(ActionOf(corner)))
			{
				active.Add(corner);
			}
		}
		if (active.Count == 0)
		{
			return;
		}
		_started = true;
		_dispatcher = Dispatcher.CurrentDispatcher;
		_tracker = new HotCornerTracker(_settings.DelayMs, active);
		UpdateMonitors();
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
		_timer.Start();
		EventTrace.Mark("hotcorners.start", string.Join(",", _tracker.Points.Select((HotCornerPoint p) => $"{p.Corner}@{p.X},{p.Y}")));
	}

	private static bool IsAction(string? action)
	{
		if (!string.IsNullOrEmpty(action) && action != "none")
		{
			return HotCornersSettings.Actions.Contains(action);
		}
		return false;
	}

	private string ActionOf(HotCorner corner)
	{
		return corner switch
		{
			HotCorner.TopLeft => _settings.TopLeft,
			HotCorner.TopRight => _settings.TopRight,
			HotCorner.BottomLeft => _settings.BottomLeft,
			_ => _settings.BottomRight,
		};
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		_dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(UpdateMonitors));
	}

	private void UpdateMonitors()
	{
		try
		{
			_tracker?.SetMonitors(Display.All().Select((Display d) => d.Bounds));
		}
		catch (Exception ex)
		{
			Log.Error("Горячие углы: мониторы", ex);
		}
	}

	private void Poll()
	{
		HotCornerTracker? tracker = _tracker;
		if (tracker == null || DesktopActivity.Locked || !Win32.GetCursorPos(out Win32.POINT pt))
		{
			return;
		}
		HotCornerPoint? hit = tracker.Update(pt.X, pt.Y, _clock.ElapsedMilliseconds, Blocked);
		if (hit.HasValue)
		{
			Fire(hit.Value);
		}
	}

	// Тянут окно / выделяют рамкой, или идёт игра/видео во весь экран — не мешаем.
	private static bool Blocked()
	{
		if (!Down(1) && !Down(2) && !Down(4))
		{
			return DesktopLayer.IsFullscreenAppActive();
		}
		return true;
	}

	private static bool Down(int vk)
	{
		return (GetAsyncKeyState(vk) & 0x8000) != 0;
	}

	private void Fire(HotCornerPoint hit)
	{
		string action = ActionOf(hit.Corner);
		EventTrace.Mark("hotcorner", $"{hit.Corner} {action}");
		try
		{
			RippleWindow.Play(hit.X, hit.Y, _accent);
		}
		catch (Exception ex)
		{
			Log.Error("Горячие углы: анимация", ex);
		}
		try
		{
			_run(action);
		}
		catch (Exception ex2)
		{
			Log.Error("Горячие углы: действие " + action, ex2);
		}
	}

	public void Dispose()
	{
		_timer.Stop();
		_tracker = null;
		if (_started)
		{
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
			_started = false;
		}
	}

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);

	// Маленькая «волна» в углу: прозрачное для мыши окно поверх всех, круг расширяется и гаснет (~350 мс).
	private sealed class RippleWindow : Window
	{
		private const double Size = 96.0;

		public static void Play(int x, int y, Color color)
		{
			double scale = Display.Scale;
			ScaleTransform grow = new ScaleTransform(0.15, 0.15);
			Ellipse circle = new Ellipse
			{
				Width = Size,
				Height = Size,
				Fill = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)),
				Stroke = new SolidColorBrush(color),
				StrokeThickness = 2.0,
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = grow
			};
			RippleWindow window = new RippleWindow
			{
				Left = (double)x / scale - Size / 2.0,
				Top = (double)y / scale - Size / 2.0,
				Width = Size,
				Height = Size,
				Content = circle
			};
			window.Show();
			Duration duration = new Duration(TimeSpan.FromMilliseconds(350.0));
			CubicEase ease = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			};
			DoubleAnimation fade = new DoubleAnimation(0.9, 0.0, duration);
			fade.Completed += delegate
			{
				window.Close();
			};
			grow.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.15, 1.0, duration)
			{
				EasingFunction = ease
			});
			grow.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.15, 1.0, duration)
			{
				EasingFunction = ease
			});
			circle.BeginAnimation(UIElement.OpacityProperty, fade);
		}

		private RippleWindow()
		{
			base.WindowStyle = WindowStyle.None;
			base.AllowsTransparency = true;
			base.Background = Brushes.Transparent;
			base.ShowInTaskbar = false;
			base.ShowActivated = false;
			base.Topmost = true;
			base.ResizeMode = ResizeMode.NoResize;
			base.IsHitTestVisible = false;
			base.Focusable = false;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			nint hwnd = new WindowInteropHelper(this).Handle;
			WindowEffects.MakeToolWindow(hwnd, noActivate: true);
			long style = NativeMethods.GetWindowLong(hwnd, -20);
			NativeMethods.SetWindowLong(hwnd, -20, style | 0x20 | 0x80000);
		}
	}
}
