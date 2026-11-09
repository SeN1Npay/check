using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Metamorph.Core.Desktop;

// Расчёт декодирования GIF-обоев: масштаб под монитор, оценка памяти, хэш кадра.
public static class GifFramePlan
{
	public const long DefaultBudget = 384L * 1024 * 1024;

	// Ниже этой доли разрешение ради бюджета не режем — дальше прореживаем кадры.
	public const double MinBudgetScale = 0.7;

	public static long EstimateBytes(int width, int height, int frames)
	{
		return (long)Math.Max(0, width) * Math.Max(0, height) * 4L * Math.Max(0, frames);
	}

	// Масштаб, при котором кадр ещё покрывает монитор (без потери качества при UniformToFill), никогда не больше 1.
	// target ≤ 0 — монитор неизвестен, только ограничение по памяти.
	public static double Scale(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int frames, long budget = DefaultBudget)
	{
		if (sourceWidth <= 0 || sourceHeight <= 0)
		{
			return 1.0;
		}
		double scale = 1.0;
		if (targetWidth > 0 && targetHeight > 0)
		{
			scale = Math.Min(1.0, Math.Max((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight));
		}
		(int w, int h) = ScaledSize(sourceWidth, sourceHeight, scale);
		long bytes = EstimateBytes(w, h, frames);
		if (budget > 0 && bytes > budget)
		{
			scale *= Math.Max(MinBudgetScale, Math.Sqrt((double)budget / bytes));
		}
		return scale;
	}

	public static (int Width, int Height) ScaledSize(int width, int height, double scale)
	{
		return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
	}

	// Дешёвый 64-битный хэш пикселей (FNV-подобный по 8 байт) — для склейки одинаковых соседних кадров.
	public static ulong Hash(ReadOnlySpan<int> pixels)
	{
		ulong h = 14695981039346656037UL;
		ReadOnlySpan<ulong> wide = MemoryMarshal.Cast<int, ulong>(pixels);
		foreach (ulong v in wide)
		{
			h = (h ^ v) * 1099511628211UL;
		}
		for (int i = wide.Length * 2; i < pixels.Length; i++)
		{
			h = (h ^ (uint)pixels[i]) * 1099511628211UL;
		}
		return h ^ (ulong)pixels.Length;
	}

	public static double[] Ends(IReadOnlyList<double> delays)
	{
		double[] ends = new double[delays.Count];
		double sum = 0.0;
		for (int i = 0; i < ends.Length; i++)
		{
			sum += delays[i];
			ends[i] = sum;
		}
		return ends;
	}
}

// Копит декодированные кадры: склеивает одинаковые соседние, следит за бюджетом памяти
// и при переполнении выкидывает каждый второй кадр (его задержка уходит предыдущему).
public sealed class GifFrameAccumulator<T>
{
	private readonly long _budget;

	private readonly List<T> _frames = new List<T>();

	private readonly List<double> _delays = new List<double>();

	private readonly List<ulong> _hashes = new List<ulong>();

	private readonly List<long> _sizes = new List<long>();

	public int Step { get; private set; } = 1;

	public long Bytes { get; private set; }

	public int Merged { get; private set; }

	public int Decimations { get; private set; }

	public int Count => _frames.Count;

	public IReadOnlyList<T> Frames => _frames;

	public IReadOnlyList<double> Delays => _delays;

	public GifFrameAccumulator(long budget = GifFramePlan.DefaultBudget)
	{
		_budget = budget;
	}

	// Нужен ли кадр исходника с этим номером (после прореживания берём каждый Step-й).
	public bool Wants(int sourceIndex)
	{
		return _frames.Count == 0 || sourceIndex % Step == 0;
	}

	// Пропущенный кадр: его время показывает предыдущий.
	public void Extend(double delay)
	{
		if (_delays.Count > 0)
		{
			_delays[^1] += delay;
		}
	}

	public bool TryMerge(ulong hash, double delay)
	{
		if (_hashes.Count == 0 || _hashes[^1] != hash)
		{
			return false;
		}
		_delays[^1] += delay;
		Merged++;
		return true;
	}

	public void Add(T frame, ulong hash, double delay, long bytes)
	{
		_frames.Add(frame);
		_hashes.Add(hash);
		_delays.Add(delay);
		_sizes.Add(bytes);
		Bytes += bytes;
		while (_budget > 0 && Bytes > _budget && _frames.Count > 1)
		{
			Decimate();
		}
	}

	private void Decimate()
	{
		int kept = 0;
		long bytes = 0L;
		for (int i = 0; i < _frames.Count; i += 2)
		{
			double delay = _delays[i] + ((i + 1 < _frames.Count) ? _delays[i + 1] : 0.0);
			_frames[kept] = _frames[i];
			_hashes[kept] = _hashes[i];
			_sizes[kept] = _sizes[i];
			_delays[kept] = delay;
			bytes += _sizes[i];
			kept++;
		}
		int extra = _frames.Count - kept;
		_frames.RemoveRange(kept, extra);
		_hashes.RemoveRange(kept, extra);
		_sizes.RemoveRange(kept, extra);
		_delays.RemoveRange(kept, extra);
		Bytes = bytes;
		Step *= 2;
		Decimations++;
	}
}
