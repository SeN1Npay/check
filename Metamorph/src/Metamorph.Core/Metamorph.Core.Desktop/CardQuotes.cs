using System;
using System.Collections.Generic;

namespace Metamorph.Core.Desktop;

public sealed record CardQuote(string Ru, string En, string AuthorRu, string AuthorEn)
{
	public string Text(bool english) => english ? En : Ru;

	public string Author(bool english) => english ? AuthorEn : AuthorRu;
}

// Цитата дня для карточки «quote»: встроенный список, смена раз в сутки.
public static class CardQuotes
{
	public static readonly IReadOnlyList<CardQuote> All = new CardQuote[]
	{
		new("Простота — высшая степень изощрённости.", "Simplicity is the ultimate sophistication.", "Леонардо да Винчи", "Leonardo da Vinci"),
		new("Лучший способ предсказать будущее — изобрести его.", "The best way to predict the future is to invent it.", "Алан Кэй", "Alan Kay"),
		new("Сначала решите задачу. Потом пишите код.", "First, solve the problem. Then, write the code.", "Джон Джонсон", "John Johnson"),
		new("Делай, что можешь, с тем, что имеешь, там, где ты есть.", "Do what you can, with what you have, where you are.", "Теодор Рузвельт", "Theodore Roosevelt"),
		new("Путь в тысячу ли начинается с первого шага.", "A journey of a thousand miles begins with a single step.", "Лао-цзы", "Lao Tzu"),
		new("Слова ничего не стоят. Покажите мне код.", "Talk is cheap. Show me the code.", "Линус Торвальдс", "Linus Torvalds"),
		new("Оставайтесь голодными. Оставайтесь безрассудными.", "Stay hungry. Stay foolish.", "Стив Джобс", "Steve Jobs"),
		new("Дизайн — это то, как вещь работает.", "Design is how it works.", "Стив Джобс", "Steve Jobs"),
		new("Единственный способ делать великую работу — любить то, что делаешь.", "The only way to do great work is to love what you do.", "Стив Джобс", "Steve Jobs"),
		new("Жизнь — это то, что происходит, пока ты строишь планы.", "Life is what happens while you are busy making other plans.", "Джон Леннон", "John Lennon"),
		new("Воображение важнее знания.", "Imagination is more important than knowledge.", "Альберт Эйнштейн", "Albert Einstein"),
		new("Красота спасёт мир.", "Beauty will save the world.", "Фёдор Достоевский", "Fyodor Dostoevsky"),
		new("Краткость — сестра таланта.", "Brevity is the sister of talent.", "Антон Чехов", "Anton Chekhov"),
		new("Мы в ответе за тех, кого приручили.", "You become responsible forever for what you have tamed.", "Антуан де Сент-Экзюпери", "Antoine de Saint-Exupéry"),
		new("Зорко одно лишь сердце.", "One sees clearly only with the heart.", "Антуан де Сент-Экзюпери", "Antoine de Saint-Exupéry"),
		new("Не ошибается тот, кто ничего не делает.", "He who does nothing makes no mistakes.", "Пословица", "Proverb"),
		new("Тише едешь — дальше будешь.", "Slow and steady wins the race.", "Пословица", "Proverb"),
		new("Утро вечера мудренее.", "The morning is wiser than the evening.", "Пословица", "Proverb"),
		new("Терпение и труд всё перетрут.", "Patience and work overcome everything.", "Пословица", "Proverb"),
		new("Познай самого себя.", "Know thyself.", "Дельфийская максима", "Delphic maxim"),
		new("Мыслю, следовательно, существую.", "I think, therefore I am.", "Рене Декарт", "René Descartes"),
		new("Что не убивает меня, делает меня сильнее.", "What does not kill me makes me stronger.", "Фридрих Ницше", "Friedrich Nietzsche"),
		new("Будь собой — прочие роли уже заняты.", "Be yourself; everyone else is already taken.", "Оскар Уайльд", "Oscar Wilde"),
		new("Неудача — это возможность начать снова, но уже мудрее.", "Failure is simply the opportunity to begin again, this time more intelligently.", "Генри Форд", "Henry Ford"),
		new("И вечный бой! Покой нам только снится.", "And eternal battle! We only dream of rest.", "Александр Блок", "Alexander Blok"),
		new("Рукописи не горят.", "Manuscripts don't burn.", "Михаил Булгаков", "Mikhail Bulgakov"),
		new("Меньше — значит больше.", "Less is more.", "Людвиг Мис ван дер Роэ", "Ludwig Mies van der Rohe"),
		new("Хорошо сделанное лучше хорошо сказанного.", "Well done is better than well said.", "Бенджамин Франклин", "Benjamin Franklin"),
		new("Что хорошо продумано, то ясно излагается.", "What is well conceived is clearly said.", "Никола Буало", "Nicolas Boileau"),
		new("Программы пишут для людей, а машины лишь исполняют их.", "Programs must be written for people to read, and only incidentally for machines to execute.", "Харольд Абельсон", "Harold Abelson"),
		new("Преждевременная оптимизация — корень всех зол.", "Premature optimization is the root of all evil.", "Дональд Кнут", "Donald Knuth"),
		new("В информатике две трудные вещи: инвалидация кеша и придумывание имён.", "There are only two hard things in computer science: cache invalidation and naming things.", "Фил Карлтон", "Phil Karlton"),
		new("Делай что должно, и будь что будет.", "Do what you must, come what may.", "Марк Аврелий", "Marcus Aurelius"),
		new("Все счастливые семьи похожи друг на друга.", "All happy families are alike.", "Лев Толстой", "Leo Tolstoy"),
		new("Мы — то, что мы постоянно делаем.", "We are what we repeatedly do.", "Уилл Дюрант", "Will Durant"),
		new("Вдохновение существует, но оно должно застать тебя за работой.", "Inspiration exists, but it has to find you working.", "Пабло Пикассо", "Pablo Picasso"),
		new("Время — мудрейший из советников.", "Time is the wisest counsellor of all.", "Перикл", "Pericles"),
		new("Счастье — не что-то готовое. Оно рождается из ваших поступков.", "Happiness is not something ready made. It comes from your own actions.", "Далай-лама XIV", "Dalai Lama XIV"),
		new("Ничего не бойся — только понимай.", "Nothing in life is to be feared, it is only to be understood.", "Мария Кюри", "Marie Curie"),
		new("Тот, кто перемещает гору, начинает с маленьких камней.", "The man who moves a mountain begins by carrying away small stones.", "Конфуций", "Confucius"),
		new("Всякое начало трудно.", "All beginnings are difficult.", "Пословица", "Proverb"),
		new("Лучшее — враг хорошего.", "The best is the enemy of the good.", "Вольтер", "Voltaire")
	};

	public static int IndexFor(DateTime date)
	{
		return DateOnly.FromDateTime(date).DayNumber % All.Count;
	}

	public static CardQuote ForDate(DateTime date)
	{
		return All[IndexFor(date)];
	}
}
