using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Metamorph.App.Audio;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Colors;
using Metamorph.Core.Effects;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;
using Color = System.Windows.Media.Color;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace Metamorph.App.Desktop.Effects;

// Эффекты поверх обоев: визуализатор, частицы, параллакс, тонировка. API используется SessionController — сигнатуры не менять.
// Всё рисование — на потоке обоев (WallpaperThread) в тех же подложках WorkerW, что и GIF/видео: под значками, без ввода.
internal sealed class EffectsController : IDisposable
{
	private sealed class Screen
	{
		public required Display Display;

		public required DesktopSurface Surface;

		public required EffectsLayer Layer;

		public required double Scale;

		public required Size Size;

		public ParticleField? Field;

		public Spring ShiftX;

		public Spring ShiftY;

		public bool ParallaxAtRest = true;

		public double Overscan = 1.0;

		public bool Active = true;

		// Подложка показывает статичные обои, нарисованные нами (иначе там GIF/видео — только накладываем слой).
		public bool OwnsWallpaper;
	}

	private const int IdleCursorMs = 100;

	private const int IdleAudioMs = 250;

	private readonly ThemeProfile _profile;

	private readonly MetamorphServices _services;

	private readonly EffectsSettings _settings;

	private readonly Dispatcher _ui;

	// --- UI-поток ---
	private IDisposable? _lease;

	private bool _disposed;

	private bool _coreStarted;

	private int _accentVersion;

	private volatile bool _leaseHeld;

	// --- поток обоев ---
	private readonly List<Screen> _screens = new List<Screen>();

	private readonly Stopwatch _clock = new Stopwatch();

	private readonly float[] _raw = new float[AudioSpectrum.BandCount];

	private readonly SilenceGate _gate = new SilenceGate();

	private DispatcherTimer? _frame;

	private DispatcherTimer? _poll;

	private bool _stopped;

	private double _lastFrame;

	private EffectColors _colors;

	private ParticlePaint? _paint;

	private VisualizerPainter? _visualizer;

	private Screen? _visualizerScreen;

	private bool _wantLease;

	private WallpaperInfo? _wallpaperInfo;

	private int _loadVersion;

	private int _pollTicks;

	private int _rebuilds;

	private int _tintMinute = -1;

	private Win32.POINT _cursor;

	// Картинка, декодированная до создания подложек, — чтобы при старте не мелькал чёрный фон.
	private (WallpaperInfo Info, LoadedWallpaper Image, double Overscan)? _preloaded;

	private bool _cursorKnown;

	public EffectsController(ThemeProfile profile, MetamorphServices services)
	{
		_profile = profile;
		_services = services;
		_settings = profile.Desktop.Effects;
		_ui = Dispatcher.CurrentDispatcher;
		_colors = EffectColors.From(profile.Palette);
		if (_settings.AccentFromWallpaper && WallpaperAccent.Current is Color accent)
		{
			_colors = _colors.WithAccent(accent);
		}
	}

	public static bool IsNeeded(ThemeProfile profile)
	{
		EffectsSettings e = profile.Desktop.Effects;
		if (!e.Visualizer.Enabled && !e.Particles.Enabled && !e.Parallax.Enabled && !e.TimeTint.Enabled)
		{
			return e.AccentFromWallpaper;
		}
		return true;
	}

	// Нужна ли своя подложка (без неё — только акцент из обоев).
	private bool NeedsSurface
	{
		get
		{
			if (!_settings.Visualizer.Enabled && !_settings.Particles.Enabled && !ParallaxOn)
			{
				return _settings.TimeTint.Enabled;
			}
			return true;
		}
	}

	private bool ParallaxOn
	{
		get
		{
			if (_settings.Parallax.Enabled)
			{
				return _settings.Parallax.Strength > 0.5;
			}
			return false;
		}
	}

	private int ActiveIntervalMs => (int)Math.Round(1000.0 / Math.Clamp(_settings.MaxFps, 5, 60));

