using System;
using System.Collections.Generic;
using Metamorph.Core.Localization;

namespace Metamorph.Core.Mascot;

public sealed class MascotBrain
{
	public const double Gravity = 2600.0;

	public const double SleepAfter = 180.0;

	private readonly Random _rnd;

	private readonly List<(double T, double X, double Y)> _drag = new List<(double, double, double)>();

	private readonly Queue<double> _pokes = new Queue<double>();

	private double _time;

	private double _decideAt;

	private double _actUntil;

	private double _walkTo;

	private double _nextChat;

	private double _fallPeak;

	private double _heldLine;

	private double _lastTrackLine = -1000000000.0;

	private double _speechAge;

	private double _speechTalk;

	private double _speechLeft;

	private double _grabDx;

	private double _grabDy;

	private double _commandDanceUntil;

	private MascotState _afterAct;

	private int _bonks;

	private int _lastHour = -1;

	private bool _lateNightSaid;

	private bool _sleptByIdle;

	private bool _thinking;

	private string? _speech;

	public MascotAnimator Animator { get; }

	public MascotSettings Settings { get; set; }

	public double UnitScale => 0.62 * Settings.Scale;

	public double HalfWidth => 58.0 * UnitScale;

	public double Height => 178.0 * UnitScale;

	public double X { get; private set; }

	public double Y { get; private set; }

	public double Vx { get; private set; }

	public double Vy { get; private set; }

	public double Left { get; private set; }

	public double Right { get; private set; } = 1000.0;


	public double Top { get; private set; }

	public double Ground { get; private set; } = 700.0;


	public MascotState State { get; private set; } = MascotState.Fall;


	public double StateTime { get; private set; }

	public double WalkTarget => _walkTo;

	public string? Speech => _speech;

	public bool Speaking { get; set; }

	public event Action<string?>? SpeechChanged;

	public MascotBrain(MascotSettings? settings = null, int? seed = null)
	{
		Random rnd;
		if (seed.HasValue)
		{
			int valueOrDefault = seed.GetValueOrDefault();
			rnd = new Random(valueOrDefault);
		}
		else
		{
			rnd = new Random();
		}
		_rnd = rnd;
		Settings = MascotSettings.Sanitize(settings);
		Animator = new MascotAnimator(_rnd.Next());
		_decideAt = 3.0;
		_nextChat = NextChatDelay();
	}

	public void SetWorld(double left, double top, double right, double ground)
	{
		Left = left;
		Top = top;
		Right = Math.Max(left + HalfWidth * 2.0 + 1.0, right);
		Ground = Math.Max(top + Height, ground);
		X = Math.Clamp(X, Left + HalfWidth, Right - HalfWidth);
		MascotState state = State;
		if ((uint)(state - 6) > 1u)
		{
			if (Math.Abs(Y - Ground) > 0.5)
			{
				Enter(MascotState.Fall);
			}
			Y = Math.Min(Y, Ground);
		}
		_walkTo = Math.Clamp(_walkTo, Left + HalfWidth, Right - HalfWidth);
	}

	public void Place(double x)
	{
		X = Math.Clamp(x, Left + HalfWidth, Right - HalfWidth);
		Y = Ground;
		double vx = (Vy = 0.0);
		Vx = vx;
		Enter(MascotState.Idle);
	}

	public void Drop(double x, double fromY)
	{
		X = Math.Clamp(x, Left + HalfWidth, Right - HalfWidth);
		Y = Math.Min(fromY, Ground);
		Vx = 0.0;
		Vy = 0.0;
		Enter(MascotState.Fall);
	}

	public void Grab(double px, double py, double t)
	{
		_grabDx = X - px;
		_grabDy = Y - py;
		_drag.Clear();
		_drag.Add((t, X, Y));
		_heldLine = _time + 1.2 + _rnd.NextDouble() * 2.0;
		Enter(MascotState.Held);
		Animator.Jiggle(0.8);
	}

	public void DragTo(double px, double py, double t)
	{
		if (State != MascotState.Held)
		{
			return;
		}
		X = px + _grabDx;
		Y = py + _grabDy;
		_drag.Add((t, X, Y));
		while (_drag.Count > 2 && _drag[0].T < t - 0.12)
		{
			_drag.RemoveAt(0);
		}
		if (_drag.Count >= 2)
		{
			(double, double, double) tuple = _drag[0];
			double num = t - tuple.Item1;
			if (num > 0.005)
			{
				Vx = (X - tuple.Item2) / num;
				Vy = (Y - tuple.Item3) / num;
			}
		}
	}

