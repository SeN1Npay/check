using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Assistant;

public sealed class OpenAiCompatibleClient : ILlmClient
{
	private readonly HttpClient _http;

	private readonly LlmSettings _settings;

	private readonly string? _apiKey;

	public OpenAiCompatibleClient(LlmSettings settings, string? apiKey, HttpClient? http = null)
	{
		_settings = settings;
		_apiKey = (string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim());
		_http = http ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(90.0)
		};
	}

	public static string CompletionsUrl(string endpoint)
	{
		string text = (endpoint ?? "").Trim().TrimEnd('/');
		if (text.Length == 0)
		{
			text = "http://localhost:11434/v1";
		}
		if (text.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return text + "/chat/completions";
	}

	public static JsonObject BuildRequest(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool>? tools, LlmSettings s)
	{
		JsonArray jsonArray = new JsonArray();
		foreach (LlmMessage message in messages)
		{
			JsonObject jsonObject = new JsonObject
			{
				["role"] = message.Role,
				["content"] = message.Content ?? ""
			};
			IReadOnlyList<LlmToolCall> toolCalls = message.ToolCalls;
			if (toolCalls != null && toolCalls.Count > 0)
			{
				JsonArray jsonArray2 = new JsonArray();
				foreach (LlmToolCall item in toolCalls)
				{
					jsonArray2.Add(new JsonObject
					{
						["id"] = item.Id,
						["type"] = "function",
						["function"] = new JsonObject
						{
							["name"] = item.Name,
							["arguments"] = item.Arguments
						}
					});
				}
				jsonObject["tool_calls"] = jsonArray2;
			}
			if (message.ToolCallId != null)
			{
				jsonObject["tool_call_id"] = message.ToolCallId;
			}
			jsonArray.Add(jsonObject);
		}
		JsonObject jsonObject2 = new JsonObject
		{
			["model"] = (string.IsNullOrWhiteSpace(s.Model) ? "qwen2.5:7b" : s.Model.Trim()),
			["messages"] = jsonArray,
			["temperature"] = Math.Clamp(s.Temperature, 0.0, 2.0),
			["max_tokens"] = Math.Clamp(s.MaxTokens, 32, 4000),
			["stream"] = false
		};
		if (tools != null && tools.Count > 0)
		{
			JsonArray jsonArray3 = new JsonArray();
			foreach (LlmTool tool in tools)
			{
				jsonArray3.Add(new JsonObject
				{
					["type"] = "function",
					["function"] = new JsonObject
					{
						["name"] = tool.Name,
						["description"] = tool.Description,
						["parameters"] = tool.Parameters.DeepClone()
					}
				});
			}
			jsonObject2["tools"] = jsonArray3;
		}
		return jsonObject2;
	}

	public static LlmReply ParseResponse(string json)
	{
		JsonNode? jsonNode = JsonNode.Parse(json);
		JsonNode jsonNode2 = jsonNode?["error"];
		if (jsonNode2 != null)
		{
			throw new InvalidOperationException(jsonNode2["message"]?.GetValue<string>() ?? jsonNode2.ToJsonString());
		}
		JsonNode? obj = jsonNode?["choices"]?[0]?["message"] ?? throw new InvalidOperationException("Пустой ответ модели");
		string value;
		string text = ((obj["content"] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out value)) ? value : null);
		List<LlmToolCall> list = new List<LlmToolCall>();
		if (obj["tool_calls"] is JsonArray jsonArray)
		{
			int num = 0;
			foreach (JsonNode item in jsonArray)
			{
				JsonNode jsonNode3 = item?["function"];
				string text2 = jsonNode3?["name"]?.GetValue<string>();
				if (string.IsNullOrEmpty(text2))
				{
					continue;
				}
				JsonNode jsonNode4 = jsonNode3?["arguments"];
				string text3;
				if (!(jsonNode4 is JsonValue jsonValue2))
				{
					if (jsonNode4 != null)
					{
						goto IL_016d;
					}
					text3 = "{}";
				}
				else
				{
					if (!jsonValue2.TryGetValue<string>(out string value2))
					{
						goto IL_016d;
					}
					text3 = value2;
				}
				goto IL_0180;
				IL_016d:
				text3 = jsonNode4.ToJsonString();
				goto IL_0180;
				IL_0180:
				string arguments = text3;
				list.Add(new LlmToolCall(item?["id"]?.GetValue<string>() ?? ("call_" + num), text2, arguments));
				num++;
			}
		}
		return new LlmReply(StripThinking(text), list);
	}

	public static string? StripThinking(string? text)
	{
		if (text == null)
		{
			return null;
		}
		for (int num = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase); num >= 0; num = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase))
		{
			int num2 = text.IndexOf("</think>", num, StringComparison.OrdinalIgnoreCase);
			string text4;
			if (num2 >= 0)
			{
				string text2 = text.Substring(0, num);
				string text3 = text;
				int num3 = num2 + 8;
				text4 = text2 + text3.Substring(num3, text3.Length - num3);
			}
			else
			{
				text4 = text.Substring(0, num);
			}
			text = text4;
		}
		return text.Trim();
	}

	public async Task<LlmReply> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool>? tools, CancellationToken ct)
	{
		string content = BuildRequest(messages, tools, _settings).ToJsonString();
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl(_settings.Endpoint))
		{
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		};
		if (_apiKey != null)
		{
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
		}
		req.Headers.TryAddWithoutValidation("X-Title", "Metamorph");
		using HttpResponseMessage resp = await _http.SendAsync(req, ct).ConfigureAwait(continueOnCapturedContext: false);
		string text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!resp.IsSuccessStatusCode)
		{
			string text2 = null;
			try
			{
				text2 = JsonNode.Parse(text)?["error"]?["message"]?.GetValue<string>();
			}
			catch (JsonException)
			{
			}
			throw new HttpRequestException($"HTTP {resp.StatusCode}: {text2 ?? text.Substring(0, Math.Min(text.Length, 200))}");
		}
		return ParseResponse(text);
	}
}
