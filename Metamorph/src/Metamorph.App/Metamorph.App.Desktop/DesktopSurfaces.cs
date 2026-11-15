using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed record SurfaceTransition(WallpaperTransitionKind Kind, double Seconds)
{
	// Прежнее поведение до настроек переходов: короткий fade.
	public static readonly SurfaceTransition Default = new SurfaceTransition(WallpaperTransitionKind.Fade, 0.35);

	public static SurfaceTransition From(TransitionSettings? s)
	{
		if (s == null)
		{
			return Default;
		}
		double seconds = double.IsFinite(s.Seconds) ? Math.Clamp(s.Seconds, 0.0, 5.0) : Default.Seconds;
		return new SurfaceTransition(s.Kind, seconds);
	}
}

internal static class DesktopSurfaces
{
	private static nint _workerW;

	private static volatile SurfaceTransition _transition = SurfaceTransition.Default;

	private static readonly Dictionary<string, DesktopSurface> Surfaces = new Dictionary<string, DesktopSurface>(StringComparer.Ordinal);

	// Подложку делят анимированные обои и эффекты — считаем пользователей, а не просто «занята».
	private static readonly Dictionary<DesktopSurface, int> InUse = new Dictionary<DesktopSurface, int>();

	public static nint WorkerW => _workerW;

	// Переход при смене обоев на подложке (из темы: Desktop.Effects.Transition).
	public static SurfaceTransition Transition
	{
		get
		{
			return _transition;
		}
		set
		{
			_transition = value ?? SurfaceTransition.Default;
		}
	}

	public static bool LayerAlive
	{
		get
		{
			if (_workerW != IntPtr.Zero)
			{
				return Win32.IsWindow(_workerW);
			}
			return false;
		}
	}

	public static int Count => Surfaces.Count;

	public static bool EnsureLayer()
	{
		if (LayerAlive)
		{
			return true;
		}
		if (Surfaces.Count > 0)
		{
			DestroyAllCore(redraw: false);
		}
		_workerW = DesktopLayer.FindWorkerW();
		return _workerW != IntPtr.Zero;
	}

	private static string Key(Display d)
	{
		return $"{d.Bounds.X},{d.Bounds.Y},{d.Bounds.Width}x{d.Bounds.Height}";
	}

	public static DesktopSurface? Acquire(Display display)
	{
		if (!EnsureLayer())
		{
			return null;
		}
		string text = Key(display);
		if (!Surfaces.TryGetValue(text, out DesktopSurface value) || !value.IsAlive)
		{
			if (value != null)
			{
				InUse.Remove(value);
				value.Dispose();
			}
			value = new DesktopSurface(_workerW, display.Bounds, DesktopLayer.ParentOrigin(_workerW));
			Surfaces[text] = value;
			EventTrace.Mark("wallpaper.surface.create", text);
		}
		else
		{
			EventTrace.Mark("wallpaper.surface.reuse", text);
		}
		InUse[value] = Users(value) + 1;
		return value;
	}

	public static void Release(DesktopSurface surface)
	{
		int users = Users(surface);
		if (users <= 1)
		{
			InUse.Remove(surface);
		}
		else
		{
			InUse[surface] = users - 1;
		}
	}

	// Сколько владельцев держат подложку (вызывать на потоке обоев).
	public static int Users(DesktopSurface surface)
	{
		if (!InUse.TryGetValue(surface, out var value))
		{
			return 0;
		}
		return value;
	}

	public static void CloseUnused()
	{
		if (WallpaperThread.IsStarted)
		{
			WallpaperThread.Post(CloseUnusedCore);
		}
	}

	public static void DestroyAll(bool redraw)
	{
		WallpaperThread.Run(delegate
		{
			DestroyAllCore(redraw);
		}, 2000);
	}

	public static string Describe()
	{
		return WallpaperThread.Query(DescribeCore, "wallpaper thread: " + (WallpaperThread.IsStarted ? "busy" : "not started"));
	}

	private static void CloseUnusedCore()
	{
		int num = 0;
		foreach (var (key, desktopSurface2) in Surfaces.ToList())
		{
			if (!InUse.ContainsKey(desktopSurface2) || !desktopSurface2.IsAlive)
			{
				InUse.Remove(desktopSurface2);
				try
				{
					desktopSurface2.Dispose();
				}
				catch (Exception ex)
				{
					Log.Error("Обои: закрытие подложки", ex);
				}
				Surfaces.Remove(key);
				num++;
			}
		}
		if (num > 0)
		{
			EventTrace.Mark("wallpaper.surface.close", num.ToString());
			Redraw();
		}
	}

	private static void DestroyAllCore(bool redraw)
	{
		foreach (DesktopSurface value in Surfaces.Values)
		{
			try
			{
				value.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("Обои: закрытие подложки", ex);
			}
		}
		bool num = Surfaces.Count > 0;
		Surfaces.Clear();
		InUse.Clear();
		if (num && redraw)
		{
			Redraw();
		}
	}

	private static void Redraw()
	{
		if (LayerAlive)
		{
			Win32.RedrawWindow(_workerW, IntPtr.Zero, IntPtr.Zero, 1413u);
		}
	}

	private static string DescribeCore()
	{
		return $"workerW=0x{((IntPtr)_workerW).ToInt64():X} alive={LayerAlive} surfaces={Surfaces.Count} inUse={InUse.Count} users={InUse.Values.Sum()} transition={_transition.Kind}/{_transition.Seconds:0.##}s";
	}
}
