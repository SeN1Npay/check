using System;
using System.Collections.Generic;
using System.Globalization;

namespace Metamorph.Core.Mascot;

public static class MascotRig
{
	public const double Left = -95.0;

	public const double Right = 95.0;

	public const double Top = -200.0;

	public const double Bottom = 8.0;

	private const double Lw = 1.6;

	private static readonly string HairBack = "M -44 -100 C -47 -136 -26 -153 0 -153 C 26 -153 47 -136 44 -100 C 44 -88 42 -78 38 -70 C 26 -66 -26 -66 -38 -70 C -42 -78 -44 -88 -44 -100 Z";

	private static readonly string TailL = "M -36 -133 C -56 -138 -71 -121 -71 -100 C -71 -80 -62 -66 -66 -50 C -68 -42 -74 -36 -81 -31 C -68 -31 -58 -42 -56 -56 C -54 -70 -58 -86 -56 -100 C -55 -112 -48 -121 -35 -124 Z";

	private static readonly string TailShadeL = "M -50 -118 C -61 -107 -63 -90 -61 -76 C -60 -68 -61 -58 -64 -50 C -58 -60 -56 -72 -57 -86 C -58 -100 -56 -110 -50 -118 Z";

	private static readonly string Face = "M -37 -104 C -37 -128 -20 -138 0 -138 C 20 -138 37 -128 37 -104 C 37 -84 22 -68 0 -66 C -22 -68 -37 -84 -37 -104 Z";

	private static readonly string FaceShade = "M -35 -113 C -30 -105 -24 -106 -18 -110 C -14 -104 -8 -104 -2 -108 C 4 -104 10 -104 15 -108 C 20 -103 28 -104 35 -113 L 35 -119 L -35 -119 Z";

	private static readonly string Bangs = "M -41 -101 C -44 -132 -26 -151 0 -151 C 26 -151 44 -132 41 -101 C 39 -108 36 -114 31 -117 C 31 -112 29 -108 26 -105 C 25 -114 21 -120 15 -123 C 14 -116 11 -110 7 -106 C 6 -115 3 -121 -2 -124 C -4 -117 -7 -111 -12 -107 C -12 -115 -15 -120 -20 -123 C -22 -116 -26 -111 -31 -107 C -31 -111 -32 -114 -33 -116 C -37 -112 -40 -107 -41 -101 Z";

	private static readonly string LockL = "M -41 -112 C -46 -97 -45 -82 -41 -72 C -39 -67 -37 -64 -33 -61 C -34 -70 -33 -83 -31 -96 C -31 -104 -33 -110 -35 -115 Z";

	private static readonly string Shine1 = "M -27 -137 C -21 -143 -13 -146 -6 -146 C -12 -143 -18 -140 -24 -134 Z";

	private static readonly string Shine2 = "M 7 -146 C 15 -146 23 -143 28 -137 C 22 -139 15 -141 9 -142 Z";

	private static readonly string Ahoge = "M 0 -150 C -3 -160 4 -171 15 -171 C 9 -167 6 -162 6 -151 Z";

	private static readonly string BowLoopOuterL = "M -40 -129 C -49 -142 -60 -137 -56 -126 C -53 -119 -46 -124 -40 -129 Z";

	private static readonly string BowLoopInnerL = "M -40 -129 C -35 -141 -25 -139 -28 -129 C -30 -122 -36 -124 -40 -129 Z";

	private static readonly string BowTailsL = "M -41 -128 L -47 -114 L -43 -116 L -40 -111 L -38 -127 Z";

	private static readonly string Neck = "M -5 -71 L 5 -71 L 5 -59 L -5 -59 Z";

	private static readonly string Dress = "M -13 -64 C -17 -58 -19 -50 -20 -42 C -22 -34 -26 -28 -29 -22 C -12 -17 12 -17 29 -22 C 26 -28 22 -34 20 -42 C 19 -50 17 -58 13 -64 Z";

	private static readonly string DressFoldL = "M -7 -40 C -9 -32 -11 -26 -13 -20 L -9.5 -19.5 C -8 -26 -6 -33 -5 -40 Z";

	private static readonly string DressFoldR = "M 7 -40 C 9 -32 11 -26 13 -20 L 9.5 -19.5 C 8 -26 6 -33 5 -40 Z";

	private static readonly string Hem = "M -28.2 -24.6 C -12 -19.6 12 -19.6 28.2 -24.6 L 29 -22 C 12 -17 -12 -17 -29 -22 Z";

