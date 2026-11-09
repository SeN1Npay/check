using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Dock;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Dock;

public class DockWindow : Window, IComponentConnector
{
	private DockBackdrop? _backdrop;

	private WindowPreviewWindow? _preview;

	private DockItem? _previewItem;

	private DockItem? _previewCandidate;

	private double _previewSince;

	private Popup? _stack;

	private bool _badges;

	private readonly DispatcherTimer _badgeTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(500.0)
	};

	private const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";

	private const double LabelGap = 10.0;

	private const double DotSize = 5.0;

	private const double DotGap = 3.0;

	private readonly DockSettings _s;

	private readonly DockPinStore _pinStore;

	private readonly Action _openMainWindow;

	private readonly WindowTracker _tracker = new WindowTracker();

	private readonly List<DockItem> _items = new List<DockItem>();

	private readonly List<DockPin> _pins;

	private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly uint _ownPid = (uint)Environment.ProcessId;

	private readonly Border _plate = new Border
	{
		IsHitTestVisible = false
	};

	private readonly System.Windows.Shapes.Rectangle _hit = new System.Windows.Shapes.Rectangle
	{
		Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0))
	};

	private readonly Border _label = new Border
	{
		IsHitTestVisible = false,
		Opacity = 0.0
	};

	private readonly TextBlock _labelText = new TextBlock
	{
		FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
		FontSize = 12.5
	};

	private readonly System.Windows.Media.Color _accent;

	private readonly System.Windows.Media.Color _accent2;

	private readonly System.Windows.Media.Color _foreground;

	private readonly System.Windows.Media.Color _background;

	private readonly System.Windows.Media.Brush _runningBrush;

	private readonly System.Windows.Media.Brush _activeBrush;

	private readonly System.Windows.Media.Brush _attentionBrush;

	private readonly System.Windows.Media.Brush _separatorBrush;

	private readonly System.Windows.Media.Brush _menuBackground;

	private readonly System.Windows.Media.Brush _menuForeground;

	private readonly System.Windows.Media.Brush _menuBorder;

	private readonly System.Windows.Media.Brush _menuHighlight;

	private readonly System.Windows.Media.Brush _menuSeparator;

	private readonly double _base;

	private readonly double _plateThickness;

	private readonly double _maxScale;

	private readonly double _sepExtent;

	private readonly double _crossExtent;

	private double _mainLength = 1920.0;

	private double _edgeOffset;

	private double _dpi = 1.0;

	private readonly DispatcherTimer _refreshSoon = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(80.0)
	};

	private readonly DispatcherTimer _refreshPeriodic = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(1200.0)
	};

	private readonly DispatcherTimer _poll = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(120.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private bool _animating;

	private double _lastFrame;

	private bool _hovering;

	private System.Windows.Point _pointer;

	private double? _pointerMain;

	private int _hoverIndex = -1;

	private double _lastHover;

	private bool _hideTarget;

	private double _hide;

	private bool _menuOpen;

	private bool _externalDrag;

	private DockItem? _pressItem;

	private System.Windows.Point _pressPoint;

	private DockItem? _dragItem;

	private bool _dragRemove;

	private bool _flipPending;

	private double _labelOpacity;

	private long _trashCount = -1L;

	private Rect _hitRect;

	private Rect _plateShown;

	private nint _hwnd;

	private HwndSource? _source;

	private uint _shellHookMessage;

	private bool _appBar;

	private readonly Display _display;

	private readonly ThemeProfile _profile;

	private AppBarHost? _appBarHost;

	internal Canvas Surface;

	private bool _contentLoaded;

	private bool Vertical => _s.Position != DockPosition.Bottom;

	private double LiftHeight
	{
		get
		{
			if (_s.HoverEffect != DockHoverEffect.Lift)
			{
				return 0.0;
			}
			return _base * 0.22;
		}
	}

	private double Now => _clock.Elapsed.TotalSeconds;

	private AppBarHost Host => _appBarHost ?? (_appBarHost = new AppBarHost(_hwnd, "dock:" + _display.Name, delegate
	{
		PositionWindow();
		return true;
	}));

	public event EventHandler? ExplorerRestarted;

	private void StartExtras()
	{
		if (_s.Blur && _s.Style != DockStyle.Minimal)
		{
			try
			{
				_backdrop = new DockBackdrop();
				_backdrop.Show();
				_backdrop.Adopt(_hwnd);
			}
			catch (Exception ex)
			{
				Log.Error("Док: размытие", ex);
				_backdrop = null;
			}
		}
		if (_s.ShowBadges && _profile.Taskbar.Mode == TaskbarMode.Hidden)
		{
			DockBadges.Acquire();
			_badges = true;
			_badgeTimer.Tick += delegate
			{
				UpdateBadges();
			};
			_badgeTimer.Start();
		}
	}

	private void StopExtras()
	{
		_badgeTimer.Stop();
		if (_badges)
		{
			DockBadges.Release();
		}
		_badges = false;
		ClosePreview();
		if (_stack != null)
		{
			_stack.IsOpen = false;
		}
		try
		{
			_backdrop?.Close();
		}
		catch (Exception ex)
		{
			Log.Error("Док: закрытие размытия", ex);
		}
		_backdrop = null;
	}

	private void UpdateBackdrop(Rect plate)
	{
		if (_backdrop == null)
		{
			return;
		}
		try
		{
			if (_hide > 0.98 || plate.Width < 1.0 || PresentationSource.FromVisual(Surface) == null)
			{
				_backdrop.Conceal();
				return;
			}
			System.Windows.Point point = Surface.PointToScreen(plate.TopLeft);
			System.Windows.Point point2 = Surface.PointToScreen(plate.BottomRight);
			System.Drawing.Rectangle rect = System.Drawing.Rectangle.FromLTRB((int)Math.Round(Math.Min(point.X, point2.X)), (int)Math.Round(Math.Min(point.Y, point2.Y)), (int)Math.Round(Math.Max(point.X, point2.X)), (int)Math.Round(Math.Max(point.Y, point2.Y)));
			rect.Intersect(_display.Bounds);
			int radius = (int)Math.Round(_plate.CornerRadius.TopLeft * _dpi);
			_backdrop.Follow(rect, radius);
		}
		catch (Exception ex)
		{
			Log.Warn("Док: размытие — " + ex.Message);
			_backdrop.Conceal();
		}
	}

	private void TrackPreview(double now)
	{
		if (!_s.Previews)
		{
			return;
		}
		DockItem dockItem = ((_hoverIndex >= 0 && _hoverIndex < _items.Count) ? _items[_hoverIndex] : null);
		if (dockItem != null && dockItem.Kind == DockItemKind.App && dockItem.IsRunning && !_menuOpen && _dragItem == null)
		{
			Popup stack = _stack;
			if (stack == null || !stack.IsOpen)
			{
				if (dockItem != _previewCandidate)
				{
					_previewCandidate = dockItem;
					_previewSince = now;
					if (_preview != null && _previewItem != dockItem)
					{
						ShowPreview(dockItem);
					}
				}
				else if (_previewItem != dockItem && now - _previewSince >= 0.5)
				{
					ShowPreview(dockItem);
				}
				return;
			}
		}
		_previewCandidate = null;
	}

	private void ShowPreview(DockItem item)
	{
		ClosePreview();
		try
		{
			List<TrackedWindow> list = (from w in item.Windows
				where Win32.IsWindow(w.Handle)
				orderby w.FirstSeen
				select w).ToList();
			if (list.Count == 0 || PresentationSource.FromVisual(item.Image) == null)
			{
				return;
			}
			System.Windows.Point point = item.Image.PointToScreen(new System.Windows.Point(0.0, 0.0));
			System.Windows.Point point2 = item.Image.PointToScreen(new System.Windows.Point(item.Image.ActualWidth, item.Image.ActualHeight));
			Rect anchorDip = new Rect(new System.Windows.Point(point.X / _dpi, point.Y / _dpi), new System.Windows.Point(point2.X / _dpi, point2.Y / _dpi));
			_preview = new WindowPreviewWindow(list, anchorDip, _s.Position, _profile.Palette, KeepPreviewOpen);
			_preview.Closed += delegate
			{
				if (_preview != null)
				{
					_ = _preview.IsLoaded;
				}
				_preview = null;
				_previewItem = null;
			};
			_previewItem = item;
			_preview.Show();
		}
		catch (Exception ex)
		{
			Log.Error("Док: превью окон", ex);
			_preview = null;
			_previewItem = null;
		}
	}

	private bool KeepPreviewOpen()
	{
		if (_previewItem == null || _menuOpen)
		{
			return false;
		}
		if (_hovering && _hoverIndex >= 0 && _hoverIndex < _items.Count && _items[_hoverIndex] == _previewItem)
		{
			return true;
		}
		return false;
	}

	private void ClosePreview()
	{
		WindowPreviewWindow preview = _preview;
		_preview = null;
		_previewItem = null;
		try
		{
			preview?.Close();
		}
		catch (Exception ex)
		{
			Log.Warn("Док: закрытие превью — " + ex.Message);
		}
	}

	private static bool IsFolderPin(DockItem item)
	{
		DockPin pin = item.Pin;
		if (pin != null && !pin.Path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
		{
			return Directory.Exists(pin.Path);
		}
		return false;
	}

	private void ShowStack(DockItem item)
	{
		ClosePreview();
		DockPin pin = item.Pin;
		if (pin == null)
		{
			return;
		}
		Popup stack = _stack;
		if (stack != null && stack.IsOpen)
		{
			_stack.IsOpen = false;
			return;
		}
		_menuOpen = true;
		_stack = FolderStack.Show(item.Image, pin.Path, _s.Position, _profile.Palette);
		_stack.Closed += delegate
		{
			_menuOpen = false;
			_lastHover = Now;
			StartAnimation();
		};
	}

	private static string? ExePathOf(DockItem item)
	{
		string text = item.Pin?.Target ?? item.Windows.FirstOrDefault()?.ExePath;
		if (text == null || !text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return text;
	}

	private void AddRecentFiles(ContextMenu menu, DockItem item, Action<string, Action, bool, bool> add, Action separator)
	{
		DockItem item2 = item;
		if (!_s.ShowRecentFiles)
		{
			return;
		}
		string text = ExePathOf(item2);
		if (text == null)
		{
			return;
		}
		IReadOnlyList<string> readOnlyList = RecentFiles.ForApp(text);
		if (readOnlyList.Count == 0)
		{
			return;
		}
		add(Loc.T("Недавние файлы"), delegate
		{
		}, arg3: false, arg4: true);
		foreach (string item3 in readOnlyList)
		{
			string text2 = System.IO.Path.GetFileName(item3);
			if (text2.Length > 50)
			{
				text2 = text2.Substring(0, 47) + "…";
			}
			string target = item3;
			add("    " + text2, delegate
			{
				Launch(item2, new string[1] { target });
			}, arg3: true, arg4: false);
		}
		separator();
	}

	private void UpdateBadges()
	{
		bool flag = false;
		foreach (DockItem item in _items)
		{
			if (item.Kind == DockItemKind.App)
			{
				BadgeInfo badgeInfo = (item.IsRunning ? DockBadges.For(item.Windows.Select((TrackedWindow w) => w.Handle)) : default(BadgeInfo));
				if (!(badgeInfo == item.Badge))
				{
					item.Badge = badgeInfo;
					flag = true;
					EnsureBadgeVisuals(item);
				}
			}
		}
		if (flag)
		{
			StartAnimation();
		}
	}

	private void EnsureBadgeVisuals(DockItem item)
	{
		if (item.OverlayImage == null)
		{
			item.OverlayImage = new System.Windows.Controls.Image
			{
				IsHitTestVisible = false,
				Stretch = Stretch.Uniform
			};
			Border obj = new Border
			{
				IsHitTestVisible = false,
				Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)),
				CornerRadius = new CornerRadius(2.0)
			};
			Border obj2 = new Border
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				CornerRadius = new CornerRadius(2.0)
			};
			Border child = obj2;
			item.ProgressFill = obj2;
			obj.Child = child;
			item.ProgressTrack = obj;
			InsertVisual(item.OverlayImage);
			InsertVisual(item.ProgressTrack);
		}
	}

	private void PlaceBadges(DockItem item, Rect icon)
	{
		if (item.OverlayImage != null && item.ProgressTrack != null && item.ProgressFill != null)
		{
			BadgeInfo badge = item.Badge;
			double num = Math.Max(12.0, icon.Width * 0.42);
			item.OverlayImage.Source = badge.Overlay;
			item.OverlayImage.Opacity = ((badge.Overlay == null) ? 0.0 : item.Appear);
			Place(item.OverlayImage, new Rect(icon.Right - num * 0.9, icon.Bottom - num * 0.9, num, num));
			bool flag = badge.Progress.HasValue || badge.Indeterminate;
			item.ProgressTrack.Opacity = (flag ? item.Appear : 0.0);
			if (flag)
			{
				double num2 = Math.Max(4.0, icon.Height * 0.1);
				Rect rect = new Rect(icon.Left + icon.Width * 0.1, icon.Bottom - num2 - icon.Height * 0.04, icon.Width * 0.8, num2);
				Place(item.ProgressTrack, rect);
				double value = (badge.Indeterminate ? (0.35 + 0.3 * Math.Sin(Now * 3.0)) : badge.Progress.GetValueOrDefault());
				item.ProgressFill.Width = Math.Max(0.0, rect.Width * Math.Clamp(value, 0.0, 1.0));
				item.ProgressFill.Background = (badge.Error ? _attentionBrush : (badge.Paused ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 226, 175)) : _activeBrush));
			}
		}
	}

	public DockWindow(ThemeProfile profile, DockPinStore pinStore, Action openMainWindow, Display? display = null)
	{
		InitializeComponent();
		_display = display ?? Display.PrimaryDisplay;
		_profile = profile;
		_s = profile.Dock;
		_pinStore = pinStore;
		_openMainWindow = openMainWindow;
		Palette palette = profile.Palette;
		_accent = BrushFactory.ToColor(palette.Accent);
		_accent2 = BrushFactory.ToColor(palette.Accent2);
		_foreground = BrushFactory.ToColor(palette.Foreground);
		_background = BrushFactory.ToColor(_s.Background ?? palette.Background);
		_runningBrush = BrushFactory.Solid(palette.Foreground, 0.7);
		_activeBrush = BrushFactory.Solid(palette.Accent);
		_attentionBrush = BrushFactory.Solid(palette.Red);
		_separatorBrush = BrushFactory.Solid(palette.Foreground, 0.28);
		_menuBackground = BrushFactory.Solid(palette.Surface, 0.98);
		_menuForeground = BrushFactory.Solid(palette.Foreground);
		_menuBorder = BrushFactory.Solid(palette.Muted, 0.8);
		_menuHighlight = BrushFactory.Solid(palette.Accent, 0.28);
		_menuSeparator = BrushFactory.Solid(palette.Muted, 0.7);
		_base = _s.IconSize;
		_plateThickness = _base + (double)(2 * _s.Padding);
		_maxScale = _s.HoverEffect switch
		{
			DockHoverEffect.Magnify => Math.Max(1.0, _s.Magnification), 
			DockHoverEffect.Lift => 1.12, 
			_ => 1.0, 
		};
		_sepExtent = Math.Max(2.0, (double)_s.Spacing * 0.5 + 2.0);
		double num = Math.Max(_base * (_maxScale - 1.0) + LiftHeight, _base * 0.55);
		int num2 = (Vertical ? 260 : 44);
		_crossExtent = Math.Ceiling((double)_s.Margin + _plateThickness + num + (double)num2 + 8.0);
		BuildVisuals(palette);
		_pins = Dedupe(pinStore.Load(ImportDefaultPins));
		_tracker.Changed += delegate
		{
			RebuildItems();
		};
		_refreshSoon.Tick += delegate
		{
			_refreshSoon.Stop();
			SafeRefresh();
		};
		_refreshPeriodic.Tick += delegate
		{
			SafeRefresh();
			UpdateTrash(force: false);
		};
		_poll.Tick += delegate
		{
			Poll();
		};
		Surface.MouseEnter += delegate
		{
			StartAnimation();
		};
		Surface.MouseMove += OnMouseMove;
		Surface.MouseLeftButtonDown += OnLeftDown;
		Surface.MouseLeftButtonUp += OnLeftUp;
		Surface.MouseRightButtonUp += OnRightUp;
		Surface.MouseDown += OnMouseDown;
		Surface.MouseWheel += OnMouseWheel;
		Surface.AllowDrop = true;
		Surface.DragEnter += OnDragOver;
		Surface.DragOver += OnDragOver;
		Surface.DragLeave += delegate
		{
			_externalDrag = false;
		};
		Surface.Drop += OnDrop;
		base.Loaded += delegate
		{
			PositionWindow();
			SafeRefresh();
			RebuildItems();
			UpdateTrash(force: true);
			_lastHover = Now + 1.5;
			_refreshPeriodic.Start();
			_poll.Start();
			StartExtras();
			StartAnimation();
		};
	}

	private void BuildVisuals(Palette palette)
	{
		double num = Math.Clamp(_s.Opacity, 0.0, 1.0);
		string borderColor = _s.BorderColor;
		System.Windows.Media.Color color = ((borderColor != null) ? BrushFactory.ToColor(borderColor) : WithAlpha(_foreground, 0.16));
		_plate.CornerRadius = new CornerRadius((_s.Style == DockStyle.Pill) ? (_plateThickness / 2.0) : ((double)_s.CornerRadius));
		switch (_s.Style)
		{
		case DockStyle.Glass:
		case DockStyle.Pill:
		{
			System.Windows.Media.Color startColor = WithAlpha(Mix(_background, Colors.White, 0.1), num * 0.92);
			System.Windows.Media.Color endColor = WithAlpha(_background, num);
			var (startPoint, endPoint) = _s.Position switch
			{
				DockPosition.Left => (new System.Windows.Point(1.0, 0.0), new System.Windows.Point(0.0, 0.0)), 
				DockPosition.Right => (new System.Windows.Point(0.0, 0.0), new System.Windows.Point(1.0, 0.0)), 
				_ => (new System.Windows.Point(0.0, 0.0), new System.Windows.Point(0.0, 1.0)), 
			};
			_plate.Background = new LinearGradientBrush(startColor, endColor, startPoint, endPoint);
			_plate.BorderBrush = new SolidColorBrush(color);
			_plate.BorderThickness = new Thickness(1.0);
			_plate.Effect = new DropShadowEffect
			{
				BlurRadius = 22.0,
				ShadowDepth = 3.0,
				Direction = 270.0,
				Opacity = 0.35,
				Color = Colors.Black
			};
			break;
		}
		case DockStyle.Flat:
			_plate.Background = new SolidColorBrush(WithAlpha(_background, num));
			_plate.BorderBrush = new SolidColorBrush(WithAlpha(BrushFactory.ToColor(palette.Muted), 0.45));
			_plate.BorderThickness = new Thickness(1.0);
			break;
		default:
			_plate.Background = null;
			_plate.BorderThickness = new Thickness(0.0);
			break;
		}
		_labelText.Foreground = BrushFactory.Solid(palette.Foreground);
		_label.Child = _labelText;
		_label.CornerRadius = new CornerRadius(7.0);
		_label.Padding = new Thickness(10.0, 4.0, 10.0, 5.0);
		_label.Background = BrushFactory.Solid(palette.Surface, 0.96);
		_label.BorderBrush = BrushFactory.Solid(palette.Muted, 0.7);
		_label.BorderThickness = new Thickness(1.0);
		Surface.Children.Add(_plate);
		Surface.Children.Add(_label);
		Surface.Children.Add(_hit);
	}

	private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color color, double alpha)
	{
		return System.Windows.Media.Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0), color.R, color.G, color.B);
	}

	private static System.Windows.Media.Color Mix(System.Windows.Media.Color a, System.Windows.Media.Color b, double t)
	{
		return System.Windows.Media.Color.FromRgb((byte)Math.Round((double)(int)a.R + (double)(b.R - a.R) * t), (byte)Math.Round((double)(int)a.G + (double)(b.G - a.G) * t), (byte)Math.Round((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	private void InsertVisual(UIElement element)
	{
		Surface.Children.Insert(Surface.Children.IndexOf(_label), element);
	}

	private DockItem CreateItem(DockItemKind kind, string key)
	{
		DockItem dockItem = new DockItem(kind, key);
		if (kind == DockItemKind.Separator)
		{
			dockItem.Line = new System.Windows.Shapes.Rectangle
			{
				Fill = _separatorBrush,
				IsHitTestVisible = false,
				RadiusX = 0.75,
				RadiusY = 0.75
			};
			InsertVisual(dockItem.Line);
			return dockItem;
		}
		dockItem.Indicators.Orientation = (Vertical ? Orientation.Vertical : Orientation.Horizontal);
		InsertVisual(dockItem.Image);
		InsertVisual(dockItem.Indicators);
		switch (kind)
		{
		case DockItemKind.Apps:
			dockItem.Name = Loc.T("Приложения");
			dockItem.Image.Source = DockIcons.AppsButton(_accent, _accent2, System.Windows.Media.Color.FromArgb(235, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			dockItem.IconRequested = true;
			break;
		case DockItemKind.Trash:
			dockItem.Name = Loc.T("Корзина");
			break;
		}
		return dockItem;
	}

	private void RemoveItemAt(int index)
	{
		DockItem dockItem = _items[index];
		Surface.Children.Remove(dockItem.Image);
		Surface.Children.Remove(dockItem.Indicators);
		if (dockItem.Line != null)
		{
			Surface.Children.Remove(dockItem.Line);
		}
		_items.RemoveAt(index);
		if (_dragItem == dockItem)
		{
			_dragItem = null;
		}
		if (_pressItem == dockItem)
		{
			_pressItem = null;
		}
	}

	private void SafeRefresh()
	{
		try
		{
			_tracker.Refresh();
		}
		catch (Exception ex)
		{
			Log.Error("Док: обновление списка окон", ex);
		}
	}

	private void ScheduleRefresh()
	{
		_refreshSoon.Stop();
		_refreshSoon.Start();
	}

	private static List<DockPin> Dedupe(IEnumerable<DockPin> pins)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		return pins.Where((DockPin p) => seen.Add(p.Path)).ToList();
	}

	private static IEnumerable<DockPin> ImportDefaultPins()
	{
		List<DockPin> list = DockApps.ImportTaskbarPins(DockApps.TaskbarPinsFolder, (string lnk) => (Target: ShellIcons.TryResolveShortcut(lnk).Target, AppId: ShellIcons.TryGetFileAppId(lnk))).ToList();
		if (list.Count == 0)
		{
			string text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
			list.Add(new DockPin
			{
				Path = text,
				Target = text,
				Name = Loc.T("Проводник")
			});
		}
		Log.Info($"Док: импортировано закреплений — {list.Count}");
		return list;
	}

	private void SavePins()
	{
		_pinStore.Save(_pins);
	}

	private void RebuildItems()
	{
		nint foreground = _tracker.Foreground;
		List<List<TrackedWindow>> list = _pins.Select((DockPin _) => new List<TrackedWindow>()).ToList();
		List<(string, List<TrackedWindow>)> list2 = new List<(string, List<TrackedWindow>)>();
		foreach (TrackedWindow window in _tracker.Windows.OrderBy((TrackedWindow w) => w.FirstSeen))
		{
			int num = _pins.FindIndex((DockPin p) => DockApps.Matches(p, window.AppId, window.ExePath));
			if (num >= 0)
			{
				list[num].Add(window);
				continue;
			}
			string key2 = window.AppKey;
			int num2 = list2.FindIndex(((string Key, List<TrackedWindow> Windows) r) => r.Key == key2);
			if (num2 >= 0)
			{
				list2[num2].Item2.Add(window);
				continue;
			}
			list2.Add((key2, new List<TrackedWindow> { window }));
		}
		List<DockItem> list3 = new List<DockItem>();
		HashSet<string> used = new HashSet<string>();
		if (_s.ShowAppsButton)
		{
			list3.Add(Get(DockItemKind.Apps, "apps"));
		}
		int count = list3.Count;
		for (int j = 0; j < _pins.Count; j++)
		{
			string text = "pin:" + _pins[j].Path.ToLowerInvariant();
			if (!used.Contains(text))
			{
				DockItem dockItem = Get(DockItemKind.App, text);
				dockItem.Pin = _pins[j];
				Assign(dockItem, list[j], foreground);
				list3.Add(dockItem);
			}
		}
		if (_s.ShowRunningApps)
		{
			foreach (var item3 in list2)
			{
				string item = item3.Item1;
				List<TrackedWindow> item2 = item3.Item2;
				DockItem dockItem2 = Get(DockItemKind.App, "run:" + item);
				dockItem2.Pin = null;
				Assign(dockItem2, item2, foreground);
				list3.Add(dockItem2);
			}
		}
		if (_s.ShowAppsButton && list3.Count > count)
		{
			list3.Insert(count, Get(DockItemKind.Separator, "sep:apps"));
		}
		if (_s.ShowTrash)
		{
			if (list3.Count > 0)
			{
				list3.Add(Get(DockItemKind.Separator, "sep:trash"));
			}
			list3.Add(Get(DockItemKind.Trash, "trash"));
		}
		HashSet<DockItem> hashSet = new HashSet<DockItem>(list3);
		List<DockItem> list4 = new List<DockItem>(list3);
		for (int k = 0; k < _items.Count; k++)
		{
			DockItem dockItem3 = _items[k];
			if (hashSet.Contains(dockItem3))
			{
				continue;
			}
			dockItem3.Removing = true;
			int index = 0;
			for (int num3 = k - 1; num3 >= 0; num3--)
			{
				int num4 = list4.IndexOf(_items[num3]);
				if (num4 >= 0)
				{
					index = num4 + 1;
					break;
				}
			}
			list4.Insert(index, dockItem3);
		}
		foreach (DockItem item4 in list3)
		{
			item4.Removing = false;
			if (!item4.IconRequested)
			{
				RequestIcon(item4);
			}
		}
		_items.Clear();
		_items.AddRange(list4);
		StartAnimation();
		DockItem Get(DockItemKind kind, string key)
		{
			string key3 = key;
			used.Add(key3);
			return _items.FirstOrDefault((DockItem i) => i.Key == key3 && i.Kind == kind) ?? CreateItem(kind, key3);
		}
	}

	private void Assign(DockItem item, List<TrackedWindow> windows, nint foreground)
	{
		bool attention = item.Attention;
		item.Windows.Clear();
		item.Windows.AddRange(windows);
		item.IsActive = windows.Any((TrackedWindow w) => w.Handle == foreground);
		item.Attention = windows.Any((TrackedWindow w) => w.IsFlashing);
		item.Name = ((!string.IsNullOrWhiteSpace(item.Pin?.Name)) ? item.Pin.Name : ((windows.Count > 0) ? NameFor(windows[0]) : item.Name));
		if (item.Attention && !attention && _s.BounceOnAttention)
		{
			StartBounce(item, loop: false, 3, decay: true);
		}
		if (item.BounceLoop && item.IsRunning)
		{
			FinishBounce(item);
		}
		UpdateIndicators(item);
	}

	private string NameFor(TrackedWindow window)
	{
		if (_names.TryGetValue(window.AppKey, out string value))
		{
			return value;
		}
		string text = null;
		if (window.IsUwp)
		{
			string appId = window.AppId;
			if (appId != null)
			{
				text = ShellIcons.TryGetDisplayName("shell:AppsFolder\\" + appId);
			}
		}
		if (text == null && !window.IsUwp && !string.IsNullOrEmpty(window.ExePath))
		{
			try
			{
				string fileDescription = FileVersionInfo.GetVersionInfo(window.ExePath).FileDescription;
				if (!string.IsNullOrWhiteSpace(fileDescription))
				{
					text = fileDescription.Trim();
				}
			}
			catch
			{
			}
			if (text == null)
			{
				text = System.IO.Path.GetFileNameWithoutExtension(window.ExePath);
			}
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return window.Title;
		}
		_names[window.AppKey] = text;
		return text;
	}

	private void RequestIcon(DockItem item)
	{
		DockItem item2 = item;
		if (item2.Kind != DockItemKind.Separator)
		{
			item2.IconRequested = true;
			base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				LoadIcon(item2, useCache: true);
			});
		}
	}

	private void LoadIcon(DockItem item, bool useCache)
	{
		int pixelSize = DockIcons.PixelSize(_base, _maxScale, _dpi);
		ImageSource imageSource = null;
		try
		{
			switch (item.Kind)
			{
			case DockItemKind.Trash:
				imageSource = DockIcons.FromShell("::{645FF040-5081-101B-9F08-00AA002F954E}", pixelSize, useCache) ?? DockIcons.Trash(_foreground, _accent);
				break;
			case DockItemKind.App:
			{
				DockPin pin = item.Pin;
				if (pin != null)
				{
					imageSource = DockIcons.FromShell(pin.Path, pixelSize, useCache);
					break;
				}
				TrackedWindow trackedWindow = item.Windows.FirstOrDefault();
				if (trackedWindow == null)
				{
					break;
				}
				if (trackedWindow.IsUwp)
				{
					string appId = trackedWindow.AppId;
					if (appId != null)
					{
						imageSource = DockIcons.FromShell("shell:AppsFolder\\" + appId, pixelSize, useCache);
					}
				}
				if (imageSource == null && !trackedWindow.IsUwp && !string.IsNullOrEmpty(trackedWindow.ExePath))
				{
					imageSource = DockIcons.FromShell(trackedWindow.ExePath, pixelSize, useCache);
				}
				if (imageSource == null)
				{
					string appId2 = trackedWindow.AppId;
					if (appId2 != null)
					{
						imageSource = DockIcons.FromShell("shell:AppsFolder\\" + appId2, pixelSize, useCache);
					}
				}
				if (imageSource == null)
				{
					imageSource = DockIcons.FromHIcon(WindowActions.GetWindowIcon(trackedWindow.Handle));
				}
				break;
			}
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Док: иконка «" + item.Name + "»: " + ex.Message);
		}
		item.Image.Source = imageSource ?? DockIcons.Placeholder(item.Name, _accent2, _background);
	}

	private void UpdateTrash(bool force)
	{
		if (!_s.ShowTrash)
		{
			return;
		}
		long num;
		try
		{
			num = ShellIcons.RecycleBinItemCount();
		}
		catch
		{
			return;
		}
		bool num2 = force || _trashCount < 0 || num == 0 != (_trashCount == 0);
		_trashCount = num;
		if (num2)
		{
			DockItem dockItem = _items.FirstOrDefault((DockItem i) => i.Kind == DockItemKind.Trash);
			if (dockItem != null)
			{
				LoadIcon(dockItem, useCache: false);
			}
		}
	}

	private void UpdateIndicators(DockItem item)
	{
		int num = ((item.Kind == DockItemKind.App && item.Windows.Count != 0) ? (_s.Indicator switch
		{
			DockIndicator.None => 0, 
			DockIndicator.Dots => Math.Min(3, item.Windows.Count), 
			_ => 1, 
		}) : 0);
		int num2 = num;
		System.Windows.Media.Brush fill = (item.Attention ? _attentionBrush : (item.IsActive ? _activeBrush : _runningBrush));
		item.Indicators.Children.Clear();
		item.IndicatorCount = num2;
		if (num2 == 0)
		{
			return;
		}
		if (_s.Indicator == DockIndicator.Line)
		{
			var (num3, num4) = IndicatorSize(item);
			item.Indicators.Children.Add(new System.Windows.Shapes.Rectangle
			{
				Fill = fill,
				RadiusX = 1.5,
				RadiusY = 1.5,
				Width = (Vertical ? num4 : num3),
				Height = (Vertical ? num3 : num4)
			});
		}
		else
		{
			for (int i = 0; i < num2; i++)
			{
				item.Indicators.Children.Add(new Ellipse
				{
					Fill = fill,
					Width = 5.0,
					Height = 5.0,
					Margin = ((i == 0) ? new Thickness(0.0) : (Vertical ? new Thickness(0.0, 3.0, 0.0, 0.0) : new Thickness(3.0, 0.0, 0.0, 0.0)))
				});
			}
		}
	}

	private (double Length, double Thickness) IndicatorSize(DockItem item)
	{
		if (_s.Indicator != DockIndicator.Line)
		{
			return (Length: (double)item.IndicatorCount * 5.0 + (double)Math.Max(0, item.IndicatorCount - 1) * 3.0, Thickness: 5.0);
		}
		return (Length: Math.Round(_base * 0.32), Thickness: 3.0);
	}

	private void StartAnimation()
	{
		if (!_animating)
		{
			_animating = true;
			_lastFrame = Now;
			CompositionTarget.Rendering += OnFrame;
		}
	}

	private void StopAnimation()
	{
		if (_animating)
		{
			_animating = false;
			CompositionTarget.Rendering -= OnFrame;
		}
	}

	private void OnFrame(object? sender, EventArgs e)
	{
		double now = Now;
		double dt = Math.Clamp(now - _lastFrame, 0.0, 0.05);
		_lastFrame = now;
		bool flag;
		try
		{
			flag = Step(now, dt);
		}
		catch (Exception ex)
		{
			Log.Error("Док: кадр анимации", ex);
			flag = false;
		}
		if (!flag)
		{
			StopAnimation();
		}
	}

	private double BaseExtent(DockItem item)
	{
		if (item.Kind != DockItemKind.Separator)
		{
			return _base;
		}
		return _sepExtent;
	}

	private bool Step(double now, double dt)
	{
		double animationSpeed = _s.AnimationSpeed;
		bool flag = false;
		UpdatePointer();
		if (_hovering)
		{
			_lastHover = now;
		}
		for (int num = _items.Count - 1; num >= 0; num--)
		{
			if (_items[num].Removing && _items[num].Appear <= 0.01)
			{
				RemoveItemAt(num);
			}
		}
		int count = _items.Count;
		double num2 = _mainLength / 2.0;
		double[] array = new double[count];
		bool[] array2 = new bool[count];
		for (int i = 0; i < count; i++)
		{
			DockItem dockItem = _items[i];
			array[i] = (BaseExtent(dockItem) + (double)_s.Spacing) * dockItem.Appear;
			array2[i] = dockItem.Kind != DockItemKind.Separator && dockItem != _dragItem;
		}
		double total;
		double[] array3 = DockLayout.ArrangeSlots(array, num2, out total);
		double[] array4 = new double[count];
		for (int j = 0; j < count; j++)
		{
			array4[j] = array3[j] + array[j] / 2.0;
		}
		bool flag2 = _s.HoverEffect == DockHoverEffect.Magnify && _hovering && _dragItem == null;
		double[] array5 = DockLayout.TargetScales(array4, array2, flag2 ? _pointerMain : null, _maxScale, _s.MagnifyRange * (_base + (double)_s.Spacing));
		int num3 = ((_s.HoverEffect == DockHoverEffect.Lift && _hovering && _dragItem == null) ? _hoverIndex : (-1));
		if (num3 >= 0 && num3 < count)
		{
			array5[num3] = _maxScale;
		}
		double[] array6 = new double[count];
		double[] array7 = new double[count];
		for (int k = 0; k < count; k++)
		{
			DockItem dockItem2 = _items[k];
			int num4 = ((!dockItem2.Removing) ? 1 : 0);
			double num5 = ((k == num3) ? LiftHeight : 0.0);
			dockItem2.Scale = DockLayout.Approach(dockItem2.Scale, array5[k], dt, 16.0 * animationSpeed);
			dockItem2.Lift = DockLayout.Approach(dockItem2.Lift, num5, dt, 16.0 * animationSpeed);
			dockItem2.Appear = DockLayout.Approach(dockItem2.Appear, num4, dt, 9.0 * animationSpeed);
			dockItem2.MainOffset = DockLayout.Approach(dockItem2.MainOffset, 0.0, dt, 12.0 * animationSpeed);
			flag |= dockItem2.Scale != array5[k] || dockItem2.Lift != num5 || dockItem2.Appear != (double)num4 || dockItem2.MainOffset != 0.0;
			double num6 = BaseExtent(dockItem2) * ((dockItem2.Kind == DockItemKind.Separator) ? 1.0 : dockItem2.Scale);
			array7[k] = num6 * dockItem2.Appear;
			array6[k] = (num6 + (double)_s.Spacing) * dockItem2.Appear;
		}
		double total2;
		double[] array8 = DockLayout.ArrangeSlots(array6, num2, out total2);
		if (_flipPending)
		{
			for (int l = 0; l < count; l++)
			{
				if (!double.IsNaN(_items[l].LastStart))
				{
					_items[l].MainOffset = _items[l].LastStart - array8[l];
				}
			}
			_flipPending = false;
			flag = true;
		}
		double num7 = (_hideTarget ? 1.0 : 0.0);
		_hide = DockLayout.Approach(_hide, num7, dt, 8.0 * animationSpeed);
		flag |= _hide != num7;
		double num8 = _hide * ((double)_s.Margin + _plateThickness + 10.0);
		double num9 = (double)_s.Margin - num8;
		double num10;
		double num11;
		if (count > 0)
		{
			num10 = num2 - total2 / 2.0 + (double)(_s.Spacing / 2) - (double)_s.Padding;
			num11 = Math.Max(0.0, total2 - (double)_s.Spacing) + (double)(2 * _s.Padding);
		}
		else
		{
			num11 = _base + (double)(2 * _s.Padding);
			num10 = num2 - num11 / 2.0;
		}
		Place(_plate, ToRect(num10, num11, num9, _plateThickness));
		UpdateBackdrop(ToRect(num10, num11, num9, _plateThickness));
		_plateShown = ToRect(num10, num11, _s.Margin, _plateThickness);
		for (int m = 0; m < count; m++)
		{
			DockItem dockItem3 = _items[m];
			double num12 = array8[m] + dockItem3.MainOffset;
			double num13 = num12 + (double)_s.Spacing * dockItem3.Appear / 2.0;
			double num14 = array7[m];
			dockItem3.LastStart = num12;
			dockItem3.LastSlot = array6[m];
			dockItem3.LastSize = num14;
			if (dockItem3.Kind == DockItemKind.Separator)
			{
				System.Windows.Shapes.Rectangle line = dockItem3.Line;
				if (line != null)
				{
					double num15 = _base * 0.7 * dockItem3.Appear;
					Place(line, ToRect(num13 + (num14 - 1.5) / 2.0, 1.5, num9 + (_plateThickness - num15) / 2.0, num15));
					line.Opacity = dockItem3.Appear;
					continue;
				}
			}
			double num16 = BounceValue(dockItem3, now);
			if (dockItem3.BounceStart >= 0.0)
			{
				flag = true;
			}
			double crossStart = num9 + (double)_s.Padding + dockItem3.Lift + num16 * _base * 0.5;
			if (dockItem3 == _dragItem)
			{
				double? pointerMain = _pointerMain;
				if (pointerMain.HasValue)
				{
					double valueOrDefault = pointerMain.GetValueOrDefault();
					num13 = valueOrDefault - num14 / 2.0;
					crossStart = Math.Max(num9 + (double)_s.Padding, CrossOf(_pointer) - num14 / 2.0);
				}
			}
			Place(dockItem3.Image, ToRect(num13, num14, crossStart, num14));
			dockItem3.Image.Opacity = dockItem3.Appear * ((dockItem3 == _dragItem && _dragRemove) ? 0.45 : 1.0);
			if (dockItem3.OverlayImage != null)
			{
				PlaceBadges(dockItem3, ToRect(num13, num14, crossStart, num14));
				if (dockItem3.Badge.Indeterminate)
				{
					flag = true;
				}
			}
			if (dockItem3.IndicatorCount > 0)
			{
				(double Length, double Thickness) tuple = IndicatorSize(dockItem3);
				double item = tuple.Length;
				double item2 = tuple.Thickness;
				double num17 = num13 + num14 / 2.0;
				double crossStart2 = num9 + Math.Max(1.5, ((double)_s.Padding - item2) / 2.0);
				Place(dockItem3.Indicators, ToRect(num17 - item / 2.0, item, crossStart2, item2));
				dockItem3.Indicators.Opacity = ((dockItem3 == _dragItem) ? 0.0 : dockItem3.Appear);
			}
			else
			{
				dockItem3.Indicators.Opacity = 0.0;
			}
		}
		int hoverIndex;
		if (_hovering)
		{
			double? pointerMain = _pointerMain;
			if (pointerMain.HasValue)
			{
				double valueOrDefault2 = pointerMain.GetValueOrDefault();
				hoverIndex = IndexAtMain(valueOrDefault2);
				goto IL_086b;
			}
		}
		hoverIndex = -1;
		goto IL_086b;
		IL_086b:
		_hoverIndex = hoverIndex;
		bool flag3 = _hovering || _dragItem != null || _externalDrag;
		double num18 = (flag3 ? ((double)_s.Margin + _plateThickness + _base * (_maxScale - 1.0) + LiftHeight + 14.0) : ((double)_s.Margin + _plateThickness));
		double num19 = (flag3 ? (_base * 0.5) : 0.0);
		_hitRect = ToRect(num10 - num19, num11 + 2.0 * num19, 0.0, Math.Max(0.0, num18 - num8));
		_hit.Visibility = ((_hide > 0.98) ? Visibility.Collapsed : Visibility.Visible);
		Place(_hit, _hitRect);
		flag |= UpdateLabel(dt, animationSpeed, num9);
		flag |= flag3 || _menuOpen;
		TrackPreview(now);
		return flag;
	}

	private double BounceValue(DockItem item, double now)
	{
		if (item.BounceStart < 0.0)
		{
			return 0.0;
		}
		double num = 0.6 / _s.AnimationSpeed;
		double num2 = now - item.BounceStart;
		if (item.BounceLoop)
		{
			if (num2 < 10.0)
			{
				return Math.Abs(Math.Sin(Math.PI * num2 / num));
			}
			FinishBounce(item);
		}
		if (num2 >= num * (double)item.BounceCount)
		{
			item.BounceStart = -1.0;
			return 0.0;
		}
		if (!item.BounceDecay)
		{
			return Math.Abs(Math.Sin(Math.PI * num2 / num));
		}
		return DockLayout.Bounce(num2, num, item.BounceCount);
	}

	private void StartBounce(DockItem item, bool loop, int count, bool decay)
	{
		if (!(item.BounceStart >= 0.0) || !item.BounceLoop)
		{
			item.BounceStart = Now;
			item.BounceLoop = loop;
			item.BounceCount = count;
			item.BounceDecay = decay;
			StartAnimation();
		}
	}

	private void FinishBounce(DockItem item)
	{
		if (!(item.BounceStart < 0.0))
		{
			double num = 0.6 / _s.AnimationSpeed;
			item.BounceLoop = false;
			item.BounceDecay = false;
			item.BounceCount = Math.Max(1, (int)Math.Ceiling((Now - item.BounceStart) / num));
		}
	}

	private bool UpdateLabel(double dt, double speed, double crossBase)
	{
		DockItem dockItem = null;
		string text = null;
		if (_dragItem != null)
		{
			if (_dragRemove)
			{
				dockItem = _dragItem;
				text = Loc.T("Открепить");
			}
		}
		else if (_s.ShowLabels && _hovering && !_menuOpen && _hoverIndex >= 0 && _hoverIndex < _items.Count)
		{
			dockItem = _items[_hoverIndex];
			text = dockItem.Name;
		}
		int num;
		double width;
		double height;
		double lastSize;
		double num2;
		if (dockItem != null)
		{
			num = ((!string.IsNullOrWhiteSpace(text)) ? 1 : 0);
			if (num != 0)
			{
				if (_labelText.Text != text)
				{
					_labelText.Text = text;
				}
				_label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
				width = _label.DesiredSize.Width;
				height = _label.DesiredSize.Height;
				lastSize = dockItem.LastSize;
				if (dockItem == _dragItem)
				{
					double? pointerMain = _pointerMain;
					if (pointerMain.HasValue)
					{
						double valueOrDefault = pointerMain.GetValueOrDefault();
						num2 = valueOrDefault - lastSize / 2.0;
						goto IL_015d;
					}
				}
				num2 = dockItem.LastStart + (double)_s.Spacing * dockItem.Appear / 2.0;
				goto IL_015d;
			}
		}
		else
		{
			num = 0;
		}
		goto IL_024c;
		IL_024c:
		double num3 = ((num != 0) ? 1.0 : 0.0);
		_labelOpacity = DockLayout.Approach(_labelOpacity, num3, dt, 14.0 * speed);
		_label.Opacity = _labelOpacity;
		return _labelOpacity != num3;
		IL_015d:
		double num4 = (Vertical ? height : width);
		double crossLength = (Vertical ? width : height);
		double mainStart = Math.Clamp(num2 + lastSize / 2.0 - num4 / 2.0, 4.0, Math.Max(4.0, _mainLength - num4 - 4.0));
		double crossStart = ((dockItem == _dragItem) ? (CrossOf(_pointer) + lastSize / 2.0 + 10.0) : (crossBase + (double)_s.Padding + dockItem.Lift + lastSize + 10.0));
		Rect rect = ToRect(mainStart, num4, crossStart, crossLength);
		Canvas.SetLeft(_label, rect.X);
		Canvas.SetTop(_label, rect.Y);
		goto IL_024c;
	}

	private Rect ToRect(double mainStart, double mainLength, double crossStart, double crossLength)
	{
		mainLength = Math.Max(0.0, mainLength);
		crossLength = Math.Max(0.0, crossLength);
		return _s.Position switch
		{
			DockPosition.Left => new Rect(crossStart, mainStart, crossLength, mainLength), 
			DockPosition.Right => new Rect(_crossExtent - crossStart - crossLength, mainStart, crossLength, mainLength), 
			_ => new Rect(mainStart, _crossExtent - crossStart - crossLength, mainLength, crossLength), 
		};
	}

	private static void Place(FrameworkElement element, Rect rect)
	{
		Canvas.SetLeft(element, rect.X);
		Canvas.SetTop(element, rect.Y);
		element.Width = rect.Width;
		element.Height = rect.Height;
	}

	private double MainOf(System.Windows.Point p)
	{
		if (!Vertical)
		{
			return p.X;
		}
		return p.Y;
	}

	private double CrossOf(System.Windows.Point p)
	{
		return _s.Position switch
		{
			DockPosition.Left => p.X, 
			DockPosition.Right => _crossExtent - p.X, 
			_ => _crossExtent - p.Y, 
		};
	}

	private int IndexAtMain(double main)
	{
		for (int i = 0; i < _items.Count; i++)
		{
			DockItem dockItem = _items[i];
			if (dockItem.IsInteractive && !double.IsNaN(dockItem.LastStart) && main >= dockItem.LastStart && main < dockItem.LastStart + dockItem.LastSlot)
			{
				return i;
			}
		}
		return -1;
	}

	private DockItem? ItemAt(System.Windows.Point p)
	{
		if (!_hitRect.Contains(p))
		{
			return null;
		}
		int num = IndexAtMain(MainOf(p));
		if (num < 0)
		{
			return null;
		}
		return _items[num];
	}

	private void UpdatePointer()
	{
		if (_menuOpen)
		{
			return;
		}
		ShellNative.POINT point;
		if (PresentationSource.FromVisual(Surface) == null)
		{
			_hovering = false;
			_pointerMain = null;
		}
		else if (ShellNative.GetCursorPos(out point))
		{
			_pointer = Surface.PointFromScreen(new System.Windows.Point(point.X, point.Y));
			bool flag = _hide < 0.5 && _hitRect.Contains(_pointer);
			_hovering = flag || _dragItem != null;
			_pointerMain = (_hovering ? new double?(MainOf(_pointer)) : null);
			if (_dragItem != null)
			{
				UpdateDrag();
			}
		}
	}

	private void Poll()
	{
		try
		{
			double now = Now;
			nint foregroundWindow = NativeMethods.GetForegroundWindow();
			bool flag;
			if (!IsOwnWindow(foregroundWindow) && WindowActions.IsFullscreen(foregroundWindow) && !ShellNative.IsZoomed(foregroundWindow))
			{
				flag = true;
			}
			else
			{
				if (CursorAtEdge())
				{
					_lastHover = now;
				}
				bool flag2 = _hovering || _menuOpen || _dragItem != null || _externalDrag || now - _lastHover < 0.8;
				flag = _s.Behavior switch
				{
					DockBehavior.AutoHide => !flag2, 
					DockBehavior.DodgeWindows => !flag2 && ForegroundOverlaps(foregroundWindow), 
					_ => false, 
				};
			}
			if (flag != _hideTarget)
			{
				_hideTarget = flag;
				StartAnimation();
			}
			if (!_animating && PresentationSource.FromVisual(Surface) != null && ShellNative.GetCursorPos(out var point) && _hitRect.Contains(Surface.PointFromScreen(new System.Windows.Point(point.X, point.Y))))
			{
				StartAnimation();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Док: " + ex.Message);
		}
	}

	private bool IsOwnWindow(nint hwnd)
	{
		if (hwnd == IntPtr.Zero)
		{
			return true;
		}
		NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
		return processId == _ownPid;
	}

	private bool CursorAtEdge()
	{
		if (!ShellNative.GetCursorPos(out var point))
		{
			return false;
		}
		System.Drawing.Rectangle bounds = _display.Bounds;
		int num = (int)Math.Round(_edgeOffset * _dpi);
		return _s.Position switch
		{
			DockPosition.Left => point.X >= bounds.Left + num && point.X <= bounds.Left + num + 2 && point.Y >= bounds.Top && point.Y < bounds.Bottom, 
			DockPosition.Right => point.X <= bounds.Right - 1 - num && point.X >= bounds.Right - 3 - num && point.Y >= bounds.Top && point.Y < bounds.Bottom, 
			_ => point.Y <= bounds.Bottom - 1 - num && point.Y >= bounds.Bottom - 3 - num && point.X >= bounds.Left && point.X < bounds.Right, 
		};
	}

	private bool ForegroundOverlaps(nint hwnd)
	{
		if (IsOwnWindow(hwnd) || ShellNative.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
		{
			return false;
		}
		if (_tracker.Windows.All((TrackedWindow w) => w.Handle != hwnd))
		{
			return false;
		}
		if (!ShellNative.GetWindowRect(hwnd, out var rect))
		{
			return false;
		}
		if (PresentationSource.FromVisual(Surface) == null)
		{
			return false;
		}
		System.Windows.Point point = Surface.PointToScreen(_plateShown.TopLeft);
		System.Windows.Point point2 = Surface.PointToScreen(_plateShown.BottomRight);
		if ((double)rect.Left < Math.Max(point.X, point2.X) - 6.0 && (double)rect.Right > Math.Min(point.X, point2.X) + 6.0 && (double)rect.Top < Math.Max(point.Y, point2.Y) - 6.0)
		{
			return (double)rect.Bottom > Math.Min(point.Y, point2.Y) + 6.0;
		}
		return false;
	}

	private void PositionWindow()
	{
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource?.CompositionTarget != null)
		{
			_dpi = presentationSource.CompositionTarget.TransformToDevice.M11;
		}
		System.Drawing.Rectangle bounds = _display.Bounds;
		double num = (double)bounds.X / _dpi;
		double num2 = (double)bounds.Y / _dpi;
		double num3 = (double)bounds.Width / _dpi;
		double num4 = (double)bounds.Height / _dpi;
		_mainLength = (Vertical ? num4 : num3);
		_edgeOffset = (double)(_appBar ? UpdateAppBar() : TaskbarOffsetPx()) / _dpi;
		switch (_s.Position)
		{
		case DockPosition.Left:
			base.Left = num + _edgeOffset;
			base.Top = num2;
			base.Width = _crossExtent;
			base.Height = num4;
			break;
		case DockPosition.Right:
			base.Left = num + num3 - _crossExtent - _edgeOffset;
			base.Top = num2;
			base.Width = _crossExtent;
			base.Height = num4;
			break;
		default:
			base.Left = num;
			base.Top = num2 + num4 - _crossExtent - _edgeOffset;
			base.Width = num3;
			base.Height = _crossExtent;
			break;
		}
		StartAnimation();
	}

	private int TaskbarOffsetPx()
	{
		NativeMethods.RECT? visibleTaskbarRect = TaskbarController.GetVisibleTaskbarRect();
		if (visibleTaskbarRect.HasValue)
		{
			NativeMethods.RECT valueOrDefault = visibleTaskbarRect.GetValueOrDefault();
			System.Drawing.Rectangle bounds = _display.Bounds;
			if (!_display.Contains((valueOrDefault.Left + valueOrDefault.Right) / 2, (valueOrDefault.Top + valueOrDefault.Bottom) / 2))
			{
				return 0;
			}
			bool flag = valueOrDefault.Right - valueOrDefault.Left > valueOrDefault.Bottom - valueOrDefault.Top;
			switch (_s.Position)
			{
			case DockPosition.Left:
				if (!flag && valueOrDefault.Right < bounds.Left + bounds.Width / 2)
				{
					return Math.Max(0, valueOrDefault.Right - bounds.Left);
				}
				break;
			case DockPosition.Right:
				if (!flag && valueOrDefault.Left > bounds.Left + bounds.Width / 2)
				{
					return Math.Max(0, bounds.Right - valueOrDefault.Left);
				}
				break;
			case DockPosition.Bottom:
				if (flag && valueOrDefault.Top > bounds.Top + bounds.Height / 2)
				{
					return Math.Max(0, bounds.Bottom - valueOrDefault.Top);
				}
				break;
			}
			return 0;
		}
		return 0;
	}

	private void RegisterAppBar()
	{
		Host.Enabled = true;
		_appBar = Host.Register();
	}

	private int UpdateAppBar()
	{
		if (!_appBar)
		{
			return 0;
		}
		int num = (int)Math.Round(((double)_s.Margin + _plateThickness) * _dpi);
		System.Drawing.Rectangle bounds = _display.Bounds;
		NativeMethods.APPBARDATA aPPBARDATA = NativeMethods.APPBARDATA.Create(_hwnd);
		switch (_s.Position)
		{
		case DockPosition.Left:
			aPPBARDATA.uEdge = 0u;
			aPPBARDATA.rc = new NativeMethods.RECT
			{
				Left = bounds.Left,
				Top = bounds.Top,
				Right = bounds.Left + num,
				Bottom = bounds.Bottom
			};
			break;
		case DockPosition.Right:
			aPPBARDATA.uEdge = 2u;
			aPPBARDATA.rc = new NativeMethods.RECT
			{
				Left = bounds.Right - num,
				Top = bounds.Top,
				Right = bounds.Right,
				Bottom = bounds.Bottom
			};
			break;
		default:
			aPPBARDATA.uEdge = 3u;
			aPPBARDATA.rc = new NativeMethods.RECT
			{
				Left = bounds.Left,
				Top = bounds.Bottom - num,
				Right = bounds.Right,
				Bottom = bounds.Bottom
			};
			break;
		}
		NativeMethods.RECT rECT = Host.Reserve(aPPBARDATA.uEdge, aPPBARDATA.rc);
		return _s.Position switch
		{
			DockPosition.Left => Math.Max(0, rECT.Left - bounds.Left), 
			DockPosition.Right => Math.Max(0, bounds.Right - rECT.Right), 
			_ => Math.Max(0, bounds.Bottom - rECT.Bottom), 
		};
	}

	private void RemoveAppBar()
	{
		if (_appBarHost != null)
		{
			_appBarHost.Enabled = false;
		}
		_appBarHost?.Remove();
		_appBar = false;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		_source = HwndSource.FromHwnd(_hwnd);
		_source?.AddHook(WndProc);
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		_shellHookMessage = NativeMethods.RegisterWindowMessage("SHELLHOOK");
		if (!ShellNative.RegisterShellHookWindow(_hwnd))
		{
			Log.Warn("Док: RegisterShellHookWindow не сработал, работаем по опросу.");
		}
		Host.Enabled = _s.Behavior == DockBehavior.AlwaysVisible;
		if (Host.Enabled)
		{
			RegisterAppBar();
		}
		PositionWindow();
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		try
		{
			if (msg == (int)_shellHookMessage && _shellHookMessage != 0)
			{
				if (_tracker.OnShellMessage(((IntPtr)wParam).ToInt32(), lParam))
				{
					ScheduleRefresh();
				}
			}
			else if (_appBarHost != null && _appBarHost.HandleMessage(msg, wParam, lParam, _display, delegate
			{
				_appBar = _appBarHost.Registered;
				ScheduleRefresh();
				this.ExplorerRestarted?.Invoke(this, EventArgs.Empty);
			}))
			{
				handled = true;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Док: сообщение окна", ex);
		}
		return IntPtr.Zero;
	}

	protected override void OnClosed(EventArgs e)
	{
		StopExtras();
		_refreshSoon.Stop();
		_refreshPeriodic.Stop();
		_poll.Stop();
		StopAnimation();
		if (_hwnd != IntPtr.Zero)
		{
			ShellNative.DeregisterShellHookWindow(_hwnd);
		}
		RemoveAppBar();
		_source?.RemoveHook(WndProc);
		base.OnClosed(e);
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		StartAnimation();
		DockItem pressItem = _pressItem;
		if (pressItem != null && pressItem.Kind == DockItemKind.App && pressItem.Pin != null && _dragItem == null && e.LeftButton == MouseButtonState.Pressed && (e.GetPosition(Surface) - _pressPoint).Length > 8.0)
		{
			_dragItem = _pressItem;
			_dragRemove = false;
		}
	}

	private void OnLeftDown(object sender, MouseButtonEventArgs e)
	{
		System.Windows.Point position = e.GetPosition(Surface);
		_pressItem = ItemAt(position);
		_pressPoint = position;
		if (_pressItem != null)
		{
			Surface.CaptureMouse();
		}
		e.Handled = true;
	}

	private void OnLeftUp(object sender, MouseButtonEventArgs e)
	{
		System.Windows.Point position = e.GetPosition(Surface);
		DockItem pressed = _pressItem;
		_pressItem = null;
		if (Surface.IsMouseCaptured)
		{
			Surface.ReleaseMouseCapture();
		}
		e.Handled = true;
		if (_dragItem != null)
		{
			EndDrag();
		}
		else if (pressed != null && ItemAt(position) == pressed)
		{
			Safe(delegate
			{
				OnItemClick(pressed);
			});
		}
	}

	private void OnRightUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (_dragItem == null)
		{
			OpenMenu(ItemAt(e.GetPosition(Surface)));
		}
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Middle)
		{
			return;
		}
		e.Handled = true;
		DockItem item = ItemAt(e.GetPosition(Surface));
		if (item != null && item.Kind == DockItemKind.App)
		{
			Safe(delegate
			{
				Launch(item);
			});
		}
	}

	private void OnMouseWheel(object sender, MouseWheelEventArgs e)
	{
		e.Handled = true;
		DockItem dockItem = ItemAt(e.GetPosition(Surface));
		if (dockItem != null && dockItem.Kind == DockItemKind.App && dockItem.Windows.Count != 0)
		{
			List<TrackedWindow> windows = dockItem.Windows.OrderBy((TrackedWindow w) => w.FirstSeen).ToList();
			int num = windows.FindIndex((TrackedWindow w) => w.Handle == NativeMethods.GetForegroundWindow());
			int next = ((num >= 0) ? ((num + ((e.Delta < 0) ? 1 : (-1)) + windows.Count) % windows.Count) : 0);
			Safe(delegate
			{
				WindowActions.Activate(windows[next].Handle);
			});
		}
	}

	private void UpdateDrag()
	{
		DockPin dockPin = _dragItem?.Pin;
		if (dockPin == null)
		{
			return;
		}
		_dragRemove = CrossOf(_pointer) > (double)_s.Margin + _plateThickness + _base * 1.2;
		if (!_dragRemove)
		{
			int num = DockLayout.InsertIndex((from i in _items.Where((DockItem i) => i.Pin != null && !i.Removing && i != _dragItem && !double.IsNaN(i.LastStart)).ToList()
				select i.LastStart + i.LastSlot / 2.0).ToList(), MainOf(_pointer));
			int num2 = _pins.IndexOf(dockPin);
			if (num2 >= 0 && num != num2)
			{
				_pins.RemoveAt(num2);
				_pins.Insert(Math.Clamp(num, 0, _pins.Count), dockPin);
				_flipPending = true;
				RebuildItems();
			}
		}
	}

	private void EndDrag()
	{
		DockItem dragItem = _dragItem;
		bool dragRemove = _dragRemove;
		_dragItem = null;
		_dragRemove = false;
		if (dragItem != null)
		{
			if (dragRemove)
			{
				Unpin(dragItem);
			}
			else
			{
				SavePins();
			}
			StartAnimation();
		}
	}

	private void OnItemClick(DockItem item)
	{
		switch (item.Kind)
		{
		case DockItemKind.Apps:
			WindowActions.OpenStartMenu();
			break;
		case DockItemKind.Trash:
			StartProcess("explorer.exe", "shell:RecycleBinFolder");
			break;
		case DockItemKind.App:
			if (IsFolderPin(item))
			{
				ShowStack(item);
				break;
			}
			ClosePreview();
			ActivateApp(item);
			break;
		case DockItemKind.Separator:
			break;
		}
	}

	private void ActivateApp(DockItem item)
	{
		List<TrackedWindow> list = item.Windows.Where((TrackedWindow w) => ShellNative.IsWindow(w.Handle)).ToList();
		if (list.Count == 0)
		{
			Launch(item);
			return;
		}
		nint foreground = NativeMethods.GetForegroundWindow();
		if (list.Count == 1)
		{
			TrackedWindow trackedWindow = list[0];
			if (trackedWindow.Handle == foreground && !ShellNative.IsIconic(trackedWindow.Handle))
			{
				WindowActions.Minimize(trackedWindow.Handle);
			}
			else
			{
				WindowActions.Activate(trackedWindow.Handle);
			}
			return;
		}
		List<TrackedWindow> list2 = list.OrderBy((TrackedWindow w) => w.FirstSeen).ToList();
		int num = list2.FindIndex((TrackedWindow w) => w.Handle == foreground);
		WindowActions.Activate(((num < 0) ? list.OrderByDescending((TrackedWindow w) => w.LastActive).First() : list2[(num + 1) % list2.Count]).Handle);
	}

	private void Launch(DockItem item, IReadOnlyList<string>? files = null)
	{
		string text = ((files != null && files.Count > 0) ? string.Join(" ", files.Select((string f) => "\"" + f + "\"")) : null);
		bool isRunning = item.IsRunning;
		try
		{
			DockPin pin = item.Pin;
			if (pin != null)
			{
				if (pin.Path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
				{
					StartProcess("explorer.exe", pin.Path);
				}
				else
				{
					if (text != null && pin.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
					{
						(string, string) tuple = ShellIcons.TryResolveShortcut(pin.Path);
						var (text2, _) = tuple;
						if (text2 != null && File.Exists(text2))
						{
							StartProcess(text2, Join(tuple.Item2, text));
							goto IL_0157;
						}
					}
					bool flag = pin.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
					StartProcess(pin.Path, flag ? Join(pin.Arguments, text) : text);
				}
			}
			else
			{
				TrackedWindow trackedWindow = item.Windows.FirstOrDefault();
				if (trackedWindow != null)
				{
					if (trackedWindow.IsUwp)
					{
						string appId = trackedWindow.AppId;
						if (appId != null)
						{
							StartProcess("explorer.exe", "shell:AppsFolder\\" + appId);
							goto IL_0157;
						}
					}
					if (!string.IsNullOrEmpty(trackedWindow.ExePath))
					{
						StartProcess(trackedWindow.ExePath, text);
					}
				}
			}
			goto IL_0157;
			IL_0157:
			if (_s.BounceOnLaunch && !isRunning)
			{
				StartBounce(item, loop: true, 0, decay: false);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Док: не удалось запустить «" + item.Name + "»", ex);
		}
	}

	private static string? Join(string? a, string? b)
	{
		if (!string.IsNullOrWhiteSpace(a))
		{
			if (!string.IsNullOrWhiteSpace(b))
			{
				return a + " " + b;
			}
			return a;
		}
		return b;
	}

	private static void StartProcess(string file, string? arguments)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(file)
		{
			UseShellExecute = true,
			Arguments = (arguments ?? "")
		};
		if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file))
		{
			processStartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(file) ?? "";
		}
		Process.Start(processStartInfo)?.Dispose();
	}

	private void Pin(DockItem item)
	{
		if (item.Pin != null)
		{
			return;
		}
		TrackedWindow trackedWindow = item.Windows.FirstOrDefault();
		if (trackedWindow != null)
		{
			DockPin pin = DockApps.PinFor(item.Name, trackedWindow.AppId, trackedWindow.ExePath);
			if (!_pins.Any((DockPin p) => string.Equals(p.Path, pin.Path, StringComparison.OrdinalIgnoreCase)))
			{
				_pins.Add(pin);
				SavePins();
				item.Key = "pin:" + pin.Path.ToLowerInvariant();
				item.Pin = pin;
				_flipPending = true;
				RebuildItems();
			}
		}
	}

	private void Unpin(DockItem item)
	{
		DockPin pin = item.Pin;
		if (pin != null)
		{
			_pins.Remove(pin);
			SavePins();
			if (item.IsRunning)
			{
				item.Key = "run:" + item.Windows[0].AppKey;
				item.Pin = null;
			}
			_flipPending = true;
			RebuildItems();
		}
	}

	private void AddPinsFromFiles(IEnumerable<string> files)
	{
		bool flag = false;
		foreach (string file in files)
		{
			string text = System.IO.Path.GetExtension(file).ToLowerInvariant();
			if (_pins.Any((DockPin p) => string.Equals(p.Path, file, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			if (Directory.Exists(file))
			{
				string fileName = System.IO.Path.GetFileName(file.TrimEnd('\\'));
				_pins.Add(new DockPin
				{
					Path = file,
					Name = ((fileName.Length > 0) ? fileName : file)
				});
				flag = true;
			}
			else
			{
				if ((!(text == ".exe") && !(text == ".lnk")) || 1 == 0)
				{
					continue;
				}
				DockPin dockPin = new DockPin
				{
					Path = file,
					Name = System.IO.Path.GetFileNameWithoutExtension(file)
				};
				if (text == ".lnk")
				{
					dockPin.Target = ShellIcons.TryResolveShortcut(file).Target;
					dockPin.AppId = ShellIcons.TryGetFileAppId(file);
				}
				else
				{
					dockPin.Target = file;
					try
					{
						string fileDescription = FileVersionInfo.GetVersionInfo(file).FileDescription;
						if (!string.IsNullOrWhiteSpace(fileDescription))
						{
							dockPin.Name = fileDescription.Trim();
						}
					}
					catch
					{
					}
				}
				_pins.Add(dockPin);
				flag = true;
			}
		}
		if (flag)
		{
			SavePins();
			RebuildItems();
		}
	}

	private void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error("Док: действие", ex);
		}
	}

	private static string[] DroppedFiles(DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop) || !(e.Data.GetData(DataFormats.FileDrop) is string[] result))
		{
			return Array.Empty<string>();
		}
		return result;
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		_externalDrag = true;
		StartAnimation();
		string[] array = DroppedFiles(e);
		DockItem dockItem = ItemAt(e.GetPosition(Surface));
		DragDropEffects effects = ((array.Length != 0) ? (dockItem?.Kind switch
		{
			DockItemKind.Trash => DragDropEffects.Move, 
			DockItemKind.App => DragDropEffects.Copy, 
			_ => array.Any((string f) => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || Directory.Exists(f)) ? DragDropEffects.Link : DragDropEffects.None, 
		}) : DragDropEffects.None);
		e.Effects = effects;
		e.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		_externalDrag = false;
		string[] files = DroppedFiles(e);
		if (files.Length == 0)
		{
			return;
		}
		DockItem item = ItemAt(e.GetPosition(Surface));
		e.Handled = true;
		Safe(delegate
		{
			switch (item?.Kind)
			{
			case DockItemKind.Trash:
				RecycleBinService.Recycle(files);
				UpdateTrash(force: false);
				break;
			case DockItemKind.App:
				Launch(item, files);
				break;
			default:
				AddPinsFromFiles(files);
				break;
			}
		});
	}

	private void OpenMenu(DockItem? item)
	{
		DockItem item2 = item;
		ContextMenu menu = new ContextMenu
		{
			Style = (Style)FindResource("DockMenuStyle"),
			Background = _menuBackground,
			Foreground = _menuForeground,
			BorderBrush = _menuBorder,
			Placement = PlacementMode.MousePoint,
			PlacementTarget = Surface
		};
		menu.Resources["Dock.MenuHighlight"] = _menuHighlight;
		menu.Resources["Dock.MenuSeparator"] = _menuSeparator;
		Style itemStyle = (Style)FindResource("DockMenuItemStyle");
		Style separatorStyle = (Style)FindResource("DockMenuSeparatorStyle");
		switch (item2?.Kind)
		{
		case DockItemKind.App:
		{
			nint foregroundWindow = NativeMethods.GetForegroundWindow();
			if (item2.Windows.Count > 0)
			{
				foreach (TrackedWindow item3 in item2.Windows.OrderBy((TrackedWindow w) => w.FirstSeen))
				{
					string text = (string.IsNullOrWhiteSpace(item3.Title) ? item2.Name : item3.Title);
					if (text.Length > 60)
					{
						text = text.Substring(0, 57) + "…";
					}
					nint handle = item3.Handle;
					Add(text, delegate
					{
						WindowActions.Activate(handle);
					}, enabled: true, handle == foregroundWindow);
				}
				Separator();
			}
			if (!IsFolderPin(item2))
			{
				AddRecentFiles(menu, item2, delegate(string h, Action a, bool en, bool b)
				{
					Add(h, a, en, b);
				}, Separator);
			}
			Add(IsFolderPin(item2) ? "Открыть папку" : (item2.IsRunning ? "Новое окно" : "Открыть"), delegate
			{
				Launch(item2);
			});
			if (item2.Pin != null)
			{
				Add("Открепить от дока", delegate
				{
					Unpin(item2);
				});
			}
			else
			{
				Add("Закрепить в доке", delegate
				{
					Pin(item2);
				});
			}
			if (!item2.IsRunning)
			{
				break;
			}
			int count = item2.Windows.Count;
			Add((count > 1) ? Loc.F("Закрыть все окна ({0})", count) : "Закрыть окно", delegate
			{
				item2.Windows.ToList().ForEach(delegate(TrackedWindow w)
				{
					WindowActions.Close(w.Handle);
				});
			});
			break;
		}
		case DockItemKind.Apps:
			Add("Открыть «Пуск»", WindowActions.OpenStartMenu);
			Add("Все приложения", delegate
			{
				StartProcess("explorer.exe", "shell:AppsFolder");
			});
			break;
		case DockItemKind.Trash:
			Add("Открыть корзину", delegate
			{
				StartProcess("explorer.exe", "shell:RecycleBinFolder");
			});
			Add("Очистить корзину", delegate
			{
				ShellNative.SHEmptyRecycleBin(_hwnd, null, 0u);
				UpdateTrash(force: true);
			}, _trashCount != 0);
			break;
		}
		if (menu.Items.Count > 0)
		{
			Separator();
		}
		Add("Настройки Metamorph", _openMainWindow);
		menu.Closed += delegate
		{
			_menuOpen = false;
			_lastHover = Now;
			StartAnimation();
		};
		_menuOpen = true;
		menu.IsOpen = true;
		StartAnimation();
		void Add(string header, Action action, bool enabled = true, bool bold = false)
		{
			Action action2 = action;
			MenuItem menuItem = new MenuItem
			{
				Header = Loc.T(header),
				IsEnabled = enabled,
				Style = itemStyle
			};
			if (bold)
			{
				menuItem.FontWeight = FontWeights.SemiBold;
			}
			menuItem.Click += delegate
			{
				Safe(action2);
			};
			menu.Items.Add(menuItem);
		}
		void Separator()
		{
			menu.Items.Add(new Separator
			{
				Style = separatorStyle
			});
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Metamorph;component/dock/dockwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			Surface = (Canvas)target;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
