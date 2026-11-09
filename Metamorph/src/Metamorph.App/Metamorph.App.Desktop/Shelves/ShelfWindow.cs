using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Metamorph.App.Dock;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Dock;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Shelves;

// Одна полка: стеклянная панель на слое рабочего стола со значками файлов своей папки.
internal sealed class ShelfWindow : Window
{
	private struct WINDOWPOS
	{
		public nint hwnd;

		public nint hwndInsertAfter;

		public int x;

		public int y;

		public int cx;

		public int cy;

		public uint flags;
	}

	private sealed record Entry(string Path, string Name, bool IsDirectory, DateTime Stamp);

	private const int WM_WINDOWPOSCHANGING = 70;

	private const uint SwpKeep = 19u;

	private const double TitleHeight = 30.0;

	private static readonly nint HwndBottom = new IntPtr(1);

	private static readonly nint HwndTop = IntPtr.Zero;

	private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff" };

	private readonly ShelvesController _owner;

	private readonly ShelfSettings _shelf;

	private readonly string _key;

	private readonly string _folder;

	private readonly ShelfRule? _rule;

	private readonly int _iconSize;

	private readonly Brush _fg;

	private readonly Brush _muted;

	private readonly Brush _hover;

	private readonly Brush _border;

	private readonly Brush _accent;

	private readonly Border _frame;

	private readonly WrapPanel _items = new WrapPanel();

	private readonly ScrollViewer _scroll;

	private readonly TextBlock _hint;

	private readonly TextBlock _count;

	private readonly TextBlock _chevron;

	private readonly Border _grip;

	private readonly DispatcherTimer _refresh = new DispatcherTimer(DispatcherPriority.Background)
	{
		Interval = TimeSpan.FromMilliseconds(400.0)
	};

	private readonly Dictionary<string, (DateTime Stamp, ImageSource? Icon)> _icons = new Dictionary<string, (DateTime, ImageSource?)>(StringComparer.OrdinalIgnoreCase);

	private Display _display;

	private FileSystemWatcher? _watcher;

	private nint _hwnd;

	private bool _front;

	private bool _collapsed;

	private double _fullHeight;

	private Point? _resizeFrom;

	private Size _resizeStart;

	private Point? _dragFrom;

	private int _iconGeneration;

	static ShelfWindow()
	{
		Loc.AddEnglish(("Открыть", "Open"), ("Показать в проводнике", "Show in Explorer"), ("Открыть папку", "Open folder"), ("Разложить рабочий стол по правилу", "Tidy desktop by rule"), ("Свернуть", "Collapse"), ("Развернуть", "Expand"), ("Перетащите сюда файлы", "Drop files here"), ("Папка не найдена", "Folder not found"), ("Полка", "Shelf"));
	}

