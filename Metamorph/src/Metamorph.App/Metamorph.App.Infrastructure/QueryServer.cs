using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal sealed class QueryServer : IDisposable
{
	private readonly Func<string, Task<string>> _answer;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	public static string PipeName { get; } = $"Metamorph.Query.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";


	public QueryServer(Func<string, Task<string>> answer)
	{
		_answer = answer;
	}

	public void Start()
	{
		Task.Run((Func<Task?>)LoopAsync);
		Task.Run((Func<Task?>)LoopAsync);
	}

	private async Task LoopAsync()
	{
		while (!_cts.IsCancellationRequested)
		{
			try
			{
				NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				try
				{
					await server.WaitForConnectionAsync(_cts.Token);
					string arg = (await new StreamReader(server, null, detectEncodingFromByteOrderMarks: true, -1, leaveOpen: true).ReadLineAsync(_cts.Token))?.Trim() ?? "";
					string value;
					try
					{
						value = await _answer(arg);
					}
					catch (Exception ex)
					{
						value = "error: " + ex.Message;
					}
					StreamWriter writer = new StreamWriter(server, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), -1, leaveOpen: true);
					await writer.WriteAsync(value);
					await writer.FlushAsync();
					server.WaitForPipeDrain();
				}
				finally
				{
					if (server != null)
					{
						await server.DisposeAsync();
					}
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex3)
			{
				Log.Warn("Query IPC: " + ex3.Message);
				await Task.Delay(300);
			}
		}
	}

	public void Dispose()
	{
		_cts.Cancel();
		_cts.Dispose();
	}
}
