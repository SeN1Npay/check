using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Metamorph.Core.Desktop;

public enum HotCorner
{
	TopLeft,
	TopRight,
	BottomLeft,
	BottomRight
}

public readonly record struct HotCornerPoint(HotCorner Corner, int X, int Y);

// Автомат горячих углов: курсор задержался в углу DelayMs → срабатывание;
// повторно угол взводится только после ухода курсора дальше RearmDistance.
public sealed class HotCornerTracker
{
	private readonly int _delayMs;

	private readonly int _hit;

	private readonly int _rearm;

	private readonly HashSet<HotCorner> _active;

	private List<HotCornerPoint> _points = new List<HotCornerPoint>();

	private HotCornerPoint? _dwell;

	private long _since;

	private HotCornerPoint? _fired;

	public IReadOnlyList<HotCornerPoint> Points => _points;

	public HotCornerTracker(int delayMs, IEnumerable<HotCorner> active, int hitSize = 2, int rearmDistance = 40)
	{
		_delayMs = Math.Max(0, delayMs);
		_hit = Math.Max(0, hitSize);
		_rearm = Math.Max(1, rearmDistance);
		_active = new HashSet<HotCorner>(active);
	}

	// Только внешние углы всего рабочего стола: угол, к которому примыкает другой монитор, не считается.
	public void SetMonitors(IEnumerable<Rectangle> monitors)
	{
		List<Rectangle> list = monitors.Where((Rectangle r) => r.Width > 0 && r.Height > 0).ToList();
		List<HotCornerPoint> points = new List<HotCornerPoint>();
		foreach (Rectangle m in list)
		{
			Add(points, list, m, HotCorner.TopLeft, m.Left, m.Top, -1, -1);
			Add(points, list, m, HotCorner.TopRight, m.Right - 1, m.Top, 1, -1);
			Add(points, list, m, HotCorner.BottomLeft, m.Left, m.Bottom - 1, -1, 1);
			Add(points, list, m, HotCorner.BottomRight, m.Right - 1, m.Bottom - 1, 1, 1);
		}
		_points = points;
		_dwell = null;
		_fired = null;
	}

	private void Add(List<HotCornerPoint> points, List<Rectangle> all, Rectangle self, HotCorner corner, int x, int y, int dx, int dy)
	{
		if (!_active.Contains(corner))
		{
			return;
		}
		foreach (Rectangle other in all)
		{
			if (!(other == self) && (other.Contains(x + dx, y) || other.Contains(x, y + dy) || other.Contains(x + dx, y + dy)))
			{
				return;
			}
		}
		points.Add(new HotCornerPoint(corner, x, y));
	}

	public HotCornerPoint? Find(int x, int y)
	{
		foreach (HotCornerPoint p in _points)
		{
			if (Math.Abs(x - p.X) <= _hit && Math.Abs(y - p.Y) <= _hit)
			{
				return p;
			}
		}
		return null;
	}

	// blocked вычисляется лениво — только когда курсор действительно в углу.
	public HotCornerPoint? Update(int x, int y, long nowMs, Func<bool>? blocked = null)
	{
		if (_fired.HasValue)
		{
			HotCornerPoint f = _fired.Value;
			double dx = x - f.X;
			double dy = y - f.Y;
			if (Math.Sqrt(dx * dx + dy * dy) <= _rearm)
			{
				return null;
			}
			_fired = null;
		}
		HotCornerPoint? hit = Find(x, y);
		if (!hit.HasValue || (blocked != null && blocked()))
		{
			_dwell = null;
			return null;
		}
		if (_dwell != hit)
		{
			_dwell = hit;
			_since = nowMs;
		}
		if (nowMs - _since < _delayMs)
		{
			return null;
		}
		_dwell = null;
		_fired = hit;
		return hit;
	}
}
