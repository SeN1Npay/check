using System;
using System.Windows;
using Metamorph.Core.Desktop;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

internal sealed record CardContext(CardTheme Theme, ThemeProfile Profile, MetamorphServices Services, CardDataStore Data);

// Содержимое одной карточки. Tick вызывается общим секундным таймером только пока карточка активна.
internal abstract class CardView : IDisposable
{
	protected CardContext Context { get; }

	protected CardTheme Theme => Context.Theme;

	public CardSettings Settings { get; }

	public bool Active { get; private set; }

	// Заметке и списку дел нужен ввод с клавиатуры — их окна активируются по клику.
	public virtual bool NeedsKeyboard => false;

	protected CardView(CardContext context, CardSettings settings)
	{
		Context = context;
		Settings = settings;
	}

	public abstract FrameworkElement Build(double width, double height);

	public void SetActive(bool active)
	{
		if (Active == active)
		{
			return;
		}
		Active = active;
		try
		{
			OnActiveChanged(active);
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: активность " + Settings.Kind, ex);
		}
	}

	protected virtual void OnActiveChanged(bool active)
	{
	}

	public virtual void Tick(long tick)
	{
	}

	public virtual void Dispose()
	{
		SetActive(active: false);
	}

	public static CardView Create(string kind, CardContext context, CardSettings settings)
	{
		return kind switch
		{
			"clock" => new ClockCard(context, settings),
			"calendar" => new CalendarCard(context, settings),
			"weather" => new WeatherCard(context, settings),
			"media" => new MediaCard(context, settings),
			"system" => new SystemCard(context, settings),
			"pomodoro" => new PomodoroCard(context, settings),
			"note" => new NoteCard(context, settings),
			"todo" => new TodoCard(context, settings),
			_ => new QuoteCard(context, settings)
		};
	}
}
