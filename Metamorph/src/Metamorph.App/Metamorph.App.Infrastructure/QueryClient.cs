using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.App.Infrastructure;

internal static class QueryClient
{
	public static async Task<string?> AskAsync(string query, int timeoutMs = 5000)
	{
		_ = 3;
		try
		{
			using (CancellationTokenSource cts = new CancellationTokenSource(timeoutMs))
			{
				NamedPipeClientStream client = new NamedPipeClientStream(".", QueryServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
				string result;
				try
				{
					await client.ConnectAsync(Math.Min(timeoutMs, 2000), cts.Token);
					await new StreamWriter(client, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), -1, leaveOpen: true)
					{
						AutoFlush = true
					}.WriteLineAsync(query.AsMemory(), cts.Token);
					result = await new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, -1, leaveOpen: true).ReadToEndAsync(cts.Token);
				}
				finally
				{
					if (client != null)
					{
						await client.DisposeAsync();
					}
				}
				return result;
			}
			IL_02d6:
			string result2;
			return result2;
		}
		catch
		{
			return null;
		}
	}
}
