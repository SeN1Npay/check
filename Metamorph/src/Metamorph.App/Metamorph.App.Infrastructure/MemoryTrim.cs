using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

// После тяжёлых операций (применение темы, смена обоев) мусор и рабочий набор остаются раздутыми.
// Schedule() можно звать сколько угодно раз: уборка один раз, через 10 с после последнего вызова и только в простое.
internal static class MemoryTrim
{
	private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(10.0);

	private const double UserIdleSeconds = 2.0;

	private const int MaxPostpones = 6;

	private static DispatcherTimer? _timer;

	private static int _postponed;

	public static void Schedule()
	{
		Dispatcher? dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null)
		{
			return;
		}
		if (!dispatcher.CheckAccess())
		{
			dispatcher.BeginInvoke(new Action(Schedule));
			return;
		}
		if (_timer == null)
		{
			_timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
			_timer.Tick += delegate
			{
				OnTick();
			};
		}
		_postponed = 0;
		_timer.Stop();
		_timer.Interval = Debounce;
		_timer.Start();
	}

	private static void OnTick()
	{
		_timer?.Stop();
		// Пользователь как раз что-то делает — паузу GC не подсовываем, ждём ещё немного.
		if (Win32.IdleSeconds() < UserIdleSeconds && _postponed++ < MaxPostpones)
		{
			_timer!.Interval = TimeSpan.FromSeconds(5.0);
			_timer.Start();
			return;
		}
		Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Trim));
	}

	private static void Trim()
	{
		try
		{
			long before = Environment.WorkingSet;
			GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
			SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
			EventTrace.Mark("memory.trim", $"{before / 1048576}MB → {Environment.WorkingSet / 1048576}MB");
		}
		catch (Exception ex)
		{
			Log.Error("Память: уборка", ex);
		}
	}

	[DllImport("kernel32.dll")]
	private static extern nint GetCurrentProcess();

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetProcessWorkingSetSize(nint process, nint minimum, nint maximum);
}
