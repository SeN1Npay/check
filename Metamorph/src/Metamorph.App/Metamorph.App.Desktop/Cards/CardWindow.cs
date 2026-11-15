using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Desktop;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

// Одно маленькое окно на карточку: слой рабочего стола (низ z-порядка), не крадёт фокус.
internal sealed class CardWindow : Window
{
	private struct WINDOWPOS
	{
		public nint hwnd;

		public nint hwndInsertAfter;

		public int x;

		public int y;

		public int cx;

		public int cy;

		public uint flags;
	}

	private const int WM_WINDOWPOSCHANGING = 70;

	private const uint SWP_NOZORDER = 4u;

	// SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
	private const uint SwpKeep = 19u;

	private static readonly nint HwndBottom = new IntPtr(1);

	private static readonly nint HwndTop = IntPtr.Zero;

	private readonly CardsController _owner;

	private readonly CardTheme _theme;

	private readonly double _margin;

	private readonly Grid _frame;

	private readonly ScaleTransform _scale = new ScaleTransform(1.0, 1.0);

	private readonly TranslateTransform _lift = new TranslateTransform();

	private readonly Border _stroke;

	private readonly Rectangle _outline;

	private readonly DropShadowEffect? _shadow;

	private nint _hwnd;

	private bool _desktopFront;

	private bool _raised;

	private bool _edit;

	private bool _dragging;

	private Point _dragStart;

	private CardRect _dragOrigin;

	public CardView View { get; }

	public Display Display { get; set; }

	public CardRect Rect { get; private set; }