	private static readonly string Collar = "M -14 -64 C -16 -60 -18 -56 -19.5 -53 L -6 -50 L 0 -44 L 6 -50 L 19.5 -53 C 18 -56 16 -60 14 -64 L 0 -56 Z";

	private static readonly string CollarStripe = "M -17 -55.2 L -6 -52.6 L 0 -47.2 L 6 -52.6 L 17 -55.2";

	private static readonly string BowL = "M 0 -50 L -9 -55 L -8 -45 Z";

	private static readonly string BowR = "M 0 -50 L 9 -55 L 8 -45 Z";

	private static readonly string BowTails = "M -1.2 -49 L -4 -40 L 0 -42 L 4 -40 L 1.2 -49 Z";

	private static readonly string BowKnot = PathBuilder.Ellipse(0.0, -50.0, 2.6, 2.4);

	private static readonly string SleeveL = "M -12 -63.5 C -18.5 -62.5 -22.5 -56 -24 -48 L -26.5 -41 L -17.5 -39 L -16 -46 C -15 -52 -13 -57 -9.5 -60.5 Z";

	private static readonly string CuffL = "M -27 -42 L -17 -39.6 L -18 -36.6 L -27.6 -38.8 Z";

	private static readonly string HandL = PathBuilder.Ellipse(-23.0, -33.5, 4.3, 4.7);

	private static readonly string Star = StarPath(0.0, 0.0, 5.5, 2.4);

	private static readonly string TailR = Mirror(TailL);

	private static readonly string TailShadeR = Mirror(TailShadeL);

	private static readonly string LockR = Mirror(LockL);

	private static readonly string BowLoopOuterR = Mirror(BowLoopOuterL);

	private static readonly string BowLoopInnerR = Mirror(BowLoopInnerL);

	private static readonly string BowTailsR = Mirror(BowTailsL);

	private static readonly string SleeveR = Mirror(SleeveL);

	private static readonly string CuffR = Mirror(CuffL);

	private static readonly string HandR = Mirror(HandL);

