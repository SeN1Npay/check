using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Remote;

public sealed class BridgeRouter
{
	private readonly string _token;

	private readonly McpServer _mcp;

	private readonly Func<bool> _filesAllowed;

	public event Action<string, long, bool>? FileTransferred;

	public BridgeRouter(string token, McpServer mcp, Func<bool> filesAllowed)
	{
		_token = token;
		_mcp = mcp;
		_filesAllowed = filesAllowed;
	}

	public static string NewToken()
	{
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
	}

	public async Task<HttpResponse> HandleAsync(HttpRequest request, CancellationToken ct)
	{
		string[] array = request.Path.Trim('/').Split('/', 2);
		if (array.Length == 0 || !TokenEquals(array[0]))
		{
			return HttpResponse.Text("Not found", 404);
		}
		string text = ((array.Length > 1) ? array[1].Trim('/') : "");
		if (text == "mcp")
		{
			if (request.Method == "GET")
			{
				return HttpResponse.Text("SSE stream is not supported; use POST", 405);
			}
			if (request.Method == "DELETE")
			{
				return HttpResponse.Empty(200);
			}
			if (request.Method != "POST")
			{
				return HttpResponse.Text("Method not allowed", 405);
			}
			string text2 = await _mcp.HandleAsync(request.BodyText, ct);
			return (text2 == null) ? HttpResponse.Empty(202) : HttpResponse.Json(text2);
		}
		if (text.StartsWith("call/", StringComparison.Ordinal) && request.Method == "POST")
		{
			JsonObject args;
			try
			{
				args = ((request.Body.Length == 0) ? new JsonObject() : ((JsonNode.Parse(request.BodyText) as JsonObject) ?? new JsonObject()));
			}
			catch (Exception ex)
			{
				return HttpResponse.Text("Bad JSON: " + ex.Message, 400);
			}
			McpServer mcp = _mcp;
			string text3 = text;
			int length = "call/".Length;
			return HttpResponse.Json((await mcp.CallAsync(text3.Substring(length, text3.Length - length), args, ct)).ToJson().ToJsonString(McpServer.Json));
		}
		if (text == "health")
		{
			return HttpResponse.Json("{\"ok\":true}");
		}
		if (text == "files")
		{
			return Files(request);
		}
		return HttpResponse.Text("Not found", 404);
	}

	private HttpResponse Files(HttpRequest request)
	{
		if (!_filesAllowed())
		{
			return HttpResponse.Text("File access is disabled in the Remote help window", 403);
		}
		if (!request.Query.TryGetValue("path", out string value) || string.IsNullOrWhiteSpace(value))
		{
			return HttpResponse.Text("path is required", 400);
		}
		string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
		try
		{
			switch (request.Method)
			{
			case "GET":
			{
				if (!File.Exists(fullPath))
				{
					return HttpResponse.Text("Not found: " + fullPath, 404);
				}
				byte[] array = File.ReadAllBytes(fullPath);
				this.FileTransferred?.Invoke(fullPath, array.Length, arg3: false);
				return HttpResponse.Bytes(array);
			}
			case "PUT":
			case "POST":
			{
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
				string value2;
				bool flag = request.Query.TryGetValue("append", out value2);
				if (flag)
				{
					bool flag2 = ((value2 == "1" || value2 == "true") ? true : false);
					flag = flag2;
				}
				if (flag)
				{
					using (FileStream fileStream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read))
					{
						fileStream.Write(request.Body);
					}
					long length = new FileInfo(fullPath).Length;
					this.FileTransferred?.Invoke(fullPath, request.Body.Length, arg3: true);
					return HttpResponse.Json(new JsonObject
					{
						["ok"] = true,
						["path"] = fullPath,
						["bytes"] = request.Body.Length,
						["total"] = length
					}.ToJsonString(McpServer.Json));
				}
				string text = fullPath + ".part";
				File.WriteAllBytes(text, request.Body);
				File.Move(text, fullPath, overwrite: true);
				this.FileTransferred?.Invoke(fullPath, request.Body.Length, arg3: true);
				return HttpResponse.Json(new JsonObject
				{
					["ok"] = true,
					["path"] = fullPath,
					["bytes"] = request.Body.Length
				}.ToJsonString(McpServer.Json));
			}
			default:
				return HttpResponse.Text("Method not allowed", 405);
			}
		}
		catch (Exception ex)
		{
			return HttpResponse.Text(ex.GetType().Name + ": " + ex.Message, 500);
		}
	}

	private bool TokenEquals(string candidate)
	{
		return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(_token));
	}
}
