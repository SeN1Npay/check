using System;
using System.Collections.Generic;
using System.Text;

namespace Metamorph.Core.Remote;

public sealed class HttpRequest
{
	public string Method { get; init; } = "GET";


	public string Path { get; init; } = "/";


	public Dictionary<string, string> Query { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


	public Dictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


	public byte[] Body { get; init; } = Array.Empty<byte>();


	public string BodyText => Encoding.UTF8.GetString(Body);

	public string? Header(string name)
	{
		if (!Headers.TryGetValue(name, out string value))
		{
			return null;
		}
		return value;
	}
}
