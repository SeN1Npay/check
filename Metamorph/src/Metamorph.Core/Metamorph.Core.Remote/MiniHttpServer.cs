using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Metamorph.Core.Remote;

public sealed class MiniHttpServer : IDisposable
{
	private sealed class BadRequestException : Exception
	{
		public int Status { get; }

		public BadRequestException(string message, int status = 400) : base(message)
		{
			Status = status;
		}
	}

	private sealed class ByteReader
	{
		[CompilerGenerated]
		private Stream _stream;

		private readonly byte[] _buffer;

		private int _start;

		private int _end;

		public ByteReader(Stream stream)
		{
			_stream = stream;
			_buffer = new byte[16384];
		}

		private async Task<bool> FillAsync(CancellationToken ct)
		{
			if (_start > 0 && _start == _end)
			{
				_start = (_end = 0);
			}
			if (_end == _buffer.Length)
			{
				Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
				_end -= _start;
				_start = 0;
			}
			int num = await _stream.ReadAsync(_buffer.AsMemory(_end), ct);
			if (num <= 0)
			{
				return false;
			}
			_end += num;
			return true;
		}

		public async Task<string?> ReadLineAsync(int max, CancellationToken ct)
		{
			List<byte> line = new List<byte>();
			do
			{
				for (int i = _start; i < _end; i++)
				{
					if (_buffer[i] != 10)
					{
						continue;
					}
					line.AddRange(new ArraySegment<byte>(_buffer, _start, i - _start));
					_start = i + 1;
					if (line.Count > 0)
					{
						if (line[line.Count - 1] == 13)
						{
							line.RemoveAt(line.Count - 1);
						}
					}
					return Encoding.UTF8.GetString(line.ToArray());
				}
				line.AddRange(new ArraySegment<byte>(_buffer, _start, _end - _start));
				_start = _end;
				if (line.Count > max)
				{
					throw new BadRequestException("Line too long", 431);
				}
			}
			while (await FillAsync(ct));
			return (line.Count == 0) ? null : Encoding.UTF8.GetString(line.ToArray());
		}

		public async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
		{
			byte[] result = new byte[count];
			int have = Math.Min(count, _end - _start);
			Buffer.BlockCopy(_buffer, _start, result, 0, have);
			_start += have;
			int num;
			for (; have < count; have += num)
			{
				num = await _stream.ReadAsync(result.AsMemory(have, count - have), ct);
				if (num <= 0)
				{
					throw new IOException("Connection closed while reading body");
				}
			}
			return result;
		}
	}

	public const long MaxBodyBytes = 157286400L;

	private const int MaxHeaderBytes = 65536;

	private readonly Func<HttpRequest, CancellationToken, Task<HttpResponse>> _handler;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	private TcpListener? _listener;

	public int Port { get; private set; }

	public event Action<Exception>? Error;

	public MiniHttpServer(Func<HttpRequest, CancellationToken, Task<HttpResponse>> handler)
	{
		_handler = handler;
	}

	public void Start(int port = 0)
	{
		_listener = new TcpListener(IPAddress.Loopback, port);
		_listener.Start();
		Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
		Task.Run((Func<Task?>)AcceptLoopAsync);
	}

