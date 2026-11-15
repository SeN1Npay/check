using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Metamorph.App.Bar;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

public sealed record SensorSnapshot(double? Cpu, double? Gpu, double? RamUsedGb, double? RamTotalGb, uint? RamLoad, double? Down, double? Up, int? BatteryPercent, bool Charging, DateTime At)
{
	public static readonly SensorSnapshot Empty = new SensorSnapshot(null, null, null, null, null, null, null, null, Charging: false, DateTime.MinValue);
}

// Один опрос датчиков на всё приложение: панель, виджет и карточки читают общий снимок.
// Опрос идёт только пока есть хотя бы одна аренда (Acquire), GPU — только по отдельной аренде (счётчик дорогой).
// Сам замер — в пуле потоков (PDH и список сетевых адаптеров бывают медленными), снимок и событие — на UI-потоке.
internal sealed class SensorHub
{
	private sealed class Lease : IDisposable
	{
		private SensorHub? _hub;

		private readonly bool _gpu;

		public Lease(SensorHub hub, bool gpu)
		{
			_hub = hub;
			_gpu = gpu;
		}

		public void Dispose()
		{
			Interlocked.Exchange(ref _hub, null)?.Release(_gpu);
		}
	}

	public static SensorHub Shared { get; } = new SensorHub();

	private readonly SystemStats _stats = new SystemStats();

	private readonly object _gpuGate = new object();

	private DispatcherTimer? _timer;

	private GpuCounter? _gpuCounter;

	private int _leases;

	private int _gpuLeases;

	private bool _sampling;

	public SensorSnapshot Current { get; private set; } = SensorSnapshot.Empty;

	// false — счётчик GPU в системе недоступен (модуль прячется).
	public bool GpuAvailable { get; private set; } = true;

	// Новый снимок (на UI-потоке, раз в секунду).
	public event Action<SensorSnapshot>? Updated;

	// Вызывать с UI-потока.
	public IDisposable Acquire(bool gpu = false)
	{
		_leases++;
		if (gpu)
		{
			_gpuLeases++;
		}
		if (_timer == null)
		{
			_timer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = TimeSpan.FromSeconds(1.0)
			};
			_timer.Tick += delegate
			{
				Sample();
			};
		}
		if (!_timer.IsEnabled)
		{
			Sample();
			_timer.Start();
		}
		return new Lease(this, gpu);
	}

	private void Release(bool gpu)
	{
		_leases = Math.Max(0, _leases - 1);
		if (gpu)
		{
			_gpuLeases = Math.Max(0, _gpuLeases - 1);
			if (_gpuLeases == 0)
			{
				lock (_gpuGate)
				{
					_gpuCounter?.Dispose();
					_gpuCounter = null;
				}
			}
		}
		if (_leases == 0)
		{
			_timer?.Stop();
		}
	}

	private void Sample()
	{
		if (_sampling)
		{
			return;
		}
		_sampling = true;
		bool wantGpu = _gpuLeases > 0;
		Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
		Task.Run(() => Read(wantGpu)).ContinueWith(delegate(Task<SensorSnapshot?> t)
		{
			dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
			{
				_sampling = false;
				if (t.IsFaulted)
				{
					Log.Error("Датчики: опрос", t.Exception?.GetBaseException());
					return;
				}
				SensorSnapshot? snapshot = t.Result;
				if (snapshot == null || _leases == 0)
				{
					return;
				}
				Current = snapshot;
				try
				{
					this.Updated?.Invoke(snapshot);
				}
				catch (Exception ex)
				{
					Log.Error("Датчики: подписчик", ex);
				}
			});
		}, TaskScheduler.Default);
	}

	// Пул потоков; _stats трогает только он (повторный запуск исключён флагом _sampling).
	private SensorSnapshot? Read(bool wantGpu)
	{
		double? gpu = null;
		if (wantGpu)
		{
			lock (_gpuGate)
			{
				_gpuCounter ??= new GpuCounter();
				gpu = (_gpuCounter.Available ? _gpuCounter.Read() : null);
				GpuAvailable = _gpuCounter.Available;
			}
		}
		(double UsedGb, double TotalGb, uint Load)? memory = SystemStats.Memory();
		(double Down, double Up)? network = _stats.Network();
		(int Percent, bool Charging)? battery = SystemStats.Battery();
		return new SensorSnapshot(_stats.Cpu(), gpu, memory?.UsedGb, memory?.TotalGb, memory?.Load, network?.Down, network?.Up, battery?.Percent, battery?.Charging ?? false, DateTime.UtcNow);
	}
}
