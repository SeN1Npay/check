using System;
using System.Threading.Tasks;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public sealed class ScriptModule : LabeledModule
{
	private readonly ScriptModuleSettings _s;

	private DateTime _lastRun;

	private bool _busy;

	public override int IntervalTicks => 2;

	public ScriptModule(BarContext context, ScriptModuleSettings settings) : base(context, settings.Label ?? "")
	{
		ScriptModuleSettings settings2 = settings;
		ScriptModule scriptModule = this;
		_s = settings2;
		SetValue("…", context.Muted);
		if (!string.IsNullOrWhiteSpace(settings2.OnClick))
		{
			Clickable(delegate
			{
				ScriptRunner.Fire(settings2.OnClick);
				scriptModule._lastRun = default(DateTime);
			});
		}
	}

	public override void Update()
	{
		if (!_busy && !(DateTime.UtcNow - _lastRun < TimeSpan.FromSeconds(Math.Max(1, _s.IntervalSeconds))))
		{
			_busy = true;
			_lastRun = DateTime.UtcNow;
			RunAsync();
		}
	}

	private async Task RunAsync()
	{
		try
		{
			string text = ScriptRunner.FirstLine(await ScriptRunner.RunAsync(_s.Command, TimeSpan.FromSeconds(Math.Clamp(_s.IntervalSeconds, 2, 30))));
			SetVisible(text.Length > 0);
			SetValue(text);
		}
		catch (Exception ex)
		{
			SetValue("!", Context.Alert);
			base.TextView.ToolTip = ex.Message;
		}
		finally
		{
			_busy = false;
		}
	}
}
