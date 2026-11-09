using System;
using System.IO;

namespace Metamorph.App.Infrastructure;

public sealed class CommandLineOptions
{
	public string? ApplyId { get; private set; }

	public bool Restore { get; private set; }

	public bool Minimized { get; private set; }

	public bool Exit { get; private set; }

	public bool Launcher { get; private set; }

	public bool Island { get; private set; }

	public string? Wallpaper { get; private set; }

	public bool Assistant { get; private set; }

	public bool Safe { get; private set; }

	public string? ImportFile { get; private set; }

	public bool Install { get; private set; }

	public bool Uninstall { get; private set; }

	public bool Quiet { get; private set; }

	public bool Purge { get; private set; }

	public bool KeepFiles { get; private set; }

	public bool RemoteHelp { get; private set; }

	public static CommandLineOptions Parse(string[] args)
	{
		CommandLineOptions commandLineOptions = new CommandLineOptions();
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i].ToLowerInvariant())
			{
			case "--apply":
				if (i + 1 < args.Length)
				{
					commandLineOptions.ApplyId = args[++i];
					continue;
				}
				break;
			case "--restore":
				commandLineOptions.Restore = true;
				continue;
			case "--minimized":
			case "--tray":
				commandLineOptions.Minimized = true;
				continue;
			case "--exit":
				commandLineOptions.Exit = true;
				continue;
			case "--launcher":
				commandLineOptions.Launcher = true;
				continue;
			case "--island":
				commandLineOptions.Island = true;
				commandLineOptions.Minimized = true;
				continue;
			case "--wallpaper":
				if (i + 1 < args.Length)
				{
					commandLineOptions.Wallpaper = args[++i];
					commandLineOptions.Minimized = true;
					continue;
				}
				break;
			case "--assistant":
				commandLineOptions.Assistant = true;
				commandLineOptions.Minimized = true;
				continue;
			case "--safe":
				commandLineOptions.Safe = true;
				continue;
			case "--import":
				if (i + 1 < args.Length)
				{
					commandLineOptions.ImportFile = Path.GetFullPath(args[++i]);
					continue;
				}
				break;
			case "--install":
				commandLineOptions.Install = true;
				continue;
			case "--uninstall":
				commandLineOptions.Uninstall = true;
				continue;
			case "--quiet":
			case "/quiet":
			case "/s":
				commandLineOptions.Quiet = true;
				continue;
			case "--purge":
				commandLineOptions.Purge = true;
				continue;
			case "--keep-files":
				commandLineOptions.KeepFiles = true;
				continue;
			case "--bridge":
			case "--remote-help":
				commandLineOptions.RemoteHelp = true;
				continue;
			}
			if (!args[i].StartsWith("-", StringComparison.Ordinal) && File.Exists(args[i]))
			{
				commandLineOptions.ImportFile = Path.GetFullPath(args[i]);
			}
		}
		return commandLineOptions;
	}

	public string ToIpcCommand()
	{
		if (Exit)
		{
			return "exit";
		}
		if (Restore)
		{
			return "restore";
		}
		if (ImportFile != null)
		{
			return "import:" + ImportFile;
		}
		if (ApplyId != null)
		{
			return "apply:" + ApplyId;
		}
		if (Launcher)
		{
			return "launcher";
		}
		if (Island)
		{
			return "island";
		}
		if (Wallpaper != null)
		{
			return "wallpaper:" + ((File.Exists(Wallpaper) || Directory.Exists(Wallpaper)) ? Path.GetFullPath(Wallpaper) : Wallpaper);
		}
		if (Assistant)
		{
			return "assistant";
		}
		if (!Minimized)
		{
			return "show";
		}
		return "noop";
	}
}
