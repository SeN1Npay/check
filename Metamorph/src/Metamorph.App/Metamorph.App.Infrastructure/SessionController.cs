using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Metamorph.App.Bar;
using Metamorph.App.Desktop;
using Metamorph.App.Desktop.Cards;
using Metamorph.App.Desktop.Effects;
using Metamorph.App.Desktop.Shelves;
using Metamorph.App.Dock;
using Metamorph.App.Island;
using Metamorph.App.Shell;
using Metamorph.App.Windows;
using Metamorph.Core.Assistant;
using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;
using Microsoft.Win32;

namespace Metamorph.App.Infrastructure;

public sealed class SessionController
{
	private readonly MetamorphServices _services;

	private readonly Action _openMainWindow;

	private readonly DispatcherTimer _taskbarGuard = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.0)
	};

	private readonly DispatcherTimer _displayDebounce = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.5)
	};

	private readonly List<BarWindow> _bars = new List<BarWindow>();

	private readonly List<DockWindow> _docks = new List<DockWindow>();

	private readonly List<IslandWindow> _islands = new List<IslandWindow>();

	private TaskbarStyleKeeper? _taskbarStyle;

	private TaskbarCentering? _centering;

	private ActiveBorderWindow? _border;

	private DesktopWidgetWindow? _widget;

	private WallpaperSession? _wallpaper;

	private CardsController? _cards;

	private EffectsController? _effects;

	private ShelvesController? _shelves;

	private HotCorners? _hotCorners;

	// Выполняет действие горячего угла (назначает App).
	public Action<string>? DesktopAction { get; set; }

	private bool _barHidden;

	private bool _dockHidden;

	private bool _islandHidden;

	private bool _shutDown;

	private string _displaySnapshot = DisplaySnapshot();

	private DateTime _lastExplorerRestart = DateTime.MinValue;

	private readonly LoopBreaker _taskbarRehide = new LoopBreaker(6, TimeSpan.FromSeconds(20.0), TimeSpan.FromMinutes(1.0));

	private ThemeProfile? _wallpaperBase;

	public ThemeProfile? Active { get; private set; }

	public bool IsBarVisible => _bars.Count > 0;

	public bool IsDockVisible => _docks.Count > 0;

	public bool IsWallpaperPaused { get; private set; }

	public bool HasSlides => _wallpaper?.HasSlides ?? false;

	public bool IsWallpaperAnimated => _wallpaper?.IsAnimated ?? false;

	public SessionController(MetamorphServices services, Action openMainWindow)
	{
		_services = services;
		_openMainWindow = openMainWindow;
		_taskbarGuard.Tick += delegate
		{
			GuardTaskbar();
		};
		_displayDebounce.Tick += delegate
		{
			_displayDebounce.Stop();
			OnDisplaysChanged();
		};
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
	}

	public void RestoreLastSession()
	{
		ThemeProfile appliedProfile = _services.State.Load().AppliedProfile;
		if (appliedProfile != null)
		{
			Activate(appliedProfile, reapplyTaskbar: true);
		}
	}

	public void Activate(ThemeProfile profile, bool reapplyTaskbar = false)
	{
		EventTrace.Mark("session.activate", profile.Id);
		CloseAll();
		Active = profile;
		_barHidden = (_dockHidden = (_islandHidden = false));
		ShowBars(profile);
		ShowDocks(profile);
		ShowIslands(profile, reapplyTaskbar ? null : profile.Name);
		if (reapplyTaskbar)
		{
			try
			{
				TaskbarController.Apply(profile.Taskbar.Mode);
			}
			catch (Exception ex)
			{
				Log.Error("Taskbar", ex);
			}
		}
		_taskbarGuard.IsEnabled = profile.Taskbar.Mode == TaskbarMode.Hidden;
		StartExtras(profile);
	}

	public void Deactivate()
	{
		CloseAll();
		Try("Обои: подложки", DesktopSurfaces.CloseUnused);
		_taskbarGuard.Stop();
		Active = null;
	}

	public void ToggleBar()
	{
		if (_bars.Count > 0)
		{
			CloseBars();
			_barHidden = true;
		}
		else if (Active != null)
		{
			_barHidden = false;
			ShowBars(Active, force: true);
		}
	}

	public void ToggleDock()
	{
		if (_docks.Count > 0)
		{
			CloseDocks();
			_dockHidden = true;
		}
		else if (Active != null)
		{
			_dockHidden = false;
			ShowDocks(Active, force: true);
		}
	}

	public void Shutdown()
	{
		if (!_shutDown)
		{
			_shutDown = true;
			SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
			ThemeProfile? active = Active;
			bool num = active != null && active.Taskbar.Mode == TaskbarMode.Hidden;
			CloseAll();
			Try("Обои: подложки", delegate
			{
				DesktopSurfaces.DestroyAll(redraw: true);
			});
			Try("Обои: поток", WallpaperThread.Shutdown);
			TrayHost.Shutdown();
			_taskbarGuard.Stop();
			if (num)
			{
				TaskbarController.SafeShow();
			}
		}
	}

	private void CloseAll()
	{
		CloseBars();
		CloseDocks();
		CloseIslands();
		StopExtras();
	}

	private void ShowBars(ThemeProfile profile, bool force = false)
	{
		CloseBars();
		if (!profile.Bar.Enabled && !force)
		{
			return;
		}
		EventTrace.Mark("session.bars.show", profile.Id);
		foreach (Display item in Display.For(profile.Bar.Screens))
		{
			try
			{
				BarWindow barWindow = new BarWindow(profile, item, _openMainWindow, _services);
				barWindow.ExplorerRestarted += delegate
				{
					OnExplorerRestarted();
				};
				barWindow.Show();
				_bars.Add(barWindow);
			}
			catch (Exception ex)
			{
				Log.Error("Не удалось показать панель на " + item.Name, ex);
			}
		}
	}

	private void CloseBars()
	{
		foreach (BarWindow bar in _bars)
		{
			try
			{
				bar.Close();
			}
			catch (Exception ex)
			{
				Log.Error("Закрытие панели", ex);
			}
		}
		_bars.Clear();
	}

	private void ShowDocks(ThemeProfile profile, bool force = false)
	{
		CloseDocks();
		if (!profile.Dock.Enabled && !force)
		{
			return;
		}
		EventTrace.Mark("session.docks.show", profile.Id);
		foreach (Display item in Display.For(profile.Dock.Screens))
		{
			try
			{
				DockWindow dockWindow = new DockWindow(profile, _services.DockPins, _openMainWindow, item);
				dockWindow.ExplorerRestarted += delegate
				{
					OnExplorerRestarted();
				};
				dockWindow.Show();
				_docks.Add(dockWindow);
			}
			catch (Exception ex)
			{
				Log.Error("Не удалось показать док на " + item.Name, ex);
			}
		}
	}

	private void CloseDocks()
	{
		foreach (DockWindow dock in _docks)
		{
			try
			{
				dock.Close();
			}
			catch (Exception ex)
			{
				Log.Error("Закрытие дока", ex);
			}
		}
		_docks.Clear();
	}

	public void Announce(string glyph, string label, string value)
	{
		foreach (IslandWindow island in _islands)
		{
			island.Announce(glyph, label, value);
		}
	}

	public void UpdateFocus(FocusTimer timer)
	{
		IslandWindow.FocusSource = (timer.Running ? timer : null);
	}

	public void SummonIsland()
	{
		ThemeProfile active = Active;
		if (active == null)
		{
			return;
		}
		if (_islands.Count == 0)
		{
			_islandHidden = false;
			ShowIslands(active, null, force: true);
		}
		foreach (IslandWindow island in _islands)
		{
			island.Summon(toggle: true);
		}
	}

	private void ShowIslands(ThemeProfile profile, string? announce, bool force = false)
	{
		CloseIslands();
		if (!profile.Island.Enabled && !force)
		{
			return;
		}
		foreach (Display item in Display.For(profile.Island.Screens))
		{
			try
			{
				IslandWindow island = new IslandWindow(profile, item, _openMainWindow, announce);
				island.Closed += delegate
				{
					if (_islands.Remove(island) && _islands.Count == 0)
					{
						_islandHidden = true;
					}
				};
				island.Show();
				_islands.Add(island);
			}
			catch (Exception ex)
			{
				Log.Error("Не удалось показать остров на " + item.Name, ex);
			}
		}
	}

	private void CloseIslands()
	{
		IslandWindow[] array = _islands.ToArray();
		_islands.Clear();
		IslandWindow[] array2 = array;
		foreach (IslandWindow islandWindow in array2)
		{
			try
			{
				islandWindow.Close();
			}
			catch (Exception ex)
			{
				Log.Error("Закрытие острова", ex);
			}
		}
	}

	private void StartExtras(ThemeProfile profile)
	{
		ThemeProfile profile2 = profile;
		Try("Стиль панели задач", delegate
		{
			if (profile2.Taskbar.Style != 0 && profile2.Taskbar.Mode != TaskbarMode.Hidden)
			{
				_taskbarStyle = new TaskbarStyleKeeper(profile2);
			}
		});
		Try("Центрирование значков панели задач", delegate
		{
			if (profile2.Taskbar.CenterIcons && profile2.Taskbar.Mode != TaskbarMode.Hidden)
			{
				_centering = new TaskbarCentering();
			}
		});
		Try("Рамка активного окна", delegate
		{
			if (profile2.Windows.ActiveBorder.Enabled)
			{
				_border = new ActiveBorderWindow(profile2);
				_border.Show();
			}
		});
		Try("Виджет рабочего стола", delegate
		{
			if (profile2.Desktop.Widget.Enabled)
			{
				_widget = new DesktopWidgetWindow(profile2, _services);
				_widget.Show();
			}
		});
		Try("Обои", StartWallpaper);
		Try("Обои: подложки", DesktopSurfaces.CloseUnused);
		StartDesktopExtras(profile2);
	}

	private void StartDesktopExtras(ThemeProfile profile)
	{
		Try("Эффекты рабочего стола", delegate
		{
			if (EffectsController.IsNeeded(profile))
			{
				_effects = new EffectsController(profile, _services);
				_effects.Start();
			}
		});
		Try("Полки", delegate
		{
			if (ShelvesController.IsNeeded(profile))
			{
				_shelves = new ShelvesController(profile, _services);
				_shelves.Start();
			}
		});
		Try("Карточки рабочего стола", delegate
		{
			if (CardsController.IsNeeded(profile))
			{
				_cards = new CardsController(profile, _services);
				_cards.Start();
			}
		});
		Try("Горячие углы", delegate
		{
			if (profile.Desktop.HotCorners.Enabled)
			{
				_hotCorners = new HotCorners(profile.Desktop.HotCorners, delegate(string action)
				{
					DesktopAction?.Invoke(action);
				}, profile.Palette.Accent);
				_hotCorners.Start();
			}
		});
		MemoryTrim.Schedule();
	}

	private void StopDesktopExtras()
	{
		Try("Карточки", delegate
		{
			_cards?.Dispose();
		});
		Try("Эффекты", delegate
		{
			_effects?.Dispose();
		});
		Try("Полки", delegate
		{
			_shelves?.Dispose();
		});
		Try("Горячие углы", delegate
		{
			_hotCorners?.Dispose();
		});
		_cards = null;
		_effects = null;
		_shelves = null;
		_hotCorners = null;
	}

	// Режим редактирования рабочего стола (карточки таскаются мышью). Возвращает false, если карточки выключены.
	public bool ToggleDesktopEdit()
	{
		if (_cards == null)
		{
			return false;
		}
		_cards.ToggleEditMode();
		return true;
	}

	public ThemeProfile EffectiveWallpaper(ThemeProfile theme)
	{
		return WallpaperChoices.Effective(theme, _services.Settings.Current.Wallpaper);
	}

	private void StartWallpaper()
	{
		ThemeProfile themeProfile = Active ?? _wallpaperBase;
		if (themeProfile == null)
		{
			return;
		}
		ThemeProfile themeProfile2 = EffectiveWallpaper(themeProfile);
		if (WallpaperSession.IsNeeded(themeProfile2))
		{
			_wallpaper = new WallpaperSession(themeProfile2, _services);
			_wallpaper.Start();
			if (IsWallpaperPaused)
			{
				_wallpaper.ManualPause = true;
			}
		}
	}

	public void RefreshWallpaper(ThemeProfile? baseTheme = null)
	{
		if (Active == null)
		{
			_wallpaperBase = baseTheme ?? _wallpaperBase;
		}
		Try("Обои: перезапуск", delegate
		{
			EventTrace.Mark("session.wallpaper.refresh");
			_wallpaper?.Dispose();
			_wallpaper = null;
			StartWallpaper();
			DesktopSurfaces.CloseUnused();
			_effects?.OnWallpaperChanged();
			MemoryTrim.Schedule();
		});
	}

	public void SetWallpaperVolume(double volume)
	{
		_wallpaper?.SetVolume(volume);
	}

	public void ToggleWallpaperPause()
	{
		IsWallpaperPaused = !IsWallpaperPaused;
		if (_wallpaper != null)
		{
			_wallpaper.ManualPause = IsWallpaperPaused;
		}
	}

	private void StopExtras()
	{
		Try("Стиль панели задач", delegate
		{
			_taskbarStyle?.Dispose();
		});
		Try("Центрирование", delegate
		{
			_centering?.Dispose();
		});
		Try("Рамка активного окна", delegate
		{
			_border?.Close();
		});
		Try("Виджет", delegate
		{
			_widget?.Close();
		});
		Try("Обои", delegate
		{
			_wallpaper?.Dispose();
		});
		StopDesktopExtras();
		_wallpaperBase = null;
		_taskbarStyle = null;
		_centering = null;
		_border = null;
		_widget = null;
		_wallpaper = null;
	}

	private static void Try(string what, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error(what, ex);
		}
	}

	public void NextWallpaper()
	{
		_wallpaper?.NextImage();
		_effects?.OnWallpaperChanged();
	}

	private void OnDisplaySettingsChanged(object? sender, EventArgs e)
	{
		EventTrace.Mark("msg.displaychange");
		Application.Current?.Dispatcher.InvokeAsync(delegate
		{
			_displayDebounce.Stop();
			_displayDebounce.Start();
		});
	}

	private static string DisplaySnapshot()
	{
		try
		{
			return string.Join(";", from d in Display.All()
				select $"{d.Name}:{d.Bounds}:{d.Primary}") + "@" + Display.Scale.ToString("0.00");
		}
		catch
		{
			return "";
		}
	}

	private void OnDisplaysChanged()
	{
		ThemeProfile profile = Active;
		if (profile == null || _shutDown)
		{
			return;
		}
		string text = DisplaySnapshot();
		if (text == _displaySnapshot && text.Length > 0)
		{
			EventTrace.Mark("session.displays.same");
			return;
		}
		_displaySnapshot = text;
		EventTrace.Mark("session.displays.rebuild", text);
		Log.Info("Конфигурация мониторов изменилась — пересоздаю панели и доки");
		if (!_barHidden)
		{
			ShowBars(profile);
		}
		if (!_dockHidden)
		{
			ShowDocks(profile);
		}
		if (!_islandHidden)
		{
			ShowIslands(profile, null);
		}
		Try("Виджет после смены мониторов", delegate
		{
			if (_widget != null)
			{
				_widget.Close();
				_widget = new DesktopWidgetWindow(profile, _services);
				_widget.Show();
			}
		});
		Try("Рабочий стол после смены мониторов", delegate
		{
			StopDesktopExtras();
			StartDesktopExtras(profile);
		});
		Try("Обои после смены мониторов", delegate
		{
			if (EffectiveWallpaper(profile).Wallpaper.EffectiveKind == WallpaperKind.Video && _wallpaper != null)
			{
				_wallpaper.Dispose();
				_wallpaper = null;
				StartWallpaper();
				DesktopSurfaces.CloseUnused();
			}
		});
	}

	private void OnExplorerRestarted()
	{
		if (Active != null && !(DateTime.UtcNow - _lastExplorerRestart < TimeSpan.FromSeconds(3.0)))
		{
			_lastExplorerRestart = DateTime.UtcNow;
			EventTrace.Mark("explorer.restarted");
			try
			{
				TaskbarController.Apply(Active.Taskbar.Mode);
			}
			catch (Exception ex)
			{
				Log.Error("Taskbar после перезапуска Проводника", ex);
			}
			Try("Стиль панели задач после перезапуска Проводника", delegate
			{
				_taskbarStyle?.Reapply();
			});
		}
	}

	private void GuardTaskbar()
	{
		try
		{
			ThemeProfile? active = Active;
			if (active != null && active.Taskbar.Mode == TaskbarMode.Hidden && TaskbarController.IsVisible())
			{
				EventTrace.Mark("taskbar.rehide");
				if (!_taskbarRehide.Allow())
				{
					Log.Warn("Проводник постоянно возвращает панель задач — перестаю её прятать на минуту.");
				}
				else
				{
					TaskbarController.SetVisible(visible: false);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("Taskbar guard", ex);
		}
	}

	public string Describe()
	{
		StringBuilder stringBuilder;
		StringBuilder stringBuilder2 = (stringBuilder = new StringBuilder());
		StringBuilder stringBuilder3 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(56, 7, stringBuilder);
		handler.AppendLiteral("active=");
		handler.AppendFormatted(Active?.Id ?? "-");
		handler.AppendLiteral(" bars=");
		handler.AppendFormatted(_bars.Count);
		handler.AppendLiteral(" docks=");
		handler.AppendFormatted(_docks.Count);
		handler.AppendLiteral(" islands=");
		handler.AppendFormatted(_islands.Count);
		handler.AppendLiteral(" hidden(bar,dock,island)=");
		handler.AppendFormatted(_barHidden);
		handler.AppendLiteral(",");
		handler.AppendFormatted(_dockHidden);
		handler.AppendLiteral(",");
		handler.AppendFormatted(_islandHidden);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder = stringBuilder2;
		StringBuilder stringBuilder4 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(54, 5, stringBuilder);
		handler.AppendLiteral("taskbarStyle=");
		handler.AppendFormatted(_taskbarStyle != null);
		handler.AppendLiteral(" centering=");
		handler.AppendFormatted(_centering != null);
		handler.AppendLiteral(" border=");
		handler.AppendFormatted(_border != null);
		handler.AppendLiteral(" widget=");
		handler.AppendFormatted(_widget != null);
		handler.AppendLiteral(" taskbarGuard=");
		handler.AppendFormatted(_taskbarGuard.IsEnabled);
		stringBuilder4.AppendLine(ref handler);
		stringBuilder = stringBuilder2;
		StringBuilder stringBuilder5 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(22, 3, stringBuilder);
		handler.AppendLiteral("wallpaper: ");
		handler.AppendFormatted(_wallpaper?.Describe() ?? "none");
		handler.AppendLiteral("; paused=");
		handler.AppendFormatted(IsWallpaperPaused);
		handler.AppendLiteral("; ");
		handler.AppendFormatted(DesktopSurfaces.Describe());
		stringBuilder5.AppendLine(ref handler);
		stringBuilder = stringBuilder2;
		StringBuilder stringBuilder6 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder);
		handler.AppendLiteral("displays: ");
		handler.AppendFormatted(_displaySnapshot);
		stringBuilder6.AppendLine(ref handler);
		return stringBuilder2.ToString();
	}
}
