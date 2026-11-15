using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Assistant;

public interface ILlmClient
{
	Task<LlmReply> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool>? tools, CancellationToken ct);
}
