using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

internal static class BarPopup
{
	public static Popup Create(UIElement target, BarContext context, UIElement content)
	{
		Popup obj = new Popup
		{
			PlacementTarget = target,
			Placement = ((context.Profile.Bar.Position == BarPosition.Top) ? PlacementMode.Bottom : PlacementMode.Top),
			StaysOpen = false,
			AllowsTransparency = true,
			PopupAnimation = PopupAnimation.Fade,
			Child = new Border
			{
				Background = context.Background,
				BorderBrush = context.Surface,
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(10.0),
				Padding = new Thickness(8.0),
				Margin = new Thickness(0.0, 4.0, 0.0, 4.0),
				Child = content
			}
		};
		TextElement(obj, context);
		return obj;
	}

	private static void TextElement(Popup popup, BarContext context)
	{
		System.Windows.Documents.TextElement.SetForeground(popup.Child, context.Foreground);
		System.Windows.Documents.TextElement.SetFontFamily(popup.Child, new FontFamily(context.Profile.Bar.FontFamily + ", Consolas"));
		System.Windows.Documents.TextElement.SetFontSize(popup.Child, context.Profile.Bar.FontSize);
	}

	public static void ShowMenu(UIElement target, BarContext context, IEnumerable<(string Text, Action Action)> items)
	{
		StackPanel stackPanel = new StackPanel
		{
			MinWidth = 180.0
		};
		Popup popup = null;
		SolidColorBrush hover = new SolidColorBrush(((SolidColorBrush)context.Muted).Color)
		{
			Opacity = 0.3
		};
		foreach (var item2 in items)
		{
			string text = item2.Text;
			Action item = item2.Action;
			Border row = new Border
			{
				Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
				CornerRadius = new CornerRadius(6.0),
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				Child = new TextBlock
				{
					Text = text,
					Foreground = context.Foreground
				}
			};
			row.MouseEnter += delegate
			{
				row.Background = hover;
			};
			row.MouseLeave += delegate
			{
				row.Background = Brushes.Transparent;
			};
			Action act = item;
			row.MouseLeftButtonUp += delegate
			{
				if (popup != null)
				{
					popup.IsOpen = false;
				}
				try
				{
					act();
				}
				catch (Exception ex)
				{
					Log.Error("Меню панели: " + text, ex);
				}
			};
			stackPanel.Children.Add(row);
		}
		popup = Create(target, context, stackPanel);
		popup.IsOpen = true;
	}
}
