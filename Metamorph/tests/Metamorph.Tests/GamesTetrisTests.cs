using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Games;
using Xunit;

namespace Metamorph.Tests;

public class GamesTetrisTests
{
	private const int Bottom = TetrisEngine.Height - 1;

	[Fact]
	public void SevenBagDealsEachPieceOncePerSeven()
	{
		SevenBag bag = new SevenBag(new GameRandom(123));
		for (int round = 0; round < 20; round++)
		{
			HashSet<TetrominoKind> seen = new HashSet<TetrominoKind>();
			for (int i = 0; i < 7; i++)
			{
				Assert.True(seen.Add(bag.Next()));
			}
			Assert.Equal(7, seen.Count);
		}
	}

	[Fact]
	public void EngineQueueStartsWithFullBagAndShowsFiveNext()
	{
		TetrisEngine e = new TetrisEngine(99);
		List<TetrominoKind> first = new List<TetrominoKind> { e.Current };
		for (int i = 0; i < TetrisEngine.NextCount; i++)
		{
			first.Add(e.PeekNext(i));
		}
		Assert.Equal(6, first.Distinct().Count());
	}

	[Fact]
	public void SameSeedSamePieces()
	{
		TetrisEngine a = new TetrisEngine(5);
		TetrisEngine b = new TetrisEngine(5);
		for (int i = 0; i < 30; i++)
		{
			Assert.Equal(a.Current, b.Current);
			a.HardDrop();
			b.HardDrop();
		}
	}

	[Fact]
	public void IPieceKicksOffLeftWall()
	{
		TetrisEngine e = new TetrisEngine(1);
		e.SpawnPiece(TetrominoKind.I);
		Assert.True(e.TryRotate(clockwise: true));
		Assert.Equal(1, e.Rotation);
		while (e.TryShift(-1))
		{
		}
		Assert.Equal(-2, e.X);
		int y = e.Y;
		// R→2 у стены: тесты (0,0) и (−1,0) не влезают, срабатывает (+2,0).
		Assert.True(e.TryRotate(clockwise: true));
		Assert.Equal(2, e.Rotation);
		Assert.Equal(0, e.X);
		Assert.Equal(y, e.Y);
	}

	[Fact]
	public void TPieceKicksOffLeftWall()
	{
		TetrisEngine e = new TetrisEngine(1);
		e.SpawnPiece(TetrominoKind.T);
		Assert.True(e.TryRotate(clockwise: true));
		while (e.TryShift(-1))
		{
		}
		Assert.Equal(-1, e.X);
		Assert.True(e.TryRotate(clockwise: false));
		Assert.Equal(0, e.Rotation);
		Assert.Equal(0, e.X);
	}

	[Fact]
	public void SingleLineClearScoresWithHardDrop()
	{
		TetrisEngine e = new TetrisEngine(3);
		for (int x = 0; x < TetrisEngine.Width; x++)
		{
			if (x < 3 || x > 6)
			{
				e.SetCell(x, Bottom, TetrominoKind.O);
			}
		}
		e.SpawnPiece(TetrominoKind.I);
		int d = e.GhostY - e.Y;
		List<TetrisEvent> events = new List<TetrisEvent>();
		e.Event += events.Add;
		Assert.Equal(d, e.HardDrop());
		Assert.Equal(1, e.Lines);
		Assert.Equal(100 + 2 * d, e.Score);
		Assert.Contains(events, ev => ev.Kind == TetrisEventKind.LineClear && ev.Value == 1);
		for (int x = 0; x < TetrisEngine.Width; x++)
		{
			Assert.Equal(0, e.Cell(x, Bottom));
		}
	}

	[Fact]
	public void TetrisBackToBackComboAndLevelUp()
	{
		TetrisEngine e = new TetrisEngine(4);
		List<TetrisEvent> events = new List<TetrisEvent>();
		e.Event += events.Add;
		long[] expected = { 800, 1250, 1300 };
		for (int round = 0; round < 3; round++)
		{
			FillWell(e);
			e.SpawnPiece(TetrominoKind.I);
			Assert.True(e.TryRotate(clockwise: true));
			while (e.TryShift(-1))
			{
			}
			e.HardDrop();
			Assert.Equal(4, e.LastClear.Lines);
			Assert.Equal(expected[round], e.LastClear.Points);
			Assert.Equal(round > 0, e.LastClear.BackToBack);
		}
		Assert.Equal(12, e.Lines);
		Assert.Equal(2, e.Level);
		Assert.Contains(events, ev => ev.Kind == TetrisEventKind.LevelUp && ev.Value == 2);
		Assert.Equal(3, events.Count(ev => ev.Kind == TetrisEventKind.LineClear && ev.Value == 4));
	}

