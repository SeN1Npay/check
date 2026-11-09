using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Shell;

internal sealed class TaskbarCentering : IDisposable
{
	private readonly Timer _timer;

	private readonly HashSet<nint> _moved = new HashSet<nint>();

	private int _busy;

	private bool _disposed;

	public TaskbarCentering()
	{
		_timer = new Timer(delegate
		{
			Tick();
		}, null, 300, 700);
	}

	private void Tick()
	{
		if (_disposed || Interlocked.Exchange(ref _busy, 1) == 1)
		{
			return;
		}
		try
		{
			foreach (nint item in TaskbarStyler.Taskbars().ToList())
			{
				CenterOne(item);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Центрирование панели задач: " + ex.Message);
		}
		finally
		{
			Interlocked.Exchange(ref _busy, 0);
		}
	}

	private void CenterOne(nint taskbar)
	{
		if (!Win32.IsWindowVisible(taskbar))
		{
			return;
		}
		nint num = FindDescendant(taskbar, "MSTaskListWClass");
		if (num == IntPtr.Zero)
		{
			return;
		}
		nint parent = Win32.GetParent(num);
		if (parent == IntPtr.Zero)
		{
			return;
		}
		Win32.GetWindowRect(taskbar, out var rect);
		Win32.GetWindowRect(parent, out var rect2);
		Win32.GetWindowRect(num, out var rect3);
		bool flag = rect.Right - rect.Left > rect.Bottom - rect.Top;
		AutomationElementCollection automationElementCollection = AutomationElement.FromHandle(num).FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
		if (automationElementCollection.Count == 0)
		{
			return;
		}
		double num2 = double.MaxValue;
		double num3 = double.MinValue;
		foreach (AutomationElement item in automationElementCollection)
		{
			Rect boundingRectangle = item.Current.BoundingRectangle;
			if (!boundingRectangle.IsEmpty && !(boundingRectangle.Width <= 0.0))
			{
				num2 = Math.Min(num2, flag ? boundingRectangle.Left : boundingRectangle.Top);
				num3 = Math.Max(num3, flag ? boundingRectangle.Right : boundingRectangle.Bottom);
			}
		}
		if (num2 >= num3)
		{
			return;
		}
		double num4 = num3 - num2;
		int num5 = (flag ? rect.Left : rect.Top);
		int num6 = (flag ? (rect.Right - rect.Left) : (rect.Bottom - rect.Top));
		int num7 = (flag ? rect2.Left : rect2.Top);
		int num8 = (flag ? (rect2.Right - rect2.Left) : (rect2.Bottom - rect2.Top));
		int value = (int)Math.Round((double)num5 + ((double)num6 - num4) / 2.0 - (double)num7);
		value = Math.Clamp(value, 0, Math.Max(0, (int)((double)num8 - num4)));
		int num9 = (flag ? (rect3.Left - rect2.Left) : (rect3.Top - rect2.Top));
		if (Math.Abs(value - num9) <= 1)
		{
			return;
		}
		Move(num, rect2, rect3, flag, value);
		lock (_moved)
		{
			_moved.Add(num);
		}
	}

	private static void Move(nint list, NativeMethods.RECT host, NativeMethods.RECT current, bool horizontal, int offset)
	{
		int x = (horizontal ? offset : (current.Left - host.Left));
		int y = (horizontal ? (current.Top - host.Top) : offset);
		Win32.SetWindowPos(list, IntPtr.Zero, x, y, 0, 0, 16405u);
	}

	private static nint FindDescendant(nint parent, string cls)
	{
		string cls2 = cls;
		nint found = IntPtr.Zero;
		Win32.EnumChildWindows(parent, delegate(nint hwnd, nint _)
		{
			if (Win32.ClassOf(hwnd) != cls2)
			{
				return true;
			}
			found = hwnd;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	public void Dispose()
	{
		_disposed = true;
		_timer.Dispose();
		List<nint> list;
		lock (_moved)
		{
			list = _moved.ToList();
		}
		foreach (nint item in list)
		{
			try
			{
				if (Win32.IsWindow(item))
				{
					Win32.GetWindowRect(Win32.GetParent(item), out var rect);
					Win32.GetWindowRect(item, out var rect2);
					bool horizontal = rect.Right - rect.Left > rect.Bottom - rect.Top;
					Move(item, rect, rect2, horizontal, 0);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Возврат иконок панели задач: " + ex.Message);
			}
		}
	}
}
