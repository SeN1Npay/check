using System;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Shelves;

// «Полки» (как Fences). API используется SessionController — сигнатуры не менять.
internal sealed class ShelvesController : IDisposable
{
	public ShelvesController(ThemeProfile profile, MetamorphServices services)
	{
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		return profile.Desktop.Shelves.Enabled;
	}

	public void Start()
	{
	}

	public void Dispose()
	{
	}
}
