using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.Core.Dock;

public sealed class WindowTracker
{
	private static readonly HashSet<string> IgnoredClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "ForegroundStaging", "MultitaskingViewFrame", "XamlExplorerHostIslandWindow", "TaskListThumbnailWnd" };

	private readonly Dictionary<nint, TrackedWindow> _windows = new Dictionary<nint, TrackedWindow>();

	private readonly uint _ownPid = (uint)Environment.ProcessId;

	private long _sequence;

	public IReadOnlyCollection<TrackedWindow> Windows => _windows.Values;

	public nint Foreground { get; private set; }

	public event EventHandler? Changed;

	public void Refresh()
	{
		bool changed = false;
		HashSet<nint> seen = new HashSet<nint>();
		ShellNative.EnumWindows(delegate(nint hwnd, nint _)
		{
			try
			{
				if (!IsTaskbarWindow(hwnd))
				{
					return true;
				}
				seen.Add(hwnd);
				if (_windows.TryGetValue(hwnd, out TrackedWindow value))
				{
					changed |= UpdateDynamic(value);
				}
				else
				{
					_windows[hwnd] = Create(hwnd);
					changed = true;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("WindowTracker: " + ex.Message);
			}
			return true;
		}, IntPtr.Zero);
		foreach (nint item in _windows.Keys.Where((nint h) => !seen.Contains(h)).ToList())
		{
			_windows.Remove(item);
			changed = true;
		}
		changed |= SetForeground(NativeMethods.GetForegroundWindow());
		if (changed)
		{
			this.Changed?.Invoke(this, EventArgs.Empty);
		}
	}

	public bool OnShellMessage(int code, nint hwnd)
	{
		switch (code)
		{
		case 32774:
		{
			if (_windows.TryGetValue(hwnd, out TrackedWindow value2) && hwnd != Foreground && !value2.IsFlashing)
			{
				value2.IsFlashing = true;
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
			return false;
		}
		case 4:
		case 32772:
			if (SetForeground(hwnd))
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
			if (!_windows.ContainsKey(hwnd))
			{
				return hwnd != IntPtr.Zero;
			}
			return false;
		case 6:
		{
			if (_windows.TryGetValue(hwnd, out TrackedWindow value) && UpdateDynamic(value))
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
			return false;
		}
		case 1:
		case 2:
		case 13:
		case 16:
			return true;
		default:
			return false;
		}
	}

	private bool SetForeground(nint hwnd)
	{
		if (hwnd == Foreground)
		{
			return false;
		}
		Foreground = hwnd;
		if (_windows.TryGetValue(hwnd, out TrackedWindow value))
		{
			value.IsFlashing = false;
			value.LastActive = Stopwatch.GetTimestamp();
		}
		return true;
	}

	private TrackedWindow Create(nint hwnd)
	{
		NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
		TrackedWindow obj = new TrackedWindow(hwnd)
		{
			ProcessId = processId,
			ExePath = (ShellIcons.TryGetProcessPath(processId) ?? ""),
			AppId = ShellIcons.TryGetWindowAppId(hwnd),
			FirstSeen = ++_sequence,
			LastActive = ((hwnd == NativeMethods.GetForegroundWindow()) ? Stopwatch.GetTimestamp() : 0)
		};
		UpdateDynamic(obj);
		return obj;
	}

	private static bool UpdateDynamic(TrackedWindow window)
	{
		string text = ReadTitle(window.Handle);
		bool flag = ShellNative.IsIconic(window.Handle);
		bool flag2 = text != window.Title || flag != window.IsMinimized;
		window.Title = text;
		window.IsMinimized = flag;
		if (window.AppId == null && window.IsUwp)
		{
			window.AppId = ShellIcons.TryGetWindowAppId(window.Handle);
			flag2 |= window.AppId != null;
		}
		return flag2;
	}

	private bool IsTaskbarWindow(nint hwnd)
	{
		if (!NativeMethods.IsWindowVisible(hwnd))
		{
			return false;
		}
		long windowLong = NativeMethods.GetWindowLong(hwnd, -20);
		bool flag = (windowLong & 0x40000) != 0;
		if (!flag)
		{
			if ((windowLong & 0x80) != 0L)
			{
				return false;
			}
			if ((windowLong & 0x8000000) != 0L)
			{
				return false;
			}
			if (ShellNative.GetWindow(hwnd, 4u) != IntPtr.Zero)
			{
				return false;
			}
		}
		if (IsCloaked(hwnd))
		{
			return false;
		}
		NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
		if (processId == _ownPid)
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		ShellNative.GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);
		if (IgnoredClasses.Contains(stringBuilder.ToString()))
		{
			return false;
		}
		if (!flag)
		{
			return NativeMethods.GetWindowTextLength(hwnd) > 0;
		}
		return true;
	}

	private static bool IsCloaked(nint hwnd)
	{
		try
		{
			int value;
			return ShellNative.DwmGetWindowAttribute(hwnd, 14, out value, 4) == 0 && value != 0;
		}
		catch (DllNotFoundException)
		{
			return false;
		}
	}

	private static string ReadTitle(nint hwnd)
	{
		int windowTextLength = NativeMethods.GetWindowTextLength(hwnd);
		if (windowTextLength <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
		NativeMethods.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}
}
