using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class DesktopSurface : IDisposable
{
	private readonly HwndSource _source;

	private readonly Grid _root;

	private IAnimatedContent? _current;

	private IAnimatedContent? _incoming;

	private bool _disposed;

	private const uint WS_CLIPSIBLINGS = 67108864u;

	private const uint WS_CLIPCHILDREN = 33554432u;

	public nint Handle => _source.Handle;

	public bool IsAlive
	{
		get
		{
			if (!_disposed && !_source.IsDisposed)
			{
				return Win32.IsWindow(_source.Handle);
			}
			return false;
		}
	}

	public DesktopSurface(nint workerW, Rectangle bounds, System.Drawing.Point origin)
	{
		HwndSourceParameters parameters = new HwndSourceParameters("Metamorph.Wallpaper")
		{
			ParentWindow = workerW,
			WindowStyle = 1442840576,
			PositionX = bounds.X - origin.X,
			PositionY = bounds.Y - origin.Y,
			Width = bounds.Width,
			Height = bounds.Height
		};
		_source = new HwndSource(parameters);
		HwndTarget compositionTarget = _source.CompositionTarget;
		if (compositionTarget != null)
		{
			compositionTarget.BackgroundColor = Colors.Black;
		}
		_root = new Grid
		{
			Background = System.Windows.Media.Brushes.Black,
			ClipToBounds = true
		};
		_source.RootVisual = _root;
	}

	public void Show(IAnimatedContent content)
	{
		IAnimatedContent content2 = content;
		if (_disposed)
		{
			content2.Dispose();
			return;
		}
		if (_incoming != null)
		{
			_root.Children.Remove(_incoming.Element);
			_incoming.Dispose();
			_incoming = null;
		}
		_root.Children.Add(content2.Element);
		if (_current == null)
		{
			content2.Element.Opacity = 1.0;
			_current = content2;
			return;
		}
		content2.Element.Opacity = 0.0;
		_incoming = content2;
		content2.WhenReady(delegate
		{
			FadeIn(content2);
		});
	}

	private void FadeIn(IAnimatedContent content)
	{
		IAnimatedContent content2 = content;
		if (_disposed || _incoming != content2)
		{
			return;
		}
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350.0))
		{
			FillBehavior = FillBehavior.HoldEnd
		};
		doubleAnimation.Completed += delegate
		{
			if (!_disposed && _incoming == content2)
			{
				IAnimatedContent current = _current;
				_current = content2;
				_incoming = null;
				content2.Element.BeginAnimation(UIElement.OpacityProperty, null);
				content2.Element.Opacity = 1.0;
				if (current != null)
				{
					_root.Children.Remove(current.Element);
					current.Dispose();
				}
			}
		};
		content2.Element.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			try
			{
				_incoming?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("Обои: закрытие", ex);
			}
			try
			{
				_current?.Dispose();
			}
			catch (Exception ex2)
			{
				Log.Error("Обои: закрытие", ex2);
			}
			_incoming = (_current = null);
			_source.Dispose();
		}
	}
}
