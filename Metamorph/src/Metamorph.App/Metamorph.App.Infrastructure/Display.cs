using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Metamorph.Core.Models;

namespace Metamorph.App.Infrastructure;

public sealed record Display(string Name, Rectangle Bounds, Rectangle WorkArea, bool Primary)
{
	public static Display PrimaryDisplay => All().FirstOrDefault() ?? new Display("primary", new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1040), Primary: true);

	public static double Scale
	{
		get
		{
			double primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
			double num = ((double?)Screen.PrimaryScreen?.Bounds.Width) ?? primaryScreenWidth;
			if (!(primaryScreenWidth > 0.0))
			{
				return 1.0;
			}
			return Math.Max(0.5, num / primaryScreenWidth);
		}
	}

	public Rect BoundsDip
	{
		get
		{
			double scale = Scale;
			return new Rect((double)Bounds.X / scale, (double)Bounds.Y / scale, (double)Bounds.Width / scale, (double)Bounds.Height / scale);
		}
	}

	public Rect WorkAreaDip
	{
		get
		{
			double scale = Scale;
			return new Rect((double)WorkArea.X / scale, (double)WorkArea.Y / scale, (double)WorkArea.Width / scale, (double)WorkArea.Height / scale);
		}
	}

	public static IReadOnlyList<Display> All()
	{
		return (from s in Screen.AllScreens
			select new Display(s.DeviceName, s.Bounds, s.WorkingArea, s.Primary) into d
			orderby d.Primary descending, d.Bounds.X
			select d).ToList();
	}

	public static IReadOnlyList<Display> For(ScreenMode mode)
	{
		if (mode != ScreenMode.All)
		{
			return new Display[1] { PrimaryDisplay };
		}
		return All();
	}

	public bool Contains(int x, int y)
	{
		return Bounds.Contains(x, y);
	}
}
