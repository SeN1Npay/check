namespace Metamorph.Core.Models;

public sealed class DesktopSettings
{
	public bool? HideIcons { get; set; }

	public WidgetSettings Widget { get; set; } = new WidgetSettings();

	public CardsSettings Cards { get; set; } = new CardsSettings();

	public EffectsSettings Effects { get; set; } = new EffectsSettings();

	public ShelvesSettings Shelves { get; set; } = new ShelvesSettings();

	public HotCornersSettings HotCorners { get; set; } = new HotCornersSettings();
}
