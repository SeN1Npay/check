using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Metamorph.Core.Services;

public static class JsoncFile
{
	private static readonly JsonDocumentOptions ReadOptions = new JsonDocumentOptions
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static JsonObject Load(string path)
	{
		if (!File.Exists(path))
		{
			return new JsonObject();
		}
		string text = File.ReadAllText(path);
		if (string.IsNullOrWhiteSpace(text))
		{
			return new JsonObject();
		}
		JsonDocumentOptions readOptions = ReadOptions;
		return (JsonNode.Parse(text, null, readOptions) as JsonObject) ?? throw new InvalidDataException(path + ": ожидался JSON-объект");
	}

	public static JsonObject Parse(string text)
	{
		JsonDocumentOptions readOptions = ReadOptions;
		return (JsonNode.Parse(text, null, readOptions) as JsonObject) ?? new JsonObject();
	}

	public static void Save(string path, JsonObject root)
	{
		BackupOnce(path);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".metamorph.tmp";
		File.WriteAllText(text, root.ToJsonString(WriteOptions));
		File.Move(text, path, overwrite: true);
	}

	public static string ToText(JsonNode? node)
	{
		return node?.ToJsonString(WriteOptions) ?? "null";
	}

	public static void BackupOnce(string path)
	{
		string text = path + ".metamorph.bak";
		if (File.Exists(path) && !File.Exists(text))
		{
			File.Copy(path, text);
		}
	}

	public static JsonObject Obj(JsonObject parent, string name)
	{
		if (parent[name] is JsonObject result)
		{
			return result;
		}
		return (JsonObject)(parent[name] = new JsonObject());
	}
}
