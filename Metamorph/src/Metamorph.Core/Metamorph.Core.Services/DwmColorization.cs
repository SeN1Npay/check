using System;
using Metamorph.Core.Colors;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public static class DwmColorization
{
	public static bool TrySet(Rgba color)
	{
		try
		{
			if (NativeMethods.DwmGetColorizationParameters(out var parameters) != 0)
			{
				return false;
			}
			parameters.clrColor = color.ToArgb();
			parameters.clrAfterGlow = color.ToArgb();
			return NativeMethods.DwmSetColorizationParameters(ref parameters, 0u) == 0;
		}
		catch (Exception ex) when (((ex is EntryPointNotFoundException || ex is DllNotFoundException) ? 1 : 0) != 0)
		{
			Log.Warn("DWM colorization недоступна: " + ex.Message);
			return false;
		}
	}
}
