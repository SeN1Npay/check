using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Games;
using Xunit;

namespace Metamorph.Tests;

public class GamesPlatformerTests
{
	private const double Dt = PlatformerEngine.Step;

	private const PlatformerInput None = PlatformerInput.None;

	private const PlatformerInput Right = PlatformerInput.Right;

	private const PlatformerInput Jump = PlatformerInput.Jump;

	private static PlatformerLevel Lvl(params string[] rows)
	{
		return PlatformerLevel.Parse("t", PlatformerWorld.Meadow, "Тест", "Test", rows);
	}

	// Ровная земля во всю ширину, Мета в колонке startCol, финиш в правом верхнем углу (не мешает).
	private static string[] Flat(int width, int height, int startCol, params (int Col, char Ch)[] onGround)
	{
		string[] rows = new string[height];
		for (int r = 0; r < height; r++)
		{
			char[] line = new string(r >= height - 2 ? '#' : '.', width).ToCharArray();
			if (r == 0)
			{
				line[width - 1] = 'E';
			}
			if (r == height - 3)
			{
				line[startCol] = 'S';
				foreach ((int col, char ch) in onGround)
				{
					line[col] = ch;
				}
			}
			rows[r] = new string(line);
		}
		return rows;
	}

	private static List<PlatformerEvent> Record(PlatformerEngine e)
	{
		List<PlatformerEvent> list = new List<PlatformerEvent>();
		e.Event += list.Add;
		return list;
	}

	private static void Run(PlatformerEngine e, PlatformerInput input, double seconds)
	{
		e.SetInput(input);
		int n = (int)Math.Round(seconds / Dt);
		for (int i = 0; i < n; i++)
		{
			e.Update(Dt);
		}
	}

	private static double Peak(PlatformerEngine e, Action<int> perTick)
	{
		double ground = e.MetaBottom;
		double peak = 0.0;
		for (int i = 0; i < 240; i++)
		{
			perTick(i);
			e.Update(Dt);
			peak = Math.Max(peak, ground - e.MetaBottom);
		}
		return peak;
	}

	[Fact]
	public void HeldJumpHeightIsAboutFourTilesAndTapIsLower()
	{
		PlatformerEngine held = new PlatformerEngine(Lvl(Flat(40, 15, 5)));
		double full = Peak(held, _ => held.SetInput(Jump));
		Assert.InRange(full, 60.0, 90.0);
		Assert.True(held.OnGround);
		PlatformerEngine tap = new PlatformerEngine(Lvl(Flat(40, 15, 5)));
		double low = Peak(tap, i => tap.SetInput(i == 0 ? Jump : None));
		Assert.InRange(low, 12.0, 40.0);
		Assert.True(low < full * 0.6);
	}

	[Fact]
	public void RunningJumpGoesHigherAndFarther()
	{
		PlatformerEngine walk = new PlatformerEngine(Lvl(Flat(80, 15, 3)));
		PlatformerEngine run = new PlatformerEngine(Lvl(Flat(80, 15, 3)));
		Run(walk, Right, 1.0);
		Run(run, Right | PlatformerInput.Run, 1.0);
		Assert.Equal(PlatformerEngine.WalkSpeed, walk.MetaVx, 3);
		Assert.Equal(PlatformerEngine.RunSpeed, run.MetaVx, 3);
		double x0 = walk.MetaX;
		double walkPeak = Peak(walk, _ => walk.SetInput(Right | Jump));
		double r0 = run.MetaX;
		double runPeak = Peak(run, _ => run.SetInput(Right | Jump | PlatformerInput.Run));
		Assert.True(runPeak > walkPeak);
		Assert.True(run.MetaX - r0 > walk.MetaX - x0);
	}

	private static PlatformerEngine Ledge()
	{
		string[] rows = new string[10];
		for (int r = 0; r < 10; r++)
		{
			rows[r] = r >= 8 ? "##########" + new string('.', 30) : new string('.', 40);
		}
		rows[7] = ".......S" + new string('.', 32);
		rows[0] = new string('.', 39) + "E";
		return new PlatformerEngine(Lvl(rows));
	}

	private static int WalkOffLedge(PlatformerEngine e)
	{
		e.SetInput(Right);
		int ticks = 0;
		while (e.OnGround && ticks < 600)
		{
			e.Update(Dt);
			ticks++;
		}
		Assert.False(e.OnGround);
		return ticks;
	}

