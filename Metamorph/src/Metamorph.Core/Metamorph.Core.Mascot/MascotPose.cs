using System.Collections.Generic;

namespace Metamorph.Core.Mascot;

public sealed class MascotPose
{
	public double X;

	public double Y;

	public double Rotation;

	public double ScaleX = 1.0;

	public double ScaleY = 1.0;

	public double Body;

	public double Breath = 1.0;

	public double Head;

	public double HeadY;

	public double Turn;

	public double ArmL;

	public double ArmR;

	public double LiftL;

	public double LiftR;

	public double Sit;

	public double TailL;

	public double TailR;

	public double Ahoge;

	public double Bangs;

	public double Skirt;

	public double EyeOpen = 1.0;

	public EyeStyle Eyes;

	public double LookX;

	public double LookY;

	public MouthStyle Mouth;

	public double MouthOpen;

	public double Blush = 0.35;

	public List<MascotFx> Fx = new List<MascotFx>();

	public MascotPose Clone()
	{
		MascotPose obj = (MascotPose)MemberwiseClone();
		obj.Fx = new List<MascotFx>(Fx);
		return obj;
	}
}
