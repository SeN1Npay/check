using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

// Своя шапка окна вместо системной: при «Акценте в заголовках окон» Windows 10 красит системную шапку
// в цвет акцента поверх тёмной темы, и окно выглядит чужеродно.
internal static class TitleBar
{
	private const double Height = 34.0;

	public static void Apply(Window window, string title)
	{
		try
		{
			if (window.Content is not UIElement content)
			{
				return;
			}
			window.Content = null;
			Brush fg = Find(window, "SubTextBrush", Brushes.Gainsboro);
			Brush accent = Find(window, "AccentBrush", Brushes.MediumPurple);
			Grid bar = new Grid
			{
				Height = Height,
				Background = Brushes.Transparent
			};
			bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
			bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			StackPanel caption = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
			};
			caption.Children.Add(new TextBlock
			{
				Text = "◆",
				Foreground = accent,
				FontSize = 12.0,
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			});
			caption.Children.Add(new TextBlock
			{
				Text = title,
				Foreground = fg,
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center
			});
			bar.Children.Add(caption);
			StackPanel buttons = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			Grid.SetColumn(buttons, 2);
			Button min = CaptionButton("\ue921", fg, danger: false, delegate
			{
				SystemCommands.MinimizeWindow(window);
			});
			Button max = CaptionButton("\ue922", fg, danger: false, delegate
			{
				if (window.WindowState == WindowState.Maximized)
				{
					SystemCommands.RestoreWindow(window);
				}
				else
				{
					SystemCommands.MaximizeWindow(window);
				}
			});
			Button close = CaptionButton("\ue8bb", fg, danger: true, delegate
			{
				SystemCommands.CloseWindow(window);
			});
			buttons.Children.Add(min);
			if (window.ResizeMode == ResizeMode.CanResize || window.ResizeMode == ResizeMode.CanResizeWithGrip)
			{
				buttons.Children.Add(max);
			}
			buttons.Children.Add(close);
			bar.Children.Add(buttons);
			DockPanel shell = new DockPanel
			{
				LastChildFill = true
			};
			DockPanel.SetDock(bar, Dock.Top);
			shell.Children.Add(bar);
			shell.Children.Add(content);
			Border frame = new Border
			{
				Child = shell,
				BorderThickness = new Thickness(1.0),
				BorderBrush = Find(window, "BorderBrush", Brushes.DimGray)
			};
			window.Content = frame;
			WindowChrome.SetWindowChrome(window, new WindowChrome
			{
				CaptionHeight = Height,
				ResizeBorderThickness = new Thickness(6.0),
				GlassFrameThickness = new Thickness(0.0),
				CornerRadius = new CornerRadius(0.0),
				UseAeroCaptionButtons = false
			});
			void Sync()
			{
				bool maximized = window.WindowState == WindowState.Maximized;
				// Развёрнутое окно без рамки выходит за экран на толщину рамки — компенсируем отступом.
				Thickness border = SystemParameters.WindowResizeBorderThickness;
				frame.Padding = maximized ? new Thickness(border.Left + 2.0, border.Top + 2.0, border.Right + 2.0, border.Bottom + 2.0) : new Thickness(0.0);
				frame.BorderThickness = maximized ? new Thickness(0.0) : new Thickness(1.0);
				((TextBlock)max.Content).Text = maximized ? "\ue923" : "\ue922";
			}
			window.StateChanged += delegate
			{
				Sync();
			};
			window.Activated += delegate
			{
				caption.Opacity = 1.0;
			};
			window.Deactivated += delegate
			{
				caption.Opacity = 0.6;
			};
			Sync();
		}
		catch (Exception ex)
		{
			Log.Error("Шапка окна", ex);
		}
	}

	private static Button CaptionButton(string glyph, Brush fg, bool danger, Action click)
	{
		TextBlock text = new TextBlock
		{
			Text = glyph,
			FontFamily = new FontFamily("Segoe MDL2 Assets"),
			FontSize = 10.0,
			Foreground = fg,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Brush hover = danger ? new SolidColorBrush(Color.FromRgb(232, 17, 35)) : new SolidColorBrush(Color.FromArgb(40, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		hover.Freeze();
		FrameworkElementFactory root = new FrameworkElementFactory(typeof(Border), "Bd");
		root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		root.AppendChild(presenter);
		ControlTemplate template = new ControlTemplate(typeof(Button))
		{
			VisualTree = root
		};
		Trigger over = new Trigger
		{
			Property = UIElement.IsMouseOverProperty,
			Value = true
		};
		over.Setters.Add(new Setter(Border.BackgroundProperty, hover, "Bd"));
		template.Triggers.Add(over);
		Button button = new Button
		{
			Content = text,
			Width = 46.0,
			Height = Height,
			Template = template,
			Focusable = false,
			Cursor = Cursors.Arrow
		};
		WindowChrome.SetIsHitTestVisibleInChrome(button, value: true);
		if (danger)
		{
			button.MouseEnter += delegate
			{
				text.Foreground = Brushes.White;
			};
			button.MouseLeave += delegate
			{
				text.Foreground = fg;
			};
		}
		button.Click += delegate
		{
			click();
		};
		return button;
	}

	private static Brush Find(FrameworkElement scope, string key, Brush fallback)
	{
		return (scope.TryFindResource(key) as Brush) ?? fallback;
	}
}
