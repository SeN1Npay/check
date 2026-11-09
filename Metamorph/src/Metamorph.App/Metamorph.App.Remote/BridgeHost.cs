using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Localization;
using Metamorph.Core.Remote;
using Metamorph.Core.Services;

namespace Metamorph.App.Remote;

internal sealed class BridgeHost : IDisposable
{
	public const string Instructions = "Metamorph Remote Help: a bridge to the user's Windows PC, open only while the user keeps the 'Remote help' window open. Purpose: diagnose and fix the Metamorph desktop customisation app (bar, dock, island, GIF/MP4 wallpapers, mascot) on real hardware.\nCoordinates are physical pixels of the virtual screen (see status). screenshot explains how image pixels map to screen pixels.\nWorkflow: status → screenshot → state/trace/logs → reproduce → screen_burst (flicker meter: look for 'FLICKER'/'BLINK' and the changed boxes) → build a fix → upload the zip with HTTP PUT {base}/files?path=%TEMP%\\mm.zip (base = this MCP URL without the trailing /mcp; large files in chunks with &append=1) → install_build {zip} → verify → rollback_build if it got worse. trace is the in-memory event log of the running app (AppBar, broadcasts, fullscreen, wallpaper restarts).\nRules: explain to the user what you are doing; do not open personal files or accounts unrelated to Metamorph; avoid destructive commands; every request must finish within ~90 s (tunnel limit), so split long work. Tools that need the user's permission return an error if it was switched off.";

	private readonly AppPaths _paths;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	private MiniHttpServer? _http;

	private Tunnel? _tunnel;

	private int _cloudflaredLines;

	public BridgePermissions Permissions { get; } = new BridgePermissions();


	public McpServer Mcp { get; }

	public BridgeRouter Router { get; }

	public string Token { get; }

	public string? PublicBase { get; private set; }

	public string? McpUrl
	{
		get
		{
			if (PublicBase != null)
			{
				return PublicBase + "/" + Token + "/mcp";
			}
			return null;
		}
	}

	public string LocalMcpUrl => $"http://127.0.0.1:{_http?.Port ?? 0}/{Token}/mcp";

	public event Action<string, bool>? StatusChanged;

	public event Action<string>? Activity;

	public event Action? UrlChanged;

	public BridgeHost(AppPaths paths, Func<string, string> bridgeAction)
	{
		_paths = paths;
		Token = BridgeRouter.NewToken();
		Mcp = new McpServer("metamorph-remote-help", App.VersionText, "Metamorph Remote Help: a bridge to the user's Windows PC, open only while the user keeps the 'Remote help' window open. Purpose: diagnose and fix the Metamorph desktop customisation app (bar, dock, island, GIF/MP4 wallpapers, mascot) on real hardware.\nCoordinates are physical pixels of the virtual screen (see status). screenshot explains how image pixels map to screen pixels.\nWorkflow: status → screenshot → state/trace/logs → reproduce → screen_burst (flicker meter: look for 'FLICKER'/'BLINK' and the changed boxes) → build a fix → upload the zip with HTTP PUT {base}/files?path=%TEMP%\\mm.zip (base = this MCP URL without the trailing /mcp; large files in chunks with &append=1) → install_build {zip} → verify → rollback_build if it got worse. trace is the in-memory event log of the running app (AppBar, broadcasts, fullscreen, wallpaper restarts).\nRules: explain to the user what you are doing; do not open personal files or accounts unrelated to Metamorph; avoid destructive commands; every request must finish within ~90 s (tunnel limit), so split long work. Tools that need the user's permission return an error if it was switched off.");
		new BridgeTools(paths, Permissions, bridgeAction).Register(Mcp);
		Router = new BridgeRouter(Token, Mcp, () => Permissions.Commands);
		Mcp.Called += delegate(string name, JsonObject args, McpResult result, TimeSpan time)
		{
			string text2 = $"{name}{Describe(args)} — {(result.IsError ? Loc.T("ошибка") : Loc.T("готово"))}, {time.TotalSeconds:0.0} {Loc.T("с")}";
			Log.Info("MCP " + text2 + (result.IsError ? (": " + result.Summary()) : ""));
			this.Activity?.Invoke(text2);
		};
		Router.FileTransferred += delegate(string path, long bytes, bool upload)
		{
			string text = $"{(upload ? Loc.T("файл получен") : Loc.T("файл отправлен"))}: {path} ({(double)bytes / 1048576.0:0.0} {Loc.T("МБ")})";
			Log.Info(text);
			this.Activity?.Invoke(text);
		};
	}

	public void Start()
	{
		_http = new MiniHttpServer(Router.HandleAsync);
		_http.Error += delegate(Exception ex)
		{
			Log.Warn("HTTP: " + ex.Message);
		};
		_http.Start();
		Log.Info($"Удалённая помощь: локальный сервер 127.0.0.1:{_http.Port}");
		Task.Run((Func<Task?>)RunAsync);
	}

	private void Report(string text, bool ok)
	{
		try
		{
			this.StatusChanged?.Invoke(text, ok);
		}
		catch
		{
		}
	}

