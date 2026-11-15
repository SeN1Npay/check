namespace Metamorph.Core.Services;

public static class AutostartService
{
	private const string ValueName = "Metamorph";

	public static bool IsEnabled()
	{
		return Reg.Get("Software\\Microsoft\\Windows\\CurrentVersion\\Run", "Metamorph") is string;
	}

	public static void SetEnabled(bool enabled, string exePath)
	{
		if (enabled)
		{
			Reg.SetString("Software\\Microsoft\\Windows\\CurrentVersion\\Run", "Metamorph", "\"" + exePath + "\" --minimized");
		}
		else
		{
			Reg.Delete("Software\\Microsoft\\Windows\\CurrentVersion\\Run", "Metamorph");
		}
	}
}
