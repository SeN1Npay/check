using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Metamorph.Core.Input;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;

namespace Metamorph.App.Views;

internal sealed class SettingsView : UserControl
{
	private readonly App _app;

	private readonly Action<string, bool> _status;

	private AppSettings _settings;

	private readonly StackPanel _root = new StackPanel();

	private readonly TextBlock _updateStatus = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
	};

	private Button? _installUpdate;

	private static readonly Dictionary<string, string> ActionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["launcher"] = "Лаунчер",
		["toggleDock"] = "Показать/скрыть док",
		["toggleBar"] = "Показать/скрыть панель",
		["nextTheme"] = "Следующая тема",
		["dayNight"] = "День/ночь",
		["showMain"] = "Окно Metamorph",
		["snapLeft"] = "Окно: левая половина",
		["snapRight"] = "Окно: правая половина",
		["snapUp"] = "Окно: верхняя половина",
		["snapDown"] = "Окно: нижняя половина",
		["maximize"] = "Окно: развернуть с отступами",
		["center"] = "Окно: по центру",
		["tileAll"] = "Разложить все окна (мастер + стек)",
		["showIsland"] = "Показать остров",
		["nextWallpaper"] = "Следующие обои",
		["pauseWallpaper"] = "Пауза GIF/видео-обоев",
		["assistant"] = "Помощник Мета",
		["surpriseTheme"] = "Удиви меня (случайная тема)",
		["editDesktop"] = "Редактировать рабочий стол (карточки)",
		["games"] = "Игры с Метой"
	};

	public SettingsView(App app, Action<string, bool> status)
	{
		_app = app;
		_status = status;
		_settings = app.Services.Settings.Load();
		base.Content = new ScrollViewer
		{
			Content = Ui.Card(_root),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		_app.UpdateFound += delegate
		{
			base.Dispatcher.InvokeAsync(ShowUpdate);
		};
		Build();
	}

	private void Build()
	{
		_root.Children.Clear();
		AppSettings s = _settings;
		_root.Children.Add(Ui.Header("Язык интерфейса"));
		ComboBox language = new ComboBox
		{
			Width = 200.0,
			HorizontalAlignment = HorizontalAlignment.Left
		};
		language.Items.Add(Loc.T("Как в системе"));
		language.Items.Add("Русский");
		language.Items.Add("English");
		ComboBox comboBox = language;
		string language2 = s.Language;
		int selectedIndex = ((language2 == "ru") ? 1 : ((language2 == "en") ? 2 : 0));
		comboBox.SelectedIndex = selectedIndex;
		language.SelectionChanged += delegate
		{
			AppSettings appSettings = s;
			appSettings.Language = language.SelectedIndex switch
			{
				1 => "ru", 
				2 => "en", 
				_ => "auto", 
			};
		};
		_root.Children.Add(Ui.Row("Язык", language));
		_root.Children.Add(Ui.Header("Горячие клавиши"));
		_root.Children.Add(Ui.Hint("Формат: Ctrl+Alt+T, Win+Alt+Left, Alt+Space. Пустое поле — без сочетания. Оконные сочетания работают, если в теме включено windows.tiling.hotkeys."));
		foreach (string item in HotkeyActions.All)
		{
			string value;
			TextBox box = Ui.TextBox(s.Hotkeys.TryGetValue(item, out value) ? value : "", 220.0);
			string a = item;
			box.TextChanged += delegate
			{
				HotkeyGesture gesture;
				bool flag = box.Text.Trim().Length == 0 || HotkeyGesture.TryParse(box.Text, out gesture);
				box.BorderBrush = Ui.Res(flag ? "BorderBrush" : "DangerBrush");
				s.Hotkeys[a] = box.Text.Trim();
			};
			_root.Children.Add(Ui.Row(ActionNames.TryGetValue(item, out string value2) ? value2 : item, box));
		}
		if (_app.HotkeyFailures.Count > 0)
		{
			TextBlock textBlock = Ui.Hint(string.Join("\n", _app.HotkeyFailures));
			textBlock.Foreground = Ui.Res("DangerBrush");
			_root.Children.Add(textBlock);
		}
		_root.Children.Add(Ui.Header("Лаунчер"));
		CheckBox checkBox = new CheckBox
		{
			Content = Loc.T("Включить лаунчер (приложения, окна, темы, команды, калькулятор)"),
			IsChecked = s.Launcher.Enabled
		};
		checkBox.Checked += delegate
		{
			s.Launcher.Enabled = true;
		};
		checkBox.Unchecked += delegate
		{
			s.Launcher.Enabled = false;
		};
		_root.Children.Add(checkBox);
		TextBox web = Ui.TextBox(s.Launcher.WebSearch);
		web.TextChanged += delegate
		{
			s.Launcher.WebSearch = web.Text.Trim();
		};
		_root.Children.Add(Ui.Row("Веб-поиск ({0} — запрос)", web));
		_root.Children.Add(Ui.Header("Расписание день/ночь"));
		IReadOnlyList<ThemeProfile> themes = _app.Services.Profiles.LoadAll();
		CheckBox checkBox2 = new CheckBox
		{
			Content = Loc.T("Автоматически переключать дневную и ночную темы"),
			IsChecked = s.Schedule.Enabled
		};
		checkBox2.Checked += delegate
		{
			s.Schedule.Enabled = true;
		};
		checkBox2.Unchecked += delegate
		{
			s.Schedule.Enabled = false;
		};
		_root.Children.Add(checkBox2);
		_root.Children.Add(Ui.Row("Дневная тема", ThemeCombo(themes, s.Schedule.DayThemeId, delegate(string id)
		{
			s.Schedule.DayThemeId = id;
		})));
		_root.Children.Add(Ui.Row("Ночная тема", ThemeCombo(themes, s.Schedule.NightThemeId, delegate(string id)
		{
			s.Schedule.NightThemeId = id;
		})));
		_root.Children.Add(Ui.Row("Начало дня (ЧЧ:ММ)", TimeBox(s.Schedule.DayStart, delegate(string v)
		{
			s.Schedule.DayStart = v;
		})));
		_root.Children.Add(Ui.Row("Начало ночи (ЧЧ:ММ)", TimeBox(s.Schedule.NightStart, delegate(string v)
		{
			s.Schedule.NightStart = v;
		})));
		CheckBox checkBox3 = new CheckBox
		{
			Content = Loc.T("По восходу и закату (нужны координаты)"),
			IsChecked = s.Schedule.FollowSun
		};
		checkBox3.Checked += delegate
		{
			s.Schedule.FollowSun = true;
		};
		checkBox3.Unchecked += delegate
		{
			s.Schedule.FollowSun = false;
		};
		_root.Children.Add(checkBox3);
		_root.Children.Add(Ui.Row("Широта", NumberBox(s.Schedule.Latitude, delegate(double? v)
		{
			s.Schedule.Latitude = v;
		})));
		_root.Children.Add(Ui.Row("Долгота", NumberBox(s.Schedule.Longitude, delegate(double? v)
		{
			s.Schedule.Longitude = v;
		})));
		_root.Children.Add(Ui.Header("Обновления"));
		_root.Children.Add(Ui.Hint("Обновления берутся из релизов GitHub-репозитория (owner/repo). Архив проверяется по SHA256SUMS.txt из релиза."));
		TextBox repo = Ui.TextBox(s.Updates.Repository, 320.0);
		repo.TextChanged += delegate
		{
			s.Updates.Repository = repo.Text.Trim();
		};
		_root.Children.Add(Ui.Row("Репозиторий", repo));
		CheckBox checkBox4 = new CheckBox
		{
			Content = Loc.T("Проверять при запуске (не чаще раза в сутки)"),
			IsChecked = s.Updates.CheckOnStartup
		};
		checkBox4.Checked += delegate
		{
			s.Updates.CheckOnStartup = true;
		};
		checkBox4.Unchecked += delegate
		{
			s.Updates.CheckOnStartup = false;
		};
		_root.Children.Add(checkBox4);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		wrapPanel.Children.Add(Ui.Button("Проверить сейчас", CheckUpdates));
		_installUpdate = Ui.Button("Установить обновление", InstallUpdate, primary: true);
		_installUpdate.IsEnabled = false;
		wrapPanel.Children.Add(_installUpdate);
		_root.Children.Add(wrapPanel);
		_root.Children.Add(_updateStatus);
		_updateStatus.Foreground = Ui.Res("SubTextBrush");
		_updateStatus.Text = Loc.F("Текущая версия: {0} ({1})", App.VersionText, App.IsStandalone ? Loc.T("со встроенным .NET") : Loc.T("требует .NET 8 Desktop Runtime"));
		ShowUpdate();
		_root.Children.Add(Ui.Header("Установка"));
		_root.Children.Add(Ui.Hint(Installer.IsInstalled ? Loc.F("Установлен для текущего пользователя: {0}", Installer.InstallDir) : Loc.T("Сейчас Metamorph запущен из папки без установки. Установка копирует его в %LocalAppData%\\Programs\\Metamorph, добавляет ярлык в «Пуск», запись в «Приложения и возможности» и связывает файлы .mmtheme. Права администратора не нужны.")));
		WrapPanel wrapPanel2 = new WrapPanel();
		if (!Installer.IsRunningFromInstallDir)
		{
			wrapPanel2.Children.Add(Ui.Button("Установить для текущего пользователя", Install));
		}
		if (Installer.IsInstalled)
		{
			wrapPanel2.Children.Add(Ui.Button("Удалить Metamorph…", Uninstall));
		}
		_root.Children.Add(wrapPanel2);
		_root.Children.Add(Ui.Header("Диагностика"));
		_root.Children.Add(Ui.Hint(_app.SafeMode ? Loc.T("Сейчас включён безопасный режим: тема, обои и Мета-тян не запускались. Примените тему вручную — следующий запуск будет обычным.") : Loc.T("Если что-то работает не так — соберите отчёт: zip с журналом, настройками (ключи и токены скрыты) и сведениями о системе появится на рабочем столе. Запуск с ключом --safe стартует без темы, обоев и маскота.")));
		WrapPanel wrapPanel3 = new WrapPanel();
		wrapPanel3.Children.Add(Ui.Button("Собрать отчёт", delegate
		{
			try
			{
				string text = _app.CreateDiagnosticsReport();
				_status(Loc.F("Отчёт сохранён: {0}", text), arg2: false);
			}
			catch (Exception ex4)
			{
				_status(ex4.Message, arg2: true);
			}
		}));
		wrapPanel3.Children.Add(Ui.Button("Открыть журнал", delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _app.Services.Paths.LogFile + "\"")
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex3)
			{
				_status(ex3.Message, arg2: true);
			}
		}));
		wrapPanel3.Children.Add(Ui.Button("Папка данных", delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(_app.Services.Paths.Root)
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex2)
			{
				_status(ex2.Message, arg2: true);
			}
		}));
		_root.Children.Add(wrapPanel3);
		_root.Children.Add(Ui.Hint("Удалённая помощь: окно со ссылкой, по которой ИИ-помощник может посмотреть экран, журналы и сам исправить ошибку на этом компьютере. Доступ работает, только пока окно открыто."));
		WrapPanel wrapPanel4 = new WrapPanel();
		wrapPanel4.Children.Add(Ui.Button("Удалённая помощь (для ИИ)…", App.StartRemoteHelp));
		_root.Children.Add(wrapPanel4);
		WrapPanel wrapPanel5 = new WrapPanel
		{
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		wrapPanel5.Children.Add(Ui.Button("Сохранить настройки", Save, primary: true));
		wrapPanel5.Children.Add(Ui.Button("Сбросить горячие клавиши", delegate
		{
			_settings.Hotkeys = HotkeyActions.Defaults();
			Build();
		}));
		wrapPanel5.Children.Add(Ui.Button("Открыть settings.json", delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo("notepad.exe", "\"" + _app.Services.Paths.SettingsFile + "\"")
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				_status(ex.Message, arg2: true);
			}
		}));
		_root.Children.Add(wrapPanel5);
	}

	private static ComboBox ThemeCombo(IReadOnlyList<ThemeProfile> themes, string selected, Action<string> set)
	{
		string selected2 = selected;
		Action<string> set2 = set;
		ComboBox combo = new ComboBox
		{
			Width = 260.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			DisplayMemberPath = "Name"
		};
		foreach (ThemeProfile theme in themes)
		{
			combo.Items.Add(theme);
		}
		combo.SelectedItem = themes.FirstOrDefault((ThemeProfile t) => t.Id == selected2);
		combo.SelectionChanged += delegate
		{
			if (combo.SelectedItem is ThemeProfile themeProfile)
			{
				set2(themeProfile.Id);
			}
		};
		return combo;
	}

	private static TextBox TimeBox(string value, Action<string> set)
	{
		Action<string> set2 = set;
		TextBox box = Ui.TextBox(value, 90.0);
		box.TextChanged += delegate
		{
			TimeSpan time;
			bool flag = TimeOfDayParser.TryParse(box.Text, out time);
			box.BorderBrush = Ui.Res(flag ? "BorderBrush" : "DangerBrush");
			if (flag)
			{
				set2(box.Text.Trim());
			}
		};
		return box;
	}

	private static TextBox NumberBox(double? value, Action<double?> set)
	{
		Action<double?> set2 = set;
		TextBox box = Ui.TextBox(value?.ToString(CultureInfo.InvariantCulture), 120.0);
		box.TextChanged += delegate
		{
			string text = box.Text.Trim().Replace(',', '.');
			if (text.Length == 0)
			{
				set2(null);
			}
			else
			{
				double result;
				bool flag = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
				box.BorderBrush = Ui.Res(flag ? "BorderBrush" : "DangerBrush");
				if (flag)
				{
					set2(result);
				}
			}
		};
		return box;
	}

	private void Save()
	{
		try
		{
			if (_settings.Updates.Repository.Length > 0 && !UpdateService.IsValidRepository(_settings.Updates.Repository))
			{
				_status(Loc.T("Репозиторий указывается как owner/repo."), arg2: true);
				return;
			}
			AppSettings appSettings = _app.Services.Settings.Load();
			_settings.Updates.LastCheckUtc = appSettings.Updates.LastCheckUtc;
			_app.Services.Settings.Save(_settings);
			_status(Loc.T("Настройки сохранены."), arg2: false);
			_settings = _app.Services.Settings.Load();
			base.Dispatcher.InvokeAsync(Build, DispatcherPriority.Background);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось сохранить настройки: ") + ex.Message, arg2: true);
		}
	}

	private void ShowUpdate()
	{
		ReleaseInfo availableUpdate = _app.AvailableUpdate;
		if ((object)availableUpdate != null && _installUpdate != null)
		{
			_installUpdate.IsEnabled = true;
			_updateStatus.Text = Loc.F("Доступна версия {0} (у вас {1}).", availableUpdate.Tag, App.VersionText) + (string.IsNullOrWhiteSpace(availableUpdate.Notes) ? "" : ("\n" + ((availableUpdate.Notes.Length > 400) ? (availableUpdate.Notes.Substring(0, 400) + "…") : availableUpdate.Notes)));
			_updateStatus.Foreground = Ui.Res("AccentBrush");
		}
	}

	private async void CheckUpdates()
	{
		_app.Services.Settings.Save(_settings);
		if (!UpdateService.IsValidRepository(_settings.Updates.Repository))
		{
			_status(Loc.T("Сначала укажите репозиторий в формате owner/repo."), arg2: true);
			return;
		}
		_updateStatus.Text = Loc.T("Проверяю…");
		try
		{
			if ((object)(await _app.CheckForUpdatesAsync(manual: true)) == null)
			{
				_updateStatus.Text = Loc.F("Установлена последняя версия ({0}).", App.VersionText);
			}
			else
			{
				ShowUpdate();
			}
		}
		catch (Exception ex)
		{
			_updateStatus.Text = Loc.T("Не удалось проверить: ") + ex.Message;
			_updateStatus.Foreground = Ui.Res("DangerBrush");
		}
	}

	private async void InstallUpdate()
	{
		ReleaseInfo availableUpdate = _app.AvailableUpdate;
		if ((object)availableUpdate == null || MessageBox.Show(Loc.F("Скачать и установить {0}? Metamorph перезапустится.", availableUpdate.Tag), "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
		{
			return;
		}
		_installUpdate.IsEnabled = false;
		Progress<double> progress = new Progress<double>(delegate(double p)
		{
			_updateStatus.Text = Loc.F("Скачиваю… {0:0}%", p * 100.0);
		});
		try
		{
			await _app.InstallUpdateAsync(availableUpdate, progress);
		}
		catch (Exception ex)
		{
			_updateStatus.Text = Loc.T("Обновление не удалось: ") + ex.Message;
			_updateStatus.Foreground = Ui.Res("DangerBrush");
			_installUpdate.IsEnabled = true;
		}
	}

	private void Install()
	{
		try
		{
			string fileName = Installer.Install(App.VersionText);
			if (MessageBox.Show(Loc.F("Metamorph установлен в {0}. Перезапустить из установленной копии?", Installer.InstallDir), "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				_app.ExitApp();
				Process.Start(new ProcessStartInfo(fileName)
				{
					UseShellExecute = true
				});
			}
			else
			{
				Build();
			}
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось установить: ") + ex.Message, arg2: true);
		}
	}

	private void Uninstall()
	{
		try
		{
			Process.Start(new ProcessStartInfo(Installer.InstalledExe, "--uninstall")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}
}
