namespace Metamorph.Core.Dock;

public sealed class TrackedWindow
{
	public nint Handle { get; }

	public uint ProcessId { get; internal set; }

	public string ExePath { get; internal set; } = "";


	public string? AppId { get; internal set; }

	public string Title { get; internal set; } = "";


	public bool IsMinimized { get; internal set; }

	public bool IsFlashing { get; internal set; }

	public long FirstSeen { get; internal set; }

	public long LastActive { get; internal set; }

	public bool IsUwp => DockApps.IsUwpHost(ExePath);

	public string AppKey => DockApps.KeyFor(AppId, ExePath);

	internal TrackedWindow(nint handle)
	{
		Handle = handle;
	}
}
