using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Metamorph.App.Desktop;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal sealed class AppBarHost
{
	private readonly nint _hwnd;

	private readonly string _tag;

	private readonly Func<AppBarHost, bool> _place;

	private readonly DispatcherTimer _coalesce;

	private readonly LoopBreaker _breaker = new LoopBreaker(10, TimeSpan.FromSeconds(3.0), TimeSpan.FromSeconds(5.0));

	private NativeMethods.RECT? _lastSet;

	private nint _explorerTray;

	private bool? _fullscreen;

	private const int GWL_EXSTYLE = -20;

	private const long WS_EX_TOPMOST = 8L;

	private const uint SWP_NOZORDER = 4u;

	public uint CallbackMessage { get; }

	public uint TaskbarCreatedMessage { get; }

	public bool Registered { get; private set; }

	public bool Enabled { get; set; } = true;


	public bool FullscreenApp => _fullscreen.GetValueOrDefault();

	public event Action<bool>? FullscreenChanged;

	public AppBarHost(nint hwnd, string tag, Func<AppBarHost, bool> place)
	{
		_hwnd = hwnd;
		_tag = tag;
		_place = place;
		CallbackMessage = NativeMethods.RegisterWindowMessage("Metamorph.AppBarMessage." + tag);
		TaskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
		_coalesce = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(120.0)
		};
		_coalesce.Tick += delegate
		{
			_coalesce.Stop();
			try
			{
				_place(this);
			}
			catch (Exception ex)
			{
				Log.Error("AppBar " + _tag + ": положение", ex);
			}
		};
		_explorerTray = TaskbarController.FindExplorerTaskbar();
	}

	public bool Register()
	{
		NativeMethods.APPBARDATA pData = NativeMethods.APPBARDATA.Create(_hwnd);
		pData.uCallbackMessage = CallbackMessage;
		bool flag = NativeMethods.SHAppBarMessage(0u, ref pData) != UIntPtr.Zero;
		Registered = flag || Registered;
		_explorerTray = TaskbarController.FindExplorerTaskbar();
		EventTrace.Mark("appbar.new", $"{_tag} ok={flag}");
		return Registered;
	}

	public void Remove()
	{
		_coalesce.Stop();
		if (Registered)
		{
			NativeMethods.APPBARDATA pData = NativeMethods.APPBARDATA.Create(_hwnd);
			NativeMethods.SHAppBarMessage(1u, ref pData);
			Registered = false;
			_lastSet = null;
			EventTrace.Mark("appbar.remove", _tag);
		}
	}

	public void Queue()
	{
		if (!_coalesce.IsEnabled)
		{
			_coalesce.Start();
		}
	}

	public NativeMethods.RECT Reserve(uint edge, NativeMethods.RECT desired)
	{
		if (!Registered)
		{
			return desired;
		}
		NativeMethods.APPBARDATA pData = NativeMethods.APPBARDATA.Create(_hwnd);
		pData.uEdge = edge;
		pData.rc = desired;
		NativeMethods.SHAppBarMessage(2u, ref pData);
		int num = desired.Bottom - desired.Top;
		int num2 = desired.Right - desired.Left;
		switch (edge)
		{
		case 1u:
			pData.rc.Bottom = pData.rc.Top + num;
			break;
		case 3u:
			pData.rc.Top = pData.rc.Bottom - num;
			break;
		case 0u:
			pData.rc.Right = pData.rc.Left + num2;
			break;
		case 2u:
			pData.rc.Left = pData.rc.Right - num2;
			break;
		}
		NativeMethods.RECT? lastSet = _lastSet;
		if (lastSet.HasValue)
		{
			NativeMethods.RECT valueOrDefault = lastSet.GetValueOrDefault();
			if (Same(valueOrDefault, pData.rc))
			{
				return pData.rc;
			}
		}
		if (!_breaker.Allow())
		{
			EventTrace.Mark("appbar.loop", _tag);
			if (_breaker.Trips == 1)
			{
				Log.Warn("AppBar " + _tag + ": Проводник гоняет положение по кругу — пауза 5 с.");
			}
			return _lastSet ?? pData.rc;
		}
		NativeMethods.SHAppBarMessage(3u, ref pData);
		_lastSet = pData.rc;
		EventTrace.Mark("appbar.setpos", $"{_tag} {pData.rc.Left},{pData.rc.Top},{pData.rc.Right},{pData.rc.Bottom}");
		return pData.rc;
	}

	public static bool MoveIfNeeded(nint hwnd, NativeMethods.RECT rc, bool topmost)
	{
		NativeMethods.RECT rect;
		bool flag = Win32.GetWindowRect(hwnd, out rect) && Same(rect, rc) && NativeMethods.IsWindowVisible(hwnd);
		if (flag && !topmost)
		{
			return false;
		}
		if (flag && topmost && IsTopmost(hwnd))
		{
			return false;
		}
		NativeMethods.SetWindowPos(hwnd, topmost ? NativeMethods.HWND_TOPMOST : IntPtr.Zero, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, 0x50u | ((!topmost) ? 4u : 0u));
		return true;
	}

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr(nint hwnd, int index);

	public static bool IsTopmost(nint hwnd)
	{
		return (((IntPtr)GetWindowLongPtr(hwnd, -20)).ToInt64() & 8) != 0;
	}

	public static bool Same(NativeMethods.RECT a, NativeMethods.RECT b)
	{
		if (a.Left == b.Left && a.Top == b.Top && a.Right == b.Right)
		{
			return a.Bottom == b.Bottom;
		}
		return false;
	}

	public bool HandleMessage(int msg, nint wParam, nint lParam, Display display, Action explorerRestarted)
	{
		if (msg == (int)CallbackMessage && CallbackMessage != 0)
		{
			bool flag;
			int num;
			bool flag2;
			switch (((IntPtr)wParam).ToInt32())
			{
			case 1:
				EventTrace.Mark("appbar.poschanged", _tag);
				Queue();
				break;
			case 2:
				{
					flag = lParam != IntPtr.Zero;
					if (flag)
					{
						Point? point = DesktopLayer.CoveredMonitor(maximized: false);
						if (point.HasValue)
						{
							Point valueOrDefault = point.GetValueOrDefault();
							num = (display.Contains(valueOrDefault.X, valueOrDefault.Y) ? 1 : 0);
							goto IL_008c;
						}
					}
					num = 0;
					goto IL_008c;
				}
				IL_008c:
				flag2 = (byte)num != 0;
				EventTrace.Mark("appbar.fullscreen", $"{_tag} claimed={flag} real={flag2}");
				SetFullscreen(flag2);
				break;
			}
			return true;
		}
		if (msg == (int)TaskbarCreatedMessage && TaskbarCreatedMessage != 0)
		{
			nint num2 = TaskbarController.FindExplorerTaskbar();
			bool flag3 = num2 != IntPtr.Zero && num2 != _explorerTray;
			EventTrace.Mark("explorer.taskbarcreated", $"{_tag} real={flag3}");
			if (Enabled && (flag3 || !Registered))
			{
				Registered = false;
				_lastSet = null;
				Register();
				Queue();
			}
			if (flag3)
			{
				explorerRestarted();
			}
			return false;
		}
		if (msg == 126)
		{
			EventTrace.Mark("display.change", _tag);
			_lastSet = null;
			Queue();
		}
		return false;
	}

	public void SetFullscreen(bool value)
	{
		if (_fullscreen != value)
		{
			_fullscreen = value;
			this.FullscreenChanged?.Invoke(value);
		}
	}
}
