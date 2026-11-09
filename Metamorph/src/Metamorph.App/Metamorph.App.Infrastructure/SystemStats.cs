using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using Metamorph.Core.Native;

namespace Metamorph.App.Infrastructure;

internal sealed class SystemStats
{
	private long _idle;

	private long _kernel;

	private long _user;

	private bool _hasCpu;

	private long _rx;

	private long _tx;

	private DateTime _netAt;

	public double? Cpu()
	{
		if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
		{
			return null;
		}
		double? result = null;
		if (_hasCpu)
		{
			long num = kernelTime - _kernel + (userTime - _user);
			if (num > 0)
			{
				result = Math.Clamp(100.0 * (double)(num - (idleTime - _idle)) / (double)num, 0.0, 100.0);
			}
		}
		long idle = idleTime;
		long kernel = kernelTime;
		long user = userTime;
		_idle = idle;
		_kernel = kernel;
		_user = user;
		_hasCpu = true;
		return result;
	}

	public static (double UsedGb, double TotalGb, uint Load)? Memory()
	{
		NativeMethods.MEMORYSTATUSEX buffer = NativeMethods.MEMORYSTATUSEX.Create();
		if (!NativeMethods.GlobalMemoryStatusEx(ref buffer))
		{
			return null;
		}
		return ((double)(buffer.ullTotalPhys - buffer.ullAvailPhys) / 1073741824.0, (double)buffer.ullTotalPhys / 1073741824.0, buffer.dwMemoryLoad);
	}

	public static (string Name, double UsedPercent, double FreeGb)? Disk(string? root = null)
	{
		try
		{
			if (root == null)
			{
				root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
			}
			DriveInfo driveInfo = new DriveInfo(root);
			if (!driveInfo.IsReady || driveInfo.TotalSize <= 0)
			{
				return null;
			}
			return (driveInfo.Name.TrimEnd('\\'), 100.0 * (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / (double)driveInfo.TotalSize, (double)driveInfo.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0);
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}
	}

	public (double Down, double Up)? Network()
	{
		long num = 0L;
		long num2 = 0L;
		try
		{
			foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces().Where(delegate(NetworkInterface n)
			{
				bool flag = n.OperationalStatus == OperationalStatus.Up;
				if (flag)
				{
					NetworkInterfaceType networkInterfaceType = n.NetworkInterfaceType;
					bool flag2 = ((networkInterfaceType == NetworkInterfaceType.Loopback || networkInterfaceType == NetworkInterfaceType.Tunnel) ? true : false);
					flag = !flag2;
				}
				return flag;
			}))
			{
				IPInterfaceStatistics iPStatistics = item.GetIPStatistics();
				num += iPStatistics.BytesReceived;
				num2 += iPStatistics.BytesSent;
			}
		}
		catch (NetworkInformationException)
		{
			return null;
		}
		DateTime utcNow = DateTime.UtcNow;
		(double, double)? result = null;
		if (_netAt != default(DateTime))
		{
			double totalSeconds = (utcNow - _netAt).TotalSeconds;
			if (totalSeconds > 0.2)
			{
				result = (Math.Max(0.0, (double)(num - _rx) / totalSeconds), Math.Max(0.0, (double)(num2 - _tx) / totalSeconds));
			}
		}
		long rx = num;
		long tx = num2;
		DateTime netAt = utcNow;
		_rx = rx;
		_tx = tx;
		_netAt = netAt;
		return result;
	}

	public static string Rate(double bytesPerSecond)
	{
		if (!(bytesPerSecond >= 1048576.0))
		{
			if (bytesPerSecond >= 1024.0)
			{
				return $"{bytesPerSecond / 1024.0:0}K";
			}
			return $"{bytesPerSecond:0}B";
		}
		return $"{bytesPerSecond / 1024.0 / 1024.0:0.0}M";
	}

	public static string Uptime()
	{
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(Environment.TickCount64);
		if (timeSpan.TotalDays >= 1.0)
		{
			return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
		}
		return $"{timeSpan.Hours}h {timeSpan.Minutes:00}m";
	}

	public static (int Percent, bool Charging)? Battery()
	{
		NativeMethods.SYSTEM_POWER_STATUS status;
		bool flag = !NativeMethods.GetSystemPowerStatus(out status);
		if (!flag)
		{
			byte batteryFlag = status.BatteryFlag;
			bool flag2 = ((batteryFlag == 128 || batteryFlag == byte.MaxValue) ? true : false);
			flag = flag2;
		}
		if (flag || status.BatteryLifePercent > 100)
		{
			return null;
		}
		return (status.BatteryLifePercent, status.ACLineStatus == 1);
	}
}
