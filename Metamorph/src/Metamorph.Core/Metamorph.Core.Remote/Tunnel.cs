using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Remote;

public sealed class Tunnel : IDisposable
{
	public const string CloudflaredUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";

	private readonly Process _process;

	public string Provider { get; }

	public string PublicUrl { get; }

	public int ProcessId
	{
		get
		{
			try
			{
				return _process.Id;
			}
			catch
			{
				return 0;
			}
		}
	}

	public bool HasExited
	{
		get
		{
			try
			{
				return _process.HasExited;
			}
			catch
			{
				return true;
			}
		}
	}

	private Tunnel(Process process, string provider, string url)
	{
		_process = process;
		Provider = provider;
		PublicUrl = url;
	}

	public static async Task<string> EnsureCloudflaredAsync(string toolsDir, Action<long, long?>? progress, CancellationToken ct)
	{
		string exe = Path.Combine(toolsDir, "cloudflared.exe");
		if (File.Exists(exe) && new FileInfo(exe).Length > 1000000)
		{
			return exe;
		}
		Directory.CreateDirectory(toolsDir);
		using HttpClient http = new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(10.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Metamorph-RemoteHelp");
		using HttpResponseMessage response = await http.GetAsync("https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe", HttpCompletionOption.ResponseHeadersRead, ct);
		response.EnsureSuccessStatusCode();
		long? total = response.Content.Headers.ContentLength;
		string temp = exe + ".download";
		Stream source = await response.Content.ReadAsStreamAsync(ct);
		try
		{
			FileStream target = File.Create(temp);
			try
			{
				byte[] buffer = new byte[131072];
				long done = 0L;
				long last = 0L;
				while (true)
				{
					int num;
					int i = (num = await source.ReadAsync(buffer, ct));
					if (num <= 0)
					{
						break;
					}
					await target.WriteAsync(buffer.AsMemory(0, i), ct);
					done += i;
					if (done - last > 524288)
					{
						progress?.Invoke(done, total);
						last = done;
					}
				}
				progress?.Invoke(done, total);
			}
			finally
			{
				if (target != null)
				{
					await target.DisposeAsync();
				}
			}
		}
		finally
		{
			if (source != null)
			{
				await source.DisposeAsync();
			}
		}
		File.Move(temp, exe, overwrite: true);
		return exe;
	}

	public static Task<Tunnel> StartCloudflaredAsync(string exe, int port, Action<string>? output, TimeSpan timeout, CancellationToken ct)
	{
		return StartAsync("cloudflared", exe, $"tunnel --no-autoupdate --url http://127.0.0.1:{port}", output, timeout, ct);
	}

	public static Task<Tunnel> StartLocalhostRunAsync(int port, Action<string>? output, TimeSpan timeout, CancellationToken ct)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
		if (!File.Exists(text))
		{
			text = "ssh";
		}
		string args = "-T -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL -o ServerAliveInterval=30 -o ExitOnForwardFailure=yes " + $"-R 80:127.0.0.1:{port} nokey@localhost.run";
		return StartAsync("localhost.run", text, args, output, timeout, ct);
	}

	private static async Task<Tunnel> StartAsync(string provider, string exe, string args, Action<string>? output, TimeSpan timeout, CancellationToken ct)
	{
		Action<string> output2 = output;
		string provider2 = provider;
		ProcessStartInfo startInfo = new ProcessStartInfo(exe, args)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			RedirectStandardInput = true
		};
		Process process = new Process
		{
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		TaskCompletionSource<string> found = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			OnLine(e.Data);
		};
		process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			OnLine(e.Data);
		};
		process.Exited += delegate
		{
			found.TrySetException(new InvalidOperationException($"{provider2} завершился (код {SafeExitCode(process)})"));
		};
		if (!process.Start())
		{
			throw new InvalidOperationException("Не удалось запустить " + exe);
		}
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		using (CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(ct))
		{
			timer.CancelAfter(timeout);
			try
			{
				string url = await found.Task.WaitAsync(timer.Token);
				return new Tunnel(process, provider2, url);
			}
			catch
			{
				Kill(process);
				throw;
			}
		}
		void OnLine(string? line)
		{
			if (line != null)
			{
				try
				{
					output2?.Invoke(line);
				}
				catch
				{
				}
				string text = TunnelUrl.Find(line);
				if (text != null)
				{
					found.TrySetResult(text);
				}
			}
		}
	}

	private static int SafeExitCode(Process p)
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

	private static void Kill(Process p)
	{
		try
		{
			if (!p.HasExited)
			{
				p.Kill(entireProcessTree: true);
			}
		}
		catch
		{
		}
		try
		{
			p.Dispose();
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		Kill(_process);
	}
}
