namespace Metamorph.Core.Assistant;

public sealed record TelegramMessage(long UpdateId, long ChatId, long FromId, string FromName, string ChatType, string Text);
