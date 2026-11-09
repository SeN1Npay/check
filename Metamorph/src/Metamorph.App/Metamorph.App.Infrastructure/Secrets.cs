using System;
using System.Security.Cryptography;
using System.Text;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

internal static class Secrets
{
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Metamorph.Secrets.v1");

	public static string? Protect(string? plain)
	{
		if (string.IsNullOrWhiteSpace(plain))
		{
			return null;
		}
		return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain.Trim()), Entropy, DataProtectionScope.CurrentUser));
	}

	public static string? Unprotect(string? protectedBase64)
	{
		if (string.IsNullOrWhiteSpace(protectedBase64))
		{
			return null;
		}
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}
		catch (Exception ex)
		{
			Log.Warn("Не удалось расшифровать секрет: " + ex.GetType().Name);
			return null;
		}
	}
}
