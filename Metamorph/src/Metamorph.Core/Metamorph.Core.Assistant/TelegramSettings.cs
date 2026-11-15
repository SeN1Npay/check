namespace Metamorph.Core.Assistant;

public sealed class TelegramSettings
{
	public bool Enabled { get; set; }

	public string? TokenProtected { get; set; }

	public long? OwnerId { get; set; }

	public string? OwnerName { get; set; }

	public bool AllowCommands { get; set; } = true;

}