	public void Release(double t)
	{
		if (State != MascotState.Held)
		{
			return;
		}
		while (_drag.Count > 1 && _drag[0].T < t - 0.12)
		{
			_drag.RemoveAt(0);
		}
		if (_drag.Count >= 2)
		{
			List<(double T, double X, double Y)> drag = _drag;
			if (t - drag[drag.Count - 1].T < 0.1)
			{
				(double, double, double) tuple = _drag[0];
				List<(double T, double X, double Y)> drag2 = _drag;
				(double, double, double) tuple2 = drag2[drag2.Count - 1];
				double num = Math.Max(0.016, tuple2.Item1 - tuple.Item1);
				Vx = (tuple2.Item2 - tuple.Item2) / num;
				Vy = (tuple2.Item3 - tuple.Item3) / num;
				goto IL_00f9;
			}
		}
		double vx = (Vy = 0.0);
		Vx = vx;
		goto IL_00f9;
		IL_00f9:
		Vx = Math.Clamp(Vx, -4200.0, 4200.0);
		Vy = Math.Clamp(Vy, -4200.0, 4200.0);
		_drag.Clear();
		Enter(MascotState.Fall);
	}

	public void Poke(bool head)
	{
		MascotState state = State;
		if ((uint)(state - 6) <= 1u)
		{
			return;
		}
		Animator.Jiggle();
		if (State == MascotState.Sleep)
		{
			Act(MascotClip.Surprised, 1.3);
			SayAuto(MascotLines.Pick(MascotLines.WakeUp, _rnd), reaction: true);
			return;
		}
		while (_pokes.Count > 0 && _pokes.Peek() < _time - 4.0)
		{
			_pokes.Dequeue();
		}
		_pokes.Enqueue(_time);
		if (_pokes.Count >= 4)
		{
			Act(MascotClip.Annoyed, 2.4);
			SayAuto(MascotLines.Pick(MascotLines.PokeMany, _rnd), reaction: true);
			_pokes.Clear();
		}
		else if (head)
		{
			Act(MascotClip.Love, 2.2);
			if (_rnd.NextDouble() < 0.6)
			{
				SayAuto(MascotLines.Pick(MascotLines.Pet, _rnd), reaction: true);
			}
		}
		else
		{
			Act((_pokes.Count == 1) ? MascotClip.Surprised : MascotClip.Happy, (_pokes.Count == 1) ? 1.1 : 1.6);
			if (_rnd.NextDouble() < 0.55)
			{
				SayAuto(MascotLines.Pick(MascotLines.Poke, _rnd), reaction: true);
			}
		}
	}

	public void Command(MascotAction action)
	{
		MascotState state = State;
		if ((uint)(state - 6) > 1u)
		{
			switch (action)
			{
			case MascotAction.Wave:
				Act(MascotClip.Wave, 2.6);
				break;
			case MascotAction.Dance:
				_commandDanceUntil = _time + 10.0;
				Enter(MascotState.Dance);
				SayAuto(MascotLines.Pick(MascotLines.Dance, _rnd), reaction: false);
				break;
			case MascotAction.Sleep:
				Enter(MascotState.Sleep);
				break;
			case MascotAction.Sit:
				Enter(MascotState.Sit);
				_decideAt = StateTime + 60.0;
				break;
			case MascotAction.Jump:
				Jump();
				break;
			case MascotAction.Think:
				Act(MascotClip.Think, 3.0);
				break;
			case MascotAction.Stretch:
				Act(MascotClip.Stretch, 2.6);
				break;
			case MascotAction.Love:
				Act(MascotClip.Love, 2.4);
				break;
			case MascotAction.Happy:
				Act(MascotClip.Happy, 2.0);
				break;
			}
		}
	}

