using System;
using System.IO;
using Metamorph.Core.Games;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Games;

// Игры с Метой. Точка входа для трея, горячей клавиши, меню Меты и помощника — сигнатуру не менять.
internal static class GameHub
{
	public const string Dino = "dino";

	public const string Tetris = "tetris";

	public const string Platformer = "platformer";

	public const string PlatformerEndless = "platformer-endless";

	private static GameWindow? _window;

	private static GameScores? _scores;

	private static PlatformerProgress? _progress;

	static GameHub()
	{
		Loc.AddEnglish(("🎮 Поиграем", "🎮 Let's play"), ("Мета-раннер", "Meta Runner"), ("Тетрис", "Tetris"), ("Все игры…", "All games…"), ("Игры с Метой", "Games with Meta"), ("Во что поиграем?", "What shall we play?"), ("Мета-тян уже размялась и ждёт тебя", "Meta-chan has warmed up and is waiting for you"), ("Беги, прыгай и пригибайся! Кристаллы, тыквы, летучие мыши — и день сменяет ночь.", "Run, jump and duck! Crystals, pumpkins, bats — and day turns into night."), ("Классика с SRS, запасом и T-спинами. Мета болеет за тебя рядом со стаканом.", "The classic with SRS, hold and T-spins. Meta cheers for you next to the well."), ("Играть", "Play"), ("Рекорд: {0}", "Best: {0}"), ("Рекорда пока нет", "No record yet"), ("← → — выбрать · Enter — играть · Esc — закрыть", "← → — choose · Enter — play · Esc — close"), ("Новый рекорд", "New record"), ("Новый рекорд!", "New record!"), ("Пауза", "Paused"), ("P / Пробел — продолжить · Esc — в меню", "P / Space — resume · Esc — menu"), ("Пробел / R — ещё раз · Esc — в меню", "Space / R — again · Esc — menu"), ("R — заново · Esc — в меню", "R — restart · Esc — menu"), ("Игра окончена", "Game over"), ("Счёт: {0}", "Score: {0}"), ("Пробел или ↑ — старт", "Space or ↑ — start"), ("Пробел/↑ — прыжок (держи — выше) · ↓ — пригнуться", "Space/↑ — jump (hold for higher) · ↓ — duck"), ("Счёт", "Score"), ("Уровень", "Level"), ("Линии", "Lines"), ("Рекорд", "Best"), ("Запас", "Hold"), ("Далее", "Next"), ("Уровень {0}!", "Level {0}!"), ("ТЕТРИС!", "TETRIS!"), ("Двойная", "Double"), ("Тройная", "Triple"), ("Одинарная", "Single"), ("T-спин", "T-spin"), ("T-спин мини", "T-spin mini"), ("Комбо ×{0}", "Combo ×{0}"), ("Подряд!", "Back-to-back!"), ("←/→ ход · ↑/X поворот · Z влево · Пробел — сброс · C — запас", "←/→ move · ↑/X rotate · Z ccw · Space drop · C hold"), ("ТЕТРИС! Ты супер!", "TETRIS! You're amazing!"), ("Четыре линии! Вау!", "Four lines! Wow!"), ("Вот это да!", "Whoa!"), ("Т-спин?! Да ты профи!", "A T-spin?! You're a pro!"), ("Уровень {0}! Танцуем!", "Level {0}! Let's dance!"), ("Ой-ой, высоко!", "Uh-oh, that's high!"), ("Осторожнее, стакан почти полон!", "Careful, it's almost full!"), ("Голова кругом… Ещё разок?", "My head is spinning… Again?"), ("Ну вот… Давай ещё!", "Oh no… Let's go again!"), ("Ай! Не заметила…", "Ouch! Didn't see that…"), ("Кристаллы коварны!", "Crystals are sneaky!"), ("Ой-ой… звёздочки…", "Ow… I see stars…"), ("Фух, чуть-чуть!", "Phew, close one!"), ("Новый рекорд! Ура-а!", "New record! Yay!"), ("В меню", "Menu"), ("Закрыть", "Close"));
		Loc.AddEnglish(("Мета-платформер", "Meta Quest"), ("Прыгай по блокам, топчи слаймиков и добегай до флага! Четыре мира и бесконечный режим.", "Hop on blocks, stomp slimes and reach the flag! Four worlds and an endless mode."), ("Цветочный луг", "Flower Meadow"), ("Хрустальная пещера", "Crystal Cave"), ("Небесные острова", "Sky Islands"), ("Лавовый замок", "Lava Castle"), ("Бесконечный путь", "Endless Road"), ("Бесконечный режим", "Endless mode"), ("Мир {0}", "World {0}"), ("Мир {0} · {1}", "World {0} · {1}"), ("Выбери уровень", "Choose a level"), ("Закрыто", "Locked"), ("Лучший этап: {0}", "Best stage: {0}"), ("Уровень пройден!", "Level clear!"), ("Все миры пройдены!", "All worlds cleared!"), ("Бонус за время: {0}", "Time bonus: {0}"), ("Монеты: {0}", "Coins: {0}"), ("Время вышло!", "Time's up!"), ("Enter — дальше · Esc — в меню", "Enter — next · Esc — menu"), ("Enter / R — заново · Esc — в меню", "Enter / R — again · Esc — menu"), ("P / Пробел — продолжить · R — заново · Esc — в меню", "P / Space — resume · R — restart · Esc — menu"), ("←/→ — ход · Пробел — прыжок · Shift — бег · Ctrl — леденец · ↓ — присесть", "←/→ — move · Space — jump · Shift — run · Ctrl — candy · ↓ — duck"), ("← → — выбрать · Enter — играть · Esc — к играм", "← → — choose · Enter — play · Esc — back to games"), ("Сахарная звезда! Я выросла~", "Sugar Star! I grew~"), ("Огненный леденец! Ловите!", "Fire Lollipop! Catch!"), ("Ай-ай!", "Ouch!"), ("Ещё жизнь!", "Extra life!"), ("Чекпоинт!", "Checkpoint!"), ("Скорее, время!", "Hurry, time's running out!"), ("Получай, большой слайм!", "Take that, big slime!"), ("Победа! Ворота открыты!", "Victory! The gate is open!"), ("Флаг мой!", "The flag is mine!"), ("Пробел — начать", "Space — start"), ("Лучший счёт: {0}", "Best score: {0}"));
	}

