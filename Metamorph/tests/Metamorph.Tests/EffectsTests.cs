using System;
using System.Collections.Generic;
using Metamorph.Core.Colors;
using Metamorph.Core.Effects;
using Metamorph.Core.Models;
using Xunit;

namespace Metamorph.Tests;

public class EffectsParticleTests
{
	public static IEnumerable<object[]> Kinds()
	{
		foreach (ParticleKind k in Enum.GetValues<ParticleKind>())
		{
			yield return new object[] { k };
		}
	}

	[Fact]
	public void CountScalesWithAreaAndDensityAndIsCapped()
	{
		int hd = ParticleField.CountFor(ParticleKind.Snow, 1920, 1080, 1.0);
		int hd2 = ParticleField.CountFor(ParticleKind.Snow, 1920, 1080, 2.0);
		int uhd = ParticleField.CountFor(ParticleKind.Snow, 3840, 2160, 1.0);
		Assert.InRange(hd, 150, 220);
		Assert.True(hd2 > hd);
		Assert.True(uhd > hd);
		Assert.Equal(ParticleField.MaxParticles, ParticleField.CountFor(ParticleKind.Rain, 7680, 4320, 3.0));
		Assert.Equal(0, ParticleField.CountFor(ParticleKind.Snow, 0, 1080, 1.0));
		Assert.True(ParticleField.CountFor(ParticleKind.Snow, 1920, 1080, 0.0) >= 1);
	}

	[Theory]
	[MemberData(nameof(Kinds))]
	public void SameSeedIsDeterministic(ParticleKind kind)
	{
		ParticleField a = new ParticleField(42);
		ParticleField b = new ParticleField(42);
		a.Configure(kind, 1920, 1080, 200, 1.0);
		b.Configure(kind, 1920, 1080, 200, 1.0);
		for (int i = 0; i < 300; i++)
		{
			a.Step(1f / 30f, 500, 500, repel: true);
			b.Step(1f / 30f, 500, 500, repel: true);
		}
		for (int i = 0; i < 200; i++)
		{
			Assert.Equal(a.Items[i].X, b.Items[i].X);
			Assert.Equal(a.Items[i].Y, b.Items[i].Y);
			Assert.Equal(a.Items[i].Alpha, b.Items[i].Alpha);
		}
	}

	[Theory]
	[MemberData(nameof(Kinds))]
	public void ParticlesStayNearScreenAndFinite(ParticleKind kind)
	{
		ParticleField f = new ParticleField(7);
		f.Configure(kind, 1280, 720, 150, 2.0);
		for (int i = 0; i < 3000; i++)
		{
			f.Step(1f / 30f);
		}
		for (int i = 0; i < f.Count; i++)
		{
			Particle p = f.Items[i];
			Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y));
			Assert.InRange(p.X, -300f, 1280f + 300f);
			Assert.InRange(p.Y, -300f, 720f + 300f);
			Assert.InRange(p.Alpha, 0f, 1f);
			Assert.True(p.Variant < ParticleField.VariantCount(kind));
		}
	}

	[Theory]
	[MemberData(nameof(Kinds))]
	public void StepDoesNotAllocate(ParticleKind kind)
	{
		ParticleField f = new ParticleField(3);
		f.Configure(kind, 1920, 1080, ParticleField.MaxParticles, 1.0);
		f.Step(0.016f, 100, 100, repel: true);
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 2000; i++)
		{
			f.Step(0.016f, 100 + i % 50, 300, repel: true);
		}
		Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
	}

	[Fact]
	public void SnowFallsAndBubblesRise()
	{
		ParticleField snow = new ParticleField(1);
		snow.Configure(ParticleKind.Snow, 1000, 2000, 1, 1.0);
		float y0 = snow.Items[0].Y;
		snow.Step(0.05f);
		Assert.True(snow.Items[0].Y > y0);
		ParticleField bubbles = new ParticleField(1);
		bubbles.Configure(ParticleKind.Bubbles, 1000, 2000, 1, 1.0);
		float b0 = bubbles.Items[0].Y;
		bubbles.Step(0.05f);
		Assert.True(bubbles.Items[0].Y < b0);
	}

	[Fact]
	public void CursorRepelsParticles()
	{
		ParticleField f = new ParticleField(5);
		f.Configure(ParticleKind.Bubbles, 1000, 1000, 1, 1.0);
		ref Particle p = ref f.Items[0];
		float cx = p.X + 20f;
		float cy = p.Y;
		float before = p.X;
		ParticleField g = new ParticleField(5);
		g.Configure(ParticleKind.Bubbles, 1000, 1000, 1, 1.0);
		f.Step(0.05f, cx, cy, repel: true);
		g.Step(0.05f);
		// С курсором справа частица смещается левее, чем без него.
		Assert.True(f.Items[0].X < g.Items[0].X);
		Assert.True(f.Items[0].X < before + 5f);
	}

	[Fact]
	public void ShootingStarEventuallyAppears()
	{
		ParticleField f = new ParticleField(11);
		f.Configure(ParticleKind.Stars, 1920, 1080, 100, 1.0);
		bool seen = false;
		for (int i = 0; i < 30 * 40 && !seen; i++)
		{
			f.Step(1f / 30f);
			seen = f.Shooting.Active;
		}
		Assert.True(seen);
		Assert.InRange(f.Shooting.Alpha, 0f, 1f);
	}
}

