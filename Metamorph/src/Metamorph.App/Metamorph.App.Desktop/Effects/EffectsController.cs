using System;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Effects;

// Эффекты поверх обоев: визуализатор, частицы, параллакс, тонировка. API используется SessionController — сигнатуры не менять.
internal sealed class EffectsController : IDisposable
{
	public EffectsController(ThemeProfile profile, MetamorphServices services)
	{
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		EffectsSettings e = profile.Desktop.Effects;
		if (!e.Visualizer.Enabled && !e.Particles.Enabled && !e.Parallax.Enabled && !e.TimeTint.Enabled)
		{
			return e.AccentFromWallpaper;
		}
		return true;
	}

	public void Start()
	{
	}

	// Обои сменились (новый файл/картинка) — для акцента из обоев и параллакса.
	public void OnWallpaperChanged()
	{
	}

	public void Dispose()
	{
	}
}
