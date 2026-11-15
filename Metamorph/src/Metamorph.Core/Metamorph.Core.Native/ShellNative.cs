using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Metamorph.Core.Native;

public static class ShellNative
{
	public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public struct POINT
	{
		public int X;

		public int Y;
	}

	public struct MONITORINFO
	{
		public int cbSize;

		public NativeMethods.RECT rcMonitor;

		public NativeMethods.RECT rcWork;

		public uint dwFlags;

		public static MONITORINFO Create()
		{
			MONITORINFO result = default(MONITORINFO);
			result.cbSize = Marshal.SizeOf<MONITORINFO>();
			return result;
		}
	}

	public struct BITMAP
	{
		public int bmType;

		public int bmWidth;

		public int bmHeight;

		public int bmWidthBytes;

		public ushort bmPlanes;

		public ushort bmBitsPixel;

		public nint bmBits;
	}

	public struct BITMAPINFOHEADER
	{
		public uint biSize;

		public int biWidth;

		public int biHeight;

		public ushort biPlanes;

		public ushort biBitCount;

		public uint biCompression;

		public uint biSizeImage;

		public int biXPelsPerMeter;

		public int biYPelsPerMeter;

		public uint biClrUsed;

		public uint biClrImportant;
	}

	public enum SIGDN : uint
	{
		NORMALDISPLAY = 0u,
		FILESYSPATH = 2147844096u
	}

	[Flags]
	public enum SIIGBF
	{
		ResizeToFit = 0,
		BiggerSizeOk = 1,
		MemoryOnly = 2,
		IconOnly = 4,
		ThumbnailOnly = 8,
		InCacheOnly = 0x10
	}

	public struct SIZE
	{
		public int cx;

		public int cy;
	}

	[ComImport]
	[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellItem
	{
		void BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);

		void GetParent(out IShellItem ppsi);

		void GetDisplayName(SIGDN sigdnName, out nint ppszName);

		void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

		void Compare(IShellItem psi, uint hint, out int piOrder);
	}

	[ComImport]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellItemImageFactory
	{
		[PreserveSig]
		int GetImage(SIZE size, SIIGBF flags, out nint phbm);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct PROPERTYKEY
	{
		public Guid fmtid;

		public uint pid;
	}

	[StructLayout(LayoutKind.Explicit, Size = 24)]
	public struct PROPVARIANT
	{
		[FieldOffset(0)]
		public ushort vt;

		[FieldOffset(8)]
		public nint pointerValue;
	}

	[ComImport]
	[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPropertyStore
	{
		[PreserveSig]
		int GetCount(out uint count);

		[PreserveSig]
		int GetAt(uint index, out PROPERTYKEY key);

		[PreserveSig]
		int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);

		[PreserveSig]
		int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);

		[PreserveSig]
		int Commit();
	}

	[ComImport]
	[Guid("00021401-0000-0000-C000-000000000046")]
	public class ShellLinkCoClass
	{
	}

	[ComImport]
	[Guid("000214F9-0000-0000-C000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellLinkW
	{
		void GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, nint findData, uint flags);

		void GetIDList(out nint idList);

		void SetIDList(nint idList);

		void GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

		void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

		void GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);

		void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);

