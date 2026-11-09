using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class WorkspacesModule : BarModule
{
	private readonly StackPanel _panel = new StackPanel
	{
		Orientation = Orientation.Horizontal,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(4.0, 0.0, 4.0, 0.0),
		Background = Brushes.Transparent
	};

	private readonly BarContext _context;

	private int _count = -1;

	private int _current = -1;

	public override FrameworkElement View => _panel;

	public override int IntervalTicks => 2;

	public WorkspacesModule(BarContext context)
	{
		_context = context;
		_panel.MouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			try
			{
				if (e.Delta < 0)
				{
					VirtualDesktops.Next();
				}
				else
				{
					VirtualDesktops.Previous();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Workspaces: колесо", ex);
			}
		};
		_panel.ToolTip = Loc.T("Рабочие столы: клик — перейти, колесо — соседний, правый клик — новый");
		_panel.MouseRightButtonUp += delegate
		{
			VirtualDesktops.Create();
		};
	}

	public override void Update()
	{
		VirtualDesktopInfo virtualDesktopInfo = VirtualDesktops.Read();
		if (virtualDesktopInfo.Count == _count && virtualDesktopInfo.Current == _current)
		{
			return;
		}
		int count = virtualDesktopInfo.Count;
		int current = virtualDesktopInfo.Current;
		_count = count;
		_current = current;
		_panel.Children.Clear();
		for (int i = 0; i < Math.Max(1, virtualDesktopInfo.Count); i++)
		{
			int index = i;
			bool flag = i == virtualDesktopInfo.Current;
			string text = ((i < virtualDesktopInfo.Names.Count && !string.IsNullOrWhiteSpace(virtualDesktopInfo.Names[i])) ? virtualDesktopInfo.Names[i] : (i + 1).ToString());
			TextBlock child = new TextBlock
			{
				Text = text,
				Foreground = (flag ? _context.Background : _context.Foreground),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			Border border = new Border
			{
				Child = child,
				MinWidth = 20.0,
				Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
				Margin = new Thickness(2.0, 3.0, 2.0, 3.0),
				CornerRadius = new CornerRadius(6.0),
				Background = (flag ? _context.Accent : Brushes.Transparent),
				Cursor = Cursors.Hand
			};
			border.MouseLeftButtonUp += delegate
			{
				try
				{
					VirtualDesktops.SwitchTo(index);
				}
				catch (Exception ex)
				{
					Log.Error("Workspaces: переключение", ex);
				}
			};
			_panel.Children.Add(border);
		}
	}
}
