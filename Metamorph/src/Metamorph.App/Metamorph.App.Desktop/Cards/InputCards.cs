using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Metamorph.Core.Desktop;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop.Cards;

internal static class CardInput
{
	public static TextBox Box(CardTheme theme, double size)
	{
		return new TextBox
		{
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Foreground = theme.Foreground,
			CaretBrush = theme.Accent,
			SelectionBrush = theme.Accent,
			FontFamily = theme.Font,
			FontSize = size,
			Padding = new Thickness(0.0),
			FocusVisualStyle = null
		};
	}

	public static TextBlock Placeholder(CardTheme theme, string text, double size)
	{
		TextBlock t = CardUi.Text(Loc.T(text), size, theme.Muted, FontWeights.Normal, theme.Font);
		t.IsHitTestVisible = false;
		t.Opacity = 0.7;
		t.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		return t;
	}

	public static FrameworkElement Header(CardTheme theme, string title, out TextBlock right)
	{
		Grid g = new Grid { Margin = new Thickness(0.0, 0.0, 0.0, 8.0) };
		StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
		left.Children.Add(new Border
		{
			Width = 8.0,
			Height = 8.0,
			CornerRadius = new CornerRadius(4.0),
			Background = theme.AccentGradient,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		});
		left.Children.Add(CardUi.Text(Loc.T(title).ToUpper(Loc.Culture), 11.0, theme.Muted, FontWeights.SemiBold, theme.Font));
		g.Children.Add(left);
		right = CardUi.Text("", 11.0, theme.Muted, FontWeights.SemiBold, theme.Font);
		right.HorizontalAlignment = HorizontalAlignment.Right;
		g.Children.Add(right);
		return g;
	}
}

internal sealed class NoteCard : CardView
{
	private readonly DispatcherTimer _save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800.0) };

	private TextBox? _box;

	private bool _dirty;

	public override bool NeedsKeyboard => true;

	public NoteCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
		_save.Tick += delegate
		{
			Flush();
		};
	}

	public override FrameworkElement Build(double width, double height)
	{
		Grid root = new Grid { Margin = new Thickness(18.0, 14.0, 14.0, 14.0) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.Children.Add(CardInput.Header(Theme, "Заметка", out TextBlock _));
		TextBox box = CardInput.Box(Theme, 14.0);
		box.AcceptsReturn = true;
		box.AcceptsTab = true;
		box.TextWrapping = TextWrapping.Wrap;
		box.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
		box.Text = Context.Data.GetNote(CardDataStore.KeyOf(Settings));
		TextBlock placeholder = CardInput.Placeholder(Theme, "Напишите что-нибудь…", 14.0);
		placeholder.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		box.TextChanged += delegate
		{
			placeholder.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
			_dirty = true;
			_save.Stop();
			_save.Start();
		};
		box.LostKeyboardFocus += delegate
		{
			Flush();
		};
		Grid.SetRow(box, 1);
		Grid.SetRow(placeholder, 1);
		root.Children.Add(box);
		root.Children.Add(placeholder);
		_box = box;
		return root;
	}

	private void Flush()
	{
		_save.Stop();
		if (_dirty && _box != null)
		{
			_dirty = false;
			Context.Data.SetNote(CardDataStore.KeyOf(Settings), _box.Text);
		}
	}

	public override void Dispose()
	{
		Flush();
		base.Dispose();
	}
}

internal sealed class TodoCard : CardView
{
	private List<CardTodo> _items = new List<CardTodo>();

	private readonly StackPanel _list = new StackPanel();

	private readonly ScrollViewer _scroll = new ScrollViewer
	{
		VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
		HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
	};

	private TextBlock _count = new TextBlock();

	public override bool NeedsKeyboard => true;

	public TodoCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		_items = Context.Data.GetTodos(CardDataStore.KeyOf(Settings));
		Grid root = new Grid { Margin = new Thickness(18.0, 14.0, 14.0, 12.0) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.Children.Add(CardInput.Header(Theme, "Задачи", out _count));
		_scroll.Content = _list;
		Grid.SetRow(_scroll, 1);
		root.Children.Add(_scroll);
		TextBox input = CardInput.Box(Theme, 13.5);
		TextBlock placeholder = CardInput.Placeholder(Theme, "Новая задача…", 13.5);
		input.TextChanged += delegate
		{
			placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
		};
		input.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key != Key.Return)
			{
				return;
			}
			e.Handled = true;
			string text = input.Text.Trim();
			if (text.Length > 0)
			{
				_items.Add(new CardTodo { Text = text });
				input.Clear();
				Save();
				_scroll.ScrollToEnd();
			}
		};
		Grid inputRow = new Grid
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
			Children = { input, placeholder }
		};
		Border line = new Border
		{
			Height = 1.0,
			Background = Theme.Border,
			VerticalAlignment = VerticalAlignment.Top,
			Margin = new Thickness(0.0, -6.0, 0.0, 0.0)
		};
		inputRow.Children.Add(line);
		Grid.SetRow(inputRow, 2);
		root.Children.Add(inputRow);
		Render();
		return root;
	}

	private void Save()
	{
		Context.Data.SetTodos(CardDataStore.KeyOf(Settings), _items);
		Render();
	}

	private void Render()
	{
		_list.Children.Clear();
		foreach (CardTodo item in _items.ToList())
		{
			_list.Children.Add(Row(item));
		}
		int done = _items.Count(i => i.Done);
		_count.Text = _items.Count == 0 ? "" : $"{done.ToString(CultureInfo.InvariantCulture)}/{_items.Count.ToString(CultureInfo.InvariantCulture)}";
	}

	private FrameworkElement Row(CardTodo item)
	{
		Grid row = new Grid
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 2.0),
			Background = Brushes.Transparent
		};
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		Border check = new Border
		{
			Width = 18.0,
			Height = 18.0,
			CornerRadius = new CornerRadius(9.0),
			BorderThickness = new Thickness(item.Done ? 0.0 : 1.5),
			BorderBrush = Theme.Muted,
			Background = item.Done ? Theme.AccentGradient : Brushes.Transparent,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
			Cursor = Cursors.Hand,
			Child = new TextBlock
			{
				Text = item.Done ? CardGlyphs.Check : "",
				FontFamily = Theme.Icons,
				FontSize = 10.0,
				Foreground = Theme.OnAccent,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
		TextBlock text = CardUi.Text(item.Text, 13.5, Theme.Foreground, FontWeights.Normal, Theme.Font);
		text.VerticalAlignment = VerticalAlignment.Center;
		text.Cursor = Cursors.Hand;
		if (item.Done)
		{
			text.TextDecorations = TextDecorations.Strikethrough;
			text.Opacity = 0.5;
		}
		Grid.SetColumn(text, 1);
		Border delete = CardUi.IconButton(Theme, CardGlyphs.Close, delegate
		{
			_items.Remove(item);
			Save();
		}, 24.0, primary: false, Loc.T("Удалить задачу"));
		delete.Opacity = 0.0;
		Grid.SetColumn(delete, 2);
		row.MouseEnter += delegate
		{
			delete.Opacity = 1.0;
		};
		row.MouseLeave += delegate
		{
			delete.Opacity = 0.0;
		};
		MouseButtonEventHandler toggle = delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			item.Done = !item.Done;
			Save();
		};
		check.MouseLeftButtonUp += toggle;
		text.MouseLeftButtonUp += toggle;
		row.Children.Add(check);
		row.Children.Add(text);
		row.Children.Add(delete);
		return row;
	}
}
