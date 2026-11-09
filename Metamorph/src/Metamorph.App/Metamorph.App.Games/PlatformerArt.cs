using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Games;
using Metamorph.Core.Mascot;

namespace Metamorph.App.Games;

// Цвета одного мира (луг, пещера, небо, замок), подмешанные к палитре темы. Всё заморожено.
internal sealed class PlatformerTheme
{
	public Brush Sky = null!;

	public Brush Far = null!;

	public Brush Mid = null!;

	public Brush MidDetail = null!;

	public Brush Cloud = null!;

	public Brush Glow = null!;

	public Brush Top = null!;

	public Brush TopLight = null!;

	public Brush TopDark = null!;

	public Brush Body = null!;

	public Brush BodyDark = null!;

	public Brush Speck = null!;

	public Brush Hard = null!;

	public Brush HardLight = null!;

	public Brush HardDark = null!;

	public Brush Brick = null!;

	public Brush BrickLight = null!;

	public Brush Mortar = null!;

	public Brush Plank = null!;

	public Brush PlankDark = null!;

	public Color Ink;

	public bool Dark;
}

// Пиксельная графика «Мета-платформера»: тайлы — прямоугольниками в сетке 16×16, персонажи — замороженной геометрией.
// Локальные координаты врагов и предметов: пиксели уровня, начало — середина нижнего края, y вниз.
internal sealed class PlatformerArt
{
	public const double FarPeriod = 640.0;

	public const double MidPeriod = 512.0;

	public const double CloudPeriod = 900.0;

	private static readonly Geometry SpikeGeo = RunnerArt.Group(RunnerArt.Poly((0, 16), (4, 3), (8, 16)), RunnerArt.Poly((8, 16), (12, 3), (16, 16)));

	private static readonly Geometry SpikeShine = RunnerArt.Group(RunnerArt.Poly((4, 3), (4.8, 13), (3.2, 13)), RunnerArt.Poly((12, 3), (12.8, 13), (11.2, 13)));

	private static readonly Geometry BlockStar = Parse(MascotRig.StarPath(8, 8.5, 5.2, 2.3));

	private static readonly Geometry SlimeBody = Parse("M-7,0 C-7.6,-4.5 -5,-11 0,-11 C5,-11 7.6,-4.5 7,0 Z");

	private static readonly Geometry SlimeShine = RunnerArt.Group(RunnerArt.Ellipse(-3.2, -8, 1.8, 1.1), RunnerArt.Ellipse(-5, -5.6, 0.7, 0.7));

	private static readonly Geometry SlimeEyes = RunnerArt.Group(RunnerArt.Ellipse(-2.4, -5.6, 1.0, 1.5), RunnerArt.Ellipse(2.4, -5.6, 1.0, 1.5));

	private static readonly Geometry SlimeMouth = Parse("M-1.2,-3.2 Q0,-2.2 1.2,-3.2 Q0,-2.6 -1.2,-3.2 Z");

	private static readonly Geometry SpikyBody = RunnerArt.Ellipse(0, -6.5, 6, 6);

	private static readonly Geometry SpikySpikes = Parse(MascotRig.StarPath(0, -6.5, 9.2, 5.2));

	private static readonly Geometry SpikyFace = RunnerArt.Group(RunnerArt.Ellipse(-2.2, -6.8, 1.1, 1.3), RunnerArt.Ellipse(2.2, -6.8, 1.1, 1.3), RunnerArt.Poly((-4, -9.6), (-0.8, -8.2), (-1, -7.6), (-4.1, -9)), RunnerArt.Poly((4, -9.6), (0.8, -8.2), (1, -7.6), (4.1, -9)));

	private static readonly Geometry BatBody = RunnerArt.Group(RunnerArt.Ellipse(0, -5, 4, 4.4), RunnerArt.Poly((-3, -8), (-2.6, -11.5), (-0.8, -8.8)), RunnerArt.Poly((3, -8), (2.6, -11.5), (0.8, -8.8)));

	private static readonly Geometry[] BatWings =
	{
		RunnerArt.Group(Parse("M-3,-6 C-6,-12 -10,-12 -12,-10 C-10,-9 -10,-7 -10.5,-6 C-8.5,-7 -7,-6 -6.5,-4 C-5.5,-5.5 -4.2,-5.5 -3,-4 Z"), Parse("M3,-6 C6,-12 10,-12 12,-10 C10,-9 10,-7 10.5,-6 C8.5,-7 7,-6 6.5,-4 C5.5,-5.5 4.2,-5.5 3,-4 Z")),
		RunnerArt.Group(Parse("M-3,-5 C-6,-1 -10,1 -12,0 C-10,-2 -10.5,-3.5 -10,-4.5 C-8,-3.5 -7,-4.5 -6.5,-6 C-5.5,-5 -4.2,-5.5 -3,-6.5 Z"), Parse("M3,-5 C6,-1 10,1 12,0 C10,-2 10.5,-3.5 10,-4.5 C8,-3.5 7,-4.5 6.5,-6 C5.5,-5 4.2,-5.5 3,-6.5 Z"))
	};

	private static readonly Geometry BatEyes = RunnerArt.Group(RunnerArt.Ellipse(-1.5, -5.6, 0.9, 1.0), RunnerArt.Ellipse(1.5, -5.6, 0.9, 1.0));

	private static readonly Geometry SnailBody = Parse("M-8,0 L6,0 C7.5,0 7.5,-2.6 6,-2.6 L-4,-2.6 L-6,-5 C-7,-6 -8.6,-5 -8.4,-3.6 Z");

	private static readonly Geometry SnailStalks = RunnerArt.Group(Parse("M-6.6,-4.6 L-7.4,-9 L-6.6,-9 L-5.9,-4.8 Z"), Parse("M-5.2,-4.4 L-4.4,-8.4 L-3.7,-8.2 L-4.6,-4.2 Z"), RunnerArt.Ellipse(-7, -9.2, 1.1, 1.1), RunnerArt.Ellipse(-4.1, -8.6, 1.1, 1.1));

	private static readonly Geometry ShellGeo = RunnerArt.Ellipse(0, -6.2, 6.4, 6.2);

	private static readonly Geometry ShellSpiral = Parse("M0,-6.2 C1.6,-6.2 1.6,-3.8 0,-3.8 C-2.6,-3.8 -2.6,-8.6 0,-8.6 C3.6,-8.6 3.6,-1.6 0,-1.6 C-4.4,-1.6 -4.6,-10.6 0,-10.6");

	private static readonly Geometry BossBody = Parse("M-16,0 C-17,-10 -11,-25 0,-25 C11,-25 17,-10 16,0 Z");

	private static readonly Geometry BossCrown = RunnerArt.Poly((-7, -23), (-8, -32), (-4, -27), (0, -34), (4, -27), (8, -32), (7, -23));

