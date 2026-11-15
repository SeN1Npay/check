namespace Metamorph.Core.Assistant;

public sealed class AssistantSettings
{
	public bool SpeakReplies { get; set; }

	public string Voice { get; set; } = "";


	public int VoicePitch { get; set; } = 25;


	public double VoiceRate { get; set; } = 1.05;


	public bool VoiceInput { get; set; }

	public LlmSettings Llm { get; set; } = new LlmSettings();


	public TelegramSettings Telegram { get; set; } = new TelegramSettings();

}
