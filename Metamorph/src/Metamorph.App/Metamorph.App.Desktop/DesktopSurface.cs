using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

// Подложка в слое WorkerW под значками: контент обоев (с переходами и параллаксом) + слой эффектов поверх.
internal sealed class DesktopSurface : IDisposable
{
	private readonly HwndSource _source;

	private readonly Grid _root;

	// Обои (картинка/GIF/видео); параллакс двигает только этот слой.
	private readonly Grid _host;

	// Эффекты рабочего стола (частицы, визуализатор, тонировка).
	private readonly Grid _overlay;

	private readonly ScaleTransform _parallaxScale = new ScaleTransform(1.0, 1.0);

	private readonly TranslateTransform _parallaxShift = new TranslateTransform();

	private IAnimatedContent? _current;

	private IAnimatedContent? _incoming;

	private bool _disposed;

	private const uint WS_CLIPSIBLINGS = 67108864u;

	private const uint WS_CLIPCHILDREN = 33554432u;

	public nint Handle => _source.Handle;

	public Rectangle Bounds { get; }

	// Размер в DIP (приложение system-DPI aware).
	public System.Windows.Size SizeDip { get; }

	// Последний показанный контент (входящий во время перехода).
	public IAnimatedContent? Content => _incoming ?? _current;

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
		Bounds = bounds;
		double scale = Metamorph.App.Infrastructure.Display.Scale;
		SizeDip = new System.Windows.Size(bounds.Width / scale, bounds.Height / scale);
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
		TransformGroup parallax = new TransformGroup();
		parallax.Children.Add(_parallaxScale);
		parallax.Children.Add(_parallaxShift);
		_host = new Grid
		{
			RenderTransform = parallax,
			RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
			IsHitTestVisible = false
		};
		_overlay = new Grid
		{
			IsHitTestVisible = false
		};
		_root = new Grid
		{
			Background = System.Windows.Media.Brushes.Black,
			ClipToBounds = true,
			IsHitTestVisible = false
		};
		_root.Children.Add(_host);
		_root.Children.Add(_overlay);
		_source.RootVisual = _root;
	}

	public void SetOverlay(UIElement? overlay)
	{
		if (_disposed)
		{
			return;
		}
		_overlay.Children.Clear();
		if (overlay != null)
		{
			_overlay.Children.Add(overlay);
		}
	}

	// Сдвиг обоев (DIP) и запас масштаба, чтобы края не открывались.
	public void SetParallax(double x, double y, double scale)
	{
		if (_disposed)
		{
			return;
		}
		if (_parallaxScale.ScaleX != scale)
		{
			_parallaxScale.ScaleX = scale;
			_parallaxScale.ScaleY = scale;
		}
		_parallaxShift.X = x;
		_parallaxShift.Y = y;
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
			_host.Children.Remove(_incoming.Element);
			_incoming.Dispose();
			_incoming = null;
		}
		_host.Children.Add(content2.Element);
		SurfaceTransition transition = DesktopSurfaces.Transition;
		if (_current == null || transition.Kind == WallpaperTransitionKind.None || transition.Seconds <= 0.01)
		{
			content2.Element.Opacity = 1.0;
			IAnimatedContent current = _current;
			_current = content2;
			if (current != null)
			{
				_host.Children.Remove(current.Element);
				current.Dispose();
			}
			return;
		}
		content2.Element.Opacity = 0.0;
		_incoming = content2;
		content2.WhenReady(delegate
		{
			Transit(content2, transition);
		});
	}

	private void Transit(IAnimatedContent content, SurfaceTransition transition)
	{
		IAnimatedContent content2 = content;
		if (_disposed || _incoming != content2)
		{
			return;
		}
		TimeSpan duration = TimeSpan.FromSeconds(Math.Clamp(transition.Seconds, 0.05, 5.0));
		IEasingFunction ease = new CubicEase
		{
			EasingMode = EasingMode.EaseInOut
		};
		FrameworkElement element = content2.Element;
		FrameworkElement old = _current?.Element;
		double width = SizeDip.Width;
		double fromOpacity = 0.0;
		switch (transition.Kind)
		{
		case WallpaperTransitionKind.Slide:
		{
			fromOpacity = 0.5;
			TranslateTransform shift = new TranslateTransform(width, 0.0);
			element.RenderTransform = shift;
			shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(width, 0.0, duration)
			{
				EasingFunction = ease
			});
			if (old != null)
			{
				TranslateTransform oldShift = new TranslateTransform();
				old.RenderTransform = oldShift;
				oldShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, (0.0 - width) * 0.35, duration)
				{
					EasingFunction = ease
				});
			}
			break;
		}
		case WallpaperTransitionKind.Zoom:
		{
			ScaleTransform zoom = new ScaleTransform(1.12, 1.12);
			element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
			element.RenderTransform = zoom;
			DoubleAnimation z = new DoubleAnimation(1.12, 1.0, duration)
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			zoom.BeginAnimation(ScaleTransform.ScaleXProperty, z);
			zoom.BeginAnimation(ScaleTransform.ScaleYProperty, z);
			break;
		}
		case WallpaperTransitionKind.Blur:
		{
			// BlurEffect — пиксельный шейдер на GPU (не устаревший BitmapEffect), живёт только на время перехода.
			BlurEffect inBlur = new BlurEffect
			{
				Radius = 36.0,
				RenderingBias = RenderingBias.Performance
			};
			element.Effect = inBlur;
			inBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(36.0, 0.0, duration)
			{
				EasingFunction = ease
			});
			if (old != null)
			{
				BlurEffect outBlur = new BlurEffect
				{
					Radius = 0.0,
					RenderingBias = RenderingBias.Performance
				};
				old.Effect = outBlur;
				outBlur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(0.0, 36.0, duration)
				{
					EasingFunction = ease
				});
			}
			break;
		}
		}
		DoubleAnimation fade = new DoubleAnimation(fromOpacity, 1.0, duration)
		{
			FillBehavior = FillBehavior.HoldEnd,
			EasingFunction = ease
		};
		fade.Completed += delegate
		{
			if (!_disposed && _incoming == content2)
			{
				IAnimatedContent current = _current;
				_current = content2;
				_incoming = null;
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = 1.0;
				element.RenderTransform = Transform.Identity;
				element.Effect = null;
				if (current != null)
				{
					_host.Children.Remove(current.Element);
					current.Element.Effect = null;
					current.Dispose();
				}
			}
		};
		element.BeginAnimation(UIElement.OpacityProperty, fade);
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
			_overlay.Children.Clear();
			_source.Dispose();
		}
	}
}
