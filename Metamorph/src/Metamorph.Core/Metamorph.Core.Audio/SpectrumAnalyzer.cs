using System;
using System.Collections.Generic;

namespace Metamorph.Core.Audio;

public sealed class SpectrumAnalyzer
{
	private readonly object _gate = new object();

	private readonly float[] _ring;

	private readonly double[] _window;

	private readonly double[] _re;

	private readonly double[] _im;

	private readonly int[] _bandStart;

	private readonly int[] _bandEnd;

	private int _write;

	private long _total;

	public int Bands { get; }

	public int FftSize { get; }

	public int SampleRate { get; }

	public double FloorDb { get; }

	public double CeilingDb { get; }

	public long TotalSamples
	{
		get
		{
			lock (_gate)
			{
				return _total;
			}
		}
	}

	public SpectrumAnalyzer(int bands = 48, int fftSize = 2048, int sampleRate = 48000, double minHz = 40.0, double maxHz = 16000.0, double floorDb = -70.0, double ceilingDb = -12.0)
	{
		if (!Fft.IsPowerOfTwo(fftSize) || fftSize < 64)
		{
			throw new ArgumentOutOfRangeException("fftSize");
		}
		if (bands < 1)
		{
			throw new ArgumentOutOfRangeException("bands");
		}
		Bands = bands;
		FftSize = fftSize;
		SampleRate = sampleRate;
		FloorDb = floorDb;
		CeilingDb = ceilingDb;
		_ring = new float[fftSize];
		_re = new double[fftSize];
		_im = new double[fftSize];
		_window = new double[fftSize];
		for (int i = 0; i < fftSize; i++)
		{
			_window[i] = 0.5 - 0.5 * Math.Cos(Math.PI * 2.0 * (double)i / (double)(fftSize - 1));
		}
		_bandStart = new int[bands];
		_bandEnd = new int[bands];
		_db = new double[bands];
		double val = (double)sampleRate / 2.0;
		maxHz = Math.Min(maxHz, val);
		double num = (double)sampleRate / (double)fftSize;
		for (int j = 0; j < bands; j++)
		{
			double num2 = minHz * Math.Pow(maxHz / minHz, (double)j / (double)bands);
			double num3 = minHz * Math.Pow(maxHz / minHz, (double)(j + 1) / (double)bands);
			int num4 = Math.Clamp((int)Math.Floor(num2 / num), 1, fftSize / 2 - 1);
			int num5 = Math.Clamp((int)Math.Ceiling(num3 / num), num4 + 1, fftSize / 2);
			_bandStart[j] = num4;
			_bandEnd[j] = num5;
		}
	}

	public double BandLowHz(int band)
	{
		return (double)_bandStart[band] * (double)SampleRate / (double)FftSize;
	}

	public void Push(ReadOnlySpan<float> mono)
	{
		lock (_gate)
		{
			ReadOnlySpan<float> readOnlySpan = mono;
			for (int i = 0; i < readOnlySpan.Length; i++)
			{
				float num = readOnlySpan[i];
				_ring[_write] = (float.IsFinite(num) ? num : 0f);
				_write = (_write + 1) % _ring.Length;
			}
			_total += mono.Length;
		}
	}

	public void Clear()
	{
		lock (_gate)
		{
			Array.Clear(_ring);
			_write = 0;
		}
	}

	public float[] Compute()
	{
		float[] result = new float[Bands];
		Compute(result);
		return result;
	}

