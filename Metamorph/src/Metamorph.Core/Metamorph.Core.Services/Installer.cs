using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Metamorph.Core.Native;
using Microsoft.Win32;

namespace Metamorph.Core.Services;

public static class Installer
{
	public const string AppKey = "Metamorph";

	public const string ProgId = "Metamorph.Theme";

	public static string InstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Metamorph");

	public static string InstalledExe => Path.Combine(InstallDir, "Metamorph.exe");

	public static string StartMenuShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Metamorph.lnk");

	public static string RemoteHelpShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Metamorph Remote Help.lnk");

	public static string UninstallKey => "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Metamorph";

	public static bool IsInstalled
	{
		get
		{
			if (Reg.Get(UninstallKey, "InstallLocation") is string path)
			{
				return Directory.Exists(path);
			}
			return false;
		}
	}

	public static bool IsRunningFromInstallDir => string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'), Path.GetFullPath(InstallDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

	public static string Install(string version)
	{
		string baseDirectory = AppContext.BaseDirectory;
		Directory.CreateDirectory(InstallDir);
		if (!IsRunningFromInstallDir)
		{
			CopyDirectory(baseDirectory, InstallDir);
		}
		string installedExe = InstalledExe;
		CreateShortcut(StartMenuShortcut, installedExe, "", "Metamorph — райсинг Windows 10");
		try
		{
			CreateShortcut(RemoteHelpShortcut, installedExe, "--remote-help", "Metamorph — удалённая помощь для ИИ-помощника");
		}
		catch (Exception ex)
		{
			Log.Warn("Ярлык удалённой помощи: " + ex.Message);
		}
		Register(installedExe, version);
		Log.Info("Установлено в " + InstallDir);
		return installedExe;
	}

	public static void Register(string exe, string version)
	{
		string uninstallKey = UninstallKey;
		Reg.SetString(uninstallKey, "DisplayName", "Metamorph");
		Reg.SetString(uninstallKey, "DisplayVersion", version);
		Reg.SetString(uninstallKey, "Publisher", "Metamorph");
		Reg.SetString(uninstallKey, "DisplayIcon", exe);
		Reg.SetString(uninstallKey, "InstallLocation", Path.GetDirectoryName(exe));
		Reg.SetString(uninstallKey, "UninstallString", "\"" + exe + "\" --uninstall");
		Reg.SetString(uninstallKey, "QuietUninstallString", "\"" + exe + "\" --uninstall --quiet");
		Reg.SetString(uninstallKey, "URLInfoAbout", "https://github.com/");
		Reg.SetDword(uninstallKey, "NoModify", 1u);
		Reg.SetDword(uninstallKey, "NoRepair", 1u);
		Reg.SetDword(uninstallKey, "EstimatedSize", (uint)Math.Max(1L, DirectorySize(Path.GetDirectoryName(exe)) / 1024));
		RegisterFileType(exe);
	}

	public static void RegisterFileType(string exe)
	{
		Reg.SetString("Software\\Classes\\.mmtheme", "", "Metamorph.Theme");
		Reg.SetString("Software\\Classes\\Metamorph.Theme", "", "Metamorph theme");
		Reg.SetString("Software\\Classes\\Metamorph.Theme\\DefaultIcon", "", "\"" + exe + "\",0");
		Reg.SetString("Software\\Classes\\Metamorph.Theme\\shell\\open\\command", "", "\"" + exe + "\" --import \"%1\"");
		SHChangeNotify(134217728, 0u, IntPtr.Zero, IntPtr.Zero);
	}

	public static void Uninstall(bool removeUserData, string userDataDir)
	{
		AutostartService.SetEnabled(enabled: false, "");
		TryDelete(StartMenuShortcut);
		TryDelete(RemoteHelpShortcut);
		DeleteTree(UninstallKey);
		DeleteTree("Software\\Classes\\Metamorph.Theme");
		if (Reg.Get("Software\\Classes\\.mmtheme", "") as string == "Metamorph.Theme")
		{
			DeleteTree("Software\\Classes\\.mmtheme");
		}
		SHChangeNotify(134217728, 0u, IntPtr.Zero, IntPtr.Zero);
		List<string> list = new List<string>();
		if (Directory.Exists(InstallDir))
		{
			list.Add(InstallDir);
		}
		if (removeUserData && Directory.Exists(userDataDir))
		{
			list.Add(userDataDir);
		}
		if (list.Count != 0)
		{
			string text = string.Join(" & ", list.Select((string t) => "rmdir /s /q \"" + t + "\""));
			Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 4 > nul & " + text)
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				WorkingDirectory = Path.GetTempPath()
			})?.Dispose();
			Log.Info("Удаление запланировано");
		}
	}

	public static void CreateShortcut(string lnkPath, string target, string arguments, string description)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
		object obj = null;
		try
		{
			obj = new ShellNative.ShellLinkCoClass();
			ShellNative.IShellLinkW obj2 = (ShellNative.IShellLinkW)obj;
			obj2.SetPath(target);
			obj2.SetArguments(arguments);
			obj2.SetDescription(description);
			obj2.SetWorkingDirectory(Path.GetDirectoryName(target));
			obj2.SetIconLocation(target, 0);
			((ShellNative.IPersistFile)obj).Save(lnkPath, remember: true);
		}
		finally
		{
			if (obj != null && Marshal.IsComObject(obj))
			{
				Marshal.ReleaseComObject(obj);
			}
		}
	}

	public static void CopyDirectory(string source, string target)
	{
		foreach (string item in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
		{
			Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, item)));
		}
		foreach (string item2 in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			string text = Path.Combine(target, Path.GetRelativePath(source, item2));
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item2, text, overwrite: true);
		}
	}

	private static long DirectorySize(string dir)
	{
		try
		{
			return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum((string f) => new FileInfo(f).Length);
		}
		catch
		{
			return 0L;
		}
	}

	private static void DeleteTree(string key)
	{
		try
		{
			Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
		}
		catch (Exception ex)
		{
			Log.Warn("Реестр " + key + ": " + ex.Message);
		}
	}

	private static void TryDelete(string file)
	{
		try
		{
			if (File.Exists(file))
			{
				File.Delete(file);
			}
		}
		catch (Exception ex)
		{
			Log.Warn(file + ": " + ex.Message);
		}
	}

	[DllImport("shell32.dll")]
	private static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
