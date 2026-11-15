using System.Drawing;
using System.Linq;
using Metamorph.Core.Desktop;
using Xunit;

namespace Metamorph.Tests;

public class HotCornersTrackerTests
{
	private static readonly HotCorner[] All = { HotCorner.TopLeft, HotCorner.TopRight, HotCorner.BottomLeft, HotCorner.BottomRight };

	private static HotCornerTracker Single(int delay = 250)
	{
		HotCornerTracker t = new HotCornerTracker(delay, All);
		t.SetMonitors(new[] { new Rectangle(0, 0, 1920, 1080) });
		return t;
	}

	[Fact]
	public void SingleMonitorHasFourCorners()
	{
		HotCornerTracker t = Single();
		Assert.Equal(4, t.Points.Count);
		Assert.Contains(t.Points, p => p.Corner == HotCorner.BottomRight && p.X == 1919 && p.Y == 1079);
	}

	[Fact]
	public void CornersBetweenMonitorsAreIgnored()
	{
		HotCornerTracker t = new HotCornerTracker(0, All);
		t.SetMonitors(new[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(1920, 0, 1920, 1080) });
		Assert.Equal(4, t.Points.Count);
		Assert.DoesNotContain(t.Points, p => p.X == 1919 || p.X == 1920);
	}

	[Fact]
	public void InactiveCornersAreSkipped()
	{
		HotCornerTracker t = new HotCornerTracker(0, new[] { HotCorner.TopLeft });
		t.SetMonitors(new[] { new Rectangle(0, 0, 1920, 1080) });
		Assert.Single(t.Points);
		Assert.Null(t.Update(1919, 0, 0));
	}

	[Fact]
	public void FiresAfterDwell()
	{
		HotCornerTracker t = Single(250);
		Assert.Null(t.Update(0, 0, 1000));
		Assert.Null(t.Update(1, 1, 1100));
		HotCornerPoint? hit = t.Update(0, 0, 1250);
		Assert.Equal(HotCorner.TopLeft, hit?.Corner);
	}

	[Fact]
	public void LeavingResetsDwell()
	{
		HotCornerTracker t = Single(250);
		t.Update(0, 0, 0);
		t.Update(500, 500, 100);
		Assert.Null(t.Update(0, 0, 260));
		Assert.NotNull(t.Update(0, 0, 510));
	}

	[Fact]
	public void RearmsOnlyAfterLeavingFar()
	{
		HotCornerTracker t = Single(0);
		Assert.NotNull(t.Update(0, 0, 0));
		Assert.Null(t.Update(0, 0, 100));
		Assert.Null(t.Update(20, 20, 200));
		Assert.Null(t.Update(0, 0, 300));
		Assert.Null(t.Update(100, 100, 400));
		Assert.NotNull(t.Update(0, 0, 500));
	}

	[Fact]
	public void BlockedSuppressesAndIsLazy()
	{
		HotCornerTracker t = Single(0);
		int calls = 0;
		Assert.Null(t.Update(500, 500, 0, () => { calls++; return true; }));
		Assert.Equal(0, calls);
		Assert.Null(t.Update(0, 0, 0, () => { calls++; return true; }));
		Assert.Equal(1, calls);
		Assert.NotNull(t.Update(0, 0, 10, () => false));
	}
}
