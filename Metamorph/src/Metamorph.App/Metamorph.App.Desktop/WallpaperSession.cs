using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class WallpaperSession : IDisposable
{
	private readonly ThemeProfile _profile;

	private readonly MetamorphServices _services;

	private readonly DispatcherTimer _timer = new DispatcherTimer();

	private AnimatedWallpaper? _animated;

	private List<string> _images = new List<string>();

	private int _index = -1;

	private bool _disposed;

	private bool _manualPause;

	public bool HasSlides => _images.Count > 1;

	public bool IsAnimated => _animated != null;

	public bool ManualPause
	{
		get
		{
			return _manualPause;
		}
		set
		{
			if (_manualPause != value)
			{
				_manualPause = value;
				_animated?.SetManualPause(value);
			}
		}
	}

	public WallpaperSession(ThemeProfile profile, MetamorphServices services)
	{
		_profile = profile;
		_services = services;
	}

	public static bool IsNeeded(ThemeProfile p)
	{
		WallpaperKind effectiveKind = p.Wallpaper.EffectiveKind;
		if ((uint)(effectiveKind - 4) <= 2u)
		{
			return true;
		}
		return false;
	}

	private string? Resolve(string? path)
	{
		return _services.Profiles.ResolvePath(_profile, path);
	}

	public void Start()
	{
		WallpaperSettings wallpaper = _profile.Wallpaper;
		switch (wallpaper.EffectiveKind)
		{
		case WallpaperKind.Slideshow:
			_images = WallpaperService.SlideshowImages(Resolve(wallpaper.Folder)).ToList();
			if (_images.Count >= 2)
			{
				if (wallpaper.Shuffle)
				{
					Shuffle(_images);
				}
				_timer.Interval = TimeSpan.FromMinutes(Math.Clamp(wallpaper.IntervalMinutes, 1, 1440));
				_timer.Tick += delegate
				{
					NextImage();
				};
				_timer.Start();
			}
			break;
		case WallpaperKind.TimeOfDay:
			_timer.Tick += delegate
			{
				ApplyTimeOfDay();
			};
			ScheduleTimeOfDay();
			break;
		case WallpaperKind.Video:
		{
			string text = Resolve(wallpaper.Path);
			if (text == null || !File.Exists(text))
			{
				Log.Warn("Файл анимированных обоев не найден: " + wallpaper.Path);
			}
			else
			{
				_animated = new AnimatedWallpaper(text, wallpaper.Style, wallpaper.Muted, _services, _manualPause);
			}
			break;
		}
		}
	}

	public void SetVolume(double volume)
	{
		_animated?.SetVolume(volume);
	}

	public void NextImage()
	{
		if (_images.Count == 0)
		{
			return;
		}
		_index = (_index + 1) % _images.Count;
		try
		{
			if (File.Exists(_images[_index]))
			{
				WallpaperService.SetWallpaper(_images[_index], _profile.Wallpaper.Style);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Слайд-шоу обоев", ex);
		}
	}

	private void ApplyTimeOfDay()
	{
		_timer.Stop();
		try
		{
			WallpaperScheduleEntry wallpaperScheduleEntry = WallpaperService.ActiveScheduleEntry(_profile.Wallpaper, DateTime.Now.TimeOfDay);
			if (wallpaperScheduleEntry != null)
			{
				_services.Wallpapers.ApplyScheduleEntry(_profile, wallpaperScheduleEntry, Resolve(wallpaperScheduleEntry.Path));
			}
		}
		catch (Exception ex)
		{
			Log.Error("Обои по времени суток", ex);
		}
		ScheduleTimeOfDay();
	}

	private void ScheduleTimeOfDay()
	{
		if (!_disposed)
		{
			List<string> list = _profile.Wallpaper.Schedule.Select((WallpaperScheduleEntry e) => e.At).ToList();
			if (list.Count >= 2)
			{
				TimeSpan timeSpan = TimeOfDayParser.UntilNextChange(list, DateTime.Now.TimeOfDay);
				_timer.Interval = ((timeSpan < TimeSpan.FromSeconds(5.0)) ? TimeSpan.FromSeconds(5.0) : (timeSpan + TimeSpan.FromSeconds(1.0)));
				_timer.Start();
			}
		}
	}

	private static void Shuffle(List<string> list)
	{
		for (int num = list.Count - 1; num > 0; num--)
		{
			int num2 = Random.Shared.Next(num + 1);
			int index = num;
			int index2 = num2;
			string value = list[num2];
			string value2 = list[num];
			list[index] = value;
			list[index2] = value2;
		}
	}

	public string Describe()
	{
		WallpaperSettings wallpaper = _profile.Wallpaper;
		List<string> list = new List<string> { $"kind={wallpaper.EffectiveKind}" };
		if (_animated != null)
		{
			list.Add(_animated.State);
		}
		if (_images.Count > 0)
		{
			list.Add($"slides={_images.Count}");
		}
		if (_manualPause)
		{
			list.Add("manualPause");
		}
		return string.Join(" ", list);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_timer.Stop();
			_animated?.Stop();
			_animated = null;
		}
	}
}
