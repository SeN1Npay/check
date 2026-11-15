using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Metamorph.App.Dock;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Launcher;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Launcher;

internal sealed class LauncherWindow : Window
{
	private const int MaxResults = 9;

	private readonly App _app;

	private readonly TextBox _query;

	private readonly StackPanel _list = new StackPanel();

	private readonly TextBlock _hint;

	private readonly List<(LauncherItem Item, Border View)> _rows = new List<(LauncherItem, Border)>();

	private List<LauncherItem> _windows = new List<LauncherItem>();

	private int _selected;

	private Palette _palette = new Palette();

	private readonly Border _root;

	public LauncherWindow(App app)
	{
		_app = app;
		base.Title = "Metamorph Launcher";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.Width = 640.0;
		base.SizeToContent = SizeToContent.Height;
		_query = new TextBox
		{
			FontSize = 20.0,
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Padding = new Thickness(4.0, 6.0, 4.0, 6.0)
		};
		_query.TextChanged += delegate
		{
			Refresh();
		};
		_query.PreviewKeyDown += OnKey;
		_hint = new TextBlock
		{
			FontSize = 11.0,
			Margin = new Thickness(6.0, 6.0, 6.0, 0.0)
		};
		StackPanel child = new StackPanel
		{
			Children = 
			{
				(UIElement)_query,
				(UIElement)new Border
				{
					Height = 1.0,
					Margin = new Thickness(0.0, 6.0, 0.0, 6.0),
					Background = Brushes.Gray,
					Opacity = 0.3
				},
				(UIElement)_list,
				(UIElement)_hint
			}
		};
		_root = new Border
		{
			Padding = new Thickness(14.0),
			CornerRadius = new CornerRadius(14.0),
			BorderThickness = new Thickness(1.0),
			Child = child,
			Effect = new DropShadowEffect
			{
				BlurRadius = 24.0,
				ShadowDepth = 4.0,
				Opacity = 0.45
			}
		};
		base.Content = new Border
		{
			Padding = new Thickness(18.0),
			Child = _root
		};
		base.Deactivated += delegate
		{
			Hide();
		};
	}

	public void Toggle()
	{
		if (base.IsVisible)
		{
			Hide();
			return;
		}
		ApplyPalette(_app.Session.Active?.Palette ?? new Palette());
		AppCatalog.RefreshAsync().ContinueWith((Task _) => base.Dispatcher.InvokeAsync(delegate
		{
			if (base.IsVisible)
			{
				Refresh();
			}
		}));
		_windows = OpenWindows();
		_query.Text = "";
		Refresh();
		PositionOnCursorScreen();
		Show();
		Activate();
		_query.Focus();
		Keyboard.Focus(_query);
	}

	private void ApplyPalette(Palette p)
	{
		_palette = p;
		_root.Background = BrushFactory.Solid(p.Background, 0.97);
		_root.BorderBrush = BrushFactory.Solid(p.Accent, 0.8);
		_query.Foreground = BrushFactory.Solid(p.Foreground);
		_query.CaretBrush = BrushFactory.Solid(p.Accent);
		_hint.Foreground = BrushFactory.Solid(p.Muted);
		_hint.Text = Loc.T("Enter — выполнить · ↑↓ — выбор · Esc — закрыть · «=» калькулятор · «>» команды");
		base.FontFamily = new FontFamily((_app.Session.Active?.Bar.FontFamily ?? "Segoe UI") + ", Segoe UI");
	}

	private void PositionOnCursorScreen()
	{
		Win32.GetCursorPos(out var pt);
		Rect workAreaDip = (Display.All().FirstOrDefault((Display d) => d.Contains(pt.X, pt.Y)) ?? Display.PrimaryDisplay).WorkAreaDip;
		base.Left = workAreaDip.Left + (workAreaDip.Width - base.Width) / 2.0;
		base.Top = workAreaDip.Top + workAreaDip.Height * 0.18;
	}

