using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Metamorph.Core.Launcher;

namespace Metamorph.Core.Assistant;

public static class IntentParser
{
	private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

	private static readonly Regex Spaces = new Regex("\\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Punct = new Regex("(?<!\\d)[.,](?!\\d)|[!?;:«»\"“”„\\[\\]…]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Address = new Regex("^(?:(?:эй|слушай|ну|а|hey|ok|окей|ок|so)\\s+)*(?:мета(?:[\\s-]?тян|чка)?|meta(?:[\\s-]?chan)?)(?:\\s+|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Please = new Regex("(?:^|\\s)(?:пожалуйста|плиз|please|pls|будь добра|можешь|можно|could you|can you)(?=\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly (Regex Re, Func<Match, string, Intent?> Make)[] Rules = Build();

	private static readonly string[] Latin = new string[32]
	{
		"a", "b", "v", "g", "d", "e", "zh", "z", "i", "y",
		"k", "l", "m", "n", "o", "p", "r", "s", "t", "u",
		"f", "kh", "ts", "ch", "sh", "sch", "", "y", "", "e",
		"yu", "ya"
	};

	private static readonly Dictionary<string, double> Numbers = new Dictionary<string, double>
	{
		["ноль"] = 0.0,
		["один"] = 1.0,
		["одну"] = 1.0,
		["одна"] = 1.0,
		["два"] = 2.0,
		["две"] = 2.0,
		["три"] = 3.0,
		["четыре"] = 4.0,
		["пять"] = 5.0,
		["шесть"] = 6.0,
		["семь"] = 7.0,
		["восемь"] = 8.0,
		["девять"] = 9.0,
		["десять"] = 10.0,
		["одиннадцать"] = 11.0,
		["двенадцать"] = 12.0,
		["тринадцать"] = 13.0,
		["четырнадцать"] = 14.0,
		["пятнадцать"] = 15.0,
		["шестнадцать"] = 16.0,
		["семнадцать"] = 17.0,
		["восемнадцать"] = 18.0,
		["девятнадцать"] = 19.0,
		["двадцать"] = 20.0,
		["тридцать"] = 30.0,
		["сорок"] = 40.0,
		["пятьдесят"] = 50.0,
		["шестьдесят"] = 60.0,
		["семьдесят"] = 70.0,
		["восемьдесят"] = 80.0,
		["девяносто"] = 90.0,
		["сто"] = 100.0,
		["полтора"] = 1.5,
		["полторы"] = 1.5,
		["пару"] = 2.0,
		["пара"] = 2.0,
		["one"] = 1.0,
		["a"] = 1.0,
		["an"] = 1.0,
		["two"] = 2.0,
		["three"] = 3.0,
		["four"] = 4.0,
		["five"] = 5.0,
		["six"] = 6.0,
		["seven"] = 7.0,
		["eight"] = 8.0,
		["nine"] = 9.0,
		["ten"] = 10.0,
		["eleven"] = 11.0,
		["twelve"] = 12.0,
		["fifteen"] = 15.0,
		["twenty"] = 20.0,
		["thirty"] = 30.0,
		["forty"] = 40.0,
		["fifty"] = 50.0,
		["sixty"] = 60.0,
		["couple"] = 2.0
	};

	private static readonly Regex DurationPart = new Regex("(?<num>\\d+(?:[.,]\\d+)?|(?:(?:[а-я]+|[a-z]+)\\s+){0,2}?)\\s*(?:of\\s+)?(?<unit>секунд[уы]?|сек|с|second|seconds|sec|secs|s|минут[уы]?|мин|м|minute|minutes|mins?|m|час(?:а|ов)?|ч|hour|hours|hrs?|h)(?![а-яa-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static Intent Parse(string? input)
	{
		string text = Normalize(input);
		if (text.Length == 0)
		{
			return new Intent(IntentKind.Unknown);
		}
		(Regex, Func<Match, string, Intent>)[] rules = Rules;
		for (int i = 0; i < rules.Length; i++)
		{
			(Regex, Func<Match, string, Intent>) tuple = rules[i];
			Regex item = tuple.Item1;
			Func<Match, string, Intent> item2 = tuple.Item2;
			Match match = item.Match(text);
			if (match.Success)
			{
				Intent intent = item2(match, text);
				if ((object)intent != null)
				{
					return intent;
				}
			}
		}
		string text2 = Regex.Replace(text, "^(?:сколько будет|посчитай|вычисли|calculate|calc|what is|what's)\\s+", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Replace('х', '*').Replace('×', '*');
		if (Calculator.LooksLikeExpression(text2) && Calculator.TryEvaluate(text2, out var _))
		{
			return new Intent(IntentKind.Calc, text2);
		}
		return new Intent(IntentKind.Unknown, text);
	}

	public static string ToLatin(string? input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(input.Length + 4);
		for (int i = 0; i < input.Length; i++)
		{
			char c = char.ToLowerInvariant(input[i]);
			switch (c)
			{
			case 'ё':
				stringBuilder.Append("e");
				break;
			case 'а':
			case 'б':
			case 'в':
			case 'г':
			case 'д':
			case 'е':
			case 'ж':
			case 'з':
			case 'и':
			case 'й':
			case 'к':
			case 'л':
			case 'м':
			case 'н':
			case 'о':
			case 'п':
			case 'р':
			case 'с':
			case 'т':
			case 'у':
			case 'ф':
			case 'х':
			case 'ц':
			case 'ч':
			case 'ш':
			case 'щ':
			case 'ъ':
			case 'ы':
			case 'ь':
			case 'э':
			case 'ю':
			case 'я':
				stringBuilder.Append(Latin[c - 1072]);
				break;
			default:
				stringBuilder.Append(c);
				break;
			}
		}
		return stringBuilder.ToString();
	}

	public static string Normalize(string? input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return "";
		}
		string text = input.Trim().ToLowerInvariant().Replace('ё', 'е');
		if (text.StartsWith('/'))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1).Replace('_', ' ');
		}
		text = Punct.Replace(text, " ");
		text = Spaces.Replace(text, " ").Trim();
		for (int i = 0; i < 2; i++)
		{
			text = Address.Replace(text, "").Trim();
		}
		return Spaces.Replace(Please.Replace(text, " "), " ").Trim();
	}

	public static TimeSpan? ParseDuration(string? text)
	{
		string text2 = Normalize(text);
		if (text2.Length == 0)
		{
			return null;
		}
		double num = 0.0;
		bool flag = false;
		if (Regex.IsMatch(text2, "(?:^|\\s)(?:пол\\s?часа|полчаса|half an hour|half hour)(?:\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			num += 1800.0;
			flag = true;
			text2 = Regex.Replace(text2, "пол\\s?часа|полчаса|half an hour|half hour", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}
		if (Regex.IsMatch(text2, "(?:^|\\s)(?:пол\\s?минуты|полминуты|half a minute)(?:\\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			num += 30.0;
			flag = true;
			text2 = Regex.Replace(text2, "пол\\s?минуты|полминуты|half a minute", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}
		foreach (Match item in DurationPart.Matches(text2))
		{
			string value = item.Groups["unit"].Value;
			string text3 = item.Groups["num"].Value.Trim();
			if (value.Length == 1 && (text3.Length == 0 || !char.IsDigit(text3[0])))
			{
				continue;
			}
			double num2;
			if (text3.Length == 0)
			{
				num2 = 1.0;
			}
			else if (char.IsDigit(text3[0]))
			{
				num2 = double.Parse(text3.Replace(',', '.'), CultureInfo.InvariantCulture);
			}
			else
			{
				List<string> list = text3.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(Numbers.ContainsKey).ToList();
				num2 = ((list.Count == 0) ? 1.0 : list.Sum((string w) => Numbers[w]));
			}
			int num3;
			switch (value[0])
			{
			case 's':
			case 'с':
				num3 = 1;
				break;
			case 'm':
			case 'м':
				num3 = 60;
				break;
			default:
				num3 = 3600;
				break;
			}
			int num4 = num3;
			num += num2 * (double)num4;
			flag = true;
		}
		if (!flag || num <= 0.0)
		{
			return null;
		}
		return TimeSpan.FromSeconds(Math.Min(num, 86400.0));
	}

	private static (Regex, Func<Match, string, Intent?>)[] Build()
	{
		List<(Regex, Func<Match, string, Intent?>)> list = new List<(Regex, Func<Match, string, Intent>)>();
		Simple("(?:отмени|останови|выключи|сбрось|убери|стоп|cancel|stop|clear)\\s+(?:мой\\s+|the\\s+)?(?:таймер|помодоро|timer|pomodoro)", IntentKind.CancelTimer);
		Simple("(?:сколько\\s+(?:еще\\s+)?(?:времени\\s+)?осталось|сколько\\s+на\\s+таймере|how\\s+much\\s+time\\s+(?:is\\s+)?left|time\\s+left|timer\\s+status)", IntentKind.TimerStatus);
		Add("(?:помодоро|pomodoro|фокус(?:ировка)?|focus)(?<x>.*)", delegate(Match m, string _)
		{
			TimeSpan? duration3 = ParseDuration(Rest(m));
			return new Intent(IntentKind.Pomodoro, null, null, duration3);
		});
		Add("(?:таймер|timer|засеки|засечь)(?<x>.*)", delegate(Match m, string _)
		{
			TimeSpan? timeSpan2 = ParseDuration(Rest(m));
			if (timeSpan2.HasValue)
			{
				string? text2 = Label(Rest(m));
				TimeSpan? duration2 = timeSpan2;
				return new Intent(IntentKind.Timer, text2, null, duration2);
			}
			return (Intent?)null;
		});
		Add("(?:напомни|разбуди|remind me|wake me)(?<x>.*)", delegate(Match m, string _)
		{
			TimeSpan? timeSpan = ParseDuration(Rest(m));
			if (timeSpan.HasValue)
			{
				string? text = Label(Rest(m));
				TimeSpan? duration = timeSpan;
				return new Intent(IntentKind.Timer, text, null, duration);
			}
			return (Intent?)null;
		});
		Add("(?:^|\\s)(?:тетрис\\w*|tetris)(?:\\s|$)", (Match _, string _) => new Intent(IntentKind.PlayGame, "tetris"));
		Add("(?:^|\\s)(?:динозавр\\w*|дино|(?:мета[\\s-]?)?раннер\\w*|бегалк\\w*|dino(?:saur)?|(?:meta[\\s-]?)?runner)(?:\\s|$)", (Match _, string _) => new Intent(IntentKind.PlayGame, "dino"));
		Simple("(?:^|\\s)(?:(?:давай\\s+)?поигра(?:ем|ть|й)|сыграем|(?:хочу|давай)\\s+(?:по)?играть|мини[\\s-]?игр\\w*|let'?s\\s+play|play\\s+(?:a\\s+)?game|play\\s+with\\s+me)(?:\\s|$)|^(?:игры|игра|game|games)$", IntentKind.PlayGame);
		Simple("(?:^|\\s)(?:удиви меня|surprise me|случайн\\w+ тем\\w*|random theme|любую тему)", IntentKind.SurpriseTheme);
		Add("(?:смени|поменяй|переключи|change|switch)\\s+(?:тему|theme)\\s+(?:на|to)\\s+(?<x>.+)", (Match m, string _) => new Intent(IntentKind.ApplyTheme, Rest(m)));
		Simple("(?:^|\\s)(?:следующ\\w+|другую|друга\\w|смени|поменяй|next|change|another)\\s+(?:тем[уаы]|theme)(?:\\s|$)", IntentKind.NextTheme);
		Simple("(?:^|\\s)(?:день\\s*ночь|день\\s+или\\s+ночь|ночн\\w+ режим|дневн\\w+ режим|темн\\w+ тем\\w|светл\\w+ тем\\w|day\\s*night|dark mode|light mode|night mode)", IntentKind.DayNight);
		Add("(?:^|\\s)(?:включи(?:ть)?|поставь|поставить|примени(?:ть)?|выбери|сделай|apply|set|use|switch to)?\\s*(?:тем[уаы]|theme)\\s+(?<x>.+)", (Match m, string _) => new Intent(IntentKind.ApplyTheme, Rest(m)));
		Simple("(?:пауз\\w*|останови|заморозь|stop|pause|freeze)\\s+(?:живые\\s+)?(?:обо\\w+|wallpaper)|(?:обои|wallpaper)\\s+(?:на паузу|pause)", IntentKind.PauseWallpaper);
		Simple("(?:^|\\s)(?:следующ\\w+|другие|смени|поменяй|next|change|another)\\s+(?:обои|wallpaper)|^(?:обои|wallpaper)$", IntentKind.NextWallpaper);
		Add("(?:^|\\s)(?:включи|поставь|сделай|set|use|apply)?\\s*(?:живые\\s+)?(?:обои|wallpaper)\\s+(?<x>.+)", (Match m, string _) => new Intent(IntentKind.SetWallpaper, Rest(m)));
		Add("(?:громкость|volume|звук)\\s+(?:на\\s+|to\\s+|в\\s+)?(?<n>\\d{1,3})\\s*%?", (Match m, string _) => new Intent(IntentKind.VolumeSet, null, Math.Clamp(int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture), 0, 100)));
		Add("(?:сделай|поставь|set)\\s+(?:громкость|volume)\\s+(?:на\\s+|to\\s+)?(?<n>\\d{1,3})", (Match m, string _) => new Intent(IntentKind.VolumeSet, null, Math.Clamp(int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture), 0, 100)));
		Simple("(?:^|\\s)(?:выключи звук|без звука|заглуши|отключи звук|mute|silence)(?:\\s|$)", IntentKind.Mute);
		Simple("(?:^|\\s)(?:включи звук|верни звук|unmute)(?:\\s|$)", IntentKind.Unmute);
		Simple("(?:^|\\s)(?:громче|погромче|прибавь|louder|volume up|turn it up|turn up)(?:\\s|$)", IntentKind.VolumeUp);
		Simple("(?:^|\\s)(?:тише|потише|убавь|quieter|volume down|turn it down|turn down)(?:\\s|$)", IntentKind.VolumeDown);
		Simple("(?:^|\\s)(?:что (?:сейчас )?(?:играет|звучит|за песн\\w|за трек)|какая песня|what'?s playing|what song|what is playing|now playing)", IntentKind.WhatsPlaying);
		Simple("(?:^|\\s)(?:следующ\\w+ (?:трек|песн\\w|композици\\w)|дальше|переключи (?:трек|песню)|скипни|пропусти|next (?:track|song)|skip)(?:\\s|$)", IntentKind.NextTrack);
		Simple("(?:^|\\s)(?:предыдущ\\w+ (?:трек|песн\\w)|прошл\\w+ (?:трек|песн\\w)|назад|верни (?:трек|песню)|previous|prev(?:ious)? (?:track|song)|go back)(?:\\s|$)", IntentKind.PreviousTrack);
		Simple("(?:^|\\s)(?:пауз[ау]|на паузу|продолжи\\w*|pause|resume|play music|stop music|(?:включи|выключи|останови|пусти|поставь)(?:ть)? музыку)(?:\\s|$)|^(?:play|стоп|stop|играй)$", IntentKind.PlayPause);
		Simple("(?:^|\\s)(?:потанцуй|станцуй|танцуй|давай танцевать|dance)(?:\\s|$)", IntentKind.Dance);
		Simple("(?:^|\\s)(?:поспи|иди спать|спи|ложись спать|go to sleep|sleep|take a nap)(?:\\s|$)", IntentKind.Sleep);
		Simple("(?:^|\\s)(?:помаши|помахай|wave)(?:\\s|$)", IntentKind.Wave);
		Simple("(?:^|\\s)(?:спрячься|уйди|исчезни|скройся|hide yourself|go away|hide)(?:\\s|$)", IntentKind.HideMascot);
		Simple("(?:^|\\s)(?:покажи |открой )?(?:остров|island)(?:\\s|$)", IntentKind.ShowIsland);
		Simple("(?:^|\\s)(?:лаунчер|launcher)(?:\\s|$)", IntentKind.Launcher);
		Add("(?:^|\\s)(?:погода|weather)(?:\\s+(?:в|во|in)\\s+(?<x>.+))?", (Match m, string _) => new Intent(IntentKind.Weather, m.Groups["x"].Success ? Rest(m) : null));
		Add("^(?:найди|найти|поищи|поискать|загугли|погугли|search(?: for)?|google|look up)\\s+(?<x>.+)", (Match m, string _) => new Intent(IntentKind.WebSearch, Rest(m)));
		Add("^(?:открой|открыть|запусти|запустить|включи|включить|open|launch|start|run)\\s+(?<x>.+)", (Match m, string _) => new Intent(IntentKind.OpenApp, Rest(m)));
		Simple("(?:который час|сколько времени|сколько сейчас времени|what time|current time|^время$|^time$)", IntentKind.Time);
		Simple("(?:какое (?:сегодня )?число|какой (?:сегодня )?день|какая (?:сегодня )?дата|сегодняшн\\w+ дат\\w|^дата$|what'?s the date|what day|today'?s date|^date$)", IntentKind.Date);
		Simple("(?:^|\\s)(?:что (?:ты )?умеешь|что ты можешь|помощь|команды|help|what can you do|commands)(?:\\s|$)", IntentKind.Help);
		Simple("(?:^|\\s)(?:статус|status|как там компьютер|что происходит)(?:\\s|$)", IntentKind.Status);
		Simple("(?:^|\\s)(?:шутк|анекдот|пошути|рассмеши|joke|make me laugh|something funny)", IntentKind.Joke);
		Simple("(?:^|\\s)(?:кто ты|ты кто|как тебя зовут|твое имя|who are you|what'?s your name|your name)", IntentKind.WhoAreYou);
		Simple("(?:^|\\s)(?:как дела|как ты|как поживаешь|как жизнь|how are you|how'?s it going|how are things)", IntentKind.HowAreYou);
		Simple("(?:^|\\s)(?:спасибо|спс|благодарю|thanks|thank you|thx|ty)(?:\\s|$)", IntentKind.Thanks);
		Simple("^(?:привет\\w*|здравствуй\\w*|хай|хеллоу|салют|приветик|доброе утро|добрый день|добрый вечер|доброй ночи|hi|hello|hey|yo|good morning|good evening|start)(?:\\s|$)", IntentKind.Greeting);
		return list.ToArray();
		void Add(string pattern, Func<Match, string, Intent?> make)
		{
			list.Add((new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), make));
		}
		static string Rest(Match m)
		{
			return m.Groups["x"].Value.Trim();
		}
		void Simple(string pattern, IntentKind kind)
		{
			Add(pattern, (Match _, string _) => new Intent(kind));
		}
	}

	private static string? Label(string rest)
	{
		string input = DurationPart.Replace(rest, " ");
		input = Regex.Replace(input, "(?:^|\\s)(?:на|через|in|for|after|пол\\s?часа|полчаса|полминуты|half an hour|чтобы|что|мне|me|to|про|о|об|about)(?=\\s|$)", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		input = Spaces.Replace(input, " ").Trim();
		int length = input.Length;
		if (length <= 1 || length >= 80)
		{
			return null;
		}
		return input;
	}
}
