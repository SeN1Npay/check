using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Metamorph.Core.Windows;

namespace Metamorph.App.Windows;

internal static class TilingActions
{
	private static int Gap(TilingSettings s)
	{
		return (int)Math.Round((double)Math.Clamp(s.Gaps, 0, 64) * Display.Scale);
	}

	private static nint Target()
	{
		nint foregroundWindow = Win32.GetForegroundWindow();
		if (!WindowMover.IsTileable(foregroundWindow) && !Win32.IsZoomed(foregroundWindow))
		{
			return IntPtr.Zero;
		}
		return foregroundWindow;
	}

	public static void Snap(TilingSettings s, SnapDirection direction)
	{
		TilingSettings s2 = s;
		Safe(delegate
		{
			nint num = Target();
			if (num != IntPtr.Zero)
			{
				IntRect work = WindowMover.WorkArea(num);
				IntRect current = (Win32.IsZoomed(num) ? TilingLayout.Maximize(work, Gap(s2)) : WindowMover.VisibleBounds(num));
				WindowMover.MoveVisible(num, TilingLayout.Snap(current, work, Gap(s2), direction));
			}
		});
	}

	public static void Maximize(TilingSettings s)
	{
		TilingSettings s2 = s;
		Safe(delegate
		{
			nint num = Target();
			if (num != IntPtr.Zero)
			{
				WindowMover.MoveVisible(num, TilingLayout.Maximize(WindowMover.WorkArea(num), Gap(s2)));
			}
		});
	}

	public static void Center(TilingSettings s)
	{
		Safe(delegate
		{
			nint num = Target();
			if (num != IntPtr.Zero)
			{
				WindowMover.MoveVisible(num, TilingLayout.Center(WindowMover.WorkArea(num)));
			}
		});
	}

	public static void TileAll(TilingSettings s)
	{
		TilingSettings s2 = s;
		Safe(delegate
		{
			nint num = Target();
			if (num != IntPtr.Zero)
			{
				List<nint> list = WindowMover.TileableOnSameMonitor(num);
				list.Remove(num);
				list.Insert(0, num);
				list = list.Take(8).ToList();
				IReadOnlyList<IntRect> readOnlyList = TilingLayout.MasterStack(WindowMover.WorkArea(num), Gap(s2), list.Count, s2.MasterRatio);
				for (int i = 0; i < list.Count; i++)
				{
					WindowMover.MoveVisible(list[i], readOnlyList[i]);
				}
			}
		});
	}

	private static void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error("Тайлинг", ex);
		}
	}
}