	public void SetThinking(bool thinking)
	{
		_thinking = thinking;
		bool flag = thinking;
		if (flag)
		{
			MascotState state = State;
			bool flag2 = (uint)(state - 6) <= 1u;
			flag = !flag2;
		}
		if (flag)
		{
			Act(MascotClip.Think, 30.0);
		}
		else if (!thinking && State == MascotState.Act && Animator.Clip == MascotClip.Think)
		{
			_actUntil = _time;
		}
	}

	public void Notify(MascotEvent e, string? arg = null)
	{
		switch (e)
		{
		case MascotEvent.Hello:
		{
			MascotState state = State;
			if ((uint)(state - 6) > 1u)
			{
				Act(MascotClip.Wave, 2.6);
			}
			SayAuto(MascotLines.Greeting(DateTime.Now.Hour, _rnd), reaction: true);
			break;
		}
		case MascotEvent.Bye:
			SayAuto(MascotLines.Pick(MascotLines.Bye, _rnd), reaction: true);
			break;
		case MascotEvent.ThemeApplied:
		{
			MascotState state = State;
			if ((uint)state <= 2u)
			{
				Act(MascotClip.Happy, 1.8);
			}
			if (!string.IsNullOrWhiteSpace(arg) && Settings.Chattiness > 0.0)
			{
				SayAuto(Loc.F("Тема «{0}» — мне идёт?", arg), reaction: false);
			}
			break;
		}
		case MascotEvent.WallpaperChanged:
			if (Settings.Chattiness > 0.0 && _rnd.NextDouble() < 0.5)
			{
				SayAuto(MascotLines.Pick(MascotLines.Wallpaper, _rnd), reaction: false);
			}
			break;
		case MascotEvent.TrackChanged:
			if (!string.IsNullOrWhiteSpace(arg) && !(Settings.Chattiness <= 0.0) && State != MascotState.Sleep && !(_time - _lastTrackLine < 240.0 / Settings.Chattiness) && !(_rnd.NextDouble() > 0.35 * Settings.Chattiness))
			{
				_lastTrackLine = _time;
				SayAuto(Loc.F("О, «{0}»! ♪", (arg.Length > 40) ? (arg.Substring(0, 40) + "…") : arg), reaction: false);
			}
			break;
		}
	}

