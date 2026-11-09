using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Games;
using Xunit;

namespace Metamorph.Tests;

public class GamesDinoTests
{
	private const double Dt = DinoEngine.Step;

	private static DinoEngine Quiet()
	{
		return new DinoEngine(7) { AutoSpawn = false };
	}

	[Fact]
	public void HeldJumpReachesFullHeightAndLands()
	{
		DinoEngine e = Quiet();
		List<DinoEvent> events = new List<DinoEvent>();
		e.Event += events.Add;
		e.SetJump(true);
		double peak = 0;
		for (int i = 0; i < 240; i++)
		{
			e.Update(Dt);
			peak = Math.Max(peak, e.PlayerY);
		}
		double expected = DinoEngine.JumpVelocity * DinoEngine.JumpVelocity / (2 * DinoEngine.Gravity);
		Assert.InRange(peak, expected * 0.95, expected * 1.05);
		Assert.True(e.OnGround);
		Assert.Single(events, ev => ev.Kind == DinoEventKind.Land);
		// Удержание после приземления — снова прыгать не должна (нужно новое нажатие).
		Assert.Single(events, ev => ev.Kind == DinoEventKind.Jump);
	}

	[Fact]
	public void TapJumpIsLowerThanHeldJump()
	{
		DinoEngine e = Quiet();
		e.SetJump(true);
		e.Update(Dt);
		e.SetJump(false);
		double peak = 0;
		for (int i = 0; i < 240; i++)
		{
			e.Update(Dt);
			peak = Math.Max(peak, e.PlayerY);
		}
		double full = DinoEngine.JumpVelocity * DinoEngine.JumpVelocity / (2 * DinoEngine.Gravity);
		Assert.True(peak < full * 0.6, $"tap peak {peak}");
		Assert.True(peak > 30);
	}

	[Fact]
	public void JumpBufferedJustBeforeLandingFires()
	{
		DinoEngine e = Quiet();
		int jumps = 0;
		e.Event += ev => jumps += ev.Kind == DinoEventKind.Jump ? 1 : 0;
		e.SetJump(true);
		e.Update(Dt);
		e.SetJump(false);
		while (!(e.PlayerVy < 0 && e.PlayerY < 15))
		{
			e.Update(Dt);
		}
		e.SetJump(true);
		for (int i = 0; i < 24; i++)
		{
			e.Update(Dt);
		}
		Assert.Equal(2, jumps);
	}

	[Fact]
	public void EarlyPressIsNotBuffered()
	{
		DinoEngine e = Quiet();
		int jumps = 0;
		e.Event += ev => jumps += ev.Kind == DinoEventKind.Jump ? 1 : 0;
		e.SetJump(true);
		e.Update(Dt);
		e.SetJump(false);
		while (e.PlayerVy > 0)
		{
			e.Update(Dt);
		}
		e.SetJump(true);
		e.Update(Dt);
		e.SetJump(false);
		for (int i = 0; i < 120; i++)
		{
			e.Update(Dt);
		}
		Assert.Equal(1, jumps);
		Assert.True(e.OnGround);
	}

	[Fact]
	public void RunningIntoCrystalEndsGame()
	{
		DinoEngine e = Quiet();
		bool over = false;
		e.Event += ev => over |= ev.Kind == DinoEventKind.GameOver;
		e.Spawn(DinoObstacleKind.CrystalLarge, 200);
		for (int i = 0; i < 120; i++)
		{
			e.Update(Dt);
		}
		Assert.True(e.IsGameOver);
		Assert.True(over);
	}

	[Fact]
	public void TimedJumpClearsCrystal()
	{
		DinoEngine e = Quiet();
		e.Spawn(DinoObstacleKind.CrystalSmall, 300);
		for (int i = 0; i < 150; i++)
		{
			e.SetJump(i >= 54 && i < 110);
			e.Update(Dt);
		}
		Assert.False(e.IsGameOver);
		Assert.True(e.Obstacles[0].Passed);
	}

	[Fact]
	public void DuckingPassesUnderMidFlyer()
	{
		DinoEngine ducking = Quiet();
		ducking.Spawn(DinoObstacleKind.Bat, 200, 50);
		ducking.SetDuck(true);
		DinoEngine standing = Quiet();
		standing.Spawn(DinoObstacleKind.Bat, 200, 50);
		for (int i = 0; i < 180; i++)
		{
			ducking.Update(Dt);
			standing.Update(Dt);
		}
		Assert.False(ducking.IsGameOver);
		Assert.True(standing.IsGameOver);
	}

	[Fact]
	public void SameSeedAndInputsGiveSameRun()
	{
		DinoEngine a = new DinoEngine(42);
		DinoEngine b = new DinoEngine(42);
		for (int i = 0; i < 120 * 40; i++)
		{
			bool jump = i % 90 < 30;
			a.SetJump(jump);
			b.SetJump(jump);
			a.Update(Dt);
			b.Update(Dt);
		}
		Assert.Equal(a.IsGameOver, b.IsGameOver);
		Assert.Equal(a.Score, b.Score);
		Assert.Equal(a.Distance, b.Distance);
		Assert.Equal(a.Obstacles.ToArray().Select(o => (o.Kind, o.X, o.Y, o.Count)), b.Obstacles.ToArray().Select(o => (o.Kind, o.X, o.Y, o.Count)));
	}

	[Fact]
	public void SpeedRampsAndMilestonesFire()
	{
		DinoEngine e = Quiet();
		List<int> milestones = new List<int>();
		e.Event += ev =>
		{
			if (ev.Kind == DinoEventKind.Milestone)
			{
				milestones.Add(ev.Value);
			}
		};
		double start = e.Speed;
		e.Update(0.2);
		for (int i = 0; i < 120 * 25; i++)
		{
			e.Update(Dt);
		}
		Assert.True(e.Speed > start);
		Assert.True(e.Speed <= DinoEngine.MaxSpeed);
		Assert.True(e.Score >= 200);
		Assert.Equal(new[] { 100, 200 }, milestones.Take(2));
	}
}
