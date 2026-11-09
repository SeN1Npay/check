using System;
using System.Globalization;
using Metamorph.Core.Native;

namespace Metamorph.App.Bar;

public sealed class LayoutModule : LabeledModule
{
	public override int IntervalTicks => 1;

	public LayoutModule(BarContext context)
		: base(context, "")
	{
	}

	public override void Update()
	{
		uint processId;
		nint keyboardLayout = NativeMethods.GetKeyboardLayout(NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out processId));
		if (keyboardLayout != IntPtr.Zero)
		{
			int culture = (int)(((IntPtr)keyboardLayout).ToInt64() & 0xFFFF);
			string text;
			try
			{
				text = CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName;
			}
			catch (CultureNotFoundException)
			{
				text = culture.ToString("X4");
			}
			SetValue(text.ToLowerInvariant(), Context.Accent);
		}
	}
}
