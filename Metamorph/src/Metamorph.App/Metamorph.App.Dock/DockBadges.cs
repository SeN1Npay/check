using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using ManagedShell.Interop;
using ManagedShell.WindowsTasks;
using Metamorph.Core.Services;

namespace Metamorph.App.Dock;

internal static class DockBadges
{
	private static TasksService? _service;

	private static Tasks? _tasks;

	private static int _users;

	public static void Acquire()
	{
		if (_users++ > 0)
		{
			return;
		}
		try
		{
			_service = new TasksService();
			_tasks = new Tasks(_service);
			_tasks.Initialize();
		}
		catch (Exception ex)
		{
			Log.Error("Бейджи дока: не удалось подключиться к ITaskbarList", ex);
			_tasks = null;
			_service = null;
		}
	}

	public static void Release()
	{
		if (_users != 0 && --_users <= 0)
		{
			try
			{
				_tasks?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("Бейджи дока: отключение", ex);
			}
			_tasks = null;
			_service = null;
		}
	}

	public static BadgeInfo For(IEnumerable<nint> handles)
	{
		ICollectionView collectionView = _tasks?.GroupedWindows;
		if (collectionView == null)
		{
			return default(BadgeInfo);
		}
		HashSet<nint> hashSet = handles.ToHashSet();
		if (hashSet.Count == 0)
		{
			return default(BadgeInfo);
		}
		double? progress = null;
		bool flag = false;
		bool flag2 = false;
		bool indeterminate = false;
		ImageSource imageSource = null;
		foreach (object item in collectionView)
		{
			if (item is ApplicationWindow applicationWindow && hashSet.Contains(applicationWindow.Handle))
			{
				if (imageSource == null)
				{
					imageSource = applicationWindow.OverlayIcon;
				}
				switch (applicationWindow.ProgressState)
				{
				case NativeMethods.TBPFLAG.TBPF_INDETERMINATE:
					indeterminate = true;
					break;
				case NativeMethods.TBPFLAG.TBPF_NORMAL:
				case NativeMethods.TBPFLAG.TBPF_ERROR:
				case NativeMethods.TBPFLAG.TBPF_PAUSED:
				{
					double value = ((applicationWindow.ProgressValue > 100) ? ((double)applicationWindow.ProgressValue / 65535.0) : ((double)applicationWindow.ProgressValue / 100.0));
					progress = Math.Max(progress.GetValueOrDefault(), Math.Clamp(value, 0.0, 1.0));
					flag |= applicationWindow.ProgressState == NativeMethods.TBPFLAG.TBPF_ERROR;
					flag2 |= applicationWindow.ProgressState == NativeMethods.TBPFLAG.TBPF_PAUSED;
					break;
				}
				}
			}
		}
		return new BadgeInfo(progress, flag, flag2, indeterminate, imageSource);
	}
}
