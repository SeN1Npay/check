using System.Text.Json.Nodes;

namespace Metamorph.Core.Assistant;

public sealed record LlmTool(string Name, string Description, JsonObject Parameters);
