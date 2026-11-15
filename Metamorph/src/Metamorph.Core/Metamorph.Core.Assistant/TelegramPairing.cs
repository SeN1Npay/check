using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace Metamorph.Core.Assistant;

public static class TelegramPairing
{
	public static string NewCode()
	{
		return RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
	}

	public static TelegramAccess Check(TelegramSettings s, TelegramMessage m, string? pairingCode)
	{
		if (m.ChatType != "private")
		{
			return TelegramAccess.Stranger;
		}
		long? ownerId = s.OwnerId;
		if (ownerId.HasValue)
		{
			long valueOrDefault = ownerId.GetValueOrDefault();
			if (valueOrDefault != m.FromId)
			{
				return TelegramAccess.Stranger;
			}
			return TelegramAccess.Owner;
		}
		if (string.IsNullOrEmpty(pairingCode))
		{
			return TelegramAccess.Stranger;
		}
		string text = new string(m.Text.Where(char.IsDigit).ToArray());
		if (!text.Contains(pairingCode, StringComparison.Ordinal) || text.Length > 12)
		{
			return TelegramAccess.Stranger;
		}
		return TelegramAccess.Pair;
	}
}
