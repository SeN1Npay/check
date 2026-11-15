using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedShell.WindowsTray;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class TrayModule : BarModule
{
	private readonly StackPanel _panel = new StackPanel
	{
		Orientation = System.Windows.Controls.Orientation.Horizontal,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(4.0, 0.0, 4.0, 0.0)
	};

	private readonly BarContext _context;

	private readonly NotificationArea? _area;

	private readonly HashSet<ManagedShell.WindowsTray.NotifyIcon> _subscribed = new HashSet<ManagedShell.WindowsTray.NotifyIcon>();

	private bool _rebuildQueued;

	public override FrameworkElement View => _panel;

	public override int IntervalTicks => int.MaxValue;

	public TrayModule(BarContext context)
	{
		_context = context;
		_area = TrayHost.GetOrCreate();
		if (_area == null)
		{
			_panel.Children.Add(new TextBlock
			{
				Text = Loc.T("трей недоступен"),
				Foreground = context.Muted,
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		else
		{
			_area.TrayIcons.CollectionChanged += OnIconsChanged;
			Rebuild();
		}
	}

	public override void Update()
	{
	}

	public override void Dispose()
	{
		if (_area != null)
		{
			_area.TrayIcons.CollectionChanged -= OnIconsChanged;
		}
		foreach (ManagedShell.WindowsTray.NotifyIcon item in _subscribed)
		{
			item.PropertyChanged -= OnIconPropertyChanged;
		}
		_subscribed.Clear();
	}

	private void OnIconsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		QueueRebuild();
	}

	private void OnIconPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		bool flag;
		switch (e.PropertyName)
		{
		case "Icon":
		case "IsHidden":
		case "Title":
		case null:
		case "":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			QueueRebuild();
		}
	}

	private void QueueRebuild()
	{
		if (!_rebuildQueued)
		{
			_rebuildQueued = true;
			_panel.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				_rebuildQueued = false;
				Rebuild();
			});
		}
	}

	private void Rebuild()
	{
		if (_area == null)
		{
			return;
		}
		_panel.Children.Clear();
		List<ManagedShell.WindowsTray.NotifyIcon> list;
		try
		{
			list = _area.TrayIcons.ToList();
		}
		catch (InvalidOperationException)
		{
			QueueRebuild();
			return;
		}
		foreach (ManagedShell.WindowsTray.NotifyIcon item in list)
		{
			if (_subscribed.Add(item))
			{
				item.PropertyChanged += OnIconPropertyChanged;
			}
			if (!item.IsHidden && item.Icon != null)
			{
				_panel.Children.Add(CreateView(item));
			}
		}
	}

	private FrameworkElement CreateView(ManagedShell.WindowsTray.NotifyIcon icon)
	{
		ManagedShell.WindowsTray.NotifyIcon icon2 = icon;
		Image image = new Image
		{
			Source = icon2.Icon,
			Width = 16.0,
			Height = 16.0,
			Stretch = Stretch.Uniform,
			SnapsToDevicePixels = true
		};
		RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
		SolidColorBrush hover = new SolidColorBrush(((SolidColorBrush)_context.Muted).Color)
		{
			Opacity = 0.35
		};
		Border border = new Border
		{
			Child = image,
			Padding = new Thickness(4.0, 3.0, 4.0, 3.0),
			Margin = new Thickness(1.0, 0.0, 1.0, 0.0),
			CornerRadius = new CornerRadius(5.0),
			Background = Brushes.Transparent,
			ToolTip = (string.IsNullOrWhiteSpace(icon2.Title) ? null : icon2.Title),
			VerticalAlignment = VerticalAlignment.Center
		};
		border.MouseEnter += delegate
		{
			border.Background = hover;
			try
			{
				double dpiScaleX = VisualTreeHelper.GetDpi(border).DpiScaleX;
				Point point = border.PointToScreen(new Point(0.0, 0.0));
				icon2.Placement = new NativeMethods.Rect
				{
					Left = (int)point.X,
					Top = (int)point.Y,
					Right = (int)(border.ActualWidth * dpiScaleX),
					Bottom = (int)(border.ActualHeight * dpiScaleX)
				};
				icon2.IconMouseEnter(MouseHelper.GetCursorPositionParam());
			}
			catch (Exception ex)
			{
				Log.Warn("Трей: " + ex.Message);
			}
		};
		border.MouseLeave += delegate
		{
			border.Background = Brushes.Transparent;
			icon2.IconMouseLeave(MouseHelper.GetCursorPositionParam());
		};
		border.MouseMove += delegate
		{
			icon2.IconMouseMove(MouseHelper.GetCursorPositionParam());
		};
		border.MouseDown += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (e.ChangedButton != 0)
			{
				icon2.IconMouseDown(e.ChangedButton, MouseHelper.GetCursorPositionParam(), SystemInformation.DoubleClickTime);
			}
		};
		border.MouseUp += delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			int doubleClickTime = SystemInformation.DoubleClickTime;
			if (e.ChangedButton == MouseButton.Left)
			{
				icon2.IconMouseDown(e.ChangedButton, MouseHelper.GetCursorPositionParam(), doubleClickTime);
			}
			icon2.IconMouseUp(e.ChangedButton, MouseHelper.GetCursorPositionParam(), doubleClickTime);
		};
		return border;
	}
}
