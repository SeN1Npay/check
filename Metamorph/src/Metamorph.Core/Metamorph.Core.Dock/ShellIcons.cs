using System;
using System.Runtime.InteropServices;
using System.Text;
using Metamorph.Core.Native;
using Metamorph.Core.Services;

namespace Metamorph.Core.Dock;

public static class ShellIcons
{
	private static readonly Guid ImageFactoryId = typeof(ShellNative.IShellItemImageFactory).GUID;

	private static readonly Guid ShellItemId = typeof(ShellNative.IShellItem).GUID;

	public static IconPixels? TryGetIcon(string parsingName, int size)
	{
		if (string.IsNullOrWhiteSpace(parsingName))
		{
			return null;
		}
		object ppv = null;
		nint phbm = IntPtr.Zero;
		try
		{
			Guid riid = ImageFactoryId;
			if (ShellNative.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref riid, out ppv) != 0 || !(ppv is ShellNative.IShellItemImageFactory shellItemImageFactory))
			{
				return null;
			}
			if (shellItemImageFactory.GetImage(new ShellNative.SIZE
			{
				cx = size,
				cy = size
			}, ShellNative.SIIGBF.BiggerSizeOk | ShellNative.SIIGBF.IconOnly, out phbm) != 0 || phbm == IntPtr.Zero)
			{
				return null;
			}
			return FromHBitmap(phbm);
		}
		catch (Exception ex)
		{
			Log.Warn("Иконка для '" + parsingName + "': " + ex.Message);
			return null;
		}
		finally
		{
			if (phbm != IntPtr.Zero)
			{
				ShellNative.DeleteObject(phbm);
			}
			if (ppv != null && Marshal.IsComObject(ppv))
			{
				Marshal.ReleaseComObject(ppv);
			}
		}
	}

	public static IconPixels? TryGetThumbnail(string path, int size)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		object ppv = null;
		nint phbm = IntPtr.Zero;
		try
		{
			Guid riid = ImageFactoryId;
			if (ShellNative.SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out ppv) != 0 || !(ppv is ShellNative.IShellItemImageFactory shellItemImageFactory))
			{
				return null;
			}
			if (shellItemImageFactory.GetImage(new ShellNative.SIZE
			{
				cx = size,
				cy = size
			}, ShellNative.SIIGBF.BiggerSizeOk | ShellNative.SIIGBF.ThumbnailOnly, out phbm) != 0 || phbm == IntPtr.Zero)
			{
				return null;
			}
			return FromHBitmap(phbm);
		}
		catch (Exception ex)
		{
			Log.Warn("Миниатюра для '" + path + "': " + ex.Message);
			return null;
		}
		finally
		{
			if (phbm != IntPtr.Zero)
			{
				ShellNative.DeleteObject(phbm);
			}
			if (ppv != null && Marshal.IsComObject(ppv))
			{
				Marshal.ReleaseComObject(ppv);
			}
		}
	}

	public static string? TryGetDisplayName(string parsingName)
	{
		object ppv = null;
		try
		{
			Guid riid = ShellItemId;
			if (ShellNative.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref riid, out ppv) != 0 || !(ppv is ShellNative.IShellItem shellItem))
			{
				return null;
			}
			shellItem.GetDisplayName(ShellNative.SIGDN.NORMALDISPLAY, out var ppszName);
			if (ppszName == IntPtr.Zero)
			{
				return null;
			}
			try
			{
				return Marshal.PtrToStringUni(ppszName);
			}
			finally
			{
				Marshal.FreeCoTaskMem(ppszName);
			}
		}
		catch
		{
			return null;
		}
		finally
		{
			if (ppv != null && Marshal.IsComObject(ppv))
			{
				Marshal.ReleaseComObject(ppv);
			}
		}
	}

	public static IconPixels? FromHBitmap(nint hbitmap)
	{
		if (ShellNative.GetObject(hbitmap, Marshal.SizeOf<ShellNative.BITMAP>(), out var bitmap) == 0 || bitmap.bmWidth <= 0 || bitmap.bmHeight == 0)
		{
			return null;
		}
		int bmWidth = bitmap.bmWidth;
		int num = Math.Abs(bitmap.bmHeight);
		ShellNative.BITMAPINFOHEADER bITMAPINFOHEADER = default(ShellNative.BITMAPINFOHEADER);
		bITMAPINFOHEADER.biSize = (uint)Marshal.SizeOf<ShellNative.BITMAPINFOHEADER>();
		bITMAPINFOHEADER.biWidth = bmWidth;
		bITMAPINFOHEADER.biHeight = -num;
		bITMAPINFOHEADER.biPlanes = 1;
		bITMAPINFOHEADER.biBitCount = 32;
		bITMAPINFOHEADER.biCompression = 0u;
		ShellNative.BITMAPINFOHEADER info = bITMAPINFOHEADER;
		byte[] array = new byte[bmWidth * num * 4];
		nint hdc = ShellNative.CreateCompatibleDC(IntPtr.Zero);
		try
		{
			if (ShellNative.GetDIBits(hdc, hbitmap, 0u, (uint)num, array, ref info, 0u) == 0)
			{
				return null;
			}
		}
		finally
		{
			ShellNative.DeleteDC(hdc);
		}
		return Normalize(bmWidth, num, array);
	}

	public static IconPixels Normalize(int width, int height, byte[] pixels)
	{
		bool flag = false;
		bool premultiplied = true;
		for (int i = 0; i < pixels.Length; i += 4)
		{
			byte b = pixels[i + 3];
			if (b != 0)
			{
				flag = true;
			}
			if (pixels[i] > b || pixels[i + 1] > b || pixels[i + 2] > b)
			{
				premultiplied = false;
			}
		}
		if (!flag)
		{
			for (int j = 3; j < pixels.Length; j += 4)
			{
				pixels[j] = byte.MaxValue;
			}
			premultiplied = true;
		}
		return new IconPixels(width, height, pixels, premultiplied);
	}

	public static string? TryGetProcessPath(uint processId)
	{
		nint num = ShellNative.OpenProcess(4096u, inherit: false, processId);
		if (num == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			uint size = (uint)stringBuilder.Capacity;
			return ShellNative.QueryFullProcessImageName(num, 0u, stringBuilder, ref size) ? stringBuilder.ToString(0, (int)size) : null;
		}
		finally
		{
			ShellNative.CloseHandle(num);
		}
	}

	public static string? TryGetWindowAppId(nint hwnd)
	{
		object propertyStore = null;
		try
		{
			Guid riid = typeof(ShellNative.IPropertyStore).GUID;
			if (ShellNative.SHGetPropertyStoreForWindow(hwnd, ref riid, out propertyStore) != 0 || !(propertyStore is ShellNative.IPropertyStore store))
			{
				return null;
			}
			return ReadString(store, ShellNative.PKEY_AppUserModel_ID);
		}
		catch
		{
			return null;
		}
		finally
		{
			if (propertyStore != null && Marshal.IsComObject(propertyStore))
			{
				Marshal.ReleaseComObject(propertyStore);
			}
		}
	}

	public static string? TryGetFileAppId(string path)
	{
		object propertyStore = null;
		try
		{
			Guid riid = typeof(ShellNative.IPropertyStore).GUID;
			if (ShellNative.SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref riid, out propertyStore) != 0 || !(propertyStore is ShellNative.IPropertyStore store))
			{
				return null;
			}
			return ReadString(store, ShellNative.PKEY_AppUserModel_ID);
		}
		catch
		{
			return null;
		}
		finally
		{
			if (propertyStore != null && Marshal.IsComObject(propertyStore))
			{
				Marshal.ReleaseComObject(propertyStore);
			}
		}
	}

	private static string? ReadString(ShellNative.IPropertyStore store, ShellNative.PROPERTYKEY key)
	{
		if (store.GetValue(ref key, out var value) != 0)
		{
			return null;
		}
		try
		{
			return (value.vt == 31 && value.pointerValue != IntPtr.Zero) ? Marshal.PtrToStringUni(value.pointerValue) : null;
		}
		finally
		{
			ShellNative.PropVariantClear(ref value);
		}
	}

	public static (string? Target, string? Arguments) TryResolveShortcut(string lnkPath)
	{
		object obj = null;
		try
		{
			obj = new ShellNative.ShellLinkCoClass();
			((ShellNative.IPersistFile)obj).Load(lnkPath, 0u);
			ShellNative.IShellLinkW obj2 = (ShellNative.IShellLinkW)obj;
			StringBuilder stringBuilder = new StringBuilder(1024);
			obj2.GetPath(stringBuilder, stringBuilder.Capacity, IntPtr.Zero, 0u);
			StringBuilder stringBuilder2 = new StringBuilder(2048);
			obj2.GetArguments(stringBuilder2, stringBuilder2.Capacity);
			string text = stringBuilder.ToString();
			string text2 = stringBuilder2.ToString();
			return (Target: string.IsNullOrWhiteSpace(text) ? null : text, Arguments: string.IsNullOrWhiteSpace(text2) ? null : text2);
		}
		catch (Exception ex)
		{
			Log.Warn("Ярлык '" + lnkPath + "': " + ex.Message);
			return (Target: null, Arguments: null);
		}
		finally
		{
			if (obj != null && Marshal.IsComObject(obj))
			{
				Marshal.ReleaseComObject(obj);
			}
		}
	}

	public static long RecycleBinItemCount()
	{
		ShellNative.SHQUERYRBINFO info = ShellNative.SHQUERYRBINFO.Create();
		if (ShellNative.SHQueryRecycleBin(null, ref info) != 0)
		{
			return 0L;
		}
		return info.i64NumItems;
	}
}
