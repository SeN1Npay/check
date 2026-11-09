using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Dock;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Dock;

internal sealed class WindowPreviewWindow : Window
{
	private const double ThumbWidth = 220.0;

	private const double ThumbHeight = 132.0;

	private readonly List<(nint Source, Border Slot)> _slots = new List<(nint, Border)>();

	private readonly List<nint> _thumbnails = new List<nint>();

	private readonly DispatcherTimer _watch = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(200.0)
	};

	private readonly Func<bool> _keepOpen;

	private readonly Rect _anchor;

	private readonly DockPosition _position;

	private nint _hwnd;

	public WindowPreviewWindow(IReadOnlyList<TrackedWindow> windows, Rect anchorDip, DockPosition position, Palette palette, Func<bool> keepOpen)
	{
		_keepOpen = keepOpen;
		_anchor = anchorDip;
		_position = position;
		base.Title = "Metamorph Preview";
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.Topmost = true;
		base.SizeToContent = SizeToContent.WidthAndHeight;
		base.Background = BrushFactory.Solid(palette.Surface);
		base.BorderBrush = BrushFactory.Solid(palette.Muted, 0.8);
		base.BorderThickness = new Thickness(1.0);
		base.FontFamily = new FontFamily("Segoe UI");
		base.FontSize = 12.0;
		SolidColorBrush hover = BrushFactory.Solid(palette.Accent, 0.25);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = ((position != 0) ? Orientation.Vertical : Orientation.Horizontal),
			Margin = new Thickness(6.0)
		};
		foreach (TrackedWindow item in windows.Take(6))
		{
			Border border = new Border
			{
				Width = 220.0,
				Height = 132.0,
				Background = BrushFactory.Solid(palette.Background)
			};
			TextBlock element = new TextBlock
			{
				Text = (string.IsNullOrWhiteSpace(item.Title) ? "—" : item.Title),
				Foreground = BrushFactory.Solid(palette.Foreground),
				TextTrimming = TextTrimming.CharacterEllipsis,
				Width = 220.0,
				Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
			};
			StackPanel child = new StackPanel
			{
				Children = 
				{
					(UIElement)border,
					(UIElement)element
				}
			};
			Border frame = new Border
			{
				Child = child,
				Padding = new Thickness(6.0),
				Margin = new Thickness(2.0),
				CornerRadius = new CornerRadius(6.0),
				Background = Brushes.Transparent,
				Cursor = Cursors.Hand,
				ToolTip = item.Title
			};
			nint handle = item.Handle;
			frame.MouseEnter += delegate
			{
				frame.Background = hover;
			};
			frame.MouseLeave += delegate
			{
				frame.Background = Brushes.Transparent;
			};
			frame.MouseUp += delegate(object _, MouseButtonEventArgs e)
			{
				try
				{
					if (e.ChangedButton == MouseButton.Left)
					{
						WindowActions.Activate(handle);
						Close();
					}
					else if (e.ChangedButton == MouseButton.Middle)
					{
						WindowActions.Close(handle);
						Close();
					}
				}
				catch (Exception ex2)
				{
					Log.Error("Превью окна", ex2);
				}
			};
			stackPanel.Children.Add(frame);
			_slots.Add((handle, border));
		}
		base.Content = stackPanel;
		_watch.Tick += delegate
		{
			try
			{
				if (!base.IsMouseOver && !_keepOpen())
				{
					Close();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Превью окна: проверка", ex);
				Close();
			}
		};
		base.Loaded += delegate
		{
			Place();
			RegisterThumbnails();
			_watch.Start();
		};
	}

	private void Place()
	{
		double actualWidth = base.ActualWidth;
		double actualHeight = base.ActualHeight;
		Rect rect = ((SystemParameters.VirtualScreenWidth > 0.0) ? new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight) : new Rect(0.0, 0.0, 1920.0, 1080.0));
		double value;
		double value2;
		switch (_position)
		{
		case DockPosition.Left:
			value = _anchor.Right + 10.0;
			value2 = _anchor.Top + _anchor.Height / 2.0 - actualHeight / 2.0;
			break;
		case DockPosition.Right:
			value = _anchor.Left - actualWidth - 10.0;
			value2 = _anchor.Top + _anchor.Height / 2.0 - actualHeight / 2.0;
			break;
		default:
			value = _anchor.Left + _anchor.Width / 2.0 - actualWidth / 2.0;
			value2 = _anchor.Top - actualHeight - 10.0;
			break;
		}
		base.Left = Math.Clamp(value, rect.Left, Math.Max(rect.Left, rect.Right - actualWidth));
		base.Top = Math.Clamp(value2, rect.Top, Math.Max(rect.Top, rect.Bottom - actualHeight));
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
	}

	private void RegisterThumbnails()
	{
		double valueOrDefault = (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11).GetValueOrDefault(1.0);
		foreach (var (num, border) in _slots)
		{
			if (Win32.IsWindow(num) && Win32.DwmRegisterThumbnail(_hwnd, num, out var thumbnail) == 0 && thumbnail != IntPtr.Zero)
			{
				_thumbnails.Add(thumbnail);
				Win32.DwmQueryThumbnailSourceSize(thumbnail, out var size);
				Point point = border.TransformToAncestor(this).Transform(new Point(0.0, 0.0));
				double num2 = border.ActualWidth * valueOrDefault;
				double num3 = border.ActualHeight * valueOrDefault;
				double val = ((size.cx > 0 && size.cy > 0) ? Math.Min(num2 / (double)size.cx, num3 / (double)size.cy) : 1.0);
				double num4 = ((size.cx > 0) ? ((double)size.cx * Math.Min(1.0, val)) : num2);
				double num5 = ((size.cy > 0) ? ((double)size.cy * Math.Min(1.0, val)) : num3);
				double num6 = point.X * valueOrDefault + (num2 - num4) / 2.0;
				double num7 = point.Y * valueOrDefault + (num3 - num5) / 2.0;
				Win32.DWM_THUMBNAIL_PROPERTIES dWM_THUMBNAIL_PROPERTIES = default(Win32.DWM_THUMBNAIL_PROPERTIES);
				dWM_THUMBNAIL_PROPERTIES.dwFlags = 29;
				dWM_THUMBNAIL_PROPERTIES.rcDestination = new NativeMethods.RECT
				{
					Left = (int)num6,
					Top = (int)num7,
					Right = (int)(num6 + num4),
					Bottom = (int)(num7 + num5)
				};
				dWM_THUMBNAIL_PROPERTIES.opacity = byte.MaxValue;
				dWM_THUMBNAIL_PROPERTIES.fVisible = true;
				dWM_THUMBNAIL_PROPERTIES.fSourceClientAreaOnly = false;
				Win32.DWM_THUMBNAIL_PROPERTIES props = dWM_THUMBNAIL_PROPERTIES;
				Win32.DwmUpdateThumbnailProperties(thumbnail, ref props);
			}
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		_watch.Stop();
		foreach (nint thumbnail in _thumbnails)
		{
			Win32.DwmUnregisterThumbnail(thumbnail);
		}
		_thumbnails.Clear();
		base.OnClosed(e);
	}
}
