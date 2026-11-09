using System.ComponentModel;

namespace Metamorph.Core.Models;

public enum WallpaperKind
{
	Keep,
	Image,
	Solid,
	Gradient,
	Slideshow,
	TimeOfDay,
	Video,
	[Browsable(false)]
	Live
}