	private static string Mirror(string path)
	{
		string[] array = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		List<string> list = new List<string>(array.Length);
		int num = 0;
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.Length == 1 && char.IsLetter(text[0]))
			{
				list.Add(text);
				num = 0;
				continue;
			}
			if (num % 2 == 0 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				list.Add((0.0 - result).ToString(CultureInfo.InvariantCulture));
			}
			else
			{
				list.Add(text);
			}
			num++;
		}
		return string.Join(' ', list);
	}

	public static string StarPath(double cx, double cy, double r, double inner)
	{
		PathBuilder pathBuilder = new PathBuilder();
		for (int i = 0; i < 10; i++)
		{
			double num = -Math.PI / 2.0 + (double)i * Math.PI / 5.0;
			double num2 = ((i % 2 == 0) ? r : inner);
			double num3 = cx + Math.Cos(num) * num2;
			double y = cy + Math.Sin(num) * num2;
			double x = num3;
			if (i == 0)
			{
				pathBuilder.M(x, y);
			}
			else
			{
				pathBuilder.L(x, y);
			}
		}
		return pathBuilder.Z().ToString();
	}

	public static List<DrawItem> Render(MascotPose p, MascotColors c, Affine view)
	{
		MascotColors c2 = c;
		MascotPose p2 = p;
		List<DrawItem> items = new List<DrawItem>(90);
		double sitDrop = 11.0 * Math.Clamp(p2.Sit, 0.0, 1.0);
		Affine root = Affine.About(0.0, 0.0, p2.Rotation, p2.ScaleX, p2.ScaleY, p2.X, p2.Y);
		Affine affine = Affine.About(0.0, -26.0, p2.Body, 1.0, p2.Breath, 0.0, sitDrop).Then(root);
		Affine head = Affine.About(0.0, -64.0, p2.Head, 1.0, 1.0, 0.0, p2.HeadY).Then(affine);
		double turn = Math.Clamp(p2.Turn, -1.0, 1.0);
		Affine.Translate(turn * 5.0, 0.0).Then(head);
		Affine t2 = Affine.About(0.0, -150.0, p2.Bangs, 1.0, 1.0, turn * 2.0).Then(head);
		Affine t3 = Affine.About(-38.0, -128.0, p2.TailL).Then(head);
		Affine t4 = Affine.About(38.0, -128.0, p2.TailR).Then(head);
		Affine armL = Affine.About(-13.0, -60.0, p2.ArmL).Then(affine);
		Affine armR = Affine.About(13.0, -60.0, 0.0 - p2.ArmR).Then(affine);
		Affine t5 = Affine.About(0.0, -50.0, p2.Skirt * 0.6).Then(affine);
		Add(TailL, t3, c2.Hair, c2.Line, 1.6);
		Add(TailShadeL, t3, c2.HairShade, null, 0.0, 0.8);
		Add(TailR, t4, c2.Hair, c2.Line, 1.6);
		Add(TailShadeR, t4, c2.HairShade, null, 0.0, 0.8);
		Add(HairBack, head, c2.HairShade, c2.Line, 1.6);
		Leg(-7.0, p2.LiftL);
		Leg(7.0, p2.LiftR);
		Add(Neck, affine, c2.SkinShade);
		Add(Dress, t5, c2.Outfit, c2.Line, 1.6);
		Add(DressFoldL, t5, c2.OutfitShade, null, 0.0, 0.9);
		Add(DressFoldR, t5, c2.OutfitShade, null, 0.0, 0.9);
		Add(Hem, t5, c2.White);
		Add(Collar, affine, c2.White, c2.Line, 1.2);
		Add(CollarStripe, affine, null, c2.Ribbon, 1.2);
		int num;
		if (!(p2.ArmL > 95.0))
		{
			num = ((p2.ArmR > 95.0) ? 1 : 0);
			if (num == 0)
			{
				Arms();
			}
		}
		else
		{
			num = 1;
		}
		Add(BowL, affine, c2.Ribbon, c2.Line, 1.0);
		Add(BowR, affine, c2.Ribbon, c2.Line, 1.0);
		Add(BowTails, affine, c2.Ribbon, c2.Line, 0.8);
		Add(BowKnot, affine, c2.Ribbon, c2.Line, 0.8);
		Add(Face, head, c2.Skin, c2.Line, 1.6);
		Add(FaceShade, t2, c2.SkinShade, null, 0.0, 0.6);
		double num2 = Math.Clamp(p2.Blush, 0.0, 1.0);
		Add(PathBuilder.Ellipse(-23.0 + turn * 4.0, -85.0, 6.5, 3.2), head, c2.Blush, null, 0.0, num2 * 0.65);
		Add(PathBuilder.Ellipse(23.0 + turn * 4.0, -85.0, 6.5, 3.2), head, c2.Blush, null, 0.0, num2 * 0.65);
		Eye(-1);
		Eye(1);
		Mouth();
		Add(LockL, t2, c2.Hair, c2.Line, 1.6);
		Add(LockR, t2, c2.Hair, c2.Line, 1.6);
		Add(Bangs, t2, c2.Hair, c2.Line, 1.6);
		Add(Shine1, t2, c2.HairLight, null, 0.0, 0.9);
		Add(Shine2, t2, c2.HairLight, null, 0.0, 0.9);
		Affine t6 = Affine.About(3.0, -150.0, p2.Ahoge, 1.0, 1.0, turn * 2.0).Then(head);
		Add(Ahoge, t6, c2.Hair, c2.Line, 1.3);
		Add(Star, Affine.Rotate(12.0).Then(Affine.Translate(24.0 + turn * 2.0, -133.0)).Then(head), "#FFD166", c2.Line, 1.0);
		Add(BowLoopOuterL, head, c2.Ribbon, c2.Line, 1.1);
		Add(BowLoopInnerL, head, c2.Ribbon, c2.Line, 1.1);
		Add(BowTailsL, head, c2.Ribbon, c2.Line, 0.9);
		Add(PathBuilder.Ellipse(-40.0, -129.0, 3.2, 3.0), head, c2.Ribbon, c2.Line, 1.0);
		Add(BowLoopOuterR, head, c2.Ribbon, c2.Line, 1.1);
		Add(BowLoopInnerR, head, c2.Ribbon, c2.Line, 1.1);
		Add(BowTailsR, head, c2.Ribbon, c2.Line, 0.9);
		Add(PathBuilder.Ellipse(40.0, -129.0, 3.2, 3.0), head, c2.Ribbon, c2.Line, 1.0);
		if (num != 0)
		{
			Arms();
		}
		foreach (MascotFx item in p2.Fx)
		{
			Effect(item);
		}
		return items;
		void Add(string path, Affine t, string? fill, string? stroke = null, double sw = 0.0, double opacity = 1.0)
		{
			if (!(opacity <= 0.01))
			{
				items.Add(new DrawItem(path, t.Then(view), fill, stroke, sw, opacity));
			}
		}
		void Arms()
		{
			Add(HandL, armL, c2.Skin, c2.Line, 1.2);
			Add(SleeveL, armL, c2.Outfit, c2.Line, 1.6);
			Add(CuffL, armL, c2.White, c2.Line, 1.0);
			Add(HandR, armR, c2.Skin, c2.Line, 1.2);
			Add(SleeveR, armR, c2.Outfit, c2.Line, 1.6);
			Add(CuffR, armR, c2.White, c2.Line, 1.0);
		}
		void Effect(MascotFx fx)
		{
			Affine affine2 = Affine.Scale(fx.Scale, fx.Scale).Then(Affine.Rotate(fx.Rotation)).Then(Affine.Translate(fx.X, fx.Y))
				.Then(root);
			double num3 = Math.Clamp(fx.Alpha, 0.0, 1.0);
			switch (fx.Kind)
			{
			case FxKind.Note:
				Add(PathBuilder.Ellipse(-2.4, 4.0, 3.4, 2.5), Affine.Rotate(-20.0).Then(affine2), c2.Ribbon, c2.Line, 1.0, num3);
				Add("M 0.6 3.4 L 0.6 -9 Q 6.5 -7 6 -1.5", affine2, null, c2.Ribbon, 1.8, num3);
				break;
			case FxKind.Z:
				Add("M -4 -4.5 L 4.5 -4.5 L -4 4.5 L 4.5 4.5", affine2, null, c2.Eyes, 2.2, num3);
				break;
			case FxKind.Heart:
				Add("M 0 4.5 C -6.5 -0.5 -7.5 -5 -4.5 -7 C -2.2 -8.4 0 -6.8 0 -5.2 C 0 -6.8 2.2 -8.4 4.5 -7 C 7.5 -5 6.5 -0.5 0 4.5 Z", affine2, "#FF5C8A", c2.Line, 1.0, num3);
				break;
			case FxKind.Sweat:
				Add("M 0 -5.5 C 2 -2.5 4 0.5 4 3 C 4 5.6 2.2 7.2 0 7.2 C -2.2 7.2 -4 5.6 -4 3 C -4 0.5 -2 -2.5 0 -5.5 Z", affine2, "#9AD7FF", c2.Line, 1.0, num3);
				break;
			case FxKind.Sparkle:
				Add("M 0 -6.5 Q 1.2 -1.2 6.5 0 Q 1.2 1.2 0 6.5 Q -1.2 1.2 -6.5 0 Q -1.2 -1.2 0 -6.5 Z", affine2, "#FFE680", c2.Line, 0.8, num3);
				break;
			case FxKind.Question:
				Add("M -3.4 -4.2 C -3.4 -9 4.2 -9 4.2 -4.2 C 4.2 -0.8 0.2 -1 0.2 2.6", affine2, null, c2.Eyes, 2.4, num3);
				Add(PathBuilder.Ellipse(0.2, 6.4, 1.4, 1.4), affine2, c2.Eyes, null, 0.0, num3);
				break;
			case FxKind.Dots:
			{
				for (int i = 0; i < 3; i++)
				{
					Add(PathBuilder.Ellipse(-6 + i * 6, 0.0, 1.8, 1.8), affine2, c2.Line, null, 0.0, num3 * (0.4 + 0.2 * (double)i));
				}
				break;
			}
			case FxKind.Star:
				Add(Star, affine2, "#FFD166", c2.Line, 1.0, num3);
				break;
			}
		}
		void Eye(int side)
		{
			double num12 = (double)(side * 15) + turn * 5.0;
			EyeStyle eyeStyle = p2.Eyes;
			if (eyeStyle == EyeStyle.Wink && side == 1)
			{
				eyeStyle = EyeStyle.Happy;
			}
			else if (eyeStyle == EyeStyle.Wink)
			{
				eyeStyle = EyeStyle.Normal;
			}
			double num13 = Math.Clamp(p2.EyeOpen, 0.0, 1.0);
			switch (eyeStyle)
			{
			case EyeStyle.Happy:
				Add(new PathBuilder().M(num12 - 9.0, -93.0).Q(num12, -106.0, num12 + 9.0, -93.0).ToString(), head, null, c2.Line, 2.8);
				break;
			case EyeStyle.Closed:
				Add(new PathBuilder().M(num12 - 9.0, -96.0).Q(num12, -89.5, num12 + 9.0, -96.0).ToString(), head, null, c2.Line, 2.4);
				Add(new PathBuilder().M(num12 + (double)(side * 9), -96.0).L(num12 + (double)side * 11.5, -98.0).ToString(), head, null, c2.Line, 1.6);
				break;
			case EyeStyle.Dizzy:
				Add(PathBuilder.Ellipse(num12, -97.0, 8.0, 9.0), head, c2.White, c2.Line, 1.6);
				Add(PathBuilder.Ellipse(num12 + 0.8, -96.4, 4.6, 5.4), head, null, c2.Line, 1.4);
				Add(PathBuilder.Ellipse(num12 + 1.2, -96.0, 1.6, 2.0), head, c2.Line);
				break;
			case EyeStyle.Heart:
				Add(Lash(num12, -97.0, side, 0.0), head, c2.Line);
				Add(new PathBuilder().M(num12, -88.0).C(num12 - 11.0, -96.0, num12 - 9.0, -106.0, num12 - 3.5, -104.0).C(num12 - 1.5, -103.3, num12, -101.0, num12, -100.0)
					.C(num12, -101.0, num12 + 1.5, -103.3, num12 + 3.5, -104.0)
					.C(num12 + 9.0, -106.0, num12 + 11.0, -96.0, num12, -88.0)
					.Z()
					.ToString(), head, "#FF5C8A", c2.Line, 1.2);
				Add(PathBuilder.Ellipse(num12 - 3.5, -100.0, 1.8, 1.4), head, c2.White, null, 0.0, 0.85);
				break;
			default:
				if (num13 < 0.18)
				{
					Add(new PathBuilder().M(num12 - 10.0, -94.0).Q(num12, -91.0, num12 + 10.0, -94.0).ToString(), head, null, c2.Line, 2.4);
				}
				else
				{
					double num14 = ((eyeStyle == EyeStyle.Wide) ? 1.12 : 1.0);
					Affine t7 = Affine.Translate(0.0 - num12, 87.0).Then(Affine.Scale(num14, num13 * num14)).Then(Affine.Translate(num12, -87.0))
						.Then(head);
					double drop2 = (1.0 - num13) * 15.0;
					double num15 = Math.Clamp(p2.LookX, -1.0, 1.0) * 2.3;
					double num16 = Math.Clamp(p2.LookY, -1.0, 1.0) * 1.6;
					double num17 = num12 + num15;
					double num18 = -95.5 + num16;
					double num19 = ((eyeStyle == EyeStyle.Wide) ? 0.7 : 1.0);
					Add(PathBuilder.Ellipse(num12, -96.0, 9.0, 10.0), t7, c2.White);
					Add(PathBuilder.Ellipse(num17, num18, 7.0, 9.0), t7, c2.Eyes);
					Add(PathBuilder.Ellipse(num17, num18 + 3.6, 5.0, 4.4), t7, c2.EyesLight, null, 0.0, 0.85);
					Add(PathBuilder.Ellipse(num17, num18 - 0.6, 3.6 * num19, 5.0 * num19), t7, c2.EyesDark);
					Add(PathBuilder.Ellipse(num17 - 2.6, num18 - 3.8, 2.6, 3.0), t7, c2.White);
					Add(PathBuilder.Ellipse(num17 + 2.8, num18 + 3.3, 1.2, 1.2), t7, c2.White, null, 0.0, 0.9);
					Add(new PathBuilder().M(num12 - 6.0, -86.4).Q(num12, -84.8, num12 + 6.0, -86.4).ToString(), t7, null, c2.Line, 0.9, 0.55);
					Add(Lash(num12, -97.0, side, drop2), head, c2.Line);
				}
				break;
			}
		}
		static string Lash(double cx, double cy, int side, double drop)
		{
			double num10 = cy + drop;
			double num11 = ((side < 0) ? (cx - 10.5) : (cx + 10.5));
			return new PathBuilder().M(cx - 10.5, num10 - 4.0).C(cx - 8.0, num10 - 12.8, cx + 8.0, num10 - 12.8, cx + 10.5, num10 - 4.0).L(num11 + (double)side * 2.8, num10 - 7.5)
				.C(cx + 7.0, num10 - 9.4, cx - 7.0, num10 - 9.4, cx - 10.5, num10 - 4.0)
				.Z()
				.ToString();
		}
		void Leg(double x, double lift)
		{
			double num20 = -26.0 + sitDrop;
			double num21 = -7.0 - Math.Max(0.0, lift);
			double num22 = Math.Max(num20 + 3.0, num21 - 11.0 + sitDrop * 0.4);
			if (num22 > num20)
			{
				Add(new PathBuilder().M(x - 3.6, num20).L(x + 3.6, num20).L(x + 3.7, num22 + 1.0)
					.L(x - 3.7, num22 + 1.0)
					.Z()
					.ToString(), root, c2.Skin, c2.Line, 1.2);
			}
			Add(new PathBuilder().M(x - 3.9, num22).L(x + 3.9, num22).L(x + 3.7, num21 + 1.0)
				.L(x - 3.7, num21 + 1.0)
				.Z()
				.ToString(), root, c2.White, c2.Line, 1.2);
			Add(new PathBuilder().M(x - 3.9, num22 + 1.6).L(x + 3.9, num22 + 1.6).ToString(), root, null, c2.Ribbon, 1.1);
			double num23 = 1.0 + 0.25 * p2.Sit;
			Add(new PathBuilder().M(x - 5.0 * num23, num21 - 1.0).C(x - 5.0 * num23, num21 - 5.0, x + 5.0 * num23, num21 - 5.0, x + 5.5 * num23, num21 - 1.0).L(x + 6.0 * num23, num21 + 5.0)
				.C(x + 6.0 * num23, num21 + 7.6, x - 6.0 * num23, num21 + 7.6, x - 6.0 * num23, num21 + 5.0)
				.Z()
				.ToString(), root, c2.Shoes, c2.Line, 1.2);
			Add(new PathBuilder().M(x - 4.6 * num23, num21 + 0.6).L(x + 4.6 * num23, num21 + 0.6).ToString(), root, null, c2.Ribbon, 1.1);
			Add(PathBuilder.Ellipse(x - 2.2 * num23, num21 + 2.8, 1.6, 0.9), root, c2.White, null, 0.0, 0.45);
		}
		void Mouth()
		{
			double num4 = turn * 5.0;
			double num5 = Math.Clamp(p2.MouthOpen, 0.0, 1.0);
			MouthStyle mouthStyle = p2.Mouth;
			bool flag = num5 > 0.08;
			if (flag)
			{
				bool flag2;
				switch (mouthStyle)
				{
				case MouthStyle.Smile:
				case MouthStyle.Cat:
				case MouthStyle.Flat:
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			if (flag)
			{
				mouthStyle = MouthStyle.Open;
			}
			double num6;
			switch (mouthStyle)
			{
			case MouthStyle.Smile:
				Add(new PathBuilder().M(num4 - 4.2, -79.0).Q(num4, -75.2, num4 + 4.2, -79.0).ToString(), head, null, c2.Line, 1.6);
				return;
			case MouthStyle.Cat:
				Add(new PathBuilder().M(num4 - 5.0, -79.2).Q(num4 - 2.5, -75.4, num4, -78.6).Q(num4 + 2.5, -75.4, num4 + 5.0, -79.2)
					.ToString(), head, null, c2.Line, 1.4);
				return;
			case MouthStyle.Flat:
				Add(new PathBuilder().M(num4 - 3.0, -78.0).L(num4 + 3.0, -78.0).ToString(), head, null, c2.Line, 1.5);
				return;
			case MouthStyle.Pout:
				Add(new PathBuilder().M(num4 - 1.5, -80.5).Q(num4 + 2.5, -79.5, num4, -78.0).Q(num4 + 2.5, -76.5, num4 - 1.5, -75.5)
					.ToString(), head, null, c2.Line, 1.4);
				return;
			case MouthStyle.O:
				Add(PathBuilder.Ellipse(num4, -77.0, 2.7, 3.5), head, c2.Mouth, c2.Line, 1.2);
				return;
			default:
				num6 = Math.Max(num5, 0.3);
				break;
			case MouthStyle.Grin:
				num6 = Math.Max(num5, 0.75);
				break;
			}
			double num7 = num6;
			double num8 = ((mouthStyle == MouthStyle.Grin) ? 6.2 : 5.0);
			double num9 = -76.5 + 6.5 * num7;
			Add(new PathBuilder().M(num4 - num8, -80.0).Q(num4, -78.6, num4 + num8, -80.0).Q(num4 + num8 * 0.8, num9, num4, num9)
				.Q(num4 - num8 * 0.8, num9, num4 - num8, -80.0)
				.Z()
				.ToString(), head, c2.Mouth, c2.Line, 1.2);
			Add(PathBuilder.Ellipse(num4, num9 - 1.6 - num7, num8 * 0.55, 1.4 + 1.1 * num7), head, c2.Tongue);
		}
	}
}