	public CardWindow(CardsController owner, CardView view, CardTheme theme, Display display, CardRect rect)
	{
		_owner = owner;
		_theme = theme;
		View = view;
		Display = display;
		// Со стеклом окно обрезается регионом по скруглению, поэтому места под внешнюю тень нет.
		_margin = theme.Glass ? 0.0 : 22.0;
		base.Title = "Metamorph Card";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.FontFamily = theme.Font;
		base.Foreground = theme.Foreground;
		base.UseLayoutRounding = true;
		CornerRadius radius = new CornerRadius(theme.Radius);
		if (!theme.Glass)
		{
			_shadow = new DropShadowEffect
			{
				BlurRadius = 26.0,
				ShadowDepth = 6.0,
				Direction = 270.0,
				Opacity = 0.32,
				Color = Colors.Black
			};
		}
		Border surface = new Border
		{
			Background = theme.Surface,
			CornerRadius = radius,
			Effect = _shadow
		};
		Border sheen = new Border
		{
			Background = theme.Sheen,
			CornerRadius = radius,
			IsHitTestVisible = false
		};
		double w = rect.Width / theme.Scale;
		double h = rect.Height / theme.Scale;
		FrameworkElement content;
		try
		{
			content = view.Build(w, h);
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: построение " + view.Settings.Kind, ex);
			content = new Grid();
		}
		Border holder = new Border
		{
			Width = w,
			Height = h,
			Child = content,
			LayoutTransform = new ScaleTransform(theme.Scale, theme.Scale),
			Clip = new RectangleGeometry(new System.Windows.Rect(0.0, 0.0, w, h), theme.Radius / theme.Scale, theme.Radius / theme.Scale)
		};
		_stroke = new Border
		{
			BorderBrush = theme.Border,
			BorderThickness = new Thickness(1.0),
			CornerRadius = radius,
			IsHitTestVisible = false
		};
		DoubleCollection dash = new DoubleCollection { 5.0, 3.0 };
		dash.Freeze();
		_outline = new Rectangle
		{
			Stroke = theme.Accent,
			StrokeThickness = 1.6,
			StrokeDashArray = dash,
			RadiusX = theme.Radius,
			RadiusY = theme.Radius,
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		TransformGroup transforms = new TransformGroup();
		transforms.Children.Add(_scale);
		transforms.Children.Add(_lift);
		_frame = new Grid
		{
			Margin = new Thickness(_margin),
			RenderTransformOrigin = new Point(0.5, 0.5),
			RenderTransform = transforms,
			Children = { surface, sheen, holder, _stroke, _outline }
		};
		base.Content = _frame;
		Place(rect);
		_frame.MouseEnter += delegate
		{
			Hover(on: true);
		};
		_frame.MouseLeave += delegate
		{
			Hover(on: false);
		};
		base.SizeChanged += delegate
		{
			UpdateRegion();
		};
	}

	public void Place(CardRect r)
	{
		Rect = r;
		base.Left = r.X - _margin;
		base.Top = r.Y - _margin;
		base.Width = r.Width + _margin * 2.0;
		base.Height = r.Height + _margin * 2.0;
	}

	public void Appear(int index)
	{
		if (!DesktopActivity.CanAnimate)
		{
			return;
		}
		TimeSpan delay = TimeSpan.FromMilliseconds(70 * Math.Min(index, 10));
		Duration duration = new Duration(TimeSpan.FromMilliseconds(460.0));
		CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		base.Opacity = 0.0;
		BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0, duration) { BeginTime = delay, EasingFunction = ease });
		DoubleAnimation grow = new DoubleAnimation(0.94, 1.0, duration) { BeginTime = delay, EasingFunction = ease };
		_scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
		_scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
		_lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(_theme.Glass ? 0.0 : 10.0, 0.0, duration) { BeginTime = delay, EasingFunction = ease });
	}

	private void Hover(bool on)
	{
		if (_dragging)
		{
			return;
		}
		if (!_edit)
		{
			_stroke.BorderBrush = on ? _theme.BorderHover : _theme.Border;
		}
		// Со стеклом подъём обрезался бы регионом окна — только подсветка рамки.
		if (_theme.Glass || !DesktopActivity.CanAnimate)
		{
			return;
		}
		Duration d = new Duration(TimeSpan.FromMilliseconds(180.0));
		CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
		_lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(on ? -3.0 : 0.0, d) { EasingFunction = ease });
		_shadow?.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(on ? 34.0 : 26.0, d));
		_shadow?.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(on ? 0.45 : 0.32, d));
	}

	public void SetEdit(bool on)
	{
		_edit = on;
		_outline.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
		_stroke.BorderBrush = _theme.Border;
		base.Cursor = on ? Cursors.SizeAll : null;
		SetRaised(on);
	}

	// В режиме редактирования карточки поднимаются над окнами, чтобы их было видно.
	private void SetRaised(bool raised)
	{
		_raised = raised;
		if (_hwnd != IntPtr.Zero)
		{
			Win32.SetWindowPos(_hwnd, raised ? HwndTop : HwndBottom, 0, 0, 0, 0, SwpKeep);
		}
	}

	public void KeepOnDesktop()
	{
		if (_hwnd == IntPtr.Zero || _raised)
		{
			return;
		}
		bool front = DesktopLayer.IsDesktopForeground();
		if (front != _desktopFront)
		{
			_desktopFront = front;
			Win32.SetWindowPos(_hwnd, front ? HwndTop : HwndBottom, 0, 0, 0, 0, SwpKeep);
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(_hwnd, !View.NeedsKeyboard);
		HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
		Win32.SetWindowPos(_hwnd, _raised ? HwndTop : HwndBottom, 0, 0, 0, 0, SwpKeep);
		if (_theme.Glass)
		{
			WindowEffects.EnableBlurBehind(_hwnd);
			UpdateRegion();
		}
	}

	private void UpdateRegion()
	{
		if (_hwnd == IntPtr.Zero || !_theme.Glass)
		{
			return;
		}
		try
		{
			double dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? Display.Scale;
			int w = (int)Math.Ceiling(base.Width * dpi);
			int h = (int)Math.Ceiling(base.Height * dpi);
			int d = (int)Math.Round(_theme.Radius * 2.0 * dpi);
			nint rgn = Win32.CreateRoundRectRgn(0, 0, w + 1, h + 1, d, d);
			// После успешного SetWindowRgn регионом владеет система.
			if (rgn != IntPtr.Zero && Win32.SetWindowRgn(_hwnd, rgn, redraw: true) == 0)
			{
				ShellNative.DeleteObject(rgn);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: регион окна", ex);
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == WM_WINDOWPOSCHANGING && !_desktopFront && !_raised && lParam != IntPtr.Zero)
		{
			WINDOWPOS pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
			if ((pos.flags & SWP_NOZORDER) == 0 && pos.hwndInsertAfter != HwndBottom)
			{
				pos.hwndInsertAfter = HwndBottom;
				Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
			}
		}
		return IntPtr.Zero;
	}

	protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseDown(e);
		if (!_edit && View.NeedsKeyboard && e.ChangedButton == MouseButton.Left)
		{
			Activate();
		}
	}

	protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonDown(e);
		if (!_edit)
		{
			return;
		}
		e.Handled = true;
		// Активируем окно, чтобы Esc дошёл до нас.
		Activate();
		_dragging = true;
		_dragStart = PointToScreen(e.GetPosition(this));
		_dragOrigin = Rect;
		CaptureMouse();
	}

	protected override void OnPreviewMouseMove(MouseEventArgs e)
	{
		base.OnPreviewMouseMove(e);
		if (!_dragging)
		{
			return;
		}
		Point now = PointToScreen(e.GetPosition(this));
		double dpi = Display.Scale;
		_owner.Drag(this, _dragOrigin.Offset((now.X - _dragStart.X) / dpi, (now.Y - _dragStart.Y) / dpi));
	}

	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseLeftButtonUp(e);
		if (_dragging)
		{
			e.Handled = true;
			EndDrag();
		}
	}

	protected override void OnLostMouseCapture(MouseEventArgs e)
	{
		base.OnLostMouseCapture(e);
		if (_dragging)
		{
			EndDrag();
		}
	}

	private void EndDrag()
	{
		_dragging = false;
		if (IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		_owner.EndDrag(this);
	}

	protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
	{
		base.OnPreviewMouseRightButtonUp(e);
		// В заметке/списке правый клик оставляем системному меню текстового поля.
		if (_edit || !View.NeedsKeyboard || e.OriginalSource is not DependencyObject src || FindTextBox(src) == null)
		{
			e.Handled = true;
			_owner.OpenMenu(this, _frame);
		}
	}

	private static TextBox? FindTextBox(DependencyObject? d)
	{
		while (d != null)
		{
			if (d is TextBox t)
			{
				return t;
			}
			d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
		}
		return null;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Escape && _edit)
		{
			e.Handled = true;
			_owner.ToggleEditMode();
		}
	}
}