	private static readonly Geometry BossEyes = RunnerArt.Group(RunnerArt.Ellipse(-5.5, -13, 2.2, 2.8), RunnerArt.Ellipse(5.5, -13, 2.2, 2.8));

	private static readonly Geometry BossBrows = RunnerArt.Group(RunnerArt.Poly((-10, -18.5), (-2.5, -16), (-2.8, -14.8), (-10.2, -17.2)), RunnerArt.Poly((10, -18.5), (2.5, -16), (2.8, -14.8), (10.2, -17.2)));

	private static readonly Geometry BossMouth = Parse("M-5,-7 Q0,-4 5,-7 Q0,-5.6 -5,-7 Z");

	private static readonly Geometry BossShine = RunnerArt.Group(RunnerArt.Ellipse(-8, -18, 3, 1.8), RunnerArt.Ellipse(-11, -13, 1.1, 1.1));

	private static readonly Geometry StarGeo = Parse(MascotRig.StarPath(0, -6.5, 7, 3.2));

	private static readonly Geometry StarEyes = RunnerArt.Group(RunnerArt.Ellipse(-1.6, -6.6, 0.7, 1.1), RunnerArt.Ellipse(1.6, -6.6, 0.7, 1.1));

	private static readonly Geometry CandyHead = RunnerArt.Ellipse(0, -9.5, 5, 5);

	private static readonly Geometry CandySwirl = Parse("M0,-9.5 C1.4,-9.5 1.4,-7.4 0,-7.4 C-2.2,-7.4 -2.2,-11.6 0,-11.6 C3,-11.6 3,-5.6 0,-5.6 C-3.6,-5.6 -3.8,-13.4 0,-13.4");

	private static readonly Geometry CandyStick = Freeze(new RectangleGeometry(new Rect(-0.7, -4.6, 1.4, 4.6)));

	private static readonly Geometry FlagCloth = Parse("M0,0 L-15,4 L0,8 Z");

	private static readonly Geometry FlagCloth2 = Parse("M0,0 L-14,3 L-15,5 L0,8 Z");

	private static readonly Geometry FlagStar = Parse(MascotRig.StarPath(-6, 4, 2.4, 1.1));

	private static readonly Geometry GemGeo = RunnerArt.Poly((0, -26), (8, -16), (5, 0), (-5, 0), (-8, -16));

	private static readonly Geometry GemLight = RunnerArt.Poly((0, -24), (-6, -16), (-3, -3), (0, -16));

	private static readonly Geometry PennantGeo = RunnerArt.Poly((0, 0), (9, 3), (0, 6));

	private readonly PlatformerTheme[] _themes = new PlatformerTheme[4];

	private readonly Brush _gold;

	private readonly Brush _goldDark;

	private readonly Brush _goldLight;

	private readonly Brush _used;

	private readonly Brush _usedDark;

	private readonly Brush _metal;

	private readonly Brush _metalDark;

	private readonly Brush _metalLight;

	private readonly Brush _lava;

	private readonly Brush _lavaTop;

	private readonly Brush _lavaBright;

	private readonly Brush _lavaGlow;

	private readonly Brush _iron;

	private readonly Brush _ink;

	private readonly Brush _white;

	private readonly Brush _slime;

	private readonly Brush _slimeDark;

	private readonly Brush _spiky;

	private readonly Brush _spikyTips;

	private readonly Brush _bat;

	private readonly Brush _batWing;

	private readonly Brush _shell;

	private readonly Pen _shellLine;

	private readonly Brush _snail;

	private readonly Brush _boss;

	private readonly Brush _bossFlash;

	private readonly Brush _cheek;

	private readonly Brush _star;

	private readonly Pen _starEdge;

	private readonly Brush _candy;

	private readonly Pen _candySwirl;

	private readonly Brush _stick;

	private readonly Brush _fire;

	private readonly Brush _fireGlow;

	private readonly Brush _coin;

	private readonly Pen _coinRim;

	private readonly Brush _coinShine;

	private readonly Brush _pole;

	private readonly Brush _flag;

	private readonly Brush _gem;

	private readonly Brush _gemGlow;

	private readonly Brush _pennantOff;

	private readonly Brush _pennantOn;

