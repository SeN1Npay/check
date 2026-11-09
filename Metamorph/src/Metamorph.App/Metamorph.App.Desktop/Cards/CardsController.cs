using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

// Стеклянные карточки на рабочем столе. API используется SessionController — сигнатуры не менять.
internal sealed class CardsController : IDisposable
{
	private readonly ThemeProfile _profile;

	private readonly CardsSettings _settings;

	private readonly CardTheme _theme;

	private readonly CardContext _context;

	private readonly CardLayoutStore _store;

	private readonly Dispatcher _dispatcher;

	private readonly List<CardWindow> _windows = new List<CardWindow>();

	// Один секундный таймер на все карточки; отдельный опрос активности рабочего стола раз в 2 с.
	private readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };

	private readonly DispatcherTimer _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2.0) };

	private List<CardSettings> _items = new List<CardSettings>();

	private CardHintWindow? _hint;

	private CardGuideWindow? _guideX;

	private CardGuideWindow? _guideY;

	private long _n;

	private bool _started;

	private bool _disposed;

	private bool _dirty;

	public CardsController(ThemeProfile profile, MetamorphServices services)
	{
		_profile = profile;
		_settings = profile.Desktop.Cards;
		_theme = new CardTheme(profile);
		_store = new CardLayoutStore(services.Paths);
		_context = new CardContext(_theme, profile, services, new CardDataStore(services.Paths));
		_dispatcher = Dispatcher.CurrentDispatcher;
		_tick.Tick += delegate
		{
			OnTick();
		};
		_poll.Tick += delegate
		{
			UpdateActivity();
		};
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		return profile.Desktop.Cards.Enabled;
	}

	public bool EditMode { get; private set; }

	public void Start()
	{
		if (_started || _disposed)
		{
			return;
		}
		_started = true;
		_items = _store.Effective(_settings, _profile.Id);
		IReadOnlyList<Display> displays = Display.All();
		for (int i = 0; i < _items.Count; i++)
		{
			CreateWindow(_items[i], i, displays);
		}
		DesktopActivity.Changed += OnActivityChanged;
		_poll.Start();
		UpdateActivity();
	}

	private CardWindow? CreateWindow(CardSettings item, int index, IReadOnlyList<Display> displays)
	{
		CardView? view = null;
		try
		{
			view = CardView.Create(item.Kind, _context, item);
			Display display = displays.FirstOrDefault(d => d.Name == item.Monitor) ?? displays.FirstOrDefault() ?? Display.PrimaryDisplay;
			(double w, double h) = _theme.SizeOf(item);
			CardRect rect = CardLayout.ToRect(item, Area(display), w, h);
			CardWindow window = new CardWindow(this, view, _theme, display, rect);
			_windows.Add(window);
			window.Show();
			window.Appear(index);
			if (EditMode)
			{
				window.SetEdit(on: true);
			}
			return window;
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: не удалось создать «" + item.Kind + "»", ex);
			view?.Dispose();
			return null;
		}
	}

	private static CardRect Area(Display display)
	{
		Rect wa = display.WorkAreaDip;
		return new CardRect(wa.X, wa.Y, wa.Width, wa.Height);
	}

	private void OnTick()
	{
		_n++;
		foreach (CardWindow w in _windows.ToList())
		{
			if (w.View.Active)
			{
				try
				{
					w.View.Tick(_n);
				}
				catch (Exception ex)
				{
					Log.Error("Карточки: обновление " + w.View.Settings.Kind, ex);
				}
			}
			w.KeepOnDesktop();
		}
	}

	private void OnActivityChanged()
	{
		_dispatcher.BeginInvoke(new Action(UpdateActivity));
	}

	// Содержимое обновляется, пока экран не заблокирован и монитор не закрыт окном;
	// анимации колец дополнительно гасятся при RDP/экономии заряда (CardRing.Enabled).
	private void UpdateActivity()
	{
		if (_disposed)
		{
			return;
		}
		bool locked = DesktopActivity.Locked;
		CardRing.Enabled = DesktopActivity.CanAnimate;
		Dictionary<string, bool> covered = new Dictionary<string, bool>();
		bool any = false;
		foreach (CardWindow w in _windows.ToList())
		{
			if (!covered.TryGetValue(w.Display.Name, out bool isCovered))
			{
				isCovered = DesktopActivity.IsCovered(w.Display);
				covered[w.Display.Name] = isCovered;
			}
			bool active = !locked && (EditMode || !isCovered);
			w.View.SetActive(active);
			any |= active;
			if (!_tick.IsEnabled)
			{
				w.KeepOnDesktop();
			}
		}
		if (any && !_tick.IsEnabled)
		{
			_tick.Start();
		}
		else if (!any && _tick.IsEnabled)
		{
			_tick.Stop();
		}
	}

	public void ToggleEditMode()
	{
		if (_disposed)
		{
			return;
		}
		if (EditMode)
		{
			ExitEdit();
		}
		else
		{
			EnterEdit();
		}
	}

	private void EnterEdit()
	{
		EditMode = true;
		foreach (CardWindow w in _windows)
		{
			w.SetEdit(on: true);
		}
		try
		{
			_hint = new CardHintWindow(_theme, ExitEdit, target => OpenMenu(null, target));
			_hint.Show();
			_hint.Activate();
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: подсказка режима редактирования", ex);
		}
		UpdateActivity();
	}

	private void ExitEdit()
	{
		if (!EditMode)
		{
			return;
		}
		EditMode = false;
		foreach (CardWindow w in _windows)
		{
			w.SetEdit(on: false);
		}
		HideGuides();
		_hint?.Close();
		_hint = null;
		SaveLayout();
		UpdateActivity();
	}

	public void Drag(CardWindow window, CardRect proposed)
	{
		IReadOnlyList<Display> displays = Display.All();
		Display display = displays.FirstOrDefault(d => d.WorkAreaDip.Contains(proposed.CenterX, proposed.CenterY)) ?? window.Display;
		CardRect area = Area(display);
		List<CardRect> others = (from w in _windows
			where w != window && w.Display.Name == display.Name
			select w.Rect).ToList();
		CardSnapResult snap = CardLayout.Snap(proposed, others, area, _settings.Grid);
		window.Display = display;
		window.Place(snap.Rect);
		ShowGuides(snap, display.WorkAreaDip);
	}

	public void EndDrag(CardWindow window)
	{
		HideGuides();
		(double x, double y) = CardLayout.ToFraction(window.Rect, Area(window.Display));
		CardSettings item = window.View.Settings;
		item.X = x;
		item.Y = y;
		item.Monitor = window.Display.Primary ? null : window.Display.Name;
		_dirty = true;
		SaveLayout();
	}

	private void ShowGuides(CardSnapResult snap, Rect area)
	{
		try
		{
			if (snap.GuideX is double gx)
			{
				_guideX ??= new CardGuideWindow(_theme, vertical: true);
				_guideX.ShowAt(gx, area);
			}
			else
			{
				_guideX?.Hide();
			}
			if (snap.GuideY is double gy)
			{
				_guideY ??= new CardGuideWindow(_theme, vertical: false);
				_guideY.ShowAt(gy, area);
			}
			else
			{
				_guideY?.Hide();
			}
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: направляющие", ex);
		}
	}

	private void HideGuides()
	{
		_guideX?.Hide();
		_guideY?.Hide();
	}

	public void OpenMenu(CardWindow? window, FrameworkElement target)
	{
		if (_disposed)
		{
			return;
		}
		try
		{
			(ContextMenu menu, Style itemStyle, Style separatorStyle) = CardMenu.Create(_theme);
			menu.PlacementTarget = target;
			if (EditMode)
			{
				MenuItem add = new MenuItem
				{
					Header = Loc.T("Добавить карточку"),
					Style = itemStyle
				};
				foreach (string kind in CardsSettings.Known)
				{
					string k = kind;
					add.Items.Add(Item(CardTheme.KindTitle(kind), delegate
					{
						AddCard(k, window?.Display);
					}));
				}
				menu.Items.Add(add);
				if (window != null)
				{
					menu.Items.Add(Item(Loc.T("Удалить"), delegate
					{
						RemoveCard(window);
					}));
				}
				menu.Items.Add(new Separator { Style = separatorStyle });
				menu.Items.Add(Item(Loc.T("Сбросить раскладку"), ResetLayout));
				menu.Items.Add(Item(Loc.T("Готово"), ExitEdit));
			}
			else
			{
				menu.Items.Add(Item(Loc.T("Редактировать рабочий стол"), EnterEdit));
			}
			if (window != null && window.View.Settings.Kind == "clock")
			{
				CardSettings item = window.View.Settings;
				bool on = item.Options.TryGetValue("seconds", out string? s) && s == "true";
				MenuItem seconds = Item(Loc.T("Секундная стрелка"), delegate
				{
					item.Options["seconds"] = on ? "false" : "true";
					_dirty = true;
					Recreate(window);
					SaveLayout();
				});
				seconds.IsChecked = on;
				menu.Items.Add(new Separator { Style = separatorStyle });
				menu.Items.Add(seconds);
			}
			menu.IsOpen = true;

			MenuItem Item(string header, Action action)
			{
				MenuItem mi = new MenuItem
				{
					Header = header,
					Style = itemStyle
				};
				mi.Click += delegate
				{
					try
					{
						action();
					}
					catch (Exception ex2)
					{
						Log.Error("Карточки: меню", ex2);
					}
				};
				return mi;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: контекстное меню", ex);
		}
	}

	private void AddCard(string kind, Display? display)
	{
		IReadOnlyList<Display> displays = Display.All();
		display ??= displays.FirstOrDefault() ?? Display.PrimaryDisplay;
		CardSettings item = new CardSettings
		{
			Kind = kind,
			Monitor = display.Primary ? null : display.Name
		};
		// Вторая заметка/список получает свой ключ данных, иначе тексты были бы общими.
		if ((kind == "note" || kind == "todo") && _items.Any(i => i.Kind == kind))
		{
			item.Options["id"] = Guid.NewGuid().ToString("N").Substring(0, 8);
		}
		(double w, double h) = _theme.SizeOf(item);
		CardRect area = Area(display);
		List<CardRect> others = (from x in _windows
			where x.Display.Name == display.Name
			select x.Rect).ToList();
		CardRect spot = CardLayout.FindFreeSpot(w, h, others, area, _settings.Grid);
		(item.X, item.Y) = CardLayout.ToFraction(spot, area);
		_items.Add(item);
		CreateWindow(item, 0, displays);
		_dirty = true;
		SaveLayout();
		UpdateActivity();
	}

	private void RemoveCard(CardWindow window)
	{
		_items.Remove(window.View.Settings);
		CloseWindow(window);
		_dirty = true;
		SaveLayout();
		UpdateActivity();
	}

	private void Recreate(CardWindow window)
	{
		CardSettings item = window.View.Settings;
		CloseWindow(window);
		CreateWindow(item, 0, Display.All());
		UpdateActivity();
	}

	private void ResetLayout()
	{
		_store.Reset(_profile.Id);
		_dirty = false;
		foreach (CardWindow w in _windows.ToList())
		{
			CloseWindow(w);
		}
		_items = CardLayoutStore.Effective(_settings, null);
		IReadOnlyList<Display> displays = Display.All();
		for (int i = 0; i < _items.Count; i++)
		{
			CreateWindow(_items[i], i, displays);
		}
		UpdateActivity();
	}

	private void CloseWindow(CardWindow window)
	{
		_windows.Remove(window);
		try
		{
			window.View.Dispose();
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: освобождение " + window.View.Settings.Kind, ex);
		}
		try
		{
			window.Close();
		}
		catch (Exception ex2)
		{
			Log.Error("Карточки: закрытие окна", ex2);
		}
	}

	private void SaveLayout()
	{
		if (_dirty)
		{
			_dirty = false;
			_store.Save(_profile.Id, _items);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		SaveLayout();
		_disposed = true;
		EditMode = false;
		DesktopActivity.Changed -= OnActivityChanged;
		_tick.Stop();
		_poll.Stop();
		try
		{
			_hint?.Close();
			_guideX?.Close();
			_guideY?.Close();
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: закрытие подсказок", ex);
		}
		_hint = null;
		_guideX = null;
		_guideY = null;
		foreach (CardWindow w in _windows.ToList())
		{
			CloseWindow(w);
		}
	}
}
