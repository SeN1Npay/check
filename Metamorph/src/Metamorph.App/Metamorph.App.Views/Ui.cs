using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Metamorph.Core.Localization;

namespace Metamorph.App.Views;

internal static class Ui
{
	public static Brush Res(string key)
	{
		return (Brush)Application.Current.FindResource(key);
	}

	public static TextBlock Header(string text)
	{
		return new TextBlock
		{
			Text = Loc.T(text),
			Style = (Style)Application.Current.FindResource("SectionHeader")
		};
	}

	public static TextBlock Hint(string text)
	{
		return new TextBlock
		{
			Text = Loc.T(text),
			Foreground = Res("SubTextBrush"),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
		};
	}

	public static TextBlock Label(string text, double width = 220.0)
	{
		return new TextBlock
		{
			Text = Loc.T(text),
			Width = width,
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
	}

	public static Button Button(string text, Action click, bool primary = false)
	{
		Action click2 = click;
		Button button = new Button
		{
			Content = Loc.T(text),
			Margin = new Thickness(0.0, 0.0, 8.0, 8.0)
		};
		if (primary)
		{
			button.Style = (Style)Application.Current.FindResource("PrimaryButton");
		}
		button.Click += delegate
		{
			click2();
		};
		return button;
	}

	public static DockPanel Row(string label, UIElement editor, string? tooltip = null, double labelWidth = 220.0)
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(0.0, 3.0, 0.0, 3.0),
			LastChildFill = true
		};
		TextBlock textBlock = Label(label, labelWidth);
		if (tooltip != null)
		{
			textBlock.ToolTip = Loc.T(tooltip);
		}
		DockPanel.SetDock(textBlock, System.Windows.Controls.Dock.Left);
		obj.Children.Add(textBlock);
		obj.Children.Add(editor);
		return obj;
	}

	public static Border Card(UIElement child)
	{
		return new Border
		{
			Background = Res("PanelBrush"),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(20.0),
			Child = child
		};
	}

	public static TextBox TextBox(string? text, double width = double.NaN)
	{
		return new TextBox
		{
			Text = (text ?? ""),
			Width = width,
			HorizontalAlignment = (double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left)
		};
	}
}