	private async Task AcceptLoopAsync()
	{
		while (!_cts.IsCancellationRequested && _listener != null)
		{
			TcpClient client;
			try
			{
				client = await _listener.AcceptTcpClientAsync(_cts.Token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (ObjectDisposedException)
			{
				break;
			}
			catch (Exception obj)
			{
				this.Error?.Invoke(obj);
				await Task.Delay(200);
				continue;
			}
			Task.Run(() => ServeAsync(client));
		}
	}

	private async Task ServeAsync(TcpClient client)
	{
		using (client)
		{
			_ = 3;
			try
			{
				client.NoDelay = true;
				NetworkStream stream = client.GetStream();
				ByteReader reader = new ByteReader(stream);
				for (int i = 0; i < 1000; i++)
				{
					if (_cts.IsCancellationRequested)
					{
						break;
					}
					using CancellationTokenSource idle = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
					idle.CancelAfter(TimeSpan.FromSeconds((i == 0) ? 30 : 120));
					HttpRequest request;
					try
					{
						request = await ReadRequestAsync(reader, stream, idle.Token);
					}
					catch (OperationCanceledException)
					{
						return;
					}
					catch (BadRequestException ex2)
					{
						await WriteAsync(stream, HttpResponse.Text(ex2.Message, ex2.Status), keepAlive: false, _cts.Token);
						return;
					}
					if (request == null)
					{
						return;
					}
					HttpResponse response;
					try
					{
						response = await _handler(request, _cts.Token);
					}
					catch (Exception ex3)
					{
						this.Error?.Invoke(ex3);
						response = HttpResponse.Text("Internal error: " + ex3.Message, 500);
					}
					bool keepAlive = !string.Equals(request.Header("Connection"), "close", StringComparison.OrdinalIgnoreCase);
					await WriteAsync(stream, response, keepAlive, _cts.Token);
					if (!keepAlive)
					{
						return;
					}
				}
			}
			catch (IOException)
			{
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception obj)
			{
				this.Error?.Invoke(obj);
			}
		}
	}

	private static async Task<HttpRequest?> ReadRequestAsync(ByteReader reader, Stream stream, CancellationToken ct)
	{
		string text = await reader.ReadLineAsync(65536, ct);
		if (text == null)
		{
			return null;
		}
		while (text.Length == 0)
		{
			text = await reader.ReadLineAsync(65536, ct);
			if (text == null)
			{
				return null;
			}
		}
		string[] parts = text.Split(' ');
		if (parts.Length < 3)
		{
			throw new BadRequestException("Bad request line");
		}
		Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		int total = 0;
		while (true)
		{
			string text2 = (await reader.ReadLineAsync(65536, ct)) ?? throw new BadRequestException("Unexpected end of headers");
			if (text2.Length == 0)
			{
				break;
			}
			total += text2.Length;
			if (total > 65536)
			{
				throw new BadRequestException("Headers too large", 431);
			}
			int num = text2.IndexOf(':');
			if (num > 0)
			{
				string key = text2.Substring(0, num).Trim();
				string text3 = text2;
				int num2 = num + 1;
				string text4 = text3.Substring(num2, text3.Length - num2).Trim();
				headers[key] = (headers.TryGetValue(key, out string value) ? (value + ", " + text4) : text4);
			}
		}
		if (headers.TryGetValue("Expect", out string value2) && value2.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
		{
			byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
			await stream.WriteAsync(bytes, ct);
		}
		byte[] body;
		string value4;
		long result2;
		if (headers.TryGetValue("Transfer-Encoding", out string value3) && value3.Contains("chunked", StringComparison.OrdinalIgnoreCase))
		{
			using MemoryStream ms = new MemoryStream();
			while (true)
			{
				string text5 = (await reader.ReadLineAsync(1024, ct)) ?? throw new BadRequestException("Bad chunk");
				int num3 = text5.IndexOf(';');
				if (num3 >= 0)
				{
					text5 = text5.Substring(0, num3);
				}
				if (!long.TryParse(text5.Trim(), NumberStyles.HexNumber, null, out var result) || result < 0)
				{
					throw new BadRequestException("Bad chunk size");
				}
				if (result == 0L)
				{
					break;
				}
				if (ms.Length + result > 157286400)
				{
					throw new BadRequestException("Body too large", 413);
				}
				MemoryStream memoryStream = ms;
				memoryStream.Write(await reader.ReadExactAsync((int)result, ct));
				await reader.ReadLineAsync(16, ct);
			}
			while (!string.IsNullOrEmpty(await reader.ReadLineAsync(65536, ct)))
			{
			}
			body = ms.ToArray();
		}
		else if (headers.TryGetValue("Content-Length", out value4) && long.TryParse(value4, out result2) && result2 > 0)
		{
			if (result2 > 157286400)
			{
				throw new BadRequestException("Body too large", 413);
			}
			body = await reader.ReadExactAsync((int)result2, ct);
		}
		else
		{
			body = Array.Empty<byte>();
		}
		string text6 = parts[1];
		int num4 = text6.IndexOf('?');
		string path = Uri.UnescapeDataString((num4 >= 0) ? text6.Substring(0, num4) : text6);
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (num4 >= 0)
		{
			string text3 = text6;
			int num5 = num4 + 1;
			string[] array = text3.Substring(num5, text3.Length - num5).Split('&', StringSplitOptions.RemoveEmptyEntries);
			foreach (string text7 in array)
			{
				int num6 = text7.IndexOf('=');
				string key2 = Uri.UnescapeDataString(((num6 >= 0) ? text7.Substring(0, num6) : text7).Replace('+', ' '));
				object obj;
				if (num6 < 0)
				{
					obj = "";
				}
				else
				{
					text3 = text7;
					num5 = num6 + 1;
					obj = Uri.UnescapeDataString(text3.Substring(num5, text3.Length - num5).Replace('+', ' '));
				}
				string value5 = (string)obj;
				dictionary[key2] = value5;
			}
		}
		return new HttpRequest
		{
			Method = parts[0].ToUpperInvariant(),
			Path = path,
			Query = dictionary,
			Headers = headers,
			Body = body
		};
	}

	private static async Task WriteAsync(Stream stream, HttpResponse response, bool keepAlive, CancellationToken ct)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("HTTP/1.1 ").Append(response.Status).Append(' ')
			.Append(Reason(response.Status))
			.Append("\r\n");
		stringBuilder.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
		stringBuilder.Append("Content-Length: ").Append(response.Body.Length).Append("\r\n");
		stringBuilder.Append("Cache-Control: no-store\r\n");
		stringBuilder.Append("Connection: ").Append(keepAlive ? "keep-alive" : "close").Append("\r\n");
		foreach (var (value, value2) in response.Headers)
		{
			stringBuilder.Append(value).Append(": ").Append(value2)
				.Append("\r\n");
		}
		stringBuilder.Append("\r\n");
		await stream.WriteAsync(Encoding.ASCII.GetBytes(stringBuilder.ToString()), ct);
		if (response.Body.Length != 0)
		{
			await stream.WriteAsync(response.Body, ct);
		}
		await stream.FlushAsync(ct);
	}

	private static string Reason(int status)
	{
		return status switch
		{
			200 => "OK", 
			202 => "Accepted", 
			204 => "No Content", 
			400 => "Bad Request", 
			401 => "Unauthorized", 
			403 => "Forbidden", 
			404 => "Not Found", 
			405 => "Method Not Allowed", 
			413 => "Payload Too Large", 
			431 => "Request Header Fields Too Large", 
			500 => "Internal Server Error", 
			_ => "Status", 
		};
	}

	public void Dispose()
	{
		_cts.Cancel();
		try
		{
			_listener?.Stop();
		}
		catch
		{
		}
		_listener = null;
	}
}
