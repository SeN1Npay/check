using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Localization;

namespace Metamorph.Core.Services;

public static class ScriptRunner
{
	public static string EncodeCommand(string command)
	{
		return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
	}

	public static ProcessStartInfo StartInfo(string command)
	{
		return new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + EncodeCommand(command))
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
	}

	public static async Task<string> RunAsync(string command, TimeSpan timeout, CancellationToken ct = default(CancellationToken))
	{
		using Process process = new Process
		{
			StartInfo = StartInfo("[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + command)
		};
		process.Start();
		Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
		Task<string> stderr = process.StandardError.ReadToEndAsync(ct);
		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(timeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch
			{
			}
			return Loc.T("таймаут");
		}
		string text = FirstLine(await stdout);
		return (text.Length <= 0) ? FirstLine(await stderr) : text;
	}

	public static void Fire(string command)
	{
		try
		{
			ProcessStartInfo processStartInfo = StartInfo(command);
			bool redirectStandardOutput = (processStartInfo.RedirectStandardError = false);
			processStartInfo.RedirectStandardOutput = redirectStandardOutput;
			using (Process.Start(processStartInfo))
			{
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Скрипт по клику: " + ex.Message);
		}
	}

	public static string FirstLine(string text)
	{
		return (from l in text.Split('\n')
			select l.Trim()).FirstOrDefault((string l) => l.Length > 0) ?? "";
	}
}