	private async Task RunAsync()
	{
		int failures = 0;
		while (!_cts.IsCancellationRequested)
		{
			Tunnel tunnel = null;
			try
			{
				tunnel = await ConnectAsync(_cts.Token);
				RemoteNative.KillWithUs(tunnel.ProcessId);
				_tunnel = tunnel;
				PublicBase = tunnel.PublicUrl.TrimEnd('/');
				Log.Info("Удалённая помощь: туннель " + tunnel.Provider + " " + PublicBase);
				this.UrlChanged?.Invoke();
				failures = 0;
				Report(Loc.T("Проверяю ссылку…"), ok: false);
				Report((await SelfCheckAsync(PublicBase, _cts.Token)) ? Loc.F("Готово ({0}). Скопируйте ссылку и отправьте её ИИ-помощнику.", tunnel.Provider) : Loc.T("Ссылка создана, но пока не отвечает — обычно она начинает работать в течение минуты. Можно уже отправлять."), ok: true);
				while (!_cts.IsCancellationRequested && !tunnel.HasExited)
				{
					await Task.Delay(2000, _cts.Token);
				}
				if (_cts.IsCancellationRequested)
				{
					break;
				}
				Log.Warn("Удалённая помощь: туннель завершился, переподключаюсь");
				PublicBase = null;
				this.UrlChanged?.Invoke();
				Report(Loc.T("Соединение прервалось, создаю новую ссылку… (её нужно будет отправить заново)"), ok: false);
			}
			catch (OperationCanceledException) when (_cts.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex2)
			{
				failures++;
				int num = Math.Min(60, 10 * failures);
				Log.Warn("Удалённая помощь: " + ex2.Message);
				Report(Loc.F("Не удалось создать ссылку: {0}. Повтор через {1} с…", ex2.Message, num), ok: false);
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(num), _cts.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
			finally
			{
				if (tunnel != null && (tunnel.HasExited || _cts.IsCancellationRequested))
				{
					tunnel.Dispose();
					if (_tunnel == tunnel)
					{
						_tunnel = null;
					}
				}
			}
		}
	}

	private async Task<Tunnel> ConnectAsync(CancellationToken ct)
	{
		int port = _http.Port;
		string cloudflareError;
		try
		{
			Report(Loc.T("Готовлю cloudflared…"), ok: false);
			string exe = await Tunnel.EnsureCloudflaredAsync(Path.Combine(_paths.Root, "tools"), delegate(long done, long? total)
			{
				Report((total.HasValue && total.GetValueOrDefault() > 0) ? Loc.F("Скачиваю cloudflared: {0:0} из {1:0} МБ…", (double)done / 1048576.0, (double)total.Value / 1048576.0) : Loc.F("Скачиваю cloudflared: {0:0} МБ…", (double)done / 1048576.0), ok: false);
			}, ct);
			Report(Loc.T("Создаю ссылку через Cloudflare…"), ok: false);
			_cloudflaredLines = 0;
			return await Tunnel.StartCloudflaredAsync(exe, port, delegate(string line)
			{
				if (Interlocked.Increment(ref _cloudflaredLines) <= 80 || line.Contains("ERR", StringComparison.Ordinal))
				{
					Log.Info("cloudflared: " + line);
				}
			}, TimeSpan.FromSeconds(60.0), ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			cloudflareError = ex2.Message;
			Log.Warn("cloudflared: " + ex2.Message);
		}
		Report(Loc.T("Cloudflare недоступен, пробую localhost.run…"), ok: false);
		try
		{
			return await Tunnel.StartLocalhostRunAsync(port, delegate(string line)
			{
				Log.Info("localhost.run: " + line);
			}, TimeSpan.FromSeconds(40.0), ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex4)
		{
			throw new InvalidOperationException("cloudflared — " + cloudflareError + "; localhost.run — " + ex4.Message);
		}
	}

	private async Task<bool> SelfCheckAsync(string publicBase, CancellationToken ct)
	{
		using HttpClient http = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(8.0)
		};
		for (int i = 0; i < 10; i++)
		{
			if (ct.IsCancellationRequested)
			{
				break;
			}
			try
			{
				if ((await http.GetStringAsync(publicBase + "/" + Token + "/health", ct)).Contains("\"ok\"", StringComparison.Ordinal))
				{
					return true;
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex2)
			{
				Log.Info($"Проверка ссылки ({i + 1}): {ex2.Message}");
			}
			await Task.Delay(3000, ct);
		}
		return false;
	}

	private static string Describe(JsonObject args)
	{
		if (args.Count == 0)
		{
			return "";
		}
		IEnumerable<string> values = args.Take(4).Select<KeyValuePair<string, JsonNode>, string>(delegate(KeyValuePair<string, JsonNode> kv)
		{
			string text = kv.Value?.ToJsonString(McpServer.Json) ?? "null";
			bool flag;
			switch (kv.Key)
			{
			case "script":
			case "text":
			case "base64":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				text = $"[{text.Length}]";
			}
			else if (text.Length > 40)
			{
				text = text.Substring(0, 40) + "…";
			}
			return kv.Key + "=" + text;
		});
		return " (" + string.Join(", ", values) + ")";
	}

	public void Dispose()
	{
		_cts.Cancel();
		try
		{
			_tunnel?.Dispose();
		}
		catch
		{
		}
		try
		{
			_http?.Dispose();
		}
		catch
		{
		}
		PublicBase = null;
		Log.Info("Удалённая помощь остановлена");
	}
}
