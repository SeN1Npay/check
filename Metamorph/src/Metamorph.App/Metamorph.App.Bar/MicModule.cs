using System;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class MicModule : LabeledModule
{
	private readonly CoreAudio _mic = new CoreAudio(capture: true);

	private int _tick;

	private PrivacySnapshot _privacy = PrivacySnapshot.Empty;

	public override int IntervalTicks => 2;

	public MicModule(BarContext context)
		: base(context, "mic")
	{
		Clickable(delegate
		{
			_mic.ToggleMute();
			Update();
		}, delegate
		{
			Settings.Open("ms-settings:sound");
		}, delegate
		{
			Settings.Open("ms-settings:privacy-microphone");
		}, delegate(int step)
		{
			(float, bool)? tuple = _mic.Read();
			if (tuple.HasValue)
			{
				(float, bool) valueOrDefault = tuple.GetValueOrDefault();
				_mic.SetLevel(valueOrDefault.Item1 + (float)step * 0.05f);
			}
			Update();
		});
	}

	public override void Update()
	{
		if (_tick++ % 4 == 0)
		{
			_privacy = PrivacyUsage.Snapshot((string n) => n.Equals("Metamorph", StringComparison.OrdinalIgnoreCase));
		}
		(float, bool)? tuple = _mic.Read();
		if (tuple.HasValue)
		{
			(float, bool) valueOrDefault = tuple.GetValueOrDefault();
			string text = ((_privacy.Microphone.Count > 0) ? " ●" : "");
			if (valueOrDefault.Item2)
			{
				SetValue(Loc.T("выкл") + text, Context.Alert);
			}
			else
			{
				SetValue($"{Math.Round(valueOrDefault.Item1 * 100f):0}%{text}", (text.Length > 0) ? Context.Yellow : null);
			}
			base.TextView.ToolTip = ((_privacy.Microphone.Count > 0) ? Loc.F("Микрофон используют: {0}", string.Join(", ", _privacy.Microphone)) : Loc.T("Клик — выключить/включить микрофон, колесо — уровень"));
		}
		else
		{
			SetValue(Loc.T("нет"), Context.Muted);
			base.TextView.ToolTip = Loc.T("Микрофон не найден");
		}
	}
}
