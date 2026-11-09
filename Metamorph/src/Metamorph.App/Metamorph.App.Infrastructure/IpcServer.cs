using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal sealed class IpcServer : IDisposable
{
	private readonly Action<string> _onCommand;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	public IpcServer(Action<string> onCommand)
	{
		_onCommand = onCommand;
	}

	public void Start()
	{
		Task.Run((Func<Task?>)LoopAsync);
	}

	private async Task LoopAsync()
	{
		while (!_cts.IsCancellationRequested)
		{
			try
			{
				using NamedPipeServerStream server = new NamedPipeServerStream(IpcChannel.PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				await server.WaitForConnectionAsync(_cts.Token);
				using StreamReader reader = new StreamReader(server);
				string text = await reader.ReadLineAsync(_cts.Token);
				if (!string.IsNullOrWhiteSpace(text))
				{
					_onCommand(text.Trim());
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex2)
			{
				Log.Error("IPC", ex2);
				await Task.Delay(500);
			}
		}
	}

	public void Dispose()
	{
		_cts.Cancel();
		_cts.Dispose();
	}
}
