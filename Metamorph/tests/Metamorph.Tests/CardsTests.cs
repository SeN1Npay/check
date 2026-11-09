using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metamorph.Core.Desktop;
using Metamorph.Core.Models;
using Xunit;

namespace Metamorph.Tests;

public class CardsLayoutTests
{
	private static readonly CardRect Screen = new CardRect(0, 0, 1920, 1080);

	[Theory]
	[InlineData("minimal")]
	[InlineData("dashboard")]
	[InlineData("clock")]
	public void Presets_AreInsideAndDoNotOverlap(string name)
	{
		List<CardSettings> cards = CardLayout.Preset(name);
		Assert.NotEmpty(cards);
		List<CardRect> rects = new List<CardRect>();
		foreach (CardSettings c in cards)
		{
			Assert.Contains(c.Kind, CardsSettings.Known);
			Assert.InRange(c.X, 0.0, 1.0);
			Assert.InRange(c.Y, 0.0, 1.0);
			(double w, double h) = CardLayout.SizeOf(c);
			CardRect r = new CardRect(c.X * Screen.Width, c.Y * Screen.Height, w, h);
			Assert.True(r.Right <= Screen.Right && r.Bottom <= Screen.Bottom, $"{c.Kind} выходит за экран");
			Assert.Equal(r, CardLayout.ToRect(c, Screen, w, h));
			rects.Add(r);
		}
		for (int i = 0; i < rects.Count; i++)
		{
			for (int j = i + 1; j < rects.Count; j++)
			{
				Assert.False(rects[i].Intersects(rects[j]), $"{cards[i].Kind} пересекается с {cards[j].Kind}");
			}
		}
	}

	[Fact]
	public void UnknownPreset_FallsBackToDashboard()
	{
		Assert.Equal(CardLayout.Preset("dashboard").Count, CardLayout.Preset("nope").Count);
	}

	[Fact]
	public void Snap_ToGrid_WhenNothingNear()
	{
		CardSnapResult r = CardLayout.Snap(new CardRect(203, 307, 100, 100), Array.Empty<CardRect>(), Screen, 16);
		Assert.Equal(208, r.Rect.X);
		Assert.Equal(304, r.Rect.Y);
		Assert.Null(r.GuideX);
		Assert.Null(r.GuideY);
	}

	[Fact]
	public void Snap_ToOtherCardEdge_WithGuide()
	{
		CardRect other = new CardRect(500, 500, 200, 100);
		// Левый край почти совпадает с левым краем соседа.
		CardSnapResult r = CardLayout.Snap(new CardRect(505, 650, 100, 50), new[] { other }, Screen, 16);
		Assert.Equal(500, r.Rect.X);
		Assert.Equal(500, r.GuideX);
		// Вплотную под соседом с зазором.
		CardSnapResult below = CardLayout.Snap(new CardRect(900, 613, 100, 50), new[] { other }, Screen, 16);
		Assert.Equal(600 + CardLayout.Gap, below.Rect.Y);
	}

	[Fact]
	public void Snap_ToScreenCenter()
	{
		CardSnapResult r = CardLayout.Snap(new CardRect(905, 207, 100, 100), Array.Empty<CardRect>(), Screen, 0);
		Assert.Equal(910, r.Rect.X);
		Assert.Equal(960, r.GuideX);
	}

	[Fact]
	public void Snap_ClampsIntoWorkArea()
	{
		CardRect area = new CardRect(0, 0, 1920, 1040);
		CardSnapResult r = CardLayout.Snap(new CardRect(1900, -50, 200, 100), Array.Empty<CardRect>(), area, 16);
		Assert.Equal(1720, r.Rect.X);
		Assert.Equal(0, r.Rect.Y);
	}

	[Fact]
	public void Fraction_RoundTrips_OnOffsetMonitor()
	{
		CardRect area = new CardRect(1920, 40, 2560, 1400);
		CardRect rect = new CardRect(2560, 390, 300, 200);
		(double x, double y) = CardLayout.ToFraction(rect, area);
		CardRect back = CardLayout.ToRect(new CardSettings { X = x, Y = y }, area, 300, 200);
		Assert.Equal(rect.X, back.X, 1);
		Assert.Equal(rect.Y, back.Y, 1);
	}

	[Fact]
	public void SizeOf_UsesExplicitSizeAndScale()
	{
		(double w, double h) = CardLayout.SizeOf(new CardSettings { Kind = "note", Width = 400, Height = 300 }, 1.5);
		Assert.Equal(600, w);
		Assert.Equal(450, h);
	}

	[Fact]
	public void FindFreeSpot_AvoidsOthers()
	{
		List<CardRect> others = CardLayout.Preset("dashboard").Select(c =>
		{
			(double w, double h) = CardLayout.SizeOf(c);
			return CardLayout.ToRect(c, Screen, w, h);
		}).ToList();
		CardRect spot = CardLayout.FindFreeSpot(270, 230, others, Screen, 16);
		Assert.DoesNotContain(others, o => o.Intersects(spot));
	}
}

