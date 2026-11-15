using System;
using System.Windows.Threading;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Shell;

internal sealed class TaskbarStyleKeeper : IDisposable
{
	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(250.0)
	};

	private readonly TaskbarStyle _style;

	private readonly Rgba _tint;

	private readonly double _opacity;

	public TaskbarStyleKeeper(ThemeProfile profile)
	{
		_style = profile.Taskbar.Style;
		_tint = Rgba.Parse(profile.Palette.Background).WithAlpha(byte.MaxValue);
		_opacity = profile.Taskbar.Opacity;
		_timer.Tick += delegate
		{
			Reapply();
		};
		Reapply();
		_timer.Start();
	}

	public void Reapply()
	{
		try
		{
			TaskbarStyler.Apply(_style, _tint, _opacity);
		}
		catch (Exception ex)
		{
			Log.Error("Стиль панели задач", ex);
			_timer.Stop();
		}
	}

	public void Dispose()
	{
		_timer.Stop();
		try
		{
			TaskbarStyler.Reset();
		}
		catch (Exception ex)
		{
			Log.Error("Сброс стиля панели задач", ex);
		}
	}
}
