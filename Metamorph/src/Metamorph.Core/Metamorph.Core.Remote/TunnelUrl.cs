using System.CodeDom.Compiler;
using System.Text.RegularExpressions;

namespace Metamorph.Core.Remote;

public static class TunnelUrl
{
	private static readonly Regex _CloudflareRegex = new Regex("https://(?!api\\.)[a-z0-9-]+\\.trycloudflare\\.com", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static Regex Cloudflare() => _CloudflareRegex;

	private static readonly Regex _LocalhostRunRegex = new Regex("https://[a-z0-9-]+\\.(?:lhr\\.life|lhr\\.rocks)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static Regex LocalhostRun() => _LocalhostRunRegex;

	public static string? Find(string? line)
	{
		if (string.IsNullOrEmpty(line))
		{
			return null;
		}
		Match match = Cloudflare().Match(line);
		if (match.Success)
		{
			return match.Value.ToLowerInvariant();
		}
		match = LocalhostRun().Match(line);
		if (!match.Success)
		{
			return null;
		}
		return match.Value.ToLowerInvariant();
	}
}
