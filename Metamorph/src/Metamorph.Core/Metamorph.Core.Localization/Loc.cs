using System;
using System.Globalization;

namespace Metamorph.Core.Localization;

public static class Loc
{
	public static string Language { get; private set; } = Detect();


	public static bool IsEnglish => Language == "en";

	public static CultureInfo Culture
	{
		get
		{
			if (!(CultureInfo.CurrentCulture.TwoLetterISOLanguageName == Language))
			{
				return CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "ru-RU");
			}
			return CultureInfo.CurrentCulture;
		}
	}

	public static event EventHandler? LanguageChanged;

	public static void SetLanguage(string? setting)
	{
		string text = setting?.Trim().ToLowerInvariant();
		string text2 = ((text == "ru") ? "ru" : ((!(text == "en")) ? Detect() : "en"));
		string text3 = text2;
		if (!(text3 == Language))
		{
			Language = text3;
			Loc.LanguageChanged?.Invoke(null, EventArgs.Empty);
		}
	}

	private static string Detect()
	{
		bool flag;
		switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
		{
		case "ru":
		case "uk":
		case "be":
		case "kk":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "en";
		}
		return "ru";
	}

	public static string T(string ru)
	{
		if (!IsEnglish || !En.Map.TryGetValue(ru, out string value))
		{
			return ru;
		}
		return value;
	}

	// Переводы новых модулей: каждый модуль регистрирует свои пары «рус → англ» при первом использовании.
	public static void AddEnglish(params (string Ru, string En)[] pairs)
	{
		lock (En.Map)
		{
			foreach (var (ru, en) in pairs)
			{
				En.Map[ru] = en;
			}
		}
	}

	public static string F(string ru, params object?[] args)
	{
		return string.Format(CultureInfo.CurrentCulture, T(ru), args);
	}
}