	public void Start()
	{
		DesktopSurfaces.Transition = SurfaceTransition.From(_settings.Transition);
		if (_settings.AccentFromWallpaper)
		{
			RefreshAccent();
		}
		else
		{
			WallpaperAccent.Reset();
		}
		if (NeedsSurface)
		{
			DesktopActivity.Changed += OnActivityChanged;
			WallpaperSession.ImageChanged += OnImageChanged;
			_coreStarted = true;
			if (WallpaperIsAnimated())
			{
				WallpaperThread.Post(StartCore);
			}
			else
			{
				PreloadThenStart();
			}
		}
		else if (_settings.AccentFromWallpaper)
		{
			WallpaperSession.ImageChanged += OnImageChanged;
		}
		EventTrace.Mark("effects.start", Describe());
	}

	// Обои сменились (новый файл/картинка) — для акцента из обоев и параллакса.
	public void OnWallpaperChanged()
	{
		if (_disposed)
		{
			return;
		}
		if (_settings.AccentFromWallpaper)
		{
			RefreshAccent();
		}
		if (_coreStarted)
		{
			WallpaperThread.Post(delegate
			{
				ReloadWallpaperCore(force: false);
			});
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		DesktopActivity.Changed -= OnActivityChanged;
		WallpaperSession.ImageChanged -= OnImageChanged;
		SetLease(want: false);
		if (_coreStarted)
		{
			WallpaperThread.Post(StopCore);
		}
	}

	private string Describe()
	{
		List<string> parts = new List<string>();
		if (_settings.Particles.Enabled)
		{
			parts.Add($"particles={_settings.Particles.Kind}");
		}
		if (_settings.Visualizer.Enabled)
		{
			parts.Add($"visualizer={_settings.Visualizer.Style}");
		}
		if (ParallaxOn)
		{
			parts.Add($"parallax={_settings.Parallax.Strength:0}");
		}
		if (_settings.TimeTint.Enabled)
		{
			parts.Add("tint");
		}
		if (_settings.AccentFromWallpaper)
		{
			parts.Add("accent");
		}
		parts.Add($"fps={_settings.MaxFps}");
		return string.Join(" ", parts);
	}

	private void OnActivityChanged()
	{
		if (!_disposed)
		{
			WallpaperThread.Post(PollActivity);
		}
	}

	private void OnImageChanged()
	{
		OnWallpaperChanged();
	}

	// ---------- Акцент из обоев (UI-поток + пул потоков) ----------

	private void RefreshAccent()
	{
		int version = ++_accentVersion;
		string path;
		try
		{
			path = AccentSourcePath();
		}
		catch (Exception ex)
		{
			Log.Error("Эффекты: файл обоев для акцента", ex);
			return;
		}
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return;
		}
		Task.Run(delegate
		{
			Rgba? picked = null;
			try
			{
				picked = WallpaperAccentPicker.Pick(WallpaperImage.SamplePixels(path));
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: акцент из обоев", ex);
			}
			if (picked is Rgba rgba)
			{
				Color color = Color.FromRgb(rgba.R, rgba.G, rgba.B);
				_ui.BeginInvoke(delegate
				{
					if (!_disposed && version == _accentVersion)
					{
						WallpaperAccent.Publish(color);
						if (_coreStarted)
						{
							WallpaperThread.Post(delegate
							{
								ApplyColorsCore(_colors.WithAccent(color));
							});
						}
					}
				});
			}
		});
	}

	private string? AccentSourcePath()
	{
		ThemeProfile effective = WallpaperChoices.Effective(_profile, _services.Settings.Current.Wallpaper);
		WallpaperSettings w = effective.Wallpaper;
		if (w.EffectiveKind == WallpaperKind.Video)
		{
			string media = _services.Profiles.ResolvePath(effective, w.Path);
			if (string.IsNullOrEmpty(media))
			{
				return null;
			}
			if (GifAnimation.IsGif(media))
			{
				return media;
			}
			return _services.Wallpapers.PosterPath(media);
		}
		return WallpaperService.GetCurrentWallpaper();
	}

	private bool WallpaperIsAnimated()
	{
		try
		{
			return WallpaperChoices.Effective(_profile, _services.Settings.Current.Wallpaper).Wallpaper.EffectiveKind == WallpaperKind.Video;
		}
		catch (Exception ex)
		{
			Log.Error("Эффекты: вид обоев", ex);
			return false;
		}
	}

