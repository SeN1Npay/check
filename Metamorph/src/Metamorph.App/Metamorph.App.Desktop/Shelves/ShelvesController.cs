using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Shelves;

// «Полки» (как Fences). API используется SessionController — сигнатуры не менять.
// Окно на каждую полку; положение, размер и свёрнутость — в shelves.json по id темы (файл темы не трогаем).
internal sealed class ShelvesController : IDisposable
{
	private const uint EventSystemForeground = 3u;

	private readonly ThemeProfile _profile;

	private readonly MetamorphServices _services;

	private readonly List<ShelfWindow> _windows = new List<ShelfWindow>();

	private readonly DispatcherTimer _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private ShelfStore? _store;

	private Win32.WinEventProc? _proc;

	private nint _hook;

	private bool _dirty;

	private bool _front;

	public ShelvesController(ThemeProfile profile, MetamorphServices services)
	{
		_profile = profile;
		_services = services;
		_saveTimer.Tick += delegate
		{
			Flush();
		};
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		return profile.Desktop.Shelves.Enabled;
	}

	public void Start()
	{
		ShelvesSettings settings = _profile.Desktop.Shelves;
		if (!settings.Enabled || settings.Items.Count == 0 || _windows.Count > 0)
		{
			return;
		}
		_store = ShelfStore.Load(Path.Combine(_services.Paths.Root, "shelves.json"));
		for (int i = 0; i < settings.Items.Count; i++)
		{
			ShelfSettings shelf = settings.Items[i];
			try
			{
				string key = ShelfStore.Key(shelf.Title, i);
				ShelfWindow window = new ShelfWindow(this, settings, shelf, key, FolderOf(shelf), _profile.Palette);
				window.Show();
				_windows.Add(window);
			}
			catch (Exception ex)
			{
				Log.Error("Полки: «" + shelf.Title + "»", ex);
			}
		}
		// Рабочий стол на переднем плане — полки поднимаем, иначе они лежат под всеми окнами.
		_proc = OnWinEvent;
		_hook = Win32.SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _proc, 0u, 0u, 0u);
		UpdateFront();
		EventTrace.Mark("shelves.start", $"{_windows.Count}");
	}

	private string FolderOf(ShelfSettings shelf)
	{
		if (!string.IsNullOrWhiteSpace(shelf.Folder))
		{
			return Path.GetFullPath(Environment.ExpandEnvironmentVariables(shelf.Folder.Trim()));
		}
		return ShelfFiles.DefaultFolder(_services.Paths.Root, shelf.Title);
	}

	internal bool IsDefaultFolder(string folder)
	{
		return ShelfFiles.IsInside(folder, Path.Combine(_services.Paths.Root, "shelves"));
	}

	internal ShelfState? Saved(string key)
	{
		return _store?.Get(_profile.Id, key);
	}

	internal void Save(string key, ShelfState state)
	{
		if (_store != null)
		{
			_store.Set(_profile.Id, key, state);
			_dirty = true;
			_saveTimer.Stop();
			_saveTimer.Start();
		}
	}

	private void Flush()
	{
		_saveTimer.Stop();
		if (!_dirty || _store == null)
		{
			return;
		}
		_dirty = false;
		try
		{
			_store.Save();
		}
		catch (Exception ex)
		{
			Log.Error("Полки: сохранение " + _store.FilePath, ex);
		}
	}

	private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
	{
		UpdateFront();
	}

	private void UpdateFront()
	{
		bool front = DesktopLayer.IsDesktopForeground();
		if (front == _front)
		{
			return;
		}
		_front = front;
		foreach (ShelfWindow window in _windows)
		{
			window.SetFront(front);
		}
	}

	public void Dispose()
	{
		if (_hook != IntPtr.Zero)
		{
			Win32.UnhookWinEvent(_hook);
			_hook = IntPtr.Zero;
		}
		foreach (ShelfWindow window in _windows.ToList())
		{
			try
			{
				window.Close();
			}
			catch (Exception ex)
			{
				Log.Error("Полки: закрытие", ex);
			}
		}
		_windows.Clear();
		Flush();
	}
}
