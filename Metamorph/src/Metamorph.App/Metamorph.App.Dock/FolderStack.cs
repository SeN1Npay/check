using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Dock;

internal static class FolderStack
{
	private const int MaxEntries = 30;

	public static Popup Show(UIElement anchor, string folder, DockPosition position, Palette palette)
	{
		string folder2 = folder;
		SolidColorBrush foreground = BrushFactory.Solid(palette.Foreground);
		SolidColorBrush hover = BrushFactory.Solid(palette.Accent, 0.25);
		WrapPanel wrapPanel = new WrapPanel
		{
			Width = 440.0
		};
		Popup popup = null;
		IEnumerable<string> enumerable;
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(folder2);
			enumerable = (from d in directoryInfo.EnumerateDirectories().Where(Visible).OrderBy<DirectoryInfo, string>((DirectoryInfo d) => d.Name, StringComparer.CurrentCultureIgnoreCase)
				select d.FullName).Concat(from f in directoryInfo.EnumerateFiles().Where(Visible)
				orderby f.LastWriteTimeUtc descending
				select f.FullName).Take(30).ToList();
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			enumerable = Array.Empty<string>();
		}
		foreach (string item in enumerable)
		{
			string fileName = Path.GetFileName(item);
			StackPanel stackPanel = new StackPanel
			{
				Width = 80.0
			};
			ImageSource imageSource = DockIcons.FromShell(item, 48);
			if (imageSource != null)
			{
				stackPanel.Children.Add(new Image
				{
					Source = imageSource,
					Width = 40.0,
					Height = 40.0,
					Margin = new Thickness(0.0, 2.0, 0.0, 4.0)
				});
			}
			stackPanel.Children.Add(new TextBlock
			{
				Text = fileName,
				Foreground = foreground,
				FontSize = 11.0,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				TextTrimming = TextTrimming.CharacterEllipsis,
				MaxHeight = 30.0
			});
			Border button = new Border
			{
				Child = stackPanel,
				Padding = new Thickness(4.0),
				Margin = new Thickness(2.0),
				CornerRadius = new CornerRadius(6.0),
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				ToolTip = item
			};
			button.MouseEnter += delegate
			{
				button.Background = hover;
			};
			button.MouseLeave += delegate
			{
				button.Background = Brushes.Transparent;
			};
			string target = item;
			button.MouseLeftButtonUp += delegate
			{
				if (popup != null)
				{
					popup.IsOpen = false;
				}
				Open(target);
			};
			wrapPanel.Children.Add(button);
		}
		StackPanel stackPanel2 = new StackPanel();
		UIElementCollection children = stackPanel2.Children;
		TextBlock textBlock = new TextBlock();
		string fileName2 = Path.GetFileName(folder2.TrimEnd('\\'));
		textBlock.Text = ((fileName2 != null && fileName2.Length > 0) ? fileName2 : folder2);
		textBlock.Foreground = BrushFactory.Solid(palette.Accent);
		textBlock.FontWeight = FontWeights.SemiBold;
		textBlock.Margin = new Thickness(6.0, 0.0, 6.0, 6.0);
		children.Add(textBlock);
		if (wrapPanel.Children.Count == 0)
		{
			stackPanel2.Children.Add(new TextBlock
			{
				Text = Loc.T("Папка пуста"),
				Foreground = BrushFactory.Solid(palette.Muted),
				Margin = new Thickness(6.0)
			});
		}
		else
		{
			stackPanel2.Children.Add(new ScrollViewer
			{
				Content = wrapPanel,
				MaxHeight = 420.0,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			});
		}
		TextBlock textBlock2 = new TextBlock
		{
			Text = Loc.T("Открыть в Проводнике") + " →",
			Foreground = BrushFactory.Solid(palette.Accent2),
			Margin = new Thickness(6.0, 8.0, 6.0, 0.0),
			Cursor = Cursors.Hand
		};
		textBlock2.MouseLeftButtonUp += delegate
		{
			if (popup != null)
			{
				popup.IsOpen = false;
			}
			Open(folder2);
		};
		stackPanel2.Children.Add(textBlock2);
		Popup popup2 = new Popup();
		popup2.PlacementTarget = anchor;
		Popup popup3 = popup2;
		popup3.Placement = position switch
		{
			DockPosition.Left => PlacementMode.Right, 
			DockPosition.Right => PlacementMode.Left, 
			_ => PlacementMode.Top, 
		};
		popup2.StaysOpen = false;
		popup2.AllowsTransparency = true;
		popup2.PopupAnimation = PopupAnimation.Fade;
		popup2.Child = new Border
		{
			Child = stackPanel2,
			Background = BrushFactory.Solid(palette.Surface, 0.98),
			BorderBrush = BrushFactory.Solid(palette.Muted, 0.8),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(10.0),
			Margin = new Thickness(12.0)
		};
		popup = popup2;
		popup.IsOpen = true;
		return popup;
	}

	private static bool Visible(FileSystemInfo info)
	{
		return (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
	}

	private static void Open(string path)
	{
		try
		{
			Process.Start(new ProcessStartInfo(path)
			{
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception ex)
		{
			Log.Error("Стопка: " + path, ex);
		}
	}
}
