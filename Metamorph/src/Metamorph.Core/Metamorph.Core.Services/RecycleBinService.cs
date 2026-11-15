using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Metamorph.Core.Services;

public static class RecycleBinService
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct SHFILEOPSTRUCT
	{
		public nint hwnd;

		public uint wFunc;

		public string pFrom;

		public string? pTo;

		public ushort fFlags;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fAnyOperationsAborted;

		public nint hNameMappings;

		public string? lpszProgressTitle;
	}

	private const uint FO_DELETE = 3u;

	private const ushort FOF_SILENT = 4;

	private const ushort FOF_NOCONFIRMATION = 16;

	private const ushort FOF_ALLOWUNDO = 64;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

	public static bool Recycle(string path)
	{
		return Recycle(new string[1] { path });
	}

	public static bool Recycle(IEnumerable<string> paths)
	{
		List<string> list = paths.Where((string p) => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p))).Select(Path.GetFullPath).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		try
		{
			SHFILEOPSTRUCT sHFILEOPSTRUCT = default(SHFILEOPSTRUCT);
			sHFILEOPSTRUCT.wFunc = 3u;
			sHFILEOPSTRUCT.pFrom = string.Join("\0", list) + "\0";
			sHFILEOPSTRUCT.fFlags = 84;
			SHFILEOPSTRUCT lpFileOp = sHFILEOPSTRUCT;
			int num = SHFileOperation(ref lpFileOp);
			if (num != 0)
			{
				Log.Warn($"Recycle failed (0x{num:X}) for {list.Count} item(s)");
			}
			return num == 0 && !lpFileOp.fAnyOperationsAborted;
		}
		catch (Exception ex)
		{
			Log.Warn("Recycle failed: " + ex.Message);
			return false;
		}
	}
}
