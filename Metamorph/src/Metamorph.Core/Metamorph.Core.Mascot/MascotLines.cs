using System;
using Metamorph.Core.Localization;

namespace Metamorph.Core.Mascot;

public static class MascotLines
{
	public static readonly string[] Morning = new string[3] { "Доброе утро! ☀\ufe0f", "Утречко! Кофе уже пьём?", "Проснулась? Я — да!" };

	public static readonly string[] Day = new string[3] { "Привет-привет!", "Я тут! Чем займёмся?", "Ура, ты вернулся!" };

	public static readonly string[] Evening = new string[3] { "Добрый вечер~", "Вечер — время красивых тем!", "Привет! Как прошёл день?" };

	public static readonly string[] Night = new string[3] { "Ночью тоже работаем? Я с тобой.", "Тс-с… все спят, а мы нет.", "Доброй ночи… или не очень?" };

	public static readonly string[] Poke = new string[6] { "Эй!", "Ой!", "Хи-хи, щекотно!", "М?", "Я тут-тут!", "Чего такое?" };

	public static readonly string[] PokeMany = new string[4] { "Ну хватит тыкать!", "Я сейчас обижусь!", "Хмф!", "У меня не кнопка, а лицо!" };

	public static readonly string[] Pet = new string[4] { "Хе-хе~", "Ня~", "Ещё погладь!", "Мурр…" };

	public static readonly string[] Held = new string[4] { "Куда мы летим?!", "Поставь меня!", "Высоко-о-о!", "Ай-ай-ай!" };

	public static readonly string[] Thrown = new string[4] { "Голова кружится…", "Я что, мячик?!", "Ой-ой… звёздочки…", "Ещё раз! …Нет, не надо." };

	public static readonly string[] Landed = new string[3] { "Оп!", "Приземлилась!", "Цела!" };

	public static readonly string[] Idle = new string[13]
	{
		"Красивый сегодня рабочий стол.", "Может, сменим тему? Нажми Ctrl+Alt+T!", "Alt+Space — лаунчер, им быстро всё открывать.", "Не забывай пить воду!", "Разомнись немножко, а?", "Я тут посижу, ладно?", "Мне нравится, как ты всё настроил.", "Ctrl+Alt+Space — и остров у тебя.", "Хочешь новые обои? Загляни во вкладку «Обои».", "Ля-ля-ля~",
		"Интересно, что там в браузере…", "Если что — я рядом. Кликни меня!", "Глазки устали? Посмотри вдаль 20 секунд."
	};

	public static readonly string[] Sleepy = new string[3] { "Хр-р…", "Ещё пять минуточек…", "Мне снятся обои…" };

	public static readonly string[] WakeUp = new string[4] { "А? Я не сплю!", "Уже проснулась!", "Ой, ты тут! Привет!", "Я просто глаза закрыла…" };

	public static readonly string[] LateNight = new string[3] { "Уже очень поздно… Может, спать?", "Глаза не устали? Пора отдыхать.", "Завтра будет новый день, честно." };

	public static readonly string[] MusicStart = new string[3] { "Музыка! Потанцуем?", "О, это мне нравится!", "Включай погромче!" };

	public static readonly string[] Wallpaper = new string[3] { "Новые обои! Красота~", "Мне нравится этот фон!", "Свежо!" };

	public static readonly string[] Dance = new string[3] { "Танцуем!", "Смотри, как умею!", "Раз-два, раз-два!" };

	public static readonly string[] Bye = new string[2] { "Пока-пока!", "Я спрячусь, позови, если что." };

	public const string Track = "О, «{0}»! ♪";

	public const string ThemeApplied = "Тема «{0}» — мне идёт?";

	public const string Thinking = "Хм-м… думаю…";

	public static string Pick(string[] pool, Random rnd)
	{
		return Loc.T(pool[rnd.Next(pool.Length)]);
	}

	public static string Greeting(int hour, Random rnd)
	{
		string[] pool;
		switch (hour)
		{
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
			pool = Morning;
			break;
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
			pool = Day;
			break;
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
			pool = Evening;
			break;
		default:
			pool = Night;
			break;
		}
		return Pick(pool, rnd);
	}
}