public class EffectsSpringTests
{
	[Fact]
	public void SpringConvergesWithoutOvershoot()
	{
		Spring s = default;
		double max = 0.0;
		for (int i = 0; i < 120; i++)
		{
			s.Step(10.0, Parallax.Omega, 1.0 / 60.0);
			max = Math.Max(max, s.Value);
		}
		Assert.True(max <= 10.0 + 1e-9);
		Assert.True(s.IsSettled(10.0));
	}

	[Fact]
	public void SpringIsStableWithLargeDt()
	{
		Spring s = new Spring { Value = -50.0, Velocity = 300.0 };
		for (int i = 0; i < 10; i++)
		{
			s.Step(0.0, Parallax.Omega, 1.5);
			Assert.True(double.IsFinite(s.Value));
		}
		Assert.True(s.IsSettled(0.0));
	}

	[Fact]
	public void ParallaxMovesOppositeToCursor()
	{
		(double x, double y) = Parallax.Target(1920, 0, 1920, 1080, 18);
		Assert.Equal(-18.0, x, 6);
		Assert.Equal(18.0, y, 6);
		(double cx, double cy) = Parallax.Target(960, 540, 1920, 1080, 18);
		Assert.Equal(0.0, cx, 6);
		Assert.Equal(0.0, cy, 6);
		Assert.Equal((0.0, 0.0), Parallax.Target(double.NaN, 1, 1920, 1080, 18));
	}

	[Fact]
	public void OverscanHidesEdgesOnBothAxes()
	{
		double w = 1920;
		double h = 1080;
		double s = 18;
		double scale = Parallax.OverscanScale(s, w, h);
		Assert.True((w * scale - w) / 2.0 >= s - 1e-9);
		Assert.True((h * scale - h) / 2.0 >= s - 1e-9);
		Assert.Equal(1.0, Parallax.OverscanScale(0, w, h));
	}
}

public class EffectsTimeTintTests
{
	private static DateTime At(double hours)
	{
		return new DateTime(2026, 6, 1).AddHours(hours);
	}

	[Fact]
	public void DayIsNeutralNightIsCoolAndDarker()
	{
		Tint noon = TimeTint.For(At(13));
		Tint night = TimeTint.For(At(2));
		Assert.Equal(0.0, noon.Alpha, 6);
		Assert.True(night.Alpha > 0.3);
		Assert.True(night.Color.B > night.Color.R);
	}

	[Fact]
	public void MorningAndEveningAreWarm()
	{
		Tint dawn = TimeTint.For(At(7));
		Tint golden = TimeTint.For(At(19));
		Assert.True(dawn.Color.R > dawn.Color.B && dawn.Alpha > 0.1);
		Assert.True(golden.Color.R > golden.Color.B && golden.Alpha > 0.1);
	}

	[Fact]
	public void InterpolationIsSmooth()
	{
		Tint prev = TimeTint.For(At(0));
		for (int m = 1; m < 24 * 60; m++)
		{
			Tint cur = TimeTint.For(At(m / 60.0));
			Assert.True(Math.Abs(cur.Alpha - prev.Alpha) < 0.02, $"скачок альфы в {m / 60}:{m % 60:00}");
			// Видимый вклад — цвет × альфа (цвет при нулевой альфе не важен).
			Assert.True(Math.Abs(cur.Color.R * cur.Alpha - prev.Color.R * prev.Alpha) <= 4 && Math.Abs(cur.Color.B * cur.Alpha - prev.Color.B * prev.Alpha) <= 4, $"скачок цвета в {m / 60}:{m % 60:00}");
			prev = cur;
		}
	}

