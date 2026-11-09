using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class GifPlayer : IDisposable
{
	private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render);

	private readonly List<GifView> _views = new List<GifView>();

	private readonly string _name;

	private GifAnimation? _animation;

	private int _frame;

	private bool _disposed;

	public GifPlayer(string path, int maxWidth)
	{
		string path2 = path;
		GifPlayer gifPlayer = this;
		_name = Path.GetFileName(path2);
		_timer.Tick += delegate
		{
			gifPlayer.Advance();
		};
		TaskScheduler scheduler = TaskScheduler.FromCurrentSynchronizationContext();
		Task.Run(() => GifAnimation.Load(path2, maxWidth)).ContinueWith(delegate(Task<GifAnimation> t)
		{
			if (!gifPlayer._disposed)
			{
				gifPlayer._animation = (t.IsCompletedSuccessfully ? t.Result : null);
				if (gifPlayer._animation == null || gifPlayer._animation.Frames.Count == 0)
				{
					Log.Warn("GIF-обои: не удалось прочитать кадры " + gifPlayer._name);
					{
						foreach (GifView view in gifPlayer._views)
						{
							view.MarkReady();
						}
						return;
					}
				}
				EventTrace.Mark("wallpaper.gif.loaded", $"{gifPlayer._name} {gifPlayer._animation.Frames.Count} frames {gifPlayer._animation.PixelWidth}x{gifPlayer._animation.PixelHeight}");
				gifPlayer._frame = 0;
				foreach (GifView view2 in gifPlayer._views)
				{
					view2.Image.Source = gifPlayer._animation.Frames[0];
					view2.MarkReady();
				}
				gifPlayer.Reschedule();
			}
		}, scheduler);
	}

	public GifView CreateView(Stretch stretch, ImageSource? poster)
	{
		GifView gifView = new GifView(this, stretch, poster);
		GifAnimation animation = _animation;
		if (animation != null)
		{
			IReadOnlyList<BitmapSource> frames = animation.Frames;
			if (frames != null && frames.Count > 0)
			{
				gifView.Image.Source = animation.Frames[Math.Clamp(_frame, 0, animation.Frames.Count - 1)];
				gifView.MarkReady();
			}
		}
		_views.Add(gifView);
		return gifView;
	}

	internal void Remove(GifView view)
	{
		_views.Remove(view);
		Reschedule();
	}

	public void Reschedule()
	{
		_timer.Stop();
		if (!_disposed && _animation != null && _animation.Frames.Count >= 2 && !_views.All((GifView v) => v.Paused))
		{
			_timer.Interval = TimeSpan.FromSeconds(_animation.DelayOf(_frame));
			_timer.Start();
		}
	}

	private void Advance()
	{
		try
		{
			if (_animation == null)
			{
				return;
			}
			_frame = (_frame + 1) % _animation.Frames.Count;
			BitmapSource source = _animation.Frames[_frame];
			foreach (GifView view in _views)
			{
				if (!view.Paused)
				{
					view.Image.Source = source;
				}
			}
			Reschedule();
		}
		catch (Exception ex)
		{
			Log.Error("GIF-обои: кадр", ex);
			_timer.Stop();
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_timer.Stop();
	}
}
