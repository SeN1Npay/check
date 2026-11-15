using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class VideoContent : IAnimatedContent, IDisposable
{
	private readonly MediaElement _media;

	private readonly MediaClock _clock;

	private readonly DispatcherTimer _readyFallback = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(4.0)
	};

	private readonly string _name;

	private Action? _onReady;

	private bool _ready;

	private bool _paused;

	private bool _disposed;

	public FrameworkElement Element { get; }

	public bool Paused
	{
		get
		{
			return _paused;
		}
		set
		{
			if (_paused != value && !_disposed)
			{
				_paused = value;
				if (value)
				{
					_clock.Controller?.Pause();
				}
				else
				{
					_clock.Controller?.Resume();
				}
				EventTrace.Mark(value ? "wallpaper.video.pause" : "wallpaper.video.resume", _name);
			}
		}
	}

	public VideoContent(string path, bool muted, double volume, Stretch stretch, ImageSource? poster)
	{
		_name = Path.GetFileName(path);
		_media = new MediaElement
		{
			Stretch = stretch,
			IsMuted = muted,
			Volume = (muted ? 0.0 : Math.Clamp(volume, 0.0, 1.0)),
			ScrubbingEnabled = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		RenderOptions.SetBitmapScalingMode(_media, BitmapScalingMode.LowQuality);
		MediaTimeline mediaTimeline = new MediaTimeline(new Uri(path, UriKind.Absolute))
		{
			RepeatBehavior = RepeatBehavior.Forever
		};
		_clock = mediaTimeline.CreateClock();
		_media.Clock = _clock;
		_media.MediaOpened += delegate
		{
			EventTrace.Mark("wallpaper.video.opened", _name);
			_media.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(MarkReady));
		};
		_media.MediaFailed += delegate(object? _, ExceptionRoutedEventArgs e)
		{
			EventTrace.Mark("wallpaper.video.failed", e.ErrorException?.Message ?? "");
			Log.Error("Видеообои: не удалось воспроизвести " + _name + " (на N-редакциях Windows нужен Media Feature Pack)", e.ErrorException);
			MarkReady();
		};
		_readyFallback.Tick += delegate
		{
			MarkReady();
		};
		_readyFallback.Start();
		Element = new Grid
		{
			Background = ((poster == null) ? ((Brush)Brushes.Black) : ((Brush)new ImageBrush(poster)
			{
				Stretch = ((stretch != 0) ? stretch : Stretch.None)
			})),
			Children = { (UIElement)_media }
		};
		_clock.Controller?.Begin();
	}

	public void SetVolume(double volume)
	{
		if (!_disposed && !_media.IsMuted)
		{
			_media.Volume = Math.Clamp(volume, 0.0, 1.0);
		}
	}

	public void WhenReady(Action ready)
	{
		if (_ready)
		{
			ready();
		}
		else
		{
			_onReady = (Action)Delegate.Combine(_onReady, ready);
		}
	}

	private void MarkReady()
	{
		_readyFallback.Stop();
		if (!_ready)
		{
			_ready = true;
			Action? onReady = _onReady;
			_onReady = null;
			onReady?.Invoke();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_readyFallback.Stop();
		try
		{
			_clock.Controller?.Stop();
			_media.Clock = null;
			_media.Close();
		}
		catch (Exception ex)
		{
			Log.Error("Видеообои: закрытие", ex);
		}
	}
}