	private IEnumerable<LauncherItem> Commands()
	{
		foreach (ThemeProfile item in _app.Services.Profiles.LoadAll())
		{
			ThemeProfile t = item;
			yield return new LauncherItem(Loc.F("Тема: {0}", t.Name), t.Description, "\ud83c\udfa8", delegate
			{
				_app.ApplyThemeAsync(t);
			});
		}
		yield return new LauncherItem(Loc.T("Metamorph: открыть"), Loc.T("Главное окно"), "◆", _app.ShowMainWindow);
		yield return new LauncherItem(Loc.T("Metamorph: показать/скрыть панель"), "", "▭", _app.Session.ToggleBar);
		yield return new LauncherItem(Loc.T("Metamorph: показать/скрыть док"), "", "▬", _app.Session.ToggleDock);
		yield return new LauncherItem(Loc.T("Metamorph: следующая тема"), "", "⟳", _app.NextTheme);
		yield return new LauncherItem(Loc.T("Metamorph: удиви меня"), Loc.T("Случайная тема"), "\ud83c\udfb2", delegate
		{
			_app.SurpriseThemeAsync();
		});
		yield return new LauncherItem(Loc.T("Metamorph: вернуть исходный вид Windows"), "", "↺", delegate
		{
			_app.RestoreOriginalAsync();
		});
		yield return new LauncherItem(Loc.T("Metamorph: собрать отчёт"), Loc.T("Диагностика"), "\ud83e\ude7a", delegate
		{
			try
			{
				_app.CreateDiagnosticsReport();
			}
			catch (Exception ex)
			{
				Log.Error("Отчёт", ex);
			}
		});
		yield return new LauncherItem(Loc.T("Перезапустить Проводник"), "explorer.exe", "⟲", ExplorerService.Restart);
		yield return new LauncherItem(Loc.T("Заблокировать"), Loc.T("Питание"), "\ud83d\udd12", PowerActions.Lock);
		yield return new LauncherItem(Loc.T("Сон"), Loc.T("Питание"), "☾", PowerActions.Sleep);
		yield return new LauncherItem(Loc.T("Выйти из системы"), Loc.T("Питание"), "⇥", PowerActions.SignOut);
		yield return new LauncherItem(Loc.T("Перезагрузка"), Loc.T("Питание"), "⟳", PowerActions.Restart);
		yield return new LauncherItem(Loc.T("Выключение"), Loc.T("Питание"), "⏻", PowerActions.Shutdown);
	}

	private static IEnumerable<LauncherItem> Apps()
	{
		return AppCatalog.Current.Select((AppEntry a) => new LauncherItem(a.Name, Loc.T("Приложение"), "▸", a.Launch, a.IconPath));
	}