	private void PreloadThenStart()
	{
		List<Rectangle> bounds = new List<Rectangle>();
		foreach (Display display in Display.All())
		{
			bounds.Add(display.Bounds);
		}
		Rectangle virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
		Task.Run(delegate
		{
			(WallpaperInfo, LoadedWallpaper, double)? preloaded = null;
			try
			{
				WallpaperInfo info = WallpaperInfo.Read();
				(int w, int h) = TargetPixels(info.Style, bounds, virtualScreen);
				double overscan = OverscanFor(w, h);
				preloaded = (info, WallpaperImage.Load(info, w, h, overscan), overscan);
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: предзагрузка обоев", ex);
			}
			WallpaperThread.Post(delegate
			{
				_preloaded = preloaded;
				StartCore();
			});
		});
	}

	private static (int W, int H) TargetPixels(WallpaperStyle style, IEnumerable<Rectangle> displays, Rectangle virtualScreen)
	{
		if (style == WallpaperStyle.Span)
		{
			return (Math.Max(1, virtualScreen.Width), Math.Max(1, virtualScreen.Height));
		}
		int w = 1;
		int h = 1;
		foreach (Rectangle r in displays)
		{
			w = Math.Max(w, r.Width);
			h = Math.Max(h, r.Height);
		}
		return (w, h);
	}

	private double OverscanFor(int w, int h)
	{
		if (!ParallaxOn)
		{
			return 1.0;
		}
		return Parallax.OverscanScale(_settings.Parallax.Strength, w, h);
	}

	// ---------- Аудио-аренда (UI-поток) ----------

	private void SetLease(bool want)
	{
		if (want && !_disposed)
		{
			if (_lease == null)
			{
				try
				{
					_lease = AudioSpectrum.Shared.Acquire();
					_leaseHeld = true;
				}
				catch (Exception ex)
				{
					Log.Error("Эффекты: захват звука", ex);
				}
				WallpaperThread.Post(EnsureFrame);
			}
		}
		else if (_lease != null)
		{
			_leaseHeld = false;
			try
			{
				_lease.Dispose();
			}
			catch (Exception ex2)
			{
				Log.Error("Эффекты: освобождение звука", ex2);
			}
			_lease = null;
		}
	}

	// ---------- Поток обоев ----------

	private void StartCore()
	{
		if (_stopped)
		{
			return;
		}
		if (!DesktopSurfaces.EnsureLayer())
		{
			Log.Warn("Эффекты: не нашёл слой WorkerW за значками — попробую позже.");
			EnsurePoll();
			return;
		}
		double scale = Display.Scale;
		IReadOnlyList<Display> displays = Display.All();
		int index = 0;
		foreach (Display display in displays)
		{
			try
			{
				DesktopSurface surface = DesktopSurfaces.Acquire(display);
				if (surface == null)
				{
					continue;
				}
				Size size = surface.SizeDip;
				EffectsLayer layer = new EffectsLayer(size);
				surface.SetOverlay(layer);
				Screen screen = new Screen
				{
					Display = display,
					Surface = surface,
					Layer = layer,
					Scale = scale,
					Size = size
				};
				if (_settings.Particles.Enabled)
				{
					ParticlesSettings ps = _settings.Particles;
					screen.Field = new ParticleField(1009 + 7919 * index);
					screen.Field.Configure(ps.Kind, (float)size.Width, (float)size.Height, ParticleField.CountFor(ps.Kind, size.Width, size.Height, ps.Density), ps.Speed);
				}
				if (ParallaxOn)
				{
					screen.Overscan = Parallax.OverscanScale(_settings.Parallax.Strength, size.Width, size.Height);
					screen.Surface.SetParallax(0.0, 0.0, screen.Overscan);
				}
				_screens.Add(screen);
				index++;
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: подложка монитора", ex);
			}
		}
		if (_screens.Count == 0)
		{
			EnsurePoll();
			return;
		}
		if (_settings.Visualizer.Enabled)
		{
			_visualizerScreen = _screens.Find((Screen s) => s.Display.Primary) ?? _screens[0];
			_visualizer = new VisualizerPainter(_settings.Visualizer);
		}
		ApplyColorsCore(_colors);
		_tintMinute = -1;
		UpdateTint();
		ReloadWallpaperCore(force: true);
		EnsurePoll();
		PollActivity();
		EventTrace.Mark("effects.surfaces", _screens.Count.ToString());
	}