	[Fact]
	public void TSpinDoubleDetected()
	{
		TetrisEngine e = new TetrisEngine(8);
		for (int x = 0; x < TetrisEngine.Width; x++)
		{
			if (x != 4)
			{
				e.SetCell(x, Bottom, TetrominoKind.J);
			}
			if (x < 3 || x > 5)
			{
				e.SetCell(x, Bottom - 1, TetrominoKind.L);
			}
		}
		e.SetCell(3, Bottom - 2, TetrominoKind.Z);
		e.SpawnPiece(TetrominoKind.T);
		Assert.True(e.TryRotate(clockwise: true));
		e.SetSoftDrop(true);
		for (int i = 0; i < 2000 && !e.Grounded; i++)
		{
			e.Update(TetrisEngine.Step);
		}
		e.SetSoftDrop(false);
		Assert.Equal(Bottom - 2, e.Y);
		Assert.True(e.TryRotate(clockwise: true));
		Assert.Equal(2, e.Rotation);
		e.HardDrop();
		Assert.Equal(2, e.LastClear.Lines);
		Assert.Equal(TSpinKind.Full, e.LastClear.TSpin);
		Assert.Equal(1200, e.LastClear.Points);
	}

	[Fact]
	public void LockDelayResetsAreCapped()
	{
		TetrisEngine e = new TetrisEngine(11);
		e.SpawnPiece(TetrominoKind.T);
		e.SetSoftDrop(true);
		for (int i = 0; i < 4000 && !e.Grounded; i++)
		{
			e.Update(TetrisEngine.Step);
		}
		e.SetSoftDrop(false);
		Assert.True(e.Grounded);
		Assert.Equal(0, e.PiecesPlaced);
		for (int i = 0; i < TetrisEngine.MaxLockResets; i++)
		{
			Assert.True(e.TryShift(i % 2 == 0 ? -1 : 1));
			Advance(e, 0.4);
			Assert.Equal(0, e.PiecesPlaced);
		}
		Assert.Equal(TetrisEngine.MaxLockResets, e.LockResets);
		// Сбросы кончились: очередной сдвиг уже не продлевает таймер.
		Assert.True(e.TryShift(-1));
		Advance(e, 0.15);
		Assert.Equal(1, e.PiecesPlaced);
	}

	[Fact]
	public void PieceLocksAfterHalfSecondOnGround()
	{
		TetrisEngine e = new TetrisEngine(12);
		e.SpawnPiece(TetrominoKind.O);
		e.SetSoftDrop(true);
		for (int i = 0; i < 4000 && !e.Grounded; i++)
		{
			e.Update(TetrisEngine.Step);
		}
		e.SetSoftDrop(false);
		Advance(e, 0.3);
		Assert.Equal(0, e.PiecesPlaced);
		Advance(e, 0.25);
		Assert.Equal(1, e.PiecesPlaced);
	}

	[Fact]
	public void HoldOncePerPiece()
	{
		TetrisEngine e = new TetrisEngine(21);
		TetrominoKind first = e.Current;
		TetrominoKind next = e.PeekNext(0);
		Assert.True(e.Hold());
		Assert.Equal(first, e.HoldPiece);
		Assert.Equal(next, e.Current);
		Assert.False(e.Hold());
		e.HardDrop();
		TetrominoKind current = e.Current;
		Assert.True(e.Hold());
		Assert.Equal(first, e.Current);
		Assert.Equal(current, e.HoldPiece);
	}

	[Fact]
	public void DasThenArrRepeats()
	{
		TetrisEngine e = new TetrisEngine(30);
		e.SpawnPiece(TetrominoKind.O);
		int x0 = e.X;
		e.SetLeft(true);
		Assert.Equal(x0 - 1, e.X);
		Advance(e, 0.1);
		Assert.Equal(x0 - 1, e.X);
		Advance(e, 0.1);
		Assert.True(e.X < x0 - 1);
		Advance(e, 0.5);
		Assert.Equal(0, e.X);
		e.SetLeft(false);
	}

	[Fact]
	public void GravityGetsFasterWithLevel()
	{
		Assert.Equal(1.0, TetrisEngine.GravityInterval(1), 6);
		for (int l = 2; l <= 20; l++)
		{
			Assert.True(TetrisEngine.GravityInterval(l) < TetrisEngine.GravityInterval(l - 1));
		}
	}

	[Fact]
	public void BlockOutEndsGame()
	{
		TetrisEngine e = new TetrisEngine(2);
		for (int y = 1; y < TetrisEngine.Height; y++)
		{
			for (int x = 1; x < TetrisEngine.Width; x++)
			{
				e.SetCell(x, y, TetrominoKind.S);
			}
		}
		bool over = false;
		e.Event += ev => over |= ev.Kind == TetrisEventKind.GameOver;
		e.HardDrop();
		Assert.True(e.IsGameOver);
		Assert.True(over);
	}

	private static void Advance(TetrisEngine e, double seconds)
	{
		for (double t = 0; t < seconds - 1e-9; t += 0.05)
		{
			e.Update(Math.Min(0.05, seconds - t));
		}
	}

	private static void FillWell(TetrisEngine e)
	{
		for (int y = Bottom - 3; y <= Bottom; y++)
		{
			for (int x = 1; x < TetrisEngine.Width; x++)
			{
				e.SetCell(x, y, TetrominoKind.Z);
			}
		}
	}
}