	public ShelfWindow(ShelvesController owner, ShelvesSettings all, ShelfSettings shelf, string key, string folder, Palette palette)
	{
		_owner = owner;
		_shelf = shelf;
		_key = key;
		_folder = folder;
		_rule = ShelfRule.Parse(shelf.Rule);
		_iconSize = Math.Clamp(all.IconSize, 16, 128);
		_fg = BrushFactory.Solid(palette.Foreground);
		_muted = BrushFactory.Solid(palette.Muted);
		_hover = BrushFactory.Solid(palette.Foreground, 0.12);
		_border = BrushFactory.Solid(palette.Foreground, 0.16);
		_accent = BrushFactory.Solid(palette.Accent);
		base.Title = "Metamorph Shelf";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.Focusable = false;
		base.Foreground = _fg;
		base.AllowDrop = true;
		ShelfState? saved = owner.Saved(key);
		_display = FindDisplay(saved?.Monitor) ?? FindDisplay(shelf.Monitor) ?? Display.PrimaryDisplay;
		ShelfRect work = WorkOf(_display);
		ShelfRect rect = ((saved != null) ? ShelfLayout.FromFractions(saved.X, saved.Y, saved.Width, saved.Height, work) : ShelfLayout.FromFractions(shelf.X, shelf.Y, shelf.Width, shelf.Height, work));
		base.Left = rect.X;
		base.Top = rect.Y;
		base.Width = rect.Width;
		base.Height = rect.Height;
		_fullHeight = rect.Height;
		_collapsed = saved?.Collapsed ?? shelf.Collapsed;
		TextBlock title = new TextBlock
		{
			Text = (string.IsNullOrWhiteSpace(shelf.Title) ? Loc.T("Полка") : shelf.Title),
			Foreground = _fg,
			FontSize = 12.5,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		_count = new TextBlock
		{
			Foreground = _muted,
			FontSize = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		_chevron = new TextBlock
		{
			Foreground = _muted,
			FontSize = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		DockPanel header = new DockPanel
		{
			LastChildFill = true,
			Margin = new Thickness(12.0, 0.0, 10.0, 0.0)
		};
		DockPanel.SetDock(_chevron, System.Windows.Controls.Dock.Left);
		DockPanel.SetDock(_count, System.Windows.Controls.Dock.Right);
		header.Children.Add(_chevron);
		header.Children.Add(_count);
		header.Children.Add(title);
		Border titleBar = new Border
		{
			Height = TitleHeight,
			Background = BrushFactory.Solid(palette.Foreground, 0.06),
			CornerRadius = new CornerRadius(10.0, 10.0, 0.0, 0.0),
			Child = header,
			Cursor = Cursors.SizeAll
		};
		titleBar.MouseLeftButtonDown += OnTitleDown;
		titleBar.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ShowTitleMenu();
		};
		_scroll = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Padding = new Thickness(6.0, 4.0, 6.0, 8.0),
			Content = _items,
			Focusable = false
		};
		_hint = new TextBlock
		{
			Foreground = _muted,
			FontSize = 12.0,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(12.0),
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		_grip = new Border
		{
			Width = 16.0,
			Height = 16.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Background = Brushes.Transparent,
			Cursor = Cursors.SizeNWSE,
			Child = new TextBlock
			{
				Text = "◢",
				FontSize = 9.0,
				Foreground = _muted,
				Opacity = 0.7,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Margin = new Thickness(0.0, 0.0, 3.0, 2.0)
			}
		};
		_grip.MouseLeftButtonDown += OnGripDown;
		_grip.MouseMove += OnGripMove;
		_grip.MouseLeftButtonUp += OnGripUp;
		Grid body = new Grid();
		body.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(TitleHeight)
		});
		body.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		Grid.SetRow(_scroll, 1);
		Grid.SetRow(_hint, 1);
		Grid.SetRow(_grip, 1);
		body.Children.Add(titleBar);
		body.Children.Add(_scroll);
		body.Children.Add(_hint);
		body.Children.Add(_grip);
		_frame = new Border
		{
			Child = body,
			CornerRadius = new CornerRadius(10.0),
			Background = BrushFactory.Solid(palette.Surface, Math.Clamp(all.Opacity, 0.05, 1.0)),
			BorderBrush = _border,
			BorderThickness = new Thickness(1.0),
			VerticalAlignment = VerticalAlignment.Top,
			ClipToBounds = true,
			Height = (_collapsed ? TitleHeight : _fullHeight)
		};
		base.Content = _frame;
		ApplyCollapsedChrome();
		_refresh.Tick += delegate
		{
			_refresh.Stop();
			Refresh();
		};
		base.DragEnter += OnDragOver;
		base.DragOver += OnDragOver;
		base.DragLeave += delegate
		{
			_frame.BorderBrush = _border;
		};
		base.Drop += OnDrop;
		base.Loaded += delegate
		{
			EnsureFolder();
			Refresh();
			StartWatcher();
		};
	}

	private static Display? FindDisplay(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		IReadOnlyList<Display> all = Display.All();
		if (int.TryParse(name, out var index) && index >= 1 && index <= all.Count)
		{
			return all[index - 1];
		}
		return all.FirstOrDefault((Display d) => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || d.Name.EndsWith(name, StringComparison.OrdinalIgnoreCase));
	}