	[Fact]
	public void CustomSunTimesShiftTheCurve()
	{
		Tint early = TimeTint.For(At(5), TimeSpan.FromHours(5), TimeSpan.FromHours(21));
		Assert.Equal(TimeTint.Dawn.Alpha, early.Alpha, 6);
		Tint invalid = TimeTint.For(At(13), TimeSpan.FromHours(20), TimeSpan.FromHours(6));
		Assert.Equal(0.0, invalid.Alpha, 6);
	}
}

public class EffectsVisualizerTests
{
	[Fact]
	public void ResampleAveragesAndInterpolates()
	{
		float[] src = { 0f, 1f, 0f, 1f };
		float[] two = new float[2];
		VisualizerBands.Resample(src, two);
		Assert.Equal(0.5f, two[0], 5);
		Assert.Equal(0.5f, two[1], 5);
		float[] seven = new float[7];
		VisualizerBands.Resample(new float[] { 0f, 1f }, seven);
		Assert.Equal(0f, seven[0], 5);
		Assert.Equal(0.5f, seven[3], 5);
		Assert.Equal(1f, seven[6], 5);
	}

	[Fact]
	public void PeaksHoldThenFallSlowly()
	{
		VisualizerBands v = new VisualizerBands(4);
		float[] loud = { 1f, 1f, 1f, 1f };
		float[] quiet = new float[4];
		for (int i = 0; i < 30; i++)
		{
			v.Update(loud, 1.0 / 30.0);
		}
		float peak = v.Peaks[0];
		Assert.True(peak > 0.9f);
		v.Update(quiet, 0.2);
		Assert.Equal(peak, v.Peaks[0], 5);
		for (int i = 0; i < 10; i++)
		{
			v.Update(quiet, 1.0 / 30.0);
		}
		Assert.True(v.Peaks[0] < peak);
		Assert.True(v.Peaks[0] >= v.Values[0]);
		for (int i = 0; i < 120; i++)
		{
			v.Update(quiet, 1.0 / 30.0);
		}
		Assert.True(v.Peaks[0] < 0.05f);
	}

	[Fact]
	public void UpdateDoesNotAllocate()
	{
		VisualizerBands v = new VisualizerBands(64);
		float[] raw = new float[48];
		v.Update(raw, 0.03);
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			raw[i % 48] = i % 7 / 7f;
			v.Update(raw, 0.03);
		}
		Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
	}

	[Fact]
	public void SilenceGateHidesAfterTwoSecondsAndReturns()
	{
		SilenceGate g = new SilenceGate();
		g.Update(0.5f, 0.5);
		g.Update(0.5f, 0.5);
		Assert.True(g.Visible && g.Fade > 0.99);
		for (int i = 0; i < 7; i++)
		{
			g.Update(0f, 0.25);
		}
		Assert.True(g.Sounding);
		for (int i = 0; i < 4; i++)
		{
			g.Update(0f, 0.25);
		}
		Assert.False(g.Sounding);
		g.Update(0f, 0.5);
		Assert.False(g.Visible);
		g.Update(0.3f, 0.1);
		Assert.True(g.Sounding && g.Visible);
	}
}

public class EffectsAccentTests
{
	[Fact]
	public void PicksVividAccentFromPixels()
	{
		List<Rgba> pixels = new List<Rgba>();
		for (int i = 0; i < 900; i++)
		{
			pixels.Add(new Rgba(20, 20, 30));
		}
		for (int i = 0; i < 100; i++)
		{
			pixels.Add(new Rgba(230, 40, 60));
		}
		Rgba? accent = WallpaperAccentPicker.Pick(pixels);
		Assert.NotNull(accent);
		Assert.True(accent.Value.R > accent.Value.G && accent.Value.R > accent.Value.B);
		Assert.Null(WallpaperAccentPicker.Pick(new List<Rgba>()));
		Rgba second = WallpaperAccentPicker.Secondary(accent.Value);
		Assert.NotEqual(accent.Value, second);
	}
}
