using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Launcher;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;

namespace Metamorph.Core.Assistant;

public sealed class AssistantEngine
{
	private const int HistoryLimit = 14;

	private readonly IAssistantHost _host;

	private readonly Func<AssistantSettings> _settings;

	private readonly Func<ILlmClient?> _llm;

	private readonly Random _rnd = new Random();

	private readonly Dictionary<AssistantSource, List<LlmMessage>> _history = new Dictionary<AssistantSource, List<LlmMessage>>();

	public static readonly string[] Jokes = new string[8] { "Почему программисты путают Хэллоуин и Рождество? Потому что 31 OCT = 25 DEC.", "Я хотела сменить тему разговора, но применилась тема оформления.", "— Мета, ты умеешь хранить секреты? — Конечно! Они у меня в %APPDATA%.", "Обои бывают живые и мёртвые. Мёртвые — это которые «Windows по умолчанию».", "Компьютер медленный? Просто он думает о прекрасном.", "Мой любимый напиток — Java. Но пью только чай.", "Есть 10 типов людей: те, кто понимает двоичную систему, и те, кто нет.", "Я не ленивая — я в режиме энергосбережения." };

	public AssistantEngine(IAssistantHost host, Func<AssistantSettings> settings, Func<ILlmClient?> llm)
	{
		_host = host;
		_settings = settings;
		_llm = llm;
	}

	public void ClearHistory(AssistantSource source)
	{
		_history.Remove(source);
	}