public class CardsStoreTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "mm-cards-" + Guid.NewGuid().ToString("N"));

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
	public void Layout_SavesPerTheme()
	{
		CardLayoutStore store = new CardLayoutStore(Path.Combine(_dir, "cards.json"));
		Assert.Null(store.Load("a"));
		store.Save("a", new[] { new CardSettings { Kind = "note", X = 0.2, Y = 0.3, Options = { ["id"] = "x" } } });
		store.Save("b", new[] { new CardSettings { Kind = "clock" } });
		List<CardSettings>? a = new CardLayoutStore(Path.Combine(_dir, "cards.json")).Load("a");
		Assert.NotNull(a);
		Assert.Single(a!);
		Assert.Equal("note", a![0].Kind);
		Assert.Equal(0.3, a[0].Y);
		Assert.Equal("x", a[0].Options["id"]);
		store.Reset("a");
		Assert.Null(store.Load("a"));
		Assert.NotNull(store.Load("b"));
	}

	[Fact]
	public void Layout_SurvivesCorruptFile()
	{
		Directory.CreateDirectory(_dir);
		string file = Path.Combine(_dir, "cards.json");
		File.WriteAllText(file, "{ not json !!!");
		CardLayoutStore store = new CardLayoutStore(file);
		Assert.Null(store.Load("a"));
		store.Save("a", new[] { new CardSettings { Kind = "quote" } });
		Assert.Single(store.Load("a")!);
	}

	[Fact]
	public void Sanitize_DropsUnknownAndClamps()
	{
		List<CardSettings> list = CardLayoutStore.Sanitize(new CardSettings?[]
		{
			new CardSettings { Kind = "bogus" },
			null,
			new CardSettings { Kind = "CLOCK", X = 5, Y = double.NaN }
		});
		Assert.Single(list);
		Assert.Equal("clock", list[0].Kind);
		Assert.Equal(1.0, list[0].X);
		Assert.Equal(0.0, list[0].Y);
	}

	[Fact]
	public void Effective_PrefersUserThenItemsThenPreset()
	{
		CardsSettings s = new CardsSettings { Preset = "minimal" };
		Assert.Equal(CardLayout.Preset("minimal").Count, CardLayoutStore.Effective(s, null).Count);
		s.Items.Add(new CardSettings { Kind = "todo" });
		Assert.Equal("todo", Assert.Single(CardLayoutStore.Effective(s, null)).Kind);
		CardsSettings custom = new CardsSettings { Preset = "custom" };
		Assert.Empty(CardLayoutStore.Effective(custom, null));
		List<CardSettings> user = new List<CardSettings> { new CardSettings { Kind = "media" } };
		Assert.Equal("media", Assert.Single(CardLayoutStore.Effective(s, user)).Kind);
		// Эффективная раскладка — копия, правки не трогают тему.
		CardLayoutStore.Effective(s, null)[0].X = 0.9;
		Assert.Equal(0.0, s.Items[0].X);
	}

	[Fact]
	public void Data_PersistsNotesAndTodos()
	{
		string file = Path.Combine(_dir, "cards-data.json");
		CardDataStore store = new CardDataStore(file);
		store.SetNote("default", "привет");
		store.SetTodos("default", new[] { new CardTodo { Text = "купить хлеб", Done = true }, new CardTodo { Text = "  " } });
		CardDataStore again = new CardDataStore(file);
		Assert.Equal("привет", again.GetNote("default"));
		CardTodo todo = Assert.Single(again.GetTodos("default"));
		Assert.True(todo.Done);
		Assert.Equal("", again.GetNote("other"));
	}

	[Fact]
	public void Data_SurvivesCorruptFile()
	{
		Directory.CreateDirectory(_dir);
		string file = Path.Combine(_dir, "cards-data.json");
		File.WriteAllText(file, "[[[");
		CardDataStore store = new CardDataStore(file);
		Assert.Equal("", store.GetNote("default"));
		Assert.Empty(store.GetTodos("default"));
	}
}

public class CardsContentTests
{
	[Fact]
	public void Quotes_HaveEnoughAndRotateDaily()
	{
		Assert.True(CardQuotes.All.Count >= 40);
		Assert.All(CardQuotes.All, q =>
		{
			Assert.False(string.IsNullOrWhiteSpace(q.Ru));
			Assert.False(string.IsNullOrWhiteSpace(q.En));
			Assert.False(string.IsNullOrWhiteSpace(q.AuthorRu));
		});
		DateTime day = new DateTime(2026, 11, 9, 8, 0, 0);
		Assert.Equal(CardQuotes.ForDate(day), CardQuotes.ForDate(day.AddHours(10)));
		Assert.NotEqual(CardQuotes.IndexFor(day), CardQuotes.IndexFor(day.AddDays(1)));
	}

	[Fact]
	public void Calendar_StartsOnLocaleFirstDay()
	{
		// 1 ноября 2026 — воскресенье.
		DateTime[] monday = CardCalendar.Month(2026, 11, DayOfWeek.Monday);
		Assert.Equal(42, monday.Length);
		Assert.Equal(new DateTime(2026, 10, 26), monday[0]);
		Assert.Equal(new DateTime(2026, 11, 1), monday[6]);
		DateTime[] sunday = CardCalendar.Month(2026, 11, DayOfWeek.Sunday);
		Assert.Equal(new DateTime(2026, 11, 1), sunday[0]);
		Assert.Equal(DayOfWeek.Monday, CardCalendar.WeekOrder(DayOfWeek.Monday)[0]);
		Assert.Equal(DayOfWeek.Sunday, CardCalendar.WeekOrder(DayOfWeek.Monday)[6]);
	}
}
