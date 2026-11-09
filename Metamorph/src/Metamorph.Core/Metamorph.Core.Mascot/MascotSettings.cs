using System;

namespace Metamorph.Core.Mascot;

public sealed class MascotSettings
{
	public bool Enabled { get; set; } = true;


	public string Name { get; set; } = "Мета";


	public double Scale { get; set; } = 1.0;


	public bool ThemeColors { get; set; } = true;


	public string Hair { get; set; } = "#F4A7C8";


	public string Eyes { get; set; } = "#7C6CF2";


	public string Outfit { get; set; } = "#3B3F63";


	public bool Walk { get; set; } = true;


	public bool Dance { get; set; } = true;


	public bool Speech { get; set; } = true;


	public double Chattiness { get; set; } = 1.0;


	public bool Sleep { get; set; } = true;


	public bool HideInFullscreen { get; set; } = true;


	public double? Position { get; set; }

	public static MascotSettings Sanitize(MascotSettings? s)
	{
		if (s == null)
		{
			s = new MascotSettings();
		}
		s.Scale = Math.Clamp(double.IsFinite(s.Scale) ? s.Scale : 1.0, 0.5, 2.0);
		s.Chattiness = Math.Clamp(double.IsFinite(s.Chattiness) ? s.Chattiness : 1.0, 0.0, 3.0);
		if (string.IsNullOrWhiteSpace(s.Name))
		{
			s.Name = "Мета";
		}
		double? position = s.Position;
		if (position.HasValue)
		{
			double valueOrDefault = position.GetValueOrDefault();
			s.Position = (double.IsFinite(valueOrDefault) ? new double?(Math.Clamp(valueOrDefault, 0.0, 1.0)) : null);
		}
		return s;
	}
}
