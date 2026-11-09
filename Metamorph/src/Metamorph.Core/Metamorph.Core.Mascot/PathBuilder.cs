using System;
using System.Globalization;
using System.Text;

namespace Metamorph.Core.Mascot;

public sealed class PathBuilder
{
	private readonly StringBuilder _sb = new StringBuilder();

	private PathBuilder Cmd(char c, params double[] values)
	{
		if (_sb.Length > 0)
		{
			_sb.Append(' ');
		}
		_sb.Append(c);
		foreach (double value in values)
		{
			_sb.Append(' ');
			_sb.Append(Math.Round(value, 2).ToString(CultureInfo.InvariantCulture));
		}
		return this;
	}

	public PathBuilder M(double x, double y)
	{
		return Cmd('M', x, y);
	}

	public PathBuilder L(double x, double y)
	{
		return Cmd('L', x, y);
	}

	public PathBuilder Q(double x1, double y1, double x, double y)
	{
		return Cmd('Q', x1, y1, x, y);
	}

	public PathBuilder C(double x1, double y1, double x2, double y2, double x, double y)
	{
		return Cmd('C', x1, y1, x2, y2, x, y);
	}

	public PathBuilder Z()
	{
		return Cmd('Z');
	}

	public override string ToString()
	{
		return _sb.ToString();
	}

	public static string Ellipse(double cx, double cy, double rx, double ry)
	{
		return new PathBuilder().M(cx - rx, cy).C(cx - rx, cy - ry * 0.5523, cx - rx * 0.5523, cy - ry, cx, cy - ry).C(cx + rx * 0.5523, cy - ry, cx + rx, cy - ry * 0.5523, cx + rx, cy)
			.C(cx + rx, cy + ry * 0.5523, cx + rx * 0.5523, cy + ry, cx, cy + ry)
			.C(cx - rx * 0.5523, cy + ry, cx - rx, cy + ry * 0.5523, cx - rx, cy)
			.Z()
			.ToString();
	}
}