	private void StopCore()
	{
		_stopped = true;
		_frame?.Stop();
		_poll?.Stop();
		ReleaseScreens();
		DesktopSurfaces.CloseUnused();
	}

	private void ReleaseScreens()
	{
		foreach (Screen screen in _screens)
		{
			try
			{
				screen.Surface.SetOverlay(null);
				screen.Surface.SetParallax(0.0, 0.0, 1.0);
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: снятие слоя", ex);
			}
			DesktopSurfaces.Release(screen.Surface);
		}
		_screens.Clear();
		_visualizerScreen = null;
	}

	private void ApplyColorsCore(EffectColors colors)
	{
		if (_stopped)
		{
			return;
		}
		_colors = colors;
		if (_settings.Particles.Enabled)
		{
			_paint = new ParticlePaint(_settings.Particles.Kind, colors);
		}
		if (_visualizer != null && _visualizerScreen != null)
		{
			Screen s = _visualizerScreen;
			Rectangle wa = s.Display.WorkArea;
			Rect workArea = new Rect((wa.X - s.Display.Bounds.X) / s.Scale, (wa.Y - s.Display.Bounds.Y) / s.Scale, wa.Width / s.Scale, wa.Height / s.Scale);
			_visualizer.Layout(s.Size, workArea, colors);
		}
		EnsureFrame();
	}

	private void UpdateTint()
	{
		if (!_settings.TimeTint.Enabled)
		{
			return;
		}
		DateTime now = DateTime.Now;
		int minute = now.Hour * 60 + now.Minute;
		if (minute == _tintMinute)
		{
			return;
		}
		_tintMinute = minute;
		Tint tint = TimeTint.For(now);
		double alpha = tint.Alpha * Math.Clamp(_settings.TimeTint.Strength, 0.0, 1.0);
		Color color = Color.FromRgb(tint.Color.R, tint.Color.G, tint.Color.B);
		foreach (Screen screen in _screens)
		{
			screen.Layer.SetTint(color, alpha);
		}
	}

	// Решает, кто рисует обои на каждой подложке, и при необходимости (пере)загружает картинку.
	private void ReloadWallpaperCore(bool force)
	{
		if (_stopped || _screens.Count == 0)
		{
			return;
		}
		bool any = false;
		foreach (Screen screen in _screens)
		{
			IAnimatedContent content = screen.Surface.Content;
			if (content != null && !(content is StaticWallpaperContent) && DesktopSurfaces.Users(screen.Surface) > 1)
			{
				// GIF/видео живы — мы только слой поверх.
				screen.OwnsWallpaper = false;
				continue;
			}
			if (!screen.OwnsWallpaper)
			{
				force = true;
			}
			screen.OwnsWallpaper = true;
			any = true;
		}
		if (!any)
		{
			_wallpaperInfo = null;
			return;
		}
		WallpaperInfo info = WallpaperInfo.Read();
		if (!force && info == _wallpaperInfo)
		{
			return;
		}
		_wallpaperInfo = info;
		int version = ++_loadVersion;
		Rectangle virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
		(int maxW, int maxH) = TargetPixels(info.Style, _screens.ConvertAll((Screen s) => s.Display.Bounds), virtualScreen);
		double overscan = OverscanFor(maxW, maxH);
		var preloaded = _preloaded;
		_preloaded = null;
		if (preloaded.HasValue && preloaded.Value.Info == info && preloaded.Value.Overscan == overscan)
		{
			ShowWallpaperCore(version, preloaded.Value.Image, info, virtualScreen);
			return;
		}
		Task.Run(delegate
		{
			LoadedWallpaper loaded;
			try
			{
				loaded = WallpaperImage.Load(info, maxW, maxH, overscan);
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: загрузка обоев", ex);
				loaded = new LoadedWallpaper(null, 0, 0);
			}
			WallpaperThread.Post(delegate
			{
				ShowWallpaperCore(version, loaded, info, virtualScreen);
			});
		});
	}

