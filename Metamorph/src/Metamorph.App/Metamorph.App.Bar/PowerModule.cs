using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;

namespace Metamorph.App.Bar;

public sealed class PowerModule : BarModule
{
	private readonly TextBlock _text;

	public override FrameworkElement View => _text;

	public override int IntervalTicks => int.MaxValue;

	public PowerModule(BarContext context)
	{
		BarContext context2 = context;
		PowerModule powerModule = this;
		_text = new TextBlock
		{
			Text = "⏻",
			Foreground = context2.Alert,
			FontSize = 14.0,
			Margin = new Thickness(8.0, 0.0, 6.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			Cursor = Cursors.Hand,
			Background = Brushes.Transparent,
			ToolTip = Loc.T("Питание")
		};
		_text.MouseLeftButtonUp += delegate
		{
			BarPopup.ShowMenu(powerModule._text, context2, new(string, Action)[5]
			{
				("\ud83d\udd12  " + Loc.T("Заблокировать"), PowerActions.Lock),
				("☾  " + Loc.T("Сон"), PowerActions.Sleep),
				("⇥  " + Loc.T("Выйти из системы"), PowerActions.SignOut),
				("⟳  " + Loc.T("Перезагрузка"), PowerActions.Restart),
				("⏻  " + Loc.T("Выключение"), PowerActions.Shutdown)
			});
		};
	}

	public override void Update()
	{
	}
}