	public void Say(string? text, double? seconds = null)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			SetSpeech(null, 0.0);
			return;
		}
		Speak(text, seconds);
		if (State == MascotState.Sleep)
		{
			Act(MascotClip.Surprised, 1.0);
		}
	}

	private void Speak(string text, double? seconds = null)
	{
		text = text.Trim();
		SetSpeech(text, seconds ?? Math.Clamp(2.4 + (double)text.Length * 0.06, 2.8, 14.0));
	}

	private void SayAuto(string text, bool reaction)
	{
		if (Settings.Speech && (reaction || _speech == null))
		{
			Speak(text);
		}
	}

	private void SetSpeech(string? text, double duration)
	{
		if (!(_speech == text) || text != null)
		{
			_speech = text;
			_speechAge = 0.0;
			_speechLeft = duration;
			_speechTalk = ((text == null) ? 0.0 : Math.Min(duration * 0.8, 0.5 + (double)text.Length * 0.045));
			this.SpeechChanged?.Invoke(text);
		}
	}

	private double NextChatDelay()
	{
		double chattiness = Settings.Chattiness;
		if (chattiness <= 0.0)
		{
			return double.PositiveInfinity;
		}
		double num = 300.0 / chattiness;
		return _time + Math.Clamp((0.0 - Math.Log(1.0 - _rnd.NextDouble())) * num, 45.0, 1500.0);
	}

	public MascotPose Update(double dt, in MascotInput input)
	{
		dt = Math.Clamp(double.IsFinite(dt) ? dt : 0.0, 0.0, 0.1);
		_time += dt;
		StateTime += dt;
		if (_speech != null)
		{
			_speechAge += dt;
			_speechLeft -= dt;
			if (_speechLeft <= 0.0)
			{
				SetSpeech(null, 0.0);
			}
		}
		Animator.Talking = Speaking || (_speech != null && _speechAge < _speechTalk);
		Animator.Bass = Math.Clamp(input.Bass, 0.0, 1.0);
		MascotState state = State;
		bool flag = (uint)(state - 6) <= 1u;
		bool flag2 = !flag;
		if (flag2)
		{
			UpdateMood(in input);
		}
		switch (State)
		{
		case MascotState.Held:
			UpdateHeld();
			break;
		case MascotState.Fall:
			UpdateFall(dt);
			break;
		case MascotState.Walk:
			UpdateWalk(dt, in input);
			break;
		case MascotState.Act:
			LookAtCursor(in input);
			if (_time >= _actUntil)
			{
				Enter(_afterAct);
			}
			break;
		case MascotState.Dance:
			Animator.VelocityX = 0.0;
			LookAtCursor(in input, 0.3);
			break;
		case MascotState.Sleep:
			Animator.LookX = 0.0;
			Animator.LookY = 0.3;
			break;
		default:
			LookAtCursor(in input);
			if (StateTime >= _decideAt)
			{
				Decide(in input);
			}
			break;
		}
		flag = flag2;
		if (flag)
		{
			state = State;
			bool flag3 = (uint)(state - 6) <= 1u;
			flag = !flag3;
		}
		if (flag)
		{
			Y = Ground;
			double vx = (Vy = 0.0);
			Vx = vx;
		}
		return Animator.Update(dt);
	}

	private void UpdateMood(in MascotInput input)
	{
		bool flag = Settings.Dance && input.MusicPlaying;
		if (State == MascotState.Sleep)
		{
			if (input.UserIdleSeconds < 1.5 && _sleptByIdle && StateTime > 2.0)
			{
				_sleptByIdle = false;
				Act(MascotClip.Stretch, 2.6);
				int hour = input.Now.Hour;
				SayAuto((hour >= 5 && hour < 12) ? MascotLines.Pick(MascotLines.Morning, _rnd) : MascotLines.Pick(MascotLines.WakeUp, _rnd), reaction: true);
			}
			return;
		}
		bool flag2 = Settings.Sleep && !flag && input.UserIdleSeconds > 180.0;
		if (flag2)
		{
			MascotState state = State;
			bool flag3 = (uint)state <= 2u;
			flag2 = flag3;
		}
		if (flag2)
		{
			_sleptByIdle = true;
			Enter(MascotState.Sleep);
			if (_rnd.NextDouble() < 0.5)
			{
				SayAuto(MascotLines.Pick(MascotLines.Sleepy, _rnd), reaction: false);
			}
			return;
		}
		flag2 = flag;
		if (flag2)
		{
			MascotState state = State;
			bool flag3 = (uint)state <= 2u;
			flag2 = flag3;
		}
		if (flag2)
		{
			Enter(MascotState.Dance);
			if (_rnd.NextDouble() < 0.3 * Settings.Chattiness)
			{
				SayAuto(MascotLines.Pick(MascotLines.MusicStart, _rnd), reaction: false);
			}
		}
		else if (State == MascotState.Dance && !flag && _time > _commandDanceUntil)
		{
			Enter(MascotState.Idle);
		}
		int hour2 = input.Now.Hour;
		if (hour2 != _lastHour)
		{
			if (_lastHour >= 0 && hour2 >= 1 && hour2 < 5 && !_lateNightSaid && input.UserIdleSeconds < 60.0)
			{
				_lateNightSaid = true;
				SayAuto(MascotLines.Pick(MascotLines.LateNight, _rnd), reaction: false);
			}
			if (hour2 >= 6)
			{
				_lateNightSaid = false;
			}
			_lastHour = hour2;
		}
		if (_time >= _nextChat)
		{
			_nextChat = NextChatDelay();
			flag2 = Settings.Speech;
			if (flag2)
			{
				MascotState state = State;
				bool flag3 = (uint)state <= 2u;
				flag2 = flag3;
			}
			if (flag2 && input.UserIdleSeconds < 120.0)
			{
				SayAuto(MascotLines.Pick(MascotLines.Idle, _rnd), reaction: false);
			}
		}
	}

	private void Decide(in MascotInput input)
	{
		bool flag = Math.Abs(input.CursorX - X) < 260.0 && input.CursorY > Ground - Height * 2.5;
		double num = _rnd.NextDouble();
		if (State == MascotState.Sit)
		{
			if (num < 0.25)
			{
				Act(MascotClip.Stretch, 2.6);
			}
			else
			{
				Enter(MascotState.Idle);
			}
		}
		else if (Settings.Walk && num < 0.46)
		{
			double num2 = (Right - Left - HalfWidth * 2.0) * (0.12 + _rnd.NextDouble() * 0.38) * (double)((!(_rnd.NextDouble() < 0.5)) ? 1 : (-1));
			double num3 = X + num2;
			if (num3 < Left + HalfWidth || num3 > Right - HalfWidth)
			{
				num3 = X - num2;
			}
			WalkTo(num3);
		}
		else if (num < 0.6)
		{
			Enter(MascotState.Sit);
		}
		else if (num < 0.66)
		{
			Act(MascotClip.Stretch, 2.6);
		}
		else if (num < 0.72)
		{
			Act(MascotClip.Think, 2.6);
		}
		else if (num < 0.78)
		{
			Jump();
		}
		else if (num < 0.84 && flag)
		{
			Act(MascotClip.Wave, 2.4);
		}
		else if (num < 0.88)
		{
			Act(MascotClip.Happy, 1.6);
		}
		else
		{
			_decideAt = StateTime + 3.0 + _rnd.NextDouble() * 5.0;
		}
	}

	public void WalkTo(double x)
	{
		MascotState state = State;
		if ((uint)(state - 6) > 1u)
		{
			_walkTo = Math.Clamp(x, Left + HalfWidth, Right - HalfWidth);
			if (!(Math.Abs(_walkTo - X) < 2.0))
			{
				Enter(MascotState.Walk);
			}
		}
	}

	private void Jump()
	{
		Vy = -900.0 * Math.Sqrt(Settings.Scale);
		Vx = 0.0;
		Enter(MascotState.Fall);
		Animator.Play(MascotClip.Jump);
	}

	private void UpdateWalk(double dt, in MascotInput input)
	{
		double num = 115.0 * UnitScale;
		int num2 = Math.Sign(_walkTo - X);
		Animator.Facing = ((num2 == 0) ? Animator.Facing : ((double)num2));
		Animator.LookX = (double)num2 * 0.6;
		Animator.LookY = 0.0;
		Animator.VelocityX = num2 * 115;
		X += (double)num2 * num * dt;
		if (num2 == 0 || Math.Sign(_walkTo - X) != num2 || StateTime > 25.0)
		{
			X = Math.Clamp((num2 == 0) ? X : _walkTo, Left + HalfWidth, Right - HalfWidth);
			Animator.VelocityX = 0.0;
			Enter(MascotState.Idle);
		}
	}

	private void UpdateHeld()
	{
		Animator.VelocityX = Vx / UnitScale * 0.6;
		Animator.VelocityY = Vy / UnitScale * 0.4;
		Animator.LookX = 0.0;
		Animator.LookY = 0.2;
		if (_time >= _heldLine)
		{
			_heldLine = double.PositiveInfinity;
			SayAuto(MascotLines.Pick(MascotLines.Held, _rnd), reaction: false);
		}
		Vx *= 0.9;
		Vy *= 0.9;
	}

	private void UpdateFall(double dt)
	{
		Vy += 2600.0 * dt;
		Vx *= 1.0 - Math.Min(1.0, 0.35 * dt);
		X += Vx * dt;
		Y += Vy * dt;
		_fallPeak = Math.Max(_fallPeak, Math.Sqrt(Vx * Vx + Vy * Vy));
		if (X < Left + HalfWidth)
		{
			X = Left + HalfWidth;
			if (Vx < -300.0)
			{
				_bonks++;
				Animator.Impact(0.5);
				Animator.Jiggle(0.6);
			}
			Vx = Math.Abs(Vx) * 0.5;
		}
		else if (X > Right - HalfWidth)
		{
			X = Right - HalfWidth;
			if (Vx > 300.0)
			{
				_bonks++;
				Animator.Impact(0.5);
				Animator.Jiggle(0.6);
			}
			Vx = (0.0 - Math.Abs(Vx)) * 0.5;
		}
		if (Y - Height < Top)
		{
			Y = Top + Height;
			if (Vy < -300.0)
			{
				_bonks++;
			}
			Vy = Math.Abs(Vy) * 0.3;
		}
		if (Animator.Clip != MascotClip.Jump)
		{
			Animator.Play(MascotClip.Fall);
		}
		Animator.VelocityX = Vx / UnitScale * 0.5;
		Animator.VelocityY = Vy / UnitScale * 0.5;
		if (Math.Abs(Vx) > 30.0)
		{
			Animator.Facing = Math.Sign(Vx);
		}
		if (Y < Ground)
		{
			return;
		}
		Y = Ground;
		double vy = Vy;
		if (vy > 650.0)
		{
			Animator.Impact(Math.Min(1.2, vy / 1800.0));
			Vy = (0.0 - vy) * 0.32;
			Vx *= 0.7;
			if (vy > 1400.0)
			{
				_bonks++;
			}
			return;
		}
		Vy = 0.0;
		Animator.Impact(Math.Min(1.0, Math.Max(vy, 200.0) / 1600.0));
		Vx = 0.0;
		bool flag = Animator.Clip == MascotClip.Jump;
		if (_fallPeak > 2600.0 || _bonks >= 2)
		{
			Act(MascotClip.Dizzy, 2.8);
			SayAuto(MascotLines.Pick(MascotLines.Thrown, _rnd), reaction: true);
		}
		else if (!flag && _fallPeak > 1100.0)
		{
			Act(MascotClip.Surprised, 1.0);
			if (_rnd.NextDouble() < 0.4)
			{
				SayAuto(MascotLines.Pick(MascotLines.Landed, _rnd), reaction: false);
			}
		}
		else
		{
			Enter(MascotState.Idle);
		}
	}

	private void LookAtCursor(in MascotInput input, double amount = 1.0)
	{
		double num = Y - Height * 0.72;
		double num2 = input.CursorX - X;
		double num3 = input.CursorY - num;
		if (Math.Sqrt(num2 * num2 + num3 * num3) < 900.0)
		{
			Animator.LookX = Math.Clamp(num2 / 320.0, -1.0, 1.0) * amount;
			Animator.LookY = Math.Clamp(num3 / 320.0, -1.0, 1.0) * amount;
			MascotState state = State;
			bool flag = ((state == MascotState.Idle || state == MascotState.Sit) ? true : false);
			if (flag && Math.Abs(num2) > 40.0)
			{
				Animator.Facing = Math.Sign(num2);
			}
		}
		else
		{
			Animator.LookX = 0.35 * Math.Sin(_time * 0.37) * amount;
			Animator.LookY = 0.15 * Math.Sin(_time * 0.23);
		}
	}

	private void Act(MascotClip clip, double seconds)
	{
		MascotState state = State;
		bool flag = ((state == MascotState.Sit || state == MascotState.Dance) ? true : false);
		_afterAct = (flag ? State : MascotState.Idle);
		if (State == MascotState.Sleep)
		{
			_afterAct = MascotState.Idle;
		}
		if (State == MascotState.Act)
		{
			_afterAct = MascotState.Idle;
		}
		Enter(MascotState.Act);
		_actUntil = _time + seconds;
		Animator.Play(clip, restart: true);
	}

	private void Enter(MascotState state)
	{
		if (state != MascotState.Act && State == state && state != MascotState.Fall)
		{
			return;
		}
		MascotState state2 = State;
		State = state;
		StateTime = 0.0;
		switch (state)
		{
		case MascotState.Idle:
			Animator.Play(MascotClip.Idle);
			_decideAt = 4.0 + _rnd.NextDouble() * 9.0;
			if (_thinking)
			{
				Act(MascotClip.Think, 30.0);
			}
			break;
		case MascotState.Walk:
			Animator.Play(MascotClip.Walk);
			break;
		case MascotState.Sit:
			Animator.Play(MascotClip.Sit);
			_decideAt = 12.0 + _rnd.NextDouble() * 25.0;
			break;
		case MascotState.Sleep:
			Animator.Play(MascotClip.Sleep);
			break;
		case MascotState.Dance:
			Animator.Play(MascotClip.Dance);
			break;
		case MascotState.Held:
			Animator.Play(MascotClip.Held);
			break;
		case MascotState.Fall:
			if (state2 != MascotState.Fall)
			{
				_fallPeak = 0.0;
				_bonks = 0;
			}
			if (Animator.Clip != MascotClip.Jump)
			{
				Animator.Play(MascotClip.Fall);
			}
			break;
		case MascotState.Act:
			break;
		}
	}
}
