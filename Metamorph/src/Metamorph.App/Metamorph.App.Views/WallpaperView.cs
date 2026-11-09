using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Metamorph.App.Desktop;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;
using Microsoft.Win32;

namespace Metamorph.App.Views;

internal sealed class WallpaperView : System.Windows.Controls.UserControl
{
	private readonly App _app;

	private readonly Action<string?> _reloadThemes;

	private readonly Action<string, bool> _status;

	private readonly WrapPanel _library = new WrapPanel();

	private readonly WrapPanel _engine = new WrapPanel();

	private readonly TextBlock _current = new TextBlock
	{
		FontSize = 15.0,
		FontWeight = FontWeights.SemiBold,
		VerticalAlignment = VerticalAlignment.Center,
		TextTrimming = TextTrimming.CharacterEllipsis
	};

	private readonly TextBlock _engineInfo = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 4.0, 0.0, 6.0)
	};

	private readonly System.Windows.Controls.Button _pause;

	private readonly System.Windows.Controls.ComboBox _fit = Combo();

	private readonly System.Windows.Controls.CheckBox _sound = new System.Windows.Controls.CheckBox();

	private readonly Slider _volume = new Slider
	{
		Minimum = 0.0,
		Maximum = 1.0,
		Width = 260.0,
		HorizontalAlignment = System.Windows.HorizontalAlignment.Left
	};

	private readonly System.Windows.Controls.CheckBox _pauseCovered = new System.Windows.Controls.CheckBox();

	private readonly System.Windows.Controls.CheckBox _pauseBattery = new System.Windows.Controls.CheckBox();

	private readonly System.Windows.Controls.CheckBox _keep = new System.Windows.Controls.CheckBox();

	private readonly System.Windows.Controls.ComboBox _rotate = Combo();

	private readonly DispatcherTimer _debounce = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(450.0)
	};

	private bool _loading;

	private static readonly int[] RotateOptions = new int[6] { 0, 5, 15, 30, 60, 180 };

	private static readonly WallpaperStyle[] FitOptions = new WallpaperStyle[4]
	{
		WallpaperStyle.Fill,
		WallpaperStyle.Fit,
		WallpaperStyle.Stretch,
		WallpaperStyle.Center
	};

	private const int TileWidth = 196;

	private WallpaperChoice? Choice => _app.WallpaperOverride;

	public WallpaperView(App app, Action<string?> reloadThemes, Action<string, bool> status)
	{
		_app = app;
		_reloadThemes = reloadThemes;
		_status = status;
		_engineInfo.Foreground = Ui.Res("SubTextBrush");
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)Ui.Header("Обои"),
				(UIElement)Ui.Hint("Свой GIF, видео (mp4, webm, mov…) или картинка — за значками рабочего стола. Добавьте файл кнопкой или просто перетащите его в это окно, потом щёлкните по плитке.")
			}
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 8.0)
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		_pause = Ui.Button("Пауза", delegate
		{
			_app.ToggleWallpaperPause();
		});
		stackPanel2.Children.Add(Ui.Button("Вернуть обои темы", delegate
		{
			Apply(null);
		}));
		stackPanel2.Children.Add(_pause);
		stackPanel2.Children.Add(Ui.Button("Следующие", delegate
		{
			_app.NextWallpaper();
		}));
		DockPanel.SetDock(stackPanel2, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(stackPanel2);
		dockPanel.Children.Add(_current);
		stackPanel.Children.Add(dockPanel);
		StackPanel stackPanel3 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		stackPanel3.Children.Add(DropZone());
		stackPanel3.Children.Add(Ui.Header("Мои обои"));
		stackPanel3.Children.Add(Ui.Hint("Небольшие файлы копируются в библиотеку Metamorph, большие видео и папки добавляются ссылкой. Правый клик по плитке — показать в проводнике или убрать."));
		WrapPanel element = new WrapPanel
		{
			Children = 
			{
				(UIElement)Ui.Button("Папка для слайд-шоу…", AddFolder),
				(UIElement)Ui.Button("Открыть папку библиотеки", delegate
				{
					Open(_app.Library.Folder);
				})
			}
		};
		stackPanel3.Children.Add(element);
		stackPanel3.Children.Add(_library);
		stackPanel3.Children.Add(Ui.Header("Как показывать"));
		string[] array = new string[4] { "заполнить (обрезать края)", "вписать целиком", "растянуть", "по центру" };
		foreach (string ru in array)
		{
			_fit.Items.Add(Loc.T(ru));
		}
		stackPanel3.Children.Add(Ui.Row("Вписывание", _fit));
		stackPanel3.Children.Add(Ui.Row("Звук видео", _sound, "Звук играет только с основного монитора"));
		stackPanel3.Children.Add(Ui.Row("Громкость", _volume));
		stackPanel3.Children.Add(Ui.Row("Пауза, когда обои закрыты окном", _pauseCovered, "Развёрнутое или полноэкранное окно — GIF/видео замирает и не тратит ресурсы"));
		stackPanel3.Children.Add(Ui.Row("Пауза от батареи", _pauseBattery));
		stackPanel3.Children.Add(Ui.Row("Оставлять мои обои при смене темы", _keep, "Иначе при применении темы вернутся её собственные обои"));
		_rotate.Items.Add(Loc.T("не менять"));
		foreach (int item in RotateOptions.Skip(1))
		{
			_rotate.Items.Add((item < 60) ? Loc.F("каждые {0} мин", item) : Loc.F("каждые {0} ч", item / 60));
		}
		stackPanel3.Children.Add(Ui.Row("Менять обои из «Моих обоев»", _rotate));
		stackPanel3.Children.Add(Ui.Header("Wallpaper Engine"));
		stackPanel3.Children.Add(Ui.Hint("Если установлен Wallpaper Engine (Steam), его видео-обои можно поставить и здесь — они добавятся в «Мои обои»."));
		WrapPanel element2 = new WrapPanel
		{
			Children = { (UIElement)Ui.Button("Найти видео Wallpaper Engine", ScanEngine) }
		};
		stackPanel3.Children.Add(element2);
		stackPanel3.Children.Add(_engineInfo);
		stackPanel3.Children.Add(_engine);
		stackPanel3.Children.Add(Ui.Header("Темы из обоев"));
		WrapPanel element3 = new WrapPanel
		{
			Children = 
			{
				(UIElement)Ui.Button("Сохранить в новую тему", SaveAsTheme),
				(UIElement)Ui.Button("Тема из картинки (палитра как pywal)", ThemeFromImage)
			}
		};
		stackPanel3.Children.Add(element3);
		DockPanel dockPanel2 = new DockPanel();
		DockPanel.SetDock(stackPanel, System.Windows.Controls.Dock.Top);
		dockPanel2.Children.Add(stackPanel);
		dockPanel2.Children.Add(new ScrollViewer
		{
			Content = stackPanel3,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		base.Content = Ui.Card(dockPanel2);
		_debounce.Tick += delegate
		{
			_debounce.Stop();
			SaveBehavior();
		};
		_fit.SelectionChanged += delegate
		{
			Tweaked();
		};
		_sound.Checked += delegate
		{
			Tweaked();
		};
		_sound.Unchecked += delegate
		{
			Tweaked();
		};
		_volume.ValueChanged += delegate
		{
			if (!_loading)
			{
				_debounce.Stop();
				_debounce.Start();
			}
		};
		System.Windows.Controls.CheckBox[] array2 = new System.Windows.Controls.CheckBox[3] { _pauseCovered, _pauseBattery, _keep };
		foreach (System.Windows.Controls.CheckBox obj in array2)
		{
			obj.Checked += delegate
			{
				SaveBehavior();
			};
			obj.Unchecked += delegate
			{
				SaveBehavior();
			};
		}
		_rotate.SelectionChanged += delegate
		{
			SaveBehavior();
		};
		_app.WallpaperChanged += delegate
		{
			base.Dispatcher.InvokeAsync(Refresh);
		};
		_app.ActiveThemeChanged += delegate
		{
			base.Dispatcher.InvokeAsync(Refresh);
		};
		base.Loaded += delegate
		{
			BuildLibrary();
			Refresh();
		};
	}

	private static System.Windows.Controls.ComboBox Combo()
	{
		return new System.Windows.Controls.ComboBox
		{
			Width = 260.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left
		};
	}

	private UIElement DropZone()
	{
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "\ud83c\udf9e  ▶  \ud83d\uddbc",
			FontSize = 26.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Foreground = Ui.Res("SubTextBrush")
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = Loc.T("Перетащите сюда GIF, видео или картинку"),
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 6.0, 0.0, 8.0)
		});
		System.Windows.Controls.Button button = Ui.Button("Добавить GIF, видео или картинку…", AddFile, primary: true);
		button.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		stackPanel.Children.Add(button);
		Border obj = new Border
		{
			Height = 150.0,
			Margin = new Thickness(0.0, 4.0, 0.0, 6.0),
			CornerRadius = new CornerRadius(12.0),
			BorderThickness = new Thickness(2.0),
			Background = Ui.Res("CardBrush"),
			AllowDrop = true,
			Child = stackPanel
		};
		Rectangle dash = new Rectangle
		{
			StrokeDashArray = new DoubleCollection { 4.0, 3.0 },
			StrokeThickness = 2.0,
			RadiusX = 12.0,
			RadiusY = 12.0,
			Stroke = Ui.Res("BorderBrush"),
			Width = 400.0,
			Height = 150.0
		};
		obj.BorderBrush = new VisualBrush(dash)
		{
			Stretch = Stretch.Fill
		};
		obj.DragOver += delegate(object _, System.Windows.DragEventArgs e)
		{
			e.Effects = (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None);
			dash.Stroke = Ui.Res("AccentBrush");
			e.Handled = true;
		};
		obj.DragLeave += delegate
		{
			dash.Stroke = Ui.Res("BorderBrush");
		};
		obj.Drop += delegate(object _, System.Windows.DragEventArgs e)
		{
			dash.Stroke = Ui.Res("BorderBrush");
			if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] source)
			{
				e.Handled = true;
				List<string> list = source.Where(WallpaperLibrary.IsSupported).ToList();
				if (list.Count == 0)
				{
					_status(Loc.T("Это не GIF, не видео и не картинка."), arg2: true);
				}
				else
				{
					AddPaths(list);
				}
			}
		};
		return obj;
	}

	private void AddFile()
	{
		string value = string.Join(";", WallpaperService.ImageExtensions.Select((string e) => "*" + e));
		string value2 = string.Join(";", WallpaperService.VideoExtensions.Select((string e) => "*" + e));
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = $"{Loc.T("GIF, видео и картинки")}|*.gif;{value2};{value}|GIF|*.gif|{Loc.T("Видео")}|{value2}|{Loc.T("Картинки")}|{value}",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog().GetValueOrDefault())
		{
			AddPaths(openFileDialog.FileNames);
		}
	}

	private void AddFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = Loc.T("Папка с картинками для слайд-шоу")
		};
		if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
		{
			AddPaths(new string[1] { folderBrowserDialog.SelectedPath });
		}
	}

	public void AddPaths(IEnumerable<string> paths)
	{
		string text = null;
		foreach (string path in paths)
		{
			try
			{
				string text2 = _app.Library.Add(path);
				if (text == null)
				{
					text = text2;
				}
			}
			catch (Exception ex)
			{
				_status(ex.Message, arg2: true);
			}
		}
		BuildLibrary();
		if (text != null)
		{
			Apply(With(text));
		}
	}

	private WallpaperChoice With(string path)
	{
		WallpaperChoice obj = Choice?.Copy() ?? new WallpaperChoice();
		obj.Path = path;
		return obj;
	}

	private void BuildLibrary()
	{
		_library.Children.Clear();
		IReadOnlyList<string> readOnlyList;
		try
		{
			readOnlyList = _app.Library.List();
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
			return;
		}
		if (readOnlyList.Count == 0)
		{
			_library.Children.Add(Ui.Hint("Пока пусто — добавьте GIF или видео кнопкой выше или перетащите файл в окно."));
			return;
		}
		foreach (string path in readOnlyList)
		{
			_library.Children.Add(Tile(path, System.IO.Path.GetFileName(path.TrimEnd('\\', '/')), null, removable: true, delegate
			{
				Apply(With(path));
			}));
		}
		Refresh();
	}

	private Border Tile(string path, string title, string? badge, bool removable, Action click, string? thumbFrom = null)
	{
		string path2 = path;
		Action click2 = click;
		WallpaperMedia wallpaperMedia = WallpaperLibrary.MediaOf(path2);
		string text = wallpaperMedia switch
		{
			WallpaperMedia.Video => "▶", 
			WallpaperMedia.Gif => "\ud83c\udf9e", 
			WallpaperMedia.Folder => "\ud83d\udcc1", 
			_ => "\ud83d\uddbc", 
		};
		Grid grid = new Grid
		{
			Height = 110.0,
			ClipToBounds = true,
			Background = Ui.Res("PanelBrush")
		};
		grid.Children.Add(new TextBlock
		{
			Text = text,
			FontSize = 30.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = Ui.Res("SubTextBrush")
		});
		Border element = new Border
		{
			CornerRadius = new CornerRadius(9.0, 9.0, 0.0, 0.0),
			ClipToBounds = true,
			Child = grid
		};
		Grid target = grid;
		string file = thumbFrom ?? ((wallpaperMedia == WallpaperMedia.Folder) ? WallpaperService.SlideshowImages(path2).FirstOrDefault() : path2);
		bool flag = (uint)(wallpaperMedia - 2) <= 1u;
		LoadThumb(target, file, flag ? text : null);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(10.0, 6.0, 10.0, 7.0)
		};
		file = badge;
		if (file == null)
		{
			file = wallpaperMedia switch
			{
				WallpaperMedia.Video => Loc.T("видео"), 
				WallpaperMedia.Gif => "GIF", 
				WallpaperMedia.Folder => Loc.T("слайд-шоу"), 
				_ => Loc.T("картинка"), 
			};
		}
		string text2 = file;
		TextBlock element2 = new TextBlock
		{
			Text = text2,
			FontSize = 11.0,
			Foreground = Ui.Res("SubTextBrush"),
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		DockPanel.SetDock(element2, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(element2);
		dockPanel.Children.Add(new TextBlock
		{
			Text = title,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(dockPanel);
		Grid grid2 = new Grid();
		grid2.Children.Add(stackPanel);
		if (removable)
		{
			System.Windows.Controls.Button remove = new System.Windows.Controls.Button
			{
				Content = "✕",
				Width = 26.0,
				Height = 26.0,
				Padding = new Thickness(0.0),
				Margin = new Thickness(0.0, 6.0, 6.0, 0.0),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				ToolTip = Loc.T("Убрать из «Моих обоев»"),
				Visibility = Visibility.Hidden
			};
			remove.Click += delegate(object _, RoutedEventArgs e)
			{
				e.Handled = true;
				Remove(path2);
			};
			grid2.Children.Add(remove);
			grid2.MouseEnter += delegate
			{
				remove.Visibility = Visibility.Visible;
			};
			grid2.MouseLeave += delegate
			{
				remove.Visibility = Visibility.Hidden;
			};
		}
		Border border = new Border();
		border.Width = 196.0;
		border.Margin = new Thickness(0.0, 0.0, 12.0, 12.0);
		border.CornerRadius = new CornerRadius(11.0);
		border.Background = Ui.Res("CardBrush");
		border.BorderBrush = Ui.Res("BorderBrush");
		border.BorderThickness = new Thickness(2.0);
		border.Cursor = System.Windows.Input.Cursors.Hand;
		border.Child = grid2;
		border.Tag = path2;
		border.ToolTip = path2;
		border.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			if (!(e.OriginalSource is DependencyObject child) || FindParent<System.Windows.Controls.Button>(child) == null)
			{
				click2();
			}
		};
		ContextMenu contextMenu = new ContextMenu();
		MenuItem menuItem = new MenuItem
		{
			Header = Loc.T("Поставить")
		};
		menuItem.Click += delegate
		{
			click2();
		};
		contextMenu.Items.Add(menuItem);
		MenuItem menuItem2 = new MenuItem
		{
			Header = Loc.T("Показать в проводнике")
		};
		menuItem2.Click += delegate
		{
			Reveal(path2);
		};
		contextMenu.Items.Add(menuItem2);
		if (removable)
		{
			MenuItem menuItem3 = new MenuItem
			{
				Header = Loc.T("Убрать из «Моих обоев»")
			};
			menuItem3.Click += delegate
			{
				Remove(path2);
			};
			contextMenu.Items.Add(menuItem3);
		}
		border.ContextMenu = contextMenu;
		return border;
	}

	private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
	{
		DependencyObject dependencyObject = child;
		while (dependencyObject != null)
		{
			if (dependencyObject is T result)
			{
				return result;
			}
			bool flag = ((dependencyObject is Visual || dependencyObject is Visual3D) ? true : false);
			dependencyObject = (flag ? VisualTreeHelper.GetParent(dependencyObject) : LogicalTreeHelper.GetParent(dependencyObject));
		}
		return null;
	}

	private static void LoadThumb(Grid target, string? file, string? overlayGlyph)
	{
		string file2 = file;
		Grid target2 = target;
		string overlayGlyph2 = overlayGlyph;
		if (file2 == null)
		{
			return;
		}
		Dispatcher dispatcher = target2.Dispatcher;
		Thread thread = new Thread((ThreadStart)delegate
		{
			ImageSource image = PosterMaker.Tile(file2, 236);
			if (image != null)
			{
				dispatcher.InvokeAsync(delegate
				{
					target2.Children.Clear();
					target2.Children.Add(new Image
					{
						Source = image,
						Stretch = Stretch.UniformToFill
					});
					if (overlayGlyph2 != null)
					{
						target2.Children.Add(new TextBlock
						{
							Text = overlayGlyph2,
							FontSize = 13.0,
							Foreground = Brushes.White,
							Margin = new Thickness(8.0, 6.0, 0.0, 0.0),
							HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
							VerticalAlignment = VerticalAlignment.Top,
							Effect = new DropShadowEffect
							{
								BlurRadius = 6.0,
								ShadowDepth = 0.0,
								Opacity = 0.9
							}
						});
					}
				});
			}
		});
		thread.IsBackground = true;
		thread.Name = "Metamorph.Thumb";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
	}

	private void Remove(string path)
	{
		try
		{
			if (string.Equals(Choice?.Path, path, StringComparison.OrdinalIgnoreCase))
			{
				Apply(null);
			}
			_app.Library.Remove(path);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
		BuildLibrary();
	}

	private async void ScanEngine()
	{
		_engine.Children.Clear();
		_engineInfo.Text = Loc.T("Ищу библиотеки Steam…");
		try
		{
			List<WallpaperEngineItem> list = await Task.Run(() => WallpaperEngineImport.Scan(WallpaperEngineImport.FindSteamLibraries()));
			if (list.Count == 0)
			{
				_engineInfo.Text = Loc.T("Видео-обои Wallpaper Engine не найдены (нужен Steam с Wallpaper Engine и подписками в мастерской).");
				return;
			}
			_engineInfo.Text = Loc.F("Найдено видео: {0}. Клик — поставить и добавить в «Мои обои».", list.Count);
			foreach (WallpaperEngineItem item in list.Take(200))
			{
				string file = item.File;
				WallpaperView wallpaperView = this;
				string path = file;
				string title = item.Title;
				string badge = Loc.T("видео");
				Action click = delegate
				{
					try
					{
						_app.Library.Add(file, false);
					}
					catch (Exception ex2)
					{
						Log.Error("Wallpaper Engine → библиотека", ex2);
					}
					BuildLibrary();
					Apply(With(file));
				};
				string preview = item.Preview;
				Border element = wallpaperView.Tile(path, title, badge, removable: false, click, (preview != null && File.Exists(preview)) ? preview : null);
				_engine.Children.Add(element);
			}
		}
		catch (Exception ex)
		{
			_engineInfo.Text = ex.Message;
			Log.Error("Wallpaper Engine", ex);
		}
	}

	private async void Apply(WallpaperChoice? choice)
	{
		_status((choice == null) ? Loc.T("Возвращаю обои темы…") : Loc.F("Ставлю обои {0}…", WallpaperChoices.Describe(choice)), arg2: false);
		try
		{
			ApplyReport applyReport = await _app.ApplyWallpaperChoiceAsync(choice);
			_status(applyReport.Success ? Loc.F("Обои: {0}", WallpaperChoices.Describe(choice)) : applyReport.Summary(), !applyReport.Success);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}

	private void Tweaked(bool immediate = true)
	{
		if (!_loading && Choice != null && immediate)
		{
			ApplyTweaks();
		}
	}

	private void ApplyTweaks()
	{
		WallpaperChoice choice = Choice;
		if (choice != null)
		{
			WallpaperChoice wallpaperChoice = choice.Copy();
			wallpaperChoice.Style = FitOptions[Math.Max(0, _fit.SelectedIndex)];
			wallpaperChoice.Muted = !_sound.IsChecked.GetValueOrDefault();
			if (wallpaperChoice.Style != choice.Style || wallpaperChoice.Muted != choice.Muted)
			{
				Apply(wallpaperChoice);
			}
		}
	}

	private void SaveBehavior()
	{
		if (!_loading)
		{
			AppSettings appSettings = _app.Services.Settings.Load();
			appSettings.Wallpaper.KeepOnThemeChange = _keep.IsChecked.GetValueOrDefault();
			appSettings.Wallpaper.RotateMinutes = RotateOptions[Math.Max(0, _rotate.SelectedIndex)];
			appSettings.Wallpaper.PauseWhenCovered = _pauseCovered.IsChecked.GetValueOrDefault();
			appSettings.Wallpaper.PauseOnBattery = _pauseBattery.IsChecked.GetValueOrDefault();
			appSettings.Wallpaper.Volume = Math.Round(_volume.Value, 2);
			_app.Services.Settings.Save(appSettings);
			_app.WallpaperPrefsChanged();
		}
	}

	private void Refresh()
	{
		_loading = true;
		try
		{
			WallpaperPrefs wallpaper = _app.Services.Settings.Current.Wallpaper;
			WallpaperChoice @override = wallpaper.Override;
			_current.Text = Loc.T("Сейчас: ") + ((@override == null) ? Loc.F("обои темы «{0}»", _app.WallpaperBaseTheme.Name) : WallpaperChoices.Describe(@override));
			_pause.Content = (_app.Session.IsWallpaperPaused ? Loc.T("Продолжить") : Loc.T("Пауза"));
			_pause.IsEnabled = _app.Session.IsWallpaperAnimated || _app.Session.IsWallpaperPaused;
			foreach (Border item in _library.Children.OfType<Border>().Concat(_engine.Children.OfType<Border>()))
			{
				string text = @override?.Path;
				item.BorderBrush = ((text != null && string.Equals(item.Tag as string, text, StringComparison.OrdinalIgnoreCase)) ? Ui.Res("AccentBrush") : Ui.Res("BorderBrush"));
			}
			WallpaperMedia wallpaperMedia = WallpaperLibrary.MediaOf(@override?.Path);
			_fit.SelectedIndex = Math.Max(0, Array.IndexOf(FitOptions, @override?.Style ?? WallpaperStyle.Fill));
			_fit.IsEnabled = @override != null;
			_sound.IsChecked = @override != null && !@override.Muted;
			_sound.IsEnabled = wallpaperMedia == WallpaperMedia.Video;
			_volume.Value = wallpaper.Volume;
			_volume.IsEnabled = wallpaperMedia == WallpaperMedia.Video;
			_pauseCovered.IsChecked = wallpaper.PauseWhenCovered;
			_pauseBattery.IsChecked = wallpaper.PauseOnBattery;
			_keep.IsChecked = wallpaper.KeepOnThemeChange;
			_rotate.SelectedIndex = Math.Max(0, Array.IndexOf(RotateOptions, wallpaper.RotateMinutes));
		}
		finally
		{
			_loading = false;
		}
	}

	private void SaveAsTheme()
	{
		if (Choice == null)
		{
			_status(Loc.T("Сначала выберите обои."), arg2: true);
			return;
		}
		try
		{
			ThemeProfile wallpaperBaseTheme = _app.WallpaperBaseTheme;
			ThemeProfile source = WallpaperChoices.Effective(wallpaperBaseTheme, _app.Services.Settings.Current.Wallpaper);
			ThemeProfile themeProfile = _app.Services.Profiles.CreateCopy(source, Loc.F("{0} + {1}", wallpaperBaseTheme.Name, WallpaperChoices.Describe(Choice)));
			_reloadThemes(themeProfile.Id);
			_status(Loc.F("Создана тема «{0}» — она в списке на вкладке «Темы».", themeProfile.Name), arg2: false);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}

	private void ThemeFromImage()
	{
		string text = Choice?.Path;
		WallpaperMedia wallpaperMedia = WallpaperLibrary.MediaOf(text);
		if ((uint)(wallpaperMedia - 1) > 1u)
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = Loc.T("Картинки") + "|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.jfif;*.gif"
			};
			if (!openFileDialog.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			text = openFileDialog.FileName;
		}
		try
		{
			PaletteExtractor.Result result = PaletteExtractor.Extract(EditorView.LoadPixels(text, 100));
			ThemeProfile themeProfile = ThemeJson.Clone(_app.WallpaperBaseTheme);
			themeProfile.Palette = result.Palette;
			themeProfile.Mode = result.Mode;
			themeProfile.Wallpaper = WallpaperChoices.Resolve((Choice?.Path == text) ? Choice : new WallpaperChoice
			{
				Path = text
			}, themeProfile) ?? new WallpaperSettings
			{
				Kind = WallpaperKind.Image,
				Path = text
			};
			ThemeProfile themeProfile2 = _app.Services.Profiles.CreateCopy(themeProfile, Loc.F("Тема из «{0}»", System.IO.Path.GetFileNameWithoutExtension(text)));
			_reloadThemes(themeProfile2.Id);
			_status(Loc.F("Создана тема «{0}» с палитрой из картинки — примените её на вкладке «Темы».", themeProfile2.Name), arg2: false);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось прочитать картинку: ") + ex.Message, arg2: true);
		}
	}

	private static void Open(string folder)
	{
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Error("Открыть папку", ex);
		}
	}

	private static void Reveal(string path)
	{
		try
		{
			string arguments = (Directory.Exists(path) ? ("\"" + path + "\"") : ("/select,\"" + path + "\""));
			Process.Start(new ProcessStartInfo("explorer.exe", arguments)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			Log.Error("Показать в проводнике", ex);
		}
	}
}