	public PlatformerArt(GamePalette p)
	{
		_themes[0] = Meadow(p);
		_themes[1] = Cave(p);
		_themes[2] = Sky(p);
		_themes[3] = Castle(p);
		Color gold = GamePalette.Mix(Color.FromRgb(0xF7, 0xB7, 0x2E), p.Yellow, 0.3);
		_gold = GamePalette.Solid(gold);
		_goldDark = GamePalette.Solid(GamePalette.Darken(gold, 0.42));
		_goldLight = GamePalette.Solid(GamePalette.Lighten(gold, 0.55));
		_used = GamePalette.Solid(Color.FromRgb(0x9A, 0x74, 0x5A));
		_usedDark = GamePalette.Solid(Color.FromRgb(0x5E, 0x42, 0x32));
		_metal = GamePalette.Solid(Color.FromRgb(0xC9, 0xD1, 0xDE));
		_metalDark = GamePalette.Solid(Color.FromRgb(0x5A, 0x60, 0x70));
		_metalLight = GamePalette.Solid(Colors.White, 0.85);
		_lava = GamePalette.Vertical((Color.FromRgb(0xFF, 0x7A, 0x1E), 0.0), (Color.FromRgb(0xC4, 0x24, 0x18), 1.0));
		_lavaTop = GamePalette.Solid(Color.FromRgb(0xFF, 0xB8, 0x30));
		_lavaBright = GamePalette.Solid(Color.FromRgb(0xFF, 0xE6, 0x7A));
		_lavaGlow = GamePalette.Vertical((Color.FromArgb(0, 0xFF, 0x7A, 0x1E), 0.0), (Color.FromArgb(0x70, 0xFF, 0x7A, 0x1E), 1.0));
		_iron = GamePalette.Solid(Color.FromRgb(0x2B, 0x26, 0x30));
		_ink = GamePalette.Solid(Color.FromRgb(0x22, 0x1A, 0x2E));
		_white = GamePalette.Solid(Colors.White, 0.9);
		Color slime = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0x6C, 0xD8, 0x6A), p.Green, 0.5));
		_slime = GamePalette.Vertical((GamePalette.Lighten(slime, 0.2), 0.0), (slime, 0.6), (GamePalette.Darken(slime, 0.2), 1.0));
		_slimeDark = GamePalette.Solid(GamePalette.Darken(slime, 0.35));
		Color spiky = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0xF2, 0x6B, 0x3A), p.Red, 0.4));
		_spiky = GamePalette.Solid(spiky);
		_spikyTips = GamePalette.Solid(Color.FromRgb(0xFF, 0xF1, 0xD6));
		Color bat = GamePalette.Mix(Color.FromRgb(0x7A, 0x52, 0xC7), p.Accent, 0.35);
		_bat = GamePalette.Solid(bat);
		_batWing = GamePalette.Solid(GamePalette.Darken(bat, 0.25));
		Color shell = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0xF0, 0xA0, 0x40), p.Yellow, 0.35));
		_shell = GamePalette.Vertical((GamePalette.Lighten(shell, 0.2), 0.0), (GamePalette.Darken(shell, 0.15), 1.0));
		_shellLine = GamePalette.Pen(GamePalette.Darken(shell, 0.45), 1.1);
		_snail = GamePalette.Solid(Color.FromRgb(0xF6, 0xD9, 0xC4));
		Color boss = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0x8E, 0x6B, 0xF0), p.Accent, 0.5));
		_boss = GamePalette.Vertical((GamePalette.Lighten(boss, 0.18), 0.0), (GamePalette.Darken(boss, 0.2), 1.0));
		_bossFlash = GamePalette.Solid(Colors.White, 0.75);
		_cheek = GamePalette.Solid(Color.FromRgb(0xFF, 0x8F, 0xA6), 0.7);
		Color star = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0xFF, 0x9C, 0xCB), p.Accent, 0.25));
		_star = GamePalette.Vertical((GamePalette.Lighten(star, 0.35), 0.0), (star, 1.0));
		_starEdge = GamePalette.Pen(GamePalette.Darken(star, 0.3), 0.8);
		_candy = GamePalette.Solid(Color.FromRgb(0xFF, 0x5A, 0x4A));
		_candySwirl = GamePalette.Pen(Color.FromRgb(0xFF, 0xF4, 0xE0), 1.4);
		_stick = GamePalette.Solid(Color.FromRgb(0xF4, 0xEA, 0xDA));
		_fire = new RadialGradientBrush(Color.FromRgb(0xFF, 0xF4, 0xB0), Color.FromRgb(0xFF, 0x6A, 0x1E));
		_fire.Freeze();
		_fireGlow = new RadialGradientBrush(Color.FromArgb(0x90, 0xFF, 0x8A, 0x2A), Color.FromArgb(0, 0xFF, 0x8A, 0x2A));
		_fireGlow.Freeze();
		_coin = GamePalette.Vertical((GamePalette.Lighten(gold, 0.35), 0.0), (gold, 0.55), (GamePalette.Darken(gold, 0.15), 1.0));
		_coinRim = GamePalette.Pen(GamePalette.Darken(gold, 0.4), 1.2);
		_coinShine = GamePalette.Solid(Colors.White, 0.8);
		_pole = GamePalette.Solid(Color.FromRgb(0xE8, 0xEC, 0xF2));
		_flag = GamePalette.Solid(GamePalette.Vivid(p.Accent));
		Color gem = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0x6F, 0xE6, 0xFF), p.Accent2, 0.4));
		_gem = GamePalette.Vertical((GamePalette.Lighten(gem, 0.3), 0.0), (GamePalette.Darken(gem, 0.25), 1.0));
		_gemGlow = new RadialGradientBrush(GamePalette.WithAlpha(gem, 0.55), GamePalette.WithAlpha(gem, 0.0));
		_gemGlow.Freeze();
		_pennantOff = GamePalette.Solid(p.Muted);
		_pennantOn = GamePalette.Solid(GamePalette.Vivid(p.Green));
	}

	public PlatformerTheme Theme(PlatformerWorld world)
	{
		return _themes[(int)world & 3];
	}

	public Brush LavaGlow => _lavaGlow;

	public Brush Gold => _gold;

	public Brush FireGlow => _fireGlow;

	private static Color T(GamePalette p, byte r, byte g, byte b, double tint = 0.12)
	{
		return GamePalette.Mix(Color.FromRgb(r, g, b), p.Accent, tint);
	}

	private static PlatformerTheme Meadow(GamePalette p)
	{
		Color top = GamePalette.Mix(T(p, 0x5F, 0xCB, 0x5A, 0.05), p.Green, 0.35);
		Color dirt = T(p, 0xB5, 0x76, 0x4A, 0.06);
		Color brick = GamePalette.Mix(T(p, 0xD2, 0x6A, 0x40, 0.05), p.Red, 0.25);
		Color hard = T(p, 0xC9, 0x9A, 0x6A, 0.06);
		Color skyTop = GamePalette.Mix(Color.FromRgb(0x72, 0xC2, 0xF5), p.Accent2, 0.3);
		Color skyBottom = GamePalette.Mix(Color.FromRgb(0xE4, 0xF6, 0xFF), p.Yellow, 0.12);
		return Make(GamePalette.Vertical((skyTop, 0.0), (skyBottom, 0.85)), GamePalette.Mix(Color.FromRgb(0x9A, 0xD9, 0xB0), skyBottom, 0.3), GamePalette.Mix(Color.FromRgb(0x58, 0xBB, 0x6A), p.Green, 0.3), Color.FromRgb(0xFF, 0xF2, 0x9A), Colors.White, Color.FromArgb(0, 255, 255, 255), top, dirt, brick, hard, Color.FromRgb(0xE2, 0xB0, 0x78), false);
	}

	private static PlatformerTheme Cave(GamePalette p)
	{
		Color top = GamePalette.Mix(T(p, 0x4E, 0xC4, 0xB0, 0.05), p.Accent2, 0.3);
		Color body = T(p, 0x4C, 0x3D, 0x72, 0.15);
		Color brick = T(p, 0x7A, 0x60, 0xA8, 0.15);
		Color hard = T(p, 0x5E, 0x50, 0x86, 0.12);
		Color skyTop = GamePalette.Mix(Color.FromRgb(0x14, 0x0F, 0x24), p.Bg, 0.25);
		Color skyBottom = GamePalette.Mix(Color.FromRgb(0x2C, 0x21, 0x4C), p.Accent, 0.12);
		Color crystal = GamePalette.Vivid(GamePalette.Mix(Color.FromRgb(0x8E, 0xE8, 0xFF), p.Accent, 0.35));
		return Make(GamePalette.Vertical((skyTop, 0.0), (skyBottom, 1.0)), GamePalette.Mix(skyBottom, Colors.Black, 0.25), GamePalette.Mix(skyBottom, body, 0.6), crystal, GamePalette.WithAlpha(crystal, 0.5), GamePalette.WithAlpha(crystal, 0.45), top, body, brick, hard, Color.FromRgb(0x8C, 0x7A, 0xB8), true);
	}

	private static PlatformerTheme Sky(GamePalette p)
	{
		Color top = GamePalette.Mix(T(p, 0x86, 0xE3, 0x9C, 0.05), p.Green, 0.3);
		Color body = T(p, 0xB4, 0x92, 0xDE, 0.15);
		Color brick = GamePalette.Mix(T(p, 0xF0, 0x8C, 0xAE, 0.08), p.Red, 0.2);
		Color hard = T(p, 0xD8, 0xC6, 0xF2, 0.1);
		Color skyTop = GamePalette.Mix(Color.FromRgb(0x5C, 0xA8, 0xFF), p.Accent2, 0.35);
		Color skyBottom = GamePalette.Mix(Color.FromRgb(0xFF, 0xE4, 0xF2), p.Accent, 0.1);
		return Make(GamePalette.Vertical((skyTop, 0.0), (skyBottom, 1.0)), GamePalette.Mix(Color.FromRgb(0xC2, 0xD8, 0xFF), skyBottom, 0.35), GamePalette.Mix(Colors.White, skyBottom, 0.25), Color.FromRgb(0xFF, 0xF4, 0xB8), Colors.White, Color.FromArgb(0, 255, 255, 255), top, body, brick, hard, Colors.White, false);
	}

	private static PlatformerTheme Castle(GamePalette p)
	{
		Color top = T(p, 0x92, 0x88, 0x9C, 0.08);
		Color body = T(p, 0x5E, 0x55, 0x68, 0.1);
		Color brick = GamePalette.Mix(T(p, 0x93, 0x4E, 0x3C, 0.05), p.Red, 0.15);
		Color hard = T(p, 0x7A, 0x72, 0x84, 0.1);
		Color skyTop = Color.FromRgb(0x17, 0x0C, 0x12);
		Color skyBottom = GamePalette.Mix(Color.FromRgb(0x52, 0x1C, 0x16), p.Red, 0.12);
		return Make(GamePalette.Vertical((skyTop, 0.0), (skyBottom, 1.0)), Color.FromRgb(0x2E, 0x1C, 0x24), Color.FromRgb(0x3A, 0x26, 0x2E), Color.FromRgb(0xFF, 0x9A, 0x3A), Color.FromRgb(0x5A, 0x3A, 0x3A), Color.FromArgb(0x60, 0xFF, 0x7A, 0x1E), top, body, brick, hard, Color.FromRgb(0x6C, 0x5A, 0x4A), true);
	}

	private static PlatformerTheme Make(Brush sky, Color far, Color mid, Color detail, Color cloud, Color glow, Color top, Color body, Color brick, Color hard, Color plank, bool dark)
	{
		return new PlatformerTheme
		{
			Sky = sky,
			Far = GamePalette.Solid(far),
			Mid = GamePalette.Solid(mid),
			MidDetail = GamePalette.Solid(detail),
			Cloud = GamePalette.Solid(cloud, dark ? 0.5 : 0.85),
			Glow = new RadialGradientBrush(glow, GamePalette.WithAlpha(glow, 0.0)).AsFrozen(),
			Top = GamePalette.Solid(top),
			TopLight = GamePalette.Solid(GamePalette.Lighten(top, 0.35)),
			TopDark = GamePalette.Solid(GamePalette.Darken(top, 0.28)),
			Body = GamePalette.Solid(body),
			BodyDark = GamePalette.Solid(GamePalette.Darken(body, 0.3)),
			Speck = GamePalette.Solid(GamePalette.Mix(body, GamePalette.Lighten(body, 0.4), 0.5)),
			Hard = GamePalette.Solid(hard),
			HardLight = GamePalette.Solid(GamePalette.Lighten(hard, 0.35)),
			HardDark = GamePalette.Solid(GamePalette.Darken(hard, 0.4)),
			Brick = GamePalette.Solid(brick),
			BrickLight = GamePalette.Solid(GamePalette.Lighten(brick, 0.3)),
			Mortar = GamePalette.Solid(GamePalette.Darken(brick, 0.5)),
			Plank = GamePalette.Solid(plank),
			PlankDark = GamePalette.Solid(GamePalette.Darken(plank, 0.35)),
			Ink = GamePalette.Darken(body, 0.6),
			Dark = dark
		};
	}

	// ── Тайлы ─────────────────────────────────────────────────────────────

	private static void R(DrawingContext dc, Brush b, double x, double y, double u, double ax, double ay, double aw, double ah)
	{
		dc.DrawRectangle(b, null, new Rect(x + ax * u, y + ay * u, aw * u, ah * u));
	}

	// (x, y) — левый верхний угол клетки на экране, s — её размер в пикселях.
	public void Tile(DrawingContext dc, TransformPool pool, PlatformerTheme th, PlatformerWorld world, PlatformerTile t, double x, double y, double s, int col, int row, bool topOpen, bool lavaTop, double time)
	{
		double u = s / 16.0;
		switch (t)
		{
		case PlatformerTile.Ground:
		{
			dc.DrawRectangle(th.Body, null, new Rect(x, y, s, s));
			int v = (col * 7 + row * 13) & 3;
			R(dc, th.Speck, x, y, u, 2 + v * 2, 9, 2, 2);
			R(dc, th.Speck, x, y, u, 11 - v, 12 + (v & 1), 3, 1);
			R(dc, th.BodyDark, x, y, u, 9 + (v & 1) * 3, 6 + v, 2, 1);
			R(dc, th.BodyDark, x, y, u, 1 + v, 14, 2, 2);
			if (topOpen)
			{
				R(dc, th.Top, x, y, u, 0, 0, 16, 5);
				R(dc, th.TopLight, x, y, u, 0, 0, 16, 1);
				R(dc, th.Top, x, y, u, 2 + v, 5, 3, 2);
				R(dc, th.Top, x, y, u, 9 - v, 5, 2, 1);
				R(dc, th.Top, x, y, u, 12, 5, 3, 2);
				R(dc, th.TopDark, x, y, u, 2 + v, 7, 3, 1);
				R(dc, th.TopDark, x, y, u, 12, 7, 3, 1);
				R(dc, th.TopDark, x, y, u, 0, 5, 2, 1);
				R(dc, th.TopDark, x, y, u, 6 + v, 5, 3, 1);
				if (world == PlatformerWorld.Meadow && ((col * 5 + row) % 7) == 0)
				{
					R(dc, th.MidDetail, x, y, u, 6, -2, 2, 2);
					R(dc, th.TopDark, x, y, u, 6.5, 0, 1, 1);
				}
			}
			break;
		}
		case PlatformerTile.Hard:
		case PlatformerTile.FirePivot:
			if (world == PlatformerWorld.Castle || world == PlatformerWorld.Cave)
			{
				dc.DrawRectangle(th.Hard, null, new Rect(x, y, s, s));
				R(dc, th.HardDark, x, y, u, 0, 7, 16, 1);
				R(dc, th.HardDark, x, y, u, 0, 15, 16, 1);
				R(dc, th.HardDark, x, y, u, ((row & 1) == 0) ? 5 : 11, 0, 1, 7);
				R(dc, th.HardDark, x, y, u, ((row & 1) == 0) ? 12 : 3, 8, 1, 7);
				R(dc, th.HardLight, x, y, u, 0, 0, 16, 1);
				R(dc, th.HardLight, x, y, u, 0, 8, 16, 1);
			}
			else
			{
				dc.DrawRectangle(th.Hard, null, new Rect(x, y, s, s));
				R(dc, th.HardLight, x, y, u, 0, 0, 16, 2);
				R(dc, th.HardLight, x, y, u, 0, 0, 2, 16);
				R(dc, th.HardDark, x, y, u, 0, 14, 16, 2);
				R(dc, th.HardDark, x, y, u, 14, 0, 2, 16);
				R(dc, th.HardDark, x, y, u, 4, 4, 8, 1);
				R(dc, th.HardDark, x, y, u, 4, 4, 1, 8);
				R(dc, th.HardLight, x, y, u, 4, 11, 8, 1);
				R(dc, th.HardLight, x, y, u, 11, 4, 1, 8);
			}
			if (t == PlatformerTile.FirePivot)
			{
				dc.DrawEllipse(_fireGlow, null, new Point(x + s / 2.0, y + s / 2.0), s * 0.7, s * 0.7);
				R(dc, _lavaTop, x, y, u, 6, 6, 4, 4);
				R(dc, _lavaBright, x, y, u, 7, 7, 2, 2);
			}
			break;
		case PlatformerTile.Brick:
		case PlatformerTile.BrickCoins:
			dc.DrawRectangle(th.Brick, null, new Rect(x, y, s, s));
			R(dc, th.BrickLight, x, y, u, 0, 0, 16, 1);
			R(dc, th.Mortar, x, y, u, 0, 7, 16, 1);
			R(dc, th.Mortar, x, y, u, 0, 15, 16, 1);
			R(dc, th.Mortar, x, y, u, 7, 0, 1, 7);
			R(dc, th.Mortar, x, y, u, 15, 0, 1, 7);
			R(dc, th.Mortar, x, y, u, 3, 8, 1, 7);
			R(dc, th.Mortar, x, y, u, 11, 8, 1, 7);
			R(dc, th.BrickLight, x, y, u, 0, 8, 3, 1);
			R(dc, th.BrickLight, x, y, u, 4, 8, 7, 1);
			R(dc, th.BrickLight, x, y, u, 12, 8, 4, 1);
			break;
		case PlatformerTile.Question:
		case PlatformerTile.QuestionPower:
		{
			dc.DrawRectangle(_goldDark, null, new Rect(x, y, s, s));
			R(dc, _gold, x, y, u, 1, 1, 14, 14);
			R(dc, _goldLight, x, y, u, 1, 1, 14, 1);
			R(dc, _goldLight, x, y, u, 1, 1, 1, 14);
			R(dc, _goldDark, x, y, u, 2, 2, 1, 1);
			R(dc, _goldDark, x, y, u, 13, 2, 1, 1);
			R(dc, _goldDark, x, y, u, 2, 13, 1, 1);
			R(dc, _goldDark, x, y, u, 13, 13, 1, 1);
			double pulse = 0.5 + 0.5 * Math.Sin(time * 5.0 + col * 0.6);
			dc.PushTransform(pool.Next(u, x, y + u * (pulse > 0.8 ? -0.5 : 0.0)));
			dc.DrawGeometry(_goldDark, null, BlockStar);
			dc.Pop();
			if (pulse > 0.65)
			{
				dc.PushOpacity((pulse - 0.65) * 2.4);
				dc.PushTransform(pool.Next(u, x, y - u * 0.8));
				dc.DrawGeometry(_goldLight, null, BlockStar);
				dc.Pop();
				dc.Pop();
			}
			break;
		}
		case PlatformerTile.Used:
			dc.DrawRectangle(_usedDark, null, new Rect(x, y, s, s));
			R(dc, _used, x, y, u, 1, 1, 14, 14);
			R(dc, _usedDark, x, y, u, 3, 3, 1, 1);
			R(dc, _usedDark, x, y, u, 12, 3, 1, 1);
			R(dc, _usedDark, x, y, u, 3, 12, 1, 1);
			R(dc, _usedDark, x, y, u, 12, 12, 1, 1);
			break;
		case PlatformerTile.OneWay:
			Plank(dc, th, world, x, y, s, col);
			break;
		case PlatformerTile.Spikes:
			dc.PushTransform(pool.Next(u, x, y));
			dc.DrawGeometry(_metal, null, SpikeGeo);
			dc.DrawGeometry(_metalLight, null, SpikeShine);
			dc.Pop();
			R(dc, _metalDark, x, y, u, 0, 14, 16, 2);
			break;
		case PlatformerTile.Lava:
		{
			dc.DrawRectangle(_lava, null, new Rect(x, y, s, s));
			if (lavaTop)
			{
				for (int i = 0; i < 4; i++)
				{
					double wave = Math.Sin(time * 3.2 + (col * 4 + i) * 0.9);
					double hgt = 2.0 + wave * 1.5;
					R(dc, _lavaTop, x, y, u, i * 4, 0, 4, 4 + hgt);
					R(dc, _lavaBright, x, y, u, i * 4, 0, 4, 1 + Math.Max(0.0, wave));
				}
			}
			double bub = (time * 0.7 + col * 0.37 + row * 0.21) % 1.0;
			R(dc, _lavaBright, x, y, u, 3 + (col * 5 % 9), 13 - bub * 9, 2, 2);
			break;
		}
		case PlatformerTile.Door:
			dc.DrawRectangle(_iron, null, new Rect(x, y, s, s));
			for (int i = 0; i < 4; i++)
			{
				R(dc, _metalDark, x, y, u, 1 + i * 4, 0, 2, 16);
				R(dc, _metal, x, y, u, 1 + i * 4, 0, 1, 16);
			}
			R(dc, _metalDark, x, y, u, 0, 6, 16, 2);
			R(dc, _metal, x, y, u, 0, 6, 16, 1);
			break;
		}
	}

	public void Plank(DrawingContext dc, PlatformerTheme th, PlatformerWorld world, double x, double y, double s, int col)
	{
		double u = s / 16.0;
		if (world == PlatformerWorld.Sky)
		{
			R(dc, th.Plank, x, y, u, 0, 1, 16, 5);
			R(dc, th.Plank, x, y, u, 2 + (col & 1) * 3, 0, 6, 1);
			R(dc, th.PlankDark, x, y, u, 0, 5, 16, 1);
			R(dc, th.PlankDark, x, y, u, 3, 6, 4, 1);
			R(dc, th.PlankDark, x, y, u, 10, 6, 4, 1);
			return;
		}
		R(dc, th.Plank, x, y, u, 0, 0, 16, 5);
		R(dc, th.PlankDark, x, y, u, 0, 4, 16, 2);
		R(dc, th.PlankDark, x, y, u, 7, 0, 1, 4);
		R(dc, th.TopLight, x, y, u, 0, 0, 16, 1);
		R(dc, th.PlankDark, x, y, u, 2, 6, 2, 3);
		R(dc, th.PlankDark, x, y, u, 12, 6, 2, 3);
	}

	// ── Объекты мира ──────────────────────────────────────────────────────

	public void Coin(DrawingContext dc, double cx, double cy, double k, double phase)
	{
		double sx = Math.Max(0.18, Math.Abs(Math.Cos(phase)));
		double rx = 5.0 * k * sx;
		double ry = 6.5 * k;
		dc.DrawEllipse(_coin, _coinRim, new Point(cx, cy), rx, ry);
		if (sx > 0.45)
		{
			dc.DrawRectangle(_coinShine, null, new Rect(cx - rx * 0.35, cy - ry * 0.55, Math.Max(1.0, k * 1.1 * sx), ry * 0.7));
		}
	}

	public void Enemy(DrawingContext dc, TransformPool pool, in PlatformerEnemy e, double sx, double sy, double k, double time)
	{
		double flip = e.Vx > 0.0 ? -1.0 : 1.0;
		switch (e.Kind)
		{
		case PlatformerEnemyKind.Slime:
		{
			double squash = e.State == PlatformerEnemyState.Squished ? 0.35 : 1.0 + 0.08 * Math.Sin(time * 9.0 + e.Phase);
			bool knocked = e.State == PlatformerEnemyState.Knocked;
			dc.PushTransform(pool.Next(k * flip, 0.0, 0.0, k * (knocked ? -1.0 : 1.0) * squash, sx, knocked ? sy - e.H * k : sy));
			dc.DrawGeometry(_slime, null, SlimeBody);
			dc.DrawGeometry(_white, null, SlimeShine);
			if (e.State != PlatformerEnemyState.Squished)
			{
				dc.DrawGeometry(_ink, null, SlimeEyes);
				dc.DrawGeometry(_ink, null, SlimeMouth);
				dc.DrawEllipse(_cheek, null, new Point(-4.6, -3.6), 1.3, 0.8);
				dc.DrawEllipse(_cheek, null, new Point(4.6, -3.6), 1.3, 0.8);
			}
			dc.Pop();
			break;
		}
		case PlatformerEnemyKind.Spiky:
		{
			bool knocked = e.State == PlatformerEnemyState.Knocked;
			double bob = Math.Abs(Math.Sin(time * 8.0 + e.Phase)) * 0.8;
			dc.PushTransform(pool.Next(k * flip, 0.0, 0.0, k * (knocked ? -1.0 : 1.0), sx, knocked ? sy - e.H * k : sy - bob * k));
			dc.DrawGeometry(_spikyTips, null, SpikySpikes);
			dc.DrawGeometry(_spiky, null, SpikyBody);
			dc.DrawGeometry(_ink, null, SpikyFace);
			dc.Pop();
			break;
		}
		case PlatformerEnemyKind.Bat:
		{
			bool knocked = e.State == PlatformerEnemyState.Knocked;
			int frame = knocked ? 1 : ((int)(time * 8.0 + e.Phase) & 1);
			dc.PushTransform(pool.Next(k * flip, 0.0, 0.0, k * (knocked ? -1.0 : 1.0), sx, knocked ? sy - e.H * k : sy));
			dc.DrawGeometry(_batWing, null, BatWings[frame]);
			dc.DrawGeometry(_bat, null, BatBody);
			dc.DrawGeometry(_white, null, BatEyes);
			dc.Pop();
			break;
		}
		case PlatformerEnemyKind.Snail:
		{
			bool knocked = e.State == PlatformerEnemyState.Knocked;
			bool shell = e.State is PlatformerEnemyState.Shell or PlatformerEnemyState.ShellMoving || knocked;
			dc.PushTransform(pool.Next(k * flip, 0.0, 0.0, k * (knocked ? -1.0 : 1.0), sx, knocked ? sy - e.H * k : sy));
			if (!shell)
			{
				dc.DrawGeometry(_snail, null, SnailBody);
				dc.DrawGeometry(_snail, null, SnailStalks);
				dc.DrawEllipse(_ink, null, new Point(-7.0, -9.2), 0.55, 0.55);
				dc.DrawEllipse(_ink, null, new Point(-4.1, -8.6), 0.55, 0.55);
				dc.PushTransform(pool.Next(1.0, 1.6, -0.8));
			}
			else if (e.State == PlatformerEnemyState.ShellMoving)
			{
				double a = time * 14.0 * (e.Vx > 0 ? 1 : -1) * flip;
				double c = Math.Cos(a);
				double s = Math.Sin(a);
				dc.PushTransform(pool.Next(c, s, -s, c, -6.2 * s, -6.2 + 6.2 * c));
			}
			else
			{
				double wob = e.Timer < 2.0 ? Math.Sin(time * 30.0) * 0.8 : 0.0;
				dc.PushTransform(pool.Next(1.0, wob, 0.0));
			}
			dc.DrawGeometry(_shell, null, ShellGeo);
			dc.DrawGeometry(null, _shellLine, ShellSpiral);
			dc.Pop();
			dc.Pop();
			break;
		}
		case PlatformerEnemyKind.Boss:
		{
			bool knocked = e.State == PlatformerEnemyState.Knocked;
			double squash = e.OnGround ? 1.0 + 0.05 * Math.Sin(time * 7.0) : 1.08;
			double sxs = e.OnGround ? 1.0 : 0.94;
			dc.PushTransform(pool.Next(k * flip * sxs, 0.0, 0.0, k * squash * (knocked ? -1.0 : 1.0), sx, knocked ? sy - e.H * k : sy));
			dc.DrawGeometry(_boss, null, BossBody);
			dc.DrawGeometry(_white, null, BossShine);
			dc.DrawGeometry(_gold, null, BossCrown);
			dc.DrawGeometry(_ink, null, BossEyes);
			dc.DrawGeometry(_ink, null, BossBrows);
			dc.DrawGeometry(_ink, null, BossMouth);
			dc.DrawEllipse(_white, null, new Point(-6.2, -14), 0.8, 0.9);
			dc.DrawEllipse(_white, null, new Point(4.8, -14), 0.8, 0.9);
			if (e.Flash > 0.0 && ((int)(e.Flash * 16.0) & 1) == 0)
			{
				dc.DrawGeometry(_bossFlash, null, BossBody);
			}
			dc.Pop();
			for (int i = 0; i < 3; i++)
			{
				Brush pip = i < e.Hp ? _candy : _metalDark;
				dc.DrawEllipse(pip, null, new Point(sx + (i - 1) * 6.0 * k, sy - 40.0 * k), 2.0 * k, 2.0 * k);
			}
			break;
		}
		}
	}

	public void Item(DrawingContext dc, TransformPool pool, in PlatformerItem it, double sx, double sy, double k, double time)
	{
		if (it.Kind == PlatformerItemKind.SugarStar)
		{
			double spin = Math.Sin(time * 6.0) * 0.12;
			dc.DrawEllipse(_fireGlow, null, new Point(sx, sy - 6.5 * k), 10.0 * k, 10.0 * k);
			dc.PushTransform(pool.Next(k * Math.Cos(spin), k * Math.Sin(spin), -k * Math.Sin(spin), k * Math.Cos(spin), sx, sy));
			dc.DrawGeometry(_star, _starEdge, StarGeo);
			dc.DrawGeometry(_ink, null, StarEyes);
			dc.Pop();
			return;
		}
		double bob = Math.Sin(time * 4.0) * 0.8;
		dc.PushTransform(pool.Next(k, sx, sy + bob * k));
		dc.DrawGeometry(_stick, null, CandyStick);
		dc.DrawGeometry(_candy, null, CandyHead);
		dc.DrawGeometry(null, _candySwirl, CandySwirl);
		dc.Pop();
	}

	public void Candy(DrawingContext dc, TransformPool pool, double cx, double cy, double k, double angle)
	{
		double c = Math.Cos(angle) * k * 0.62;
		double s = Math.Sin(angle) * k * 0.62;
		// Поворот вокруг центра головки леденца (0, −9.5).
		dc.PushTransform(pool.Next(c, s, -s, c, cx - 9.5 * s, cy + 9.5 * c));
		dc.DrawGeometry(_candy, null, CandyHead);
		dc.DrawGeometry(null, _candySwirl, CandySwirl);
		dc.Pop();
	}

	public void FireBall(DrawingContext dc, double cx, double cy, double k)
	{
		dc.DrawEllipse(_fireGlow, null, new Point(cx, cy), 7.0 * k, 7.0 * k);
		dc.DrawEllipse(_fire, null, new Point(cx, cy), 3.6 * k, 3.6 * k);
	}

	public void Flag(DrawingContext dc, TransformPool pool, double x, double top, double bottom, double flagY, double k, double time)
	{
		double pw = Math.Max(2.0, Math.Round(2.0 * k));
		dc.DrawRectangle(_pole, null, new Rect(Math.Round(x - pw / 2.0), top, pw, bottom - top));
		dc.DrawEllipse(_gold, _coinRim, new Point(x, top), 3.0 * k, 3.0 * k);
		dc.PushTransform(pool.Next(k, x - pw / 2.0, flagY));
		dc.DrawGeometry(_flag, null, ((int)(time * 4.0) & 1) == 0 ? FlagCloth : FlagCloth2);
		dc.DrawGeometry(_white, null, FlagStar);
		dc.Pop();
		dc.DrawRectangle(_metalDark, null, new Rect(Math.Round(x - 4.0 * k), bottom - 3.0 * k, Math.Round(8.0 * k), 3.0 * k));
	}

	public void Gem(DrawingContext dc, TransformPool pool, double x, double bottom, double k, double time)
	{
		double bob = Math.Sin(time * 2.4) * 2.0;
		double g = 1.0 + 0.15 * Math.Sin(time * 3.1);
		dc.DrawEllipse(_gemGlow, null, new Point(x, bottom - 16.0 * k), 26.0 * k * g, 26.0 * k * g);
		dc.PushTransform(pool.Next(k, x, bottom - (4.0 + bob) * k));
		dc.DrawGeometry(_gem, _starEdge, GemGeo);
		dc.DrawGeometry(_white, null, GemLight);
		dc.Pop();
	}

	public void Checkpoint(DrawingContext dc, TransformPool pool, double x, double bottom, double k, bool reached, double time)
	{
		double h = 24.0 * k;
		double pw = Math.Max(1.5, Math.Round(1.5 * k));
		dc.DrawRectangle(_pole, null, new Rect(Math.Round(x), bottom - h, pw, h));
		double wave = reached ? Math.Sin(time * 6.0) * 0.12 : 0.0;
		dc.PushTransform(pool.Next(k, wave * k, 0.0, k * (1.0 - Math.Abs(wave)), x + pw, bottom - h + (reached ? 0.0 : 12.0 * k)));
		dc.DrawGeometry(reached ? _pennantOn : _pennantOff, null, PennantGeo);
		dc.Pop();
	}

	// ── Фоны ──────────────────────────────────────────────────────────────

	public static Geometry Hills(double period, int bumps, double minH, double maxH, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		double step = period / bumps;
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext c = g.Open())
		{
			c.BeginFigure(new Point(0.0, 400.0), isFilled: true, isClosed: true);
			double h0 = r.Range(minH, maxH);
			c.LineTo(new Point(0.0, -h0), isStroked: false, isSmoothJoin: true);
			double prev = h0;
			for (int i = 0; i < bumps; i++)
			{
				double next = i == bumps - 1 ? h0 : r.Range(minH, maxH);
				double x0 = i * step;
				double peak = Math.Max(prev, next) + r.Range(8.0, 22.0);
				c.BezierTo(new Point(x0 + step * 0.3, -peak), new Point(x0 + step * 0.7, -peak), new Point(x0 + step, -next), isStroked: false, isSmoothJoin: true);
				prev = next;
			}
			c.LineTo(new Point(period, 400.0), isStroked: false, isSmoothJoin: true);
		}
		g.Freeze();
		return g;
	}

	// Сталактиты сверху (y от 0 вниз) и сталагмиты снизу (до 0 вверх).
	public static Geometry Stalactites(double period, int count, double maxLen, bool fromTop, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		StreamGeometry g = new StreamGeometry();
		using (StreamGeometryContext c = g.Open())
		{
			double step = period / count;
			double sign = fromTop ? 1.0 : -1.0;
			double edge = fromTop ? -400.0 : 400.0;
			c.BeginFigure(new Point(0.0, edge), isFilled: true, isClosed: true);
			c.LineTo(new Point(0.0, 0.0), isStroked: false, isSmoothJoin: false);
			for (int i = 0; i < count; i++)
			{
				double x0 = i * step;
				double w = step * r.Range(0.35, 0.7);
				double mid = x0 + r.Range(w * 0.5, step - w * 0.5);
				double len = r.Range(maxLen * 0.3, maxLen);
				c.LineTo(new Point(mid - w / 2.0, sign * r.Range(0.0, 10.0)), isStroked: false, isSmoothJoin: false);
				c.LineTo(new Point(mid, sign * len), isStroked: false, isSmoothJoin: false);
				c.LineTo(new Point(mid + w / 2.0, sign * r.Range(0.0, 10.0)), isStroked: false, isSmoothJoin: false);
			}
			c.LineTo(new Point(period, 0.0), isStroked: false, isSmoothJoin: false);
			c.LineTo(new Point(period, edge), isStroked: false, isSmoothJoin: false);
		}
		g.Freeze();
		return g;
	}

	public static Geometry Crystals(double period, int count, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int i = 0; i < count; i++)
		{
			double x = (i + r.Range(0.1, 0.9)) * period / count;
			double s = r.Range(0.7, 1.5);
			for (int j = 0; j < 3; j++)
			{
				double dx = (j - 1) * 6.0 * s;
				double h = (j == 1 ? 22.0 : 13.0) * s;
				double lean = (j - 1) * 3.0 * s;
				g.Children.Add(Freeze(RunnerArt.Poly((x + dx - 3.0 * s, 0.0), (x + dx + lean, -h), (x + dx + 3.0 * s, 0.0))));
			}
		}
		g.Freeze();
		return g;
	}

	public static Geometry CastleWall(double period, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int row = 0; row < 16; row++)
		{
			double y = -row * 12.0;
			double off = (row & 1) == 0 ? 0.0 : 12.0;
			for (double x = -off; x < period; x += 24.0)
			{
				if (r.NextDouble() < 0.85)
				{
					g.Children.Add(new RectangleGeometry(new Rect(x + 1.0, y - 11.0, 22.0, 10.0)));
				}
			}
		}
		g.Freeze();
		return g;
	}

	public static Geometry Windows(double period, int count, double y, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int i = 0; i < count; i++)
		{
			double x = (i + r.Range(0.2, 0.8)) * period / count;
			StreamGeometry s = new StreamGeometry();
			using (StreamGeometryContext c = s.Open())
			{
				c.BeginFigure(new Point(x - 7.0, y), isFilled: true, isClosed: true);
				c.LineTo(new Point(x - 7.0, y - 18.0), isStroked: false, isSmoothJoin: false);
				c.ArcTo(new Point(x + 7.0, y - 18.0), new Size(7.0, 7.0), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: false, isSmoothJoin: false);
				c.LineTo(new Point(x + 7.0, y), isStroked: false, isSmoothJoin: false);
			}
			s.Freeze();
			g.Children.Add(s);
		}
		g.Freeze();
		return g;
	}

	public static Geometry Islands(double period, int count, int seed)
	{
		GameRandom r = new GameRandom((ulong)seed);
		GeometryGroup g = new GeometryGroup();
		for (int i = 0; i < count; i++)
		{
			double x = (i + r.Range(0.1, 0.6)) * period / count;
			double y = -r.Range(60.0, 150.0);
			double w = r.Range(40.0, 90.0);
			StreamGeometry s = new StreamGeometry();
			using (StreamGeometryContext c = s.Open())
			{
				c.BeginFigure(new Point(x, y), isFilled: true, isClosed: true);
				c.LineTo(new Point(x + w, y), isStroked: false, isSmoothJoin: true);
				c.BezierTo(new Point(x + w * 0.85, y + 18.0), new Point(x + w * 0.6, y + 34.0), new Point(x + w * 0.45, y + 40.0), isStroked: false, isSmoothJoin: true);
				c.BezierTo(new Point(x + w * 0.3, y + 26.0), new Point(x + w * 0.1, y + 14.0), new Point(x, y), isStroked: false, isSmoothJoin: true);
			}
			s.Freeze();
			g.Children.Add(s);
			g.Children.Add(new EllipseGeometry(new Point(x + w * 0.3, y - 7.0), 6.0, 8.0));
			g.Children.Add(new RectangleGeometry(new Rect(x + w * 0.3 - 1.0, y - 2.0, 2.0, 3.0)));
		}
		g.Freeze();
		return g;
	}

	public static Geometry Cloud()
	{
		return RunnerArt.Group(RunnerArt.Ellipse(0, 0, 30, 12), RunnerArt.Ellipse(-20, 4, 18, 9), RunnerArt.Ellipse(22, 3, 20, 10), RunnerArt.Ellipse(4, -9, 18, 12));
	}

	// Обложка карточки в хабе: кусочек луга с блоками, слаймиком и флагом.
	public static void DrawCard(DrawingContext dc, GamePalette pal, double w, double h)
	{
		PlatformerArt art = new PlatformerArt(pal);
		PlatformerTheme th = art.Theme(PlatformerWorld.Meadow);
		TransformPool pool = new TransformPool();
		dc.DrawRectangle(th.Sky, null, new Rect(0.0, 0.0, w, h));
		double s = 20.0;
		double gy = h - s * 2.0;
		dc.PushTransform(pool.Next(0.9, 0.0, gy + 6.0));
		dc.DrawGeometry(th.Far, null, Hills(FarPeriod, 4, 30.0, 60.0, 5));
		dc.Pop();
		dc.PushTransform(pool.Next(0.7, 210.0, 34.0));
		dc.DrawGeometry(th.Cloud, null, Cloud());
		dc.Pop();
		for (int c = 0; c * s < w; c++)
		{
			art.Tile(dc, pool, th, PlatformerWorld.Meadow, PlatformerTile.Ground, c * s, gy, s, c, 13, topOpen: true, lavaTop: false, 0.0);
			art.Tile(dc, pool, th, PlatformerWorld.Meadow, PlatformerTile.Ground, c * s, gy + s, s, c, 14, topOpen: false, lavaTop: false, 0.0);
		}
		art.Tile(dc, pool, th, PlatformerWorld.Meadow, PlatformerTile.Brick, 120.0, gy - s * 4.0, s, 0, 0, false, false, 0.0);
		art.Tile(dc, pool, th, PlatformerWorld.Meadow, PlatformerTile.Question, 140.0, gy - s * 4.0, s, 1, 0, false, false, 0.3);
		art.Tile(dc, pool, th, PlatformerWorld.Meadow, PlatformerTile.Brick, 160.0, gy - s * 4.0, s, 2, 0, false, false, 0.0);
		art.Coin(dc, 150.0, gy - s * 5.6, 1.25, 0.3);
		PlatformerEnemy slime = new PlatformerEnemy
		{
			Kind = PlatformerEnemyKind.Slime,
			Vx = -1.0
		};
		art.Enemy(dc, pool, in slime, 196.0, gy, 1.5, 0.0);
		art.Flag(dc, pool, 252.0, gy - s * 5.0, gy, gy - s * 4.6, 1.3, 0.0);
	}

	private static Geometry Parse(string data)
	{
		Geometry g = Geometry.Parse(data);
		g.Freeze();
		return g;
	}

	private static Geometry Freeze(Geometry g)
	{
		g.Freeze();
		return g;
	}
}

internal static class FreezableExtensions
{
	public static T AsFrozen<T>(this T f) where T : Freezable
	{
		f.Freeze();
		return f;
	}
}
