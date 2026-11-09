using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

// Плашка «Режим редактирования» сверху по центру основного монитора.
internal sealed class CardHintWindow : Window
{
	private readonly Action _done;

	public CardHintWindow(CardTheme theme, Action done, Action<FrameworkElement> menu)
	{
		_done = done;
		base.Title = "Metamorph Cards Hint";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.SizeToContent = SizeToContent.WidthAndHeight;
		base.FontFamily = theme.Font;
		Border button = new Border
		{
			Background = theme.AccentGradient,
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(14.0, 5.0, 14.0, 6.0),
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0),
			Cursor = Cursors.Hand,
			Child = CardUi.Text(Loc.T("Готово"), 13.0, theme.OnAccent, FontWeights.SemiBold, theme.Font)
		};
		button.MouseLeftButtonUp += delegate
		{
			_done();
		};
		Border pill = new Border
		{
			Margin = new Thickness(18.0),
			Padding = new Thickness(16.0, 7.0, 7.0, 7.0),
			CornerRadius = new CornerRadius(20.0),
			Background = BrushFactory.Solid(theme.Palette.Surface, 0.95),
			BorderBrush = theme.Accent,
			BorderThickness = new Thickness(1.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 22.0,
				ShadowDepth = 4.0,
				Direction = 270.0,
				Opacity = 0.4,
				Color = Colors.Black
			},
			Child = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Children =
				{
					new Border
					{
						Width = 8.0,
						Height = 8.0,
						CornerRadius = new CornerRadius(4.0),
						Background = theme.AccentGradient,
						VerticalAlignment = VerticalAlignment.Center,
						Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
					},
					new TextBlock
					{
						Text = Loc.T("Режим редактирования — перетаскивайте карточки, Esc — готово"),
						FontSize = 13.0,
						Foreground = theme.Foreground,
						VerticalAlignment = VerticalAlignment.Center
					},
					button
				}
			}
		};
		base.Content = pill;
		base.MouseRightButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			menu(pill);
		};
		base.Loaded += delegate
		{
			Rect wa = Display.PrimaryDisplay.WorkAreaDip;
			base.Left = wa.X + (wa.Width - base.ActualWidth) / 2.0;
			base.Top = wa.Y + 4.0;
			if (DesktopActivity.CanAnimate)
			{
				TranslateTransform t = new TranslateTransform(0.0, -16.0);
				pill.RenderTransform = t;
				Duration d = new Duration(TimeSpan.FromMilliseconds(320.0));
				CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
				t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, d) { EasingFunction = ease });
				BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, d));
			}
		};
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		WindowEffects.MakeToolWindow(new WindowInteropHelper(this).Handle, noActivate: false);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			_done();
		}
	}
}

// Направляющая привязки: тонкая линия поверх всего, клики проходят насквозь.
internal sealed class CardGuideWindow : Window
{
	private readonly bool _vertical;

	public CardGuideWindow(CardTheme theme, bool vertical)
	{
		_vertical = vertical;
		base.Title = "Metamorph Cards Guide";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = BrushFactory.Solid(theme.Palette.Accent, 0.85);
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.ResizeMode = ResizeMode.NoResize;
		base.IsHitTestVisible = false;
		base.Focusable = false;
		base.Width = 1.0;
		base.Height = 1.0;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		nint hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(hwnd, noActivate: true);
		// WS_EX_TRANSPARENT — мышь проходит насквозь.
		NativeMethods.SetWindowLong(hwnd, -20, NativeMethods.GetWindowLong(hwnd, -20) | 0x20);
	}

	public void ShowAt(double pos, Rect area)
	{
		if (_vertical)
		{
			base.Left = pos - 0.75;
			base.Top = area.Y;
			base.Width = 1.5;
			base.Height = area.Height;
		}
		else
		{
			base.Left = area.X;
			base.Top = pos - 0.75;
			base.Width = area.Width;
			base.Height = 1.5;
		}
		if (!base.IsVisible)
		{
			Show();
		}
	}
}

// Контекстное меню в цветах темы (шаблоны собираются из строки — XAML-файлов в проекте нет).
internal static class CardMenu
{
	private const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

