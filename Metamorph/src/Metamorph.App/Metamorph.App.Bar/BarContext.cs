using System;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed record BarContext(Brush Foreground, Brush Accent, Brush Muted, Brush Alert, string ClockFormat, Action OpenMainWindow)
{
	public ThemeProfile Profile { get; init; } = new ThemeProfile();


	public MetamorphServices? Services { get; init; }

	public bool Primary { get; init; } = true;


	public Brush Background { get; init; } = Brushes.Black;


	public Brush Surface { get; init; } = Brushes.DimGray;


	public Brush Green { get; init; } = Brushes.LightGreen;


	public Brush Yellow { get; init; } = Brushes.Khaki;


	public static BarContext For(ThemeProfile profile, MetamorphServices? services, Action openMainWindow, bool primary)
	{
		Palette palette = profile.Palette;
		return new BarContext(BrushFactory.Solid(palette.Foreground), BrushFactory.Solid(palette.Accent), BrushFactory.Solid(palette.Muted), BrushFactory.Solid(palette.Red), profile.Bar.ClockFormat, openMainWindow)
		{
			Profile = profile,
			Services = services,
			Primary = primary,
			Background = BrushFactory.Solid(palette.Background),
			Surface = BrushFactory.Solid(palette.Surface),
			Green = BrushFactory.Solid(palette.Green),
			Yellow = BrushFactory.Solid(palette.Yellow)
		};
	}
}
