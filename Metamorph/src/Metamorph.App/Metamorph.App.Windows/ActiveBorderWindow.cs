using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Metamorph.Core.Windows;

namespace Metamorph.App.Windows;

internal sealed class ActiveBorderWindow : Window
{
	private readonly Border _border = new Border();

	private readonly double _width;

	private readonly uint _ownPid = (uint)Environment.ProcessId;

	private readonly DispatcherTimer _poll = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(400.0)
	};

	private Win32.WinEventProc? _proc;

	private nint _hookForeground;

	private nint _hookLocation;

	private nint _hookMinimize;

	private nint _target;

	private nint _hwnd;

	public ActiveBorderWindow(ThemeProfile profile)
	{
		ActiveBorderSettings activeBorder = profile.Windows.ActiveBorder;
		_width = Math.Clamp(activeBorder.Width, 1, 16);
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.Width = (base.Height = 1.0);
		base.Left = (base.Top = -10000.0);
		Color color = BrushFactory.ToColor(activeBorder.Color ?? profile.Palette.Accent);
		Color endColor = BrushFactory.ToColor(profile.Palette.Accent2);
		_border.BorderBrush = ((activeBorder.Gradient && activeBorder.Color == null) ? ((Brush)new LinearGradientBrush(color, endColor, 45.0)) : ((Brush)new SolidColorBrush(color)));
		_border.BorderThickness = new Thickness(_width);
		_border.CornerRadius = new CornerRadius(Math.Max(0, activeBorder.Radius));
		_border.IsHitTestVisible = false;
		base.Content = _border;
		_poll.Tick += delegate
		{
			Update();
		};
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		long windowLong = NativeMethods.GetWindowLong(_hwnd, -20);
		NativeMethods.SetWindowLong(_hwnd, -20, windowLong | 0x20 | 0x80000 | 0x80 | 0x8000000);
		_proc = OnWinEvent;
		_hookForeground = Win32.SetWinEventHook(3u, 3u, IntPtr.Zero, _proc, 0u, 0u, 0u);
		_hookMinimize = Win32.SetWinEventHook(22u, 23u, IntPtr.Zero, _proc, 0u, 0u, 0u);
		_hookLocation = Win32.SetWinEventHook(32779u, 32779u, IntPtr.Zero, _proc, 0u, 0u, 2u);
		_poll.Start();
		Update();
	}

	private void OnWinEvent(nint hook, uint type, nint hwnd, int idObject, int idChild, uint thread, uint time)
	{
		try
		{
			if (type != 32779 || (hwnd == _target && idObject == 0))
			{
				Update();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Рамка окна: " + ex.Message);
		}
	}

	private void Update()
	{
		nint foregroundWindow = Win32.GetForegroundWindow();
		Win32.GetWindowThreadProcessId(foregroundWindow, out var pid);
		if (pid == _ownPid)
		{
			return;
		}
		_target = foregroundWindow;
		if (!WindowMover.IsTileable(foregroundWindow) || Win32.IsZoomed(foregroundWindow) || IsFullscreen(foregroundWindow))
		{
			Hide();
			return;
		}
		IntRect intRect = WindowMover.VisibleBounds(foregroundWindow);
		if (intRect.Width <= 0 || intRect.Height <= 0)
		{
			Hide();
			return;
		}
		double scale = Display.Scale;
		double width = _width;
		base.Left = (double)intRect.Left / scale - width;
		base.Top = (double)intRect.Top / scale - width;
		base.Width = (double)intRect.Width / scale + 2.0 * width;
		base.Height = (double)intRect.Height / scale + 2.0 * width;
		if (!base.IsVisible)
		{
			Show();
		}
	}

	private static bool IsFullscreen(nint hwnd)
	{
		IntRect intRect = WindowMover.VisibleBounds(hwnd);
		Win32.MONITORINFO info = Win32.MONITORINFO.Create();
		Win32.GetMonitorInfo(Win32.MonitorFromWindow(hwnd, 2u), ref info);
		NativeMethods.RECT rcMonitor = info.rcMonitor;
		if (intRect.Left <= rcMonitor.Left && intRect.Top <= rcMonitor.Top && intRect.Right >= rcMonitor.Right)
		{
			return intRect.Bottom >= rcMonitor.Bottom;
		}
		return false;
	}

	protected override void OnClosed(EventArgs e)
	{
		_poll.Stop();
		nint[] array = new nint[3] { _hookForeground, _hookLocation, _hookMinimize };
		foreach (nint num in array)
		{
			if (num != IntPtr.Zero)
			{
				Win32.UnhookWinEvent(num);
			}
		}
		base.OnClosed(e);
	}
}
