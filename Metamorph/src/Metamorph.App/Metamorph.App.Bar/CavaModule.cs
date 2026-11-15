using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Metamorph.App.Audio;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Audio;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class CavaModule : BarModule
{
	private const string Levels = "▁▂▃▄▅▆▇█";

	private const int Bars = 12;

	private readonly BarContext _context;

	private readonly TextBlock _text;

	private readonly DispatcherTimer _timer = new DispatcherTimer(DispatcherPriority.Render)
	{
		Interval = TimeSpan.FromMilliseconds(50.0)
	};

	private readonly float[] _raw = new float[48];

	private readonly SpectrumSmoother _smoother = new SpectrumSmoother(12)
	{
		Attack = 25.0,
		Decay = 6.0
	};

	private readonly StringBuilder _builder = new StringBuilder(12);

	private IDisposable? _lease;

	private long _lastTicks = Environment.TickCount64;

	private bool _idle;

	public override FrameworkElement View => _text;

	public override int IntervalTicks => int.MaxValue;

	public CavaModule(BarContext context)
	{
		_context = context;
		_text = new TextBlock
		{
			Text = new string("▁▂▃▄▅▆▇█"[0], 12),
			Foreground = context.Muted,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(7.0, 0.0, 7.0, 0.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			ToolTip = Loc.T("Визуализатор звука: клик — пауза/воспроизведение")
		};
		_text.MouseLeftButtonUp += async delegate
		{
			try
			{
				await MediaInfo.PlayPauseAsync();
			}
			catch (Exception ex2)
			{
				Log.Error("Cava: пауза", ex2);
			}
		};
		try
		{
			_lease = AudioSpectrum.Shared.Acquire();
		}
		catch (Exception ex)
		{
			Log.Error("Cava: захват звука", ex);
		}
		_timer.Tick += delegate
		{
			Tick();
		};
		_timer.Start();
	}

	public override void Update()
	{
	}

	public override void Suspend(bool suspended)
	{
		if (suspended)
		{
			_timer.Stop();
			_lease?.Dispose();
			_lease = null;
			return;
		}
		try
		{
			_lease ??= AudioSpectrum.Shared.Acquire();
		}
		catch (Exception ex)
		{
			Log.Error("Cava: захват звука", ex);
		}
		_lastTicks = Environment.TickCount64;
		_timer.Start();
	}

	private void Tick()
	{
		try
		{
			long tickCount = Environment.TickCount64;
			double dtSeconds = Math.Clamp((double)(tickCount - _lastTicks) / 1000.0, 0.001, 0.25);
			_lastTicks = tickCount;
			bool num = AudioSpectrum.Shared.Read(_raw);
			if (!num)
			{
				Array.Clear(_raw);
			}
			float[] array = _smoother.Update(SpectrumAnalyzer.Resample(_raw, 12), dtSeconds);
			if (!num && _smoother.IsSilent)
			{
				if (!_idle)
				{
					_idle = true;
					_text.Text = new string("▁▂▃▄▅▆▇█"[0], 12);
					_text.Foreground = _context.Muted;
				}
				return;
			}
			_idle = false;
			_builder.Clear();
			float[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				int index = (int)Math.Round(Math.Clamp(array2[i], 0f, 1f) * (float)("▁▂▃▄▅▆▇█".Length - 1));
				_builder.Append("▁▂▃▄▅▆▇█"[index]);
			}
			string text = _builder.ToString();
			if (_text.Text != text)
			{
				_text.Text = text;
			}
			_text.Foreground = _context.Accent;
		}
		catch (Exception ex)
		{
			Log.Error("Cava: кадр", ex);
			_timer.Stop();
		}
	}

	public override void Dispose()
	{
		_timer.Stop();
		_lease?.Dispose();
		_lease = null;
	}
}
