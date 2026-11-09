using System;
using System.Collections.Generic;

namespace Metamorph.Core.Services;

public sealed class LoopBreaker
{
	private readonly int _max;

	private readonly double _windowMs;

	private readonly double _cooldownMs;

	private readonly Queue<double> _hits = new Queue<double>();

	private double _blockedUntil = double.MinValue;

	private readonly Func<double> _now;

	public int Trips { get; private set; }

	public bool IsBlocked => _now() < _blockedUntil;

	public LoopBreaker(int max, TimeSpan window, TimeSpan cooldown, Func<double>? now = null)
	{
		_max = Math.Max(1, max);
		_windowMs = window.TotalMilliseconds;
		_cooldownMs = cooldown.TotalMilliseconds;
		_now = now ?? ((Func<double>)(() => EventTrace.Now()));
	}

	public bool Allow()
	{
		double num = _now();
		if (num < _blockedUntil)
		{
			return false;
		}
		_hits.Enqueue(num);
		while (_hits.Count > 0 && num - _hits.Peek() > _windowMs)
		{
			_hits.Dequeue();
		}
		if (_hits.Count <= _max)
		{
			return true;
		}
		_hits.Clear();
		_blockedUntil = num + _cooldownMs;
		Trips++;
		return false;
	}
}
