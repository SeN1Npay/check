using System.Linq;
using Metamorph.Core.Desktop;
using Xunit;

namespace Metamorph.Tests;

public class PerfGifFramePlanTests
{
	[Fact]
	public void ScaleNeverUpscales()
	{
		Assert.Equal(1.0, GifFramePlan.Scale(800, 600, 1920, 1080, 10));
	}

	[Fact]
	public void ScaleCoversMonitor()
	{
		double s = GifFramePlan.Scale(3840, 2160, 1920, 1080, 10);
		Assert.Equal(0.5, s, 3);
		// Не вписываем, а покрываем: обе стороны не меньше монитора.
		double s2 = GifFramePlan.Scale(4000, 1000, 1920, 1080, 1);
		(int w, int h) = GifFramePlan.ScaledSize(4000, 1000, s2);
		Assert.True(w >= 1920 && h >= 1080 || s2 == 1.0);
	}

	[Fact]
	public void UnknownTargetKeepsSize()
	{
		Assert.Equal(1.0, GifFramePlan.Scale(1000, 1000, 0, 0, 1));
	}

	[Fact]
	public void BudgetShrinksResolutionOnlyMildly()
	{
		double s = GifFramePlan.Scale(1920, 1080, 1920, 1080, 1000, 100L * 1024 * 1024);
		Assert.Equal(GifFramePlan.MinBudgetScale, s, 3);
	}

	[Fact]
	public void EstimateIsBgra()
	{
		Assert.Equal(1920L * 1080 * 4 * 10, GifFramePlan.EstimateBytes(1920, 1080, 10));
	}

	[Fact]
	public void HashDistinguishesFrames()
	{
		int[] a = Enumerable.Range(0, 101).ToArray();
		int[] b = (int[])a.Clone();
		Assert.Equal(GifFramePlan.Hash(a), GifFramePlan.Hash(b));
		b[100] = -1;
		Assert.NotEqual(GifFramePlan.Hash(a), GifFramePlan.Hash(b));
		b[100] = 100;
		b[3] = 7;
		Assert.NotEqual(GifFramePlan.Hash(a), GifFramePlan.Hash(b));
	}

	[Fact]
	public void IdenticalConsecutiveFramesMergeDelays()
	{
		GifFrameAccumulator<string> acc = new GifFrameAccumulator<string>(1000);
		acc.Add("a", 1, 0.1, 10);
		Assert.True(acc.TryMerge(1, 0.2));
		Assert.False(acc.TryMerge(2, 0.1));
		acc.Add("b", 2, 0.1, 10);
		Assert.Equal(new[] { "a", "b" }, acc.Frames);
		Assert.Equal(0.3, acc.Delays[0], 6);
		Assert.Equal(1, acc.Merged);
		Assert.Equal(new[] { 0.3, 0.4 }, GifFramePlan.Ends(acc.Delays).Select(d => System.Math.Round(d, 6)));
	}

	[Fact]
	public void OverBudgetDropsEveryOtherFrameKeepingDuration()
	{
		GifFrameAccumulator<int> acc = new GifFrameAccumulator<int>(45);
		for (int i = 0; i < 8; i++)
		{
			if (acc.Wants(i))
			{
				acc.Add(i, (ulong)i, 0.1, 10);
			}
			else
			{
				acc.Extend(0.1);
			}
		}
		Assert.True(acc.Bytes <= 45);
		Assert.True(acc.Decimations >= 1);
		Assert.Equal(new[] { 0, 2, 4, 6 }, acc.Frames);
		Assert.All(acc.Delays, d => Assert.Equal(0.2, d, 6));
		Assert.Equal(0.8, acc.Delays.Sum(), 6);
		Assert.Equal(2, acc.Step);
	}

	[Fact]
	public void SingleHugeFrameIsKept()
	{
		GifFrameAccumulator<int> acc = new GifFrameAccumulator<int>(5);
		acc.Add(1, 1, 0.1, 100);
		Assert.Equal(1, acc.Count);
	}
}
