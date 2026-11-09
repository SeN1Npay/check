using System;
using System.IO;

namespace Metamorph.Core.Services;

public static class Log
{
	private static readonly object Sync = new object();

	private static string? _path;

	public static string? FilePath => _path;

	public static void Init(string path)
	{
		_path = path;
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (fileInfo.Exists && fileInfo.Length > 1000000)
			{
				File.Move(path, OldPath(path), overwrite: true);
			}
		}
		catch
		{
		}
	}

	public static string OldPath(string path)
	{
		return Path.ChangeExtension(path, ".old.log");
	}

	public static void Info(string message)
	{
		Write("INFO", message);
	}

	public static void Warn(string message)
	{
		Write("WARN", message);
	}

	public static void Error(string message, Exception? ex = null)
	{
		Write("ERROR", (ex == null) ? message : $"{message}: {ex}");
	}

	private static void Write(string level, string message)
	{
		if (_path == null)
		{
			return;
		}
		try
		{
			lock (Sync)
			{
				File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}
}