	public static (ContextMenu Menu, Style Item, Style Separator) Create(CardTheme theme)
	{
		string bg = BrushFactory.ToColor(theme.Palette.Surface, 0.97).ToString();
		string fg = BrushFactory.ToColor(theme.Palette.Foreground).ToString();
		string border = BrushFactory.ToColor(theme.Palette.Muted, 0.4).ToString();
		string hover = BrushFactory.ToColor(theme.Palette.Accent, 0.22).ToString();
		string accent = BrushFactory.ToColor(theme.Palette.Accent).ToString();
		ContextMenu menu = new ContextMenu
		{
			Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint
		};
		try
		{
			menu.Style = Parse("<Style " + Ns + " TargetType=\"ContextMenu\"><Setter Property=\"OverridesDefaultStyle\" Value=\"True\"/><Setter Property=\"HasDropShadow\" Value=\"False\"/><Setter Property=\"Foreground\" Value=\"" + fg + "\"/><Setter Property=\"FontSize\" Value=\"13\"/><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"ContextMenu\"><Border Background=\"" + bg + "\" BorderBrush=\"" + border + "\" BorderThickness=\"1\" CornerRadius=\"10\" Padding=\"4\" Margin=\"0,0,10,10\" MinWidth=\"190\"><Border.Effect><DropShadowEffect BlurRadius=\"14\" ShadowDepth=\"3\" Direction=\"270\" Opacity=\"0.35\"/></Border.Effect><StackPanel IsItemsHost=\"True\" KeyboardNavigation.DirectionalNavigation=\"Cycle\"/></Border></ControlTemplate></Setter.Value></Setter></Style>");
			Style item = Parse("<Style " + Ns + " TargetType=\"MenuItem\"><Setter Property=\"OverridesDefaultStyle\" Value=\"True\"/><Setter Property=\"Foreground\" Value=\"" + fg + "\"/><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"MenuItem\"><Grid><Border x:Name=\"Bd\" Background=\"Transparent\" CornerRadius=\"6\" Padding=\"8,6,10,6\"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width=\"20\"/><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"Auto\"/></Grid.ColumnDefinitions><TextBlock x:Name=\"Check\" Text=\"✓\" Foreground=\"" + accent + "\" Visibility=\"Hidden\" VerticalAlignment=\"Center\"/><ContentPresenter Grid.Column=\"1\" ContentSource=\"Header\" RecognizesAccessKey=\"False\" VerticalAlignment=\"Center\"/><TextBlock x:Name=\"Arrow\" Grid.Column=\"2\" Text=\"›\" Margin=\"16,0,0,0\" Opacity=\"0.7\" Visibility=\"Collapsed\" VerticalAlignment=\"Center\"/></Grid></Border><Popup x:Name=\"PART_Popup\" IsOpen=\"{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}\" Placement=\"Right\" HorizontalOffset=\"4\" VerticalOffset=\"-5\" AllowsTransparency=\"True\" Focusable=\"False\" PopupAnimation=\"Fade\"><Border Background=\"" + bg + "\" BorderBrush=\"" + border + "\" BorderThickness=\"1\" CornerRadius=\"10\" Padding=\"4\" Margin=\"0,0,10,10\" MinWidth=\"170\"><Border.Effect><DropShadowEffect BlurRadius=\"14\" ShadowDepth=\"3\" Direction=\"270\" Opacity=\"0.35\"/></Border.Effect><StackPanel IsItemsHost=\"True\" KeyboardNavigation.DirectionalNavigation=\"Cycle\"/></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property=\"Role\" Value=\"SubmenuHeader\"><Setter TargetName=\"Arrow\" Property=\"Visibility\" Value=\"Visible\"/></Trigger><Trigger Property=\"IsChecked\" Value=\"True\"><Setter TargetName=\"Check\" Property=\"Visibility\" Value=\"Visible\"/></Trigger><Trigger Property=\"IsHighlighted\" Value=\"True\"><Setter TargetName=\"Bd\" Property=\"Background\" Value=\"" + hover + "\"/></Trigger><Trigger Property=\"IsEnabled\" Value=\"False\"><Setter Property=\"Opacity\" Value=\"0.45\"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
			Style separator = Parse("<Style " + Ns + " TargetType=\"Separator\"><Setter Property=\"OverridesDefaultStyle\" Value=\"True\"/><Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"Separator\"><Border Height=\"1\" Margin=\"8,4\" Background=\"" + border + "\"/></ControlTemplate></Setter.Value></Setter></Style>");
			return (menu, item, separator);
		}
		catch (Exception ex)
		{
			// Если шаблон не разобрался — стандартное меню лучше, чем никакого.
			Log.Error("Карточки: стиль меню", ex);
			menu.Style = null;
			return (menu, new Style(typeof(MenuItem)), new Style(typeof(Separator)));
		}
	}

	private static Style Parse(string xaml)
	{
		return (Style)XamlReader.Parse(xaml);
	}
}
