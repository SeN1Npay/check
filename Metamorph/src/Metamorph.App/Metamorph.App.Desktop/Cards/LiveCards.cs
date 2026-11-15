using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop.Cards;

internal sealed class WeatherCard : CardView
{
	private TextBlock _glyph = new TextBlock();

	private TextBlock _temp = new TextBlock();

	private TextBlock _desc = new TextBlock();

	private TextBlock _place = new TextBlock();

	private WeatherNow? _now;

	private DateTime _loadedAt;

	private bool _busy;

	private bool _failed;

	private bool _disposed;

	public WeatherCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		Grid root = new Grid { Margin = new Thickness(22.0, 14.0, 22.0, 14.0) };
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		_glyph = CardUi.Text("", Math.Min(64.0, height * 0.45), Theme.Accent, FontWeights.Normal, new FontFamily("Segoe UI Symbol, Segoe UI Emoji"));
		_glyph.VerticalAlignment = VerticalAlignment.Center;
		_glyph.Margin = new Thickness(0.0, 0.0, 18.0, 0.0);
		root.Children.Add(_glyph);
		StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		Grid.SetColumn(info, 1);
		_temp = CardUi.Text("", 40.0, Theme.Foreground, FontWeights.Light, Theme.Font);
		_temp.Margin = new Thickness(-2.0, -8.0, 0.0, -2.0);
		_desc = CardUi.Text("", 14.0, Theme.Foreground, FontWeights.Medium, Theme.Font);
		_place = CardUi.Text("", 12.0, Theme.Muted, FontWeights.Normal, Theme.Font);
		_place.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
		info.Children.Add(_temp);
		info.Children.Add(_desc);
		info.Children.Add(_place);
		root.Children.Add(info);
		Render();
		return root;
	}

	protected override void OnActiveChanged(bool active)
	{
		if (active)
		{
			MaybeRefresh();
		}
	}

	public override void Tick(long tick)
	{
		if (tick % 30 == 0)
		{
			MaybeRefresh();
		}
	}

	private void MaybeRefresh()
	{
		// Раз в 15 минут; после ошибки без данных — повтор через 2 минуты.
		TimeSpan every = (_now == null && _failed) ? TimeSpan.FromMinutes(2.0) : TimeSpan.FromMinutes(15.0);
		if (!_busy && !_disposed && DateTime.UtcNow - _loadedAt >= every)
		{
			_ = RefreshAsync();
		}
	}

	private async Task RefreshAsync()
	{
		_busy = true;
		try
		{
			WeatherNow? w = await Context.Services.Weather.GetAsync(Context.Profile.Bar.Weather);
			_failed = w == null;
			_now = w ?? _now;
		}
		catch (Exception ex)
		{
			_failed = true;
			Log.Error("Карточки: погода", ex);
		}
		finally
		{
			_loadedAt = DateTime.UtcNow;
			_busy = false;
		}
		if (!_disposed)
		{
			Render();
		}
	}

	private void Render()
	{
		WeatherNow? w = _now;
		if (w == null)
		{
			_glyph.Text = "☁";
			_glyph.Opacity = 0.45;
			_temp.Text = "—";
			_desc.Text = _failed ? Loc.T("Нет данных о погоде") : Loc.T("Загрузка…");
			_place.Text = !_failed ? "" : ((string.IsNullOrWhiteSpace(Context.Profile.Bar.Weather.City) && Context.Profile.Bar.Weather.Latitude == null) ? Loc.T("Укажите город: «Редактор» → «Панель (бар)» → «Погода»") : Loc.T("Проверьте подключение"));
			return;
		}
		_glyph.Text = w.Icon;
		_glyph.Opacity = 1.0;
		_temp.Text = w.TemperatureText;
		_desc.Text = ClockCard.Capitalize(w.Description);
		_place.Text = $"{w.Place} · {Loc.T("ветер")} {w.WindSpeed:0} {Loc.T("км/ч")}";
		_place.Opacity = _failed ? 0.6 : 1.0;
	}

	public override void Dispose()
	{
		_disposed = true;
		base.Dispose();
	}
}

internal sealed class MediaCard : CardView
{
	private readonly Border _art = new Border();

	private TextBlock _title = new TextBlock();

	private TextBlock _artist = new TextBlock();

	private readonly ScaleTransform _progress = new ScaleTransform(0.0, 1.0);

	private Border _play = new Border();

	private readonly Grid _full = new Grid();

	private readonly StackPanel _empty = new StackPanel();

	private MediaDetails? _media;

	private DateTime _fetchedAt;

	private string? _artKey;

	private bool _busy;

	private bool _disposed;

