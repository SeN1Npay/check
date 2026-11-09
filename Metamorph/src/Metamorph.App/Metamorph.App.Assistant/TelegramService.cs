using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;

namespace Metamorph.App.Assistant;

internal sealed class TelegramService : IDisposable
{
	private readonly App _app;

	private CancellationTokenSource? _cts;

	private TelegramBot? _bot;

	private string? _signature;

	private int _wrongCodes;

	private DateTime _pairingUntil;

	public string Status { get; private set; } = Loc.T("Выключен");


	public string? BotName { get; private set; }

	public bool Running => _cts != null;

	public string? PairingCode { get; private set; }

	public event Action? StatusChanged;

	public TelegramService(App app)
	{
		_app = app;
	}

	public void Sync(bool force = false)
	{
		TelegramSettings telegram = _app.Services.Settings.Current.Assistant.Telegram;
		string text = telegram.Enabled + "|" + telegram.TokenProtected;
		if (!force && text == _signature)
		{
			return;
		}
		_signature = text;
		Stop();
		if (!telegram.Enabled)
		{
			SetStatus(Loc.T("Выключен"));
			return;
		}
		string text2 = Secrets.Unprotect(telegram.TokenProtected);
		if (string.IsNullOrWhiteSpace(text2))
		{
			SetStatus(Loc.T("Нужен токен бота от @BotFather"));
			return;
		}
		TelegramBot bot;
		try
		{
			bot = new TelegramBot(text2);
		}
		catch (ArgumentException)
		{
			SetStatus(Loc.T("Неверный формат токена"));
			return;
		}
		_bot = bot;
		CancellationTokenSource cts = (_cts = new CancellationTokenSource());
		SetStatus(Loc.T("Подключаюсь…"));
		Task.Run(() => RunAsync(bot, cts.Token));
	}

	public string BeginPairing()
	{
		PairingCode = TelegramPairing.NewCode();
		_pairingUntil = DateTime.UtcNow.AddMinutes(10.0);
		_wrongCodes = 0;
		return PairingCode;
	}

	public void Stop()
	{
		_cts?.Cancel();
		_cts = null;
		_bot = null;
	}

	public async Task NotifyOwnerAsync(string text)
	{
		long? ownerId = _app.Services.Settings.Current.Assistant.Telegram.OwnerId;
		TelegramBot bot = _bot;
		if (!ownerId.HasValue)
		{
			return;
		}
		long valueOrDefault = ownerId.GetValueOrDefault();
		if (bot == null)
		{
			return;
		}
		try
		{
			await bot.SendAsync(valueOrDefault, text, CancellationToken.None);
		}
		catch (Exception ex)
		{
			Log.Warn("Telegram: уведомление не отправлено — " + ex.Message);
		}
	}

	private async Task RunAsync(TelegramBot bot, CancellationToken ct)
	{
		try
		{
			BotName = await bot.GetMeAsync(ct);
			SetStatus(Loc.F("Работает: @{0}", BotName));
		}
		catch (HttpRequestException ex) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			bool flag = default(bool);
			switch (ex.StatusCode)
			{
			case HttpStatusCode.Unauthorized:
			case HttpStatusCode.NotFound:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			return flag;
		}).Invoke())
		{
			SetStatus(Loc.T("Telegram отклонил токен — проверьте его в @BotFather"));
			return;
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex3)
		{
			SetStatus(Loc.F("Нет связи с Telegram: {0}", ex3.Message));
		}
		int delay = 2;
		while (!ct.IsCancellationRequested)
		{
			try
			{
				IReadOnlyList<TelegramMessage> readOnlyList = await bot.PollAsync(25, ct);
				if (delay > 2)
				{
					SetStatus(Loc.F("Работает: @{0}", BotName ?? "bot"));
				}
				delay = 2;
				foreach (TelegramMessage item in readOnlyList)
				{
					await HandleAsync(bot, item, ct);
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex5)
			{
				Log.Warn("Telegram: " + ex5.Message);
				SetStatus(Loc.F("Переподключение: {0}", ex5.Message));
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(delay), ct);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				delay = Math.Min(60, delay * 2);
			}
		}
	}

	private async Task HandleAsync(TelegramBot bot, TelegramMessage m, CancellationToken ct)
	{
		TelegramMessage i = m;
		TelegramSettings telegram = _app.Services.Settings.Current.Assistant.Telegram;
		string text = ((DateTime.UtcNow < _pairingUntil) ? PairingCode : null);
		switch (TelegramPairing.Check(telegram, i, text))
		{
		case TelegramAccess.Pair:
			PairingCode = null;
			await _app.Dispatcher.InvokeAsync(delegate
			{
				AppSettings appSettings = _app.Services.Settings.Load();
				appSettings.Assistant.Telegram.OwnerId = i.FromId;
				appSettings.Assistant.Telegram.OwnerName = i.FromName;
				_app.Services.Settings.Save(appSettings);
				_app.MascotSay(Loc.F("Привязала Telegram: {0}!", i.FromName));
			});
			await bot.SendAsync(i.ChatId, Loc.F("Привет, {0}! Теперь я отвечаю только тебе. Напиши «что ты умеешь».", i.FromName), ct);
			SetStatus(Loc.F("Работает: @{0}", BotName ?? "bot"));
			break;
		case TelegramAccess.Stranger:
			if (text != null && i.Text.Length < 40 && ++_wrongCodes > 10)
			{
				PairingCode = null;
			}
			if (i.ChatType == "private" && i.Text.StartsWith("/start", StringComparison.Ordinal))
			{
				await bot.SendAsync(i.ChatId, Loc.T("Это личный бот Metamorph. Чтобы привязать его, откройте Metamorph → «Мета-тян» → Telegram → «Привязать» и пришлите код."), ct);
			}
			break;
		default:
		{
			if (i.Text.Trim() == "/start")
			{
				await bot.SendAsync(i.ChatId, Loc.T("Я тут! Спрашивай что угодно или командуй: «следующая тема», «что играет», «громкость 30», «таймер на 10 минут»."), ct);
				break;
			}
			bot.TypingAsync(i.ChatId, ct);
			AssistantReply assistantReply = await _app.Dispatcher.InvokeAsync(() => _app.AskAssistantAsync(i.Text, AssistantSource.Telegram)).Task.Unwrap();
			await bot.SendAsync(i.ChatId, assistantReply.Text, ct);
			break;
		}
		}
	}

	private void SetStatus(string status)
	{
		Status = status;
		_app.Dispatcher.InvokeAsync(delegate
		{
			this.StatusChanged?.Invoke();
		});
	}

	public void Dispose()
	{
		Stop();
	}
}
