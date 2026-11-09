using System.IO;
using System.IO.Pipes;

namespace Metamorph.App.Infrastructure;

internal static class IpcClient
{
	public static bool TrySend(string command)
	{
		try
		{
			using NamedPipeClientStream namedPipeClientStream = new NamedPipeClientStream(".", IpcChannel.PipeName, PipeDirection.Out);
			namedPipeClientStream.Connect(2000);
			using StreamWriter streamWriter = new StreamWriter(namedPipeClientStream)
			{
				AutoFlush = true
			};
			streamWriter.WriteLine(command);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
