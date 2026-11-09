using System.Collections.Generic;

namespace Metamorph.Core.Assistant;

public sealed record LlmReply(string? Content, IReadOnlyList<LlmToolCall> ToolCalls);
