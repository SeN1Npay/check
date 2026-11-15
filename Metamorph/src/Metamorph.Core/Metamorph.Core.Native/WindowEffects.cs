using System;
using System.Runtime.InteropServices;

namespace Metamorph.Core.Native;

public static class WindowEffects
{
	public static bool EnableBlurBehind(nint hwnd)
	{
		NativeMethods.AccentPolicy accentPolicy = default(NativeMethods.AccentPolicy);
		accentPolicy.AccentState = NativeMethods.AccentState.EnableBlurBehind;
		NativeMethods.AccentPolicy structure = accentPolicy;
		int num = Marshal.SizeOf<NativeMethods.AccentPolicy>();
		nint num2 = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.StructureToPtr(structure, num2, fDeleteOld: false);
			NativeMethods.WindowCompositionAttributeData windowCompositionAttributeData = default(NativeMethods.WindowCompositionAttributeData);
			windowCompositionAttributeData.Attribute = 19;
			windowCompositionAttributeData.Data = num2;
			windowCompositionAttributeData.SizeOfData = num;
			NativeMethods.WindowCompositionAttributeData data = windowCompositionAttributeData;
			return NativeMethods.SetWindowCompositionAttribute(hwnd, ref data) != 0;
		}
		catch (EntryPointNotFoundException)
		{
			return false;
		}
		finally
		{
			Marshal.FreeHGlobal(num2);
		}
	}

	public static void UseDarkTitleBar(nint hwnd, bool dark)
	{
		int value = (dark ? 1 : 0);
		try
		{
			if (NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref value, 4) != 0)
			{
				NativeMethods.DwmSetWindowAttribute(hwnd, 19, ref value, 4);
			}
		}
		catch (DllNotFoundException)
		{
		}
		catch (EntryPointNotFoundException)
		{
		}
	}

	public static void MakeToolWindow(nint hwnd, bool noActivate)
	{
		long num = NativeMethods.GetWindowLong(hwnd, -20) | 0x80;
		if (noActivate)
		{
			num |= 0x8000000;
		}
		NativeMethods.SetWindowLong(hwnd, -20, num);
	}
}
