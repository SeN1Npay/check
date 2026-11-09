using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Remote;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Remote;

internal sealed class BridgeTools
{
	private const int MaxText = 60000;

	private static readonly SemaphoreSlim InputLock = new SemaphoreSlim(1, 1);

	private readonly AppPaths _paths;

	private readonly BridgePermissions _perms;

	private readonly Func<string, string> _bridgeAction;

	private readonly string _installDir;

	private readonly string _mainExe;

	private string BackupsDir => Path.Combine(_paths.Root, "backups");

	public BridgeTools(AppPaths paths, BridgePermissions perms, Func<string, string> bridgeAction)
	{
		_paths = paths;
		_perms = perms;
		_bridgeAction = bridgeAction;
		_installDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
		string processPath = Environment.ProcessPath;
		_mainExe = ((processPath != null && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ? processPath : Path.Combine(_installDir, "Metamorph.exe"));
	}

	public void Register(McpServer mcp)
	{
		mcp.Add(new McpTool("status", "PC and Metamorph overview: versions, install dir, whether the main app is running, monitors (physical pixels, DPI), cursor, permissions, power.", McpTool.Schema(), (JsonObject a, CancellationToken ct) => StatusAsync()));
		mcp.Add(new McpTool("screenshot", "Screenshot as JPEG/PNG. Default: whole virtual screen scaled to max_width. The text part explains how to convert image pixels to screen coordinates for the input tool. The Remote help window itself is excluded from captures when Windows supports it.", McpTool.Schema(("monitor", "integer", "Monitor index from status; -1 = all monitors (default)"), ("region", "string", "Optional 'x,y,w,h' in physical screen pixels (overrides monitor)"), ("max_width", "integer", "Downscale so the image is at most this wide (default 1600, max 4000)"), ("scale", "number", "Explicit scale 0.05..1 (overrides max_width)"), ("format", "string", "jpeg (default) or png"), ("quality", "integer", "JPEG quality 20..95 (default 75)"), ("cursor", "boolean", "Draw a red ring where the mouse cursor is (default true)")), (JsonObject a, CancellationToken ct) => Task.FromResult(Screenshot(a))));
		mcp.Add(new McpTool("screen_burst", "Capture a burst of frames and measure flicker: per-step changed-pixel ratio, changed boxes in screen coordinates and A→B→A blink detection. Returns a text report, a contact sheet and, if blinks are found, the frames around the first blink. Blinks shorter than the capture interval can be missed: use interval_ms=0 for the maximum rate (~15-30 fps).", McpTool.Schema(("frames", "integer", "Number of frames 2..60 (default 24)"), ("interval_ms", "integer", "Delay between frame starts, 0..2000 (default 80)"), ("monitor", "integer", "Monitor index; -1 = all (default: primary)"), ("region", "string", "Optional 'x,y,w,h' in physical pixels, e.g. just the bar or the dock"), ("analysis_step", "integer", "Analyse every Nth pixel (1..16, default 4)"), ("sheet", "boolean", "Return the contact sheet image (default true)")), BurstAsync));
		mcp.Add(new McpTool("windows", "Top-level windows in Z-order (top first): hwnd, class, process, rect, flags (topmost/layered/tool/cloaked...). desktop=true also lists children of Progman/WorkerW (where wallpapers live).", McpTool.Schema(("all", "boolean", "Include hidden and cloaked windows (default false)"), ("desktop", "boolean", "Include Progman/WorkerW children (default true)"), ("filter", "string", "Only rows containing this text (class, title or process)")), (JsonObject a, CancellationToken ct) => Task.FromResult(Windows(a))));
		mcp.Add(new McpTool("processes", "Processes with CPU % (sampled over 0.7 s), working set and window title.", McpTool.Schema(("filter", "string", "Substring of the process name or title"), ("top", "integer", "Max rows (default 40)")), ProcessesAsync));
		mcp.Add(new McpTool("logs", "Tail of Metamorph log files from %AppData%\\Metamorph. which: main (metamorph.log, default), old (metamorph.old.log), remote (remote-help.log), list (all files in the data folder).", McpTool.Schema(("which", "string", "main | old | remote | list"), ("lines", "integer", "Last N lines (default 200, max 5000)"), ("grep", "string", "Only lines containing this text (case-insensitive); prefix with re: for a regex")), (JsonObject a, CancellationToken ct) => Task.FromResult(Logs(a))));
		mcp.Add(new McpTool("trace", "In-memory event trace of the running Metamorph (AppBar messages, broadcasts, fullscreen changes, wallpaper restarts, loop breakers). Best tool to find what makes the screen flicker.", McpTool.Schema(("lines", "integer", "Last N events (default 400, max 4000)")), async (JsonObject a, CancellationToken ct) => McpResult.Text((await QueryClient.AskAsync($"trace {a.Int("lines", 400, 10, 4000)}", 6000)) ?? "Metamorph is not running or does not answer (use metamorph {action:start})")));
		mcp.Add(new McpTool("state", "Live state of the running Metamorph (theme, bar/dock/wallpaper/mascot status) plus settings.json (secrets redacted) and state.json.", McpTool.Schema(), (JsonObject a, CancellationToken ct) => StateAsync()));
		mcp.Add(new McpTool("metamorph", "Control the main Metamorph app. action: start | stop | restart | safe (restart with --safe) | kill (force, restores the taskbar) | command (IPC: show | exit | restore | bar | dock | mascot | next-wallpaper | pause-wallpaper | launcher | island | apply:<themeId> | wallpaper:<file|folder|theme>). args: extra command-line arguments for start/restart.", McpTool.Schema(("action!", "string", "start | stop | restart | safe | kill | command"), ("command", "string", "IPC command for action=command"), ("args", "string", "Command-line arguments for start/restart")), MetamorphAsync));
		mcp.Add(new McpTool("run_powershell", "Run a Windows PowerShell 5.1 script as the current user (not elevated) and return exit code, stdout and stderr. Must finish within timeout_s (max 90 — the tunnel drops longer requests). Requires the 'commands' permission.", McpTool.Schema(("script!", "string", "PowerShell script"), ("timeout_s", "integer", "1..90, default 30"), ("cwd", "string", "Working directory (default: Metamorph install folder)")), PowerShellAsync));
		mcp.Add(new McpTool("file_list", "List a folder (supports %ENV% variables). Requires the 'commands' permission.", McpTool.Schema(("path!", "string", "Folder"), ("pattern", "string", "Search pattern, default *"), ("recursive", "boolean", "Default false"), ("max", "integer", "Max entries (default 300)")), (JsonObject a, CancellationToken ct) => Task.FromResult(FileList(a))));
		mcp.Add(new McpTool("file_read", "Read a file: text (UTF-8), base64, or image (png/jpg/gif/bmp returned as an image). For large binaries use HTTP GET {base}/files?path=... instead. Requires the 'commands' permission.", McpTool.Schema(("path!", "string", "File path (supports %ENV% variables)"), ("mode", "string", "text (default) | base64 | image"), ("offset", "integer", "Byte offset (default 0)"), ("max_bytes", "integer", "Default 200000, max 4000000")), (JsonObject a, CancellationToken ct) => Task.FromResult(FileRead(a))));
		mcp.Add(new McpTool("file_write", "Write a file from text or base64 (creates folders). For large files use HTTP PUT {base}/files?path=... (add &append=1 for further chunks). Requires the 'commands' permission.", McpTool.Schema(("path!", "string", "File path"), ("text", "string", "UTF-8 text"), ("base64", "string", "Binary content"), ("append", "boolean", "Append instead of overwrite")), (JsonObject a, CancellationToken ct) => Task.FromResult(FileWrite(a))));
		mcp.Add(new McpTool("install_build", "Install a Metamorph build from a zip (already uploaded to this PC, or downloaded from url): stops Metamorph, backs up every file it replaces, extracts (locked files are renamed to *.old-<time>), starts Metamorph and checks that it answers. Use rollback_build to undo. Requires the 'commands' permission.", McpTool.Schema(("zip", "string", "Path to the zip on this PC"), ("url", "string", "Or an https URL to download the zip from"), ("target", "string", "Install folder (default: the folder of the running Metamorph.exe)"), ("start", "boolean", "Start Metamorph afterwards (default true)"), ("args", "string", "Command-line arguments for the start")), InstallAsync));
		mcp.Add(new McpTool("rollback_build", "Undo install_build: restores the files from a backup (default: the latest) and removes files that the build added. Without 'backup' and with list=true only lists backups.", McpTool.Schema(("backup", "string", "Backup file name from the list"), ("list", "boolean", "Only list backups"), ("start", "boolean", "Start Metamorph afterwards (default true)")), RollbackAsync));
		mcp.Add(new McpTool("input", "Mouse and keyboard in physical screen pixels (same coordinates as status/screenshot). action: move | click | double_click | right_click | middle_click | scroll | drag | type | hotkey | cursor. Requires the 'input' permission. Optionally returns a screenshot afterwards.", McpTool.Schema(("action!", "string", "move | click | double_click | right_click | middle_click | scroll | drag | type | hotkey | cursor"), ("x", "integer", "X (default: current cursor)"), ("y", "integer", "Y (default: current cursor)"), ("x2", "integer", "Drag target X"), ("y2", "integer", "Drag target Y"), ("notches", "integer", "Scroll notches: positive = up, negative = down"), ("text", "string", "Text for type (Unicode, \\n = Enter)"), ("keys", "string", "Hotkey like ctrl+shift+esc, win+d, alt+tab, enter"), ("wait_ms", "integer", "Pause after the action (default 400)"), ("screenshot", "boolean", "Return a screenshot after the action (default false)"), ("monitor", "integer", "Monitor for that screenshot (default all)")), InputAsync));
		mcp.Add(new McpTool("bridge", "The Remote help window: info | minimize | restore | extend (adds 2 hours before auto-stop).", McpTool.Schema(("action", "string", "info (default) | minimize | restore | extend")), (JsonObject a, CancellationToken ct) => Task.FromResult(McpResult.Text(_bridgeAction(a.Str("action") ?? "info")))));
	}

	private async Task<McpResult> StatusAsync()
	{
		StringBuilder sb = new StringBuilder();
		FileInfo fileInfo = new FileInfo(_mainExe);
		StringBuilder stringBuilder = sb;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(35, 2, stringBuilder);
		handler.AppendLiteral("Metamorph Remote Help ");
		handler.AppendFormatted(App.VersionText);
		handler.AppendLiteral(", bridge pid ");
		handler.AppendFormatted(Environment.ProcessId);
		stringBuilder2.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
		handler.AppendLiteral("Install folder: ");
		handler.AppendFormatted(_installDir);
		stringBuilder3.AppendLine(ref handler);
		if (fileInfo.Exists)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(32, 3, stringBuilder);
			handler.AppendLiteral("Metamorph.exe: ");
			handler.AppendFormatted((double)fileInfo.Length / 1048576.0, "0.0");
			handler.AppendLiteral(" MB (");
			handler.AppendFormatted((fileInfo.Length > 60000000) ? "standalone, includes .NET" : "framework-dependent, needs .NET 8 Desktop Runtime");
			handler.AppendLiteral("), modified ");
			handler.AppendFormatted(fileInfo.LastWriteTime, "yyyy-MM-dd HH:mm");
			stringBuilder4.AppendLine(ref handler);
		}
		string text = await QueryClient.AskAsync("ping", 2500);
		stringBuilder = sb;
		StringBuilder stringBuilder5 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder);
		handler.AppendLiteral("Main app: ");
		handler.AppendFormatted(text ?? "NOT running or not answering");
		stringBuilder5.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder6 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder);
		handler.AppendLiteral("Data folder: ");
		handler.AppendFormatted(_paths.Root);
		stringBuilder6.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder7 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(31, 3, stringBuilder);
		handler.AppendLiteral("Windows: ");
		handler.AppendFormatted(WindowsVersion());
		handler.AppendLiteral("; .NET ");
		handler.AppendFormatted(Environment.Version);
		handler.AppendLiteral("; ");
		handler.AppendFormatted(Environment.ProcessorCount);
		handler.AppendLiteral(" logical CPUs");
		stringBuilder7.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder8 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(41, 4, stringBuilder);
		handler.AppendLiteral("User: ");
		handler.AppendFormatted(Environment.UserName);
		handler.AppendLiteral("; UI culture ");
		handler.AppendFormatted(CultureInfo.CurrentUICulture.Name);
		handler.AppendLiteral("; local time ");
		handler.AppendFormatted(DateTime.Now, "yyyy-MM-dd HH:mm:ss zzz");
		handler.AppendLiteral("; uptime ");
		handler.AppendFormatted(TimeSpan.FromMilliseconds(Environment.TickCount64), "d\\.hh\\:mm");
		stringBuilder8.AppendLine(ref handler);
		try
		{
			PowerStatus powerStatus = SystemInformation.PowerStatus;
			stringBuilder = sb;
			StringBuilder stringBuilder9 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(17, 2, stringBuilder);
			handler.AppendLiteral("Power: ");
			handler.AppendFormatted(powerStatus.PowerLineStatus);
			handler.AppendLiteral(", battery ");
			handler.AppendFormatted(powerStatus.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) ? "none" : $"{powerStatus.BatteryLifePercent:P0}");
			stringBuilder9.AppendLine(ref handler);
		}
		catch
		{
		}
		List<RemoteNative.Monitor> list = RemoteNative.Monitors();
		RemoteNative.RECT value = RemoteNative.VirtualScreen(list);
		stringBuilder = sb;
		StringBuilder stringBuilder10 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder);
		handler.AppendLiteral("Virtual screen: ");
		handler.AppendFormatted(value);
		stringBuilder10.AppendLine(ref handler);
		foreach (RemoteNative.Monitor item in list)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder11 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(35, 7, stringBuilder);
			handler.AppendLiteral("  monitor ");
			handler.AppendFormatted(item.Index);
			handler.AppendFormatted(item.Primary ? " (primary)" : "");
			handler.AppendLiteral(": ");
			handler.AppendFormatted(item.Bounds);
			handler.AppendLiteral(", work area ");
			handler.AppendFormatted(item.Work);
			handler.AppendLiteral(", DPI ");
			handler.AppendFormatted(item.Dpi);
			handler.AppendLiteral(" (");
			handler.AppendFormatted((double)item.Dpi / 96.0, "P0");
			handler.AppendLiteral("), ");
			handler.AppendFormatted(item.Device);
			stringBuilder11.AppendLine(ref handler);
		}
		RemoteNative.POINT pOINT = RemoteNative.Cursor();
		stringBuilder = sb;
		StringBuilder stringBuilder12 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, stringBuilder);
		handler.AppendLiteral("Cursor: ");
		handler.AppendFormatted(pOINT.X);
		handler.AppendLiteral(",");
		handler.AppendFormatted(pOINT.Y);
		stringBuilder12.AppendLine(ref handler);
		stringBuilder = sb;
		StringBuilder stringBuilder13 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(56, 2, stringBuilder);
		handler.AppendLiteral("Permissions: commands/files/builds = ");
		handler.AppendFormatted(_perms.Commands ? "allowed" : "DENIED");
		handler.AppendLiteral(", mouse/keyboard = ");
		handler.AppendFormatted(_perms.Input ? "allowed" : "DENIED");
		stringBuilder13.AppendLine(ref handler);
		int value2 = (Directory.Exists(BackupsDir) ? Directory.GetFiles(BackupsDir, "build-*.zip").Length : 0);
		stringBuilder = sb;
		StringBuilder stringBuilder14 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder);
		handler.AppendLiteral("Build backups: ");
		handler.AppendFormatted(value2);
		stringBuilder14.AppendLine(ref handler);
		return McpResult.Text(sb.ToString());
	}

	private static string WindowsVersion()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion");
			string text = (registryKey?.GetValue("ProductName") as string) ?? "Windows";
			string value = (registryKey?.GetValue("DisplayVersion") as string) ?? (registryKey?.GetValue("ReleaseId") as string) ?? "";
			string text2 = (registryKey?.GetValue("CurrentBuild") as string) ?? "";
			string value2 = registryKey?.GetValue("UBR")?.ToString() ?? "";
			if (int.TryParse(text2, out var result) && result >= 22000)
			{
				text = text.Replace("Windows 10", "Windows 11");
			}
			return $"{text} {value} (build {text2}.{value2})";
		}
		catch
		{
			return Environment.OSVersion.VersionString;
		}
	}

	private async Task<McpResult> StateAsync()
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("== live state ==");
		StringBuilder stringBuilder = sb;
		stringBuilder.AppendLine((await QueryClient.AskAsync("state", 6000)) ?? "Metamorph is not running or does not answer");
		try
		{
			if (File.Exists(_paths.SettingsFile))
			{
				sb.AppendLine("== settings.json (redacted) ==");
				sb.AppendLine(Clip(DiagnosticsReport.Redact(File.ReadAllText(_paths.SettingsFile)), 25000));
			}
			if (File.Exists(_paths.StateFile))
			{
				sb.AppendLine("== state.json ==");
				sb.AppendLine(Clip(File.ReadAllText(_paths.StateFile), 15000));
			}
		}
		catch (Exception ex)
		{
			sb.AppendLine("settings: " + ex.Message);
		}
		return McpResult.Text(sb.ToString());
	}

	private RemoteNative.RECT ResolveArea(JsonObject a, int defaultMonitor, out string label)
	{
		List<RemoteNative.Monitor> list = RemoteNative.Monitors();
		RemoteNative.RECT rECT = RemoteNative.VirtualScreen(list);
		string text = a.Str("region");
		if (text != null && text.Length > 0)
		{
			int[] array = (from v in text.Split(new char[4] { ',', ' ', 'x', ';' }, StringSplitOptions.RemoveEmptyEntries)
				select int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
			if (array.Length != 4)
			{
				throw new ArgumentException("region must be 'x,y,w,h'");
			}
			RemoteNative.RECT rECT2 = default(RemoteNative.RECT);
			rECT2.Left = Math.Max(rECT.Left, array[0]);
			rECT2.Top = Math.Max(rECT.Top, array[1]);
			rECT2.Right = Math.Min(rECT.Right, array[0] + array[2]);
			rECT2.Bottom = Math.Min(rECT.Bottom, array[1] + array[3]);
			RemoteNative.RECT result = rECT2;
			if (result.Width <= 0 || result.Height <= 0)
			{
				throw new ArgumentException($"region is outside the screen {rECT}");
			}
			label = "region";
			return result;
		}
		int num = a.Int("monitor", defaultMonitor, -2, 64);
		if (num == -2)
		{
			num = list.FindIndex((RemoteNative.Monitor m) => m.Primary);
		}
		if (num >= 0 && num < list.Count)
		{
			label = $"monitor {num}";
			return list[num].Bounds;
		}
		label = "all monitors";
		return rECT;
	}

	private McpResult Screenshot(JsonObject a)
	{
		string label;
		RemoteNative.RECT area = ResolveArea(a, -1, out label);
		using Bitmap bitmap = RemoteNative.Capture(area);
		RemoteNative.POINT pOINT = RemoteNative.Cursor();
		if (a.Bool("cursor", fallback: true))
		{
			DrawCursor(bitmap, pOINT.X - area.Left, pOINT.Y - area.Top);
		}
		double num = a.Num("scale", 0.0, 0.0, 1.0);
		if (num < 0.05)
		{
			num = Math.Min(1.0, (double)a.Int("max_width", 1600, 200, 4000) / (double)bitmap.Width);
		}
		using Bitmap bitmap2 = ((num < 0.999) ? RemoteNative.Scale(bitmap, num) : null);
		Bitmap bitmap3 = bitmap2 ?? bitmap;
		bool flag = string.Equals(a.Str("format"), "png", StringComparison.OrdinalIgnoreCase);
		byte[] data = (flag ? RemoteNative.Png(bitmap3) : RemoteNative.Jpeg(bitmap3, a.Int("quality", 75, 20, 95)));
		string value = num.ToString("0.####", CultureInfo.InvariantCulture);
		return McpResult.Text($"{label}: screen x={area.Left} y={area.Top} {area.Width}x{area.Height} physical px → image {bitmap3.Width}x{bitmap3.Height} (scale {value}). Screen point for image pixel (px,py): x = {area.Left} + px/{value}, y = {area.Top} + py/{value}. Cursor at {pOINT.X},{pOINT.Y}. {DateTime.Now:HH:mm:ss.fff}").AddImage(data, flag ? "image/png" : "image/jpeg");
	}

	private static void DrawCursor(Bitmap bmp, int x, int y)
	{
		if (x < -20 || y < -20 || x > bmp.Width + 20 || y > bmp.Height + 20)
		{
			return;
		}
		using Graphics graphics = Graphics.FromImage(bmp);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using Pen pen = new Pen(Color.FromArgb(220, 255, 255, 255), 5f);
		using Pen pen2 = new Pen(Color.FromArgb(240, 255, 30, 60), 2.5f);
		graphics.DrawEllipse(pen, x - 14, y - 14, 28, 28);
		graphics.DrawEllipse(pen2, x - 14, y - 14, 28, 28);
	}

	private async Task<McpResult> BurstAsync(JsonObject a, CancellationToken ct)
	{
		int count = a.Int("frames", 24, 2, 60);
		int interval = a.Int("interval_ms", 80, 0, 2000);
		int step = a.Int("analysis_step", 4, 1, 16);
		string label;
		RemoteNative.RECT area = ResolveArea(a, -2, out label);
		double keepScale = Math.Min(1.0, 960.0 / (double)area.Width);
		List<byte[]> lumas = new List<byte[]>(count);
		List<Bitmap> kept = new List<Bitmap>(count);
		List<double> times = new List<double>(count);
		int lw = 0;
		int lh = 0;
		Stopwatch sw = Stopwatch.StartNew();
		try
		{
			for (int j = 0; j < count; j++)
			{
				ct.ThrowIfCancellationRequested();
				double totalMilliseconds = sw.Elapsed.TotalMilliseconds;
				using (Bitmap bitmap = RemoteNative.Capture(area))
				{
					times.Add(totalMilliseconds);
					int stride;
					byte[] bgra = RemoteNative.Pixels(bitmap, out stride);
					lumas.Add(FlickerMeter.ToLuma(bgra, bitmap.Width, bitmap.Height, stride, step, out lw, out lh));
					kept.Add((keepScale < 0.999) ? FastScale(bitmap, keepScale) : ((Bitmap)bitmap.Clone()));
				}
				double num = (double)interval - (sw.Elapsed.TotalMilliseconds - totalMilliseconds);
				if (j + 1 < count && num > 1.0)
				{
					await Task.Delay(TimeSpan.FromMilliseconds(num), ct);
				}
			}
			FlickerMeter.Report report = FlickerMeter.Analyze(lumas, lw, lh);
			double num2;
			if (times.Count <= 1)
			{
				num2 = 0.0;
			}
			else
			{
				num2 = (times[times.Count - 1] - times[0]) / (double)(times.Count - 1);
			}
			double intervalMs = num2;
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(42, 8, stringBuilder2);
			handler.AppendFormatted(label);
			handler.AppendLiteral(": screen x=");
			handler.AppendFormatted(area.Left);
			handler.AppendLiteral(" y=");
			handler.AppendFormatted(area.Top);
			handler.AppendLiteral(" ");
			handler.AppendFormatted(area.Width);
			handler.AppendLiteral("x");
			handler.AppendFormatted(area.Height);
			handler.AppendLiteral("; analysed ");
			handler.AppendFormatted(lw);
			handler.AppendLiteral("x");
			handler.AppendFormatted(lh);
			handler.AppendLiteral(" (every ");
			handler.AppendFormatted(step);
			handler.AppendLiteral("th px)");
			stringBuilder3.AppendLine(ref handler);
			stringBuilder.Append(report.Format(lw, lh, intervalMs));
			stringBuilder.AppendLine("frame start times, ms: " + string.Join(" ", times.Select((double t) => t.ToString("0", CultureInfo.InvariantCulture))));
			List<FlickerMeter.Pair> list = (from p in report.Pairs
				where p.HasBox && p.Changed > 0.0005
				orderby p.Changed descending
				select p).Take(8).ToList();
			if (list.Count > 0)
			{
				stringBuilder.AppendLine("largest changes in screen coordinates (x,y,w,h):");
				foreach (FlickerMeter.Pair item in list)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(15, 6, stringBuilder2);
					handler.AppendLiteral("  step ");
					handler.AppendFormatted(item.Index);
					handler.AppendLiteral(": ");
					handler.AppendFormatted(area.Left + item.Left * step);
					handler.AppendLiteral(",");
					handler.AppendFormatted(area.Top + item.Top * step);
					handler.AppendLiteral(",");
					handler.AppendFormatted((item.Right - item.Left + 1) * step);
					handler.AppendLiteral(",");
					handler.AppendFormatted((item.Bottom - item.Top + 1) * step);
					handler.AppendLiteral(" (");
					handler.AppendFormatted(item.Changed, "P2");
					handler.AppendLiteral(")");
					stringBuilder4.AppendLine(ref handler);
				}
			}
			McpResult mcpResult = McpResult.Text(stringBuilder.ToString());
			if (a.Bool("sheet", fallback: true))
			{
				mcpResult.AddImage(ContactSheet(kept, report, lw, times), "image/jpeg");
			}
			if (report.Blinks.Count > 0)
			{
				int num3 = report.Blinks[0];
				foreach (int item2 in new int[3]
				{
					num3 - 1,
					num3,
					num3 + 1
				}.Where((int i) => i >= 0 && i < kept.Count))
				{
					mcpResult.AddImage(RemoteNative.Jpeg(kept[item2], 80L), "image/jpeg");
				}
				mcpResult.AddText($"Images after the sheet: frames {Math.Max(0, num3 - 1)}..{Math.Min(kept.Count - 1, num3 + 1)} around the first blink ({kept[0].Width}x{kept[0].Height}).");
			}
			return mcpResult;
		}
		finally
		{
			foreach (Bitmap item3 in kept)
			{
				item3.Dispose();
			}
		}
	}

	private static Bitmap FastScale(Bitmap source, double scale)
	{
		int width = Math.Max(1, (int)Math.Round((double)source.Width * scale));
		int height = Math.Max(1, (int)Math.Round((double)source.Height * scale));
		Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.Bilinear;
		graphics.DrawImage(source, 0, 0, width, height);
		return bitmap;
	}

	private static byte[] ContactSheet(List<Bitmap> frames, FlickerMeter.Report report, int lw, List<double> times)
	{
		int num = ((frames.Count <= 4) ? frames.Count : ((frames.Count <= 12) ? 4 : 6));
		int num2 = ((num >= 6) ? 300 : 400);
		Bitmap bitmap = frames[0];
		int num3 = Math.Max(1, (int)Math.Round((double)(num2 * bitmap.Height) / (double)bitmap.Width));
		int num4 = (frames.Count + num - 1) / num;
		while (num4 * num3 > 3000 && num2 > 120)
		{
			num2 -= 40;
			num3 = Math.Max(1, (int)Math.Round((double)(num2 * bitmap.Height) / (double)bitmap.Width));
		}
		using Bitmap bitmap2 = new Bitmap(num * num2, num4 * (num3 + 18), PixelFormat.Format24bppRgb);
		using Graphics graphics = Graphics.FromImage(bitmap2);
		graphics.Clear(Color.FromArgb(24, 24, 37));
		graphics.InterpolationMode = InterpolationMode.Bilinear;
		using Font font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Pixel);
		using Pen pen = new Pen(Color.FromArgb(255, 255, 40, 70), 2f);
		HashSet<int> hashSet = report.Blinks.ToHashSet();
		double num5 = (double)num2 / (double)Math.Max(1, lw);
		for (int i = 0; i < frames.Count; i++)
		{
			int num6 = i % num * num2;
			int num7 = i / num * (num3 + 18);
			graphics.DrawImage(frames[i], num6, num7 + 18, num2, num3);
			FlickerMeter.Pair pair = ((i > 0) ? report.Pairs[i - 1] : default(FlickerMeter.Pair));
			string s = ((i == 0) ? $"#0  {times[0]:0} ms" : $"#{i}  {times[i]:0} ms  Δ {pair.Changed:P1}{(hashSet.Contains(i) ? "  BLINK" : "")}");
			using SolidBrush brush = new SolidBrush(hashSet.Contains(i) ? Color.FromArgb(255, 255, 90, 110) : Color.FromArgb(255, 205, 214, 244));
			graphics.DrawString(s, font, brush, num6 + 4, num7 + 2);
			if (i > 0 && pair.HasBox && pair.Changed > 0.0005)
			{
				graphics.DrawRectangle(pen, (float)num6 + (float)((double)pair.Left * num5), (float)(num7 + 18) + (float)((double)pair.Top * num5), Math.Max(2f, (float)((double)(pair.Right - pair.Left + 1) * num5)), Math.Max(2f, (float)((double)(pair.Bottom - pair.Top + 1) * num5)));
			}
		}
		return RemoteNative.Jpeg(bitmap2, 72L);
	}

	private static McpResult Windows(JsonObject a)
	{
		List<RemoteNative.WindowInfo> list = RemoteNative.Windows(a.Bool("all", fallback: false), a.Bool("desktop", fallback: true));
		string value = a.Str("filter");
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("z  hwnd       process(pid)  rect(x,y wxh)  flags  class  \"title\"");
		int num = 0;
		foreach (RemoteNative.WindowInfo item in list)
		{
			string text = $"{item.Z,-3} 0x{((IntPtr)item.Hwnd).ToInt64():X}  {item.Process}({item.Pid})  {item.Rect}  [{item.Flags}]  {item.Class}  \"{item.Title}\"";
			if (string.IsNullOrEmpty(value) || text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				stringBuilder.AppendLine(text);
				if (++num >= 600)
				{
					stringBuilder.AppendLine("… (truncated)");
					break;
				}
			}
		}
		return McpResult.Text(stringBuilder.ToString());
	}

	private static async Task<McpResult> ProcessesAsync(JsonObject a, CancellationToken ct)
	{
		string filter = a.Str("filter");
		int top = a.Int("top", 40, 1, 500);
		Dictionary<int, TimeSpan> before = new Dictionary<int, TimeSpan>();
		Process[] processes = Process.GetProcesses();
		foreach (Process process in processes)
		{
			try
			{
				before[process.Id] = process.TotalProcessorTime;
			}
			catch
			{
			}
			process.Dispose();
		}
		Stopwatch sw = Stopwatch.StartNew();
		await Task.Delay(700, ct);
		double totalMilliseconds = sw.Elapsed.TotalMilliseconds;
		List<(double Cpu, long Ws, string Text)> list = new List<(double Cpu, long Ws, string Text)>();
		processes = Process.GetProcesses();
		foreach (Process process2 in processes)
		{
			using (process2)
			{
				try
				{
					double num = 0.0;
					try
					{
						if (before.TryGetValue(process2.Id, out var value))
						{
							num = (process2.TotalProcessorTime - value).TotalMilliseconds / totalMilliseconds / (double)Environment.ProcessorCount * 100.0;
						}
					}
					catch
					{
					}
					string value2 = "";
					try
					{
						value2 = process2.MainWindowTitle;
					}
					catch
					{
					}
					string text = $"{process2.Id,6}  {process2.ProcessName,-28} {num,5:0.0}%  {(double)process2.WorkingSet64 / 1048576.0,7:0} MB  {value2}";
					if (string.IsNullOrEmpty(filter) || text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add((num, process2.WorkingSet64, text));
					}
				}
				catch
				{
				}
			}
		}
		StringBuilder stringBuilder = new StringBuilder("   pid  name                           cpu      memory  title\n");
		foreach (var item in (from r in list
			orderby r.Cpu descending, r.Ws descending
			select r).Take(top))
		{
			stringBuilder.AppendLine(item.Item3);
		}
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(44, 1, stringBuilder2);
		handler.AppendLiteral("(");
		handler.AppendFormatted(list.Count);
		handler.AppendLiteral(" matching processes; CPU is % of all cores)");
		stringBuilder2.AppendLine(ref handler);
		return McpResult.Text(stringBuilder.ToString());
	}

	private McpResult Logs(JsonObject a)
	{
		string text;
		switch ((a.Str("which") ?? "main").ToLowerInvariant())
		{
		case "list":
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (FileInfo item in from f in new DirectoryInfo(_paths.Root).EnumerateFiles("*", SearchOption.TopDirectoryOnly)
				orderby f.Name
				select f)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(5, 3, stringBuilder2);
				handler.AppendFormatted<string>(item.Name, -36);
				handler.AppendLiteral(" ");
				handler.AppendFormatted(item.Length, 10, "N0");
				handler.AppendLiteral(" B  ");
				handler.AppendFormatted(item.LastWriteTime, "yyyy-MM-dd HH:mm:ss");
				stringBuilder2.AppendLine(ref handler);
			}
			return McpResult.Text(stringBuilder.ToString());
		}
		case "old":
			text = Log.OldPath(_paths.LogFile);
			break;
		case "remote":
			text = Path.Combine(_paths.Root, "remote-help.log");
			break;
		default:
			text = _paths.LogFile;
			break;
		}
		string text2 = text;
		if (!File.Exists(text2))
		{
			return McpResult.Error("No such log: " + text2);
		}
		string[] source = ReadShared(text2).Split('\n');
		string grep = a.Str("grep");
		IEnumerable<string> source2 = source.Select((string l) => l.TrimEnd('\r'));
		if (!string.IsNullOrEmpty(grep))
		{
			if (grep.StartsWith("re:", StringComparison.Ordinal))
			{
				text = grep;
				Regex re = new Regex(text.Substring(3, text.Length - 3), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2.0));
				source2 = source2.Where((string l) => re.IsMatch(l));
			}
			else
			{
				source2 = source2.Where((string l) => l.Contains(grep, StringComparison.OrdinalIgnoreCase));
			}
		}
		int count = a.Int("lines", 200, 1, 5000);
		List<string> list = source2.Where((string l) => l.Length > 0).TakeLast(count).ToList();
		return McpResult.Text($"{text2} ({new FileInfo(text2).Length:N0} B), last {list.Count} lines:\n" + Clip(string.Join("\n", list), 60000));
	}

	private static string ReadShared(string path)
	{
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
		return streamReader.ReadToEnd();
	}

	private static string Clip(string text, int max)
	{
		if (text.Length <= max)
		{
			return text;
		}
		int num = max / 5;
		string text2 = text.Substring(0, num);
		string obj = $"\n… [{text.Length - max:N0} chars skipped] …\n";
		int num2 = max - num;
		int length = text.Length;
		int num3 = length - num2;
		return text2 + obj + text.Substring(num3, length - num3);
	}

	private static int? MainPid(string? pingOrPid)
	{
		if (pingOrPid == null)
		{
			return null;
		}
		Match match = Regex.Match(pingOrPid, "(?:pid=)?(\\d+)");
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return null;
		}
		return result;
	}

	private async Task<McpResult> MetamorphAsync(JsonObject a, CancellationToken ct)
	{
		string text = (a.Str("action") ?? "").ToLowerInvariant();
		string args = a.Str("args") ?? "";
		switch (text)
		{
		case "start":
			return McpResult.Text(await StartMainAsync(args, ct));
		case "stop":
			return McpResult.Text(await StopMainAsync(force: false, ct));
		case "kill":
			return McpResult.Text(await StopMainAsync(force: true, ct));
		case "restart":
			return McpResult.Text(await StopMainAsync(force: false, ct) + "\n" + await StartMainAsync(args, ct));
		case "safe":
			return McpResult.Text(await StopMainAsync(force: false, ct) + "\n" + await StartMainAsync(("--safe " + args).Trim(), ct));
		case "command":
		{
			string command = a.Str("command");
			if (string.IsNullOrWhiteSpace(command))
			{
				return McpResult.Error("command is required");
			}
			string text2 = await QueryClient.AskAsync("command " + command, 8000);
			if (text2 != null)
			{
				return McpResult.Text(text2);
			}
			return IpcClient.TrySend(command) ? McpResult.Text("sent (no answer channel)") : McpResult.Error("Metamorph is not running");
		}
		default:
			return McpResult.Error("action must be start | stop | restart | safe | kill | command");
		}
	}

	private async Task<string> StartMainAsync(string args, CancellationToken ct)
	{
		string text = await QueryClient.AskAsync("ping", 1500);
		if (text != null)
		{
			return "Already running: " + text;
		}
		if (!File.Exists(_mainExe))
		{
			return "Not found: " + _mainExe;
		}
		Process.Start(new ProcessStartInfo(_mainExe, args)
		{
			UseShellExecute = false,
			WorkingDirectory = Path.GetDirectoryName(_mainExe)
		})?.Dispose();
		Stopwatch sw = Stopwatch.StartNew();
		while (sw.Elapsed < TimeSpan.FromSeconds(25.0))
		{
			await Task.Delay(700, ct);
			string text2 = await QueryClient.AskAsync("ping", 1500);
			if (text2 != null)
			{
				return $"Started in {sw.Elapsed.TotalSeconds:0.0} s: {text2}";
			}
		}
		return "Started, but it does not answer after 25 s — check logs (it may have crashed: logs {which:'main'}, run_powershell Get-WinEvent).";
	}

	private async Task<string> StopMainAsync(bool force, CancellationToken ct)
	{
		int? num = MainPid(await QueryClient.AskAsync("pid", 2000));
		List<Process> candidates = new List<Process>();
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			try
			{
				candidates.Add(Process.GetProcessById(valueOrDefault));
			}
			catch
			{
			}
		}
		else if (force)
		{
			candidates.AddRange(from p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_mainExe))
				where p.Id != Environment.ProcessId && IsMainApp(p)
				select p);
		}
		if (candidates.Count == 0)
		{
			return (!num.HasValue) ? "Metamorph was not running" : "Metamorph already exited";
		}
		try
		{
			if (!force)
			{
				IpcClient.TrySend("exit");
				Stopwatch sw = Stopwatch.StartNew();
				while (sw.Elapsed < TimeSpan.FromSeconds(12.0) && candidates.Any((Process p) => !Exited(p)))
				{
					await Task.Delay(300, ct);
				}
				if (candidates.All(Exited))
				{
					return $"Stopped gracefully in {sw.Elapsed.TotalSeconds:0.0} s";
				}
			}
			foreach (Process item in candidates.Where((Process p) => !Exited(p)))
			{
				try
				{
					item.Kill(entireProcessTree: true);
					item.WaitForExit(5000);
				}
				catch
				{
				}
			}
			TaskbarController.SafeShow();
			return force ? "Killed; taskbar restored" : "Did not exit within 12 s — killed; taskbar restored";
		}
		finally
		{
			foreach (Process item2 in candidates)
			{
				item2.Dispose();
			}
		}
	}

	private static bool IsMainApp(Process p)
	{
		try
		{
			return !p.MainWindowTitle.Contains("Remote help", StringComparison.OrdinalIgnoreCase) && !p.MainWindowTitle.Contains("удалённая помощь", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return true;
		}
	}

	private static bool Exited(Process p)
	{
		try
		{
			return p.HasExited;
		}
		catch
		{
			return true;
		}
	}

	private McpResult? Denied(bool input = false)
	{
		if (input ? _perms.Input : _perms.Commands)
		{
			return null;
		}
		return McpResult.Error(input ? "The user disabled mouse/keyboard control in the Remote help window." : "The user disabled commands and file access in the Remote help window.");
	}

	private async Task<McpResult> PowerShellAsync(JsonObject a, CancellationToken ct)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		string text = a.Str("script");
		if (string.IsNullOrWhiteSpace(text))
		{
			return McpResult.Error("script is required");
		}
		int timeout = a.Int("timeout_s", 30, 1, 90);
		string text2 = Path.Combine(Path.GetTempPath(), "metamorph-remote");
		Directory.CreateDirectory(text2);
		string file = Path.Combine(text2, $"cmd-{Guid.NewGuid():N}.ps1");
		await File.WriteAllTextAsync(file, "$ProgressPreference='SilentlyContinue'\n[Console]::OutputEncoding=[System.Text.Encoding]::UTF8\n$OutputEncoding=[System.Text.Encoding]::UTF8\n" + text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
		string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
		if (!File.Exists(text3))
		{
			text3 = "powershell.exe";
		}
		string text4 = a.Str("cwd");
		ProcessStartInfo startInfo = new ProcessStartInfo(text3, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + file + "\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			WorkingDirectory = ((!string.IsNullOrWhiteSpace(text4) && Directory.Exists(Environment.ExpandEnvironmentVariables(text4))) ? Environment.ExpandEnvironmentVariables(text4) : _installDir)
		};
		Stopwatch sw = Stopwatch.StartNew();
		using (Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("powershell did not start"))
		{
			process.StandardInput.Close();
			Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
			Task<string> stderr = process.StandardError.ReadToEndAsync(ct);
			bool timedOut = false;
			using (CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				limit.CancelAfter(TimeSpan.FromSeconds(timeout));
				try
				{
					await process.WaitForExitAsync(limit.Token);
				}
				catch (OperationCanceledException)
				{
					timedOut = true;
					try
					{
						process.Kill(entireProcessTree: true);
					}
					catch
					{
					}
				}
			}
			try
			{
				await Task.WhenAll<string>(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(3.0), CancellationToken.None);
			}
			catch
			{
			}
			try
			{
				File.Delete(file);
			}
			catch
			{
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(timedOut ? $"TIMEOUT after {timeout} s (process tree killed)" : $"exit code {SafeExit(process)}, {sw.Elapsed.TotalSeconds:0.0} s");
			string text5 = Out(stdout).TrimEnd();
			string text6 = Out(stderr).TrimEnd();
			if (text5.Length > 0)
			{
				stringBuilder.AppendLine("--- stdout ---").AppendLine(Clip(text5, 60000));
			}
			if (text6.Length > 0)
			{
				stringBuilder.AppendLine("--- stderr ---").AppendLine(Clip(text6, 20000));
			}
			return timedOut ? McpResult.Error(stringBuilder.ToString()) : McpResult.Text(stringBuilder.ToString());
		}
		static string Out(Task<string> t)
		{
			if (!t.IsCompletedSuccessfully)
			{
				return "";
			}
			return t.Result;
		}
	}

	private static int SafeExit(Process p)
	{
		try
		{
			return p.ExitCode;
		}
		catch
		{
			return -1;
		}
	}

	private static string FullPath(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			throw new ArgumentException("path is required");
		}
		return Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"')));
	}

	private McpResult FileList(JsonObject a)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		string text = FullPath(a.Str("path"));
		if (!Directory.Exists(text))
		{
			return McpResult.Error("No such folder: " + text);
		}
		int num = a.Int("max", 300, 1, 5000);
		EnumerationOptions enumerationOptions = new EnumerationOptions
		{
			RecurseSubdirectories = a.Bool("recursive", fallback: false),
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.None
		};
		StringBuilder stringBuilder = new StringBuilder(text + "\n");
		int num2 = 0;
		foreach (FileSystemInfo item in new DirectoryInfo(text).EnumerateFileSystemInfos(a.Str("pattern") ?? "*", enumerationOptions))
		{
			string relativePath = Path.GetRelativePath(text, item.FullName);
			stringBuilder.AppendLine((item is FileInfo fileInfo) ? $"{relativePath,-60} {fileInfo.Length,12:N0} B  {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}" : $"{relativePath + "\\",-60} {"<dir>",12}    {item.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
			if (++num2 >= num)
			{
				stringBuilder.AppendLine("… (truncated)");
				break;
			}
		}
		return McpResult.Text(stringBuilder.ToString());
	}

	private McpResult FileRead(JsonObject a)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		string text = FullPath(a.Str("path"));
		if (!File.Exists(text))
		{
			return McpResult.Error("No such file: " + text);
		}
		string text2 = (a.Str("mode") ?? "text").ToLowerInvariant();
		string text3 = Path.GetExtension(text).ToLowerInvariant();
		bool flag = text2 == "image";
		if (!flag)
		{
			bool flag2 = text2 == "text";
			if (flag2)
			{
				bool flag3;
				switch (text3)
				{
				case ".png":
				case ".jpg":
				case ".jpeg":
				case ".gif":
				case ".bmp":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = flag3;
			}
			flag = flag2;
		}
		if (flag)
		{
			using (Bitmap bitmap = new Bitmap(text))
			{
				double num = Math.Min(1.0, 1600.0 / (double)Math.Max(1, bitmap.Width));
				using Bitmap bitmap2 = ((num < 0.999) ? RemoteNative.Scale(bitmap, num) : null);
				return McpResult.Text($"{text}: {bitmap.Width}x{bitmap.Height}").AddImage(RemoteNative.Png(bitmap2 ?? bitmap), "image/png");
			}
		}
		int num2 = Math.Max(0, a.Int("offset", 0, 0));
		int num3 = a.Int("max_bytes", 200000, 1, 4000000);
		using FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		long length = fileStream.Length;
		fileStream.Seek(Math.Min(num2, length), SeekOrigin.Begin);
		byte[] array = new byte[(int)Math.Min(num3, Math.Max(0L, length - fileStream.Position))];
		int i;
		int num4;
		for (i = 0; i < array.Length; i += num4)
		{
			num4 = fileStream.Read(array, i, array.Length - i);
			if (num4 <= 0)
			{
				break;
			}
		}
		string text4 = $"{text}: {length:N0} bytes, returned {i:N0} from offset {num2:N0}{((num2 + i < length) ? " (more available)" : "")}\n";
		return (text2 == "base64") ? McpResult.Text(text4 + Convert.ToBase64String(array, 0, i)) : McpResult.Text(text4 + Encoding.UTF8.GetString(array, 0, i));
	}

	private McpResult FileWrite(JsonObject a)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		string text = FullPath(a.Str("path"));
		string text2 = a.Str("base64");
		byte[] array;
		if (text2 != null)
		{
			array = Convert.FromBase64String(text2);
		}
		else
		{
			if (a["text"] == null)
			{
				return McpResult.Error("text or base64 is required");
			}
			array = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(a.Str("text") ?? "");
		}
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		if (a.Bool("append", fallback: false))
		{
			using FileStream fileStream = new FileStream(text, FileMode.Append, FileAccess.Write, FileShare.Read);
			fileStream.Write(array);
		}
		else
		{
			File.WriteAllBytes(text, array);
		}
		return McpResult.Text($"wrote {array.Length:N0} bytes → {text} (now {new FileInfo(text).Length:N0} bytes)");
	}

	private async Task<McpResult> InstallAsync(JsonObject a, CancellationToken ct)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		StringBuilder log = new StringBuilder();
		string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		string text = a.Str("url");
		string zipPath;
		if (text != null && text.Length > 0)
		{
			Directory.CreateDirectory(_paths.Updates);
			zipPath = Path.Combine(_paths.Updates, "remote-" + stamp + ".zip");
			using HttpClient http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(80.0)
			};
			http.DefaultRequestHeaders.UserAgent.ParseAdd("Metamorph-RemoteHelp");
			string path = zipPath;
			await File.WriteAllBytesAsync(path, await http.GetByteArrayAsync(text, ct), ct);
			log.AppendLine($"downloaded {new FileInfo(zipPath).Length:N0} B → {zipPath}");
		}
		else
		{
			zipPath = FullPath(a.Str("zip"));
		}
		if (!File.Exists(zipPath))
		{
			return McpResult.Error("zip not found: " + zipPath);
		}
		string text2 = a.Str("target");
		string target = ((text2 != null && text2.Length > 0) ? FullPath(text2) : _installDir);
		using ZipArchive zip = ZipFile.OpenRead(zipPath);
		List<ZipArchiveEntry> source2 = zip.Entries.Where((ZipArchiveEntry e) => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\')).ToList();
		string prefix = CommonPrefix(source2.Select((ZipArchiveEntry e) => e.FullName.Replace('\\', '/')).ToList());
		List<(ZipArchiveEntry Entry, string Rel)> entries = (from x in source2.Select(delegate(ZipArchiveEntry e)
			{
				string text3 = e.FullName.Replace('\\', '/');
				int length = prefix.Length;
				return (Entry: e, Rel: text3.Substring(length, text3.Length - length));
			})
			where x.Rel.Length > 0
			select x).ToList();
		if (!entries.Any(((ZipArchiveEntry Entry, string Rel) x) => x.Rel.Equals("Metamorph.exe", StringComparison.OrdinalIgnoreCase)))
		{
			return McpResult.Error("The zip has no Metamorph.exe at its root (or inside a single top folder).");
		}
		StringBuilder stringBuilder = log;
		StringBuilder stringBuilder3 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder);
		handler.AppendLiteral("zip: ");
		handler.AppendFormatted(entries.Count);
		handler.AppendLiteral(" files");
		handler.AppendFormatted((prefix.Length > 0) ? (" (top folder '" + prefix.TrimEnd('/') + "' stripped)") : "");
		handler.AppendLiteral(" → ");
		handler.AppendFormatted(target);
		stringBuilder3.AppendLine(ref handler);
		StringBuilder stringBuilder4 = log;
		stringBuilder4.AppendLine(await StopMainAsync(force: false, ct));
		Directory.CreateDirectory(BackupsDir);
		string backup = Path.Combine(BackupsDir, "build-" + stamp + ".zip");
		List<string> added = new List<string>();
		using (ZipArchive archive = ZipFile.Open(backup, ZipArchiveMode.Create))
		{
			foreach (var item3 in entries)
			{
				string item = item3.Rel;
				string path2 = SafeCombine(target, item);
				if (File.Exists(path2))
				{
					using FileStream source = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
					ZipArchiveEntry zipArchiveEntry = archive.CreateEntry(item, CompressionLevel.Fastest);
					using Stream stream = zipArchiveEntry.Open();
					await source.CopyToAsync(stream, ct);
				}
				else
				{
					added.Add(item);
				}
			}
			ZipArchiveEntry zipArchiveEntry2 = archive.CreateEntry("_metamorph_added_files.txt");
			StreamWriter writer = new StreamWriter(zipArchiveEntry2.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			try
			{
				await writer.WriteAsync(string.Join("\n", added));
			}
			finally
			{
				if (writer != null)
				{
					await writer.DisposeAsync();
				}
			}
		}
		stringBuilder = log;
		StringBuilder stringBuilder5 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(25, 3, stringBuilder);
		handler.AppendLiteral("backup: ");
		handler.AppendFormatted(backup);
		handler.AppendLiteral(" (");
		handler.AppendFormatted(new FileInfo(backup).Length, "N0");
		handler.AppendLiteral(" B, ");
		handler.AppendFormatted(added.Count);
		handler.AppendLiteral(" new files)");
		stringBuilder5.AppendLine(ref handler);
		PruneBackups();
		int num = 0;
		foreach (var item4 in entries)
		{
			ZipArchiveEntry entry = item4.Entry;
			string item2 = item4.Rel;
			string dest = SafeCombine(target, item2);
			int num2 = num;
			num = num2 + ((await ReplaceFileAsync(dest, async delegate(Stream s)
			{
				Stream e2 = entry.Open();
				try
				{
					await e2.CopyToAsync(s, ct);
				}
				finally
				{
					if (e2 != null)
					{
						await e2.DisposeAsync();
					}
				}
			}, stamp, ct)) ? 1 : 0);
		}
		stringBuilder = log;
		StringBuilder stringBuilder6 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 2, stringBuilder);
		handler.AppendLiteral("extracted ");
		handler.AppendFormatted(entries.Count);
		handler.AppendLiteral(" files");
		handler.AppendFormatted((num > 0) ? $"; {num} locked file(s) renamed to *.old-{stamp} (removed on next start)" : "");
		stringBuilder6.AppendLine(ref handler);
		if (a.Bool("start", fallback: true))
		{
			stringBuilder4 = log;
			stringBuilder4.AppendLine(await StartMainAsync(a.Str("args") ?? "", ct));
		}
		log.AppendLine("Undo: rollback_build {backup:'" + Path.GetFileName(backup) + "'}");
		return McpResult.Text(log.ToString());
	}

	private async Task<McpResult> RollbackAsync(JsonObject a, CancellationToken ct)
	{
		McpResult mcpResult = Denied();
		if (mcpResult != null)
		{
			return mcpResult;
		}
		List<FileInfo> list = (Directory.Exists(BackupsDir) ? (from f in new DirectoryInfo(BackupsDir).GetFiles("build-*.zip")
			orderby f.Name descending
			select f).ToList() : new List<FileInfo>());
		if (a.Bool("list", fallback: false) || list.Count == 0)
		{
			return McpResult.Text((list.Count == 0) ? "No build backups" : string.Join("\n", list.Select((FileInfo f) => $"{f.Name}  {f.Length:N0} B")));
		}
		string name = a.Str("backup");
		FileInfo chosen = (string.IsNullOrWhiteSpace(name) ? list[0] : list.FirstOrDefault((FileInfo f) => f.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
		if (chosen == null)
		{
			return McpResult.Error("No such backup: " + name);
		}
		StringBuilder log = new StringBuilder("rollback from " + chosen.Name + "\n");
		StringBuilder stringBuilder = log;
		stringBuilder.AppendLine(await StopMainAsync(force: false, ct));
		string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
		using (ZipArchive zip = ZipFile.OpenRead(chosen.FullName))
		{
			int restored = 0;
			foreach (ZipArchiveEntry entry in zip.Entries.Where((ZipArchiveEntry e) => !e.FullName.EndsWith('/')))
			{
				if (entry.FullName == "_metamorph_added_files.txt")
				{
					using (StreamReader reader = new StreamReader(entry.Open()))
					{
						string[] array = (await reader.ReadToEndAsync(ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
						foreach (string text in array)
						{
							string text2 = SafeCombine(_installDir, text.Trim());
							try
							{
								if (File.Exists(text2))
								{
									File.Delete(text2);
								}
							}
							catch
							{
								try
								{
									File.Move(text2, text2 + ".old-" + stamp);
								}
								catch
								{
								}
							}
						}
					}
					continue;
				}
				await ReplaceFileAsync(SafeCombine(_installDir, entry.FullName), async delegate(Stream s)
				{
					Stream e2 = entry.Open();
					try
					{
						await e2.CopyToAsync(s, ct);
					}
					finally
					{
						if (e2 != null)
						{
							await e2.DisposeAsync();
						}
					}
				}, stamp, ct);
				restored++;
			}
			StringBuilder stringBuilder2 = log;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder2);
			handler.AppendLiteral("restored ");
			handler.AppendFormatted(restored);
			handler.AppendLiteral(" files");
			stringBuilder2.AppendLine(ref handler);
		}
		if (a.Bool("start", fallback: true))
		{
			stringBuilder = log;
			stringBuilder.AppendLine(await StartMainAsync("", ct));
		}
		return McpResult.Text(log.ToString());
	}

	private static async Task<bool> ReplaceFileAsync(string dest, Func<Stream, Task> write, string stamp, CancellationToken ct)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(dest));
		string temp = dest + ".new-" + stamp;
		FileStream stream = File.Create(temp);
		try
		{
			await write(stream);
		}
		finally
		{
			if (stream != null)
			{
				await stream.DisposeAsync();
			}
		}
		try
		{
			File.Move(temp, dest, overwrite: true);
			return false;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			File.Move(dest, dest + ".old-" + stamp, overwrite: true);
			File.Move(temp, dest);
			return true;
		}
	}

	private static string SafeCombine(string root, string rel)
	{
		string fullPath = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
		ReadOnlySpan<char> readOnlySpan = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		char reference = Path.DirectorySeparatorChar;
		string value = string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference));
		if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Unsafe path in zip: " + rel);
		}
		return fullPath;
	}

	internal static string CommonPrefix(List<string> names)
	{
		if (names.Count == 0)
		{
			return "";
		}
		int num = names[0].IndexOf('/');
		if (num <= 0)
		{
			return "";
		}
		string prefix = names[0].Substring(0, num + 1);
		if (!names.All((string n) => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
		{
			return "";
		}
		return prefix;
	}

	private void PruneBackups()
	{
		try
		{
			foreach (FileInfo item in (from f in new DirectoryInfo(BackupsDir).GetFiles("build-*.zip")
				orderby f.Name descending
				select f).Skip(5))
			{
				item.Delete();
			}
		}
		catch
		{
		}
	}

	private async Task<McpResult> InputAsync(JsonObject a, CancellationToken ct)
	{
		McpResult mcpResult = Denied(input: true);
		if (mcpResult != null)
		{
			return mcpResult;
		}
		string action = (a.Str("action") ?? "").ToLowerInvariant();
		RemoteNative.POINT cursor = RemoteNative.Cursor();
		int x = a.Int("x", cursor.X);
		int y = a.Int("y", cursor.Y);
		await InputLock.WaitAsync(ct);
		string done;
		try
		{
			switch (action)
			{
			case "cursor":
				return McpResult.Text($"cursor at {cursor.X},{cursor.Y}");
			case "move":
				RemoteNative.MoveTo(x, y);
				done = $"moved to {x},{y}";
				break;
			case "click":
			case "left_click":
				RemoteNative.Click(x, y, "left", 1);
				done = $"clicked {x},{y}";
				break;
			case "double_click":
				RemoteNative.Click(x, y, "left", 2);
				done = $"double-clicked {x},{y}";
				break;
			case "right_click":
				RemoteNative.Click(x, y, "right", 1);
				done = $"right-clicked {x},{y}";
				break;
			case "middle_click":
				RemoteNative.Click(x, y, "middle", 1);
				done = $"middle-clicked {x},{y}";
				break;
			case "scroll":
			{
				int num3 = a.Int("notches", -3, -50, 50);
				RemoteNative.Scroll(x, y, num3);
				done = $"scrolled {num3} at {x},{y}";
				break;
			}
			case "drag":
			{
				int num = a.Int("x2", x);
				int num2 = a.Int("y2", y);
				RemoteNative.Drag(x, y, num, num2);
				done = $"dragged {x},{y} → {num},{num2}";
				break;
			}
			case "type":
			{
				string text2 = a.Str("text") ?? "";
				RemoteNative.TypeText(text2);
				done = $"typed {text2.Length} chars";
				break;
			}
			case "hotkey":
			case "key":
			{
				string text = a.Str("keys") ?? a.Str("text") ?? "";
				RemoteNative.Hotkey(text);
				done = "pressed " + text;
				break;
			}
			default:
				return McpResult.Error("action must be move | click | double_click | right_click | middle_click | scroll | drag | type | hotkey | cursor");
			}
		}
		finally
		{
			InputLock.Release();
		}
		await Task.Delay(a.Int("wait_ms", 400, 0, 10000), ct);
		if (!a.Bool("screenshot", fallback: false))
		{
			return McpResult.Text(done);
		}
		McpResult mcpResult2 = Screenshot(new JsonObject { ["monitor"] = a.Int("monitor", -1) });
		mcpResult2.Content.Insert(0, new JsonObject
		{
			["type"] = "text",
			["text"] = done
		});
		return mcpResult2;
	}
}
