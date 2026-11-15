using System;
using System.IO;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;

namespace Metamorph.App.Bar;

public sealed class DiskModule : LabeledModule
{
	public override int IntervalTicks => 60;

	public DiskModule(BarContext context)
		: base(context, "disk")
	{
		Clickable(delegate
		{
			Settings.Open(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
		});
	}

	public override void Update()
	{
		(string, double, double)? tuple = SystemStats.Disk();
		if (tuple.HasValue)
		{
			(string, double, double) valueOrDefault = tuple.GetValueOrDefault();
			SetValue($"{valueOrDefault.Item2:0}%", (valueOrDefault.Item2 >= 90.0) ? Context.Alert : null);
			base.TextView.ToolTip = Loc.F("{0} свободно {1:0.0} ГБ", valueOrDefault.Item1, valueOrDefault.Item3);
		}
	}
}
