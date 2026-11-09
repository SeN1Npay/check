using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Metamorph.App.Remote;

internal static class RemoteNative
{
	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;

		public int Width => Right - Left;

		public int Height => Bottom - Top;

		public override string ToString()
		{
			return $"{Left},{Top} {Width}x{Height}";
		}
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MONITORINFOEX
	{
		public int cbSize;

		public RECT rcMonitor;

		public RECT rcWork;

		public uint dwFlags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szDevice;
	}

	private delegate bool MonitorEnumProc(nint monitor, nint hdc, ref RECT rect, nint data);

	public sealed record Monitor(int Index, string Device, RECT Bounds, RECT Work, bool Primary, uint Dpi);

	private delegate bool EnumWindowsProc(nint hwnd, nint data);

	public sealed record WindowInfo(int Z, nint Hwnd, string Class, string Title, uint Pid, string Process, RECT Rect, string Flags);

	private struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public uint mouseData;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	private struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;
	}

	private struct INPUT
	{
		public uint type;

		public InputUnion u;
	}

	public struct POINT
	{
		public int X;

		public int Y;
	}

	private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
	{
		public long PerProcessUserTimeLimit;

		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public nuint MinimumWorkingSetSize;

		public nuint MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public nuint Affinity;

		public uint PriorityClass;

		public uint SchedulingClass;
	}

	private struct IO_COUNTERS
	{
		public ulong ReadOperationCount;

		public ulong WriteOperationCount;

		public ulong OtherOperationCount;

		public ulong ReadTransferCount;

		public ulong WriteTransferCount;

		public ulong OtherTransferCount;
	}

	private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
	{
		public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;

		public IO_COUNTERS IoInfo;

		public nuint ProcessMemoryLimit;

		public nuint JobMemoryLimit;

		public nuint PeakProcessMemoryUsed;

		public nuint PeakJobMemoryUsed;
	}

	private static readonly nint PerMonitorV2 = new IntPtr(-4);

	private const uint INPUT_MOUSE = 0u;

	private const uint INPUT_KEYBOARD = 1u;

	private const uint MOUSEEVENTF_LEFTDOWN = 2u;

	private const uint MOUSEEVENTF_LEFTUP = 4u;

	private const uint MOUSEEVENTF_RIGHTDOWN = 8u;

	private const uint MOUSEEVENTF_RIGHTUP = 16u;

	private const uint MOUSEEVENTF_MIDDLEDOWN = 32u;

	private const uint MOUSEEVENTF_MIDDLEUP = 64u;

	private const uint MOUSEEVENTF_WHEEL = 2048u;

	private const uint KEYEVENTF_EXTENDEDKEY = 1u;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const uint KEYEVENTF_UNICODE = 4u;

	private static nint _job;

	[DllImport("user32.dll")]
	private static extern nint SetThreadDpiAwarenessContext(nint context);

	public static T PerMonitor<T>(Func<T> action)
	{
		nint num = IntPtr.Zero;
		try
		{
			num = SetThreadDpiAwarenessContext(PerMonitorV2);
		}
		catch (EntryPointNotFoundException)
		{
		}
		try
		{
			return action();
		}
		finally
		{
			if (num != IntPtr.Zero)
			{
				try
				{
					SetThreadDpiAwarenessContext(num);
				}
				catch
				{
				}
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc proc, nint data);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool GetMonitorInfo(nint monitor, ref MONITORINFOEX info);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

	public static List<Monitor> Monitors()
	{
		return PerMonitor(delegate
		{
			List<Monitor> list = new List<Monitor>();
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(nint h, nint _, ref RECT _, nint _)
			{
				MONITORINFOEX mONITORINFOEX = default(MONITORINFOEX);
				mONITORINFOEX.cbSize = Marshal.SizeOf<MONITORINFOEX>();
				MONITORINFOEX info = mONITORINFOEX;
				if (GetMonitorInfo(h, ref info))
				{
					uint dpi = 96u;
					try
					{
						if (GetDpiForMonitor(h, 0, out var dpiX, out var _) == 0)
						{
							dpi = dpiX;
						}
					}
					catch
					{
					}
					list.Add(new Monitor(0, info.szDevice, info.rcMonitor, info.rcWork, (info.dwFlags & 1) != 0, dpi));
				}
				return true;
			}, IntPtr.Zero);
			return (from m in list
				orderby m.Primary descending, m.Bounds.Left
				select m).Select((Monitor m, int i) => m with
			{
				Index = i
			}).ToList();
		});
	}

	public static RECT VirtualScreen(List<Monitor> monitors)
	{
		RECT result;
		if (monitors.Count != 0)
		{
			result = default(RECT);
			result.Left = monitors.Min((Monitor m) => m.Bounds.Left);
			result.Top = monitors.Min((Monitor m) => m.Bounds.Top);
			result.Right = monitors.Max((Monitor m) => m.Bounds.Right);
			result.Bottom = monitors.Max((Monitor m) => m.Bounds.Bottom);
			return result;
		}
		result = default(RECT);
		result.Right = 1920;
		result.Bottom = 1080;
		return result;
	}

	public static Bitmap Capture(RECT area)
	{
		return PerMonitor(delegate
		{
			Bitmap bitmap = new Bitmap(Math.Max(1, area.Width), Math.Max(1, area.Height), PixelFormat.Format32bppArgb);
			using Graphics graphics = Graphics.FromImage(bitmap);
			graphics.CopyFromScreen(area.Left, area.Top, 0, 0, new Size(bitmap.Width, bitmap.Height), (CopyPixelOperation)1087111200);
			return bitmap;
		});
	}

	public static Bitmap Scale(Bitmap source, double scale)
	{
		int width = Math.Max(1, (int)Math.Round((double)source.Width * scale));
		int height = Math.Max(1, (int)Math.Round((double)source.Height * scale));
		Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.DrawImage(source, 0, 0, width, height);
		return bitmap;
	}

	public static byte[] Jpeg(Bitmap bmp, long quality)
	{
		ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First((ImageCodecInfo c) => c.FormatID == ImageFormat.Jpeg.Guid);
		using EncoderParameters encoderParameters = new EncoderParameters(1);
		encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, Math.Clamp(quality, 20L, 95L));
		using MemoryStream memoryStream = new MemoryStream();
		bmp.Save(memoryStream, encoder, encoderParameters);
		return memoryStream.ToArray();
	}

	public static byte[] Png(Bitmap bmp)
	{
		using MemoryStream memoryStream = new MemoryStream();
		bmp.Save(memoryStream, ImageFormat.Png);
		return memoryStream.ToArray();
	}

	public static byte[] Pixels(Bitmap bmp, out int stride)
	{
		BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			stride = Math.Abs(bitmapData.Stride);
			byte[] array = new byte[stride * bmp.Height];
			Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
			return array;
		}
		finally
		{
			bmp.UnlockBits(bitmapData);
		}
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc proc, nint data);

	[DllImport("user32.dll")]
	private static extern bool EnumChildWindows(nint parent, EnumWindowsProc proc, nint data);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hwnd, StringBuilder text, int max);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hwnd, StringBuilder text, int max);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hwnd);

	[DllImport("user32.dll")]
	private static extern bool IsZoomed(nint hwnd);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hwnd, out RECT rect);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr(nint hwnd, int index);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern nint GetParent(nint hwnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);

	public static string ClassOf(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		if (GetClassName(hwnd, stringBuilder, stringBuilder.Capacity) <= 0)
		{
			return "";
		}
		return stringBuilder.ToString();
	}

	private static string TitleOf(nint hwnd)
	{
		StringBuilder stringBuilder = new StringBuilder(512);
		if (GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity) <= 0)
		{
			return "";
		}
		return stringBuilder.ToString();
	}

	public static List<WindowInfo> Windows(bool all, bool desktopChildren)
	{
		return PerMonitor((Func<List<WindowInfo>>)delegate
		{
			Dictionary<uint, string> names = new Dictionary<uint, string>();
			nint foreground = GetForegroundWindow();
			List<WindowInfo> list = new List<WindowInfo>();
			int z = 0;
			EnumWindows(delegate(nint hwnd, nint _)
			{
				Add(hwnd, "");
				return true;
			}, IntPtr.Zero);
			if (desktopChildren)
			{
				foreach (nint parent in DesktopHosts())
				{
					EnumChildWindows(parent, delegate(nint child, nint _)
					{
						if (GetParent(child) == parent)
						{
							Add(child, $"  └ child of {ClassOf(parent)} 0x{((IntPtr)parent).ToInt64():X}: ");
						}
						return true;
					}, IntPtr.Zero);
				}
			}
			return list;
			void Add(nint hwnd, string prefix)
			{
				bool flag = IsWindowVisible(hwnd);
				DwmGetWindowAttribute(hwnd, 14, out var value2, 4);
				if (all || (flag && value2 == 0))
				{
					GetWindowThreadProcessId(hwnd, out var pid2);
					GetWindowRect(hwnd, out var rect);
					long num = ((IntPtr)GetWindowLongPtr(hwnd, -20)).ToInt64();
					List<string> list2 = new List<string>();
					if (hwnd == foreground)
					{
						list2.Add("FOREGROUND");
					}
					if (!flag)
					{
						list2.Add("hidden");
					}
					if (value2 != 0)
					{
						list2.Add("cloaked");
					}
					if ((num & 8) != 0L)
					{
						list2.Add("topmost");
					}
					if ((num & 0x80000) != 0L)
					{
						list2.Add("layered");
					}
					if ((num & 0x80) != 0L)
					{
						list2.Add("tool");
					}
					if ((num & 0x20) != 0L)
					{
						list2.Add("click-through");
					}
					if ((num & 0x8000000) != 0L)
					{
						list2.Add("noactivate");
					}
					if (IsIconic(hwnd))
					{
						list2.Add("minimized");
					}
					if (IsZoomed(hwnd))
					{
						list2.Add("maximized");
					}
					list.Add(new WindowInfo(z++, hwnd, prefix + ClassOf(hwnd), TitleOf(hwnd), pid2, ProcessName(pid2), rect, string.Join(" ", list2)));
				}
			}
			string ProcessName(uint pid)
			{
				if (names.TryGetValue(pid, out string value))
				{
					return value;
				}
				try
				{
					value = Process.GetProcessById((int)pid).ProcessName;
				}
				catch
				{
					value = "?";
				}
				return names[pid] = value;
			}
		});
	}

	private static IEnumerable<nint> DesktopHosts()
	{
		nint num = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
		if (num != IntPtr.Zero)
		{
			yield return num;
		}
		nint w = IntPtr.Zero;
		while (true)
		{
			nint num2;
			w = (num2 = FindWindowEx(IntPtr.Zero, w, "WorkerW", null));
			if (num2 != IntPtr.Zero)
			{
				yield return w;
				continue;
			}
			break;
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint count, INPUT[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT p);

	private static void Send(params INPUT[] inputs)
	{
		if (inputs.Length == 0 || SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != 0)
		{
			return;
		}
		throw new InvalidOperationException($"SendInput failed (error {Marshal.GetLastWin32Error()}); the target window may be elevated (admin)");
	}

	private static INPUT Mouse(uint flags, uint data = 0u)
	{
		INPUT result = default(INPUT);
		result.type = 0u;
		result.u = new InputUnion
		{
			mi = new MOUSEINPUT
			{
				dwFlags = flags,
				mouseData = data
			}
		};
		return result;
	}

	private static INPUT Key(ushort vk, bool up, bool extended)
	{
		INPUT result = default(INPUT);
		result.type = 1u;
		result.u = new InputUnion
		{
			ki = new KEYBDINPUT
			{
				wVk = vk,
				dwFlags = ((up ? 2u : 0u) | (extended ? 1u : 0u))
			}
		};
		return result;
	}

	public static POINT Cursor()
	{
		POINT p;
		return PerMonitor(() => (!GetCursorPos(out p)) ? default(POINT) : p);
	}

	public static void MoveTo(int x, int y)
	{
		PerMonitor(() => SetCursorPos(x, y));
	}

	public static void Click(int x, int y, string button, int count)
	{
		MoveTo(x, y);
		Thread.Sleep(40);
		(uint, uint) tuple = ((button == "right") ? (8u, 16u) : ((!(button == "middle")) ? (2u, 4u) : (32u, 64u)));
		(uint, uint) tuple2 = tuple;
		uint item = tuple2.Item1;
		uint item2 = tuple2.Item2;
		for (int i = 0; i < Math.Clamp(count, 1, 3); i++)
		{
			Send(Mouse(item), Mouse(item2));
			Thread.Sleep(60);
		}
	}

	public static void Scroll(int x, int y, int notches)
	{
		MoveTo(x, y);
		Thread.Sleep(30);
		Send(Mouse(2048u, (uint)(notches * 120)));
	}

	public static void Drag(int x1, int y1, int x2, int y2)
	{
		MoveTo(x1, y1);
		Thread.Sleep(50);
		Send(Mouse(2u));
		for (int i = 1; i <= 12; i++)
		{
			MoveTo(x1 + (x2 - x1) * i / 12, y1 + (y2 - y1) * i / 12);
			Thread.Sleep(20);
		}
		Send(Mouse(4u));
	}

	public static void TypeText(string text)
	{
		List<INPUT> list = new List<INPUT>();
		string text2 = text.Replace("\r\n", "\n");
		foreach (char c in text2)
		{
			if (c == '\n')
			{
				list.Add(Key(13, up: false, extended: false));
				list.Add(Key(13, up: true, extended: false));
				continue;
			}
			list.Add(new INPUT
			{
				type = 1u,
				u = new InputUnion
				{
					ki = new KEYBDINPUT
					{
						wScan = c,
						dwFlags = 4u
					}
				}
			});
			list.Add(new INPUT
			{
				type = 1u,
				u = new InputUnion
				{
					ki = new KEYBDINPUT
					{
						wScan = c,
						dwFlags = 6u
					}
				}
			});
		}
		foreach (INPUT[] item in list.Chunk(64))
		{
			Send(item);
			Thread.Sleep(10);
		}
	}

	public static void Hotkey(string combo)
	{
		List<(ushort Vk, bool Extended)> source = (from k in combo.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			select VirtualKey(k) ?? throw new ArgumentException("Unknown key: " + k)).ToList();
		Send(source.Select<(ushort, bool), INPUT>(((ushort Vk, bool Extended) k) => Key(k.Vk, up: false, k.Extended)).Concat(from k in Enumerable.Reverse(source)
			select Key(k.Vk, up: true, k.Extended)).ToArray());
	}

	public static (ushort Vk, bool Extended)? VirtualKey(string name)
	{
		if (name.Length == 1 && char.IsLetterOrDigit(name[0]))
		{
			return (char.ToUpperInvariant(name[0]), false);
		}
		int length = name.Length;
		bool flag = (uint)(length - 2) <= 1u;
		if (flag && name[0] == 'f')
		{
			if (int.TryParse(name.Substring(1, name.Length - 1), out var result) && result >= 1 && result <= 24)
			{
				return ((ushort)(111 + result), false);
			}
		}
		switch (name)
		{
		case "ctrl":
		case "control":
			return (17, false);
		case "alt":
			return (18, false);
		case "shift":
			return (16, false);
		case "meta":
		case "windows":
		case "win":
			return (91, true);
		case "enter":
		case "return":
			return (13, false);
		case "esc":
		case "escape":
			return (27, false);
		case "tab":
			return (9, false);
		case "space":
			return (32, false);
		case "backspace":
			return (8, false);
		case "del":
		case "delete":
			return (46, true);
		case "ins":
		case "insert":
			return (45, true);
		case "home":
			return (36, true);
		case "end":
			return (35, true);
		case "pgup":
		case "pageup":
			return (33, true);
		case "pgdn":
		case "pagedown":
			return (34, true);
		case "left":
			return (37, true);
		case "up":
			return (38, true);
		case "right":
			return (39, true);
		case "down":
			return (40, true);
		case "prtsc":
		case "printscreen":
			return (44, true);
		case "apps":
		case "menu":
			return (93, true);
		case "volumeup":
			return (175, true);
		case "volumedown":
			return (174, true);
		case "mute":
			return (173, true);
		case "playpause":
			return (179, true);
		case "comma":
			return (188, false);
		case "dot":
		case "period":
			return (190, false);
		case "minus":
			return (189, false);
		case "plus":
		case "equals":
			return (187, false);
		default:
			return null;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

	public static bool ExcludeFromCapture(nint hwnd)
	{
		try
		{
			return SetWindowDisplayAffinity(hwnd, 17u);
		}
		catch
		{
			return false;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern nint CreateJobObject(nint attributes, string? name);

	[DllImport("kernel32.dll")]
	private static extern bool SetInformationJobObject(nint job, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);

	[DllImport("kernel32.dll")]
	private static extern bool AssignProcessToJobObject(nint job, nint process);

	public static void KillWithUs(int pid)
	{
		if (pid <= 0)
		{
			return;
		}
		try
		{
			if (_job == IntPtr.Zero)
			{
				nint job = CreateJobObject(IntPtr.Zero, null);
				JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
				{
					BasicLimitInformation = 
					{
						LimitFlags = 8192u
					}
				};
				SetInformationJobObject(job, 9, ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());
				_job = job;
			}
			using Process process = Process.GetProcessById(pid);
			AssignProcessToJobObject(_job, process.Handle);
		}
		catch
		{
		}
	}
}
