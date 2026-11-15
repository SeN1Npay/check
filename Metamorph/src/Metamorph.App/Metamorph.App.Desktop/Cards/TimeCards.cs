using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Metamorph.Core.Desktop;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop.Cards;

internal sealed class ClockCard : CardView
{
	private TextBlock? _time;

	private TextBlock? _hours;

	private TextBlock? _minutes;

	private TextBlock _date = new TextBlock();

	private CardRing? _ring;

	private TextBlock? _seconds;

	private int _lastSecond = -1;

	public ClockCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		string style = (Theme.Settings.ClockStyle ?? "modern").Trim().ToLowerInvariant();
		bool withSeconds = Settings.Options.TryGetValue("seconds", out string? s) && s == "true";
		bool custom = !string.IsNullOrWhiteSpace(Theme.Settings.FontFamily);
		// Без явного шрифта у каждого стиля своё начертание — так стили реально отличаются.
		FontFamily font = custom ? Theme.Font : style switch
		{
			"mono" => Theme.Mono,
			"retro" => new FontFamily("Bahnschrift, Segoe UI"),
			_ => new FontFamily("Segoe UI Variable Display, Segoe UI")
		};
		double big = Math.Min(height * 0.5, width * 0.22);
		Grid root = new Grid { Margin = new Thickness(26.0, 14.0, 22.0, 16.0) };
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		StackPanel stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		root.Children.Add(stack);
		switch (style)
		{
		case "stacked":
		{
			double size = Math.Min(height * 0.34, width * 0.36);
			_hours = CardUi.Text("", size, Theme.Foreground, FontWeights.Black, font);
			_minutes = CardUi.Text("", size, Theme.AccentGradient, FontWeights.Black, font);
			_hours.Margin = new Thickness(-2.0, -size * 0.2, 0.0, -size * 0.18);
			_minutes.Margin = new Thickness(-2.0, -size * 0.18, 0.0, -size * 0.12);
			_date = CardUi.Text("", 14.0, Theme.Muted, FontWeights.SemiBold, Theme.Font);
			_date.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
			stack.Children.Add(_hours);
			stack.Children.Add(_minutes);
			stack.Children.Add(_date);
			break;
		}
		case "thin":
			_time = CardUi.Text("", big * 1.12, Theme.Foreground, FontWeights.Light, font);
			_date = CardUi.Text("", 15.0, Theme.Muted, FontWeights.Light, Theme.Font);
			break;
		case "mono":
			_time = CardUi.Text("", big * 0.92, Theme.Foreground, FontWeights.Bold, font);
			_date = CardUi.Text("", 14.0, Theme.Accent, FontWeights.Normal, Theme.Mono);
			break;
		case "retro":
			_time = CardUi.Text("", big, Theme.AccentGradient, FontWeights.SemiBold, font);
			_time.Effect = new DropShadowEffect
			{
				Color = Theme.AccentColor,
				BlurRadius = 22.0,
				ShadowDepth = 0.0,
				Opacity = 0.75
			};
			_date = CardUi.Text("", 14.0, Theme.Accent2, FontWeights.Bold, font);
			break;
		default:
			_time = CardUi.Text("", big, Theme.Foreground, FontWeights.SemiBold, font);
			_date = CardUi.Text("", 16.0, Theme.Accent, FontWeights.Medium, Theme.Font);
			break;
		}
		if (_time != null)
		{
			_time.Margin = new Thickness(-3.0, -big * 0.2, 0.0, -big * 0.08);
			_date.Margin = new Thickness(1.0, 4.0, 0.0, 0.0);
			stack.Children.Add(_time);
			stack.Children.Add(_date);
		}
		if (withSeconds)
		{
			double ringSize = style == "stacked" ? 54.0 : Math.Clamp(height * 0.42, 54.0, 96.0);
			_ring = new CardRing(ringSize, ringSize * 0.08, Theme.Track, Theme.AccentGradient);
			_seconds = CardUi.Text("", ringSize * 0.3, Theme.Foreground, FontWeights.SemiBold, Theme.Mono);
			_seconds.HorizontalAlignment = HorizontalAlignment.Center;
			_seconds.VerticalAlignment = VerticalAlignment.Center;
			Grid ringBox = new Grid
			{
				Margin = new Thickness(14.0, 0.0, 0.0, 0.0),
				VerticalAlignment = style == "stacked" ? VerticalAlignment.Bottom : VerticalAlignment.Center,
				Children = { _ring, _seconds }
			};
			Grid.SetColumn(ringBox, 1);
			root.Children.Add(ringBox);
		}
		Update(animate: false);
		return root;
	}

	protected override void OnActiveChanged(bool active)
	{
		if (active)
		{
			Update(animate: false);
		}
	}

	public override void Tick(long tick)
	{
		Update(animate: true);
	}

	private void Update(bool animate)
	{
		DateTime now = DateTime.Now;
		string time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
		if (_time != null && _time.Text != time)
		{
			_time.Text = time;
		}
		if (_hours != null && _minutes != null)
		{
			_hours.Text = now.ToString("HH", CultureInfo.InvariantCulture);
			_minutes.Text = now.ToString("mm", CultureInfo.InvariantCulture);
		}
		string style = (Theme.Settings.ClockStyle ?? "").ToLowerInvariant();
		string date = style == "mono" ? now.ToString("ddd · dd.MM.yyyy", Loc.Culture) : now.ToString("dddd, d MMMM", Loc.Culture);
		date = style == "retro" ? date.ToUpper(Loc.Culture) : Capitalize(date);
		if (_date.Text != date)
		{
			_date.Text = date;
		}
		if (_ring != null && _seconds != null && now.Second != _lastSecond)
		{
			_seconds.Text = now.Second.ToString("00", CultureInfo.InvariantCulture);
			// Переход через ноль — без анимации, иначе дуга «отматывается» назад.
			bool wrap = now.Second < _lastSecond || _lastSecond < 0;
			_lastSecond = now.Second;
			if (wrap || !animate)
			{
				_ring.Animate(now.Second / 60.0, animate: false);
			}
			if (animate)
			{
				_ring.Animate((now.Second + 1) / 60.0, animate: true, 1.0, linear: true);
			}
		}
	}

	internal static string Capitalize(string s)
	{
		return s.Length == 0 ? s : char.ToUpper(s[0], Loc.Culture) + s.Substring(1);
	}
}

