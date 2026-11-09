namespace Metamorph.Core.Models;

public sealed class TaskbarSettings
{
	public TaskbarMode Mode { get; set; }

	public bool CenterIcons { get; set; }

	public TaskbarStyle Style { get; set; }

	public double Opacity { get; set; } = 0.55;


	public bool? HideSearch { get; set; }

	public bool? HideTaskView { get; set; }

	public bool? HideCortana { get; set; }

	public bool? HidePeople { get; set; }

	public bool? HideNews { get; set; }

	public bool? SmallIcons { get; set; }

	public TaskbarGrouping? Grouping { get; set; }
}
