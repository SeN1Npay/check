using System.Collections.Generic;

namespace Metamorph.Core.Assistant;

public sealed record LlmMessage(string Role, string? Content, IReadOnlyList<LlmToolCall>? ToolCalls = null, string? ToolCallId = null);
