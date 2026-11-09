using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Metamorph.Core.Native;

namespace Metamorph.App.Dock;

internal sealed class DockBackdrop : Window
{
	private nint _hwnd;

	private Rectangle _rect;

	private int _radius = -1;

	private bool _visible;

	public nint Handle => _hwnd;

	public DockBackdrop()
	{
		base.Title = "Metamorph Dock Backdrop";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.Left = -32000.0;
		base.Top = -32000.0;
		base.Width = 1.0;
		base.Height = 1.0;
		base.IsHitTestVisible = false;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		long windowLong = NativeMethods.GetWindowLong(_hwnd, -20);
		NativeMethods.SetWindowLong(_hwnd, -20, windowLong | 0x20);
		WindowEffects.EnableBlurBehind(_hwnd);
	}

	public void Adopt(nint dock)
	{
		if (_hwnd != IntPtr.Zero && dock != IntPtr.Zero)
		{
			NativeMethods.SetWindowLong(dock, -8, ((IntPtr)_hwnd).ToInt64());
		}
	}

	public void Follow(Rectangle rect, int radius)
	{
		if (_hwnd == IntPtr.Zero)
		{
			return;
		}
		if (rect.Width < 2 || rect.Height < 2)
		{
			Conceal();
			return;
		}
		bool num = rect.Size != _rect.Size || radius != _radius;
		if (rect != _rect || !_visible)
		{
			Win32.SetWindowPos(_hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, 0x14u | ((!_visible) ? 64u : 0u));
			_rect = rect;
			_visible = true;
		}
		if (num)
		{
			_radius = radius;
			int num2 = Math.Max(0, radius * 2);
			Win32.SetWindowRgn(_hwnd, Win32.CreateRoundRectRgn(0, 0, rect.Width + 1, rect.Height + 1, num2, num2), redraw: true);
		}
	}

	public void Conceal()
	{
		if (_visible && _hwnd != IntPtr.Zero)
		{
			Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, 151u);
			_visible = false;
		}
	}
}
