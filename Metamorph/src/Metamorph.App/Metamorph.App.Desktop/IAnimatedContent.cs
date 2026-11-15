using System;
using System.Windows;

namespace Metamorph.App.Desktop;

internal interface IAnimatedContent : IDisposable
{
	FrameworkElement Element { get; }

	bool Paused { get; set; }

	void WhenReady(Action ready);
}