	private static ShelfRect WorkOf(Display display)
	{
		Rect r = display.WorkAreaDip;
		return new ShelfRect(r.X, r.Y, r.Width, r.Height);
	}

	private void EnsureFolder()
	{
		try
		{
			if (!Directory.Exists(_folder) && _owner.IsDefaultFolder(_folder))
			{
				Directory.CreateDirectory(_folder);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Полки: папка " + _folder, ex);
		}
	}

	// ---- Содержимое ----

	private void StartWatcher()
	{
		if (!Directory.Exists(_folder))
		{
			return;
		}
		try
		{
			_watcher = new FileSystemWatcher(_folder)
			{
				IncludeSubdirectories = false,
				NotifyFilter = (NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.LastWrite)
			};
			_watcher.Created += OnFolderChanged;
			_watcher.Deleted += OnFolderChanged;
			_watcher.Renamed += OnFolderChanged;
			_watcher.Changed += OnFolderChanged;
			_watcher.Error += delegate(object _, ErrorEventArgs e)
			{
				Log.Warn("Полки: наблюдение за " + _folder + ": " + e.GetException().Message);
				OnFolderChanged(this, EventArgs.Empty);
			};
			_watcher.EnableRaisingEvents = true;
		}
		catch (Exception ex)
		{
			Log.Error("Полки: наблюдение за " + _folder, ex);
		}
	}

	// Пачка событий от копирования многих файлов → одно обновление через 400 мс тишины.
	private void OnFolderChanged(object? sender, EventArgs e)
	{
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
		{
			_refresh.Stop();
			_refresh.Start();
		});
	}