	private static List<LauncherItem> OpenWindows()
	{
		uint own = (uint)Environment.ProcessId;
		List<LauncherItem> result = new List<LauncherItem>();
		Win32.EnumWindows(delegate(nint hwnd, nint _)
		{
			try
			{
				if (!Win32.IsWindowVisible(hwnd) || Win32.IsCloaked(hwnd))
				{
					return true;
				}
				if (Win32.GetAncestor(hwnd, 3u) != hwnd)
				{
					return true;
				}
				if ((NativeMethods.GetWindowLong(hwnd, -20) & 0x80) != 0L)
				{
					return true;
				}
				Win32.GetWindowThreadProcessId(hwnd, out var pid);
				if (pid == own)
				{
					return true;
				}
				int windowTextLength = NativeMethods.GetWindowTextLength(hwnd);
				if (windowTextLength == 0)
				{
					return true;
				}
				StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
				NativeMethods.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
				string text = stringBuilder.ToString();
				if ((text == "Program Manager" || (text != null && text.Length == 0)) ? true : false)
				{
					return true;
				}
				string text2;
				try
				{
					text2 = Process.GetProcessById((int)pid).ProcessName;
				}
				catch
				{
					text2 = "";
				}
				nint h = hwnd;
				result.Add(new LauncherItem(text, Loc.F("Окно · {0}", text2), "❐", delegate
				{
					Focus(h);
				}, null, h));
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		return result;
	}

	private static void Focus(nint hwnd)
	{
		if (Win32.IsIconic(hwnd))
		{
			Win32.ShowWindow(hwnd, 9);
		}
		Win32.SetForegroundWindow(hwnd);
	}

	private IEnumerable<LauncherItem> Search(string query)
	{
		string q = query.Trim();
		if (q.StartsWith('='))
		{
			string text = q;
			string text2 = text.Substring(1, text.Length - 1);
			if (Calculator.TryEvaluate(text2, out var value))
			{
				yield return CalcItem(text2, value);
			}
			yield break;
		}
		if (q.Length > 0 && Calculator.LooksLikeExpression(q) && Calculator.TryEvaluate(q, out var value2))
		{
			yield return CalcItem(q, value2);
		}
		if (q.StartsWith('>'))
		{
			IEnumerable<LauncherItem> items = Commands();
			string text = q;
			foreach (LauncherItem item in FuzzyMatcher.Filter(items, text.Substring(1, text.Length - 1), (LauncherItem i) => i.Title, 9))
			{
				yield return item;
			}
			yield break;
		}
		if (q.Length == 0)
		{
			foreach (LauncherItem item2 in _windows.Take(5))
			{
				yield return item2;
			}
			foreach (LauncherItem item3 in Apps().Take(9))
			{
				yield return item3;
			}
			yield break;
		}
		IEnumerable<LauncherItem> items2 = _windows.Concat(Apps()).Concat(Commands());
		foreach (LauncherItem item4 in FuzzyMatcher.Filter(items2, q, (LauncherItem i) => i.Title, 9))
		{
			yield return item4;
		}
		string web = _app.Services.Settings.Current.Launcher.WebSearch;
		if (!string.IsNullOrWhiteSpace(web) && web.Contains("{0}"))
		{
			yield return new LauncherItem(Loc.F("Искать в интернете: {0}", q), new Uri(web.Replace("{0}", "x")).Host, "\ud83d\udd0d", delegate
			{
				Process.Start(new ProcessStartInfo(string.Format(web, Uri.EscapeDataString(q)))
				{
					UseShellExecute = true
				});
			});
		}
	}

	private static LauncherItem CalcItem(string expr, double value)
	{
		string text = Calculator.Format(value);
		return new LauncherItem("= " + text, Loc.F("{0} · Enter — скопировать", expr.Trim()), "∑", delegate
		{
			Clipboard.SetText(text);
		});
	}

	private void Refresh()
	{
		List<LauncherItem> list;
		try
		{
			list = Search(_query.Text).Take(10).ToList();
		}
		catch (Exception ex)
		{
			Log.Error("Лаунчер: поиск", ex);
			list = new List<LauncherItem>();
		}
		_list.Children.Clear();
		_rows.Clear();
		foreach (LauncherItem item in list)
		{
			AddRow(item);
		}
		_selected = 0;
		Highlight();
	}

	private void AddRow(LauncherItem item)
	{
		SolidColorBrush foreground = BrushFactory.Solid(_palette.Foreground);
		SolidColorBrush foreground2 = BrushFactory.Solid(_palette.Muted);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(40.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		ImageSource imageSource = ((item.IconPath != null) ? SafeIcon(item.IconPath) : ((item.Window != IntPtr.Zero) ? WindowIcon(item.Window) : null));
		FrameworkElement element = ((imageSource == null) ? ((FrameworkElement)new TextBlock
		{
			Text = item.Glyph,
			FontSize = 18.0,
			Foreground = BrushFactory.Solid(_palette.Accent),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		}) : ((FrameworkElement)new Image
		{
			Source = imageSource,
			Width = 28.0,
			Height = 28.0
		}));
		grid.Children.Add(element);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = item.Title,
			FontSize = 15.0,
			Foreground = foreground,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		if (item.Subtitle.Length > 0)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = item.Subtitle,
				FontSize = 11.0,
				Foreground = foreground2,
				TextTrimming = TextTrimming.CharacterEllipsis
			});
		}
		Grid.SetColumn(stackPanel, 1);
		grid.Children.Add(stackPanel);
		Border border = new Border
		{
			Child = grid,
			Padding = new Thickness(6.0, 5.0, 6.0, 5.0),
			CornerRadius = new CornerRadius(8.0),
			Cursor = Cursors.Hand
		};
		int index = _rows.Count;
		border.MouseEnter += delegate
		{
			_selected = index;
			Highlight();
		};
		border.MouseLeftButtonUp += delegate
		{
			Execute(index);
		};
		_rows.Add((item, border));
		_list.Children.Add(border);
	}

	private static ImageSource? SafeIcon(string path)
	{
		try
		{
			return DockIcons.FromShell(path, 32);
		}
		catch
		{
			return null;
		}
	}

	private static ImageSource? WindowIcon(nint hwnd)
	{
		try
		{
			nint num = Win32.SendMessage(hwnd, 127, new IntPtr(1), IntPtr.Zero);
			if (num == IntPtr.Zero)
			{
				num = Win32.SendMessage(hwnd, 127, new IntPtr(2), IntPtr.Zero);
			}
			return (num == IntPtr.Zero) ? null : DockIcons.FromHIcon(num);
		}
		catch
		{
			return null;
		}
	}

	private void Highlight()
	{
		SolidColorBrush solidColorBrush = BrushFactory.Solid(_palette.Accent, 0.22);
		for (int i = 0; i < _rows.Count; i++)
		{
			_rows[i].View.Background = ((i == _selected) ? solidColorBrush : Brushes.Transparent);
		}
	}

	private void OnKey(object sender, KeyEventArgs e)
	{
		Key key = e.Key;
		if (key <= Key.Return)
		{
			if (key != Key.Tab)
			{
				if (key == Key.Return)
				{
					Execute(_selected);
					e.Handled = true;
				}
				return;
			}
			if (Keyboard.Modifiers != 0)
			{
				return;
			}
		}
		else
		{
			switch (key)
			{
			default:
				return;
			case Key.Escape:
				Hide();
				e.Handled = true;
				return;
			case Key.Down:
				break;
			case Key.Up:
				if (_rows.Count > 0)
				{
					_selected = (_selected - 1 + _rows.Count) % _rows.Count;
				}
				Highlight();
				e.Handled = true;
				return;
			}
		}
		if (_rows.Count > 0)
		{
			_selected = (_selected + 1) % _rows.Count;
		}
		Highlight();
		e.Handled = true;
	}

	private void Execute(int index)
	{
		if (index < 0 || index >= _rows.Count)
		{
			return;
		}
		LauncherItem item = _rows[index].Item;
		Hide();
		try
		{
			item.Execute();
		}
		catch (Exception ex)
		{
			Log.Error("Лаунчер: " + item.Title, ex);
			MessageBox.Show(ex.Message, "Metamorph", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		WindowEffects.MakeToolWindow(new WindowInteropHelper(this).Handle, noActivate: false);
	}
}
