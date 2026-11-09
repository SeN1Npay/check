using System;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

// Стеклянные карточки на рабочем столе. API используется SessionController — сигнатуры не менять.
internal sealed class CardsController : IDisposable
{
	public CardsController(ThemeProfile profile, MetamorphServices services)
	{
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		return profile.Desktop.Cards.Enabled;
	}

	public bool EditMode { get; private set; }

	public void Start()
	{
	}

	public void ToggleEditMode()
	{
		EditMode = !EditMode;
	}

	public void Dispose()
	{
	}
}
