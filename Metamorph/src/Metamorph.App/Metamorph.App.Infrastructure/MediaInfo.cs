using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Metamorph.App.Infrastructure;

internal static class MediaInfo
{
	private static GlobalSystemMediaTransportControlsSessionManager? _manager;

	private static bool _unavailable;

	private static async Task<GlobalSystemMediaTransportControlsSession?> SessionAsync()
	{
		if (_unavailable)
		{
			return null;
		}
		try
		{
			if ((object)_manager == null)
			{
				_manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			}
			return _manager.GetCurrentSession();
		}
		catch (Exception ex)
		{
			_unavailable = true;
			Log.Error("Медиасессии недоступны", ex);
			return null;
		}
	}

	public static async Task<MediaNow?> GetAsync()
	{
		GlobalSystemMediaTransportControlsSession session = await SessionAsync();
		if ((object)session == null)
		{
			return null;
		}
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties globalSystemMediaTransportControlsSessionMediaProperties = await session.TryGetMediaPropertiesAsync();
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = session.GetPlaybackInfo();
			bool playing = (object)playbackInfo != null && playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
			string text = globalSystemMediaTransportControlsSessionMediaProperties?.Title ?? "";
			if (text.Length == 0)
			{
				return null;
			}
			return new MediaNow(text, globalSystemMediaTransportControlsSessionMediaProperties?.Artist ?? "", playing, session.SourceAppUserModelId ?? "");
		}
		catch (Exception ex)
		{
			Log.Error("Медиасессия", ex);
			return null;
		}
	}

	public static async Task<MediaDetails?> GetDetailsAsync()
	{
		GlobalSystemMediaTransportControlsSession session = await SessionAsync();
		if ((object)session == null)
		{
			return null;
		}
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties globalSystemMediaTransportControlsSessionMediaProperties = await session.TryGetMediaPropertiesAsync();
			string text = globalSystemMediaTransportControlsSessionMediaProperties?.Title ?? "";
			if (text.Length == 0)
			{
				return null;
			}
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = session.GetPlaybackInfo();
			bool flag = (object)playbackInfo != null && playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
			TimeSpan timeSpan = TimeSpan.Zero;
			TimeSpan timeSpan2 = TimeSpan.Zero;
			try
			{
				GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = session.GetTimelineProperties();
				timeSpan2 = timelineProperties.EndTime - timelineProperties.StartTime;
				timeSpan = timelineProperties.Position - timelineProperties.StartTime;
				if (flag && timelineProperties.LastUpdatedTime.Year > 2000)
				{
					TimeSpan timeSpan3 = DateTimeOffset.Now - timelineProperties.LastUpdatedTime;
					if (timeSpan3 > TimeSpan.Zero && timeSpan3 < TimeSpan.FromHours(3.0))
					{
						timeSpan += timeSpan3;
					}
				}
				if (timeSpan2 > TimeSpan.Zero && timeSpan > timeSpan2)
				{
					timeSpan = timeSpan2;
				}
				if (timeSpan < TimeSpan.Zero)
				{
					timeSpan = TimeSpan.Zero;
				}
			}
			catch
			{
			}
			return new MediaDetails(text, globalSystemMediaTransportControlsSessionMediaProperties?.Artist ?? "", globalSystemMediaTransportControlsSessionMediaProperties?.AlbumTitle ?? "", flag, session.SourceAppUserModelId ?? "", timeSpan, timeSpan2);
		}
		catch (Exception ex)
		{
			Log.Error("Медиасессия", ex);
			return null;
		}
	}

	public static async Task<byte[]?> GetThumbnailAsync()
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await SessionAsync();
		if ((object)globalSystemMediaTransportControlsSession == null)
		{
			return null;
		}
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties globalSystemMediaTransportControlsSessionMediaProperties = await globalSystemMediaTransportControlsSession.TryGetMediaPropertiesAsync();
			if (globalSystemMediaTransportControlsSessionMediaProperties?.Thumbnail == null)
			{
				return null;
			}
			using IRandomAccessStreamWithContentType stream = await globalSystemMediaTransportControlsSessionMediaProperties.Thumbnail.OpenReadAsync();
			ulong size = stream.Size;
			if (size == 0L || size > 16777216)
			{
				return null;
			}
			using DataReader reader = new DataReader(stream.GetInputStreamAt(0uL));
			await reader.LoadAsync((uint)size);
			byte[] array = new byte[size];
			reader.ReadBytes(array);
			return array;
		}
		catch (Exception ex)
		{
			Log.Error("Обложка трека", ex);
			return null;
		}
	}

	public static async Task PlayPauseAsync()
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await SessionAsync();
		if ((object)globalSystemMediaTransportControlsSession != null)
		{
			await globalSystemMediaTransportControlsSession.TryTogglePlayPauseAsync();
		}
	}

	public static async Task NextAsync()
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await SessionAsync();
		if ((object)globalSystemMediaTransportControlsSession != null)
		{
			await globalSystemMediaTransportControlsSession.TrySkipNextAsync();
		}
	}

	public static async Task PreviousAsync()
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await SessionAsync();
		if ((object)globalSystemMediaTransportControlsSession != null)
		{
			await globalSystemMediaTransportControlsSession.TrySkipPreviousAsync();
		}
	}

	public static async Task SeekAsync(TimeSpan position)
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await SessionAsync();
		if ((object)globalSystemMediaTransportControlsSession != null)
		{
			await globalSystemMediaTransportControlsSession.TryChangePlaybackPositionAsync(Math.Max(0L, position.Ticks));
		}
	}

	public static void OpenPlayer(string aumid)
	{
		if (string.IsNullOrWhiteSpace(aumid))
		{
			return;
		}
		try
		{
			if (aumid.Contains('!'))
			{
				Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + aumid)
				{
					UseShellExecute = true
				});
				return;
			}
			Process[] processesByName = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(aumid));
			foreach (Process process in processesByName)
			{
				using (process)
				{
					nint mainWindowHandle = process.MainWindowHandle;
					if (mainWindowHandle == IntPtr.Zero)
					{
						continue;
					}
					if (Win32.IsIconic(mainWindowHandle))
					{
						Win32.ShowWindow(mainWindowHandle, 9);
					}
					Win32.SetForegroundWindow(mainWindowHandle);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("Открытие плеера " + aumid, ex);
		}
	}

	public static string Format(MediaNow m, int max = 48)
	{
		string text = ((m.Artist.Length > 0) ? (m.Artist + " — " + m.Title) : m.Title);
		if (text.Length <= max)
		{
			return text;
		}
		return text.Substring(0, max - 1) + "…";
	}
}
