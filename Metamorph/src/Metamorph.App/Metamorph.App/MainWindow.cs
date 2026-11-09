using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.App.Views;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;

namespace Metamorph.App;

public class MainWindow : Window, IComponentConnector
{
	private readonly App _app;

	private ThemeProfile? _selected;

	private ThemeProfile? _working;

	private bool _loadingOptions = true;

	private readonly EditorView _editor;

	private readonly SettingsView _settings;

	private readonly GalleryView _gallery;

	private readonly WallpaperView _wallpapers;

	private readonly MascotView _mascot;

	private static readonly string[] ThemeExtensions = new string[4] { ".mmtheme", ".yaml", ".yml", ".json" };

	internal TextBlock ActiveThemeText;

	internal TabControl Tabs;

	internal ListBox ThemeList;

	internal TextBlock ThemeName;

	internal TextBlock ThemeDescription;

	internal Border PreviewDesktop;

	internal Border PreviewBar;

	internal TextBlock PreviewBarLeft;

	internal TextBlock PreviewBarCenter;

	internal TextBlock PreviewBarRight;

	internal Border PreviewWindow;

	internal Border PreviewTitle;

	internal TextBlock PreviewTitleText;

	internal TextBlock PreviewTerminal;

	internal Border PreviewTaskbar;

	internal Border PreviewIsland;

	internal TextBlock PreviewIslandText;

	internal StackPanel PreviewIslandBars;

	internal Border PreviewDock;

	internal StackPanel PreviewDockIcons;

	internal CheckBox BarEnabledCheck;

	internal ComboBox BarPositionCombo;

	internal ComboBox TaskbarModeCombo;

	internal CheckBox DockEnabledCheck;

	internal ComboBox DockPositionCombo;

	internal ComboBox DockBehaviorCombo;

	internal ComboBox DockEffectCombo;

	internal CheckBox IslandEnabledCheck;

	internal CheckBox TitleAccentCheck;

	internal CheckBox TransparencyCheck;

	internal Button ApplyButton;

	internal Button SaveCopyButton;

	internal Button EditJsonButton;

	internal TabItem WallpaperTab;

	internal TabItem MascotTab;

	internal TabItem EditorTab;

	internal TabItem SettingsTab;

	internal TabItem GalleryTab;

	internal CheckBox AutostartCheck;

	internal TextBlock StatusText;

	private bool _contentLoaded;

	public MainWindow(App app)
	{
		InitializeComponent();
		_app = app;
		Localizer.Apply(this);
		_editor = new EditorView(app, delegate(string? id)
		{
			LoadThemes(id);
		}, SetStatus);
		_settings = new SettingsView(app, SetStatus);
		_gallery = new GalleryView(app, delegate(string? id)
		{
			LoadThemes(id);
		}, SetStatus);
		EditorTab.Content = _editor;
		SettingsTab.Content = _settings;
		GalleryTab.Content = _gallery;
		_wallpapers = new WallpaperView(app, delegate(string? id)
		{
			LoadThemes(id);
		}, SetStatus);
		WallpaperTab.Content = _wallpapers;
		_mascot = new MascotView(app, SetStatus);
		MascotTab.Content = _mascot;
		base.DragOver += OnDragOver;
		base.Drop += OnDrop;
		Tabs.SelectionChanged += Tabs_SelectionChanged;
		base.SourceInitialized += delegate
		{
			WindowEffects.UseDarkTitleBar(new WindowInteropHelper(this).Handle, dark: true);
		};
		_app.ActiveThemeChanged += delegate
		{
			UpdateActiveText();
		};
		AutostartCheck.IsChecked = SafeGet(AutostartService.IsEnabled);
		_loadingOptions = false;
		LoadThemes();
		UpdateActiveText();
	}