	private void ShowWallpaperCore(int version, LoadedWallpaper loaded, WallpaperInfo info, Rectangle virtualScreen)
	{
		if (_stopped || version != _loadVersion)
		{
			return;
		}
		foreach (Screen screen in _screens)
		{
			IAnimatedContent content = screen.Surface.Content;
			if (!screen.OwnsWallpaper || (content != null && !(content is StaticWallpaperContent) && DesktopSurfaces.Users(screen.Surface) > 1))
			{
				continue;
			}
			try
			{
				screen.Surface.Show(new StaticWallpaperContent(loaded, info, screen.Display.Bounds, virtualScreen, screen.Scale));
			}
			catch (Exception ex)
			{
				Log.Error("Эффекты: показ обоев", ex);
			}
		}
		EventTrace.Mark("effects.wallpaper", Path.GetFileName(info.Path ?? "(color)"));
	}

	private void EnsurePoll()
	{
		if (_stopped)
		{
			return;
		}
		if (_poll == null)
		{
			_poll = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(1.0)
			};
			_poll.Tick += delegate
			{
				OnPoll();
			};
		}
		_poll.Start();
	}

	private void OnPoll()
	{
		try
		{
			if (_stopped)
			{
				return;
			}
			_pollTicks++;
			if (_screens.Count == 0 || !DesktopSurfaces.LayerAlive || _screens.Exists((Screen s) => !s.Surface.IsAlive))
			{
				Rebuild();
				return;
			}
			PollActivity();
			UpdateTint();
			if (_pollTicks % 5 == 0)
			{
				// Обои могли смениться в обход Metamorph, а видео — уйти (тогда рисуем статичные сами).
				ReloadWallpaperCore(force: false);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Эффекты: проверка", ex);
		}
	}

	private void Rebuild()
	{
		if (++_rebuilds > 20)
		{
			_poll?.Stop();
			_frame?.Stop();
			Log.Warn("Эффекты: слой рабочего стола постоянно пропадает — эффекты выключены до следующего применения темы.");
			return;
		}
		EventTrace.Mark("effects.rebuild", _rebuilds.ToString());
		_frame?.Stop();
		ReleaseScreens();
		_wallpaperInfo = null;
		StartCore();
	}

	private void PollActivity()
	{
		if (_stopped || _screens.Count == 0)
		{
			return;
		}
		// Один опрос перекрытия на все мониторы (то же, что DesktopActivity.ShouldAnimate).
		bool can = DesktopActivity.CanAnimate;
		System.Drawing.Point? covered = can ? DesktopLayer.CoveredMonitor(maximized: true) : null;
		foreach (Screen screen in _screens)
		{
			bool active = can && !(covered.HasValue && screen.Display.Contains(covered.Value.X, covered.Value.Y));
			if (screen.Active != active)
			{
				screen.Active = active;
				EventTrace.Mark("effects.active", $"{screen.Display.Name}={active}");
			}
		}
		bool want = _visualizer != null && _visualizerScreen != null && _visualizerScreen.Active;
		if (want != _wantLease)
		{
			_wantLease = want;
			if (!want)
			{
				_gate.Reset();
				_visualizer?.Bands.Clear();
				_visualizerScreen?.Layer.ClearVisualizer();
			}
			_ui.BeginInvoke(delegate
			{
				SetLease(want);
			});
		}
		EnsureFrame();
	}

	private void EnsureFrame()
	{
		if (_stopped || !_screens.Exists((Screen s) => s.Active))
		{
			return;
		}
		if (_frame == null)
		{
			_frame = new DispatcherTimer(DispatcherPriority.Render)
			{
				Interval = TimeSpan.FromMilliseconds(ActiveIntervalMs)
			};
			_frame.Tick += delegate
			{
				OnFrame();
			};
		}
		if (!_frame.IsEnabled)
		{
			_clock.Restart();
			_lastFrame = 0.0;
			_frame.Interval = TimeSpan.FromMilliseconds(ActiveIntervalMs);
			_frame.Start();
		}
	}

	private void SetInterval(int ms)
	{
		TimeSpan interval = TimeSpan.FromMilliseconds(ms);
		if (_frame != null && _frame.Interval != interval)
		{
			_frame.Interval = interval;
		}
	}

	private void OnFrame()
	{
		try
		{
			if (_stopped)
			{
				_frame?.Stop();
				return;
			}
			double now = _clock.Elapsed.TotalSeconds;
			double dt = Math.Clamp(now - _lastFrame, 0.0, 0.1);
			_lastFrame = now;
			bool parallax = ParallaxOn;
			bool repel = _settings.Particles.Enabled && _settings.Particles.MouseInteraction;
			bool moved = false;
			if ((parallax || repel) && Win32.GetCursorPos(out var pt))
			{
				moved = !_cursorKnown || pt.X != _cursor.X || pt.Y != _cursor.Y;
				_cursor = pt;
				_cursorKnown = true;
			}
			bool busy = false;
			bool waitingCursor = false;
			bool waitingAudio = false;
			foreach (Screen screen in _screens)
			{
				if (!screen.Active)
				{
					continue;
				}
				double lx = (_cursor.X - screen.Display.Bounds.X) / screen.Scale;
				double ly = (_cursor.Y - screen.Display.Bounds.Y) / screen.Scale;
				if (screen.Field != null && _paint != null)
				{
					bool onScreen = _cursorKnown && screen.Display.Contains(_cursor.X, _cursor.Y);
					screen.Field.Step((float)dt, (float)lx, (float)ly, repel && onScreen);
					screen.Layer.DrawParticles(screen.Field, _paint);
					busy = true;
				}
				if (parallax && _cursorKnown)
				{
					(double tx, double ty) = Parallax.Target(lx, ly, screen.Size.Width, screen.Size.Height, _settings.Parallax.Strength);
					if (moved || !screen.ParallaxAtRest)
					{
						screen.ShiftX.Step(tx, Parallax.Omega, dt);
						screen.ShiftY.Step(ty, Parallax.Omega, dt);
						bool settled = screen.ShiftX.IsSettled(tx) && screen.ShiftY.IsSettled(ty);
						if (settled)
						{
							screen.ShiftX = new Spring
							{
								Value = tx
							};
							screen.ShiftY = new Spring
							{
								Value = ty
							};
						}
						screen.ParallaxAtRest = settled;
						screen.Surface.SetParallax(screen.ShiftX.Value, screen.ShiftY.Value, screen.Overscan);
						busy |= !settled;
					}
					waitingCursor = true;
				}
				if (screen == _visualizerScreen && _visualizer != null)
				{
					if (FrameVisualizer(screen, dt))
					{
						busy = true;
					}
					else
					{
						waitingAudio = _leaseHeld;
					}
				}
			}
			if (busy)
			{
				SetInterval(ActiveIntervalMs);
			}
			else if (waitingCursor || waitingAudio)
			{
				// Ничего не движется: только дешёвый опрос курсора (10 Гц) или уровня звука (4 Гц).
				SetInterval(waitingCursor ? IdleCursorMs : IdleAudioMs);
			}
			else
			{
				_frame?.Stop();
			}
		}
		catch (Exception ex)
		{
			_frame?.Stop();
			Log.Error("Эффекты: кадр", ex);
		}
	}

	// true — визуализатор виден и требует полной частоты кадров.
	private bool FrameVisualizer(Screen screen, double dt)
	{
		if (!_leaseHeld || _visualizer == null)
		{
			return false;
		}
		AudioSpectrum spectrum = AudioSpectrum.Shared;
		if (!spectrum.IsActive)
		{
			if (!_gate.Visible)
			{
				_gate.Update(0f, dt);
				screen.Layer.ClearVisualizer();
				return false;
			}
			Array.Clear(_raw);
		}
		else
		{
			spectrum.Read(_raw);
		}
		_visualizer.Bands.Update(_raw, dt);
		_gate.Update(_visualizer.Bands.Level, dt);
		if (_gate.Visible)
		{
			screen.Layer.DrawVisualizer(_visualizer, _gate.Fade);
			return true;
		}
		screen.Layer.ClearVisualizer();
		return false;
	}
}
