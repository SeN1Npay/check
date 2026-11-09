using System;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;

namespace Metamorph.App.Bar;

public sealed class VolumeModule : LabeledModule
{
	private readonly CoreAudio _audio = new CoreAudio();

	public override int IntervalTicks => 2;

	public VolumeModule(BarContext context)
		: base(context, "vol")
	{
		Clickable(delegate
		{
			_audio.ToggleMute();
			Update();
		}, delegate
		{
			Settings.Open("ms-settings:sound");
		}, null, delegate(int step)
		{
			(float, bool)? tuple = _audio.Read();
			if (tuple.HasValue)
			{
				(float, bool) valueOrDefault = tuple.GetValueOrDefault();
				_audio.SetLevel(Math.Clamp(valueOrDefault.Item1 + (float)step * 0.05f, 0f, 1f));
			}
			Update();
		});
	}

	public override void Update()
	{
		(float, bool)? tuple = _audio.Read();
		if (tuple.HasValue)
		{
			(float, bool) valueOrDefault = tuple.GetValueOrDefault();
			if (valueOrDefault.Item2)
			{
				SetValue(Loc.T("выкл"), Context.Muted);
				return;
			}
			SetValue($"{Math.Round(valueOrDefault.Item1 * 100f):0}%");
		}
		else
		{
			SetValue("--");
		}
	}
}
