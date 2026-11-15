using System;

namespace Metamorph.Core.Services;

public sealed class UpdateSettings
{
	public string Repository { get; set; } = "";


	public bool CheckOnStartup { get; set; } = true;


	public DateTime? LastCheckUtc { get; set; }

	public string? SkippedVersion { get; set; }
}
