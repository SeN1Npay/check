using System.Collections.Generic;

namespace Metamorph.Core.Models;

// Стеклянные карточки-виджеты на рабочем столе.
public sealed class CardsSettings
{
	public static readonly IReadOnlyList<string> Known = new string[]
	{
		"clock", "calendar", "weather", "media", "system", "pomodoro", "note", "todo", "quote"
	};

	public static readonly IReadOnlyList<string> Presets = new string[] { "minimal", "dashboard", "clock", "custom" };

	public bool Enabled { get; set; }

	// minimal | dashboard | clock | custom (custom — раскладка из Items).
	public string Preset { get; set; } = "dashboard";

	public List<CardSettings> Items { get; set; } = new List<CardSettings>();

	public bool Glass { get; set; } = true;

	public double Opacity { get; set; } = 0.72;

	public int CornerRadius { get; set; } = 18;

	public double Scale { get; set; } = 1.0;

	// Шаг сетки привязки в DIP.
	public int Grid { get; set; } = 16;

	// Стиль больших часов: modern | thin | mono | retro | stacked.
	public string ClockStyle { get; set; } = "modern";

	public string? FontFamily { get; set; }
}

public sealed class CardSettings
{
	public string Kind { get; set; } = "clock";

	// Позиция — доля от рабочей области монитора (0..1), чтобы раскладка переживала смену разрешения.
	public double X { get; set; }

	public double Y { get; set; }

	public double? Width { get; set; }

	public double? Height { get; set; }

	// Имя монитора (DeviceName); пусто — основной.
	public string? Monitor { get; set; }

	public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
}
