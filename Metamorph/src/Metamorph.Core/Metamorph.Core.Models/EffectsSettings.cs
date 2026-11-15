namespace Metamorph.Core.Models;

// Эффекты поверх обоев (под значками рабочего стола).
public sealed class EffectsSettings
{
	public DesktopVisualizerSettings Visualizer { get; set; } = new DesktopVisualizerSettings();

	public ParticlesSettings Particles { get; set; } = new ParticlesSettings();

	public ParallaxSettings Parallax { get; set; } = new ParallaxSettings();

	public TransitionSettings Transition { get; set; } = new TransitionSettings();

	public TimeTintSettings TimeTint { get; set; } = new TimeTintSettings();

	// Акцент карточек/рамок подстраивается под текущие обои.
	public bool AccentFromWallpaper { get; set; }

	// Кадров в секунду для эффектов (ограничение сверху).
	public int MaxFps { get; set; } = 30;
}

public enum VisualizerStyle
{
	Bars,
	Mirror,
	Wave,
	Ring
}

public enum VisualizerPlacement
{
	Bottom,
	Center,
	AroundClock
}

public sealed class DesktopVisualizerSettings
{
	public bool Enabled { get; set; }

	public VisualizerStyle Style { get; set; }

	public VisualizerPlacement Placement { get; set; }

	public int Bars { get; set; } = 64;

	public double Height { get; set; } = 140.0;

	public double Opacity { get; set; } = 0.55;

	// Градиент accent → accent2 вместо одного цвета.
	public bool Gradient { get; set; } = true;
}

public enum ParticleKind
{
	Snow,
	Sakura,
	Stars,
	Fireflies,
	Leaves,
	Rain,
	Bubbles
}

public sealed class ParticlesSettings
{
	public bool Enabled { get; set; }

	public ParticleKind Kind { get; set; }

	// 0.1..3 — множитель количества.
	public double Density { get; set; } = 1.0;

	public double Speed { get; set; } = 1.0;

	// Частицы отталкиваются от курсора.
	public bool MouseInteraction { get; set; } = true;
}

public sealed class ParallaxSettings
{
	public bool Enabled { get; set; }

	// Максимальный сдвиг в DIP.
	public double Strength { get; set; } = 18.0;
}

public enum WallpaperTransitionKind
{
	None,
	Fade,
	Slide,
	Zoom,
	Blur
}

public sealed class TransitionSettings
{
	public WallpaperTransitionKind Kind { get; set; } = WallpaperTransitionKind.Fade;

	public double Seconds { get; set; } = 0.8;
}

public sealed class TimeTintSettings
{
	public bool Enabled { get; set; }

	// 0..1 — сила тонировки (утро тёплое, ночь холодная и темнее).
	public double Strength { get; set; } = 0.25;
}
