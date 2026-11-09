using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Metamorph.App.Assistant;
using Metamorph.App.Infrastructure;
using Metamorph.App.Mascot;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;
using Metamorph.Core.Services;

namespace Metamorph.App.Views;

internal sealed class MascotView : UserControl
{
	private readonly App _app;

	private readonly Action<string, bool> _status;

	private readonly MascotCanvas _preview = new MascotCanvas
	{
		Width = 230.0,
		Height = 250.0
	};

	private readonly MascotAnimator _anim = new MascotAnimator(11);

	private readonly DispatcherTimer _frame = new DispatcherTimer(DispatcherPriority.Render)
	{
		Interval = TimeSpan.FromMilliseconds(33.0)
	};

	private readonly DispatcherTimer _debounce = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(400.0)
	};

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private double _last;

	private double _clipUntil;

	private readonly CheckBox _enabled = Check("Показывать Мету на рабочем столе");

	private readonly TextBox _name = Ui.TextBox("", 220.0);

	private readonly Slider _scale = SliderOf(0.5, 2.0);

	private readonly CheckBox _themeColors = Check("Цвета из текущей темы");

	private readonly TextBox _hair = Ui.TextBox("", 110.0);

	private readonly TextBox _eyes = Ui.TextBox("", 110.0);

	private readonly TextBox _outfit = Ui.TextBox("", 110.0);

	private readonly CheckBox _walk = Check("Гуляет по низу экрана");

	private readonly CheckBox _dance = Check("Танцует под музыку");

	private readonly CheckBox _speech = Check("Говорит реплики в облачке");

	private readonly Slider _chatty = SliderOf(0.0, 3.0);

	private readonly CheckBox _sleep = Check("Засыпает, когда никого нет");

	private readonly CheckBox _hideFs = Check("Прячется в играх и полноэкранном видео");

	private readonly CheckBox _speak = Check("Озвучивать ответы голосом");

	private readonly ComboBox _voice = new ComboBox
	{
		Width = 300.0,
		HorizontalAlignment = HorizontalAlignment.Left
	};

	private readonly Slider _pitch = SliderOf(-50.0, 50.0);

	private readonly Slider _rate = SliderOf(0.5, 2.0);

	private readonly CheckBox _voiceInput = Check("Голосовой ввод в чате — кнопка \ud83c\udfa4 (экспериментально)");

	private readonly CheckBox _llmOn = Check("Подключить языковую модель (свободный разговор)");

	private readonly TextBox _endpoint = Ui.TextBox("", 340.0);

	private readonly TextBox _model = Ui.TextBox("", 220.0);

	private readonly PasswordBox _apiKey = new PasswordBox
	{
		Width = 340.0,
		HorizontalAlignment = HorizontalAlignment.Left
	};

	private readonly TextBox _persona = new TextBox
	{
		Height = 60.0,
		TextWrapping = TextWrapping.Wrap,
		AcceptsReturn = true,
		VerticalScrollBarVisibility = ScrollBarVisibility.Auto
	};

	private readonly Slider _temperature = SliderOf(0.0, 1.5);

	private readonly TextBlock _llmInfo = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
	};

	private readonly CheckBox _tgOn = Check("Включить Telegram-бота (экспериментально)");

	private readonly PasswordBox _token = new PasswordBox
	{
		Width = 340.0,
		HorizontalAlignment = HorizontalAlignment.Left
	};

	private readonly CheckBox _tgCommands = Check("Разрешить команды компьютеру из Telegram (тема, обои, музыка, громкость, таймер)");

	private readonly TextBlock _tgStatus = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		FontWeight = FontWeights.SemiBold,
		Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
	};

	private readonly TextBlock _tgOwner = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 2.0, 0.0, 4.0)
	};

	private readonly TextBlock _tgCode = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		FontSize = 15.0,
		Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
	};

	private bool _loading;

	public MascotView(App app, Action<string, bool> status)
	{
		_app = app;
		_status = status;
		_llmInfo.Foreground = Ui.Res("SubTextBrush");
		_tgOwner.Foreground = Ui.Res("SubTextBrush");
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		stackPanel.Children.Add(Ui.Header("Мета-тян"));
		stackPanel.Children.Add(Ui.Hint("Чиби-маскот на рабочем столе. Гуляет, спит, танцует под музыку, реагирует на темы. Её можно тащить и бросать мышью, клик — потыкать, клик по голове — погладить, двойной клик или Ctrl+Alt+A — чат, правый клик — меню."));
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 6.0)
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 20.0, 0.0)
		};
		stackPanel2.Children.Add(new Border
		{
			Background = Ui.Res("CardBrush"),
			CornerRadius = new CornerRadius(14.0),
			Child = _preview,
			Padding = new Thickness(6.0)
		});
		WrapPanel wrapPanel = new WrapPanel
		{
			Width = 250.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		(string, MascotClip)[] array = new(string, MascotClip)[8]
		{
			("Помаши", MascotClip.Wave),
			("Танец", MascotClip.Dance),
			("Радость", MascotClip.Happy),
			("Любовь", MascotClip.Love),
			("Сон", MascotClip.Sleep),
			("Думает", MascotClip.Think),
			("Ой!", MascotClip.Surprised),
			("Кружится", MascotClip.Dizzy)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, MascotClip) tuple = array[i];
			string item = tuple.Item1;
			MascotClip clip = tuple.Item2;
			Button button = Ui.Button(item, delegate
			{
				PlayPreview(clip);
			});
			button.Padding = new Thickness(8.0, 3.0, 8.0, 3.0);
			button.Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			wrapPanel.Children.Add(button);
		}
		stackPanel2.Children.Add(wrapPanel);
		DockPanel.SetDock(stackPanel2, System.Windows.Controls.Dock.Left);
		dockPanel.Children.Add(stackPanel2);
		StackPanel stackPanel3 = new StackPanel
		{
			Children = 
			{
				(UIElement)_enabled,
				(UIElement)Ui.Row("Имя", _name, "Как она себя называет в чате и Telegram.", 160.0),
				(UIElement)Ui.Row("Размер", _scale, null, 160.0),
				(UIElement)_themeColors
			}
		};
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		stackPanel4.Children.Add(Ui.Label("Волосы", 60.0));
		stackPanel4.Children.Add(_hair);
		stackPanel4.Children.Add(Ui.Label("Глаза", 50.0));
		stackPanel4.Children.Add(_eyes);
		stackPanel4.Children.Add(Ui.Label("Одежда", 60.0));
		stackPanel4.Children.Add(_outfit);
		TextBox[] array2 = new TextBox[3] { _hair, _eyes, _outfit };
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].Margin = new Thickness(0.0, 0.0, 14.0, 0.0);
		}
		stackPanel3.Children.Add(stackPanel4);
		stackPanel3.Children.Add(_walk);
		stackPanel3.Children.Add(_dance);
		stackPanel3.Children.Add(_speech);
		stackPanel3.Children.Add(Ui.Row("Болтливость", _chatty, "0 — молчит, пока не спросят; 3 — болтушка.", 160.0));
		stackPanel3.Children.Add(_sleep);
		stackPanel3.Children.Add(_hideFs);
		WrapPanel wrapPanel2 = new WrapPanel
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		wrapPanel2.Children.Add(Ui.Button("Чат с Метой (Ctrl+Alt+A)", _app.ShowAssistant, primary: true));
		wrapPanel2.Children.Add(Ui.Button("Позвать сюда", delegate
		{
			if (!_app.IsMascotVisible)
			{
				_app.ToggleMascot();
			}
			_app.MascotBrain?.Command(MascotAction.Wave);
		}));
		stackPanel3.Children.Add(wrapPanel2);
		dockPanel.Children.Add(stackPanel3);
		stackPanel.Children.Add(dockPanel);
		stackPanel.Children.Add(Ui.Header("Голос"));
		stackPanel.Children.Add(Ui.Hint("Синтез речи Windows. Русские голоса (например, Irina) ставятся с языковым пакетом: Параметры → Время и язык → Речь."));
		stackPanel.Children.Add(_speak);
		stackPanel.Children.Add(Ui.Row("Голос", _voice));
		stackPanel.Children.Add(Ui.Row("Высота", _pitch, "Чиби-голос — чуть выше обычного."));
		stackPanel.Children.Add(Ui.Row("Скорость", _rate));
		stackPanel.Children.Add(Ui.Button("Проверить голос", delegate
		{
			_app.Voice.SpeakAsync(Loc.F("Привет! Я {0}. Так звучит мой голос.", _app.Services.Settings.Current.Mascot.Name), _app.Services.Settings.Current.Assistant);
		}));
		stackPanel.Children.Add(_voiceInput);
		stackPanel.Children.Add(Ui.Header("Языковая модель"));
		stackPanel.Children.Add(Ui.Hint("Без модели Мета понимает простые команды. С моделью — разговаривает и сама решает, что сделать (сменить тему, обои, громкость, поставить таймер). Подходит любой OpenAI-совместимый API: Ollama и LM Studio работают локально и бесплатно, OpenRouter/OpenAI — по ключу."));
		stackPanel.Children.Add(_llmOn);
		WrapPanel wrapPanel3 = new WrapPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		wrapPanel3.Children.Add(Ui.Button("Ollama", delegate
		{
			SetEndpoint("http://localhost:11434/v1", "qwen2.5:7b");
		}));
		wrapPanel3.Children.Add(Ui.Button("LM Studio", delegate
		{
			SetEndpoint("http://localhost:1234/v1", "local-model");
		}));
		wrapPanel3.Children.Add(Ui.Button("OpenRouter", delegate
		{
			SetEndpoint("https://openrouter.ai/api/v1", "openai/gpt-4o-mini");
		}));
		wrapPanel3.Children.Add(Ui.Button("OpenAI", delegate
		{
			SetEndpoint("https://api.openai.com/v1", "gpt-4o-mini");
		}));
		stackPanel.Children.Add(Ui.Row("Готовые адреса", wrapPanel3));
		stackPanel.Children.Add(Ui.Row("Адрес API", _endpoint));
		stackPanel.Children.Add(Ui.Row("Модель", _model));
		stackPanel.Children.Add(Ui.Row("Ключ API", _apiKey, "Хранится зашифрованным (DPAPI) только для вашей учётной записи Windows. Для локальных моделей не нужен."));
		stackPanel.Children.Add(Ui.Row("Характер (дополнительно)", _persona, "Свои инструкции: как говорить, о чём помнить."));
		stackPanel.Children.Add(Ui.Row("Креативность", _temperature));
		stackPanel.Children.Add(Ui.Button("Проверить подключение", TestLlm));
		stackPanel.Children.Add(_llmInfo);
		stackPanel.Children.Add(Ui.Header("Telegram"));
		stackPanel.Children.Add(Ui.Hint("Мета может отвечать вам в Telegram, пока компьютер включён. Создайте бота у @BotFather (команда /newbot), вставьте токен, нажмите «Привязать» и отправьте боту код — после этого он отвечает только вам."));
		stackPanel.Children.Add(_tgOn);
		stackPanel.Children.Add(Ui.Row("Токен бота", _token, "Хранится зашифрованным (DPAPI)."));
		stackPanel.Children.Add(_tgCommands);
		WrapPanel wrapPanel4 = new WrapPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		};
		wrapPanel4.Children.Add(Ui.Button("Сохранить токен", SaveToken, primary: true));
		wrapPanel4.Children.Add(Ui.Button("Привязать", Pair));
		wrapPanel4.Children.Add(Ui.Button("Отвязать", Unpair));
		stackPanel.Children.Add(wrapPanel4);
		stackPanel.Children.Add(_tgStatus);
		stackPanel.Children.Add(_tgOwner);
		stackPanel.Children.Add(_tgCode);
		stackPanel.Children.Add(Ui.Header("Discord"));
		stackPanel.Children.Add(Ui.Hint("Discord-бот — в следующей версии: те же ответы и команды через Gateway, привязка владельца кодом."));
		base.Content = Ui.Card(new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		CheckBox[] array3 = new CheckBox[12]
		{
			_enabled, _themeColors, _walk, _dance, _speech, _sleep, _hideFs, _speak, _voiceInput, _llmOn,
			_tgOn, _tgCommands
		};
		foreach (CheckBox obj in array3)
		{
			obj.Checked += delegate
			{
				Changed(immediate: true);
			};
			obj.Unchecked += delegate
			{
				Changed(immediate: true);
			};
		}
		Slider[] array4 = new Slider[5] { _scale, _chatty, _pitch, _rate, _temperature };
		for (int i = 0; i < array4.Length; i++)
		{
			array4[i].ValueChanged += delegate
			{
				Changed(immediate: false);
			};
		}
		array2 = new TextBox[7] { _name, _hair, _eyes, _outfit, _endpoint, _model, _persona };
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].TextChanged += delegate
			{
				Changed(immediate: false);
			};
		}
		_voice.SelectionChanged += delegate
		{
			Changed(immediate: true);
		};
		_apiKey.PasswordChanged += delegate
		{
			Changed(immediate: false);
		};
		_debounce.Tick += delegate
		{
			_debounce.Stop();
			Save();
		};
		_frame.Tick += delegate
		{
			Frame();
		};
		base.IsVisibleChanged += delegate
		{
			if (base.IsVisible)
			{
				Load();
				_last = _clock.Elapsed.TotalSeconds;
				_frame.Start();
			}
			else
			{
				_frame.Stop();
				if (_debounce.IsEnabled)
				{
					_debounce.Stop();
					Save();
				}
			}
		};
		_app.Telegram.StatusChanged += UpdateTelegram;
		base.Unloaded += delegate
		{
			_app.Telegram.StatusChanged -= UpdateTelegram;
		};
	}

	private static CheckBox Check(string text)
	{
		return new CheckBox
		{
			Content = Loc.T(text),
			Margin = new Thickness(0.0, 3.0, 0.0, 3.0)
		};
	}

	private static Slider SliderOf(double min, double max)
	{
		return new Slider
		{
			Minimum = min,
			Maximum = max,
			Width = 260.0,
			HorizontalAlignment = HorizontalAlignment.Left
		};
	}

	private void Load()
	{
		_loading = true;
		try
		{
			AppSettings current = _app.Services.Settings.Current;
			MascotSettings mascot = current.Mascot;
			_enabled.IsChecked = mascot.Enabled;
			_name.Text = mascot.Name;
			_scale.Value = mascot.Scale;
			_themeColors.IsChecked = mascot.ThemeColors;
			_hair.Text = mascot.Hair;
			_eyes.Text = mascot.Eyes;
			_outfit.Text = mascot.Outfit;
			_walk.IsChecked = mascot.Walk;
			_dance.IsChecked = mascot.Dance;
			_speech.IsChecked = mascot.Speech;
			_chatty.Value = mascot.Chattiness;
			_sleep.IsChecked = mascot.Sleep;
			_hideFs.IsChecked = mascot.HideInFullscreen;
			AssistantSettings assistant = current.Assistant;
			_speak.IsChecked = assistant.SpeakReplies;
			_voice.Items.Clear();
			_voice.Items.Add(Loc.T("Автоматически (по языку)"));
			foreach (var (text, text2) in Voice.Voices())
			{
				_voice.Items.Add(text + " — " + text2);
			}
			_voice.SelectedIndex = 0;
			for (int i = 1; i < _voice.Items.Count; i++)
			{
				if (((string)_voice.Items[i]).StartsWith(assistant.Voice + " — ", StringComparison.OrdinalIgnoreCase) && assistant.Voice.Length > 0)
				{
					_voice.SelectedIndex = i;
				}
			}
			_pitch.Value = assistant.VoicePitch;
			_rate.Value = assistant.VoiceRate;
			_voiceInput.IsChecked = assistant.VoiceInput;
			_llmOn.IsChecked = assistant.Llm.Enabled;
			_endpoint.Text = assistant.Llm.Endpoint;
			_model.Text = assistant.Llm.Model;
			_apiKey.Password = "";
			_apiKey.ToolTip = (string.IsNullOrEmpty(assistant.Llm.ApiKeyProtected) ? Loc.T("Ключ не задан") : Loc.T("Ключ сохранён (введите новый, чтобы заменить)"));
			_persona.Text = assistant.Llm.Persona;
			_temperature.Value = assistant.Llm.Temperature;
			_tgOn.IsChecked = assistant.Telegram.Enabled;
			_token.Password = "";
			_tgCommands.IsChecked = assistant.Telegram.AllowCommands;
			UpdateTelegram();
			UpdateColorsEnabled();
		}
		finally
		{
			_loading = false;
		}
	}

	private void UpdateColorsEnabled()
	{
		bool flag = !_themeColors.IsChecked.GetValueOrDefault();
		TextBox hair = _hair;
		TextBox eyes = _eyes;
		bool flag3 = (_outfit.IsEnabled = flag);
		bool isEnabled = (eyes.IsEnabled = flag3);
		hair.IsEnabled = isEnabled;
	}

	private void Changed(bool immediate)
	{
		if (!_loading)
		{
			UpdateColorsEnabled();
			_debounce.Stop();
			if (immediate)
			{
				Save();
			}
			else
			{
				_debounce.Start();
			}
		}
	}

	private void Save()
	{
		try
		{
			AppSettings appSettings = _app.Services.Settings.Load();
			MascotSettings mascot = appSettings.Mascot;
			mascot.Enabled = _enabled.IsChecked.GetValueOrDefault();
			mascot.Name = ((_name.Text.Trim().Length > 0) ? _name.Text.Trim() : "Мета");
			mascot.Scale = Math.Round(_scale.Value, 2);
			mascot.ThemeColors = _themeColors.IsChecked.GetValueOrDefault();
			if (IsHex(_hair.Text))
			{
				mascot.Hair = _hair.Text.Trim();
			}
			if (IsHex(_eyes.Text))
			{
				mascot.Eyes = _eyes.Text.Trim();
			}
			if (IsHex(_outfit.Text))
			{
				mascot.Outfit = _outfit.Text.Trim();
			}
			mascot.Walk = _walk.IsChecked.GetValueOrDefault();
			mascot.Dance = _dance.IsChecked.GetValueOrDefault();
			mascot.Speech = _speech.IsChecked.GetValueOrDefault();
			mascot.Chattiness = Math.Round(_chatty.Value, 1);
			mascot.Sleep = _sleep.IsChecked.GetValueOrDefault();
			mascot.HideInFullscreen = _hideFs.IsChecked.GetValueOrDefault();
			appSettings.Mascot = MascotSettings.Sanitize(mascot);
			AssistantSettings assistant = appSettings.Assistant;
			assistant.SpeakReplies = _speak.IsChecked.GetValueOrDefault();
			assistant.Voice = ((_voice.SelectedIndex <= 0) ? "" : ((string)_voice.SelectedItem).Split(" — ")[0]);
			assistant.VoicePitch = (int)Math.Round(_pitch.Value);
			assistant.VoiceRate = Math.Round(_rate.Value, 2);
			assistant.VoiceInput = _voiceInput.IsChecked.GetValueOrDefault();
			assistant.Llm.Enabled = _llmOn.IsChecked.GetValueOrDefault();
			assistant.Llm.Endpoint = _endpoint.Text.Trim();
			assistant.Llm.Model = _model.Text.Trim();
			if (_apiKey.Password.Length > 0)
			{
				assistant.Llm.ApiKeyProtected = Secrets.Protect(_apiKey.Password);
			}
			assistant.Llm.Persona = _persona.Text;
			assistant.Llm.Temperature = Math.Round(_temperature.Value, 2);
			assistant.Telegram.Enabled = _tgOn.IsChecked.GetValueOrDefault();
			assistant.Telegram.AllowCommands = _tgCommands.IsChecked.GetValueOrDefault();
			_app.Services.Settings.Save(appSettings);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}

	private static bool IsHex(string text)
	{
		return Regex.IsMatch(text.Trim(), "^#[0-9A-Fa-f]{6}$");
	}

	private void SetEndpoint(string endpoint, string model)
	{
		_endpoint.Text = endpoint;
		_model.Text = model;
		Changed(immediate: true);
	}

	private async void TestLlm()
	{
		Save();
		LlmSettings llm = _app.Services.Settings.Current.Assistant.Llm;
		_llmInfo.Text = Loc.T("Проверяю…");
		try
		{
			OpenAiCompatibleClient openAiCompatibleClient = new OpenAiCompatibleClient(llm, Secrets.Unprotect(llm.ApiKeyProtected));
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(60.0));
			LlmReply llmReply = await openAiCompatibleClient.CompleteAsync(new LlmMessage[2]
			{
				new LlmMessage("system", Loc.T("Ответь одним коротким предложением.")),
				new LlmMessage("user", Loc.T("Привет! Ты на связи?"))
			}, null, cts.Token);
			_llmInfo.Text = Loc.T("Работает! Ответ модели: ") + (llmReply.Content ?? "—");
			_status(Loc.T("Языковая модель подключена"), arg2: false);
		}
		catch (Exception ex) when (((ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException || ex is JsonException) ? 1 : 0) != 0)
		{
			_llmInfo.Text = Loc.F("Не получилось: {0}", ex.Message) + "\n" + Loc.T("Для Ollama: установите ollama.com, выполните «ollama pull qwen2.5:7b» и проверьте, что сервер запущен.");
		}
	}

	private void SaveToken()
	{
		if (_token.Password.Trim().Length == 0)
		{
			_status(Loc.T("Вставьте токен бота от @BotFather"), arg2: true);
			return;
		}
		AppSettings appSettings = _app.Services.Settings.Load();
		appSettings.Assistant.Telegram.TokenProtected = Secrets.Protect(_token.Password.Trim());
		appSettings.Assistant.Telegram.Enabled = true;
		_app.Services.Settings.Save(appSettings);
		_token.Password = "";
		_loading = true;
		_tgOn.IsChecked = true;
		_loading = false;
		_status(Loc.T("Токен сохранён"), arg2: false);
		UpdateTelegram();
	}

	private void Pair()
	{
		if (!_app.Telegram.Running)
		{
			_status(Loc.T("Сначала включите бота и сохраните токен"), arg2: true);
			return;
		}
		string text = _app.Telegram.BeginPairing();
		string botName = _app.Telegram.BotName;
		string text2 = ((botName != null) ? ("@" + botName) : Loc.T("вашему боту"));
		_tgCode.Text = Loc.F("Отправьте {0} код: {1} (действует 10 минут)", text2, text);
	}

	private void Unpair()
	{
		AppSettings appSettings = _app.Services.Settings.Load();
		appSettings.Assistant.Telegram.OwnerId = null;
		appSettings.Assistant.Telegram.OwnerName = null;
		_app.Services.Settings.Save(appSettings);
		_tgCode.Text = "";
		UpdateTelegram();
	}

	private void UpdateTelegram()
	{
		TelegramSettings telegram = _app.Services.Settings.Current.Assistant.Telegram;
		_tgStatus.Text = Loc.T("Статус: ") + _app.Telegram.Status;
		_token.ToolTip = (string.IsNullOrEmpty(telegram.TokenProtected) ? Loc.T("Токен не задан") : Loc.T("Токен сохранён (введите новый, чтобы заменить)"));
		TextBlock tgOwner = _tgOwner;
		long? ownerId = telegram.OwnerId;
		string text;
		if (ownerId.HasValue)
		{
			long valueOrDefault = ownerId.GetValueOrDefault();
			text = Loc.F("Владелец: {0} (id {1})", telegram.OwnerName ?? "?", valueOrDefault);
		}
		else
		{
			text = Loc.T("Владелец не привязан");
		}
		tgOwner.Text = text;
		if (telegram.OwnerId.HasValue && _app.Telegram.PairingCode == null)
		{
			_tgCode.Text = "";
		}
	}

	private void PlayPreview(MascotClip clip)
	{
		_anim.Play(clip, restart: true);
		double totalSeconds = _clock.Elapsed.TotalSeconds;
		bool flag = (uint)(clip - 4) <= 1u;
		_clipUntil = totalSeconds + (flag ? 6.0 : 2.6);
	}

	private void Frame()
	{
		double totalSeconds = _clock.Elapsed.TotalSeconds;
		double dt = Math.Clamp(totalSeconds - _last, 0.0, 0.1);
		_last = totalSeconds;
		if (_anim.Clip != 0 && totalSeconds > _clipUntil)
		{
			_anim.Play(MascotClip.Idle);
		}
		Point position = Mouse.GetPosition(_preview);
		_anim.LookX = Math.Clamp((position.X - 115.0) / 200.0, -1.0, 1.0);
		_anim.LookY = Math.Clamp((position.Y - 80.0) / 200.0, -1.0, 1.0);
		_anim.Bass = 0.5 + 0.4 * Math.Max(0.0, Math.Sin(totalSeconds * 7.5));
		MascotSettings mascot = _app.Services.Settings.Current.Mascot;
		MascotColors c = MascotColors.From(new MascotSettings
		{
			ThemeColors = _themeColors.IsChecked.GetValueOrDefault(),
			Hair = (IsHex(_hair.Text) ? _hair.Text.Trim() : mascot.Hair),
			Eyes = (IsHex(_eyes.Text) ? _eyes.Text.Trim() : mascot.Eyes),
			Outfit = (IsHex(_outfit.Text) ? _outfit.Text.Trim() : mascot.Outfit)
		}, _app.Session.Active?.Palette);
		MascotPose p = _anim.Update(dt);
		_preview.SetShadow(new Point(115.0, 232.0), 80.0, 1.0);
		_preview.SetItems(MascotRig.Render(p, c, Affine.Scale(1.05, 1.05).Then(Affine.Translate(115.0, 232.0))));
	}
}