	public static GameScores Scores => _scores ?? throw new InvalidOperationException("GameHub не инициализирован");

	public static PlatformerProgress Progress => _progress ?? throw new InvalidOperationException("GameHub не инициализирован");

	// Гарантирует регистрацию английских строк до первого Loc.T из модуля игр.
	public static void RegisterStrings()
	{
	}

	// game: null — окно выбора игры; "dino" | "tetris" | "platformer" — сразу игра.
	public static void Show(App app, string? game = null)
	{
		try
		{
			_scores ??= new GameScores(Path.Combine(app.Services.Paths.Root, "games.json"));
			_progress ??= new PlatformerProgress(Path.Combine(app.Services.Paths.Root, "platformer.json"));
			string? id = Normalize(game);
			if (_window == null)
			{
				_window = new GameWindow(app, new GamePalette(PaletteOf(app)));
				_window.Closed += delegate
				{
					_window = null;
				};
				_window.Navigate(id);
				_window.Show();
			}
			else
			{
				_window.Navigate(id);
				if (_window.WindowState == System.Windows.WindowState.Minimized)
				{
					_window.WindowState = System.Windows.WindowState.Normal;
				}
			}
			_window.Activate();
			_window.Focus();
		}
		catch (Exception ex)
		{
			Log.Error("Игры: не удалось открыть окно", ex);
		}
	}

	// Сохраняет результат; при новом рекорде — тост на «острове».
	public static bool Submit(App app, string game, int score)
	{
		try
		{
			bool record = Scores.Submit(game, score);
			if (record)
			{
				app.Session?.Announce("🏆", Loc.T("Новый рекорд"), score.ToString());
			}
			return record;
		}
		catch (Exception ex)
		{
			Log.Error("Игры: не удалось сохранить результат", ex);
			return false;
		}
	}

	public static Palette PaletteOf(App app)
	{
		try
		{
			return app.Session?.Active?.Palette ?? app.WallpaperBaseTheme.Palette ?? new Palette();
		}
		catch (Exception ex)
		{
			Log.Warn("Игры: палитра темы недоступна: " + ex.Message);
			return new Palette();
		}
	}

	private static string? Normalize(string? game)
	{
		string g = (game ?? "").Trim().ToLowerInvariant();
		if (g.Contains("tetris") || g.Contains("тетрис"))
		{
			return Tetris;
		}
		if (g.Contains("platform") || g.Contains("платформ") || g.Contains("mario") || g.Contains("марио") || g.Contains("quest"))
		{
			return Platformer;
		}
		if (g.Contains("dino") || g.Contains("run") || g.Contains("дино") || g.Contains("раннер"))
		{
			return Dino;
		}
		return null;
	}
}
