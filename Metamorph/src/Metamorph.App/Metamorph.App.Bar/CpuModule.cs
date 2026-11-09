using System;
using Metamorph.Core.Native;

namespace Metamorph.App.Bar;

public sealed class CpuModule : LabeledModule
{
	private long _idle;

	private long _kernel;

	private long _user;

	private bool _hasSample;

	public CpuModule(BarContext context)
		: base(context, "cpu")
	{
		SetValue(" --%");
	}

	public override void Update()
	{
		if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
		{
			return;
		}
		if (_hasSample)
		{
			long num = kernelTime - _kernel + (userTime - _user);
			if (num > 0)
			{
				double num2 = Math.Clamp(100.0 * (double)(num - (idleTime - _idle)) / (double)num, 0.0, 100.0);
				SetValue($"{num2,3:0}%", (num2 >= 90.0) ? Context.Alert : null);
			}
		}
		long idle = idleTime;
		long kernel = kernelTime;
		long user = userTime;
		_idle = idle;
		_kernel = kernel;
		_user = user;
		_hasSample = true;
	}
}
