using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Metamorph.Core.Input;
using Metamorph.Core.Localization;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

public sealed class HotkeyManager : IDisposable
{
	private readonly HwndSource _source;

	private readonly Dictionary<int, string> _actions = new Dictionary<int, string>();

	private readonly Action<string> _handler;

	public List<string> Failures { get; } = new List<string>();


	public HotkeyManager(Action<string> handler)
	{
		_handler = handler;
		_source = new HwndSource(new HwndSourceParameters("Metamorph.Hotkeys")
		{
			WindowStyle = 0,
			ParentWindow = new IntPtr(-3)
		});
		_source.AddHook(WndProc);
	}

	public void Apply(IReadOnlyDictionary<string, string> map)
	{
		UnregisterAll();
		Failures.Clear();
		int num = 1;
		foreach (var (text3, text4) in map)
		{
			if (!string.IsNullOrWhiteSpace(text4))
			{
				if (!HotkeyGesture.TryParse(text4, out var gesture))
				{
					Failures.Add(Loc.F("{0}: не понял сочетание «{1}»", text3, text4));
				}
				else if (Win32.RegisterHotKey(_source.Handle, num, gesture.Modifiers | 0x4000u, gesture.Key))
				{
					_actions[num] = text3;
					num++;
				}
				else
				{
					Failures.Add(Loc.F("{0}: «{1}» уже занято другой программой", text3, gesture));
				}
			}
		}
		if (Failures.Count > 0)
		{
			Log.Warn("Горячие клавиши: " + string.Join("; ", Failures));
		}
	}

	private void UnregisterAll()
	{
		foreach (int key in _actions.Keys)
		{
			Win32.UnregisterHotKey(_source.Handle, key);
		}
		_actions.Clear();
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && _actions.TryGetValue(((IntPtr)wParam).ToInt32(), out string value))
		{
			handled = true;
			try
			{
				_handler(value);
			}
			catch (Exception ex)
			{
				Log.Error("Горячая клавиша " + value, ex);
			}
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		UnregisterAll();
		_source.RemoveHook(WndProc);
		_source.Dispose();
	}
}
