using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Metamorph.App.Bar;

public sealed class LauncherModule : BarModule
{
	private readonly TextBlock _text;

	public override FrameworkElement View => _text;

	public override int IntervalTicks => int.MaxValue;

	public LauncherModule(BarContext context)
	{
		BarContext context2 = context;
		_text = new TextBlock
		{
			Text = "◆",
			Foreground = context2.Accent,
			FontSize = 15.0,
			Margin = new Thickness(4.0, 0.0, 10.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Cursor = Cursors.Hand,
			ToolTip = "Metamorph"
		};
		_text.MouseLeftButtonUp += delegate
		{
			context2.OpenMainWindow();
		};
	}

	public override void Update()
	{
	}
}
