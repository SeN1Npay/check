using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

// Кисти и шрифты карточек: создаются один раз на тему и замораживаются.
internal sealed class CardTheme
{
	public Palette Palette { get; }

	public CardsSettings Settings { get; }

	public Brush Foreground { get; }

	public Brush Muted { get; }

	public Brush Accent { get; }

	public Brush Accent2 { get; }

	public Brush Red { get; }

	public Brush Green { get; }

	public Brush Surface { get; }

	public Brush Border { get; }

	public Brush BorderHover { get; }

	public Brush Track { get; }

	public Brush Hover { get; }

	public Brush AccentGradient { get; }

	public Brush Sheen { get; }

	public Brush OnAccent { get; }

	public Color AccentColor { get; }

	public FontFamily Font { get; }

	public FontFamily Mono { get; }

	public FontFamily Icons { get; }

	public double Radius { get; }

	public double Scale { get; }

	public bool Glass => Settings.Glass;

	static CardTheme()
	{
		Loc.AddEnglish(("Заметка", "Note"), ("Задачи", "Tasks"), ("Новая задача…", "New task…"), ("Напишите что-нибудь…", "Write something…"), ("Ничего не играет", "Nothing playing"), ("Нет данных о погоде", "No weather data"), ("Проверьте подключение", "Check your connection"), ("Загрузка…", "Loading…"), ("Фокус", "Focus"), ("Перерыв", "Break"), ("Таймер", "Timer"), ("Пауза", "Paused"), ("Помодоро", "Pomodoro"), ("Цитата дня", "Quote of the day"), ("ветер", "wind"), ("км/ч", "km/h"), ("Часы", "Clock"), ("Календарь", "Calendar"), ("Погода", "Weather"), ("Музыка", "Media"), ("Система", "System"), ("Список дел", "To-do"), ("Цитата", "Quote"), ("Добавить карточку", "Add card"), ("Удалить", "Remove"), ("Сбросить раскладку", "Reset layout"), ("Готово", "Done"), ("Редактировать рабочий стол", "Edit desktop"), ("Режим редактирования — перетаскивайте карточки, Esc — готово", "Edit mode — drag cards around, Esc — done"), ("Удалить задачу", "Delete task"), ("Раунд {0}", "Round {0}"), ("Сегодня", "Today"), ("Секундная стрелка", "Seconds ring"), ("Следующая цитата", "Next quote"), ("заряд", "battery"));
	}

	public CardTheme(ThemeProfile profile)
	{
		Palette p = profile.Palette;
		Settings = profile.Desktop.Cards;
		Palette = p;
		Foreground = BrushFactory.Solid(p.Foreground);
		Muted = BrushFactory.Solid(p.Muted);
		Accent = BrushFactory.Solid(p.Accent);
		Accent2 = BrushFactory.Solid(p.Accent2);
		Red = BrushFactory.Solid(p.Red);
		Green = BrushFactory.Solid(p.Green);
		Surface = BrushFactory.Solid(p.Surface, Math.Clamp(Settings.Opacity, 0.05, 1.0));
		Border = BrushFactory.Solid(p.Muted, 0.35);
		BorderHover = BrushFactory.Solid(p.Accent, 0.55);
		Track = BrushFactory.Solid(p.Muted, 0.25);
		Hover = BrushFactory.Solid(p.Foreground, 0.08);
		OnAccent = BrushFactory.Solid(p.Background);
		AccentColor = BrushFactory.ToColor(p.Accent);
		AccentGradient = Freeze(new LinearGradientBrush(BrushFactory.ToColor(p.Accent), BrushFactory.ToColor(p.Accent2), new Point(0.0, 0.0), new Point(1.0, 1.0)));
		// Лёгкий блик сверху — «стекло».
		Sheen = Freeze(new LinearGradientBrush(new GradientStopCollection
		{
			new GradientStop(Color.FromArgb(28, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.0),
			new GradientStop(Color.FromArgb(0, byte.MaxValue, byte.MaxValue, byte.MaxValue), 0.45)
		}, 90.0));
		string font = string.IsNullOrWhiteSpace(Settings.FontFamily) ? profile.Bar.FontFamily : Settings.FontFamily;
		Font = new FontFamily(font + ", Segoe UI Variable Display, Segoe UI");
		Mono = new FontFamily("Cascadia Mono, Consolas");
		Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
		Radius = Math.Clamp(Settings.CornerRadius, 0, 48);
		Scale = Math.Clamp(double.IsFinite(Settings.Scale) ? Settings.Scale : 1.0, 0.5, 3.0);
	}

	public static string KindTitle(string kind)
	{
		return Loc.T(kind switch
		{
			"clock" => "Часы",
			"calendar" => "Календарь",
			"weather" => "Погода",
			"media" => "Музыка",
			"system" => "Система",
			"pomodoro" => "Помодоро",
			"note" => "Заметка",
			"todo" => "Список дел",
			"quote" => "Цитата",
			_ => kind
		});
	}

	public (double Width, double Height) SizeOf(CardSettings card)
	{
		return CardLayout.SizeOf(card, Scale, Settings.ClockStyle);
	}

	private static Brush Freeze(Brush b)
	{
		b.Freeze();
		return b;
	}
}
