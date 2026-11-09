using System;
using System.Linq;
using Metamorph.Core.Audio;
using Xunit;

namespace Metamorph.Tests;

public class SpectrumAutoGainTests
{
	private static float[] Noise(int n, double amplitude, int seed)
	{
		Random r = new Random(seed);
		float[] x = new float[n];
		for (int i = 0; i < n; i++)
		{
			// Розовый-ish шум: сумма белого и низкочастотного синуса — как «громкий мастер» со всеми полосами.
			x[i] = (float)(amplitude * ((r.NextDouble() * 2.0 - 1.0) * 0.7 + 0.3 * Math.Sin(i * 0.02)));
		}
		return x;
	}

	[Fact]
	public void LoudMasterDoesNotSaturateEveryBand()
	{
		SpectrumAnalyzer a = new SpectrumAnalyzer();
		float[] bands = new float[48];
		for (int frame = 0; frame < 60; frame++)
		{
			a.Push(Noise(2048, 0.9, frame));
			a.Compute(bands);
		}
		int maxed = bands.Count(b => b >= 0.99f);
		Assert.True(maxed < 24, $"{maxed} из 48 полос в максимуме");
		Assert.True(bands.Max() > 0.6f);
	}

	[Fact]
	public void WithoutAutoGainLoudMasterSaturates()
	{
		// Документирует исходную проблему «сплошного блока» в cava.
		SpectrumAnalyzer a = new SpectrumAnalyzer { AutoGain = false };
		float[] bands = new float[48];
		a.Push(Noise(2048, 0.9, 1));
		a.Compute(bands);
		Assert.True(bands.Count(b => b >= 0.99f) >= 24);
	}

	[Fact]
	public void SilenceStaysAtZero()
	{
		SpectrumAnalyzer a = new SpectrumAnalyzer();
		float[] bands = new float[48];
		for (int frame = 0; frame < 30; frame++)
		{
			a.Push(Noise(2048, 1e-5, frame));
			a.Compute(bands);
		}
		Assert.True(bands.Max() < 0.05f);
	}

	[Fact]
	public void GainRisesFastAndFallsSlowly()
	{
		SpectrumAnalyzer a = new SpectrumAnalyzer();
		a.StepGain(-40.0, 0.0);
		(_, double up) = a.StepGain(0.0, 0.2);
		Assert.True(up > -5.0, $"потолок {up}");
		(_, double down) = a.StepGain(-40.0, 1.0);
		Assert.InRange(down, up - 6.5, up - 5.5);
	}
}
