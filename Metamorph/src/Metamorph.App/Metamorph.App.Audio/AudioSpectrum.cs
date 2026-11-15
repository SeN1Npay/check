using System;
using System.Threading;
using System.Windows.Threading;
using Metamorph.Core.Audio;
using Metamorph.Core.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Metamorph.App.Audio;

internal sealed class AudioSpectrum
{
	private sealed class Lease : IDisposable
	{
		private AudioSpectrum? _owner;

		public Lease(AudioSpectrum owner)
		{
			_owner = owner;
		}

		public void Dispose()
		{
			_owner?.Release();
			_owner = null;
		}
	}

	public const int BandCount = 48;

	private readonly object _gate = new object();

	private readonly DispatcherTimer _watch = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(3.0)
	};

	private readonly float[] _cache = new float[48];

	private WasapiLoopbackCapture? _capture;

	private SpectrumAnalyzer _analyzer = new SpectrumAnalyzer();

	private string? _deviceId;

	private int _leases;

	private long _lastDataTicks;

	private long _cacheTicks;

	private bool _failedLogged;

	private float[] _mono = Array.Empty<float>();

	public static AudioSpectrum Shared { get; } = new AudioSpectrum();


	public bool IsActive => Environment.TickCount64 - Interlocked.Read(ref _lastDataTicks) < 150;

	private AudioSpectrum()
	{
		_watch.Tick += delegate
		{
			Watch();
		};
	}

	public IDisposable Acquire()
	{
		_leases++;
		if (_leases == 1)
		{
			Start();
			_watch.Start();
		}
		return new Lease(this);
	}

	private void Release()
	{
		if (_leases != 0)
		{
			_leases--;
			if (_leases <= 0)
			{
				_watch.Stop();
				Stop();
			}
		}
	}

	public bool Read(float[] target)
	{
		if (!IsActive)
		{
			Array.Clear(target);
			return false;
		}
		// Читают с UI-потока (панель, остров, Мета) и с потока обоев (эффекты) — кэш под замком.
		lock (_cache)
		{
			long tickCount = Environment.TickCount64;
			if (tickCount - _cacheTicks > 12)
			{
				_analyzer.Compute(_cache);
				_cacheTicks = tickCount;
			}
			Array.Copy(_cache, target, Math.Min(target.Length, _cache.Length));
		}
		return true;
	}

	private void Start()
	{
		lock (_gate)
		{
			if (_capture != null)
			{
				return;
			}
			try
			{
				using MMDeviceEnumerator mMDeviceEnumerator = new MMDeviceEnumerator();
				if (mMDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
				{
					MMDevice defaultAudioEndpoint = mMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
					_deviceId = defaultAudioEndpoint.ID;
					WasapiLoopbackCapture wasapiLoopbackCapture = new WasapiLoopbackCapture(defaultAudioEndpoint);
					WaveFormat waveFormat = wasapiLoopbackCapture.WaveFormat;
					if (_analyzer.SampleRate != waveFormat.SampleRate)
					{
						_analyzer = new SpectrumAnalyzer(48, 2048, waveFormat.SampleRate);
					}
					wasapiLoopbackCapture.DataAvailable += OnData;
					wasapiLoopbackCapture.RecordingStopped += OnStopped;
					wasapiLoopbackCapture.StartRecording();
					_capture = wasapiLoopbackCapture;
					_failedLogged = false;
				}
			}
			catch (Exception ex)
			{
				if (!_failedLogged)
				{
					Log.Error("Захват звука для визуализатора", ex);
				}
				_failedLogged = true;
				_capture = null;
			}
		}
	}

	private void Stop()
	{
		WasapiLoopbackCapture capture;
		lock (_gate)
		{
			capture = _capture;
			_capture = null;
		}
		if (capture != null)
		{
			try
			{
				capture.DataAvailable -= OnData;
				capture.RecordingStopped -= OnStopped;
				capture.StopRecording();
				capture.Dispose();
			}
			catch (Exception ex)
			{
				Log.Error("Остановка захвата звука", ex);
			}
			_analyzer.Clear();
		}
	}

	private void Watch()
	{
		try
		{
			if (_leases == 0)
			{
				return;
			}
			string text = null;
			using (MMDeviceEnumerator mMDeviceEnumerator = new MMDeviceEnumerator())
			{
				if (mMDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
				{
					text = mMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
				}
			}
			if (_capture == null || text != _deviceId)
			{
				Stop();
				Start();
			}
		}
		catch (Exception ex)
		{
			Log.Error("Проверка устройства звука", ex);
		}
	}

	private void OnStopped(object? sender, StoppedEventArgs e)
	{
		if (e.Exception != null)
		{
			Log.Error("Захват звука остановлен", e.Exception);
		}
		lock (_gate)
		{
			if (sender == _capture)
			{
				_capture = null;
			}
		}
	}

	private void OnData(object? sender, WaveInEventArgs e)
	{
		try
		{
			if (!(sender is WasapiCapture wasapiCapture) || e.BytesRecorded <= 0)
			{
				return;
			}
			WaveFormat waveFormat = wasapiCapture.WaveFormat;
			int num = Math.Max(1, waveFormat.Channels);
			int num2 = Math.Max(1, waveFormat.BitsPerSample / 8);
			int num3 = e.BytesRecorded / (num2 * num);
			if (num3 <= 0)
			{
				return;
			}
			if (_mono.Length < num3)
			{
				_mono = new float[num3];
			}
			bool flag = waveFormat.BitsPerSample == 32 && waveFormat.Encoding != WaveFormatEncoding.Pcm;
			byte[] buffer = e.Buffer;
			bool flag2 = false;
			for (int i = 0; i < num3; i++)
			{
				double num4 = 0.0;
				for (int j = 0; j < num; j++)
				{
					int num5 = (i * num + j) * num2;
					double num6 = num4;
					num4 = num6 + num2 switch
					{
						4 => (!flag) ? ((double)BitConverter.ToInt32(buffer, num5) / 2147483648.0) : ((double)BitConverter.ToSingle(buffer, num5)), 
						3 => (double)((buffer[num5] | (buffer[num5 + 1] << 8) | (buffer[num5 + 2] << 16)) << 8 >> 8) / 8388608.0, 
						2 => (double)BitConverter.ToInt16(buffer, num5) / 32768.0, 
						_ => 0.0, 
					};
				}
				float num7 = (float)(num4 / (double)num);
				_mono[i] = num7;
				if (num7 != 0f)
				{
					flag2 = true;
				}
			}
			_analyzer.Push(_mono.AsSpan(0, num3));
			if (flag2)
			{
				Interlocked.Exchange(ref _lastDataTicks, Environment.TickCount64);
			}
		}
		catch (Exception ex)
		{
			if (!_failedLogged)
			{
				Log.Error("Обработка звука", ex);
			}
			_failedLogged = true;
		}
	}
}
