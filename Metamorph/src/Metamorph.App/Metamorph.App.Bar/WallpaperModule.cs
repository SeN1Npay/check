using System.Windows;
using Metamorph.Core.Localization;
using Metamorph.Core.Wallpaper;

namespace Metamorph.App.Bar;

public sealed class WallpaperModule : LabeledModule
{
	private static App? App => Application.Current as App;

	public override int IntervalTicks => 4;

	public WallpaperModule(BarContext context)
		: base(context, "wall")
	{
		Clickable(delegate
		{
			App?.NextWallpaper();
		}, delegate
		{
			App?.ToggleWallpaperPause();
			Update();
		}, delegate
		{
			App?.ShowWallpapersTab();
		});
	}

	public override void Update()
	{
		App app = App;
		if (app != null)
		{
			string text = WallpaperChoices.Describe(app.WallpaperOverride).Trim('«', '»');
			if (text.Length > 16)
			{
				text = text.Substring(0, 15) + "…";
			}
			bool flag = app.Session?.IsWallpaperPaused ?? false;
			SetValue(text + (flag ? " ⏸" : ""), flag ? Context.Muted : null);
			base.TextView.ToolTip = Loc.T("Клик — следующие обои, правый — пауза, средний — вкладка «Обои»");
		}
	}
}
