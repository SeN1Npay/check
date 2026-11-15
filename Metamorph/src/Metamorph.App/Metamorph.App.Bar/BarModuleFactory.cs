using System;
using Metamorph.Core.Models;

namespace Metamorph.App.Bar;

public static class BarModuleFactory
{
	public static BarModule? Create(string name, BarContext context)
	{
		return name.Trim().ToLowerInvariant() switch
		{
			"launcher" => new LauncherModule(context), 
			"tray" => new TrayModule(context), 
			"window" => new WindowTitleModule(context), 
			"clock" => new ClockModule(context), 
			"cpu" => new CpuModule(context), 
			"ram" => new RamModule(context), 
			"battery" => new BatteryModule(context), 
			"layout" => new LayoutModule(context), 
			"uptime" => new UptimeModule(context), 
			"workspaces" => new WorkspacesModule(context), 
			"media" => new MediaModule(context), 
			"volume" => new VolumeModule(context), 
			"network" => new NetworkModule(context), 
			"bluetooth" => new BluetoothModule(context), 
			"power" => new PowerModule(context), 
			"weather" => new WeatherModule(context), 
			"disk" => new DiskModule(context), 
			"cava" => new CavaModule(context), 
			"gpu" => new GpuModule(context), 
			"mic" => new MicModule(context), 
			"timer" => new TimerModule(context), 
			"screenshot" => new ScreenshotModule(context), 
			"wallpaper" => new WallpaperModule(context), 
			_ => CreateScript(name, context), 
		};
	}

	public static BarModule? CreateFor(string name, BarContext context)
	{
		if (!context.Primary && name.Trim().Equals("tray", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return Create(name, context);
	}

	private static BarModule? CreateScript(string name, BarContext context)
	{
		if (!BarModules.IsScript(name, out string scriptName))
		{
			return LogUnknown(name);
		}
		ScriptModuleSettings scriptModuleSettings = context.Profile.Bar.Scripts.Find((ScriptModuleSettings s) => string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase));
		if (scriptModuleSettings != null)
		{
			return new ScriptModule(context, scriptModuleSettings);
		}
		return LogUnknown(name);
	}

	private static BarModule? LogUnknown(string name)
	{
		return null;
	}
}
