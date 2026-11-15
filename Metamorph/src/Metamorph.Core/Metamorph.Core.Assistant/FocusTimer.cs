using System;

namespace Metamorph.Core.Assistant;

public sealed class FocusTimer
{
	private readonly Func<DateTime> _clock;

	private DateTime _endsAt;

	private TimeSpan _pausedLeft;

	public FocusPhase Phase { get; private set; }

	public string? Label { get; private set; }

	public TimeSpan Duration { get; private set; }

	public bool Paused { get; private set; }

	public int Round { get; private set; }

	public TimeSpan FocusLength { get; private set; } = TimeSpan.FromMinutes(25.0);


	public TimeSpan BreakLength { get; private set; } = TimeSpan.FromMinutes(5.0);


	public bool Running => Phase != FocusPhase.Idle;

	public TimeSpan Remaining
	{
		get
		{
			if (Phase == FocusPhase.Idle)
			{
				return TimeSpan.Zero;
			}
			if (Paused)
			{
				return _pausedLeft;
			}
			TimeSpan timeSpan = _endsAt - _clock();
			if (!(timeSpan > TimeSpan.Zero))
			{
				return TimeSpan.Zero;
			}
			return timeSpan;
		}
	}

	public double Progress
	{
		get
		{
			if (!(Duration.TotalSeconds > 0.0))
			{
				return 0.0;
			}
			return Math.Clamp(1.0 - Remaining.TotalSeconds / Duration.TotalSeconds, 0.0, 1.0);
		}
	}

	public event Action<FocusPhase, string?>? Finished;

	public event Action? Changed;

	public FocusTimer(Func<DateTime>? clock = null)
	{
		_clock = clock ?? ((Func<DateTime>)(() => DateTime.UtcNow));
	}

	public void Start(TimeSpan duration, string? label = null)
	{
		if (!(duration <= TimeSpan.Zero))
		{
			Round = 0;
			Begin(FocusPhase.Timer, duration, label);
		}
	}

	public void StartPomodoro(TimeSpan? focus = null, TimeSpan? rest = null)
	{
		TimeSpan focusLength;
		if (focus.HasValue)
		{
			TimeSpan valueOrDefault = focus.GetValueOrDefault();
			if (valueOrDefault > TimeSpan.Zero)
			{
				focusLength = valueOrDefault;
				goto IL_0030;
			}
		}
		focusLength = TimeSpan.FromMinutes(25.0);
		goto IL_0030;
		IL_0030:
		FocusLength = focusLength;
		TimeSpan breakLength;
		if (rest.HasValue)
		{
			TimeSpan valueOrDefault2 = rest.GetValueOrDefault();
			if (valueOrDefault2 > TimeSpan.Zero)
			{
				breakLength = valueOrDefault2;
				goto IL_0065;
			}
		}
		breakLength = TimeSpan.FromMinutes(5.0);
		goto IL_0065;
		IL_0065:
		BreakLength = breakLength;
		Round = 1;
		Begin(FocusPhase.Focus, FocusLength, null);
	}

	public void Pause()
	{
		if (Phase != 0 && !Paused)
		{
			_pausedLeft = Remaining;
			Paused = true;
			this.Changed?.Invoke();
		}
	}

	public void Resume()
	{
		if (Paused)
		{
			Paused = false;
			_endsAt = _clock() + _pausedLeft;
			this.Changed?.Invoke();
		}
	}

	public void Cancel()
	{
		if (Phase != 0)
		{
			Phase = FocusPhase.Idle;
			Paused = false;
			Label = null;
			Round = 0;
			this.Changed?.Invoke();
		}
	}

	public void Tick()
	{
		if (Phase != 0 && !Paused && !(Remaining > TimeSpan.Zero))
		{
			FocusPhase phase = Phase;
			string label = Label;
			switch (Phase)
			{
			case FocusPhase.Focus:
				Begin(FocusPhase.Break, (Round % 4 == 0) ? TimeSpan.FromTicks(BreakLength.Ticks * 3) : BreakLength, null);
				break;
			case FocusPhase.Break:
				Round++;
				Begin(FocusPhase.Focus, FocusLength, null);
				break;
			default:
				Phase = FocusPhase.Idle;
				Label = null;
				this.Changed?.Invoke();
				break;
			}
			this.Finished?.Invoke(phase, label);
		}
	}

	public static string Format(TimeSpan t)
	{
		long num = (long)Math.Ceiling(Math.Max(0.0, t.TotalSeconds));
		if (num >= 3600)
		{
			return $"{num / 3600}:{num / 60 % 60:00}:{num % 60:00}";
		}
		return $"{num / 60}:{num % 60:00}";
	}

	private void Begin(FocusPhase phase, TimeSpan duration, string? label)
	{
		Phase = phase;
		Duration = duration;
		Label = label;
		Paused = false;
		_endsAt = _clock() + duration;
		this.Changed?.Invoke();
	}
}
