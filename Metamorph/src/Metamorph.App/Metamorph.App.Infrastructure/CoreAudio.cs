using System;
using System.Runtime.InteropServices;

namespace Metamorph.App.Infrastructure;

internal sealed class CoreAudio
{
	[ComImport]
	[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	private class MMDeviceEnumeratorCoClass
	{
	}

	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceEnumerator
	{
		[PreserveSig]
		int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
	}

	[ComImport]
	[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioEndpointVolume
	{
		void RegisterControlChangeNotify(nint notify);

		void UnregisterControlChangeNotify(nint notify);

		void GetChannelCount(out uint count);

		void SetMasterVolumeLevel(float levelDb, ref Guid context);

		void SetMasterVolumeLevelScalar(float level, ref Guid context);

		void GetMasterVolumeLevel(out float levelDb);

		void GetMasterVolumeLevelScalar(out float level);

		void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);

		void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);

		void GetChannelVolumeLevel(uint channel, out float levelDb);

		void GetChannelVolumeLevelScalar(uint channel, out float level);

		void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

		void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
	}

	private IAudioEndpointVolume? _volume;

	private DateTime _acquired;

	private readonly bool _capture;

	public CoreAudio(bool capture = false)
	{
		_capture = capture;
	}

	private IAudioEndpointVolume? Endpoint()
	{
		if (_volume != null && DateTime.UtcNow - _acquired < TimeSpan.FromSeconds(15.0))
		{
			return _volume;
		}
		try
		{
			if (_volume != null)
			{
				Marshal.ReleaseComObject(_volume);
			}
			_volume = null;
			IMMDeviceEnumerator iMMDeviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
			try
			{
				if (iMMDeviceEnumerator.GetDefaultAudioEndpoint(_capture ? 1 : 0, (!_capture) ? 1 : 0, out IMMDevice endpoint) != 0 || endpoint == null)
				{
					return null;
				}
				try
				{
					Guid iid = typeof(IAudioEndpointVolume).GUID;
					if (endpoint.Activate(ref iid, 23, IntPtr.Zero, out object instance) != 0)
					{
						return null;
					}
					_volume = (IAudioEndpointVolume)instance;
					_acquired = DateTime.UtcNow;
				}
				finally
				{
					Marshal.ReleaseComObject(endpoint);
				}
			}
			finally
			{
				Marshal.ReleaseComObject(iMMDeviceEnumerator);
			}
		}
		catch (COMException)
		{
			_volume = null;
		}
		return _volume;
	}

	public (float Level, bool Muted)? Read()
	{
		IAudioEndpointVolume audioEndpointVolume = Endpoint();
		if (audioEndpointVolume == null)
		{
			return null;
		}
		try
		{
			audioEndpointVolume.GetMasterVolumeLevelScalar(out var level);
			audioEndpointVolume.GetMute(out var mute);
			return (level, mute);
		}
		catch (COMException)
		{
			_volume = null;
			return null;
		}
	}

	public void SetLevel(float level)
	{
		Guid context = Guid.Empty;
		Endpoint()?.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
	}

	public void ToggleMute()
	{
		IAudioEndpointVolume audioEndpointVolume = Endpoint();
		if (audioEndpointVolume != null)
		{
			audioEndpointVolume.GetMute(out var mute);
			Guid context = Guid.Empty;
			audioEndpointVolume.SetMute(!mute, ref context);
		}
	}
}