	[Fact]
	public void CoyoteTimeAllowsLateJump()
	{
		PlatformerEngine e = Ledge();
		List<PlatformerEvent> ev = Record(e);
		WalkOffLedge(e);
		Run(e, Right, 0.05);
		Run(e, Right | Jump, 0.05);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Jump);
		Assert.True(e.MetaVy < 0.0);
	}

	[Fact]
	public void NoJumpAfterCoyoteTimeExpires()
	{
		PlatformerEngine e = Ledge();
		List<PlatformerEvent> ev = Record(e);
		WalkOffLedge(e);
		Run(e, Right, 0.2);
		Run(e, Right | Jump, 0.1);
		Assert.DoesNotContain(ev, x => x.Kind == PlatformerEventKind.Jump);
	}

	[Fact]
	public void JumpPressedJustBeforeLandingIsBuffered()
	{
		PlatformerEngine e = new PlatformerEngine(Lvl(Flat(40, 15, 5)));
		List<PlatformerEvent> ev = Record(e);
		double ground = e.MetaBottom;
		Run(e, Jump, 0.05);
		e.SetInput(None);
		int guard = 0;
		while (!(e.MetaVy > 0.0 && ground - e.MetaBottom < 6.0) && guard++ < 600)
		{
			e.Update(Dt);
		}
		Assert.False(e.OnGround);
		Run(e, Jump, 0.15);
		Assert.Equal(2, ev.Count(x => x.Kind == PlatformerEventKind.Jump));
	}

	[Fact]
	public void EarlyPressIsNotBuffered()
	{
		PlatformerEngine e = new PlatformerEngine(Lvl(Flat(40, 15, 5)));
		List<PlatformerEvent> ev = Record(e);
		Run(e, Jump, 0.05);
		Run(e, None, 0.1);
		Run(e, Jump, Dt);
		Run(e, None, 1.5);
		Assert.Equal(1, ev.Count(x => x.Kind == PlatformerEventKind.Jump));
		Assert.True(e.OnGround);
	}

	[Fact]
	public void NoTunnelingThroughWallAtRunSpeed()
	{
		string[] rows = Flat(40, 15, 3);
		for (int r = 0; r < 13; r++)
		{
			char[] line = rows[r].ToCharArray();
			line[20] = 'X';
			rows[r] = new string(line);
		}
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		e.SetInput(Right | PlatformerInput.Run);
		for (int i = 0; i < 40; i++)
		{
			e.Update(0.25);
			Assert.True(e.MetaLeft + e.MetaWidth <= 20 * 16 + 1e-6, $"x={e.MetaLeft}");
		}
		Assert.Equal(20 * 16, e.MetaLeft + e.MetaWidth, 6);
	}

	[Theory]
	[InlineData('#')]
	[InlineData('=')]
	public void NoTunnelingThroughThinFloorAtTerminalVelocity(char floor)
	{
		string[] rows = new string[40];
		for (int r = 0; r < 40; r++)
		{
			rows[r] = new string('.', 12);
		}
		rows[1] = "...S........";
		rows[0] = "...........E";
		rows[38] = new string(floor, 12);
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		e.Update(0.25);
		for (int i = 0; i < 40; i++)
		{
			e.Update(0.1);
			Assert.True(e.MetaBottom <= 38 * 16 + 1e-6);
		}
		Assert.True(e.OnGround);
		Assert.Equal(38 * 16, e.MetaBottom, 6);
		Assert.Equal(PlatformerPhase.Playing, e.Phase);
	}

	[Fact]
	public void StompingSlimeSquishesItAndBounces()
	{
		string[] rows = Flat(40, 10, 5);
		rows[7] = rows[7].Replace('S', '.');
		rows[3] = ".....S" + new string('.', 34);
		rows[7] = "......s" + new string('.', 33);
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		List<PlatformerEvent> ev = Record(e);
		Run(e, None, 0.6);
		PlatformerEvent stomp = Assert.Single(ev, x => x.Kind == PlatformerEventKind.Stomp);
		Assert.Equal(100, stomp.Value);
		Assert.Equal(PlatformerPhase.Playing, e.Phase);
		Assert.Equal(PlatformerEnemyState.Squished, e.Enemies[0].State);
		Assert.Equal(100, e.Score);
	}

	[Fact]
	public void WalkingIntoSlimeKillsSmallMeta()
	{
		PlatformerEngine e = new PlatformerEngine(Lvl(Flat(40, 10, 3, (9, 's'))));
		List<PlatformerEvent> ev = Record(e);
		Run(e, Right, 1.5);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Death);
		Assert.Equal(PlatformerPhase.Dying, e.Phase);
		Assert.Equal(PlatformerEngine.StartLives - 1, e.Lives);
		Assert.DoesNotContain(ev, x => x.Kind == PlatformerEventKind.Stomp);
	}

	[Fact]
	public void SpikyCannotBeStomped()
	{
		string[] rows = Flat(40, 10, 5);
		rows[7] = "......k" + new string('.', 33);
		rows[3] = ".....S" + new string('.', 34);
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		List<PlatformerEvent> ev = Record(e);
		Run(e, None, 0.6);
		Assert.DoesNotContain(ev, x => x.Kind == PlatformerEventKind.Stomp);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Death);
	}

	[Fact]
	public void StompedSnailBecomesShellAndKickedShellKnocksEnemies()
	{
		string[] rows = Flat(40, 10, 3);
		rows[7] = "....n.................s.................";
		rows[3] = "...S....................................";
		rows[8] = "##############################..........";
		rows[9] = rows[8];
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		List<PlatformerEvent> ev = Record(e);
		int guard = 0;
		while (!ev.Any(x => x.Kind == PlatformerEventKind.Stomp) && guard++ < 240)
		{
			Run(e, None, Dt);
		}
		Assert.Equal(PlatformerEnemyState.Shell, e.Enemies[0].State);
		Run(e, None, 0.5);
		guard = 0;
		while (!ev.Any(x => x.Kind == PlatformerEventKind.Kick) && guard++ < 480)
		{
			Run(e, Right, Dt);
		}
		Assert.Equal(PlatformerEnemyState.ShellMoving, e.Enemies[0].State);
		Assert.True(e.Enemies[0].Vx > 0.0);
		Run(e, None, 1.5);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Knock);
		Assert.True(e.Enemies[1].State is PlatformerEnemyState.Knocked or PlatformerEnemyState.Gone);
		Assert.Equal(PlatformerPhase.Playing, e.Phase);
	}

	private static PlatformerEngine BlockLevel(char block)
	{
		string[] rows = Flat(40, 10, 2);
		char[] line = rows[3].ToCharArray();
		line[4] = block;
		rows[3] = new string(line);
		return new PlatformerEngine(Lvl(rows));
	}

	private static void JumpUnderBlock(PlatformerEngine e)
	{
		while (e.MetaX < 4 * 16 + 8)
		{
			Run(e, Right, Dt);
		}
		Run(e, None, 0.3);
		Run(e, Jump, 0.5);
		Run(e, None, 0.4);
	}

	[Fact]
	public void SugarStarFromBlockMakesMetaBig()
	{
		PlatformerEngine e = BlockLevel('P');
		List<PlatformerEvent> ev = Record(e);
		JumpUnderBlock(e);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.PowerUpAppear);
		Assert.Equal(PlatformerTile.Used, e.TileAt(4, 3));
		Run(e, None, 1.5);
		Run(e, Right, 3.0);
		Assert.Equal(PlatformerPower.Big, e.Power);
		Assert.Equal(PlatformerEngine.BigH, e.MetaHeight);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.PowerUp);
	}

	[Fact]
	public void SmallMetaBumpsBrickBigMetaBreaksIt()
	{
		PlatformerEngine small = BlockLevel('B');
		List<PlatformerEvent> se = Record(small);
		JumpUnderBlock(small);
		Assert.Contains(se, x => x.Kind == PlatformerEventKind.BlockBump);
		Assert.Equal(PlatformerTile.Brick, small.TileAt(4, 3));
		PlatformerEngine big = BlockLevel('B');
		big.SetPower(PlatformerPower.Big);
		List<PlatformerEvent> be = Record(big);
		JumpUnderBlock(big);
		Assert.Contains(be, x => x.Kind == PlatformerEventKind.BlockBreak);
		Assert.Equal(PlatformerTile.Empty, big.TileAt(4, 3));
	}

	[Fact]
	public void QuestionBlockGivesCoin()
	{
		PlatformerEngine e = BlockLevel('?');
		List<PlatformerEvent> ev = Record(e);
		JumpUnderBlock(e);
		Assert.Single(ev, x => x.Kind == PlatformerEventKind.BlockCoin);
		Assert.Equal(1, e.Coins);
		Assert.Equal(PlatformerTile.Used, e.TileAt(4, 3));
	}

	[Fact]
	public void HurtWhileBigShrinksWithInvincibilityThenSmallHitKills()
	{
		PlatformerEngine e = new PlatformerEngine(Lvl(Flat(60, 10, 3, (9, 's'), (30, 's'))));
		e.SetPower(PlatformerPower.Big);
		List<PlatformerEvent> ev = Record(e);
		int guard = 0;
		while (!ev.Any(x => x.Kind == PlatformerEventKind.Hurt) && guard++ < 600)
		{
			Run(e, Right, Dt);
		}
		Assert.Equal(PlatformerPower.Small, e.Power);
		Assert.Equal(PlatformerEngine.SmallH, e.MetaHeight);
		Assert.True(e.Invincible > 1.5);
		Run(e, Right, 0.5);
		Assert.Equal(PlatformerPhase.Playing, e.Phase);
		Assert.DoesNotContain(ev, x => x.Kind == PlatformerEventKind.Death);
		Run(e, Right, 4.0);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Death);
		Assert.Equal(PlatformerPhase.Dying, e.Phase);
	}

	[Fact]
	public void FireCandyKnocksEnemy()
	{
		PlatformerEngine e = new PlatformerEngine(Lvl(Flat(40, 10, 3, (12, 'k'))));
		e.SetPower(PlatformerPower.Fire);
		List<PlatformerEvent> ev = Record(e);
		Run(e, PlatformerInput.Fire, Dt);
		Run(e, None, 1.0);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Throw);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.Knock);
		Assert.Equal(PlatformerEnemyState.Knocked, e.Enemies[0].State);
	}

	[Fact]
	public void LivesRunOutIntoGameOver()
	{
		string[] rows = Flat(20, 10, 3);
		rows[8] = "#####...............";
		rows[9] = rows[8];
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		List<PlatformerEvent> ev = Record(e);
		for (int i = 0; i < 10 && e.Phase != PlatformerPhase.GameOver; i++)
		{
			Run(e, Right, 4.0);
		}
		Assert.Equal(PlatformerPhase.GameOver, e.Phase);
		Assert.Equal(PlatformerEngine.StartLives, ev.Count(x => x.Kind == PlatformerEventKind.Death));
		Assert.Single(ev, x => x.Kind == PlatformerEventKind.GameOver);
	}

	[Fact]
	public void ReachingFlagClearsLevelWithBonus()
	{
		string[] rows = Flat(30, 15, 2);
		rows[0] = new string('.', 30);
		char[] line = rows[12].ToCharArray();
		line[12] = 'E';
		rows[12] = new string(line);
		PlatformerEngine e = new PlatformerEngine(Lvl(rows));
		List<PlatformerEvent> ev = Record(e);
		Run(e, Right, 12.0);
		Assert.Equal(PlatformerPhase.Cleared, e.Phase);
		PlatformerEvent goal = Assert.Single(ev, x => x.Kind == PlatformerEventKind.Goal);
		Assert.True(goal.Value >= 100);
		Assert.Equal(0, (int)e.TimeLeft);
		Assert.Contains(ev, x => x.Kind == PlatformerEventKind.LevelClear);
		Assert.True(e.Score > goal.Value);
	}

	[Fact]
	public void BundledLevelsParseAndAreCompletable()
	{
		Assert.Equal(4, PlatformerLevels.All.Count);
		Assert.Equal(new[] { "1-1", "1-2", "1-3", "1-4" }, PlatformerLevels.All.Select(l => l.Id));
		Assert.Equal(new[] { PlatformerWorld.Meadow, PlatformerWorld.Cave, PlatformerWorld.Sky, PlatformerWorld.Castle }, PlatformerLevels.All.Select(l => l.World));
		foreach (PlatformerLevel level in PlatformerLevels.All)
		{
			Assert.Equal(15, level.Height);
			Assert.True(level.Width >= 140, level.Id);
			Assert.True(level.StartCol >= 0 && level.GoalCol > level.StartCol, level.Id);
			Assert.True(PlatformerReach.CanFinish(level), "Уровень не проходится: " + level.Id);
		}
		Assert.Contains(PlatformerLevels.All[2].Spawns, s => s.Kind == PlatformerSpawnKind.PlatformH);
		Assert.Contains(PlatformerLevels.All[2].Spawns, s => s.Kind == PlatformerSpawnKind.PlatformV);
		Assert.Contains(PlatformerLevels.All[3].Spawns, s => s.Kind == PlatformerSpawnKind.FireBar);
		Assert.Single(PlatformerLevels.All[3].Spawns, s => s.Kind == PlatformerSpawnKind.Boss);
	}

	[Fact]
	public void ReachDetectsImpossibleGap()
	{
		string[] rows = Flat(40, 10, 2);
		rows[0] = new string('.', 40);
		rows[7] = "..S..............................E......";
		rows[8] = "##########..........##################..";
		rows[9] = rows[8];
		Assert.False(PlatformerReach.CanFinish(Lvl(rows)));
		rows[8] = "##########....######################....";
		rows[9] = rows[8];
		Assert.True(PlatformerReach.CanFinish(Lvl(rows)));
	}

	[Fact]
	public void ParserRejectsBadLevels()
	{
		Assert.Throws<FormatException>(() => Lvl("....", "..S.", "####", "####"));
		Assert.Throws<FormatException>(() => Lvl("...E", "..S.", "####", "###"));
		Assert.Throws<FormatException>(() => Lvl("...E", "..S.", "##Q#", "####"));
		Assert.Throws<FormatException>(() => PlatformerLevel.Join(new[] { "..", ".." }, new[] { "...", ".." }));
	}

	[Theory]
	[InlineData(1UL)]
	[InlineData(42UL)]
	[InlineData(777UL)]
	[InlineData(123456789UL)]
	[InlineData(0xDEADBEEFUL)]
	public void GeneratedLevelsAreCompletableAndDeterministic(ulong seed)
	{
		for (int stage = 0; stage < 8; stage++)
		{
			PlatformerLevel a = PlatformerGenerator.Generate(seed, stage);
			PlatformerLevel b = PlatformerGenerator.Generate(seed, stage);
			Assert.True(PlatformerReach.CanFinish(a), $"seed {seed}, stage {stage}");
			Assert.Equal(a.Tiles.ToArray(), b.Tiles.ToArray());
			Assert.Equal(a.Spawns, b.Spawns);
			Assert.Equal((PlatformerWorld)(stage % 4), a.World);
		}
	}

	[Fact]
	public void SameInputsGiveSameRun()
	{
		PlatformerEngine a = new PlatformerEngine(PlatformerLevels.All[0]);
		PlatformerEngine b = new PlatformerEngine(PlatformerLevels.All[0]);
		for (int i = 0; i < 120 * 30; i++)
		{
			PlatformerInput input = Right | (i % 70 < 30 ? Jump : None) | (i % 300 < 150 ? PlatformerInput.Run : None);
			a.SetInput(input);
			b.SetInput(input);
			a.Update(Dt);
			b.Update(Dt);
		}
		Assert.Equal(a.Phase, b.Phase);
		Assert.Equal(a.Score, b.Score);
		Assert.Equal(a.Coins, b.Coins);
		Assert.Equal(a.MetaLeft, b.MetaLeft);
		Assert.Equal(a.MetaTop, b.MetaTop);
		Assert.Equal(a.CameraX, b.CameraX);
		Assert.Equal(a.Enemies.ToArray().Select(x => (x.State, x.X, x.Y)), b.Enemies.ToArray().Select(x => (x.State, x.X, x.Y)));
		Assert.True(a.Score > 0 || a.MetaX > 200.0);
	}

	[Fact]
	public void CameraStaysInsideLevelAndFollows()
	{
		PlatformerEngine e = new PlatformerEngine(PlatformerLevels.All[0]);
		Assert.Equal(0.0, e.CameraX);
		Run(e, Right | PlatformerInput.Run, 3.0);
		Assert.True(e.CameraX > 0.0);
		Assert.True(e.MetaX > e.CameraX && e.MetaX < e.CameraX + e.ViewWidth);
		Assert.InRange(e.CameraX, 0.0, e.Width * 16 - e.ViewWidth);
	}

	[Fact]
	public void ProgressUnlocksNextLevel()
	{
		string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mm-platformer-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			PlatformerProgress p = new PlatformerProgress(path);
			Assert.Equal(1, p.Unlocked);
			Assert.True(p.Complete(0));
			Assert.False(p.Complete(0));
			Assert.Equal(2, new PlatformerProgress(path).Unlocked);
			p.Complete(3);
			Assert.Equal(PlatformerLevels.Count, new PlatformerProgress(path).Unlocked);
		}
		finally
		{
			System.IO.File.Delete(path);
		}
	}
}
