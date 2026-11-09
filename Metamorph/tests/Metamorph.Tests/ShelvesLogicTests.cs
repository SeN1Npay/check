using System;
using System.IO;
using Metamorph.Core.Desktop;
using Xunit;

namespace Metamorph.Tests;

public class ShelvesLogicTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "mm-shelves-" + Guid.NewGuid().ToString("N"));

	public ShelvesLogicTests()
	{
		Directory.CreateDirectory(_root);
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch
		{
		}
	}

	[Fact]
	public void RuleParsesExtensionsAndKeywords()
	{
		ShelfRule r = ShelfRule.Parse(".png, jpg ,folders")!;
		Assert.True(r.Matches("C:\\x\\a.PNG", isDirectory: false));
		Assert.True(r.Matches("a.jpg", isDirectory: false));
		Assert.False(r.Matches("a.txt", isDirectory: false));
		Assert.True(r.Matches("dir", isDirectory: true));
		Assert.False(r.Matches("desktop.ini", isDirectory: false));
		Assert.Null(ShelfRule.Parse("  "));
		Assert.Null(ShelfRule.Parse(", ."));
	}

	[Fact]
	public void ShortcutsRule()
	{
		ShelfRule r = ShelfRule.Parse("shortcuts")!;
		Assert.True(r.Matches("Steam.lnk", isDirectory: false));
		Assert.True(r.Matches("site.url", isDirectory: false));
		Assert.False(r.Matches("folder", isDirectory: true));
		Assert.False(r.Matches("a.exe", isDirectory: false));
	}

	[Fact]
	public void UniqueNameAddsSuffix()
	{
		string[] taken = { Path.Combine("f", "a.txt"), Path.Combine("f", "a (2).txt"), Path.Combine("f", "dir.v1") };
		Func<string, bool> exists = p => Array.IndexOf(taken, p) >= 0;
		Assert.Equal(Path.Combine("f", "b.txt"), ShelfFiles.UniqueName("f", "b.txt", false, exists));
		Assert.Equal(Path.Combine("f", "a (3).txt"), ShelfFiles.UniqueName("f", "a.txt", false, exists));
		Assert.Equal(Path.Combine("f", "dir.v1 (2)"), ShelfFiles.UniqueName("f", "dir.v1", true, exists));
	}

	[Fact]
	public void SafeNameStripsInvalidChars()
	{
		Assert.Equal("a_b_c", ShelfFiles.SafeName("a:b?c"));
		Assert.Equal("shelf", ShelfFiles.SafeName("  .. "));
	}

	[Fact]
	public void MoveIntoNeverOverwrites()
	{
		string shelf = Path.Combine(_root, "shelf");
		Directory.CreateDirectory(shelf);
		File.WriteAllText(Path.Combine(shelf, "a.txt"), "old");
		string src = Path.Combine(_root, "a.txt");
		File.WriteAllText(src, "new");
		string? dest = ShelfFiles.MoveInto(src, shelf, sameVolume: true);
		Assert.Equal(Path.Combine(shelf, "a (2).txt"), dest);
		Assert.False(File.Exists(src));
		Assert.Equal("old", File.ReadAllText(Path.Combine(shelf, "a.txt")));
		Assert.Equal("new", File.ReadAllText(dest!));
	}

	[Fact]
	public void CrossVolumeCopiesAndKeepsOriginal()
	{
		string shelf = Path.Combine(_root, "shelf");
		string dir = Path.Combine(_root, "photos");
		Directory.CreateDirectory(Path.Combine(dir, "sub"));
		File.WriteAllText(Path.Combine(dir, "sub", "x.png"), "x");
		string? dest = ShelfFiles.MoveInto(dir, shelf, sameVolume: false);
		Assert.True(Directory.Exists(dir));
		Assert.True(File.Exists(Path.Combine(dest!, "sub", "x.png")));
	}

	[Fact]
	public void MoveIntoSkipsSelfAndAlreadyThere()
	{
		string shelf = Path.Combine(_root, "shelf");
		Directory.CreateDirectory(shelf);
		File.WriteAllText(Path.Combine(shelf, "a.txt"), "");
		Assert.Null(ShelfFiles.MoveInto(Path.Combine(shelf, "a.txt"), shelf));
		Assert.Null(ShelfFiles.MoveInto(_root, shelf));
		Assert.Null(ShelfFiles.MoveInto(Path.Combine(_root, "missing.txt"), shelf));
	}

	[Fact]
	public void SortIntoMovesOnlyMatching()
	{
		string desktop = Path.Combine(_root, "Desktop");
		string shelf = Path.Combine(desktop, "Images");
		Directory.CreateDirectory(desktop);
		File.WriteAllText(Path.Combine(desktop, "a.png"), "");
		File.WriteAllText(Path.Combine(desktop, "b.txt"), "");
		Directory.CreateDirectory(Path.Combine(desktop, "folder"));
		var moved = ShelfFiles.SortInto(desktop, shelf, ShelfRule.Parse(".png,folders")!);
		Assert.Equal(2, moved.Count);
		Assert.True(File.Exists(Path.Combine(shelf, "a.png")));
		Assert.True(Directory.Exists(Path.Combine(shelf, "folder")));
		Assert.True(File.Exists(Path.Combine(desktop, "b.txt")));
		// Сама папка полки, лежащая на рабочем столе, в себя не переносится.
		Assert.True(Directory.Exists(shelf));
	}

	[Fact]
	public void LayoutSnapsAndClamps()
	{
		ShelfRect work = new ShelfRect(0, 0, 1920, 1040);
		ShelfRect r = ShelfLayout.Fit(new ShelfRect(37, 41, 330, 210), work);
		Assert.Equal(32, r.X);
		Assert.Equal(48, r.Y);
		Assert.Equal(336, r.Width);
		Assert.Equal(208, r.Height);
		ShelfRect off = ShelfLayout.Fit(new ShelfRect(1900, -50, 400, 5000), work);
		Assert.Equal(1520, off.X);
		Assert.Equal(0, off.Y);
		Assert.Equal(1040, off.Height);
		ShelfRect tiny = ShelfLayout.Fit(new ShelfRect(0, 0, 10, 10), work);
		Assert.Equal(ShelfLayout.MinWidth, tiny.Width);
		Assert.Equal(ShelfLayout.MinHeight, tiny.Height);
	}

	[Fact]
	public void FractionsRoundTrip()
	{
		ShelfRect work = new ShelfRect(100, 0, 1000, 800);
		ShelfRect r = ShelfLayout.FromFractions(0.25, 0.5, 200, 100, work);
		Assert.Equal(350, r.X);
		Assert.Equal(400, r.Y);
		(double fx, double fy) = ShelfLayout.ToFractions(r, work);
		Assert.Equal(0.25, fx, 6);
		Assert.Equal(0.5, fy, 6);
	}

	[Fact]
	public void StorePersistsPerTheme()
	{
		string path = Path.Combine(_root, "shelves.json");
		ShelfStore store = ShelfStore.Load(path);
		store.Set("mocha", "Фото", new ShelfState { X = 0.1, Y = 0.2, Width = 300, Height = 200, Collapsed = true });
		store.Save();
		ShelfStore back = ShelfStore.Load(path);
		Assert.True(back.Get("mocha", "Фото")!.Collapsed);
		Assert.Null(back.Get("other", "Фото"));
		File.WriteAllText(path, "{broken");
		Assert.Null(ShelfStore.Load(path).Get("mocha", "Фото"));
	}
}
