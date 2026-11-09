namespace Metamorph.Core.Models;

public sealed class DesktopSettings
{
	public bool? HideIcons { get; set; }

	public WidgetSettings Widget { get; set; } = new WidgetSettings();

}
