using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Metamorph.Core.Dock;

namespace Metamorph.App.Dock;

internal sealed class DockItem
{
	public double Scale = 1.0;

	public double Lift;

	public double Appear;

	public double MainOffset;

	public double LastStart = double.NaN;

	public double LastSlot;

	public double LastSize;

	public double BounceStart = -1.0;

	public bool BounceLoop;

	public bool BounceDecay;

	public int BounceCount;

	public BadgeInfo Badge;

	public DockItemKind Kind { get; }

	public string Key { get; set; }

	public DockPin? Pin { get; set; }

	public string Name { get; set; } = "";


	public List<TrackedWindow> Windows { get; } = new List<TrackedWindow>();


	public bool IsActive { get; set; }

	public bool Attention { get; set; }

	public bool Removing { get; set; }

	public bool IconRequested { get; set; }

	public Image Image { get; } = new Image
	{
		Stretch = Stretch.Uniform,
		IsHitTestVisible = false
	};


	public StackPanel Indicators { get; } = new StackPanel
	{
		IsHitTestVisible = false
	};


	public Rectangle? Line { get; set; }

	public int IndicatorCount { get; set; }

	public Image? OverlayImage { get; set; }

	public Border? ProgressTrack { get; set; }

	public Border? ProgressFill { get; set; }

	public bool IsRunning => Windows.Count > 0;

	public bool IsInteractive
	{
		get
		{
			if (Kind != DockItemKind.Separator)
			{
				return !Removing;
			}
			return false;
		}
	}

	public DockItem(DockItemKind kind, string key)
	{
		Kind = kind;
		Key = key;
		RenderOptions.SetBitmapScalingMode(Image, BitmapScalingMode.HighQuality);
	}
}
