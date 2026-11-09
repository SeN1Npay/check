using System;
using System.Globalization;
using System.IO;

namespace Metamorph.Core.Services;

public sealed class CrashGuard
{
	public const int Threshold = 2;

	private readonly string _path;

	public int FailedStarts { get; private set; }

	public CrashGuard(string path)
	{
		_path = path;
	}

	public bool Begin()
	{
		FailedStarts = Read();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			File.WriteAllText(_path, (FailedStarts + 1).ToString(CultureInfo.InvariantCulture));
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
		}
		return FailedStarts >= 2;
	}

	public void MarkHealthy()
	{
		FailedStarts = 0;
		try
		{
			if (File.Exists(_path))
			{
				File.Delete(_path);
			}
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
		}
	}

	private int Read()
	{
		try
		{
			int result;
			return (File.Exists(_path) && int.TryParse(File.ReadAllText(_path).Trim(), out result)) ? Math.Max(0, result) : 0;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return 0;
		}
	}
}