	private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.OriginalSource == Tabs && Tabs.SelectedItem == EditorTab && _working != null && !_editor.Dirty)
		{
			_editor.Load(_working);
		}
	}

	private void OnDragOver(object sender, DragEventArgs e)
	{
		e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void OnDrop(object sender, DragEventArgs e)
	{
		if (!(e.Data.GetData(DataFormats.FileDrop) is string[] array) || array.Length == 0)
		{
			return;
		}
		e.Handled = true;
		List<string> list = array.Where((string f) => ThemeExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
		List<string> list2 = array.Where(WallpaperLibrary.IsSupported).ToList();
		foreach (string item in list)
		{
			_app.ImportThemeFile(item);
		}
		if (list2.Count > 0)
		{
			Tabs.SelectedItem = WallpaperTab;
			_wallpapers.AddPaths(list2);
		}
		else if (list.Count == 0)
		{
			SetStatus(Loc.T("Перетащите картинку, GIF, видео, папку или файл темы (.mmtheme)."), error: true);
		}
	}

	public void ShowWallpapers()
	{
		Tabs.SelectedItem = WallpaperTab;
	}

	public void ShowMascot()
	{
		Tabs.SelectedItem = MascotTab;
	}

	public void ShowEditor()
	{
		Tabs.SelectedItem = EditorTab;
	}

	private void LoadThemes(string? selectId = null)
	{
		ProfileStore store = _app.Services.Profiles;
		List<ThemeCard> list = (from t in store.LoadAll()
			select new ThemeCard(t, BrushFactory.Wallpaper(t, store.ResolveWallpaperPath(t)))).ToList();
		ThemeList.ItemsSource = list;
		string target = selectId ?? _selected?.Id ?? _app.Session.Active?.Id;
		ThemeList.SelectedItem = list.FirstOrDefault((ThemeCard c) => c.Profile.Id == target) ?? list.FirstOrDefault();
		if (store.LastErrors.Count > 0)
		{
			SetStatus(Loc.T("Пропущены файлы тем с ошибками: ") + string.Join(" | ", store.LastErrors), error: true);
		}
		else if (list.Count == 0)
		{
			SetStatus(Loc.F("Темы не найдены. Добавьте JSON в {0}", _app.Services.Paths.UserThemes), error: true);
		}
	}

	private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (ThemeList.SelectedItem is ThemeCard themeCard)
		{
			Select(themeCard.Profile);
		}
	}

	private void Select(ThemeProfile profile)
	{
		_selected = profile;
		_working = ThemeJson.Clone(profile);
		ThemeName.Text = profile.Name;
		string text = (string.IsNullOrWhiteSpace(profile.Author) ? "" : ("  ·  " + Loc.F("автор: {0}", profile.Author)));
		ThemeDescription.Text = (string.IsNullOrWhiteSpace(profile.Description) ? Loc.T("Без описания") : profile.Description) + text;
		_loadingOptions = true;
		BarEnabledCheck.IsChecked = profile.Bar.Enabled;
		BarPositionCombo.SelectedIndex = ((profile.Bar.Position != BarPosition.Top) ? 1 : 0);
		TaskbarModeCombo.SelectedIndex = (int)profile.Taskbar.Mode;
		DockEnabledCheck.IsChecked = profile.Dock.Enabled;
		DockPositionCombo.SelectedIndex = (int)profile.Dock.Position;
		DockBehaviorCombo.SelectedIndex = (int)profile.Dock.Behavior;
		DockEffectCombo.SelectedIndex = (int)profile.Dock.HoverEffect;
		IslandEnabledCheck.IsChecked = profile.Island.Enabled;
		TitleAccentCheck.IsChecked = profile.System.AccentOnTitleBars;
		TransparencyCheck.IsChecked = profile.System.Transparency;
		_loadingOptions = false;
		EditJsonButton.IsEnabled = !profile.IsBuiltIn;
		EditJsonButton.ToolTip = (profile.IsBuiltIn ? Loc.T("Встроенные темы не редактируются — сначала «Сохранить как свою».") : profile.SourcePath);
		UpdatePreview();
	}

	private void Option_Changed(object sender, RoutedEventArgs e)
	{
		ReadOptions();
	}

	private void Option_Changed(object sender, SelectionChangedEventArgs e)
	{
		ReadOptions();
	}

	private void ReadOptions()
	{
		if (!_loadingOptions && _working != null)
		{
			_working.Bar.Enabled = BarEnabledCheck.IsChecked.GetValueOrDefault();
			_working.Bar.Position = ((BarPositionCombo.SelectedIndex == 1) ? BarPosition.Bottom : BarPosition.Top);
			TaskbarSettings taskbar = _working.Taskbar;
			taskbar.Mode = TaskbarModeCombo.SelectedIndex switch
			{
				1 => TaskbarMode.AutoHide, 
				2 => TaskbarMode.Hidden, 
				_ => TaskbarMode.Normal, 
			};
			_working.Dock.Enabled = DockEnabledCheck.IsChecked.GetValueOrDefault();
			if (DockPositionCombo.SelectedIndex >= 0)
			{
				_working.Dock.Position = (DockPosition)DockPositionCombo.SelectedIndex;
			}
			if (DockBehaviorCombo.SelectedIndex >= 0)
			{
				_working.Dock.Behavior = (DockBehavior)DockBehaviorCombo.SelectedIndex;
			}
			if (DockEffectCombo.SelectedIndex >= 0)
			{
				_working.Dock.HoverEffect = (DockHoverEffect)DockEffectCombo.SelectedIndex;
			}
			_working.Island.Enabled = IslandEnabledCheck.IsChecked.GetValueOrDefault();
			_working.System.AccentOnTitleBars = TitleAccentCheck.IsChecked.GetValueOrDefault();
			_working.System.Transparency = TransparencyCheck.IsChecked.GetValueOrDefault();
			UpdatePreview();
		}
	}

	private void UpdatePreview()
	{
		if (_working != null)
		{
			ThemeProfile working = _working;
			Palette palette = working.Palette;
			bool flag = working.Mode == ColorMode.Dark;
			PreviewDesktop.Background = BrushFactory.Wallpaper(working, _app.Services.Profiles.ResolveWallpaperPath(working));
			string hex = ((working.System.AccentOnTaskbar && flag) ? Rgba.Parse(palette.Accent).Darken(0.45).ToHex() : (flag ? "#202020" : "#EEEEEE"));
			PreviewTaskbar.Background = BrushFactory.Solid(hex, working.System.Transparency ? 0.85 : 1.0);
			PreviewTaskbar.Visibility = ((working.Taskbar.Mode == TaskbarMode.Hidden) ? Visibility.Collapsed : Visibility.Visible);
			PreviewTaskbar.Height = ((working.Taskbar.Mode == TaskbarMode.AutoHide) ? 3 : 22);
			PreviewBar.Visibility = ((!working.Bar.Enabled) ? Visibility.Collapsed : Visibility.Visible);
			PreviewBar.Background = BrushFactory.Solid(palette.Background, working.Bar.Opacity);
			bool flag2 = working.Bar.Position == BarPosition.Bottom;
			PreviewBar.VerticalAlignment = (flag2 ? VerticalAlignment.Bottom : VerticalAlignment.Top);
			PreviewBar.Margin = ((flag2 && working.Taskbar.Mode == TaskbarMode.Normal) ? new Thickness(0.0, 0.0, 0.0, 22.0) : new Thickness(0.0));
			FillBarPreview(PreviewBarLeft, working.Bar.Left, working);
			FillBarPreview(PreviewBarCenter, working.Bar.Center, working);
			FillBarPreview(PreviewBarRight, working.Bar.Right, working);
			UpdateDockPreview(working);
			UpdateIslandPreview(working);
			SolidColorBrush solidColorBrush = BrushFactory.Solid(palette.Accent);
			PreviewWindow.Background = BrushFactory.Solid(palette.Background);
			PreviewWindow.BorderBrush = (working.System.AccentOnTitleBars ? solidColorBrush : BrushFactory.Solid(palette.Muted));
			string hex2 = (working.System.AccentOnTitleBars ? palette.Accent : (flag ? "#202020" : "#FFFFFF"));
			PreviewTitle.Background = BrushFactory.Solid(hex2);
			PreviewTitleText.Foreground = BrushFactory.Solid((Rgba.Parse(hex2).Luminance > 0.55) ? "#111111" : "#F5F5F5");
			FillTerminal(working);
		}
	}

	private void UpdateIslandPreview(ThemeProfile p)
	{
		IslandSettings island = p.Island;
		Palette palette = p.Palette;
		PreviewIsland.Visibility = ((!island.Enabled || island.Idle == IslandIdle.Hidden) ? Visibility.Collapsed : Visibility.Visible);
		if (!island.Enabled)
		{
			return;
		}
		int num = ((p.Bar.Enabled && p.Bar.Position == BarPosition.Top && !island.OverBar) ? 18 : 0);
		PreviewIsland.Margin = new Thickness(0.0, (double)num + (double)Math.Clamp(island.Offset, 0, 200) * 0.4, 0.0, 0.0);
		PreviewIsland.Height = Math.Clamp((double)island.Height * 0.4, 10.0, 24.0);
		PreviewIsland.Width = ((island.Idle == IslandIdle.Dot) ? (PreviewIsland.Height * 2.4) : Math.Clamp((double)island.Width * 0.4, 48.0, 240.0));
		PreviewIsland.CornerRadius = new CornerRadius(PreviewIsland.Height / 2.0);
		PreviewIsland.Background = BrushFactory.Solid(island.Background ?? palette.Background, island.Opacity);
		string hex = ((island.Background == null) ? palette.Foreground : ((Rgba.Parse(island.Background).Luminance > 0.55) ? "#111111" : "#F5F5F5"));
		PreviewIslandText.Foreground = BrushFactory.Solid(hex);
		PreviewIslandText.Visibility = ((island.Idle != 0) ? Visibility.Collapsed : Visibility.Visible);
		try
		{
			PreviewIslandText.Text = DateTime.Now.ToString(island.ClockFormat, CultureInfo.CurrentCulture);
		}
		catch (FormatException)
		{
			PreviewIslandText.Text = DateTime.Now.ToString("HH:mm");
		}
		PreviewIslandBars.Children.Clear();
		if (island.Visualizer && island.Idle == IslandIdle.Clock)
		{
			double[] array = new double[5] { 3.0, 6.0, 4.0, 7.0, 5.0 };
			foreach (double height in array)
			{
				PreviewIslandBars.Children.Add(new Border
				{
					Width = 1.6,
					Height = height,
					Margin = new Thickness(0.6, 0.0, 0.6, 0.0),
					CornerRadius = new CornerRadius(0.8),
					Background = BrushFactory.Solid(palette.Accent),
					VerticalAlignment = VerticalAlignment.Center
				});
			}
		}
	}

	private void UpdateDockPreview(ThemeProfile p)
	{
		DockSettings dock = p.Dock;
		Palette palette = p.Palette;
		PreviewDock.Visibility = ((!dock.Enabled) ? Visibility.Collapsed : Visibility.Visible);
		if (!dock.Enabled)
		{
			return;
		}
		bool flag = dock.Position != DockPosition.Bottom;
		Border previewDock = PreviewDock;
		previewDock.HorizontalAlignment = dock.Position switch
		{
			DockPosition.Left => HorizontalAlignment.Left, 
			DockPosition.Right => HorizontalAlignment.Right, 
			_ => HorizontalAlignment.Center, 
		};
		PreviewDock.VerticalAlignment = (flag ? VerticalAlignment.Center : VerticalAlignment.Bottom);
		int num = ((!flag && p.Taskbar.Mode == TaskbarMode.Normal) ? 22 : 0);
		int num2 = ((dock.Style == DockStyle.Pill || dock.Style == DockStyle.Minimal) ? 8 : 4);
		previewDock = PreviewDock;
		previewDock.Margin = dock.Position switch
		{
			DockPosition.Left => new Thickness(num2, 0.0, 0.0, 0.0), 
			DockPosition.Right => new Thickness(0.0, 0.0, num2, 0.0), 
			_ => new Thickness(0.0, 0.0, 0.0, num2 + num), 
		};
		bool flag2 = p.Mode == ColorMode.Dark;
		string hex = dock.Background ?? ((dock.Style == DockStyle.Flat) ? palette.Surface : palette.Background);
		PreviewDock.Background = ((dock.Style == DockStyle.Minimal) ? Brushes.Transparent : BrushFactory.Solid(hex, (dock.Style == DockStyle.Glass) ? Math.Min(dock.Opacity, 0.7) : dock.Opacity));
		PreviewDock.BorderBrush = BrushFactory.Solid(dock.BorderColor ?? (flag2 ? "#FFFFFF" : "#000000"), 0.12);
		PreviewDock.BorderThickness = ((dock.Style == DockStyle.Glass) ? new Thickness(1.0) : new Thickness(0.0));
		PreviewDock.CornerRadius = new CornerRadius((dock.Style == DockStyle.Pill) ? 15.0 : Math.Clamp((double)dock.CornerRadius / 2.0, 0.0, 12.0));
		StackPanel previewDockIcons = PreviewDockIcons;
		previewDockIcons.Children.Clear();
		previewDockIcons.Orientation = (flag ? Orientation.Vertical : Orientation.Horizontal);
		string[] array = new string[7] { palette.Accent, palette.Accent2, palette.Green, palette.Yellow, palette.Red, palette.Muted, palette.Foreground };
		for (int i = 0; i < array.Length; i++)
		{
			double num3 = ((dock.HoverEffect == DockHoverEffect.Magnify) ? (1.0 + (Math.Min(dock.Magnification, 2.2) - 1.0) * 0.5 * Math.Max(0.0, 1.0 - (double)Math.Abs(i - 2) / 1.6)) : 1.0);
			int num4 = ((dock.HoverEffect == DockHoverEffect.Lift && i == 2) ? (-4) : 0);
			Grid grid = new Grid
			{
				Margin = (flag ? new Thickness(0.0, 2.0, 0.0, 2.0) : new Thickness(2.0, 0.0, 2.0, 0.0))
			};
			grid.Children.Add(new Border
			{
				Width = 18.0 * num3,
				Height = 18.0 * num3,
				CornerRadius = new CornerRadius(5.0 * num3),
				Background = BrushFactory.Solid(array[i]),
				RenderTransform = new TranslateTransform(flag ? ((dock.Position == DockPosition.Left) ? (-num4) : num4) : 0, (!flag) ? num4 : 0),
				VerticalAlignment = VerticalAlignment.Bottom
			});
			bool flag3 = dock.Indicator != DockIndicator.None;
			if (flag3)
			{
				bool flag4;
				switch (i)
				{
				case 0:
				case 2:
				case 4:
					flag4 = true;
					break;
				default:
					flag4 = false;
					break;
				}
				flag3 = flag4;
			}
			if (flag3)
			{
				UIElementCollection children = grid.Children;
				Border border = new Border();
				border.Width = (flag ? 2 : ((dock.Indicator == DockIndicator.Line) ? 8 : 3));
				border.Height = ((!flag) ? 2 : ((dock.Indicator == DockIndicator.Line) ? 8 : 3));
				border.CornerRadius = new CornerRadius(1.5);
				border.Background = BrushFactory.Solid(palette.Accent);
				previewDock = border;
				previewDock.HorizontalAlignment = dock.Position switch
				{
					DockPosition.Left => HorizontalAlignment.Left, 
					DockPosition.Right => HorizontalAlignment.Right, 
					_ => HorizontalAlignment.Center, 
				};
				border.VerticalAlignment = (flag ? VerticalAlignment.Center : VerticalAlignment.Bottom);
				Border border2 = border;
				border2.Margin = dock.Position switch
				{
					DockPosition.Left => new Thickness(-5.0, 0.0, 0.0, 0.0), 
					DockPosition.Right => new Thickness(0.0, 0.0, -5.0, 0.0), 
					_ => new Thickness(0.0, 0.0, 0.0, -5.0), 
				};
				children.Add(border);
			}
			previewDockIcons.Children.Add(grid);
			if (i == 4 && dock.ShowTrash)
			{
				previewDockIcons.Children.Add(new Border
				{
					Width = (flag ? 18.0 : 1.0),
					Height = (flag ? 1.0 : 18.0),
					Margin = new Thickness(3.0),
					Background = BrushFactory.Solid(palette.Foreground, 0.25)
				});
			}
		}
	}

	private static void FillBarPreview(TextBlock target, IEnumerable<string> modules, ThemeProfile p)
	{
		TextBlock target2 = target;
		target2.Inlines.Clear();
		SolidColorBrush fg = BrushFactory.Solid(p.Palette.Foreground);
		SolidColorBrush accent = BrushFactory.Solid(p.Palette.Accent);
		using (IEnumerator<string> enumerator = modules.GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				switch (enumerator.Current.Trim().ToLowerInvariant())
				{
				case "launcher":
					target2.Inlines.Add(new Run("◆  ")
					{
						Foreground = accent
					});
					break;
				case "window":
					Add("", "pwsh — ~/projects");
					break;
				case "clock":
					Add("", SafeFormat(p.Bar.ClockFormat));
					break;
				case "cpu":
					Add("cpu", "7%");
					break;
				case "ram":
					Add("ram", "6.1G");
					break;
				case "battery":
					Add("bat", "87%");
					break;
				case "layout":
					target2.Inlines.Add(new Run("ru   ")
					{
						Foreground = accent
					});
					break;
				case "uptime":
					Add("up", "2h 14m");
					break;
				case "tray":
					target2.Inlines.Add(new Run("▴ ● ♪   ")
					{
						Foreground = fg
					});
					break;
				}
			}
		}
		void Add(string label, string value)
		{
			if (label.Length > 0)
			{
				target2.Inlines.Add(new Run(label + " ")
				{
					Foreground = accent
				});
			}
			target2.Inlines.Add(new Run(value + "   ")
			{
				Foreground = fg
			});
		}
	}

	private void FillTerminal(ThemeProfile p)
	{
		Palette palette = p.Palette;
		TextBlock previewTerminal = PreviewTerminal;
		previewTerminal.Inlines.Clear();
		previewTerminal.Foreground = BrushFactory.Solid(palette.Foreground);
		previewTerminal.Inlines.Add(R(Environment.UserName.ToLowerInvariant(), palette.Accent));
		previewTerminal.Inlines.Add(R("@", palette.Muted));
		previewTerminal.Inlines.Add(R("metamorph", palette.Accent2));
		previewTerminal.Inlines.Add(R(" ~ $ ", palette.Foreground));
		previewTerminal.Inlines.Add(R("neofetch", palette.Green));
		previewTerminal.Inlines.Add(new LineBreak());
		previewTerminal.Inlines.Add(new LineBreak());
		(string, string)[] obj = new(string, string)[8]
		{
			("OS", "Windows 10 x64"),
			("WM", "Metamorph"),
			("Theme", p.Name),
			("Bar", p.Bar.Enabled ? "polybar-like" : "off"),
			("Dock", p.Dock.Enabled ? $"{p.Dock.Style} · {p.Dock.Position} · {p.Dock.Behavior}" : "off"),
			("Island", p.Island.Enabled ? "dynamic island" : "off"),
			default((string, string)),
			default((string, string))
		};
		string text2 = p.Wallpaper.EffectiveKind.ToString().ToLowerInvariant();
		object obj2;
		if (p.Wallpaper.EffectiveKind == WallpaperKind.Video)
		{
			string path = p.Wallpaper.Path;
			if (path != null)
			{
				obj2 = " · " + Path.GetFileName(path);
				goto IL_0259;
			}
		}
		obj2 = "";
		goto IL_0259;
		IL_0259:
		obj[6] = ("Wall", text2 + (string?)obj2);
		obj[7] = ("Taskbar", p.Taskbar.Mode.ToString());
		(string, string)[] array = obj;
		for (int i = 0; i < array.Length; i++)
		{
			var (value, text3) = array[i];
			previewTerminal.Inlines.Add(R($"{value,-8}", palette.Accent));
			previewTerminal.Inlines.Add(R(text3, palette.Foreground));
			previewTerminal.Inlines.Add(new LineBreak());
		}
		previewTerminal.Inlines.Add(new LineBreak());
		string[] array2 = new string[7] { palette.Red, palette.Green, palette.Yellow, palette.Accent, palette.Accent2, palette.Muted, palette.Foreground };
		foreach (string hex2 in array2)
		{
			previewTerminal.Inlines.Add(R("███", hex2));
		}
		static Run R(string text, string hex)
		{
			return new Run(text)
			{
				Foreground = BrushFactory.Solid(hex)
			};
		}
	}

	private async void Apply_Click(object sender, RoutedEventArgs e)
	{
		if (_working != null)
		{
			await RunBusy(Loc.F("Применяю «{0}»…", _working.Name), async delegate
			{
				ApplyReport applyReport = await _app.ApplyThemeAsync(_working);
				string text = (applyReport.Success ? (" " + Loc.T("Если панель задач или рамки окон не перекрасились — нажмите «Перезапустить Проводник».")) : "");
				SetStatus(applyReport.Summary() + text, !applyReport.Success);
			});
		}
	}

	private void SaveCopy_Click(object sender, RoutedEventArgs e)
	{
		if (_working == null)
		{
			return;
		}
		try
		{
			ThemeProfile themeProfile = _app.Services.Profiles.CreateCopy(_working);
			LoadThemes(themeProfile.Id);
			SetStatus(Loc.F("Тема сохранена: {0}", themeProfile.SourcePath));
		}
		catch (Exception ex)
		{
			SetStatus(Loc.T("Не удалось сохранить тему: ") + ex.Message, error: true);
		}
	}

	private void EditJson_Click(object sender, RoutedEventArgs e)
	{
		string text = _selected?.SourcePath;
		if (text != null && File.Exists(text))
		{
			Process.Start(new ProcessStartInfo("notepad.exe", "\"" + text + "\"")
			{
				UseShellExecute = true
			});
			SetStatus(Loc.T("После сохранения JSON нажмите «Обновить список»."));
		}
	}

	private async void Restore_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show(this, Loc.T("Вернуть цвета, обои и панель задач к состоянию до первого применения темы Metamorph?"), Loc.T("Вернуть исходный вид"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			await RunBusy(Loc.T("Возвращаю исходный вид…"), async delegate
			{
				ApplyReport applyReport = await _app.RestoreOriginalAsync();
				SetStatus(applyReport.Summary(), !applyReport.Success);
			});
		}
	}

	private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
	{
		await RunBusy(Loc.T("Перезапускаю Проводник…"), async delegate
		{
			await Task.Run((Action)ExplorerService.Restart);
			SetStatus(Loc.T("Проводник перезапущен."));
		});
	}

	private void OpenThemesFolder_Click(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _app.Services.Paths.UserThemes + "\"")
		{
			UseShellExecute = true
		});
	}

	private void Reload_Click(object sender, RoutedEventArgs e)
	{
		LoadThemes();
		if (_app.Services.Profiles.LastErrors.Count == 0)
		{
			SetStatus(Loc.T("Список тем обновлён."));
		}
	}

	private void Autostart_Changed(object sender, RoutedEventArgs e)
	{
		if (_loadingOptions)
		{
			return;
		}
		string processPath = Environment.ProcessPath;
		if (processPath == null)
		{
			return;
		}
		try
		{
			AutostartService.SetEnabled(AutostartCheck.IsChecked.GetValueOrDefault(), processPath);
			SetStatus(AutostartCheck.IsChecked.GetValueOrDefault() ? Loc.T("Metamorph будет запускаться вместе с Windows.") : Loc.T("Автозапуск отключён."));
		}
		catch (Exception ex)
		{
			SetStatus(Loc.T("Автозапуск: ") + ex.Message, error: true);
		}
	}

	private async Task RunBusy(string message, Func<Task> action)
	{
		base.IsEnabled = false;
		SetStatus(message);
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			SetStatus(ex.Message, error: true);
		}
		finally
		{
			base.IsEnabled = true;
		}
	}

	private void UpdateActiveText()
	{
		TextBlock activeThemeText = ActiveThemeText;
		ThemeProfile active = _app.Session.Active;
		activeThemeText.Text = ((active != null) ? Loc.F("Активна: {0}", active.Name) : Loc.T("Тема не применена"));
	}

	private void SetStatus(string text, bool error = false)
	{
		StatusText.Text = text;
		StatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "SubTextBrush");
	}

	private static string SafeFormat(string format)
	{
		try
		{
			return DateTime.Now.ToString(format, CultureInfo.CurrentCulture);
		}
		catch (FormatException)
		{
			return DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
		}
	}

	private static T? SafeGet<T>(Func<T> getter)
	{
		try
		{
			return getter();
		}
		catch
		{
			return default(T);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Metamorph;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			ActiveThemeText = (TextBlock)target;
			break;
		case 2:
			Tabs = (TabControl)target;
			break;
		case 3:
			ThemeList = (ListBox)target;
			ThemeList.SelectionChanged += ThemeList_SelectionChanged;
			break;
		case 4:
			ThemeName = (TextBlock)target;
			break;
		case 5:
			ThemeDescription = (TextBlock)target;
			break;
		case 6:
			PreviewDesktop = (Border)target;
			break;
		case 7:
			PreviewBar = (Border)target;
			break;
		case 8:
			PreviewBarLeft = (TextBlock)target;
			break;
		case 9:
			PreviewBarCenter = (TextBlock)target;
			break;
		case 10:
			PreviewBarRight = (TextBlock)target;
			break;
		case 11:
			PreviewWindow = (Border)target;
			break;
		case 12:
			PreviewTitle = (Border)target;
			break;
		case 13:
			PreviewTitleText = (TextBlock)target;
			break;
		case 14:
			PreviewTerminal = (TextBlock)target;
			break;
		case 15:
			PreviewTaskbar = (Border)target;
			break;
		case 16:
			PreviewIsland = (Border)target;
			break;
		case 17:
			PreviewIslandText = (TextBlock)target;
			break;
		case 18:
			PreviewIslandBars = (StackPanel)target;
			break;
		case 19:
			PreviewDock = (Border)target;
			break;
		case 20:
			PreviewDockIcons = (StackPanel)target;
			break;
		case 21:
			BarEnabledCheck = (CheckBox)target;
			BarEnabledCheck.Checked += Option_Changed;
			BarEnabledCheck.Unchecked += Option_Changed;
			break;
		case 22:
			BarPositionCombo = (ComboBox)target;
			BarPositionCombo.SelectionChanged += Option_Changed;
			break;
		case 23:
			TaskbarModeCombo = (ComboBox)target;
			TaskbarModeCombo.SelectionChanged += Option_Changed;
			break;
		case 24:
			DockEnabledCheck = (CheckBox)target;
			DockEnabledCheck.Checked += Option_Changed;
			DockEnabledCheck.Unchecked += Option_Changed;
			break;
		case 25:
			DockPositionCombo = (ComboBox)target;
			DockPositionCombo.SelectionChanged += Option_Changed;
			break;
		case 26:
			DockBehaviorCombo = (ComboBox)target;
			DockBehaviorCombo.SelectionChanged += Option_Changed;
			break;
		case 27:
			DockEffectCombo = (ComboBox)target;
			DockEffectCombo.SelectionChanged += Option_Changed;
			break;
		case 28:
			IslandEnabledCheck = (CheckBox)target;
			IslandEnabledCheck.Checked += Option_Changed;
			IslandEnabledCheck.Unchecked += Option_Changed;
			break;
		case 29:
			TitleAccentCheck = (CheckBox)target;
			TitleAccentCheck.Checked += Option_Changed;
			TitleAccentCheck.Unchecked += Option_Changed;
			break;
		case 30:
			TransparencyCheck = (CheckBox)target;
			TransparencyCheck.Checked += Option_Changed;
			TransparencyCheck.Unchecked += Option_Changed;
			break;
		case 31:
			ApplyButton = (Button)target;
			ApplyButton.Click += Apply_Click;
			break;
		case 32:
			SaveCopyButton = (Button)target;
			SaveCopyButton.Click += SaveCopy_Click;
			break;
		case 33:
			EditJsonButton = (Button)target;
			EditJsonButton.Click += EditJson_Click;
			break;
		case 34:
			WallpaperTab = (TabItem)target;
			break;
		case 35:
			MascotTab = (TabItem)target;
			break;
		case 36:
			EditorTab = (TabItem)target;
			break;
		case 37:
			SettingsTab = (TabItem)target;
			break;
		case 38:
			GalleryTab = (TabItem)target;
			break;
		case 39:
			((Button)target).Click += Restore_Click;
			break;
		case 40:
			((Button)target).Click += RestartExplorer_Click;
			break;
		case 41:
			((Button)target).Click += OpenThemesFolder_Click;
			break;
		case 42:
			((Button)target).Click += Reload_Click;
			break;
		case 43:
			AutostartCheck = (CheckBox)target;
			AutostartCheck.Checked += Autostart_Changed;
			AutostartCheck.Unchecked += Autostart_Changed;
			break;
		case 44:
			StatusText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
