using System;
using System.Runtime.InteropServices;

namespace Metamorph.App.Bar;

internal sealed class GpuCounter : IDisposable
{
	private struct CounterItem
	{
		public nint Name;

		public uint Status;

		public double Value;
	}

	private const uint PdhMoreData = 2147485650u;

	private const uint PdhFmtDouble = 512u;

	private const uint PdhFmtNoCap100 = 32768u;

	private nint _query;

	private nint _counter;

	private readonly object _gate = new object();

	public bool Available { get; private set; } = true;


	public GpuCounter()
	{
		try
		{
			if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0 || PdhAddEnglishCounter(_query, "\\GPU Engine(*engtype_3D)\\Utilization Percentage", IntPtr.Zero, out _counter) != 0)
			{
				Available = false;
			}
			else
			{
				PdhCollectQueryData(_query);
			}
		}
		catch (Exception ex) when (((ex is DllNotFoundException || ex is EntryPointNotFoundException) ? 1 : 0) != 0)
		{
			Available = false;
		}
	}

	public double? Read()
	{
		lock (_gate)
		{
			if (!Available || _query == IntPtr.Zero)
			{
				return null;
			}
			if (PdhCollectQueryData(_query) != 0)
			{
				return null;
			}
			uint bufferSize = 0u;
			uint itemCount;
			uint num = PdhGetFormattedCounterArray(_counter, 33280u, ref bufferSize, out itemCount, IntPtr.Zero);
			if (num == 0 && itemCount == 0)
			{
				return 0.0;
			}
			if (num != 2147485650u || bufferSize == 0)
			{
				return null;
			}
			nint num2 = Marshal.AllocHGlobal((int)bufferSize);
			try
			{
				if (PdhGetFormattedCounterArray(_counter, 33280u, ref bufferSize, out itemCount, num2) != 0)
				{
					return null;
				}
				int num3 = Marshal.SizeOf<CounterItem>();
				double num4 = 0.0;
				for (int i = 0; i < itemCount; i++)
				{
					CounterItem counterItem = Marshal.PtrToStructure<CounterItem>(num2 + i * num3);
					if (counterItem.Status <= 1 && counterItem.Value > 0.0)
					{
						num4 += counterItem.Value;
					}
				}
				return Math.Clamp(num4, 0.0, 100.0);
			}
			finally
			{
				Marshal.FreeHGlobal(num2);
			}
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_query != IntPtr.Zero)
			{
				PdhCloseQuery(_query);
			}
			_query = IntPtr.Zero;
			Available = false;
		}
	}

	[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
	private static extern uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

	[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
	private static extern uint PdhAddEnglishCounter(nint query, string path, nint userData, out nint counter);

	[DllImport("pdh.dll")]
	private static extern uint PdhCollectQueryData(nint query);

	[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
	private static extern uint PdhGetFormattedCounterArray(nint counter, uint format, ref uint bufferSize, out uint itemCount, nint items);

	[DllImport("pdh.dll")]
	private static extern uint PdhCloseQuery(nint query);
}