		void GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);

		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);

		void GetHotkey(out short hotkey);

		void SetHotkey(short hotkey);

		void GetShowCmd(out int showCmd);

		void SetShowCmd(int showCmd);

		void GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);

		void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

		void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relPath, uint reserved);

		void Resolve(nint hwnd, uint flags);

		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
	}

	[ComImport]
	[Guid("0000010b-0000-0000-C000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPersistFile
	{
		void GetClassID(out Guid classId);

		[PreserveSig]
		int IsDirty();

		void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);

		void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);

		void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);

		void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
	}

	public struct SHQUERYRBINFO
	{
		public int cbSize;

		public long i64Size;

		public long i64NumItems;

		public static SHQUERYRBINFO Create()
		{
			SHQUERYRBINFO result = default(SHQUERYRBINFO);
			result.cbSize = Marshal.SizeOf<SHQUERYRBINFO>();
			return result;
		}
	}

	public const uint GW_OWNER = 4u;

	public const long WS_EX_APPWINDOW = 262144L;

	public const int DWMWA_CLOAKED = 14;

	public const int WM_SYSCOMMAND = 274;

	public const int WM_GETICON = 127;

	public const int SC_MINIMIZE = 61472;

	public const int SC_RESTORE = 61728;

	public const int SC_CLOSE = 61536;

	public const int ICON_SMALL = 0;

	public const int ICON_BIG = 1;

	public const int ICON_SMALL2 = 2;

	public const int GCLP_HICON = -14;

	public const int GCLP_HICONSM = -34;

	public const int SW_RESTORE = 9;

	public const int HSHELL_WINDOWCREATED = 1;

	public const int HSHELL_WINDOWDESTROYED = 2;

	public const int HSHELL_WINDOWACTIVATED = 4;

	public const int HSHELL_REDRAW = 6;

	public const int HSHELL_WINDOWREPLACED = 13;

	public const int HSHELL_MONITORCHANGED = 16;

	public const int HSHELL_HIGHBIT = 32768;

	public const int HSHELL_FLASH = 32774;

	public const int HSHELL_RUDEAPPACTIVATED = 32772;

	public const byte VK_LWIN = 91;

	public const uint KEYEVENTF_KEYUP = 2u;

	public const uint MONITOR_DEFAULTTONEAREST = 2u;

	public const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

	public const ushort VT_LPWSTR = 31;

	public static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY
	{
		fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
		pid = 5u
	};

	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

	[DllImport("user32.dll")]
	public static extern nint GetWindow(nint hWnd, uint cmd);

	[DllImport("user32.dll")]
	public static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool IsWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool IsZoomed(nint hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetClassName(nint hWnd, StringBuilder name, int maxCount);

	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(nint hWnd, out NativeMethods.RECT rect);

	[DllImport("user32.dll")]
	public static extern bool GetCursorPos(out POINT point);

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern void SwitchToThisWindow(nint hWnd, bool altTab);

	[DllImport("user32.dll")]
	public static extern nint GetLastActivePopup(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool ShowWindowAsync(nint hWnd, int cmdShow);

	[DllImport("user32.dll")]
	public static extern bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);

	[DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
	public static extern nint SendMessageTimeout(nint hWnd, int msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

	[DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
	private static extern nint GetClassLongPtr64(nint hWnd, int index);

	[DllImport("user32.dll", EntryPoint = "GetClassLongW")]
	private static extern uint GetClassLong32(nint hWnd, int index);

	public static nint GetClassLongPtr(nint hWnd, int index)
	{
		if (IntPtr.Size != 8)
		{
			return new IntPtr(GetClassLong32(hWnd, index));
		}
		return GetClassLongPtr64(hWnd, index);
	}

	[DllImport("user32.dll")]
	public static extern bool RegisterShellHookWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool DeregisterShellHookWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool AllowSetForegroundWindow(uint processId);

	[DllImport("user32.dll")]
	public static extern void keybd_event(byte vk, byte scan, uint flags, nuint extraInfo);

	[DllImport("user32.dll")]
	public static extern nint MonitorFromWindow(nint hWnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern nint OpenProcess(uint access, bool inherit, uint processId);

	[DllImport("kernel32.dll")]
	public static extern bool CloseHandle(nint handle);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
	public static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);

	[DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
	public static extern int GetObject(nint h, int size, out BITMAP bitmap);

	[DllImport("gdi32.dll")]
	public static extern int GetDIBits(nint hdc, nint hbm, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);

	[DllImport("gdi32.dll")]
	public static extern nint CreateCompatibleDC(nint hdc);

	[DllImport("gdi32.dll")]
	public static extern bool DeleteDC(nint hdc);

	[DllImport("gdi32.dll")]
	public static extern bool DeleteObject(nint handle);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHCreateItemFromParsingName(string path, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

	[DllImport("shell32.dll")]
	public static extern int SHGetPropertyStoreForWindow(nint hwnd, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object propertyStore);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHGetPropertyStoreFromParsingName(string path, nint pbc, int flags, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object propertyStore);

	[DllImport("ole32.dll")]
	public static extern int PropVariantClear(ref PROPVARIANT value);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHEmptyRecycleBin(nint hwnd, string? rootPath, uint flags);
}