internal sealed class CalendarCard : CardView
{
	private readonly UniformGrid _days = new UniformGrid { Columns = 7, Rows = 6 };

	private TextBlock _title = new TextBlock();

	private DateTime _shown;

	private DateTime _today;

	public CalendarCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		_today = DateTime.Today;
		_shown = new DateTime(_today.Year, _today.Month, 1);
		StackPanel root = new StackPanel { Margin = new Thickness(18.0, 14.0, 18.0, 14.0) };
		Grid header = new Grid { Margin = new Thickness(4.0, 0.0, 0.0, 8.0) };
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		_title = CardUi.Text("", 17.0, Theme.Foreground, FontWeights.SemiBold, Theme.Font);
		_title.VerticalAlignment = VerticalAlignment.Center;
		_title.Cursor = Cursors.Hand;
		_title.ToolTip = Loc.T("Сегодня");
		_title.MouseLeftButtonUp += delegate
		{
			_shown = new DateTime(_today.Year, _today.Month, 1);
			Render();
		};
		header.Children.Add(_title);
		StackPanel nav = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Children =
			{
				CardUi.IconButton(Theme, CardGlyphs.Left, delegate
				{
					_shown = _shown.AddMonths(-1);
					Render();
				}, 28.0),
				CardUi.IconButton(Theme, CardGlyphs.Right, delegate
				{
					_shown = _shown.AddMonths(1);
					Render();
				}, 28.0)
			}
		};
		Grid.SetColumn(nav, 1);
		header.Children.Add(nav);
		root.Children.Add(header);
		DateTimeFormatInfo fmt = Loc.Culture.DateTimeFormat;
		UniformGrid week = new UniformGrid { Columns = 7, Margin = new Thickness(0.0, 0.0, 0.0, 4.0) };
		foreach (DayOfWeek d in CardCalendar.WeekOrder(fmt.FirstDayOfWeek))
		{
			TextBlock t = CardUi.Text(ClockCard.Capitalize(fmt.GetShortestDayName(d)), 11.0, CardCalendar.IsWeekend(d) ? Theme.Accent2 : Theme.Muted, FontWeights.SemiBold, Theme.Font);
			t.HorizontalAlignment = HorizontalAlignment.Center;
			t.Opacity = 0.85;
			week.Children.Add(t);
		}
		root.Children.Add(week);
		_days.Height = Math.Max(150.0, height - 116.0);
		root.Children.Add(_days);
		Render();
		return root;
	}

	private void Render()
	{
		DateTimeFormatInfo fmt = Loc.Culture.DateTimeFormat;
		_title.Text = ClockCard.Capitalize(fmt.GetMonthName(_shown.Month)) + (_shown.Year != _today.Year ? " " + _shown.Year : "");
		_days.Children.Clear();
		foreach (DateTime day in CardCalendar.Month(_shown.Year, _shown.Month, fmt.FirstDayOfWeek))
		{
			bool inMonth = day.Month == _shown.Month;
			bool today = day == _today;
			bool weekend = CardCalendar.IsWeekend(day.DayOfWeek);
			TextBlock t = CardUi.Text(day.Day.ToString(CultureInfo.InvariantCulture), 13.0, today ? Theme.OnAccent : (weekend ? Theme.Accent2 : Theme.Foreground), today ? FontWeights.Bold : FontWeights.Normal, Theme.Font);
			t.HorizontalAlignment = HorizontalAlignment.Center;
			t.VerticalAlignment = VerticalAlignment.Center;
			if (!inMonth)
			{
				t.Opacity = 0.32;
			}
			_days.Children.Add(new Border
			{
				Margin = new Thickness(2.0, 1.5, 2.0, 1.5),
				CornerRadius = new CornerRadius(9.0),
				Background = today ? Theme.AccentGradient : Brushes.Transparent,
				Child = t
			});
		}
	}

	public override void Tick(long tick)
	{
		if (tick % 30 == 0 && DateTime.Today != _today)
		{
			bool current = _shown.Year == _today.Year && _shown.Month == _today.Month;
			_today = DateTime.Today;
			if (current)
			{
				_shown = new DateTime(_today.Year, _today.Month, 1);
			}
			Render();
		}
	}
}

