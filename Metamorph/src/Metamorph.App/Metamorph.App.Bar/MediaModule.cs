using System;
using System.Threading.Tasks;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class MediaModule : LabeledModule
{
	private bool _busy;

	public override int IntervalTicks => 2;

	public MediaModule(BarContext context)
		: base(context, "♪")
	{
		SetVisible(visible: false);
		Clickable(delegate
		{
			MediaInfo.PlayPauseAsync();
		}, delegate
		{
			MediaInfo.NextAsync();
		}, delegate
		{
			MediaInfo.PreviousAsync();
		});
		base.TextView.ToolTip = Loc.T("Клик — пауза/play, правый — следующий трек, средний — предыдущий");
	}

	public override void Update()
	{
		if (!_busy)
		{
			_busy = true;
			Refresh();
		}
	}

	private async Task Refresh()
	{
		try
		{
			MediaNow mediaNow = await MediaInfo.GetAsync();
			SetVisible((object)mediaNow != null);
			if ((object)mediaNow != null)
			{
				SetValue((mediaNow.Playing ? "" : "❚❚ ") + MediaInfo.Format(mediaNow), mediaNow.Playing ? null : Context.Muted);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Модуль media", ex);
		}
		finally
		{
			_busy = false;
		}
	}
}
