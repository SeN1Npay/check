using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.App.Bar;

internal static class CalendarPopup
{
	private static Popup? _open;

	public static void Toggle(UIElement target, BarContext context)
	{
		BarContext context2 = context;
		Popup open = _open;
		if (open != null && open.IsOpen)
		{
			_open.IsOpen = false;
			_open = null;
			return;
		}
		DateTime month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
		ContentControl host = new ContentControl();
		Render();
		host.MouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			month = month.AddMonths((e.Delta <= 0) ? 1 : (-1));
			Render();
		};
		_open = BarPopup.Create(target, context2, host);
		_open.Placement = ((context2.Profile.Bar.Position == BarPosition.Top) ? PlacementMode.Bottom : PlacementMode.Top);
		_open.IsOpen = true;
		void Render()
		{
			host.Content = Build(month, context2, delegate
			{
				month = month.AddMonths(-1);
				Render();
			}, delegate
			{
				month = month.AddMonths(1);
				Render();
			});
		}
	}

	private static UIElement Build(DateTime month, BarContext context, Action prev, Action next)
	{
		CultureInfo culture = Loc.Culture;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(4.0)
		};
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		TextBlock element = new TextBlock
		{
			Text = culture.TextInfo.ToTitleCase(month.ToString("MMMM yyyy", culture)),
			Foreground = context.Accent,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		Grid.SetColumn(element, 1);
		grid.Children.Add(Arrow("‹", prev, context, 0));
		grid.Children.Add(element);
		grid.Children.Add(Arrow("›", next, context, 2));
		stackPanel.Children.Add(grid);
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = 7
		};
		DayOfWeek firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
		for (int i = 0; i < 7; i++)
		{
			DayOfWeek dayOfWeek = (DayOfWeek)((int)(firstDayOfWeek + i) % 7);
			uniformGrid.Children.Add(Cell(culture.DateTimeFormat.GetShortestDayName(dayOfWeek), context.Muted, null));
		}
		int num = (month.DayOfWeek - firstDayOfWeek + 7) % 7;
		for (int j = 0; j < num; j++)
		{
			uniformGrid.Children.Add(new TextBlock());
		}
		int num2 = DateTime.DaysInMonth(month.Year, month.Month);
		for (int k = 1; k <= num2; k++)
		{
			DateTime dateTime = new DateTime(month.Year, month.Month, k);
			bool flag = dateTime == DateTime.Today;
			DayOfWeek dayOfWeek2 = dateTime.DayOfWeek;
			bool flag2 = ((dayOfWeek2 == DayOfWeek.Sunday || dayOfWeek2 == DayOfWeek.Saturday) ? true : false);
			bool flag3 = flag2;
			uniformGrid.Children.Add(Cell(k.ToString(CultureInfo.InvariantCulture), flag ? context.Background : (flag3 ? context.Alert : context.Foreground), flag ? context.Accent : null));
		}
		stackPanel.Children.Add(uniformGrid);
		TextBlock element2 = new TextBlock
		{
			Text = DateTime.Now.ToString("dddd, d MMMM yyyy", culture),
			Foreground = context.Muted,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Center,
			Cursor = Cursors.Hand
		};
		stackPanel.Children.Add(element2);
		return stackPanel;
	}

	private static UIElement Arrow(string text, Action click, BarContext context, int column)
	{
		Action click2 = click;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.Foreground = context.Foreground;
		textBlock.Padding = new Thickness(8.0, 0.0, 8.0, 0.0);
		textBlock.Cursor = Cursors.Hand;
		textBlock.Background = Brushes.Transparent;
		textBlock.FontSize = 15.0;
		textBlock.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			click2();
			e.Handled = true;
		};
		Grid.SetColumn(textBlock, column);
		return textBlock;
	}

	private static UIElement Cell(string text, Brush foreground, Brush? background)
	{
		return new Border
		{
			Width = 30.0,
			Height = 24.0,
			CornerRadius = new CornerRadius(6.0),
			Background = (background ?? Brushes.Transparent),
			Child = new TextBlock
			{
				Text = text,
				Foreground = foreground,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}
}