	public void Compute(float[] result)
	{
		lock (_gate)
		{
			int num = _ring.Length;
			for (int i = 0; i < num; i++)
			{
				_re[i] = (double)_ring[(_write + i) % num] * _window[i];
				_im[i] = 0.0;
			}
			Fft.Transform(_re, _im);
			double num2 = 4.0 / (double)num;
			double peak = double.NegativeInfinity;
			for (int j = 0; j < Bands && j < result.Length; j++)
			{
				double num4 = 0.0;
				for (int k = _bandStart[j]; k < _bandEnd[j]; k++)
				{
					double num5 = Math.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]) * num2;
					if (num5 > num4)
					{
						num4 = num5;
					}
				}
				double num6 = Math.Log2((double)Math.Max(1, _bandStart[j]) / (double)Math.Max(1, _bandStart[0]));
				double db = 20.0 * Math.Log10(num4 + 1E-12) + 1.5 * num6;
				_db[j] = db;
				peak = Math.Max(peak, db);
			}
			(double floor, double ceiling) = Range(peak);
			double range = ceiling - floor;
			for (int j = 0; j < Bands && j < result.Length; j++)
			{
				result[j] = (float)Math.Clamp((_db[j] - floor) / range, 0.0, 1.0);
			}
		}
	}

	// Автоусиление: потолок следует за громкостью трека (быстро вверх, медленно вниз), чтобы громкая музыка
	// не упиралась всеми полосами в максимум, а тихая не терялась у пола.
	public bool AutoGain { get; set; } = true;

	// Сколько дБ под текущим потолком видно на шкале.
	public double DynamicRangeDb { get; set; } = 48.0;

	private readonly double[] _db;

	private double _gainCeiling = double.NaN;

	private long _gainAt;

	private (double Floor, double Ceiling) Range(double peak)
	{
		if (!AutoGain || double.IsNegativeInfinity(peak))
		{
			return (FloorDb, CeilingDb);
		}
		long now = System.Diagnostics.Stopwatch.GetTimestamp();
		double dt = (_gainAt == 0L) ? 0.0 : Math.Min(1.0, (double)(now - _gainAt) / (double)System.Diagnostics.Stopwatch.Frequency);
		_gainAt = now;
		return StepGain(peak, dt);
	}

	// Шаг автоусиления; dt — секунды с прошлого кадра. Вынесено отдельно для тестов.
	public (double Floor, double Ceiling) StepGain(double peakDb, double dt)
	{
		double minCeiling = FloorDb + DynamicRangeDb * 0.5;
		double target = Math.Clamp(peakDb + 2.0, minCeiling, 6.0);
		if (double.IsNaN(_gainCeiling))
		{
			_gainCeiling = Math.Max(target, CeilingDb);
		}
		else if (target > _gainCeiling)
		{
			_gainCeiling += (target - _gainCeiling) * (1.0 - Math.Exp(-dt * 25.0));
		}
		else
		{
			// ~6 дБ/с вниз: пауза в треке не раздувает шум.
			_gainCeiling = Math.Max(target, _gainCeiling - 6.0 * dt);
		}
		return (Math.Max(FloorDb, _gainCeiling - DynamicRangeDb), _gainCeiling);
	}

	public static float[] Resample(IReadOnlyList<float> bands, int count)
	{
		float[] array = new float[Math.Max(0, count)];
		if (bands.Count == 0 || count <= 0)
		{
			return array;
		}
		for (int i = 0; i < count; i++)
		{
			double d = (double)(i * bands.Count) / (double)count;
			double a = (double)((i + 1) * bands.Count) / (double)count;
			int num = (int)Math.Floor(d);
			int num2 = Math.Max(num + 1, (int)Math.Ceiling(a));
			double num3 = 0.0;
			for (int j = num; j < num2 && j < bands.Count; j++)
			{
				num3 += (double)bands[j];
			}
			array[i] = (float)(num3 / (double)Math.Max(1, Math.Min(num2, bands.Count) - num));
		}
		return array;
	}

	public static float Bass(IReadOnlyList<float> bands, int take = 4)
	{
		if (bands.Count == 0)
		{
			return 0f;
		}
		take = Math.Clamp(take, 1, bands.Count);
		float num = 0f;
		for (int i = 0; i < take; i++)
		{
			num += bands[i];
		}
		return num / (float)take;
	}
}
