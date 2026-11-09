using System;
using System.IO;
using Metamorph.Core.Games;
using Xunit;

namespace Metamorph.Tests;

public class GamesScoresTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "mm-games-" + Guid.NewGuid().ToString("N"));

	private string File_ => Path.Combine(_dir, "games.json");

	public void Dispose()
	{
		try
		{
			Directory.Delete(_dir, recursive: true);
		}
		catch
		{
		}
	}

	[Fact]
	public void MissingFileMeansNoRecords()
	{
		GameScores s = new GameScores(File_);
		Assert.Equal(0, s.Best("dino"));
		Assert.Null(s.LastGame);
	}

	[Fact]
	public void SubmitKeepsBestAndPersists()
	{
		GameScores s = new GameScores(File_);
		Assert.True(s.Submit("dino", 120));
		Assert.False(s.Submit("dino", 80));
		Assert.False(s.Submit("dino", 120));
		Assert.True(s.Submit("tetris", 5000));
		GameScores reloaded = new GameScores(File_);
		Assert.Equal(120, reloaded.Best("dino"));
		Assert.Equal(5000, reloaded.Best("tetris"));
		Assert.Equal("tetris", reloaded.LastGame);
		Assert.Equal(3, reloaded.Get("dino").Plays);
		Assert.NotNull(reloaded.Get("dino").LastPlayed);
	}

	[Fact]
	public void ZeroScoreIsNotARecord()
	{
		GameScores s = new GameScores(File_);
		Assert.False(s.Submit("dino", 0));
	}

	[Fact]
	public void CorruptFileIsIgnoredAndOverwritten()
	{
		Directory.CreateDirectory(_dir);
		File.WriteAllText(File_, "{ \"games\": [1, 2, oops");
		GameScores s = new GameScores(File_);
		Assert.Equal(0, s.Best("tetris"));
		Assert.True(s.Submit("tetris", 10));
		Assert.Equal(10, new GameScores(File_).Best("tetris"));
	}
}
