using System.Collections.Generic;

namespace Metamorph.Core.Models;

// «Полки» (как Fences): полупрозрачные зоны на рабочем столе со ярлыками из папки.
public sealed class ShelvesSettings
{
	public bool Enabled { get; set; }

	public List<ShelfSettings> Items { get; set; } = new List<ShelfSettings>();

	public double Opacity { get; set; } = 0.6;

	public int IconSize { get; set; } = 40;
}

public sealed class ShelfSettings
{
	public string Title { get; set; } = "Полка";

	// Папка, содержимое которой показывает полка (пусто — %AppData%\Metamorph\shelves\<Title>).
	public string? Folder { get; set; }

	// Автосортировка рабочего стола по правилам: расширения через запятую (".png,.jpg") или "folders" / "shortcuts".
	public string? Rule { get; set; }

	public double X { get; set; } = 0.05;

	public double Y { get; set; } = 0.1;

	public double Width { get; set; } = 320.0;

	public double Height { get; set; } = 220.0;

	public bool Collapsed { get; set; }

	public string? Monitor { get; set; }
}

public sealed class HotCornersSettings
{
	public static readonly IReadOnlyList<string> Actions = new string[]
	{
		"none", "showDesktop", "taskView", "launcher", "island", "assistant", "lock", "screenSaver", "games", "editDesktop", "nextWallpaper"
	};

	public bool Enabled { get; set; }

	public string TopLeft { get; set; } = "taskView";

	public string TopRight { get; set; } = "none";

	public string BottomLeft { get; set; } = "showDesktop";

	public string BottomRight { get; set; } = "none";

	// Сколько держать курсор в углу, мс.
	public int DelayMs { get; set; } = 250;
}
