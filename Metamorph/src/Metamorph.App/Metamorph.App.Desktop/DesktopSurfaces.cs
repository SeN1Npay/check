using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal static class DesktopSurfaces
{
	private static nint _workerW;

	private static readonly Dictionary<string, DesktopSurface> Surfaces = new Dictionary<string, DesktopSurface>(StringComparer.Ordinal);

	private static readonly HashSet<DesktopSurface> InUse = new HashSet<DesktopSurface>();

	public static nint WorkerW => _workerW;

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
			value?.Dispose();
			value = new DesktopSurface(_workerW, display.Bounds, DesktopLayer.ParentOrigin(_workerW));
			Surfaces[text] = value;
			EventTrace.Mark("wallpaper.surface.create", text);
		}
		else
		{
			EventTrace.Mark("wallpaper.surface.reuse", text);
		}
		InUse.Add(value);
		return value;
	}

	public static void Release(DesktopSurface surface)
	{
		InUse.Remove(surface);
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
			if (!InUse.Contains(desktopSurface2) || !desktopSurface2.IsAlive)
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
		return $"workerW=0x{((IntPtr)_workerW).ToInt64():X} alive={LayerAlive} surfaces={Surfaces.Count} inUse={InUse.Count}";
	}
}
