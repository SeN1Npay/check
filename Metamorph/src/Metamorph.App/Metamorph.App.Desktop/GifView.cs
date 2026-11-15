using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class GifView : IAnimatedContent, IDisposable
{
	private readonly GifPlayer _player;

	private Action? _onReady;

	private bool _ready;

	private bool _paused;

	private bool _disposed;

	public Image Image { get; }

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
				EventTrace.Mark(value ? "wallpaper.gif.pause" : "wallpaper.gif.resume");
			}
		}
	}

	public GifView(GifPlayer player, Stretch stretch, ImageSource? poster)
	{
		_player = player;
		Image = new Image
		{
			Stretch = stretch,
			Source = poster,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.HighQuality);
		Element = new Grid
		{
			Background = Brushes.Black,
			Children = { (UIElement)Image }
		};
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

	internal void MarkReady()
	{
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
		if (!_disposed)
		{
			_disposed = true;
			_player.Remove(this);
			Image.Source = null;
		}
	}
}
