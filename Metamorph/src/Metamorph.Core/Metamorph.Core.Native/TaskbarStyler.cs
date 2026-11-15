using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Metamorph.Core.Colors;
using Metamorph.Core.Models;

namespace Metamorph.Core.Native;

public static class TaskbarStyler
{
	public static IEnumerable<nint> Taskbars()
	{
		uint ownPid = (uint)Environment.ProcessId;
		string[] array = new string[2] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
		foreach (string cls in array)
		{
			nint hwnd = IntPtr.Zero;
			while (true)
			{
				nint num;
				hwnd = (num = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, cls, null));
				if (num == IntPtr.Zero)
				{
					break;
				}
				NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
				if (processId != ownPid)
				{
					yield return hwnd;
				}
			}
		}
	}

	public static (NativeMethods.AccentState State, uint Color) Policy(TaskbarStyle style, Rgba tint, double opacity)
	{
		byte b = (byte)Math.Clamp(Math.Round(opacity * 255.0), 0.0, 255.0);
		return style switch
		{
			TaskbarStyle.Transparent => (State: NativeMethods.AccentState.EnableTransparentGradient, Color: 0u), 
			TaskbarStyle.Blur => (State: NativeMethods.AccentState.EnableBlurBehind, Color: tint.WithAlpha(b).ToAbgr()), 
			TaskbarStyle.Acrylic => (State: NativeMethods.AccentState.EnableAcrylicBlurBehind, Color: tint.WithAlpha(Math.Max(b, (byte)1)).ToAbgr()), 
			TaskbarStyle.Tinted => (State: NativeMethods.AccentState.EnableTransparentGradient, Color: tint.WithAlpha(b).ToAbgr()), 
			_ => (State: NativeMethods.AccentState.Disabled, Color: 0u), 
		};
	}

	public static void Apply(TaskbarStyle style, Rgba tint, double opacity)
	{
		var (state, abgr) = Policy(style, tint, opacity);
		foreach (nint item in Taskbars())
		{
			SetAccent(item, state, abgr);
		}
	}

	public static void Reset()
	{
		foreach (nint item in Taskbars())
		{
			SetAccent(item, NativeMethods.AccentState.Disabled, 0u);
			Win32.SendMessageTimeout(item, 794, IntPtr.Zero, IntPtr.Zero, 2u, 300u, out var _);
		}
	}

	public static bool SetAccent(nint hwnd, NativeMethods.AccentState state, uint abgr)
	{
		NativeMethods.AccentPolicy accentPolicy = default(NativeMethods.AccentPolicy);
		accentPolicy.AccentState = state;
		accentPolicy.AccentFlags = ((state != 0) ? 2 : 0);
		accentPolicy.GradientColor = abgr;
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
}