	private List<Entry> ReadEntries()
	{
		List<Entry> list = new List<Entry>();
		DirectoryInfo dir = new DirectoryInfo(_folder);
		foreach (FileSystemInfo info in dir.EnumerateFileSystemInfos())
		{
			if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && !ShelfFiles.IsSystemName(info.Name))
			{
				bool isDirectory = (info.Attributes & FileAttributes.Directory) != 0;
				string ext = Path.GetExtension(info.Name);
				bool hideExt = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
				list.Add(new Entry(info.FullName, hideExt ? Path.GetFileNameWithoutExtension(info.Name) : info.Name, isDirectory, info.LastWriteTimeUtc));
			}
		}
		return (from e in list
			orderby e.IsDirectory descending, e.Name
			select e).ToList();
	}

	private void Refresh()
	{
		List<Entry> entries;
		try
		{
			if (!Directory.Exists(_folder))
			{
				_items.Children.Clear();
				_count.Text = "";
				ShowHint(Loc.T("Папка не найдена") + "\n" + _folder);
				return;
			}
			entries = ReadEntries();
		}
		catch (Exception ex)
		{
			Log.Error("Полки: чтение " + _folder, ex);
			return;
		}
		_items.Children.Clear();
		foreach (Entry entry in entries)
		{
			_items.Children.Add(BuildItem(entry));
		}
		_count.Text = ((entries.Count > 0) ? entries.Count.ToString() : "");
		ShowHint((entries.Count == 0) ? Loc.T("Перетащите сюда файлы") : null);
		LoadIcons(entries);
	}

	private void ShowHint(string? text)
	{
		_hint.Text = text ?? "";
		_hint.Visibility = ((text == null || _collapsed) ? Visibility.Collapsed : Visibility.Visible);
	}

	private FrameworkElement BuildItem(Entry entry)
	{
		Image image = new Image
		{
			Width = _iconSize,
			Height = _iconSize,
			Stretch = Stretch.Uniform,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
		if (_icons.TryGetValue(entry.Path, out var cached) && cached.Stamp == entry.Stamp)
		{
			image.Source = cached.Icon;
		}
		TextBlock label = new TextBlock
		{
			Text = entry.Name,
			Foreground = _fg,
			FontSize = 11.0,
			TextAlignment = TextAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			TextTrimming = TextTrimming.CharacterEllipsis,
			MaxHeight = 30.0,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		StackPanel stack = new StackPanel();
		stack.Children.Add(image);
		stack.Children.Add(label);
		Border item = new Border
		{
			Width = _iconSize + 40,
			Margin = new Thickness(2.0),
			Padding = new Thickness(4.0, 6.0, 4.0, 4.0),
			CornerRadius = new CornerRadius(6.0),
			Background = Brushes.Transparent,
			Child = stack,
			Tag = image,
			ToolTip = entry.Name
		};
		item.MouseEnter += delegate
		{
			item.Background = _hover;
		};
		item.MouseLeave += delegate
		{
			item.Background = Brushes.Transparent;
		};
		item.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
			{
				e.Handled = true;
				_dragFrom = null;
				Open(entry.Path);
			}
			else
			{
				_dragFrom = e.GetPosition(this);
			}
		};
		item.MouseLeftButtonUp += delegate
		{
			_dragFrom = null;
		};
		// Перетаскивание значка наружу (в Проводник, в другую полку).
		item.MouseMove += delegate(object _, MouseEventArgs e)
		{
			if (_dragFrom.HasValue && e.LeftButton == MouseButtonState.Pressed)
			{
				Vector delta = e.GetPosition(this) - _dragFrom.Value;
				if (Math.Abs(delta.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(delta.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					_dragFrom = null;
					try
					{
						DragDrop.DoDragDrop(item, new DataObject(DataFormats.FileDrop, new string[1] { entry.Path }), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
					}
					catch (Exception ex)
					{
						Log.Error("Полки: перетаскивание", ex);
					}
				}
			}
		};
		item.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			ShowMenu(item, (Loc.T("Открыть"), delegate
			{
				Open(entry.Path);
			}), (Loc.T("Показать в проводнике"), delegate
			{
				Reveal(entry.Path);
			}));
		};
		return item;
	}

	// Значки достаём по одному в простое UI-потока: COM оболочки любит STA, а окно при этом не замирает.
	private void LoadIcons(List<Entry> entries)
	{
		int generation = ++_iconGeneration;
		int pixels = (int)Math.Round(_iconSize * Display.Scale);
		Queue<(Entry, Image)> queue = new Queue<(Entry, Image)>();
		for (int i = 0; i < entries.Count && i < _items.Children.Count; i++)
		{
			if (_items.Children[i] is Border { Tag: Image image } && image.Source == null)
			{
				queue.Enqueue((entries[i], image));
			}
		}
		void Next()
		{
			if (generation != _iconGeneration || queue.Count == 0)
			{
				return;
			}
			(Entry entry, Image target) = queue.Dequeue();
			try
			{
				ImageSource? icon = LoadIcon(entry, pixels);
				_icons[entry.Path] = (entry.Stamp, icon);
				target.Source = icon;
			}
			catch (Exception ex)
			{
				Log.Error("Полки: значок " + entry.Path, ex);
			}
			base.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Next));
		}
		base.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Next));
	}

	private static ImageSource? LoadIcon(Entry entry, int pixels)
	{
		IconPixels? p = null;
		if (!entry.IsDirectory && ImageExtensions.Contains(Path.GetExtension(entry.Path)))
		{
			p = ShellIcons.TryGetThumbnail(entry.Path, pixels);
		}
		p ??= ShellIcons.TryGetIcon(entry.Path, pixels);
		return ((object)p == null) ? null : DockIcons.ToBitmap(p);
	}

	private static void Open(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo(path)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Error("Полки: открытие " + path, ex);
		}
	}

	private static void Reveal(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Error("Полки: проводник " + path, ex);
		}
	}

	private void ShowMenu(FrameworkElement target, params (string Header, Action Run)[] items)
	{
		ContextMenu menu = new ContextMenu
		{
			PlacementTarget = target,
			Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
		};
		foreach (var (header, run) in items)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = header
			};
			Action run2 = run;
			menuItem.Click += delegate
			{
				try
				{
					run2();
				}
				catch (Exception ex)
				{
					Log.Error("Полки: " + header, ex);
				}
			};
			menu.Items.Add(menuItem);
		}
		menu.IsOpen = true;
	}

	private void ShowTitleMenu()
	{
		List<(string, Action)> items = new List<(string, Action)> { (Loc.T("Открыть папку"), delegate
		{
			EnsureFolder();
			Open(_folder);
		}) };
		if (_rule != null)
		{
			items.Add((Loc.T("Разложить рабочий стол по правилу"), SortDesktop));
		}
		items.Add((Loc.T(_collapsed ? "Развернуть" : "Свернуть"), ToggleCollapsed));
		ShowMenu(_frame, items.ToArray());
	}

	private void SortDesktop()
	{
		ShelfRule? rule = _rule;
		if (rule == null)
		{
			return;
		}
		string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		string folder = _folder;
		Task.Run(() => ShelfFiles.SortInto(desktop, folder, rule, delegate(string path, Exception ex)
		{
			Log.Error("Полки: перенос " + path, ex);
		})).ContinueWith(delegate(Task<List<string>> t)
		{
			if (t.IsCompletedSuccessfully)
			{
				Log.Info($"Полки: «{_shelf.Title}» — разложено с рабочего стола: {t.Result.Count}");
			}
			OnFolderChanged(this, EventArgs.Empty);
		});
	}

	// ---- Перетаскивание файлов на полку ----

	private static string[]? DroppedFiles(IDataObject data)
	{
		if (!data.GetDataPresent(DataFormats.FileDrop))
		{
			return null;
		}
		return data.GetData(DataFormats.FileDrop) as string[];
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		string[]? files = DroppedFiles(e.Data);
		if (files == null || files.Length == 0 || files.All((string f) => ShelfFiles.IsInside(f, _folder)))
		{
			e.Effects = DragDropEffects.None;
		}
		else
		{
			e.Effects = (files.All((string f) => ShelfFiles.SameVolume(f, _folder)) ? DragDropEffects.Move : DragDropEffects.Copy);
			_frame.BorderBrush = _accent;
		}
		e.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		_frame.BorderBrush = _border;
		string[]? files = DroppedFiles(e.Data);
		if (files == null || files.Length == 0)
		{
			return;
		}
		e.Handled = true;
		string folder = _folder;
		Task.Run(delegate
		{
			foreach (string file in files)
			{
				try
				{
					ShelfFiles.MoveInto(file, folder);
				}
				catch (Exception ex)
				{
					Log.Error("Полки: перенос " + file, ex);
				}
			}
		}).ContinueWith(delegate
		{
			OnFolderChanged(this, EventArgs.Empty);
		});
	}

	// ---- Перемещение, размер, сворачивание ----

	private void OnTitleDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if (e.ClickCount == 2)
		{
			ToggleCollapsed();
			return;
		}
		try
		{
			DragMove();
		}
		catch (InvalidOperationException)
		{
			return;
		}
		Settle();
	}

	private void OnGripDown(object sender, MouseButtonEventArgs e)
	{
		if (!_collapsed && _grip.CaptureMouse())
		{
			_resizeFrom = e.GetPosition(this);
			_resizeStart = new Size(base.Width, _fullHeight);
			e.Handled = true;
		}
	}

	private void OnGripMove(object sender, MouseEventArgs e)
	{
		if (_resizeFrom.HasValue && e.LeftButton == MouseButtonState.Pressed)
		{
			Vector delta = e.GetPosition(this) - _resizeFrom.Value;
			base.Width = Math.Max(ShelfLayout.MinWidth, _resizeStart.Width + delta.X);
			_fullHeight = Math.Max(ShelfLayout.MinHeight, _resizeStart.Height + delta.Y);
			base.Height = _fullHeight;
			SetFrameHeight(_fullHeight);
		}
	}

	private void OnGripUp(object sender, MouseButtonEventArgs e)
	{
		if (_resizeFrom.HasValue)
		{
			_resizeFrom = null;
			_grip.ReleaseMouseCapture();
			Settle();
		}
	}

	// После перетаскивания/ресайза: монитор по центру полки, сетка 16 px, внутри рабочей области; сохранить.
	private void Settle()
	{
		Point center = new Point(base.Left + base.Width / 2.0, base.Top + TitleHeight / 2.0);
		Display display = Display.All().FirstOrDefault((Display d) => d.BoundsDip.Contains(center)) ?? _display;
		_display = display;
		ShelfRect work = WorkOf(display);
		ShelfRect rect = ShelfLayout.Fit(new ShelfRect(base.Left, base.Top, base.Width, _fullHeight), work);
		base.Left = rect.X;
		base.Top = rect.Y;
		base.Width = rect.Width;
		base.Height = rect.Height;
		_fullHeight = rect.Height;
		if (!_collapsed)
		{
			SetFrameHeight(_fullHeight);
		}
		Persist();
	}

	private void Persist()
	{
		ShelfRect work = WorkOf(_display);
		(double x, double y) = ShelfLayout.ToFractions(new ShelfRect(base.Left, base.Top, base.Width, _fullHeight), work);
		_owner.Save(_key, new ShelfState
		{
			X = x,
			Y = y,
			Width = base.Width,
			Height = _fullHeight,
			Collapsed = _collapsed,
			Monitor = _display.Name
		});
	}

	private void ToggleCollapsed()
	{
		_collapsed = !_collapsed;
		ApplyCollapsedChrome();
		double target = (_collapsed ? TitleHeight : _fullHeight);
		DoubleAnimation roll = new DoubleAnimation(_frame.ActualHeight, target, new Duration(TimeSpan.FromMilliseconds(180.0)))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		};
		roll.Completed += delegate
		{
			SetFrameHeight(_collapsed ? TitleHeight : _fullHeight);
		};
		_frame.BeginAnimation(FrameworkElement.HeightProperty, roll);
		Persist();
	}

	private void SetFrameHeight(double height)
	{
		_frame.BeginAnimation(FrameworkElement.HeightProperty, null);
		_frame.Height = height;
	}

	private void ApplyCollapsedChrome()
	{
		_chevron.Text = (_collapsed ? "▸" : "▾");
		_grip.Visibility = ((!_collapsed) ? Visibility.Visible : Visibility.Collapsed);
		if (_collapsed)
		{
			_hint.Visibility = Visibility.Collapsed;
		}
		else if (_hint.Text.Length > 0)
		{
			_hint.Visibility = Visibility.Visible;
		}
	}

	// ---- Слой рабочего стола ----

	internal void SetFront(bool front)
	{
		_front = front;
		if (_hwnd != IntPtr.Zero)
		{
			Win32.SetWindowPos(_hwnd, front ? HwndTop : HwndBottom, 0, 0, 0, 0, SwpKeep);
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
		Win32.SetWindowPos(_hwnd, HwndBottom, 0, 0, 0, 0, SwpKeep);
	}

	// Пока рабочий стол не на переднем плане — никому не даём поднять полку над окнами.
	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == WM_WINDOWPOSCHANGING && !_front && lParam != IntPtr.Zero)
		{
			WINDOWPOS pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
			if ((pos.flags & 4) == 0 && pos.hwndInsertAfter != HwndBottom)
			{
				pos.hwndInsertAfter = HwndBottom;
				Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
			}
		}
		return IntPtr.Zero;
	}

	protected override void OnClosed(EventArgs e)
	{
		_refresh.Stop();
		_iconGeneration++;
		if (_watcher != null)
		{
			_watcher.EnableRaisingEvents = false;
			_watcher.Dispose();
			_watcher = null;
		}
		base.OnClosed(e);
	}
}
