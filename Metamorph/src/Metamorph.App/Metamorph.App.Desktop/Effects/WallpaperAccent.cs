using System;
using System.Windows.Media;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Effects;

// Акцент, извлечённый из текущих обоев (Desktop.Effects.AccentFromWallpaper). Changed вызывается на UI-потоке.
public static class WallpaperAccent
{
	public static Color? Current { get; private set; }

	public static event Action<Color>? Changed;

	internal static void Publish(Color color)
	{
		if (Current == color)
		{
			return;
		}
		Current = color;
		EventTrace.Mark("effects.accent", color.ToString());
		try
		{
			WallpaperAccent.Changed?.Invoke(color);
		}
		catch (Exception ex)
		{
			Log.Error("Эффекты: подписчик акцента обоев", ex);
		}
	}

	internal static void Reset()
	{
		Current = null;
	}
}