internal sealed class QuoteCard : CardView
{
	private TextBlock _text = new TextBlock();

	private TextBlock _author = new TextBlock();

	private int _offset;

	private DateTime _day;

	public QuoteCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		Grid root = new Grid
		{
			Margin = new Thickness(22.0, 12.0, 22.0, 14.0),
			Cursor = Cursors.Hand,
			Background = Brushes.Transparent,
			ToolTip = Loc.T("Следующая цитата")
		};
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		TextBlock mark = CardUi.Text("“", 44.0, Theme.AccentGradient, FontWeights.Bold, new FontFamily("Georgia, Segoe UI"));
		mark.Margin = new Thickness(-2.0, -8.0, 0.0, -26.0);
		mark.Opacity = 0.9;
		root.Children.Add(mark);
		_text = CardUi.Text("", 15.0, Theme.Foreground, FontWeights.Normal, Theme.Font);
		_text.FontStyle = FontStyles.Italic;
		_text.TextWrapping = TextWrapping.Wrap;
		_text.TextTrimming = TextTrimming.WordEllipsis;
		_text.VerticalAlignment = VerticalAlignment.Center;
		_text.Margin = new Thickness(22.0, 4.0, 0.0, 4.0);
		Grid.SetRow(_text, 1);
		root.Children.Add(_text);
		_author = CardUi.Text("", 12.0, Theme.Accent, FontWeights.SemiBold, Theme.Font);
		_author.HorizontalAlignment = HorizontalAlignment.Right;
		Grid.SetRow(_author, 2);
		root.Children.Add(_author);
		root.MouseLeftButtonUp += delegate
		{
			_offset++;
			Render();
		};
		Render();
		return root;
	}

	private void Render()
	{
		_day = DateTime.Today;
		int index = (CardQuotes.IndexFor(_day) + _offset) % CardQuotes.All.Count;
		CardQuote q = CardQuotes.All[index];
		string text = q.Text(Loc.IsEnglish);
		_text.Text = text;
		_text.FontSize = text.Length > 70 ? 14.0 : 15.5;
		_author.Text = "— " + q.Author(Loc.IsEnglish);
	}

	public override void Tick(long tick)
	{
		if (tick % 60 == 0 && DateTime.Today != _day)
		{
			_offset = 0;
			Render();
		}
	}
}
