using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;

namespace Metamorph.App;

public sealed class ThemeCard
{
	public ThemeProfile Profile { get; }

	public string Name => Profile.Name;

	public string Badge
	{
		get
		{
			if (!Profile.IsBuiltIn)
			{
				return Loc.T("своя");
			}
			return Loc.T("встроенная");
		}
	}

	public Brush PreviewBrush { get; }

	public IReadOnlyList<Brush> Swatches { get; }

	public ThemeCard(ThemeProfile profile, Brush preview)
	{
		Profile = profile;
		PreviewBrush = preview;
		Palette palette = profile.Palette;
		Swatches = ((IEnumerable<string>)new string[8] { palette.Background, palette.Surface, palette.Foreground, palette.Accent, palette.Accent2, palette.Red, palette.Green, palette.Yellow }).Select((Func<string, Brush>)((string hex) => BrushFactory.Solid(hex))).ToList();
	}
}
