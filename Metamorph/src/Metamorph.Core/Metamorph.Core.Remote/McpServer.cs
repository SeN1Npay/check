using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Remote;

public sealed class McpServer
{
	public static readonly string[] SupportedVersions = new string[3] { "2025-06-18", "2025-03-26", "2024-11-05" };

	public static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private readonly Dictionary<string, McpTool> _tools = new Dictionary<string, McpTool>(StringComparer.Ordinal);

	private readonly string _name;

	private readonly string _version;

	private readonly string _instructions;

	public IReadOnlyCollection<McpTool> Tools => _tools.Values;

	public event Action<string, JsonObject, McpResult, TimeSpan>? Called;

	public McpServer(string name, string version, string instructions)
	{
		_name = name;
		_version = version;
		_instructions = instructions;
	}

	public void Add(McpTool tool)
	{
		_tools[tool.Name] = tool;
	}

	public async Task<McpResult> CallAsync(string name, JsonObject args, CancellationToken ct)
	{
		if (!_tools.TryGetValue(name, out McpTool value))
		{
			return McpResult.Error("Unknown tool: " + name);
		}
		Stopwatch sw = Stopwatch.StartNew();
		McpResult mcpResult;
		try
		{
			mcpResult = await value.Run(args, ct);
		}
		catch (OperationCanceledException)
		{
			mcpResult = McpResult.Error("Cancelled");
		}
		catch (Exception ex2)
		{
			mcpResult = McpResult.Error(ex2.GetType().Name + ": " + ex2.Message);
		}
		sw.Stop();
		try
		{
			this.Called?.Invoke(name, args, mcpResult, sw.Elapsed);
		}
		catch
		{
		}
		return mcpResult;
	}

	public async Task<string?> HandleAsync(string body, CancellationToken ct)
	{
		JsonNode jsonNode;
		try
		{
			jsonNode = JsonNode.Parse(body);
		}
		catch (JsonException)
		{
			return Error(null, -32700, "Parse error").ToJsonString(Json);
		}
		if (jsonNode is JsonArray jsonArray)
		{
			if (jsonArray.Count == 0)
			{
				return Error(null, -32600, "Empty batch").ToJsonString(Json);
			}
			JsonArray responses = new JsonArray();
			foreach (JsonNode item in jsonArray)
			{
				JsonObject jsonObject = await HandleOneAsync(item, ct);
				if (jsonObject != null)
				{
					responses.Add(jsonObject);
				}
			}
			return (responses.Count == 0) ? null : responses.ToJsonString(Json);
		}
		return (await HandleOneAsync(jsonNode, ct))?.ToJsonString(Json);
	}

	private async Task<JsonObject?> HandleOneAsync(JsonNode? node, CancellationToken ct)
	{
		if (node is JsonObject jsonObject)
		{
			JsonNode? jsonNode = jsonObject["method"];
			if (jsonNode != null && jsonNode.GetValueKind() == JsonValueKind.String)
			{
				JsonNode id = jsonObject["id"]?.DeepClone();
				string value = jsonObject["method"].GetValue<string>();
				JsonObject jsonObject2 = (jsonObject["params"] as JsonObject) ?? new JsonObject();
				bool isNotification = jsonObject["id"] == null;
				try
				{
					JsonNode value2;
					switch (value)
					{
					case "initialize":
					{
						string text2 = jsonObject2["protocolVersion"]?.GetValue<string>();
						value2 = new JsonObject
						{
							["protocolVersion"] = ((text2 != null && SupportedVersions.Contains(text2)) ? text2 : SupportedVersions[0]),
							["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
							["serverInfo"] = new JsonObject
							{
								["name"] = _name,
								["version"] = _version
							},
							["instructions"] = _instructions
						};
						break;
					}
					case "ping":
						value2 = new JsonObject();
						break;
					case "tools/list":
						value2 = new JsonObject { ["tools"] = new JsonArray(((IEnumerable<McpTool>)_tools.Values.OrderBy<McpTool, string>((McpTool t) => t.Name, StringComparer.Ordinal)).Select((Func<McpTool, JsonNode>)((McpTool t) => new JsonObject
						{
							["name"] = t.Name,
							["description"] = t.Description,
							["inputSchema"] = t.InputSchema.DeepClone()
						})).ToArray()) };
						break;
					case "tools/call":
					{
						string text = jsonObject2["name"]?.GetValue<string>();
						if (text == null || !_tools.ContainsKey(text))
						{
							return isNotification ? null : Error(id, -32602, "Unknown tool: " + text);
						}
						JsonObject args = (jsonObject2["arguments"] as JsonObject) ?? new JsonObject();
						value2 = (await CallAsync(text, args, ct)).ToJson();
						break;
					}
					default:
						if (value.StartsWith("notifications/", StringComparison.Ordinal))
						{
							return null;
						}
						return isNotification ? null : Error(id, -32601, "Method not found: " + value);
					}
					if (isNotification)
					{
						return null;
					}
					return new JsonObject
					{
						["jsonrpc"] = "2.0",
						["id"] = id,
						["result"] = value2
					};
				}
				catch (Exception ex)
				{
					return isNotification ? null : Error(id, -32603, ex.Message);
				}
			}
		}
		return Error(null, -32600, "Invalid request");
	}

	private static JsonObject Error(JsonNode? id, int code, string message)
	{
		return new JsonObject
		{
			["jsonrpc"] = "2.0",
			["id"] = id,
			["error"] = new JsonObject
			{
				["code"] = code,
				["message"] = message
			}
		};
	}
}
