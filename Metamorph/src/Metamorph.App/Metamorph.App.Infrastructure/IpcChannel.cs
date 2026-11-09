using System;
using System.Diagnostics;

namespace Metamorph.App.Infrastructure;

internal static class IpcChannel
{
	public static string PipeName { get; } = $"Metamorph.Ipc.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";

}