	public async Task<AssistantReply> AskAsync(string text, AssistantSource source = AssistantSource.Local, CancellationToken ct = default(CancellationToken))
	{
		text = (text ?? "").Trim();
		if (text.Length == 0)
		{
			return new AssistantReply(Loc.T("М?"), IntentKind.Unknown);
		}
		AssistantSettings assistantSettings = _settings();
		Intent intent = IntentParser.Parse(text);
		bool flag = source != AssistantSource.Local;
		bool llmOn = assistantSettings.Llm.Enabled && _llm() != null;
		if (intent.Kind != 0)
		{
			if (flag && (intent.IsLocalOnly || intent.Kind == IntentKind.PlayGame))
			{
				return new AssistantReply(Loc.T("Это я могу сделать только на самом компьютере."), intent.Kind, MascotAction.Think);
			}
			if (flag && intent.IsCommand && !assistantSettings.Telegram.AllowCommands)
			{
				return new AssistantReply(Loc.T("Команды из Telegram выключены в настройках Metamorph."), intent.Kind, MascotAction.Think);
			}
			AssistantReply assistantReply = await ExecuteAsync(intent);
			if ((object)assistantReply != null)
			{
				return assistantReply;
			}
		}
		if (llmOn)
		{
			try
			{
				return await AskLlmAsync(text, source, ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex2)
			{
				return new AssistantReply(Loc.F("Не могу достучаться до языковой модели: {0}", Short(ex2.Message)), IntentKind.Unknown, MascotAction.Think);
			}
		}
		return intent.Kind switch
		{
			IntentKind.ApplyTheme => new AssistantReply(Loc.F("Не нашла тему «{0}». Есть: {1}.", intent.Text, string.Join(", ", _host.ThemeNames.Take(8))), intent.Kind, MascotAction.Think), 
			IntentKind.SetWallpaper => new AssistantReply(Loc.F("Не нашла обои «{0}». Есть: {1}.", intent.Text, string.Join(", ", _host.WallpaperNames.Take(8))), intent.Kind, MascotAction.Think), 
			IntentKind.OpenApp => new AssistantReply(Loc.F("Не нашла приложение «{0}».", intent.Text), intent.Kind, MascotAction.Think), 
			_ => new AssistantReply(Loc.T("Я пока понимаю простые команды: «следующая тема», «таймер на 5 минут», «громче», «что играет», «открой блокнот». А если подключить языковую модель в настройках — поболтаем по-настоящему!"), IntentKind.Unknown, MascotAction.Think), 
		};
	}

	public async Task<AssistantReply?> ExecuteAsync(Intent intent)
	{
		string mascotName = _host.MascotName;
		switch (intent.Kind)
		{
		case IntentKind.Greeting:
			return new AssistantReply(MascotLines.Greeting(DateTime.Now.Hour, _rnd), intent.Kind, MascotAction.Wave);
		case IntentKind.Thanks:
			return new AssistantReply(Pick("Всегда пожалуйста!", "Обращайся~", "Рада помочь!"), intent.Kind, MascotAction.Love);
		case IntentKind.HowAreYou:
			return new AssistantReply(Pick("Отлично! Сижу на твоём рабочем столе и любуюсь темой.", "Хорошо! А у тебя?", "Прекрасно — ты же рядом!"), intent.Kind, MascotAction.Happy);
		case IntentKind.WhoAreYou:
			return new AssistantReply(Loc.F("Я {0} — маскот и помощница Metamorph. Меняю темы и обои, слежу за музыкой и таймерами, а ещё просто симпатичная.", mascotName), intent.Kind, MascotAction.Wave);
		case IntentKind.Help:
			return new AssistantReply(Loc.T("Умею: «следующая тема», «тема Nord», «удиви меня», «обои котик» (по имени файла из «Моих обоев»), «следующие обои», «громкость 30», «тише», «следующий трек», «что играет», «таймер на 10 минут», «помодоро», «который час», «открой блокнот», «найди котиков», «потанцуй», «пошути»."), intent.Kind, MascotAction.Happy);
		case IntentKind.Time:
			return new AssistantReply(Loc.F("Сейчас {0}.", DateTime.Now.ToString("t", Loc.Culture)), intent.Kind);
		case IntentKind.Date:
			return new AssistantReply(Loc.F("Сегодня {0}.", DateTime.Now.ToString("dddd, d MMMM yyyy", Loc.Culture)), intent.Kind);
		case IntentKind.Joke:
			return new AssistantReply(Loc.T(Jokes[_rnd.Next(Jokes.Length)]), intent.Kind, MascotAction.Happy);
		case IntentKind.Status:
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Loc.F("Тема: {0}.", _host.ActiveTheme ?? Loc.T("стандартная Windows")));
			string text4 = await _host.NowPlayingAsync();
			if (text4 != null)
			{
				sb.Append(' ').Append(Loc.F("Играет: {0}.", text4));
			}
			int? volume = _host.Volume;
			if (volume.HasValue)
			{
				int valueOrDefault = volume.GetValueOrDefault();
				sb.Append(' ').Append(Loc.F("Громкость: {0}%.", valueOrDefault));
			}
			if (_host.Timer.Running)
			{
				sb.Append(' ').Append(Loc.F("Таймер: осталось {0}.", FocusTimer.Format(_host.Timer.Remaining)));
			}
			return new AssistantReply(sb.ToString(), intent.Kind);
		}
		case IntentKind.NextTheme:
			await _host.NextThemeAsync();
			return new AssistantReply(Loc.F("Готово! Теперь тема «{0}».", _host.ActiveTheme ?? "?"), intent.Kind, MascotAction.Happy);
		case IntentKind.SurpriseTheme:
		{
			string text8 = await _host.SurpriseThemeAsync();
			return new AssistantReply((text8 == null) ? Loc.T("Ой, тем пока нет.") : Loc.F("Та-дам! «{0}». Нравится?", text8), intent.Kind, MascotAction.Happy);
		}
		case IntentKind.ApplyTheme:
		{
			if (string.IsNullOrWhiteSpace(intent.Text))
			{
				return null;
			}
			string text = await _host.ApplyThemeAsync(intent.Text);
			return (text == null) ? null : new AssistantReply(Loc.F("Включила тему «{0}»!", text), intent.Kind, MascotAction.Happy);
		}
		case IntentKind.DayNight:
			_host.ToggleDayNight();
			return new AssistantReply(Loc.T("Переключила день/ночь."), intent.Kind, MascotAction.Happy);
		case IntentKind.NextWallpaper:
			await _host.NextWallpaperAsync();
			return new AssistantReply(Loc.T("Новые обои!"), intent.Kind, MascotAction.Happy);
		case IntentKind.PauseWallpaper:
			return new AssistantReply(_host.ToggleWallpaperPause() ? Loc.T("Обои на паузе.") : Loc.T("Обои снова двигаются!"), intent.Kind);
		case IntentKind.SetWallpaper:
		{
			if (string.IsNullOrWhiteSpace(intent.Text))
			{
				return null;
			}
			string text6 = await _host.SetWallpaperAsync(intent.Text);
			return (text6 == null) ? null : new AssistantReply(Loc.F("Поставила обои «{0}».", text6), intent.Kind, MascotAction.Happy);
		}
		case IntentKind.VolumeUp:
			return VolumeReply(_host.SetVolume(null, 10), intent.Kind);
		case IntentKind.VolumeDown:
			return VolumeReply(_host.SetVolume(null, -10), intent.Kind);
		case IntentKind.VolumeSet:
			return VolumeReply(_host.SetVolume((int)intent.Number.GetValueOrDefault(50.0), 0), intent.Kind);
		case IntentKind.Mute:
			_host.SetMute(mute: true);
			return new AssistantReply(Loc.T("Звук выключен. Тс-с…"), intent.Kind);
		case IntentKind.Unmute:
			_host.SetMute(mute: false);
			return new AssistantReply(Loc.T("Звук включён!"), intent.Kind);
		case IntentKind.PlayPause:
			await _host.MediaAsync(MediaCommand.PlayPause);
			return new AssistantReply(Loc.T("Готово."), intent.Kind);
		case IntentKind.NextTrack:
			await _host.MediaAsync(MediaCommand.Next);
			return new AssistantReply(Loc.T("Следующий трек!"), intent.Kind, MascotAction.Dance);
		case IntentKind.PreviousTrack:
			await _host.MediaAsync(MediaCommand.Previous);
			return new AssistantReply(Loc.T("Возвращаю предыдущий."), intent.Kind);
		case IntentKind.WhatsPlaying:
		{
			string text2 = await _host.NowPlayingAsync();
			return new AssistantReply((text2 == null) ? Loc.T("Сейчас ничего не играет.") : Loc.F("Играет {0} ♪", text2), intent.Kind, (text2 == null) ? null : new MascotAction?(MascotAction.Dance));
		}
		case IntentKind.Timer:
		{
			TimeSpan timeSpan = intent.Duration ?? TimeSpan.FromMinutes(5.0);
			_host.Timer.Start(timeSpan, intent.Text);
			string text7 = intent.Text;
			return new AssistantReply((text7 != null) ? Loc.F("Таймер на {0}: «{1}». Напомню!", Human(timeSpan), text7) : Loc.F("Засекла {0}!", Human(timeSpan)), intent.Kind, MascotAction.Happy);
		}
		case IntentKind.Pomodoro:
			_host.Timer.StartPomodoro(intent.Duration);
			return new AssistantReply(Loc.F("Помодоро: {0} фокуса, потом перерыв. Я не буду отвлекать!", Human(_host.Timer.FocusLength)), intent.Kind, MascotAction.Happy);
		case IntentKind.CancelTimer:
			if (!_host.Timer.Running)
			{
				return new AssistantReply(Loc.T("Таймер и так не запущен."), intent.Kind);
			}
			_host.Timer.Cancel();
			return new AssistantReply(Loc.T("Таймер отменён."), intent.Kind);
		case IntentKind.TimerStatus:
			return new AssistantReply(_host.Timer.Running ? Loc.F("Осталось {0}.", FocusTimer.Format(_host.Timer.Remaining)) : Loc.T("Таймер не запущен."), intent.Kind);
		case IntentKind.OpenApp:
		{
			if (string.IsNullOrWhiteSpace(intent.Text))
			{
				return null;
			}
			string text5 = _host.OpenApp(intent.Text);
			return (text5 == null) ? null : new AssistantReply(Loc.F("Открываю {0}.", text5), intent.Kind);
		}
		case IntentKind.WebSearch:
			_host.OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(intent.Text ?? ""));
			return new AssistantReply(Loc.F("Ищу «{0}»…", intent.Text), intent.Kind);
		case IntentKind.Weather:
		{
			string text3 = intent.Text;
			string stringToEscape = ((text3 != null && text3.Length > 0) ? Loc.F("погода {0}", text3) : Loc.T("погода"));
			_host.OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(stringToEscape));
			return new AssistantReply(Loc.T("Открыла прогноз погоды."), intent.Kind);
		}
		case IntentKind.Calc:
		{
			double value;
			return Calculator.TryEvaluate(intent.Text ?? "", out value) ? new AssistantReply(Loc.F("{0} = {1}", intent.Text, Calculator.Format(value)), intent.Kind, MascotAction.Think) : null;
		}
		case IntentKind.Dance:
			_host.Mascot(MascotAction.Dance);
			return new AssistantReply(Pick("Танцуем!", "Смотри, как умею!"), intent.Kind);
		case IntentKind.Sleep:
			_host.Mascot(MascotAction.Sleep);
			return new AssistantReply(Pick("Спокойной ночи… Хр-р.", "Я чуть-чуть посплю."), intent.Kind);
		case IntentKind.Wave:
			_host.Mascot(MascotAction.Wave);
			return new AssistantReply(Pick("Привет-привет!", "Машу!"), intent.Kind);
		case IntentKind.HideMascot:
			_host.HideMascot();
			return new AssistantReply(Loc.T("Спрячусь. Позови через трей!"), intent.Kind);
		case IntentKind.ShowIsland:
			_host.ShowIsland();
			return new AssistantReply(Loc.T("Вот остров!"), intent.Kind);
		case IntentKind.Launcher:
			_host.ShowLauncher();
			return new AssistantReply(Loc.T("Открыла лаунчер."), intent.Kind);
		case IntentKind.PlayGame:
			_host.PlayGame(intent.Text);
			return new AssistantReply(intent.Text switch
			{
				"tetris" => Pick("Тетрис! Чур, я подсказываю, куда ставить палку!", "Обожаю тетрис! Сложи мне четыре линии разом~", "Тетрис-тетрис! Посмотрим, кто дольше продержится."),
				"dino" => Pick("Бежим! Я прыгаю — ты жмёшь пробел!", "Мета-раннер! Не дай мне врезаться в кристалл~", "На старт… внимание… бежим!"),
				_ => Pick("Ура, поиграем! Выбирай игру~", "Давай! Я уже размялась!", "Игры! Самое лучшее предложение за сегодня!")
			}, intent.Kind, MascotAction.Happy);
		default:
			return null;
		}
	}

	public string SystemPrompt(AssistantSource source)
	{
		AssistantSettings assistantSettings = _settings();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Loc.F("Ты — {0}, чиби-маскот и помощница в приложении Metamorph для Windows (темы, обои, панель, док, «остров»). ", _host.MascotName));
		stringBuilder.Append(Loc.T("Характер: весёлая, добрая, немного озорная, заботливая. Отвечай коротко — 1–3 предложения, без markdown, можно эмодзи изредка. Отвечай на языке пользователя. "));
		stringBuilder.Append(Loc.T("Если просят что-то сделать на компьютере — вызывай инструменты; никогда не говори, что сделала, если не вызвала инструмент. "));
		if (source != 0)
		{
			stringBuilder.Append(Loc.T("Пользователь пишет тебе из Telegram, он сейчас не у компьютера. "));
		}
		stringBuilder.Append(Loc.F("Сейчас {0}. ", DateTime.Now.ToString("f", Loc.Culture)));
		stringBuilder.Append(Loc.F("Активная тема: {0}. Доступные темы: {1}. ", _host.ActiveTheme ?? "Windows", string.Join(", ", _host.ThemeNames.Take(30))));
		if (!string.IsNullOrWhiteSpace(assistantSettings.Llm.Persona))
		{
			stringBuilder.Append(assistantSettings.Llm.Persona.Trim());
		}
		return stringBuilder.ToString();
	}

	public IReadOnlyList<LlmTool> Tools(AssistantSource source)
	{
		AssistantSettings assistantSettings = _settings();
		bool flag = source != AssistantSource.Local;
		List<LlmTool> list = new List<LlmTool>();
		list.Add(new LlmTool("get_status", "Current theme, music, volume and timer.", Obj(Array.Empty<(string, string, string)>())));
		if (flag && !assistantSettings.Telegram.AllowCommands)
		{
			return list;
		}
		list.Add(new LlmTool("apply_theme", "Apply a Metamorph theme by name. Use name \"next\" for the next theme or \"surprise\" for a random one.", Obj(new(string, string, string)[1] { ("name", "string", "Theme name") })));
		list.Add(new LlmTool("set_wallpaper", "Set a wallpaper from the user's library (My wallpapers: images, GIFs, videos) by file name, or \"next\".", Obj(new(string, string, string)[1] { ("name", "string", "File name (or part of it) or next") })));
		list.Add(new LlmTool("media", "Control music playback.", Obj(new(string, string, string)[1] { ("command", "string", "play_pause | next | previous") })));
		list.Add(new LlmTool("set_volume", "Set system volume 0-100.", Obj(new(string, string, string)[1] { ("level", "integer", "Volume percent") })));
		list.Add(new LlmTool("start_timer", "Start a countdown timer.", Obj(new(string, string, string)[2]
		{
			("minutes", "number", "Duration in minutes"),
			("label", "string", "What to remind about (may be empty)")
		})));
		list.Add(new LlmTool("mascot", "Make yourself (the mascot) do an animation.", Obj(new(string, string, string)[1] { ("action", "string", "wave | dance | sleep | jump | happy | love") })));
		if (!flag)
		{
			list.Add(new LlmTool("open_app", "Launch an installed application by name.", Obj(new(string, string, string)[1] { ("name", "string", "App name, e.g. Notepad, Spotify") })));
			list.Add(new LlmTool("web_search", "Open a web search in the browser.", Obj(new(string, string, string)[1] { ("query", "string", "Search query") })));
			list.Add(new LlmTool("play_game", "Open a mini-game starring you (the mascot): \"dino\" (runner like the Chrome dino), \"tetris\", or \"menu\" for the game picker.", Obj(new(string, string, string)[1] { ("game", "string", "dino | tetris | menu") })));
		}
		return list;
		static JsonObject Obj((string Name, string Type, string Desc)[] props)
		{
			JsonObject jsonObject = new JsonObject();
			JsonArray jsonArray = new JsonArray();
			for (int i = 0; i < props.Length; i++)
			{
				(string Name, string Type, string Desc) tuple = props[i];
				string item = tuple.Name;
				string item2 = tuple.Type;
				string item3 = tuple.Desc;
				jsonObject[item] = new JsonObject
				{
					["type"] = item2,
					["description"] = item3
				};
				jsonArray.Add(item);
			}
			return new JsonObject
			{
				["type"] = "object",
				["properties"] = jsonObject,
				["required"] = jsonArray
			};
		}
	}

	private async Task<AssistantReply> AskLlmAsync(string text, AssistantSource source, CancellationToken ct)
	{
		ILlmClient llm = _llm() ?? throw new InvalidOperationException(Loc.T("Языковая модель не настроена"));
		if (!_history.TryGetValue(source, out List<LlmMessage> history))
		{
			Dictionary<AssistantSource, List<LlmMessage>> history2 = _history;
			List<LlmMessage> value;
			history = (value = new List<LlmMessage>());
			history2[source] = value;
		}
		history.Add(new LlmMessage("user", text));
		Trim(history);
		IReadOnlyList<LlmTool> tools = Tools(source);
		MascotAction? mood = null;
		for (int round = 0; round < 4; round++)
		{
			List<LlmMessage> list = new List<LlmMessage>
			{
				new LlmMessage("system", SystemPrompt(source))
			};
			list.AddRange(history);
			LlmReply llmReply = await llm.CompleteAsync(list, (round < 3) ? tools : null, ct);
			if (llmReply.ToolCalls.Count == 0)
			{
				string text2 = (string.IsNullOrWhiteSpace(llmReply.Content) ? Loc.T("Хм… не знаю, что сказать.") : llmReply.Content.Trim());
				history.Add(new LlmMessage("assistant", text2));
				Trim(history);
				return new AssistantReply(text2, IntentKind.Unknown, mood, FromLlm: true);
			}
			history.Add(new LlmMessage("assistant", llmReply.Content, llmReply.ToolCalls));
			foreach (LlmToolCall call in llmReply.ToolCalls)
			{
				(string, MascotAction?) obj = await RunToolAsync(call, source, tools);
				string item = obj.Item1;
				MascotAction? item2 = obj.Item2;
				MascotAction? mascotAction = mood;
				if (!mascotAction.HasValue)
				{
					mood = item2;
				}
				history.Add(new LlmMessage("tool", item, null, call.Id));
			}
		}
		return new AssistantReply(Loc.T("Готово!"), IntentKind.Unknown, mood, FromLlm: true);
	}

	private async Task<(string Result, MascotAction? Mood)> RunToolAsync(LlmToolCall call, AssistantSource source, IReadOnlyList<LlmTool> allowed)
	{
		LlmToolCall call2 = call;
		if (!allowed.Any((LlmTool t) => t.Name == call2.Name))
		{
			return (Result: "error: tool not allowed", Mood: null);
		}
		JsonObject args;
		try
		{
			args = (JsonNode.Parse(string.IsNullOrWhiteSpace(call2.Arguments) ? "{}" : call2.Arguments) as JsonObject) ?? new JsonObject();
		}
		catch (JsonException)
		{
			args = new JsonObject();
		}
		try
		{
			string name = call2.Name;
			if (name != null)
			{
				switch (name.Length)
				{
				case 10:
					switch (name[0])
					{
					case 'g':
						if (!(name == "get_status"))
						{
							break;
						}
						return (Result: (await ExecuteAsync(new Intent(IntentKind.Status))).Text, Mood: null);
					case 's':
					{
						if (!(name == "set_volume"))
						{
							break;
						}
						double num2 = Num("level");
						if (double.IsNaN(num2))
						{
							return (Result: "error: level required", Mood: null);
						}
						int? num3 = _host.SetVolume((int)Math.Clamp(num2, 0.0, 100.0), 0);
						object item;
						if (num3.HasValue)
						{
							int? num4 = num3;
							item = "ok: volume " + num4;
						}
						else
						{
							item = "error: no audio device";
						}
						return (Result: (string)item, Mood: null);
					}
					case 'w':
						if (!(name == "web_search") || source != 0)
						{
							break;
						}
						_host.OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(Str("query")));
						return (Result: "ok", Mood: null);
					}
					break;
				case 11:
					switch (name[0])
					{
					case 'a':
					{
						if (!(name == "apply_theme"))
						{
							break;
						}
						string text5 = Str("name").Trim();
						string text6 = text5.ToLowerInvariant();
						Intent intent = ((!(text6 == "next") && (text6 == null || text6.Length != 0)) ? ((!(text6 == "surprise") && !(text6 == "random")) ? new Intent(IntentKind.ApplyTheme, text5) : new Intent(IntentKind.SurpriseTheme)) : new Intent(IntentKind.NextTheme));
						Intent intent2 = intent;
						AssistantReply assistantReply2 = await ExecuteAsync(intent2);
						return ((object)assistantReply2 == null) ? (Result: "error: theme not found. Available: " + string.Join(", ", _host.ThemeNames), Mood: (MascotAction?)null) : (Result: "ok: " + assistantReply2.Text, Mood: MascotAction.Happy);
					}
					case 's':
					{
						if (!(name == "start_timer"))
						{
							break;
						}
						double num = Num("minutes");
						if (double.IsNaN(num) || num <= 0.0)
						{
							return (Result: "error: minutes required", Mood: null);
						}
						string text4 = Str("label");
						_host.Timer.Start(TimeSpan.FromMinutes(Math.Min(num, 1440.0)), string.IsNullOrWhiteSpace(text4) ? null : text4);
						return (Result: "ok: timer started", Mood: MascotAction.Happy);
					}
					}
					break;
				case 13:
				{
					if (!(name == "set_wallpaper"))
					{
						break;
					}
					string text2 = Str("name").Trim();
					AssistantReply assistantReply = await ExecuteAsync((text2.Equals("next", StringComparison.OrdinalIgnoreCase) || text2.Length == 0) ? new Intent(IntentKind.NextWallpaper) : new Intent(IntentKind.SetWallpaper, text2));
					return ((object)assistantReply == null) ? (Result: "error: wallpaper not found. Available: " + string.Join(", ", _host.WallpaperNames), Mood: (MascotAction?)null) : (Result: "ok: " + assistantReply.Text, Mood: MascotAction.Happy);
				}
				case 9:
				{
					if (!(name == "play_game") || source != 0)
					{
						break;
					}
					string game = Str("game").Trim().ToLowerInvariant();
					_host.PlayGame(game.Contains("tetris") ? "tetris" : (game.Contains("dino") || game.Contains("run") ? "dino" : null));
					return (Result: "ok: game window opened", Mood: MascotAction.Happy);
				}
				case 5:
				{
					if (!(name == "media"))
					{
						break;
					}
					string text3 = Str("command").ToLowerInvariant();
					await _host.MediaAsync(text3.StartsWith("next") ? MediaCommand.Next : (text3.StartsWith("prev") ? MediaCommand.Previous : MediaCommand.PlayPause));
					return (Result: "ok", Mood: null);
				}
				case 6:
					if (!(name == "mascot"))
					{
						break;
					}
					return (Result: "ok", Mood: Str("action").ToLowerInvariant() switch
					{
						"dance" => MascotAction.Dance, 
						"sleep" => MascotAction.Sleep, 
						"jump" => MascotAction.Jump, 
						"love" => MascotAction.Love, 
						"happy" => MascotAction.Happy, 
						_ => MascotAction.Wave, 
					});
				case 8:
				{
					if (!(name == "open_app") || source != 0)
					{
						break;
					}
					string text = _host.OpenApp(Str("name"));
					return (Result: (text == null) ? "error: app not found" : ("ok: opened " + text), Mood: null);
				}
				}
			}
		}
		catch (Exception ex2)
		{
			return (Result: "error: " + Short(ex2.Message), Mood: null);
		}
		return (Result: "error: unknown tool", Mood: null);
		double Num(string key)
		{
			if (!(args[key] is JsonValue jsonValue) || (!jsonValue.TryGetValue<double>(out var value) && !double.TryParse(jsonValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
			{
				return double.NaN;
			}
			return value;
		}
		string Str(string key)
		{
			if (args[key] is JsonValue jsonValue2)
			{
				if (jsonValue2.TryGetValue<string>(out string value2))
				{
					return value2;
				}
				return jsonValue2.ToJsonString();
			}
			return "";
		}
	}

	private static void Trim(List<LlmMessage> history)
	{
		while (history.Count > 14)
		{
			history.RemoveAt(0);
		}
		while (true)
		{
			bool flag = history.Count > 0;
			if (flag)
			{
				string role = history[0].Role;
				bool flag2 = ((role == "tool" || role == "assistant") ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				history.RemoveAt(0);
				continue;
			}
			break;
		}
	}

	private AssistantReply VolumeReply(int? level, IntentKind kind)
	{
		if (level.HasValue)
		{
			return new AssistantReply(Loc.F("Громкость {0}%.", level), kind);
		}
		return new AssistantReply(Loc.T("Не вижу аудиоустройство."), kind, MascotAction.Think);
	}

	private string Pick(params string[] options)
	{
		return Loc.T(options[_rnd.Next(options.Length)]);
	}

	public static string Human(TimeSpan t)
	{
		long num = (long)Math.Round(t.TotalSeconds);
		long num2 = num / 3600;
		long num3 = num / 60 % 60;
		long num4 = num % 60;
		List<string> list = new List<string>();
		if (num2 > 0)
		{
			list.Add(Loc.F("{0} ч", num2));
		}
		if (num3 > 0)
		{
			list.Add(Loc.F("{0} мин", num3));
		}
		if (num4 > 0 || list.Count == 0)
		{
			list.Add(Loc.F("{0} сек", num4));
		}
		return string.Join(" ", list);
	}

	private static string Short(string s)
	{
		if (s.Length <= 160)
		{
			return s;
		}
		return s.Substring(0, 160) + "…";
	}
}
