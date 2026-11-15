using Metamorph.Core.Models;
using Xunit;

namespace Metamorph.Tests;

public class ThemeJsonTests
{
	[Fact]
	public void DesktopSectionsHaveDefaults()
	{
		DesktopSettings d = new DesktopSettings();
		Assert.False(d.Cards.Enabled);
		Assert.Equal("dashboard", d.Cards.Preset);
		Assert.False(d.Effects.Particles.Enabled);
		Assert.False(d.HotCorners.Enabled);
	}

	[Fact]
	public void OldThemeWithoutNewSectionsStillLoads()
	{
		string path = System.IO.Path.Combine(TestPaths.Themes, "catppuccin-mocha.json");
		ThemeProfile p = ThemeJson.Deserialize(System.IO.File.ReadAllText(path))!;
		Assert.NotNull(p.Desktop.Cards);
		Assert.NotNull(p.Desktop.Effects.Visualizer);
		Assert.Empty(ThemeValidator.Validate(p));
	}

	[Fact]
	public void NewSectionsRoundTrip()
	{
		ThemeProfile p = new ThemeProfile { Id = "rt", Name = "rt" };
		p.Desktop.Cards.Enabled = true;
		p.Desktop.Cards.Items.Add(new CardSettings { Kind = "weather", X = 0.25, Y = 0.5 });
		p.Desktop.Effects.Particles.Kind = ParticleKind.Sakura;
		p.Bar.Style = BarStyle.Floating;
		ThemeProfile back = ThemeJson.Deserialize(ThemeJson.Serialize(p))!;
		Assert.True(back.Desktop.Cards.Enabled);
		Assert.Equal("weather", back.Desktop.Cards.Items[0].Kind);
		Assert.Equal(ParticleKind.Sakura, back.Desktop.Effects.Particles.Kind);
		Assert.Equal(BarStyle.Floating, back.Bar.Style);
	}

	[Fact]
	public void AllBuiltInThemesValidate()
	{
		foreach (string file in System.IO.Directory.GetFiles(TestPaths.Themes, "*.json"))
		{
			ThemeProfile p = ThemeJson.Deserialize(System.IO.File.ReadAllText(file))!;
			Assert.True(ThemeValidator.Validate(p).Count == 0, file + ": " + string.Join("; ", ThemeValidator.Validate(p)));
		}
	}
}

internal static class TestPaths
{
	public static string Root => Find();

	public static string Themes => System.IO.Path.Combine(Root, "themes");

	private static string Find([System.Runtime.CompilerServices.CallerFilePath] string here = "")
	{
		// tests/Metamorph.Tests/<file>.cs → корень репозитория Metamorph/
		return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(here)!, "..", ".."));
	}
}
