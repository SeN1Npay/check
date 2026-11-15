using Metamorph.Core.Mascot;

namespace Metamorph.Core.Assistant;

public sealed record AssistantReply(string Text, IntentKind Intent, MascotAction? Mood = null, bool FromLlm = false);
