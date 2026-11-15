using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Remote;

public sealed record McpTool(string Name, string Description, JsonObject InputSchema, Func<JsonObject, CancellationToken, Task<McpResult>> Run)
{
	public static JsonObject Schema(params (string Name, string Type, string Description)[] props)
	{
		JsonObject jsonObject = new JsonObject();
		JsonArray jsonArray = new JsonArray();
		for (int i = 0; i < props.Length; i++)
		{
			(string Name, string Type, string Description) tuple = props[i];
			string item = tuple.Name;
			string item2 = tuple.Type;
			string item3 = tuple.Description;
			string text = item.TrimEnd('!');
			if (item.EndsWith('!'))
			{
				jsonArray.Add(text);
			}
			jsonObject[text] = new JsonObject
			{
				["type"] = item2,
				["description"] = item3
			};
		}
		JsonObject jsonObject2 = new JsonObject
		{
			["type"] = "object",
			["properties"] = jsonObject
		};
		if (jsonArray.Count > 0)
		{
			jsonObject2["required"] = jsonArray;
		}
		return jsonObject2;
	}
}