	public MediaCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		double artSize = Math.Max(64.0, height - 36.0);
		Grid root = new Grid { Margin = new Thickness(18.0) };
		_full.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		_full.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		_art.Width = artSize;
		_art.Height = artSize;
		_art.CornerRadius = new CornerRadius(Math.Min(14.0, Theme.Radius));
		_art.Background = Theme.AccentGradient;
		_art.Margin = new Thickness(0.0, 0.0, 16.0, 0.0);
		_art.Child = new TextBlock
		{
			Text = CardGlyphs.Music,
			FontFamily = Theme.Icons,
			FontSize = artSize * 0.36,
			Foreground = Theme.OnAccent,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_full.Children.Add(_art);
		StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		Grid.SetColumn(info, 1);
		_title = CardUi.Text("", 15.0, Theme.Foreground, FontWeights.SemiBold, Theme.Font);
		_artist = CardUi.Text("", 12.5, Theme.Muted, FontWeights.Normal, Theme.Font);
		_artist.Margin = new Thickness(0.0, 2.0, 0.0, 8.0);
		Border bar = new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Theme.AccentGradient,
			RenderTransform = _progress
		};
		Border track = new Border
		{
			Height = 4.0,
			CornerRadius = new CornerRadius(2.0),
			Background = Theme.Track,
			Child = bar,
			ClipToBounds = true,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		_play = CardUi.IconButton(Theme, CardGlyphs.Play, delegate
		{
			Command(MediaInfo.PlayPauseAsync);
		}, 36.0, primary: true);
		StackPanel controls = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Children =
			{
				CardUi.IconButton(Theme, CardGlyphs.Previous, delegate
				{
					Command(MediaInfo.PreviousAsync);
				}),
				new Border { Width = 10.0 },
				_play,
				new Border { Width = 10.0 },
				CardUi.IconButton(Theme, CardGlyphs.Next, delegate
				{
					Command(MediaInfo.NextAsync);
				})
			}
		};
		info.Children.Add(_title);
		info.Children.Add(_artist);
		info.Children.Add(track);
		info.Children.Add(controls);
		_full.Children.Add(info);
		_empty.VerticalAlignment = VerticalAlignment.Center;
		_empty.HorizontalAlignment = HorizontalAlignment.Center;
		_empty.Orientation = Orientation.Horizontal;
		_empty.Children.Add(new TextBlock
		{
			Text = CardGlyphs.Music,
			FontFamily = Theme.Icons,
			FontSize = 22.0,
			Foreground = Theme.Muted,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0)
		});
		_empty.Children.Add(CardUi.Text(Loc.T("Ничего не играет"), 15.0, Theme.Muted, FontWeights.Medium, Theme.Font));
		root.Children.Add(_full);
		root.Children.Add(_empty);
		Render();
		return root;
	}

	protected override void OnActiveChanged(bool active)
	{
		if (active)
		{
			_ = RefreshAsync();
		}
	}

	public override void Tick(long tick)
	{
		if (tick % 2 == 0)
		{
			_ = RefreshAsync();
		}
		else
		{
			UpdateProgress();
		}
	}

	private async void Command(Func<Task> action)
	{
		try
		{
			await action();
			await Task.Delay(300);
			await RefreshAsync();
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: управление медиа", ex);
		}
	}

	private async Task RefreshAsync()
	{
		if (_busy || _disposed)
		{
			return;
		}
		_busy = true;
		try
		{
			MediaDetails? media = await MediaInfo.GetDetailsAsync();
			_media = media;
			_fetchedAt = DateTime.UtcNow;
			if (media != null && media.Key != _artKey)
			{
				_artKey = media.Key;
				SetArt(Decode(await MediaInfo.GetThumbnailAsync()));
			}
		}
		catch (Exception ex)
		{
			Log.Error("Карточки: медиа", ex);
		}
		finally
		{
			_busy = false;
		}
		if (!_disposed)
		{
			Render();
		}
	}

	private void SetArt(ImageSource? image)
	{
		if (image == null)
		{
			_art.Background = Theme.AccentGradient;
			_art.Child.Visibility = Visibility.Visible;
			return;
		}
		ImageBrush brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
		brush.Freeze();
		_art.Background = brush;
		_art.Child.Visibility = Visibility.Collapsed;
	}

	private void Render()
	{
		MediaDetails? m = _media;
		_full.Visibility = m == null ? Visibility.Collapsed : Visibility.Visible;
		_empty.Visibility = m == null ? Visibility.Visible : Visibility.Collapsed;
		if (m == null)
		{
			return;
		}
		_title.Text = m.Title;
		_artist.Text = string.IsNullOrWhiteSpace(m.Album) || m.Album == m.Title ? m.Artist : m.Artist + " — " + m.Album;
		CardUi.SetGlyph(_play, m.Playing ? CardGlyphs.Pause : CardGlyphs.Play);
		UpdateProgress();
	}

	private void UpdateProgress()
	{
		MediaDetails? m = _media;
		if (m == null || m.Duration <= TimeSpan.Zero)
		{
			_progress.ScaleX = 0.0;
			return;
		}
		TimeSpan pos = m.Position + (m.Playing ? DateTime.UtcNow - _fetchedAt : TimeSpan.Zero);
		_progress.ScaleX = Math.Clamp(pos.TotalSeconds / m.Duration.TotalSeconds, 0.0, 1.0);
	}

	private static ImageSource? Decode(byte[]? bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			return null;
		}
		try
		{
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.StreamSource = new MemoryStream(bytes);
			image.DecodePixelWidth = 200;
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.EndInit();
			image.Freeze();
			return image;
		}
		catch
		{
			return null;
		}
	}

	public override void Dispose()
	{
		_disposed = true;
		base.Dispose();
	}
}

