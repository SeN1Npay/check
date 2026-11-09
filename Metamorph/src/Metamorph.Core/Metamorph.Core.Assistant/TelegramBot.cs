using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Assistant;

public sealed class TelegramBot : IDisposable
{
	private readonly HttpClient _http;

	private readonly string _base;

	private long _offset;

	public TelegramBot(string token, HttpClient? http = null)
	{
		if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
		{
			throw new ArgumentException("token");
		}
		_base = "https://api.telegram.org/bot" + token.Trim() + "/";
		_http = http ?? new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60.0)
		};
	}

	public async Task<string?> GetMeAsync(CancellationToken ct)
	{
		return (await CallAsync("getMe", null, ct).ConfigureAwait(continueOnCapturedContext: false))?["result"]?["username"]?.GetValue<string>();
	}

	public async Task<IReadOnlyList<TelegramMessage>> PollAsync(int timeoutSeconds, CancellationToken ct)
	{
		JsonObject jsonObject = new JsonObject();
		jsonObject["offset"] = _offset;
		jsonObject["timeout"] = Math.Clamp(timeoutSeconds, 0, 50);
		jsonObject["allowed_updates"] = new JsonArray("message");
		JsonObject body = jsonObject;
		long maxUpdateId;
		List<TelegramMessage> result = ParseUpdates(await CallAsync("getUpdates", body, ct).ConfigureAwait(continueOnCapturedContext: false), out maxUpdateId);
		if (maxUpdateId >= _offset)
		{
			_offset = maxUpdateId + 1;
		}
		return result;
	}

	public static List<TelegramMessage> ParseUpdates(JsonNode? root, out long maxUpdateId)
	{
		maxUpdateId = -1L;
		List<TelegramMessage> list = new List<TelegramMessage>();
		if (!(root?["result"] is JsonArray jsonArray))
		{
			return list;
		}
		foreach (JsonNode item in jsonArray)
		{
			long valueOrDefault = (item?["update_id"]?.GetValue<long>()).GetValueOrDefault(-1L);
			maxUpdateId = Math.Max(maxUpdateId, valueOrDefault);
			JsonNode? obj = item?["message"];
			string text = obj?["text"]?.GetValue<string>();
			JsonNode jsonNode = obj?["chat"];
			JsonNode jsonNode2 = obj?["from"];
			if (obj != null && text != null && jsonNode != null && jsonNode2 != null)
			{
				JsonNode? jsonNode3 = jsonNode2["is_bot"];
				if (jsonNode3 == null || !jsonNode3.GetValue<bool>())
				{
					string fromName = jsonNode2["first_name"]?.GetValue<string>() ?? jsonNode2["username"]?.GetValue<string>() ?? "?";
					list.Add(new TelegramMessage(valueOrDefault, jsonNode["id"].GetValue<long>(), jsonNode2["id"].GetValue<long>(), fromName, jsonNode["type"]?.GetValue<string>() ?? "", text));
				}
			}
		}
		return list;
	}

	public Task SendAsync(long chatId, string text, CancellationToken ct)
	{
		return CallAsync("sendMessage", new JsonObject
		{
			["chat_id"] = chatId,
			["text"] = ((text.Length > 4000) ? text.Substring(0, 4000) : text)
		}, ct);
	}

	public Task TypingAsync(long chatId, CancellationToken ct)
	{
		return CallAsync("sendChatAction", new JsonObject
		{
			["chat_id"] = chatId,
			["action"] = "typing"
		}, ct);
	}

	private async Task<JsonNode?> CallAsync(string method, JsonObject? body, CancellationToken ct)
	{
		HttpResponseMessage httpResponseMessage = ((body != null) ? (await _http.PostAsJsonAsync(_base + method, body, ct).ConfigureAwait(continueOnCapturedContext: false)) : (await _http.GetAsync(_base + method, ct).ConfigureAwait(continueOnCapturedContext: false)));
		using HttpResponseMessage resp = httpResponseMessage;
		string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		JsonNode jsonNode = null;
		try
		{
			jsonNode = JsonNode.Parse(json);
		}
		catch (JsonException)
		{
		}
		if (!resp.IsSuccessStatusCode || jsonNode == null || !(jsonNode["ok"]?.GetValue<bool>()).GetValueOrDefault())
		{
			string text = jsonNode?["description"]?.GetValue<string>() ?? ((int)resp.StatusCode).ToString(CultureInfo.InvariantCulture);
			throw new HttpRequestException("Telegram: " + text, null, resp.StatusCode);
		}
		return jsonNode;
	}

	public void Dispose()
	{
		_http.Dispose();
	}
}
