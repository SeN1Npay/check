using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;

namespace Metamorph.App.Bar;

public sealed class TimerModule : LabeledModule
{
	private static FocusTimer? Timer
	{
		get
		{
			if (!(Application.Current is App app))
			{
				return null;
			}
			return app.Focus;
		}
	}

	public override int IntervalTicks => 1;

	public TimerModule(BarContext context)
		: base(context, "")
	{
		Clickable(Toggle, Menu, delegate
		{
			Timer?.Cancel();
		});
	}

	private void Toggle()
	{
		FocusTimer timer = Timer;
		if (timer != null)
		{
			if (!timer.Running)
			{
				timer.StartPomodoro();
			}
			else if (timer.Paused)
			{
				timer.Resume();
			}
			else
			{
				timer.Pause();
			}
			Update();
		}
	}

	private void Menu()
	{
		FocusTimer t = Timer;
		if (t == null)
		{
			return;
		}
		List<(string, Action)> list = new List<(string, Action)>();
		if (t.Running)
		{
			list.Add((Loc.T(t.Paused ? "Продолжить таймер" : "Пауза таймера"), Toggle));
			list.Add((Loc.T("Отменить таймер"), delegate
			{
				t.Cancel();
				Update();
			}));
		}
		int[] array = new int[4] { 5, 10, 15, 30 };
		foreach (int num in array)
		{
			int i = num;
			list.Add((Loc.F("Таймер на {0} мин", i), delegate
			{
				t.Start(TimeSpan.FromMinutes(i));
				Update();
			}));
		}
		list.Add((Loc.T("Помодоро 25/5"), delegate
		{
			t.StartPomodoro();
			Update();
		}));
		BarPopup.ShowMenu(View, Context, list);
	}

	public override void Update()
	{
		FocusTimer timer = Timer;
		if (timer == null || !timer.Running)
		{
			SetValue("⏱", Context.Muted);
			base.TextView.ToolTip = Loc.T("Клик — помодоро 25 минут, правый клик — таймеры");
			return;
		}
		string text = ((timer.Phase == FocusPhase.Break) ? "☕" : "⏱");
		SetValue(text + " " + FocusTimer.Format(timer.Remaining) + (timer.Paused ? " ⏸" : ""), timer.Paused ? Context.Muted : Context.Accent);
		TextBlock textView = base.TextView;
		string label = timer.Label;
		textView.ToolTip = ((label != null && label.Length > 0) ? (label + " — ") : "") + Loc.T("клик — пауза, средний — отменить, правый — меню");
	}
}