internal sealed class SystemCard : CardView
{
	private sealed class Gauge
	{
		public required CardRing Ring;

		public required TextBlock Value;

		public required FrameworkElement Root;
	}

	private Gauge? _cpu;

	private Gauge? _ram;

	private Gauge? _gpu;

	private TextBlock _footer = new TextBlock();

	private IDisposable? _lease;

	public SystemCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		double ring = Math.Clamp(height - 84.0, 56.0, 96.0);
		Grid root = new Grid { Margin = new Thickness(18.0, 14.0, 18.0, 12.0) };
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		UniformGrid row = new UniformGrid { Rows = 1 };
		_cpu = Make("CPU", ring, Theme.AccentGradient);
		_ram = Make("RAM", ring, Theme.Accent2);
		_gpu = Make("GPU", ring, Theme.Accent);
		row.Children.Add(_cpu.Root);
		row.Children.Add(_ram.Root);
		row.Children.Add(_gpu.Root);
		root.Children.Add(row);
		_footer = CardUi.Text("", 12.0, Theme.Muted, FontWeights.Normal, Theme.Font);
		_footer.HorizontalAlignment = HorizontalAlignment.Center;
		_footer.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
		Grid.SetRow(_footer, 1);
		root.Children.Add(_footer);
		Apply(SensorHub.Shared.Current, animate: false);
		return root;
	}

	private Gauge Make(string label, double size, Brush fill)
	{
		CardRing r = new CardRing(size, Math.Max(5.0, size * 0.09), Theme.Track, fill);
		TextBlock value = CardUi.Text("—", size * 0.22, Theme.Foreground, FontWeights.SemiBold, Theme.Font);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		value.VerticalAlignment = VerticalAlignment.Center;
		TextBlock caption = CardUi.Text(label, 11.0, Theme.Muted, FontWeights.SemiBold, Theme.Font);
		caption.HorizontalAlignment = HorizontalAlignment.Center;
		caption.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		StackPanel stack = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Children =
			{
				new Grid { Children = { r, value } },
				caption
			}
		};
		return new Gauge { Ring = r, Value = value, Root = stack };
	}

	protected override void OnActiveChanged(bool active)
	{
		if (active)
		{
			_lease ??= SensorHub.Shared.Acquire(gpu: true);
			SensorHub.Shared.Updated -= OnUpdated;
			SensorHub.Shared.Updated += OnUpdated;
			Apply(SensorHub.Shared.Current, animate: false);
		}
		else
		{
			SensorHub.Shared.Updated -= OnUpdated;
			_lease?.Dispose();
			_lease = null;
		}
	}

	private void OnUpdated(SensorSnapshot s)
	{
		if (Active)
		{
			Apply(s, animate: true);
		}
	}

	private void Apply(SensorSnapshot s, bool animate)
	{
		Set(_cpu, s.Cpu, animate);
		Set(_ram, s.RamLoad, animate);
		Set(_gpu, s.Gpu, animate);
		if (_ram != null && s.RamUsedGb.HasValue && s.RamTotalGb.HasValue)
		{
			_ram.Root.ToolTip = $"{s.RamUsedGb:0.0} / {s.RamTotalGb:0} GB";
		}
		string footer = "";
		if (s.Down.HasValue && s.Up.HasValue)
		{
			footer = $"↓ {SystemStats.Rate(s.Down.Value)}/s   ↑ {SystemStats.Rate(s.Up.Value)}/s";
		}
		if (s.BatteryPercent.HasValue)
		{
			footer += (footer.Length > 0 ? "   " : "") + (s.Charging ? "⚡ " : "") + Loc.T("заряд") + " " + s.BatteryPercent.Value.ToString(CultureInfo.InvariantCulture) + "%";
		}
		_footer.Text = footer;
	}

	private static void Set(Gauge? g, double? percent, bool animate)
	{
		if (g == null)
		{
			return;
		}
		if (!percent.HasValue)
		{
			g.Value.Text = "—";
			g.Ring.Animate(0.0, animate: false);
			return;
		}
		double p = Math.Clamp(percent.Value, 0.0, 100.0);
		g.Value.Text = p.ToString("0", CultureInfo.InvariantCulture) + "%";
		g.Ring.Animate(p / 100.0, animate);
	}

	public override void Dispose()
	{
		base.Dispose();
		SensorHub.Shared.Updated -= OnUpdated;
		_lease?.Dispose();
		_lease = null;
	}
}

