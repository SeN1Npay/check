using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Metamorph.Core.Native;

namespace Metamorph.App.Bar;

public sealed class WindowTitleModule : LabeledModule
{
	private const int MaxLength = 70;

	private readonly uint _ownPid = (uint)Environment.ProcessId;

	public override int IntervalTicks => 1;

	public WindowTitleModule(BarContext context)
		: base(context, "")
	{
		((TextBlock)View).TextTrimming = TextTrimming.CharacterEllipsis;
	}

	public override void Update()
	{
		nint foregroundWindow = NativeMethods.GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return;
		}
		NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
		if (processId != _ownPid)
		{
			StringBuilder stringBuilder = new StringBuilder(NativeMethods.GetWindowTextLength(foregroundWindow) + 1);
			NativeMethods.GetWindowText(foregroundWindow, stringBuilder, stringBuilder.Capacity);
			string text = stringBuilder.ToString().Trim();
			if (text.Length == 0 || text == "Program Manager")
			{
				text = "~";
			}
			if (text.Length > 70)
			{
				text = text.Substring(0, 69) + "…";
			}
			SetValue(text);
		}
	}
}
