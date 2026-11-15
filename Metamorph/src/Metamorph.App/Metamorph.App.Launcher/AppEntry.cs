using System.Diagnostics;

namespace Metamorph.App.Launcher;

internal sealed record AppEntry(string Name, string ParsingName, bool FromAppsFolder)
{
	public string IconPath
	{
		get
		{
			if (!FromAppsFolder)
			{
				return ParsingName;
			}
			return "shell:AppsFolder\\" + ParsingName;
		}
	}

	public void Launch()
	{
		string text = (FromAppsFolder ? ("shell:AppsFolder\\" + ParsingName) : ParsingName);
		if (FromAppsFolder)
		{
			Process.Start(new ProcessStartInfo("explorer.exe", text)
			{
				UseShellExecute = true
			});
		}
		else
		{
			Process.Start(new ProcessStartInfo(text)
			{
				UseShellExecute = true
			});
		}
	}
}
