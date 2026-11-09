namespace Metamorph.Core.Assistant;

public sealed class LlmSettings
{
	public bool Enabled { get; set; }

	public string Endpoint { get; set; } = "http://localhost:11434/v1";


	public string Model { get; set; } = "qwen2.5:7b";


	public string? ApiKeyProtected { get; set; }

	public string Persona { get; set; } = "";


	public double Temperature { get; set; } = 0.7;


	public int MaxTokens { get; set; } = 400;

}
