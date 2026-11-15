using System.Windows;

namespace Metamorph.App.Bar;

public abstract class BarModule
{
	public abstract FrameworkElement View { get; }

	public virtual int IntervalTicks => 2;

	public abstract void Update();

	// Панель не видна (полноэкранное окно или автоскрытие): модуль может отпустить ресурсы.
	public virtual void Suspend(bool suspended)
	{
	}

	public virtual void Dispose()
	{
	}
}
