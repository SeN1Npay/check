using System;

namespace Metamorph.Core.Effects;

// Полосы визуализатора: пересэмплирование, сглаживание и падающие пики — без выделений памяти на кадр.
public sealed class VisualizerBands
{
	private readonly float[] _target;

	private readonly float[] _values;

	private readonly float[] _peaks;

	private readonly float[] _peakHold;

	private readonly float[] _peakVelocity;

	public const float PeakHoldSeconds = 0.35f;

	// Ускорение падения пика (доли высоты в с²) — медленно.
	public const float PeakGravity = 1.4f;

	public double Attack { get; set; } = 28.0;

	public double Decay { get; set; } = 7.0;

	public int Count => _values.Length;

	public float[] Values => _values;

	public float[] Peaks => _peaks;

	public float Level { get; private set; }

	public VisualizerBands(int count)
	{
		count = Math.Clamp(count, 1, 256);
		_target = new float[count];
		_values = new float[count];
		_peaks = new float[count];
		_peakHold = new float[count];
		_peakVelocity = new float[count];
	}

	public void Update(ReadOnlySpan<float> raw, double dt)
	{
		Resample(raw, _target);
		float up = (float)(1.0 - Math.Exp(-Attack * Math.Max(0.0, dt)));
		float down = (float)(1.0 - Math.Exp(-Decay * Math.Max(0.0, dt)));
		float fdt = (float)Math.Max(0.0, dt);
		float level = 0f;
		for (int i = 0; i < _values.Length; i++)
		{
			float t = _target[i];
			float v = _values[i];
			v += (t - v) * (t > v ? up : down);
			_values[i] = v;
			if (v > level)
			{
				level = v;
			}
			if (v >= _peaks[i])
			{
				_peaks[i] = v;
				_peakHold[i] = PeakHoldSeconds;
				_peakVelocity[i] = 0f;
			}
			else if (_peakHold[i] > 0f)
			{
				_peakHold[i] -= fdt;
			}
			else
			{
				_peakVelocity[i] += PeakGravity * fdt;
				_peaks[i] = Math.Max(v, _peaks[i] - _peakVelocity[i] * fdt);
			}
		}
		Level = level;
	}

	public void Clear()
	{
		Array.Clear(_target);
		Array.Clear(_values);
		Array.Clear(_peaks);
		Array.Clear(_peakHold);
		Array.Clear(_peakVelocity);
		Level = 0f;
	}

	// Среднее по исходным полосам, попадающим в целевую (или линейная интерполяция, если целевых больше).
	public static void Resample(ReadOnlySpan<float> source, Span<float> target)
	{
		int n = source.Length;
		int m = target.Length;
		if (m == 0)
		{
			return;
		}
		if (n == 0)
		{
			target.Clear();
			return;
		}
		if (m >= n)
		{
			for (int i = 0; i < m; i++)
			{
				double pos = m == 1 ? 0.0 : (double)i * (n - 1) / (m - 1);
				int a = (int)pos;
				int b = Math.Min(n - 1, a + 1);
				double f = pos - a;
				target[i] = (float)(source[a] + (source[b] - source[a]) * f);
			}
			return;
		}
		for (int i = 0; i < m; i++)
		{
			int from = i * n / m;
			int to = Math.Max(from + 1, (i + 1) * n / m);
			float sum = 0f;
			for (int j = from; j < to; j++)
			{
				sum += source[j];
			}
			target[i] = sum / (to - from);
		}
	}
}

// Прячет визуализатор после тишины и плавно показывает при звуке.
public sealed class SilenceGate
{
	public double SilenceSeconds { get; set; } = 2.0;

	public double FadeSeconds { get; set; } = 0.45;

	public float Threshold { get; set; } = 0.02f;

	private double _quiet;

	public double Fade { get; private set; }

	public bool Sounding { get; private set; }

	// Нужно ли рисовать (видно или ещё затухает).
	public bool Visible => Fade > 0.001;

	public void Update(float level, double dt)
	{
		dt = Math.Max(0.0, dt);
		if (level > Threshold)
		{
			_quiet = 0.0;
			Sounding = true;
		}
		else
		{
			_quiet += dt;
			if (_quiet >= SilenceSeconds)
			{
				Sounding = false;
			}
		}
		double step = FadeSeconds <= 0.0 ? 1.0 : dt / FadeSeconds;
		Fade = Sounding ? Math.Min(1.0, Fade + step) : Math.Max(0.0, Fade - step);
	}

	public void Reset()
	{
		_quiet = 0.0;
		Fade = 0.0;
		Sounding = false;
	}
}
