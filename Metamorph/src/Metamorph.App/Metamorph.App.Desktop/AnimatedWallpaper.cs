using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class AnimatedWallpaper
{
	private sealed record Placed(Display Display, DesktopSurface Surface, IAnimatedContent Content);

	private readonly string _path;

	private readonly WallpaperStyle _style;

	private readonly bool _muted;

	private readonly MetamorphServices _services;

	private readonly List<Placed> _placed = new List<Placed>();

	private readonly LoopBreaker _restarts = new LoopBreaker(3, TimeSpan.FromMinutes(1.0), TimeSpan.FromMinutes(2.0));

	private DispatcherTimer? _guard;

	private GifPlayer? _gif;

	private bool _manualPause;

	private bool _stopped;

	private int _coveredStreak;

	private volatile string _state = "animated=starting";

	private int _retries;

	public string State => _state;

	public AnimatedWallpaper(string path, WallpaperStyle style, bool muted, MetamorphServices services, bool manualPause)
	{
		_path = path;
		_style = style;
		_muted = muted;
		_services = services;
		_manualPause = manualPause;
		WallpaperThread.Post(Begin);
	}

	public void SetManualPause(bool value)
	{
		WallpaperThread.Post(delegate
		{
			_manualPause = value;
			UpdatePause();
		});
	}

	public void SetVolume(double volume)
	{
		WallpaperThread.Post(delegate
		{
			foreach (Placed item in _placed)
			{
				if (item.Content is VideoContent videoContent)
				{
					videoContent.SetVolume(volume);
				}
			}
		});
	}

	public void Stop()
	{
		WallpaperThread.Post(delegate
		{
			_stopped = true;
			StopCore();
			_state = "animated=stopped";
		});
	}

	private void Begin()
	{
		if (!_stopped)
		{
			if (_guard == null)
			{
				_guard = new DispatcherTimer(DispatcherPriority.Background)
				{
					Interval = TimeSpan.FromSeconds(1.5)
				};
			}
			_guard.Tick -= OnGuard;
			_guard.Tick += OnGuard;
			_guard.Start();
			StartCore();
		}
	}

	private void OnGuard(object? sender, EventArgs e)
	{
		Guard();
	}

	private void StartCore()
	{
		if (!DesktopSurfaces.EnsureLayer())
		{
			_state = "animated=no-workerw";
			Log.Warn("Не нашёл слой WorkerW за значками рабочего стола — GIF и видео на рабочем столе недоступны.");
			return;
		}
		WallpaperPrefs wallpaper = _services.Settings.Current.Wallpaper;
		Stretch stretch = StretchOf(_style);
		ImageSource poster = PosterMaker.Load(_services.Wallpapers.PosterPath(_path));
		IReadOnlyList<Display> readOnlyList = Display.All();
		bool flag = GifAnimation.IsGif(_path);
		if (flag)
		{
			_gif = new GifPlayer(_path, (readOnlyList.Count == 0) ? 1920 : readOnlyList.Max((Display d) => d.Bounds.Width));
		}
		foreach (Display item in readOnlyList)
		{
			try
			{
				DesktopSurface desktopSurface = DesktopSurfaces.Acquire(item);
				if (desktopSurface != null)
				{
					IAnimatedContent animatedContent2;
					if (!flag)
					{
						IAnimatedContent animatedContent = new VideoContent(_path, _muted || !item.Primary, wallpaper.Volume, stretch, poster);
						animatedContent2 = animatedContent;
					}
					else
					{
						IAnimatedContent animatedContent = _gif.CreateView(stretch, poster);
						animatedContent2 = animatedContent;
					}
					IAnimatedContent content = animatedContent2;
					desktopSurface.Show(content);
					_placed.Add(new Placed(item, desktopSurface, content));
				}
			}
			catch (Exception ex)
			{
				Log.Error("Анимированные обои", ex);
			}
		}
		if (_placed.Count == 0)
		{
			_state = "animated=no-surfaces";
			return;
		}
		EventTrace.Mark("wallpaper.animated.start", $"{Path.GetFileName(_path)} × {_placed.Count}");
		_guard?.Start();
		UpdatePause();
	}

	private void StopCore()
	{
		_guard?.Stop();
		foreach (Placed item in _placed)
		{
			DesktopSurfaces.Release(item.Surface);
		}
		_placed.Clear();
		_gif?.Dispose();
		_gif = null;
	}

	private static Stretch StretchOf(WallpaperStyle style)
	{
		return style switch
		{
			WallpaperStyle.Fit => Stretch.Uniform, 
			WallpaperStyle.Stretch => Stretch.Fill, 
			WallpaperStyle.Center => Stretch.None, 
			_ => Stretch.UniformToFill, 
		};
	}

	private void Guard()
	{
		try
		{
			if (_stopped)
			{
				return;
			}
			if (_placed.Count == 0)
			{
				if (++_retries > 20)
				{
					_guard?.Stop();
					_state = "animated=off (no desktop layer after explorer restart)";
					Log.Warn("Слой рабочего стола так и не появился — GIF/видео выключены до следующей смены обоев.");
				}
				else
				{
					StartCore();
				}
			}
			else if (!DesktopSurfaces.LayerAlive || _placed.Any((Placed p) => !p.Surface.IsAlive))
			{
				EventTrace.Mark("workerw.lost");
				StopCore();
				DesktopSurfaces.CloseUnused();
				if (!_restarts.Allow())
				{
					_state = "animated=off (workerw keeps disappearing)";
					Log.Warn("Слой рабочего стола пропадает слишком часто — анимированные обои выключены до следующей смены обоев.");
					return;
				}
				Log.Info("Слой рабочего стола пересоздан (перезапуск Проводника?) — поднимаю обои заново.");
				_retries = 0;
				_guard?.Start();
				StartCore();
			}
			else
			{
				UpdatePause();
			}
		}
		catch (Exception ex)
		{
			Log.Error("Анимированные обои: проверка", ex);
		}
	}

	private void UpdatePause()
	{
		if (_placed.Count == 0)
		{
			return;
		}
		WallpaperPrefs wallpaper = _services.Settings.Current.Wallpaper;
		bool flag = wallpaper.PauseOnBattery && OnBattery();
		Point? point = DesktopLayer.CoveredMonitor(wallpaper.PauseWhenCovered);
		_coveredStreak = (point.HasValue ? (_coveredStreak + 1) : 0);
		foreach (Placed item in _placed)
		{
			int num;
			if (point.HasValue)
			{
				Point valueOrDefault = point.GetValueOrDefault();
				if (item.Display.Contains(valueOrDefault.X, valueOrDefault.Y))
				{
					num = ((_coveredStreak >= 2) ? 1 : 0);
					goto IL_00ab;
				}
			}
			num = 0;
			goto IL_00ab;
			IL_00ab:
			bool flag2 = (byte)num != 0;
			bool flag3 = _manualPause || flag || flag2;
			if (item.Content.Paused != flag3)
			{
				item.Content.Paused = flag3;
			}
		}
		_gif?.Reschedule();
		_state = "animated file=" + Path.GetFileName(_path) + " surfaces=" + string.Join(",", _placed.Select((Placed p) => p.Display.Name + ":" + (p.Content.Paused ? "paused" : "playing"))) + (flag ? " battery" : "") + ((_restarts.Trips > 0) ? $" restarts-blocked={_restarts.Trips}" : "");
	}

	private static bool OnBattery()
	{
		try
		{
			return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
		}
		catch
		{
			return false;
		}
	}
}