internal sealed class PomodoroCard : CardView
{
	private CardRing? _ring;

	private TextBlock _time = new TextBlock();

	private TextBlock _phase = new TextBlock();

	private Border _play = new Border();

	private FocusTimer? _timer;

	private static FocusTimer? AppTimer => Application.Current is App app ? app.Focus : null;

	public PomodoroCard(CardContext context, CardSettings settings)
		: base(context, settings)
	{
	}

	public override FrameworkElement Build(double width, double height)
	{
		double size = Math.Clamp(Math.Min(width - 60.0, height - 96.0), 100.0, 220.0);
		StackPanel root = new StackPanel
		{
			Margin = new Thickness(16.0, 18.0, 16.0, 14.0),
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_ring = new CardRing(size, size * 0.065, Theme.Track, Theme.AccentGradient);
		_phase = CardUi.Text("", 11.5, Theme.Muted, FontWeights.SemiBold, Theme.Font);
		_phase.HorizontalAlignment = HorizontalAlignment.Center;
		_time = CardUi.Text("", size * 0.22, Theme.Foreground, FontWeights.SemiBold, Theme.Font);
		_time.HorizontalAlignment = HorizontalAlignment.Center;
		root.Children.Add(new Grid
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Children =
			{
				_ring,
				new StackPanel
				{
					VerticalAlignment = VerticalAlignment.Center,
					Children = { _phase, _time }
				}
			}
		});
		_play = CardUi.IconButton(Theme, CardGlyphs.Play, Toggle, 40.0, primary: true);
		root.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Children =
			{
				CardUi.IconButton(Theme, CardGlyphs.Reset, delegate
				{
					_timer?.Cancel();
				}, 34.0),
				new Border { Width = 14.0 },
				_play
			}
		});
		_timer = AppTimer;
		if (_timer != null)
		{
			_timer.Changed += OnChanged;
		}
		Render(animate: false);
		return root;
	}

	private void Toggle()
	{
		FocusTimer? t = _timer;
		if (t == null)
		{
			return;
		}
		if (!t.Running)
		{
			t.StartPomodoro();
		}
		else if (t.Paused)
		{
			t.Resume();
		}
		else
		{
			t.Pause();
		}
	}

	private void OnChanged()
	{
		Render(animate: Active);
	}

	protected override void OnActiveChanged(bool active)
	{
		if (active)
		{
			Render(animate: false);
		}
	}

	public override void Tick(long tick)
	{
		Render(animate: true);
	}

	private void Render(bool animate)
	{
		FocusTimer? t = _timer;
		if (_ring == null)
		{
			return;
		}
		if (t == null || !t.Running)
		{
			_phase.Text = Loc.T("Помодоро").ToUpper(Loc.Culture);
			_time.Text = FocusTimer.Format(t?.FocusLength ?? TimeSpan.FromMinutes(25.0));
			_ring.SetFill(Theme.AccentGradient);
			_ring.Animate(0.0, animate);
			CardUi.SetGlyph(_play, CardGlyphs.Play);
			return;
		}
		string phase = t.Phase switch
		{
			FocusPhase.Break => Loc.T("Перерыв"),
			FocusPhase.Timer => Loc.T("Таймер"),
			_ => Loc.T("Фокус")
		};
		if (t.Phase == FocusPhase.Focus && t.Round > 0)
		{
			phase += " · " + t.Round.ToString(CultureInfo.InvariantCulture);
		}
		_phase.Text = (t.Paused ? Loc.T("Пауза") : phase).ToUpper(Loc.Culture);
		_time.Text = FocusTimer.Format(t.Remaining);
		_ring.SetFill(t.Phase == FocusPhase.Break ? Theme.Green : Theme.AccentGradient);
		_ring.Animate(t.Progress, animate, 0.9);
		CardUi.SetGlyph(_play, t.Paused ? CardGlyphs.Play : CardGlyphs.Pause);
	}

	public override void Dispose()
	{
		if (_timer != null)
		{
			_timer.Changed -= OnChanged;
		}
		base.Dispose();
	}
}
