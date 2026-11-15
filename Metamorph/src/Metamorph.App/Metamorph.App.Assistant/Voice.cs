using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;
using Metamorph.Core.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;

namespace Metamorph.App.Assistant;

internal sealed class Voice : IDisposable
{
	private SpeechSynthesizer? _synth;

	private WasapiOut? _output;

	private WaveFileReader? _reader;

	private int _generation;

	public event Action<bool>? SpeakingChanged;

	public static IReadOnlyList<(string Name, string Language)> Voices()
	{
		try
		{
			return SpeechSynthesizer.AllVoices.Select((VoiceInformation v) => (DisplayName: v.DisplayName, Language: v.Language)).ToList();
		}
		catch (Exception ex)
		{
			Log.Error("Голоса Windows недоступны", ex);
			return Array.Empty<(string, string)>();
		}
	}

	public async Task SpeakAsync(string text, AssistantSettings settings)
	{
		AssistantSettings settings2 = settings;
		Stop();
		string text2 = Clean(text);
		if (text2.Length == 0)
		{
			return;
		}
		int generation = ++_generation;
		try
		{
			if ((object)_synth == null)
			{
				_synth = new SpeechSynthesizer();
			}
			IReadOnlyList<VoiceInformation> allVoices = SpeechSynthesizer.AllVoices;
			string lang = (Loc.IsEnglish ? "en" : "ru");
			VoiceInformation voiceInformation = allVoices.FirstOrDefault((VoiceInformation v) => v.DisplayName.Equals(settings2.Voice, StringComparison.OrdinalIgnoreCase)) ?? (from v in allVoices
				where v.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase)
				orderby v.Gender == VoiceGender.Female descending
				select v).FirstOrDefault() ?? SpeechSynthesizer.DefaultVoice;
			if ((object)voiceInformation != null)
			{
				_synth.Voice = voiceInformation;
			}
			_synth.Options.AudioPitch = Math.Clamp(1.0 + (double)settings2.VoicePitch / 100.0, 0.0, 2.0);
			_synth.Options.SpeakingRate = Math.Clamp(settings2.VoiceRate, 0.5, 3.0);
			using SpeechSynthesisStream stream = await _synth.SynthesizeTextToStreamAsync(text2);
			MemoryStream ms = new MemoryStream();
			await stream.AsStreamForRead().CopyToAsync(ms);
			ms.Position = 0L;
			if (generation != _generation)
			{
				return;
			}
			_reader = new WaveFileReader(ms);
			_output = new WasapiOut(AudioClientShareMode.Shared, 120);
			WaveFileReader reader = _reader;
			WasapiOut output = _output;
			output.PlaybackStopped += delegate
			{
				Application.Current?.Dispatcher.InvokeAsync(delegate
				{
					reader.Dispose();
					output.Dispose();
					if (_output == output)
					{
						_output = null;
						_reader = null;
						this.SpeakingChanged?.Invoke(obj: false);
					}
				});
			};
			output.Init(reader);
			output.Play();
			this.SpeakingChanged?.Invoke(obj: true);
		}
		catch (Exception ex)
		{
			Log.Error("Синтез речи", ex);
			this.SpeakingChanged?.Invoke(obj: false);
		}
	}

	public void Stop()
	{
		_generation++;
		WasapiOut output = _output;
		if (output == null)
		{
			return;
		}
		try
		{
			output.Stop();
		}
		catch
		{
		}
	}

	public static string Clean(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (char.IsSurrogate(c))
			{
				continue;
			}
			UnicodeCategory unicodeCategory = char.GetUnicodeCategory(c);
			bool flag = ((unicodeCategory == UnicodeCategory.NonSpacingMark || (uint)(unicodeCategory - 27) <= 1u) ? true : false);
			if (!flag || c == '\u0301')
			{
				switch (c)
				{
				case '#':
				case '*':
				case '_':
				case '`':
				case '~':
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					stringBuilder.Append(c);
				}
			}
		}
		return stringBuilder.ToString().Replace("…", ".").Trim();
	}

	public static async Task<(string? Text, string? Error)> ListenOnceAsync()
	{
		_ = 1;
		try
		{
			string tag = (Loc.IsEnglish ? "en-US" : "ru-RU");
			IReadOnlyList<Language> supportedTopicLanguages = SpeechRecognizer.SupportedTopicLanguages;
			Language language = supportedTopicLanguages.FirstOrDefault((Language l) => l.LanguageTag.Equals(tag, StringComparison.OrdinalIgnoreCase)) ?? supportedTopicLanguages.FirstOrDefault((Language l) => l.LanguageTag.StartsWith(tag.Substring(0, 2), StringComparison.OrdinalIgnoreCase));
			if ((object)language == null)
			{
				return (Text: null, Error: Loc.F("Нет распознавания речи для языка {0}. Установите языковой пакет с «Распознаванием речи» в Параметрах Windows.", tag));
			}
			using SpeechRecognizer recognizer = new SpeechRecognizer(language);
			recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));
			SpeechRecognitionCompilationResult speechRecognitionCompilationResult = await recognizer.CompileConstraintsAsync();
			if (speechRecognitionCompilationResult.Status != 0)
			{
				return (Text: null, Error: Loc.F("Распознавание недоступно: {0}", speechRecognitionCompilationResult.Status));
			}
			recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(6.0);
			recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
			SpeechRecognitionResult speechRecognitionResult = await recognizer.RecognizeAsync();
			if (speechRecognitionResult.Status != 0)
			{
				return (Text: null, Error: Loc.F("Не расслышала: {0}", speechRecognitionResult.Status));
			}
			return ((string? Text, string? Error))(string.IsNullOrWhiteSpace(speechRecognitionResult.Text) ? (Text: null, Error: Loc.T("Не расслышала. Попробуй ещё раз!")) : (Text: speechRecognitionResult.Text, Error: null));
		}
		catch (Exception ex) when (ex.HResult == -2147199735)
		{
			return (Text: null, Error: Loc.T("Включите «Распознавание голоса в сети»: Параметры → Конфиденциальность → Голосовые функции."));
		}
		catch (UnauthorizedAccessException)
		{
			return (Text: null, Error: Loc.T("Нет доступа к микрофону: Параметры → Конфиденциальность → Микрофон → разрешить классическим приложениям."));
		}
		catch (Exception ex3)
		{
			Log.Error("Распознавание речи", ex3);
			return (Text: null, Error: Loc.F("Распознавание не сработало: {0}", ex3.Message));
		}
	}

	public void Dispose()
	{
		Stop();
		_synth?.Dispose();
		_synth = null;
	}
}
