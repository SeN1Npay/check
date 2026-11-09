using System;
using System.Collections.Generic;
using System.Text;

namespace Metamorph.Core.Remote;

public sealed class HttpResponse
{
	public int Status { get; init; } = 200;


	public string ContentType { get; init; } = "application/json; charset=utf-8";


	public byte[] Body { get; init; } = Array.Empty<byte>();


	public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


	public static HttpResponse Json(string json, int status = 200)
	{
		return new HttpResponse
		{
			Status = status,
			Body = Encoding.UTF8.GetBytes(json)
		};
	}

	public static HttpResponse Text(string text, int status = 200)
	{
		return new HttpResponse
		{
			Status = status,
			ContentType = "text/plain; charset=utf-8",
			Body = Encoding.UTF8.GetBytes(text)
		};
	}

	public static HttpResponse Bytes(byte[] data, string contentType = "application/octet-stream")
	{
		return new HttpResponse
		{
			Body = data,
			ContentType = contentType
		};
	}

	public static HttpResponse Empty(int status)
	{
		return new HttpResponse
		{
			Status = status,
			ContentType = "text/plain; charset=utf-8"
		};
	}
}
