using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class DesktopWidgetWindow : Window
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

	private static readonly nint HwndBottom = new IntPtr(1);

	private static readonly nint HwndTop = IntPtr.Zero;

	private readonly ThemeProfile _profile;

	private readonly MetamorphServices _services;

	private readonly WidgetSettings _w;

	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private readonly SystemStats _stats = new SystemStats();

	private readonly List<Action> _updaters = new List<Action>();

	private readonly Brush _fg;

	private readonly Brush _accent;

	private readonly Brush _muted;

	private readonly Brush _track;

	private nint _hwnd;

	private bool _desktopFront;

	private long _tick;

	private WeatherNow? _weather;

	private DateTime _weatherAt;

	private MediaNow? _media;

	public DesktopWidgetWindow(ThemeProfile profile, MetamorphServices services)
	{
		_profile = profile;
		_services = services;
		_w = profile.Desktop.Widget;
		Palette palette = profile.Palette;
		_fg = BrushFactory.Solid(palette.Foreground);
		_accent = BrushFactory.Solid(palette.Accent);
		_muted = BrushFactory.Solid(palette.Muted);
		_track = BrushFactory.Solid(palette.Muted, 0.35);
		base.Title = "Metamorph Widget";
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.ShowInTaskbar = false;
		base.ShowActivated = false;
		base.ResizeMode = ResizeMode.NoResize;
		base.SizeToContent = SizeToContent.WidthAndHeight;
		base.Focusable = false;
		base.FontFamily = new FontFamily((_w.FontFamily ?? profile.Bar.FontFamily) + ", Consolas");
		base.Foreground = _fg;
		double num = Math.Clamp(_w.Scale, 0.5, 3.0);
		base.LayoutTransform = new ScaleTransform(num, num);
		StackPanel stackPanel = new StackPanel
		{
			MinWidth = 240.0
		};
		Border border = new Border
		{
			Child = stackPanel,
			Padding = new Thickness(18.0, 14.0, 18.0, 14.0),
			CornerRadius = new CornerRadius(14.0),
			Background = ((_w.Opacity > 0.0) ? BrushFactory.Solid(palette.Background, Math.Clamp(_w.Opacity, 0.0, 1.0)) : Brushes.Transparent)
		};
		if (_w.Opacity <= 0.0)
		{
			border.Effect = new DropShadowEffect
			{
				BlurRadius = 6.0,
				ShadowDepth = 0.0,
				Opacity = 0.6,
				Color = Colors.Black
			};
		}
		base.Content = border;
		foreach (string module in _w.Modules)
		{
			Build(stackPanel, module.Trim().ToLowerInvariant());
		}
		_timer.Tick += delegate
		{
			Tick();
		};
		base.Loaded += delegate
		{
			Tick();
			Position();
			_timer.Start();
		};
		base.SizeChanged += delegate
		{
			Position();
		};
	}

	private void Build(Panel stack, string module)
	{
		if (module == null)
		{
			return;
		}
		switch (module.Length)
		{
		case 5:
			switch (module[0])
			{
			case 'c':
				if (module == "clock")
				{
					TextBlock t2 = Text(64.0, _fg, FontWeights.Light);
					t2.Margin = new Thickness(0.0, -8.0, 0.0, -4.0);
					stack.Children.Add(t2);
					_updaters.Add(delegate
					{
						t2.Text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
					});
				}
				break;
			case 'm':
			{
				if (!(module == "media"))
				{
					break;
				}
				TextBlock v8;
				FrameworkElement row2 = Row("♪", out v8);
				row2.Visibility = Visibility.Collapsed;
				stack.Children.Add(row2);
				_updaters.Add(delegate
				{
					if (_tick % 3 == 0L)
					{
						RefreshMediaAsync();
					}
					row2.Visibility = (((object)_media == null) ? Visibility.Collapsed : Visibility.Visible);
					MediaNow media = _media;
					if ((object)media != null)
					{
						v8.Text = (media.Playing ? "▶ " : "❚❚ ") + MediaInfo.Format(media, 40);
					}
				});
				break;
			}
			}
			break;
		case 4:
			switch (module[1])
			{
			case 'a':
				if (module == "date")
				{
					TextBlock t = Text(16.0, _accent);
					t.Margin = new Thickness(2.0, 0.0, 0.0, 10.0);
					stack.Children.Add(t);
					_updaters.Add(delegate
					{
						t.Text = DateTime.Now.ToString("dddd, d MMMM", Loc.Culture);
					});
				}
				break;
			case 'i':
			{
				if (!(module == "disk"))
				{
					break;
				}
				stack.Children.Add(Row("disk", out TextBlock v7, out Border bar4));
				_updaters.Add(delegate
				{
					if (_tick % 30 == 0L)
					{
						(string, double, double)? tuple4 = SystemStats.Disk();
						if (tuple4.HasValue)
						{
							(string, double, double) valueOrDefault5 = tuple4.GetValueOrDefault();
							v7.Text = $"{valueOrDefault5.Item1} {valueOrDefault5.Item3:0}G " + Loc.T("свободно");
							SetBar(bar4, valueOrDefault5.Item2);
						}
					}
				});
				break;
			}
			}
			break;
		case 3:
			switch (module[0])
			{
			case 'c':
			{
				if (!(module == "cpu"))
				{
					break;
				}
				stack.Children.Add(Row("cpu", out TextBlock v, out Border bar));
				_updaters.Add(delegate
				{
					double? num = _stats.Cpu();
					if (num.HasValue)
					{
						double valueOrDefault = num.GetValueOrDefault();
						v.Text = $"{valueOrDefault:0}%";
						SetBar(bar, valueOrDefault);
					}
				});
				break;
			}
			case 'r':
			{
				if (!(module == "ram"))
				{
					break;
				}
				stack.Children.Add(Row("ram", out TextBlock v2, out Border bar2));
				_updaters.Add(delegate
				{
					if (_tick % 2 == 0L)
					{
						(double, double, uint)? tuple = SystemStats.Memory();
						if (tuple.HasValue)
						{
							(double, double, uint) valueOrDefault2 = tuple.GetValueOrDefault();
							v2.Text = $"{valueOrDefault2.Item1:0.0}/{valueOrDefault2.Item2:0}G";
							SetBar(bar2, valueOrDefault2.Item3);
						}
					}
				});
				break;
			}
			}
			break;
		case 7:
			switch (module[0])
			{
			case 'n':
			{
				if (!(module == "network"))
				{
					break;
				}
				stack.Children.Add(Row("net", out TextBlock v4));
				_updaters.Add(delegate
				{
					(double, double)? tuple3 = _stats.Network();
					if (tuple3.HasValue)
					{
						(double, double) valueOrDefault4 = tuple3.GetValueOrDefault();
						v4.Text = $"↓ {SystemStats.Rate(valueOrDefault4.Item1)}/s  ↑ {SystemStats.Rate(valueOrDefault4.Item2)}/s";
					}
				});
				break;
			}
			case 'b':
			{
				if (!(module == "battery"))
				{
					break;
				}
				TextBlock v3;
				Border bar3;
				FrameworkElement row = Row("bat", out v3, out bar3);
				stack.Children.Add(row);
				_updaters.Add(delegate
				{
					if (_tick % 20 == 0L)
					{
						(int, bool)? tuple2 = SystemStats.Battery();
						row.Visibility = ((!tuple2.HasValue) ? Visibility.Collapsed : Visibility.Visible);
						if (tuple2.HasValue)
						{
							(int, bool) valueOrDefault3 = tuple2.GetValueOrDefault();
							v3.Text = $"{valueOrDefault3.Item1}%{(valueOrDefault3.Item2 ? " ⚡" : "")}";
							SetBar(bar3, valueOrDefault3.Item1);
						}
					}
				});
				break;
			}
			case 'w':
			{
				if (!(module == "weather"))
				{
					break;
				}
				TextBlock v5;
				FrameworkElement element = Row(Loc.T("погода"), out v5);
				stack.Children.Add(element);
				_updaters.Add(delegate
				{
					WeatherNow weather = _weather;
					if ((object)weather != null)
					{
						v5.Text = $"{weather.Icon} {weather.TemperatureText}  {weather.Description}";
					}
					if (DateTime.UtcNow - _weatherAt > TimeSpan.FromMinutes(15.0))
					{
						RefreshWeatherAsync();
					}
				});
				break;
			}
			}
			break;
		case 8:
			if (module == "hostname")
			{
				stack.Children.Add(Row("host", out TextBlock value));
				value.Text = Environment.UserName + "@" + Environment.MachineName.ToLowerInvariant();
			}
			break;
		case 6:
		{
			if (!(module == "uptime"))
			{
				break;
			}
			stack.Children.Add(Row("up", out TextBlock v6));
			_updaters.Add(delegate
			{
				if (_tick % 30 == 0L)
				{
					v6.Text = SystemStats.Uptime();
				}
			});
			break;
		}
		}
	}

	private async Task RefreshWeatherAsync()
	{
		_weatherAt = DateTime.UtcNow;
		try
		{
			_weather = (await _services.Weather.GetAsync(_profile.Bar.Weather)) ?? _weather;
		}
		catch (Exception ex)
		{
			Log.Error("Погода (виджет)", ex);
		}
	}

	private async Task RefreshMediaAsync()
	{
		try
		{
			_media = await MediaInfo.GetAsync();
		}
		catch (Exception ex)
		{
			Log.Error("Медиа (виджет)", ex);
		}
	}

	private TextBlock Text(double size, Brush brush, FontWeight? weight = null)
	{
		return new TextBlock
		{
			FontSize = size,
			Foreground = brush,
			FontWeight = (weight ?? FontWeights.Normal)
		};
	}

	private FrameworkElement Row(string label, out TextBlock value)
	{
		Grid obj = new Grid
		{
			Margin = new Thickness(2.0, 3.0, 2.0, 3.0),
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(52.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				}
			}
		};
		TextBlock textBlock = Text(13.0, _accent);
		textBlock.Text = label;
		value = Text(13.0, _fg);
		Grid.SetColumn(value, 1);
		obj.Children.Add(textBlock);
		obj.Children.Add(value);
		return obj;
	}

	private FrameworkElement Row(string label, out TextBlock value, out Border bar)
	{
		StackPanel obj = new StackPanel
		{
			Children = { (UIElement)Row(label, out value) }
		};
		Border border = new Border
		{
			Height = 3.0,
			Margin = new Thickness(54.0, 0.0, 2.0, 5.0),
			Background = _track,
			CornerRadius = new CornerRadius(1.5)
		};
		bar = new Border
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			Width = 0.0,
			Background = _accent,
			CornerRadius = new CornerRadius(1.5)
		};
		border.Child = bar;
		obj.Children.Add(border);
		return obj;
	}

	private static void SetBar(Border bar, double percent)
	{
		if (bar.Parent is Border border)
		{
			double num = Math.Max(0.0, border.ActualWidth);
			bar.Width = num * Math.Clamp(percent, 0.0, 100.0) / 100.0;
		}
	}

	private void Tick()
	{
		foreach (Action updater in _updaters)
		{
			try
			{
				updater();
			}
			catch (Exception ex)
			{
				Log.Error("Виджет рабочего стола", ex);
			}
		}
		_tick++;
		KeepOnDesktop();
	}

	private void Position()
	{
		Rect workAreaDip = Display.PrimaryDisplay.WorkAreaDip;
		double actualWidth = base.ActualWidth;
		double actualHeight = base.ActualHeight;
		WidgetCorner corner = _w.Corner;
		bool flag = ((corner == WidgetCorner.TopRight || corner == WidgetCorner.BottomRight) ? true : false);
		bool flag2 = flag;
		corner = _w.Corner;
		flag = (uint)(corner - 2) <= 1u;
		bool flag3 = flag;
		base.Left = (flag2 ? (workAreaDip.Right - actualWidth - 28.0) : (workAreaDip.Left + 28.0));
		base.Top = (flag3 ? (workAreaDip.Bottom - actualHeight - 28.0) : (workAreaDip.Top + 28.0));
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_hwnd = new WindowInteropHelper(this).Handle;
		WindowEffects.MakeToolWindow(_hwnd, noActivate: true);
		long windowLong = NativeMethods.GetWindowLong(_hwnd, -20);
		NativeMethods.SetWindowLong(_hwnd, -20, windowLong | 0x20 | 0x80000);
		HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
		Win32.SetWindowPos(_hwnd, HwndBottom, 0, 0, 0, 0, 19u);
	}

	private void KeepOnDesktop()
	{
		if (_hwnd != IntPtr.Zero)
		{
			bool flag = DesktopLayer.IsDesktopForeground();
			if (flag != _desktopFront)
			{
				_desktopFront = flag;
				Win32.SetWindowPos(_hwnd, flag ? HwndTop : HwndBottom, 0, 0, 0, 0, 19u);
			}
		}
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 70 && !_desktopFront && lParam != IntPtr.Zero)
		{
			WINDOWPOS structure = Marshal.PtrToStructure<WINDOWPOS>(lParam);
			if ((structure.flags & 4) == 0 && structure.hwndInsertAfter != HwndBottom)
			{
				structure.hwndInsertAfter = HwndBottom;
				Marshal.StructureToPtr(structure, lParam, fDeleteOld: false);
			}
		}
		return IntPtr.Zero;
	}

	protected override void OnClosed(EventArgs e)
	{
		_timer.Stop();
		base.OnClosed(e);
	}
}
