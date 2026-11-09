using System;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop;

// Горячие углы экрана. API используется SessionController — сигнатуры не менять.
internal sealed class HotCorners : IDisposable
{
	// run — выполнить действие из HotCornersSettings.Actions (обрабатывает App.RunDesktopAction).
	public HotCorners(HotCornersSettings settings, Action<string> run)
	{
	}

	public void Start()
	{
	}

	public void Dispose()
	{
	}
}
