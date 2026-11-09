using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Metamorph.App.Views;
using Metamorph.Core.Localization;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Remote;

internal sealed class BridgeWindow : Window
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromHours(4.0);

	private readonly BridgeHost _host;

	private readonly TextBlock _status = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		FontWeight = FontWeights.SemiBold,
		Margin = new Thickness(0.0, 8.0, 0.0, 6.0)
	};

	private readonly TextBox _link = new TextBox
	{
		IsReadOnly = true,
		FontFamily = new FontFamily("Consolas"),
		FontSize = 13.0,
		Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
		VerticalContentAlignment = VerticalAlignment.Center
	};

	private readonly Button _copy;

	private readonly TextBlock _timer = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
	};

	private readonly ListBox _log = new ListBox
	{
		MinHeight = 120.0,
		BorderThickness = new Thickness(1.0)
	};

	private readonly DispatcherTimer _tick = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private DateTime _deadline = DateTime.Now + Lifetime;

	private bool _stopped;

	public BridgeWindow(AppPaths paths)
	{
		base.Title = Loc.T("Metamorph — удалённая помощь");
		base.Width = 620.0;
		base.Height = 700.0;
		base.MinWidth = 480.0;
		base.MinHeight = 520.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.Background = Ui.Res("BaseBrush");
		base.Foreground = Ui.Res("TextBrush");
		try
		{
			base.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/metamorph.ico"));
		}
		catch
		{
		}
		_log.Background = Ui.Res("PanelBrush");
		_log.Foreground = Ui.Res("TextBrush");
		_log.BorderBrush = Ui.Res("BorderBrush");
		_log.FontFamily = new FontFamily("Consolas");
		_log.FontSize = 12.0;
		_host = new BridgeHost(paths, OnBridgeAction);
		_copy = Ui.Button("Копировать", CopyLink, primary: true);
		_copy.Margin = new Thickness(0.0);
		_copy.IsEnabled = false;
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)Ui.Header("Удалённая помощь"),
				(UIElement)Ui.Hint("Пока это окно открыто, ИИ-помощник по ссылке ниже может видеть экран, читать журналы Metamorph, выполнять команды и ставить исправленные сборки — чтобы найти и починить ошибку прямо на этом компьютере."),
				(UIElement)Ui.Hint("1. Дождитесь ссылки.  2. Нажмите «Копировать» и отправьте её в чат ИИ-помощнику.  3. Когда закончите — закройте окно: доступ сразу прекратится."),
				(UIElement)new TextBlock
				{
					Text = Loc.T("Не показывайте ссылку посторонним: по ней можно управлять этим компьютером."),
					Foreground = Ui.Res("DangerBrush"),
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0.0, 2.0, 0.0, 4.0)
				},
				(UIElement)_status
			}
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		DockPanel.SetDock(_copy, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(_copy);
		dockPanel.Children.Add(_link);
		stackPanel.Children.Add(dockPanel);
		CheckBox checkBox = new CheckBox
		{
			Content = Loc.T("Разрешить команды PowerShell, доступ к файлам и установку сборок"),
			IsChecked = true,
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
		};
		checkBox.Checked += delegate
		{
			_host.Permissions.Commands = true;
		};
		checkBox.Unchecked += delegate
		{
			_host.Permissions.Commands = false;
		};
		CheckBox checkBox2 = new CheckBox
		{
			Content = Loc.T("Разрешить управление мышью и клавиатурой"),
			IsChecked = true,
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
		};
		checkBox2.Checked += delegate
		{
			_host.Permissions.Input = true;
		};
		checkBox2.Unchecked += delegate
		{
			_host.Permissions.Input = false;
		};
		stackPanel.Children.Add(checkBox);
		stackPanel.Children.Add(checkBox2);
		stackPanel.Children.Add(Ui.Hint("Снимки экрана и журналы Metamorph доступны всегда, пока окно открыто. Само это окно на снимки не попадает."));
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
		};
		Button button = Ui.Button("+2 часа", delegate
		{
			_deadline = _deadline.AddHours(2.0);
		});
		button.Margin = new Thickness(0.0);
		DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
		dockPanel2.Children.Add(button);
		dockPanel2.Children.Add(_timer);
		stackPanel.Children.Add(dockPanel2);
		stackPanel.Children.Add(Ui.Label("Действия ИИ-помощника", 400.0));
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Button element = Ui.Button("Свернуть", delegate
		{
			base.WindowState = WindowState.Minimized;
		});
		Button button2 = Ui.Button("Остановить и закрыть", base.Close);
		button2.Foreground = Ui.Res("DangerBrush");
		wrapPanel.Children.Add(element);
		wrapPanel.Children.Add(button2);
		DockPanel dockPanel3 = new DockPanel
		{
			Margin = new Thickness(22.0, 16.0, 22.0, 14.0)
		};
		DockPanel.SetDock(stackPanel, System.Windows.Controls.Dock.Top);
		DockPanel.SetDock(wrapPanel, System.Windows.Controls.Dock.Bottom);
		dockPanel3.Children.Add(stackPanel);
		dockPanel3.Children.Add(wrapPanel);
		dockPanel3.Children.Add(_log);
		base.Content = dockPanel3;
		_host.StatusChanged += delegate(string text, bool ok)
		{
			string text2 = text;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				_status.Text = text2;
				_status.Foreground = (ok ? Ui.Res("AccentBrush") : Ui.Res("TextBrush"));
			});
		};
		_host.UrlChanged += delegate
		{
			base.Dispatcher.BeginInvoke(new Action(UpdateLink));
		};
		_host.Activity += delegate(string line)
		{
			string line2 = line;
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				AddLog(line2);
			});
		};
		_tick.Tick += delegate
		{
			UpdateTimer();
		};
		base.SourceInitialized += delegate
		{
			nint handle = new WindowInteropHelper(this).Handle;
			WindowEffects.UseDarkTitleBar(handle, dark: true);
			RemoteNative.ExcludeFromCapture(handle);
		};
		base.Loaded += delegate
		{
			_status.Text = Loc.T("Запускаю…");
			UpdateLink();
			UpdateTimer();
			_tick.Start();
			_host.Start();
			AddLog(Loc.F("Локальный адрес (для ИИ на этом ПК): {0}", _host.LocalMcpUrl));
		};
	}

	private void UpdateLink()
	{
		string mcpUrl = _host.McpUrl;
		_link.Text = mcpUrl ?? Loc.T("ссылка ещё создаётся…");
		_copy.IsEnabled = mcpUrl != null;
		if (mcpUrl != null)
		{
			AddLog(Loc.T("Ссылка готова"));
		}
	}

	private void UpdateTimer()
	{
		TimeSpan timeSpan = _deadline - DateTime.Now;
		if (timeSpan <= TimeSpan.Zero)
		{
			Log.Info("Удалённая помощь: время вышло");
			Close();
		}
		else
		{
			_timer.Text = Loc.F("Автоотключение через {0}", timeSpan.ToString("h\\:mm\\:ss"));
		}
	}

	private void AddLog(string line)
	{
		_log.Items.Add($"{DateTime.Now:HH:mm:ss}  {line}");
		while (_log.Items.Count > 300)
		{
			_log.Items.RemoveAt(0);
		}
		ListBox log = _log;
		ItemCollection items = _log.Items;
		log.ScrollIntoView(items[items.Count - 1]);
	}

	private void CopyLink()
	{
		string mcpUrl = _host.McpUrl;
		if (mcpUrl == null)
		{
			return;
		}
		for (int i = 0; i < 5; i++)
		{
			try
			{
				Clipboard.SetText(mcpUrl);
				_copy.Content = Loc.T("Скопировано ✓");
				DispatcherTimer reset = new DispatcherTimer
				{
					Interval = TimeSpan.FromSeconds(2.0)
				};
				reset.Tick += delegate
				{
					reset.Stop();
					_copy.Content = Loc.T("Копировать");
				};
				reset.Start();
				return;
			}
			catch (COMException)
			{
				Thread.Sleep(60);
			}
		}
		_link.Focus();
		_link.SelectAll();
	}

	private string OnBridgeAction(string action)
	{
		string action2 = action;
		return base.Dispatcher.Invoke(delegate
		{
			switch (action2.ToLowerInvariant())
			{
			case "minimize":
				base.WindowState = WindowState.Minimized;
				return "minimized";
			case "restore":
				base.WindowState = WindowState.Normal;
				Activate();
				return "restored";
			case "extend":
				_deadline = _deadline.AddHours(2.0);
				UpdateTimer();
				return $"auto-stop at {_deadline:HH:mm}";
			default:
				return $"window {base.WindowState}, auto-stop at {_deadline:HH:mm}, commands={_host.Permissions.Commands}, input={_host.Permissions.Input}, provider url {_host.PublicBase ?? "(none)"}";
			}
		});
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (!e.Cancel && !_stopped)
		{
			_stopped = true;
			_tick.Stop();
			_host.Dispose();
		}
	}
}
