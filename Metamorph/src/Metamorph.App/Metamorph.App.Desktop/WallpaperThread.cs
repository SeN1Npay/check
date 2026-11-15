using System;
using System.Threading;
using System.Windows.Threading;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal static class WallpaperThread
{
	private static readonly object Sync = new object();

	private static Dispatcher? _dispatcher;

	public static bool IsStarted
	{
		get
		{
			lock (Sync)
			{
				Dispatcher dispatcher = _dispatcher;
				return dispatcher != null && !dispatcher.HasShutdownStarted;
			}
		}
	}

	public static bool IsCurrent => _dispatcher?.CheckAccess() ?? false;

	private static Dispatcher Current
	{
		get
		{
			lock (Sync)
			{
				Dispatcher dispatcher = _dispatcher;
				if (dispatcher != null && !dispatcher.HasShutdownStarted)
				{
					return dispatcher;
				}
				ManualResetEventSlim ready = new ManualResetEventSlim();
				try
				{
					Dispatcher created = null;
					Thread thread = new Thread((ThreadStart)delegate
					{
						created = Dispatcher.CurrentDispatcher;
						SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(created));
						created.UnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs e)
						{
							Log.Error("Поток обоев", e.Exception);
							e.Handled = true;
						};
						ready.Set();
						Dispatcher.Run();
					});
					thread.IsBackground = true;
					thread.Name = "Metamorph.Wallpaper";
					thread.SetApartmentState(ApartmentState.STA);
					thread.Start();
					ready.Wait();
					_dispatcher = created;
					EventTrace.Mark("wallpaper.thread.start");
					return _dispatcher;
				}
				finally
				{
					if (ready != null)
					{
						((IDisposable)ready).Dispose();
					}
				}
			}
		}
	}

	public static void Post(Action action)
	{
		Action action2 = action;
		Current.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
		{
			try
			{
				action2();
			}
			catch (Exception ex)
			{
				Log.Error("Поток обоев", ex);
			}
		});
	}

	public static void Run(Action action, int timeoutMs)
	{
		if (!IsStarted)
		{
			return;
		}
		if (IsCurrent)
		{
			action();
			return;
		}
		try
		{
			Current.Invoke(action, DispatcherPriority.Send, CancellationToken.None, TimeSpan.FromMilliseconds(timeoutMs));
		}
		catch (Exception ex)
		{
			Log.Error("Поток обоев", ex);
		}
	}

	public static T Query<T>(Func<T> query, T fallback, int timeoutMs = 400)
	{
		if (!IsStarted)
		{
			return fallback;
		}
		if (IsCurrent)
		{
			return query();
		}
		try
		{
			DispatcherOperation dispatcherOperation = Current.BeginInvoke(DispatcherPriority.Send, query);
			return (T)((dispatcherOperation.Wait(TimeSpan.FromMilliseconds(timeoutMs)) == DispatcherOperationStatus.Completed && dispatcherOperation.Result is T val) ? ((object)val) : ((object)fallback));
		}
		catch
		{
			return fallback;
		}
	}

	public static void Shutdown()
	{
		Dispatcher dispatcher;
		lock (Sync)
		{
			dispatcher = _dispatcher;
			_dispatcher = null;
		}
		if (dispatcher == null || dispatcher.HasShutdownStarted)
		{
			return;
		}
		try
		{
			dispatcher.InvokeShutdown();
		}
		catch (Exception ex)
		{
			Log.Error("Поток обоев: остановка", ex);
		}
	}
}
