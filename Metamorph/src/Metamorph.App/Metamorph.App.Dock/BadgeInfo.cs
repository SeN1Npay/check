using System.Windows.Media;

namespace Metamorph.App.Dock;

internal readonly record struct BadgeInfo(double? Progress, bool Error, bool Paused, bool Indeterminate, ImageSource? Overlay)
{
	public bool IsEmpty
	{
		get
		{
			if (!Progress.HasValue && !Indeterminate)
			{
				return Overlay == null;
			}
			return false;
		}
	}
}
