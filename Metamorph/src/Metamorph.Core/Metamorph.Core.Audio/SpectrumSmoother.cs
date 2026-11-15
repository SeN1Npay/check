using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Audio;

public sealed class SpectrumSmoother
{
	private float[] _values;

	public float[] Values => _values;

	public double Attack { get; set; } = 30.0;


	public double Decay { get; set; } = 7.0;


	public bool IsSilent => _values.All((float v) => v <= 0.001f);

	public SpectrumSmoother(int bands)
	{
		_values = new float[bands];
	}

	public float[] Update(IReadOnlyList<float> target, double dtSeconds)
	{
		if (target.Count != _values.Length)
		{
			_values = new float[target.Count];
		}
		if (dtSeconds <= 0.0)
		{
			return _values;
		}
		for (int i = 0; i < _values.Length; i++)
		{
			float num = Math.Clamp(target[i], 0f, 1f);
			double num2 = ((num > _values[i]) ? Attack : Decay);
			double num3 = 1.0 - Math.Exp((0.0 - num2) * dtSeconds);
			float num4 = _values[i] + (num - _values[i]) * (float)num3;
			_values[i] = ((num4 < 0.001f) ? 0f : num4);
		}
		return _values;
	}
}
