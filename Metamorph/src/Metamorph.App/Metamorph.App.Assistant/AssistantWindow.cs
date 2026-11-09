using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Assistant;

internal sealed class AssistantWindow : Window
{
	private readonly App _app;

	private readonly StackPanel _messages = new StackPanel
	{
		Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
	};

	private readonly ScrollViewer _scroll;

	private readonly TextBox _input = new TextBox
	{
		FontSize = 14.0,
		Padding = new Thickness(8.0, 6.0, 8.0, 6.0),
		VerticalContentAlignment = VerticalAlignment.Center
	};

	private readonly Button _send;

	private readonly Button _mic;

	private readonly WrapPanel _chips = new WrapPanel
	{
		Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
	};

	private readonly TextBlock _title = new TextBlock
	{
		FontSize = 15.0,
		FontWeight = FontWeights.SemiBold,
		VerticalAlignment = VerticalAlignment.Center
	};

	private bool _busy;

	private bool _closingForReal;

	public AssistantWindow(App app)
	{
		_app = app;
		base.Title = "Metamorph — " + Loc.T("Помощник");
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowInTaskbar = false;
		base.Topmost = true;
		base.Width = 380.0;
		base.Height = 460.0;
		Border border = new Border
		{
			Background = Res("PanelBrush"),
			BorderBrush = Res("BorderBrush"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(14.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 18.0,
				ShadowDepth = 3.0,
				Opacity = 0.4
			},
			Margin = new Thickness(10.0)
		};
		DockPanel dockPanel = (DockPanel)(border.Child = new DockPanel());
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		Button button = new Button
		{
			Content = "✕",
			Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
			Margin = new Thickness(0.0),
			ToolTip = Loc.T("Закрыть (Esc)")
		};
		button.Click += delegate
		{
			Hide();
		};
		Button button2 = new Button
		{
			Content = "⚙",
			Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
			ToolTip = Loc.T("Настройки помощника")
		};
		button2.Click += delegate
		{
			Hide();
			_app.ShowMascotTab();
		};
		DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
		DockPanel.SetDock(button2, System.Windows.Controls.Dock.Right);
		dockPanel2.Children.Add(button);
		dockPanel2.Children.Add(button2);
		dockPanel2.Children.Add(_title);
		dockPanel2.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
			{
				DragMove();
			}
		};
		DockPanel.SetDock(dockPanel2, System.Windows.Controls.Dock.Top);
		dockPanel.Children.Add(dockPanel2);
		DockPanel dockPanel3 = new DockPanel
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		_send = new Button
		{
			Content = "➤",
			Padding = new Thickness(12.0, 4.0, 12.0, 4.0),
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			ToolTip = Loc.T("Отправить (Enter)")
		};
		_send.Style = (Style)Application.Current.FindResource("PrimaryButton");
		_send.Click += delegate
		{
			Send(_input.Text);
		};
		_mic = new Button
		{
			Content = "\ud83c\udfa4",
			Padding = new Thickness(10.0, 4.0, 10.0, 4.0),
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			ToolTip = Loc.T("Сказать голосом (экспериментально)")
		};
		_mic.Click += async delegate
		{
			await ListenAsync();
		};
		DockPanel.SetDock(_send, System.Windows.Controls.Dock.Right);
		DockPanel.SetDock(_mic, System.Windows.Controls.Dock.Right);
		dockPanel3.Children.Add(_send);
		dockPanel3.Children.Add(_mic);
		dockPanel3.Children.Add(_input);
		StackPanel element = new StackPanel
		{
			Children = 
			{
				(UIElement)dockPanel3,
				(UIElement)_chips
			}
		};
		DockPanel.SetDock(element, System.Windows.Controls.Dock.Bottom);
		dockPanel.Children.Add(element);
		_scroll = new ScrollViewer
		{
			Content = _messages,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		dockPanel.Children.Add(_scroll);
		base.Content = border;
		string[] array = new string[5] { "Удиви меня", "Что играет?", "Таймер на 5 минут", "Пошути", "Что ты умеешь?" };
		foreach (string ru in array)
		{
			string text = Loc.T(ru);
			Button button3 = new Button
			{
				Content = text,
				Padding = new Thickness(8.0, 2.0, 8.0, 2.0),
				Margin = new Thickness(0.0, 0.0, 6.0, 6.0),
				FontSize = 12.0
			};
			button3.Click += delegate
			{
				Send(text);
			};
			_chips.Children.Add(button3);
		}
		_input.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				Send(_input.Text);
			}
		};
		base.PreviewKeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				Hide();
			}
		};
		base.SourceInitialized += delegate
		{
			WindowEffects.MakeToolWindow(new WindowInteropHelper(this).Handle, noActivate: false);
		};
		base.Closing += delegate(object? _, CancelEventArgs e)
		{
			if (!_closingForReal)
			{
				e.Cancel = true;
				Hide();
			}
		};
	}

	public new void Close()
	{
		_closingForReal = true;
		base.Close();
	}

	public void Toggle()
	{
		if (base.IsVisible && base.IsActive)
		{
			Hide();
			return;
		}
		AppSettings current = _app.Services.Settings.Current;
		_title.Text = Loc.F("{0} — помощник", current.Mascot.Name);
		_mic.Visibility = ((!current.Assistant.VoiceInput) ? Visibility.Collapsed : Visibility.Visible);
		if (_messages.Children.Count == 0)
		{
			Add(Loc.F("Привет! Я {0}. Напиши команду или просто поболтаем.", current.Mascot.Name) + (current.Assistant.Llm.Enabled ? "" : (" " + Loc.T("(Для свободного разговора подключите языковую модель в настройках.)"))), mine: false);
		}
		Place();
		Show();
		Activate();
		_input.Focus();
		Keyboard.Focus(_input);
	}

	private void Place()
	{
		Rect workAreaDip = Display.PrimaryDisplay.WorkAreaDip;
		MascotBrain mascotBrain = _app.MascotBrain;
		double value;
		double num;
		if (mascotBrain != null)
		{
			value = mascotBrain.X - base.Width / 2.0;
			num = mascotBrain.Y - mascotBrain.Height - base.Height - 20.0;
			if (num < workAreaDip.Top)
			{
				num = workAreaDip.Top + 10.0;
			}
		}
		else
		{
			value = workAreaDip.Right - base.Width - 20.0;
			num = workAreaDip.Bottom - base.Height - 20.0;
		}
		base.Left = Math.Clamp(value, workAreaDip.Left, Math.Max(workAreaDip.Left, workAreaDip.Right - base.Width));
		base.Top = Math.Clamp(num, workAreaDip.Top, Math.Max(workAreaDip.Top, workAreaDip.Bottom - base.Height));
	}

	private async void Send(string text)
	{
		text = text.Trim();
		if (text.Length == 0 || _busy)
		{
			return;
		}
		_busy = true;
		_input.Text = "";
		_chips.Visibility = Visibility.Collapsed;
		Add(text, mine: true);
		TextBlock typing = Add("…", mine: false);
		try
		{
			typing.Text = (await _app.AskAssistantAsync(text)).Text;
		}
		catch (Exception ex)
		{
			Log.Error("Чат помощника", ex);
			typing.Text = Loc.T("Ой, что-то пошло не так.");
		}
		finally
		{
			_busy = false;
			_scroll.ScrollToEnd();
			_input.Focus();
		}
	}

	private async Task ListenAsync()
	{
		if (_busy)
		{
			return;
		}
		_mic.IsEnabled = false;
		_mic.Content = "\ud83d\udc42";
		_app.MascotBrain?.Say(Loc.T("Слушаю…"), 6.0);
		try
		{
			var (text, text2) = await Voice.ListenOnceAsync();
			if (text != null)
			{
				Send(text);
			}
			else if (text2 != null)
			{
				Add(text2, mine: false);
			}
		}
		finally
		{
			_mic.IsEnabled = true;
			_mic.Content = "\ud83c\udfa4";
		}
	}

	private TextBlock Add(string text, bool mine)
	{
		TextBlock textBlock = new TextBlock
		{
			Text = text,
			TextWrapping = TextWrapping.Wrap,
			FontSize = 13.5,
			Foreground = (mine ? Res("AccentTextBrush") : Res("TextBrush"))
		};
		Border element = new Border
		{
			Child = textBlock,
			Background = (mine ? Res("AccentBrush") : Res("CardBrush")),
			CornerRadius = (mine ? new CornerRadius(14.0, 14.0, 4.0, 14.0) : new CornerRadius(14.0, 14.0, 14.0, 4.0)),
			Padding = new Thickness(11.0, 7.0, 11.0, 8.0),
			Margin = (mine ? new Thickness(48.0, 3.0, 0.0, 3.0) : new Thickness(0.0, 3.0, 48.0, 3.0)),
			HorizontalAlignment = (mine ? HorizontalAlignment.Right : HorizontalAlignment.Left)
		};
		_messages.Children.Add(element);
		while (_messages.Children.Count > 60)
		{
			_messages.Children.RemoveAt(0);
		}
		_scroll.ScrollToEnd();
		return textBlock;
	}

	private static Brush Res(string key)
	{
		return (Application.Current.TryFindResource(key) as Brush) ?? Brushes.Gray;
	}
}
