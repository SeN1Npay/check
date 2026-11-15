using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Metamorph.Core.Remote;

public sealed class McpResult
{
	public List<JsonObject> Content { get; } = new List<JsonObject>();


	public bool IsError { get; init; }

	public static McpResult Text(string text)
	{
		return new McpResult().AddText(text);
	}

	public static McpResult Error(string text)
	{
		return new McpResult
		{
			IsError = true
		}.AddText(text);
	}

	public McpResult AddText(string text)
	{
		Content.Add(new JsonObject
		{
			["type"] = "text",
			["text"] = text
		});
		return this;
	}

	public McpResult AddImage(byte[] data, string mimeType)
	{
		Content.Add(new JsonObject
		{
			["type"] = "image",
			["data"] = Convert.ToBase64String(data),
			["mimeType"] = mimeType
		});
		return this;
	}

	public string Summary()
	{
		return string.Join("\n", Content.Select((JsonObject c) => (c["type"]?.GetValue<string>() == "text") ? (c["text"]?.GetValue<string>() ?? "") : "[image]"));
	}

	public JsonObject ToJson()
	{
		return new JsonObject
		{
			["content"] = new JsonArray(Content.Select((JsonObject c) => c.DeepClone()).ToArray()),
			["isError"] = IsError
		};
	}
}
