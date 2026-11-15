using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Metamorph.App.Assistant;
using Metamorph.App.Desktop;
using Metamorph.App.Games;
using Metamorph.App.Infrastructure;
using Metamorph.App.Launcher;
using Metamorph.App.Mascot;
using Metamorph.App.Remote;
using Metamorph.App.Windows;
using Metamorph.Core.Assistant;
using Metamorph.Core.Localization;
using Metamorph.Core.Mascot;
using Metamorph.Core.Models;
using Metamorph.Core.Native;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;
using Metamorph.Core.Windows;

namespace Metamorph.App;

public class App : Application
{
	private AssistantEngine? _assistant;

	private AssistantWindow? _assistantWindow;

	private Voice? _voice;

	private TelegramService? _telegram;

	private (string Key, ILlmClient Client)? _llm;

	private readonly DispatcherTimer _focusTick = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private MascotWindow? _mascot;

	private bool _mascotHooks;

	private string? _lastWallpaperKey;

	private readonly DispatcherTimer _wallpaperRotation = new DispatcherTimer();

	private WallpaperLibrary? _library;

	private const string MutexName = "Local\\Metamorph.SingleInstance";

	private Mutex? _mutex;

	private IpcServer? _ipc;

	private QueryServer? _query;

	private TrayIcon? _tray;

	private MainWindow? _main;

	private HotkeyManager? _hotkeys;

	private LauncherWindow? _launcher;

	private readonly DispatcherTimer _schedule = new DispatcherTimer();

	private bool? _lastPeriodIsDay;

	private bool _exiting;

	private readonly Random _surprise = new Random();

	private CrashGuard? _crashGuard;

	private static readonly DateTime StartedAt = DateTime.Now;

	private Mutex? _remoteMutex;

	private bool _contentLoaded;

	internal FocusTimer Focus { get; } = new FocusTimer();


	internal TelegramService Telegram => _telegram ?? (_telegram = new TelegramService(this));

	internal Voice Voice => _voice ?? (_voice = new Voice());

	internal AssistantEngine Assistant => _assistant ?? (_assistant = new AssistantEngine(new AssistantHost(this, Focus), () => Services.Settings.Current.Assistant, CreateLlm));

	internal MascotBrain? MascotBrain => _mascot?.Brain;

	public bool IsMascotVisible => _mascot != null;

	public WallpaperLibrary Library => _library ?? (_library = new WallpaperLibrary(Services.Paths.WallpaperLibrary));

	public WallpaperChoice? WallpaperOverride => Services.Settings.Current.Wallpaper.Override;

	public ThemeProfile WallpaperBaseTheme => Session.Active ?? Services.State.Load().AppliedProfile ?? Services.Profiles.Find("catppuccin-mocha") ?? new ThemeProfile
	{
		Id = "default",
		Name = "Default"
	};

	public new static App Current => (App)Application.Current;

	public MetamorphServices Services { get; private set; }

	public SessionController Session { get; private set; }

	public static Version AppVersion => typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);

	public static string VersionText => $"{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Build}";

	public static bool IsStandalone => string.Equals(Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory()).TrimEnd('\\'), Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

	public ReleaseInfo? AvailableUpdate { get; private set; }

	public IReadOnlyList<string> HotkeyFailures
	{
		get
		{
			IReadOnlyList<string> readOnlyList = _hotkeys?.Failures;
			return readOnlyList ?? Array.Empty<string>();
		}
	}

	public bool SafeMode { get; private set; }

	public event EventHandler? WallpaperChanged;

	public event EventHandler? ActiveThemeChanged;

	public event EventHandler? UpdateFound;

	private void InitAssistant()
	{
		_focusTick.Tick += delegate
		{
			try
			{
				Focus.Tick();
			}
			catch (Exception ex)
			{
				Log.Error("Таймер", ex);
			}
		};
		Focus.Changed += delegate
		{
			if (Focus.Running)
			{
				_focusTick.Start();
			}
			else
			{
				_focusTick.Stop();
			}
			Session?.UpdateFocus(Focus);
		};
		Focus.Finished += OnFocusFinished;
		Voice.SpeakingChanged += delegate(bool speaking)
		{
			MascotBrain mascotBrain = MascotBrain;
			if (mascotBrain != null)
			{
				mascotBrain.Speaking = speaking;
			}
		};
		if (!SafeMode)
		{
			Telegram.Sync();
		}
	}

	private void OnAssistantSettingsChanged()
	{
		_llm = null;
		_telegram?.Sync();
	}

	private ILlmClient? CreateLlm()
	{
		LlmSettings llm = Services.Settings.Current.Assistant.Llm;
		if (!llm.Enabled)
		{
			return null;
		}
		string text = llm.Endpoint + "|" + llm.Model + "|" + llm.ApiKeyProtected + "|" + llm.Temperature + "|" + llm.MaxTokens;
		(string, ILlmClient)? llm2 = _llm;
		if (llm2.HasValue)
		{
			(string, ILlmClient) valueOrDefault = llm2.GetValueOrDefault();
			if (valueOrDefault.Item1 == text)
			{
				return valueOrDefault.Item2;
			}
		}
		OpenAiCompatibleClient openAiCompatibleClient = new OpenAiCompatibleClient(llm, Secrets.Unprotect(llm.ApiKeyProtected));
		_llm = (text, openAiCompatibleClient);
		return openAiCompatibleClient;
	}

	internal async Task<AssistantReply> AskAssistantAsync(string text, AssistantSource source = AssistantSource.Local)
	{
		AssistantSettings settings = Services.Settings.Current.Assistant;
		MascotBrain brain = MascotBrain;
		bool thinking = settings.Llm.Enabled;
		if (thinking)
		{
			brain?.SetThinking(thinking: true);
		}
		AssistantReply assistantReply;
		try
		{
			assistantReply = await Assistant.AskAsync(text, source);
		}
		catch (Exception ex)
		{
			Log.Error("Помощник", ex);
			assistantReply = new AssistantReply(Loc.T("Ой, что-то пошло не так. Подробности — в журнале."), IntentKind.Unknown, MascotAction.Think);
		}
		finally
		{
			if (thinking)
			{
				brain?.SetThinking(thinking: false);
			}
		}
		brain = MascotBrain;
		if (source == AssistantSource.Local)
		{
			brain?.Say(assistantReply.Text);
			MascotAction? mood = assistantReply.Mood;
			if (mood.HasValue)
			{
				MascotAction valueOrDefault = mood.GetValueOrDefault();
				brain?.Command(valueOrDefault);
			}
			if (settings.SpeakReplies)
			{
				Voice.SpeakAsync(assistantReply.Text, settings);
			}
		}
		else if (brain != null && assistantReply.Intent != 0)
		{
			MascotAction? mood = assistantReply.Mood;
			if (mood.HasValue)
			{
				MascotAction valueOrDefault2 = mood.GetValueOrDefault();
				brain.Command(valueOrDefault2);
			}
		}
		return assistantReply;
	}

	public void ShowAssistant()
	{
		try
		{
			if (_assistantWindow == null)
			{
				_assistantWindow = new AssistantWindow(this);
			}
			_assistantWindow.Toggle();
		}
		catch (Exception ex)
		{
			Log.Error("Окно помощника", ex);
		}
	}

	private void OnFocusFinished(FocusPhase phase, string? label)
	{
		string text = phase switch
		{
			FocusPhase.Focus => Loc.T("Помодоро окончен — перерыв! Разомнись и попей воды."), 
			FocusPhase.Break => Loc.T("Перерыв окончен — снова за дело!"), 
			_ => (label == null) ? Loc.T("Время вышло! ⏰") : Loc.F("Время! {0} ⏰", label), 
		};
		try
		{
			SystemSounds.Exclamation.Play();
		}
		catch
		{
		}
		MascotSay(text);
		MascotBrain?.Command(MascotAction.Jump);
		Session?.Announce("⏰", (phase == FocusPhase.Timer) ? Loc.T("Таймер") : Loc.T("Помодоро"), label ?? text);
		if (Services.Settings.Current.Assistant.SpeakReplies)
		{
			Voice.SpeakAsync(text, Services.Settings.Current.Assistant);
		}
		if (Win32.IdleSeconds() > 120.0)
		{
			TelegramService telegram = _telegram;
			if (telegram != null && telegram.Running)
			{
				_telegram.NotifyOwnerAsync(text);
			}
		}
	}

	private void ShutdownAssistant()
	{
		_focusTick.Stop();
		_telegram?.Dispose();
		_voice?.Dispose();
		_assistantWindow?.Close();
	}

	private void InitMascot()
	{
		if (!_mascotHooks)
		{
			_mascotHooks = true;
			ActiveThemeChanged += delegate
			{
				if (_mascot != null)
				{
					_mascot.SetPalette(Session.Active?.Palette);
					_mascot.Brain.Notify(MascotEvent.ThemeApplied, Session.Active?.Name);
				}
			};
			WallpaperChanged += delegate
			{
				WallpaperChoice @override = Services.Settings.Current.Wallpaper.Override;
				string text = ((@override == null) ? ("theme:" + Session.Active?.Id) : @override.Path);
				if (!(text == _lastWallpaperKey))
				{
					_lastWallpaperKey = text;
					_mascot?.Brain.Notify(MascotEvent.WallpaperChanged);
				}
			};
		}
		if (!SafeMode && Services.Settings.Current.Mascot.Enabled)
		{
			ShowMascotWindow(hello: true, drop: false);
		}
	}

	private void ShowMascotWindow(bool hello, bool drop)
	{
		if (_mascot != null)
		{
			return;
		}
		try
		{
			_mascot = new MascotWindow(this, Services.Settings.Current.Mascot, Session.Active?.Palette);
			_mascot.Start(hello, drop);
		}
		catch (Exception ex)
		{
			Log.Error("Маскот не запустился", ex);
			_mascot = null;
		}
	}

	private void CloseMascotWindow(bool savePosition)
	{
		if (_mascot == null)
		{
			return;
		}
		MascotWindow mascot = _mascot;
		_mascot = null;
		if (savePosition)
		{
			try
			{
				AppSettings appSettings = Services.Settings.Load();
				appSettings.Mascot.Position = mascot.PositionFraction;
				Services.Settings.Save(appSettings);
			}
			catch (Exception ex)
			{
				Log.Error("Маскот: позиция", ex);
			}
		}
		mascot.Close();
	}

	public void ToggleMascot()
	{
		AppSettings appSettings = Services.Settings.Load();
		appSettings.Mascot.Enabled = !appSettings.Mascot.Enabled;
		if (!appSettings.Mascot.Enabled && _mascot != null)
		{
			appSettings.Mascot.Position = _mascot.PositionFraction;
		}
		Services.Settings.Save(appSettings);
		OnMascotSettingsChanged();
	}

	public void ShowMascotTab()
	{
		ShowMainWindow();
		_main?.ShowMascot();
	}

	private void OnMascotSettingsChanged()
	{
		if (!_exiting)
		{
			OnAssistantSettingsChanged();
			MascotSettings mascot = Services.Settings.Current.Mascot;
			if (!mascot.Enabled)
			{
				CloseMascotWindow(savePosition: false);
			}
			else if (_mascot == null)
			{
				ShowMascotWindow(hello: true, drop: true);
			}
			else
			{
				_mascot.ApplySettings(mascot);
			}
		}
	}

	internal void MascotSay(string text)
	{
		if (_mascot != null)
		{
			_mascot.Brain.Say(text);
		}
		else
		{
			_tray?.ShowHint(Services.Settings.Current.Mascot.Name, text);
		}
	}

	private void InitWallpaper()
	{
		Services.Engine.WallpaperOverride = (ThemeProfile p) => WallpaperChoices.Effective(p, Services.Settings.Current.Wallpaper);
		_wallpaperRotation.Tick += delegate
		{
			try
			{
				NextWallpaper();
			}
			catch (Exception ex)
			{
				Log.Error("Ротация обоев", ex);
			}
		};
		UpdateWallpaperRotation();
		if (!SafeMode && Session.Active == null && WallpaperOverride != null)
		{
			Session.RefreshWallpaper(WallpaperBaseTheme);
		}
	}

	private void UpdateWallpaperRotation()
	{
		int rotateMinutes = Services.Settings.Current.Wallpaper.RotateMinutes;
		_wallpaperRotation.Stop();
		if (rotateMinutes > 0)
		{
			_wallpaperRotation.Interval = TimeSpan.FromMinutes(Math.Clamp(rotateMinutes, 1, 1440));
			_wallpaperRotation.Start();
		}
	}

	internal void WallpaperPrefsChanged()
	{
		UpdateWallpaperRotation();
		Session.SetWallpaperVolume(Services.Settings.Current.Wallpaper.Volume);
	}

	public async Task<ApplyReport> ApplyWallpaperChoiceAsync(WallpaperChoice? choice)
	{
		AppSettings appSettings = Services.Settings.Load();
		appSettings.Wallpaper.Override = choice?.Copy();
		Services.Settings.Save(appSettings);
		ThemeProfile theme = WallpaperBaseTheme;
		ThemeProfile effective = WallpaperChoices.Effective(theme, appSettings.Wallpaper);
		EnsurePoster(effective);
		ApplyReport applyReport = await Task.Run(() => Services.Engine.ApplyWallpaper(effective));
		Session.RefreshWallpaper(theme);
		UpdateWallpaperRotation();
		this.WallpaperChanged?.Invoke(this, EventArgs.Empty);
		if (!applyReport.Success)
		{
			Log.Warn(applyReport.Summary());
		}
		return applyReport;
	}

	internal void EnsurePoster(ThemeProfile effective)
	{
		WallpaperSettings wallpaper = effective.Wallpaper;
		if (wallpaper.EffectiveKind != WallpaperKind.Video || string.IsNullOrWhiteSpace(wallpaper.Path))
		{
			return;
		}
		try
		{
			string text = Services.Profiles.ResolvePath(effective, wallpaper.Path);
			if (text != null)
			{
				PosterMaker.Ensure(Services.Wallpapers, text);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Постер обоев: " + ex.Message);
		}
	}

	public void NextWallpaper()
	{
		if (Session.HasSlides && WallpaperOverride == null)
		{
			Session.NextWallpaper();
			return;
		}
		WallpaperPrefs wallpaper = Services.Settings.Current.Wallpaper;
		IReadOnlyList<string> readOnlyList = Library.List();
		if (readOnlyList.Count == 0)
		{
			_tray?.ShowHint(Loc.T("Обои"), Loc.T("«Мои обои» пусты — добавьте GIF, видео или картинку на вкладке «Обои»."));
			return;
		}
		WallpaperChoice choice = WallpaperChoices.Next(wallpaper.Override, readOnlyList);
		ApplyWallpaperChoiceAsync(choice);
		if (_main == null || !_main.IsVisible)
		{
			_tray?.ShowHint(Loc.T("Обои"), WallpaperChoices.Describe(choice));
		}
	}

	public void ToggleWallpaperPause()
	{
		Session.ToggleWallpaperPause();
		this.WallpaperChanged?.Invoke(this, EventArgs.Empty);
		if (_main == null || !_main.IsVisible)
		{
			_tray?.ShowHint(Loc.T("Обои"), Session.IsWallpaperPaused ? Loc.T("Анимированные обои на паузе") : Loc.T("Анимированные обои снова двигаются"));
		}
	}

	public void ShowWallpapersTab()
	{
		ShowMainWindow();
		_main?.ShowWallpapers();
	}

	public async Task<bool> SetWallpaperFromArgumentAsync(string arg, bool addToLibrary = true)
	{
		arg = arg.Trim().Trim('"');
		if (arg.Equals("theme", StringComparison.OrdinalIgnoreCase) || arg.Equals("reset", StringComparison.OrdinalIgnoreCase))
		{
			await ApplyWallpaperChoiceAsync(null);
			return true;
		}
		if (!WallpaperLibrary.IsSupported(arg))
		{
			_tray?.ShowHint("Metamorph", Loc.F("Не поддерживается как обои: {0}", arg));
			return false;
		}
		string text = Path.GetFullPath(arg);
		try
		{
			if (addToLibrary)
			{
				text = Library.Add(text);
			}
		}
		catch (Exception ex)
		{
			Log.Error("Добавление обоев в библиотеку", ex);
		}
		await ApplyWallpaperChoiceAsync(new WallpaperChoice
		{
			Path = text
		});
		return true;
	}

	private void OnThemeChangingWallpaper()
	{
		AppSettings current = Services.Settings.Current;
		if (!current.Wallpaper.KeepOnThemeChange && current.Wallpaper.Override != null)
		{
			AppSettings appSettings = Services.Settings.Load();
			appSettings.Wallpaper.Override = null;
			Services.Settings.Save(appSettings);
			this.WallpaperChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void ClearWallpaperOverride()
	{
		if (Services.Settings.Current.Wallpaper.Override != null)
		{
			AppSettings appSettings = Services.Settings.Load();
			appSettings.Wallpaper.Override = null;
			Services.Settings.Save(appSettings);
			this.WallpaperChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		ModernStyles.Install(this);
		CommandLineOptions commandLineOptions = CommandLineOptions.Parse(e.Args);
		if (commandLineOptions.Uninstall)
		{
			RunUninstall(commandLineOptions);
			return;
		}
		if (commandLineOptions.RemoteHelp)
		{
			RunRemoteHelp();
			return;
		}
		_mutex = new Mutex(initiallyOwned: true, "Local\\Metamorph.SingleInstance", out var createdNew);
		if (!createdNew)
		{
			IpcClient.TrySend(commandLineOptions.ToIpcCommand());
			_mutex.Dispose();
			_mutex = null;
			Shutdown();
			return;
		}
		Services = new MetamorphServices();
		Log.Init(Services.Paths.LogFile);
		Log.Info("Запуск Metamorph " + VersionText + ", args: " + string.Join(' ', e.Args));
		CleanupReplacedFiles();
		Loc.SetLanguage(Services.Settings.Current.Language);
		base.DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			Log.Error("Необработанное исключение", args.ExceptionObject as Exception);
			TaskbarController.SafeShow();
		};
		if (commandLineOptions.Install && RunInstall(commandLineOptions))
		{
			return;
		}
		_crashGuard = new CrashGuard(Path.Combine(Services.Paths.Root, "startup.guard"));
		bool crashed = _crashGuard.Begin();
		SafeMode = commandLineOptions.Safe || crashed;
		if (SafeMode)
		{
			Log.Warn(crashed ? $"Безопасный режим: {_crashGuard.FailedStarts} запуска подряд завершились сбоем" : "Безопасный режим (--safe)");
			TaskbarController.SafeShow();
		}
		Session = new SessionController(Services, ShowMainWindow);
		Session.DesktopAction = RunDesktopAction;
		_tray = new TrayIcon(this);
		_ipc = new IpcServer(delegate(string command)
		{
			string command2 = command;
			base.Dispatcher.InvokeAsync(delegate
			{
				HandleCommand(command2);
			});
		});
		_ipc.Start();
		_query = new QueryServer(AnswerQueryAsync);
		_query.Start();
		if (commandLineOptions.Exit)
		{
			ExitApp();
			return;
		}
		_hotkeys = new HotkeyManager(OnHotkey);
		Services.Settings.Changed += delegate
		{
			base.Dispatcher.InvokeAsync(OnSettingsChanged);
		};
		Loc.LanguageChanged += delegate
		{
			base.Dispatcher.InvokeAsync(OnLanguageChanged);
		};
		if (!SafeMode)
		{
			Session.RestoreLastSession();
		}
		InitWallpaper();
		InitMascot();
		InitAssistant();
		RegisterHotkeys();
		_schedule.Tick += delegate
		{
			CheckSchedule();
		};
		CheckSchedule();
		if (commandLineOptions.Restore)
		{
			RestoreOriginalAsync();
		}
		else
		{
			string applyId = commandLineOptions.ApplyId;
			if (applyId != null)
			{
				ApplyThemeByIdAsync(applyId);
			}
		}
		string file = commandLineOptions.ImportFile;
		if (file != null)
		{
			base.Dispatcher.InvokeAsync(() => ImportThemeFile(file));
		}
		else if (!commandLineOptions.Minimized || SafeMode)
		{
			ShowMainWindow();
		}
		if (SafeMode)
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				_tray?.ShowHint(Loc.T("Безопасный режим"), crashed ? Loc.T("Прошлые запуски завершились сбоем, поэтому тема, обои и Мета-тян не запускались. Выберите тему вручную или соберите отчёт: «Настройки» → «Диагностика».") : Loc.T("Тема, обои и Мета-тян не запускались. Выберите тему вручную, когда будете готовы."));
			}, DispatcherPriority.ApplicationIdle);
		}
		DispatcherTimer healthy = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30.0)
		};
		healthy.Tick += delegate
		{
			healthy.Stop();
			_crashGuard.MarkHealthy();
		};
		healthy.Start();
		if (commandLineOptions.Launcher)
		{
			base.Dispatcher.InvokeAsync(ShowLauncher, DispatcherPriority.ApplicationIdle);
		}
		if (commandLineOptions.Assistant)
		{
			base.Dispatcher.InvokeAsync(ShowAssistant, DispatcherPriority.ApplicationIdle);
		}
		if (commandLineOptions.Island)
		{
			base.Dispatcher.InvokeAsync(Session.SummonIsland, DispatcherPriority.ApplicationIdle);
		}
		string wp = commandLineOptions.Wallpaper;
		if (wp != null)
		{
			base.Dispatcher.InvokeAsync(() => SetWallpaperFromArgumentAsync(wp), DispatcherPriority.ApplicationIdle);
		}
		CheckForUpdatesAsync(manual: false);
	}

	public void ShowMainWindow()
	{
		if (_main == null)
		{
			_main = CreateMainWindow();
		}
		_main.Show();
		if (_main.WindowState == WindowState.Minimized)
		{
			_main.WindowState = WindowState.Normal;
		}
		_main.Activate();
	}

	private MainWindow CreateMainWindow()
	{
		MainWindow window = new MainWindow(this);
		window.Closing += delegate(object? _, CancelEventArgs args)
		{
			if (!_exiting)
			{
				args.Cancel = true;
				window.Hide();
				_tray?.ShowHintOnce(Loc.T("Metamorph работает в трее"), Loc.T("Панель и тема активны. Выход — через значок в трее."));
			}
		};
		return window;
	}

	public void ShowLauncher()
	{
		if (Services.Settings.Current.Launcher.Enabled)
		{
			if (_launcher == null)
			{
				_launcher = new LauncherWindow(this);
			}
			_launcher.Toggle();
		}
	}

	public async Task<ApplyReport> ApplyThemeAsync(ThemeProfile profile)
	{
		ThemeProfile snapshot = ThemeJson.Clone(profile);
		OnThemeChangingWallpaper();
		EnsurePoster(WallpaperChoices.Effective(snapshot, Services.Settings.Current.Wallpaper));
		ApplyReport applyReport = await Task.Run(() => Services.Engine.Apply(snapshot));
		if (applyReport.Success || applyReport.Done.Count > 0)
		{
			Session.Activate(snapshot);
			RegisterHotkeys();
			this.ActiveThemeChanged?.Invoke(this, EventArgs.Empty);
		}
		if (_main == null || !_main.IsVisible)
		{
			_tray?.ShowHint(applyReport.Success ? Loc.T("Тема применена") : Loc.T("Тема применена частично"), applyReport.Summary());
		}
		return applyReport;
	}

	public async Task<ApplyReport> RestoreOriginalAsync()
	{
		Session.Deactivate();
		ClearWallpaperOverride();
		ApplyReport applyReport = await Task.Run(() => Services.Engine.RestoreOriginal());
		RegisterHotkeys();
		this.ActiveThemeChanged?.Invoke(this, EventArgs.Empty);
		if (_main == null || !_main.IsVisible)
		{
			_tray?.ShowHint("Metamorph", applyReport.Summary());
		}
		return applyReport;
	}

	private async Task ApplyThemeByIdAsync(string id)
	{
		ThemeProfile themeProfile = Services.Profiles.Find(id);
		if (themeProfile == null)
		{
			_tray?.ShowHint("Metamorph", Loc.F("Тема «{0}» не найдена.", id));
		}
		else
		{
			await ApplyThemeAsync(themeProfile);
		}
	}

	public void NextTheme()
	{
		IReadOnlyList<ThemeProfile> readOnlyList = Services.Profiles.LoadAll();
		if (readOnlyList.Count != 0)
		{
			ThemeProfile a = Session.Active;
			int num = ((a != null) ? readOnlyList.ToList().FindIndex((ThemeProfile p) => p.Id == a.Id) : (-1));
			ApplyThemeAsync(readOnlyList[(num + 1 + readOnlyList.Count) % readOnlyList.Count]);
		}
	}

	public string? CreateDiagnosticsReport()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		string text = Path.Combine(string.IsNullOrEmpty(folderPath) ? Services.Paths.Root : folderPath, $"Metamorph-report-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
		IEnumerable<string> extra = (from d in Display.All()
			select $"Display: {d.Name} {d.Bounds.Width}x{d.Bounds.Height} scale {Display.Scale:0.##}{(d.Primary ? " primary" : "")}").Concat(new string[4]
		{
			"Active theme: " + (Session?.Active?.Id ?? "(none)"),
			"Themes: " + string.Join(", ", from t in Services.Profiles.LoadAll()
				select t.Id),
			$"Safe mode: {SafeMode}, standalone: {IsStandalone}, installed: {Installer.IsInstalled}",
			$"Mascot visible: {IsMascotVisible}, wallpaper override: {WallpaperChoices.Describe(WallpaperOverride)}"
		});
		string settingsJson = null;
		try
		{
			if (File.Exists(Services.Paths.SettingsFile))
			{
				settingsJson = File.ReadAllText(Services.Paths.SettingsFile);
			}
		}
		catch (Exception ex)
		{
			Log.Warn("Отчёт: settings.json — " + ex.Message);
		}
		string logFile = Services.Paths.LogFile;
		DiagnosticsReport.Create(text, DiagnosticsReport.SystemInfo(VersionText, extra), settingsJson, new(string, string)[3]
		{
			("metamorph.log", logFile),
			("metamorph.old.log", Log.OldPath(logFile)),
			("state.json", Services.Paths.StateFile)
		}, new(string, string)[2]
		{
			("trace.txt", EventTrace.Dump(1500)),
			("session.txt", Session?.Describe() ?? "")
		});
		Log.Info("Отчёт сохранён: " + text);
		try
		{
			Process.Start("explorer.exe", "/select,\"" + text + "\"");
		}
		catch (Exception ex2)
		{
			Log.Warn("Отчёт: не открыть Проводник — " + ex2.Message);
		}
		return text;
	}

	public async Task<ThemeProfile?> SurpriseThemeAsync()
	{
		List<ThemeProfile> list = (from p in Services.Profiles.LoadAll()
			where p.Id != Session.Active?.Id
			select p).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		ThemeProfile pick = list[_surprise.Next(list.Count)];
		await ApplyThemeAsync(pick);
		return pick;
	}

	public void ToggleDayNight()
	{
		ScheduleSettings schedule = Services.Settings.Current.Schedule;
		string id = ((Session.Active?.Id == schedule.DayThemeId) ? schedule.NightThemeId : schedule.DayThemeId);
		ApplyThemeByIdAsync(id);
	}

	private void CheckSchedule()
	{
		_schedule.Stop();
		ScheduleSettings schedule = Services.Settings.Current.Schedule;
		if (!schedule.Enabled)
		{
			_lastPeriodIsDay = null;
			return;
		}
		try
		{
			DateTime now = DateTime.Now;
			bool flag = DayNightSchedule.IsDay(schedule, now);
			if (_lastPeriodIsDay != flag)
			{
				_lastPeriodIsDay = flag;
				string text = DayNightSchedule.ThemeFor(schedule, now);
				if (Session.Active?.Id != text)
				{
					ApplyThemeByIdAsync(text);
				}
			}
			TimeSpan timeSpan = DayNightSchedule.UntilNextChange(schedule, now) + TimeSpan.FromSeconds(5.0);
			_schedule.Interval = ((timeSpan > TimeSpan.FromMinutes(10.0)) ? TimeSpan.FromMinutes(10.0) : ((timeSpan < TimeSpan.FromSeconds(10.0)) ? TimeSpan.FromSeconds(10.0) : timeSpan));
		}
		catch (Exception ex)
		{
			Log.Error("Расписание тем", ex);
			_schedule.Interval = TimeSpan.FromMinutes(10.0);
		}
		_schedule.Start();
	}

	private void RegisterHotkeys()
	{
		if (_hotkeys != null)
		{
			AppSettings settings = Services.Settings.Current;
			bool tiling = Session.Active?.Windows.Tiling.Hotkeys ?? false;
			string[] tilingActions = new string[7] { "snapLeft", "snapRight", "snapUp", "snapDown", "maximize", "center", "tileAll" };
			Dictionary<string, string> map = (from kv in settings.Hotkeys
				where tiling || !tilingActions.Contains<string>(kv.Key, StringComparer.OrdinalIgnoreCase)
				where settings.Launcher.Enabled || !kv.Key.Equals("launcher", StringComparison.OrdinalIgnoreCase)
				select kv).ToDictionary<KeyValuePair<string, string>, string, string>((KeyValuePair<string, string> kv) => kv.Key, (KeyValuePair<string, string> kv) => kv.Value, StringComparer.OrdinalIgnoreCase);
			_hotkeys.Apply(map);
		}
	}

	public void ToggleDesktopEdit()
	{
		if (!Session.ToggleDesktopEdit())
		{
			Session.Announce("🧩", Loc.T("Карточки"), Loc.T("включите в «Редактор» → «Рабочий стол»"));
		}
	}

	// Действия горячих углов и прочих «кнопок рабочего стола» (HotCornersSettings.Actions).
	public void RunDesktopAction(string action)
	{
		try
		{
			switch (action)
			{
			case "showDesktop":
				Win32.SendChord(91, 68);
				break;
			case "taskView":
				Win32.SendChord(91, 9);
				break;
			case "launcher":
				ShowLauncher();
				break;
			case "island":
				Session.SummonIsland();
				break;
			case "assistant":
				ShowAssistant();
				break;
			case "lock":
				Win32.LockWorkStation();
				break;
			case "screenSaver":
				Win32.SendMessage(Win32.GetForegroundWindow(), 274, new IntPtr(61760), IntPtr.Zero);
				break;
			case "games":
				GameHub.Show(this);
				break;
			case "editDesktop":
				ToggleDesktopEdit();
				break;
			case "nextWallpaper":
				Session.NextWallpaper();
				break;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Действие рабочего стола " + action, ex);
		}
	}

	private void OnHotkey(string action)
	{
		if (action.Equals("editDesktop", StringComparison.OrdinalIgnoreCase))
		{
			ToggleDesktopEdit();
			return;
		}
		if (action.Equals("games", StringComparison.OrdinalIgnoreCase))
		{
			GameHub.Show(this);
			return;
		}
		TilingSettings s = Session.Active?.Windows.Tiling ?? new TilingSettings();
		string text = action.ToLowerInvariant();
		if (text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 8:
			switch (text[5])
			{
			case 'h':
				if (text == "launcher")
				{
					ShowLauncher();
				}
				break;
			case 'g':
				if (text == "daynight")
				{
					ToggleDayNight();
				}
				break;
			case 'a':
				if (text == "showmain")
				{
					ShowMainWindow();
				}
				break;
			case 'e':
				if (text == "snapleft")
				{
					TilingActions.Snap(s, SnapDirection.Left);
				}
				break;
			case 'o':
				if (text == "snapdown")
				{
					TilingActions.Snap(s, SnapDirection.Down);
				}
				break;
			case 'i':
				if (text == "maximize")
				{
					TilingActions.Maximize(s);
				}
				break;
			}
			break;
		case 10:
			switch (text[0])
			{
			case 't':
				if (text == "toggledock")
				{
					Session.ToggleDock();
				}
				break;
			case 's':
				if (text == "showisland")
				{
					Session.SummonIsland();
				}
				break;
			}
			break;
		case 9:
			switch (text[0])
			{
			case 't':
				if (text == "togglebar")
				{
					Session.ToggleBar();
				}
				break;
			case 'n':
				if (text == "nexttheme")
				{
					NextTheme();
				}
				break;
			case 's':
				if (text == "snapright")
				{
					TilingActions.Snap(s, SnapDirection.Right);
				}
				break;
			case 'a':
				if (text == "assistant")
				{
					ShowAssistant();
				}
				break;
			}
			break;
		case 6:
			switch (text[0])
			{
			case 's':
				if (text == "snapup")
				{
					TilingActions.Snap(s, SnapDirection.Up);
				}
				break;
			case 'c':
				if (text == "center")
				{
					TilingActions.Center(s);
				}
				break;
			}
			break;
		case 13:
			switch (text[0])
			{
			case 'n':
				if (text == "nextwallpaper")
				{
					NextWallpaper();
				}
				break;
			case 's':
				if (text == "surprisetheme")
				{
					SurpriseThemeAsync();
				}
				break;
			}
			break;
		case 7:
			if (text == "tileall")
			{
				TilingActions.TileAll(s);
			}
			break;
		case 14:
			if (text == "pausewallpaper")
			{
				ToggleWallpaperPause();
			}
			break;
		case 11:
		case 12:
			break;
		}
	}

	private void OnSettingsChanged()
	{
		Loc.SetLanguage(Services.Settings.Current.Language);
		RegisterHotkeys();
		_lastPeriodIsDay = null;
		CheckSchedule();
		UpdateWallpaperRotation();
		OnMascotSettingsChanged();
	}

	private void OnLanguageChanged()
	{
		_tray?.Rebuild();
		if (_main != null)
		{
			bool isVisible = _main.IsVisible;
			double left = _main.Left;
			double top = _main.Top;
			double left2 = left;
			_exiting = true;
			_main.Close();
			_exiting = false;
			_main = null;
			if (isVisible)
			{
				ShowMainWindow();
				_main.Left = left2;
				_main.Top = top;
			}
		}
	}

	public ThemeProfile? ImportThemeFile(string file, bool askToApply = true)
	{
		try
		{
			ThemeProfile themeProfile = ImportCore(file);
			this.ActiveThemeChanged?.Invoke(this, EventArgs.Empty);
			ShowMainWindow();
			if (askToApply && MessageBox.Show(Loc.F("Тема «{0}» добавлена. Применить сейчас?", themeProfile.Name), "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				ApplyThemeAsync(themeProfile);
			}
			return themeProfile;
		}
		catch (Exception ex)
		{
			Log.Error("Импорт " + file, ex);
			MessageBox.Show(Loc.F("Не удалось импортировать тему: {0}", ex.Message), "Metamorph", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return null;
		}
	}

	private ThemeProfile ImportCore(string file)
	{
		switch (Path.GetExtension(file).ToLowerInvariant())
		{
		case ".mmtheme":
			return ThemePackage.Import(file, Services.Paths, Services.Profiles);
		case ".yaml":
		case ".yml":
		{
			ThemeProfile themeProfile2 = Base16.Import(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));
			Services.Profiles.Save(themeProfile2);
			return themeProfile2;
		}
		default:
		{
			ThemeProfile themeProfile = ThemeJson.Deserialize(File.ReadAllText(file));
			if (string.IsNullOrWhiteSpace(themeProfile.Id))
			{
				themeProfile.Id = Base16.Slug(Path.GetFileNameWithoutExtension(file));
			}
			IReadOnlyList<string> readOnlyList = ThemeValidator.Validate(themeProfile);
			if (readOnlyList.Count > 0)
			{
				throw new InvalidDataException(string.Join("; ", readOnlyList));
			}
			Services.Profiles.Save(themeProfile);
			return themeProfile;
		}
		}
	}

	private void HandleCommand(string command)
	{
		Log.Info("IPC: " + command);
		switch (command)
		{
		case "show":
			ShowMainWindow();
			return;
		case "restore":
			RestoreOriginalAsync();
			return;
		case "exit":
			ExitApp();
			return;
		case "launcher":
			ShowLauncher();
			return;
		case "island":
			Session.SummonIsland();
			return;
		case "assistant":
			ShowAssistant();
			return;
		case "bar":
			Session.ToggleBar();
			return;
		case "dock":
			Session.ToggleDock();
			return;
		case "mascot":
			ToggleMascot();
			return;
		case "next-wallpaper":
			NextWallpaper();
			return;
		case "pause-wallpaper":
			ToggleWallpaperPause();
			return;
		case "remote-help":
			StartRemoteHelp();
			return;
		}
		if (command.StartsWith("wallpaper:", StringComparison.Ordinal))
		{
			string text = command;
			int length = "wallpaper:".Length;
			SetWallpaperFromArgumentAsync(text.Substring(length, text.Length - length));
		}
		else if (command.StartsWith("apply:", StringComparison.Ordinal))
		{
			string text = command;
			int length = "apply:".Length;
			ApplyThemeByIdAsync(text.Substring(length, text.Length - length));
		}
		else if (command.StartsWith("import:", StringComparison.Ordinal))
		{
			string text = command;
			int length = "import:".Length;
			ImportThemeFile(text.Substring(length, text.Length - length));
		}
	}

	private async Task<string> AnswerQueryAsync(string query)
	{
		int num = query.IndexOf(' ');
		string text = ((num < 0) ? query : query.Substring(0, num)).ToLowerInvariant();
		object obj;
		if (num >= 0)
		{
			int num2 = num + 1;
			obj = query.Substring(num2, query.Length - num2).Trim();
		}
		else
		{
			obj = "";
		}
		string arg = (string)obj;
		int result;
		return text switch
		{
			"ping" => $"Metamorph {VersionText} pid={Environment.ProcessId} safe={SafeMode}", 
			"pid" => Environment.ProcessId.ToString(), 
			"trace" => EventTrace.Dump(int.TryParse(arg, out result) ? Math.Clamp(result, 10, 4000) : 400), 
			"state" => await OnUiAsync(StateText, 3000), 
			"command" => await OnUiAsync(delegate
			{
				HandleCommand(arg);
				return "ok";
			}, 3000), 
			_ => "unknown query; use: ping | pid | trace [n] | state | command <show|exit|restore|bar|dock|mascot|next-wallpaper|pause-wallpaper|apply:id|wallpaper:path|…>", 
		};
	}

	private async Task<string> OnUiAsync(Func<string> action, int timeoutMs)
	{
		DispatcherOperation<string> op = base.Dispatcher.InvokeAsync(action);
		return (await Task.WhenAny(op.Task, Task.Delay(timeoutMs)) != op.Task) ? $"UI thread did not respond within {timeoutMs} ms — the interface is hanging" : (await op.Task);
	}

	private string StateText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(52, 6, stringBuilder2);
		handler.AppendLiteral("Metamorph ");
		handler.AppendFormatted(VersionText);
		handler.AppendLiteral(" pid=");
		handler.AppendFormatted(Environment.ProcessId);
		handler.AppendLiteral(" uptime=");
		handler.AppendFormatted(DateTime.Now - StartedAt, "hh\\:mm\\:ss");
		handler.AppendLiteral(" safe=");
		handler.AppendFormatted(SafeMode);
		handler.AppendLiteral(" standalone=");
		handler.AppendFormatted(IsStandalone);
		handler.AppendLiteral(" installed=");
		handler.AppendFormatted(Installer.IsInstalled);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.Append(Session.Describe());
		WallpaperPrefs wallpaper = Services.Settings.Current.Wallpaper;
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(79, 7, stringBuilder2);
		handler.AppendLiteral("wallpaper override: ");
		handler.AppendFormatted(WallpaperChoices.Describe(wallpaper.Override));
		handler.AppendLiteral(" style=");
		handler.AppendFormatted(wallpaper.Override?.Style);
		handler.AppendLiteral(" muted=");
		handler.AppendFormatted(wallpaper.Override?.Muted);
		handler.AppendLiteral(" ");
		handler.AppendLiteral("rotate=");
		handler.AppendFormatted(wallpaper.RotateMinutes);
		handler.AppendLiteral("m pauseCovered=");
		handler.AppendFormatted(wallpaper.PauseWhenCovered);
		handler.AppendLiteral(" pauseBattery=");
		handler.AppendFormatted(wallpaper.PauseOnBattery);
		handler.AppendLiteral(" volume=");
		handler.AppendFormatted(wallpaper.Volume);
		stringBuilder4.AppendLine(ref handler);
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(37, 2, stringBuilder2);
		handler.AppendLiteral("mascot visible=");
		handler.AppendFormatted(IsMascotVisible);
		handler.AppendLiteral("; main window visible=");
		handler.AppendFormatted(_main?.IsVisible ?? false);
		stringBuilder5.AppendLine(ref handler);
		stringBuilder.AppendLine("event counters (total / last 10 s):");
		foreach (KeyValuePair<string, long> item in (from kv in EventTrace.Counters()
			orderby kv.Value descending
			select kv).Take(25))
		{
			item.Deconstruct(out var key, out var value);
			string text = key;
			long value2 = value;
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder2);
			handler.AppendLiteral("  ");
			handler.AppendFormatted<string>(text, -32);
			handler.AppendLiteral(" ");
			handler.AppendFormatted(value2, 7);
			handler.AppendLiteral(" / ");
			handler.AppendFormatted(EventTrace.Rate(text, 10.0));
			stringBuilder6.AppendLine(ref handler);
		}
		return stringBuilder.ToString();
	}

	public async Task<ReleaseInfo?> CheckForUpdatesAsync(bool manual)
	{
		AppSettings current = Services.Settings.Current;
		string repository = current.Updates.Repository;
		if (!UpdateService.IsValidRepository(repository))
		{
			return null;
		}
		if (!manual)
		{
			if (!current.Updates.CheckOnStartup)
			{
				return null;
			}
			DateTime? lastCheckUtc = current.Updates.LastCheckUtc;
			if (lastCheckUtc.HasValue)
			{
				DateTime valueOrDefault = lastCheckUtc.GetValueOrDefault();
				if (DateTime.UtcNow - valueOrDefault < TimeSpan.FromHours(20.0))
				{
					return null;
				}
			}
		}
		try
		{
			ReleaseInfo releaseInfo = await Services.Updates.GetLatestAsync(repository);
			AppSettings appSettings = Services.Settings.Load();
			appSettings.Updates.LastCheckUtc = DateTime.UtcNow;
			Services.Settings.Save(appSettings);
			if ((object)releaseInfo == null || !UpdateService.IsNewer(releaseInfo.Version, AppVersion))
			{
				return null;
			}
			if (!manual && appSettings.Updates.SkippedVersion == releaseInfo.Version.ToString())
			{
				return null;
			}
			AvailableUpdate = releaseInfo;
			this.UpdateFound?.Invoke(this, EventArgs.Empty);
			if (!manual)
			{
				_tray?.ShowHint(Loc.T("Доступно обновление Metamorph"), Loc.F("Версия {0}. Установить — во вкладке «Настройки».", releaseInfo.Tag));
			}
			return releaseInfo;
		}
		catch (Exception ex)
		{
			Log.Error("Проверка обновлений", ex);
			if (manual)
			{
				throw;
			}
			return null;
		}
	}

	public async Task InstallUpdateAsync(ReleaseInfo release, IProgress<double>? progress = null)
	{
		ReleaseAsset asset = UpdateService.PickAsset(release, IsStandalone) ?? throw new InvalidOperationException(Loc.T("В релизе нет подходящего zip-архива."));
		UpdateService.ScheduleReplace(await Services.Updates.DownloadAsync(release, asset, progress), AppContext.BaseDirectory, Environment.ProcessId);
		ExitApp();
	}

	private bool RunInstall(CommandLineOptions options)
	{
		try
		{
			string fileName = Installer.Install(VersionText);
			if (!options.Quiet)
			{
				MessageBox.Show(Loc.F("Metamorph установлен в {0}. Ярлык добавлен в меню «Пуск».", Installer.InstallDir), "Metamorph", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			if (Installer.IsRunningFromInstallDir)
			{
				return false;
			}
			_mutex?.ReleaseMutex();
			_mutex?.Dispose();
			_mutex = null;
			Process.Start(new ProcessStartInfo(fileName)
			{
				UseShellExecute = true
			});
			Shutdown();
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("Установка", ex);
			if (!options.Quiet)
			{
				MessageBox.Show(Loc.F("Не удалось установить: {0}", ex.Message), "Metamorph", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			return false;
		}
	}

	public static void StartRemoteHelp()
	{
		try
		{
			Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Metamorph.exe"), "--remote-help")
			{
				UseShellExecute = false
			})?.Dispose();
		}
		catch (Exception ex)
		{
			Log.Error("Удалённая помощь", ex);
			MessageBox.Show(ex.Message, "Metamorph", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void RunRemoteHelp()
	{
		_remoteMutex = new Mutex(initiallyOwned: true, "Local\\Metamorph.RemoteHelp", out var createdNew);
		if (!createdNew)
		{
			_remoteMutex.Dispose();
			_remoteMutex = null;
			ActivateRemoteHelpWindow();
			Shutdown();
			return;
		}
		AppPaths appPaths = new AppPaths();
		Log.Init(Path.Combine(appPaths.Root, "remote-help.log"));
		Log.Info($"Удалённая помощь {VersionText}, pid {Environment.ProcessId}");
		try
		{
			Loc.SetLanguage(new AppSettingsStore(appPaths).Current.Language);
		}
		catch
		{
		}
		base.DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			Log.Error("Удалённая помощь: ошибка интерфейса", args.Exception);
			args.Handled = true;
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			Log.Error("Удалённая помощь: сбой", args.ExceptionObject as Exception);
		};
		TaskScheduler.UnobservedTaskException += delegate(object? _, UnobservedTaskExceptionEventArgs args)
		{
			Log.Warn("Удалённая помощь: " + args.Exception.GetBaseException().Message);
			args.SetObserved();
		};
		BridgeWindow bridgeWindow = new BridgeWindow(appPaths);
		bridgeWindow.Closed += delegate
		{
			try
			{
				_remoteMutex?.ReleaseMutex();
			}
			catch
			{
			}
			_remoteMutex?.Dispose();
			_remoteMutex = null;
			Shutdown();
		};
		bridgeWindow.Show();
		bridgeWindow.Activate();
	}

	private static void ActivateRemoteHelpWindow()
	{
		Process[] processesByName = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "Metamorph"));
		foreach (Process process in processesByName)
		{
			using (process)
			{
				try
				{
					if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero)
					{
						string mainWindowTitle = process.MainWindowTitle;
						if (mainWindowTitle.Contains(Loc.T("удалённая помощь"), StringComparison.OrdinalIgnoreCase) || mainWindowTitle.Contains("Remote help", StringComparison.OrdinalIgnoreCase))
						{
							NativeMethods.ShowWindow(process.MainWindowHandle, 9);
							Win32.SetForegroundWindow(process.MainWindowHandle);
							break;
						}
					}
				}
				catch
				{
				}
			}
		}
	}

	private static void CleanupReplacedFiles()
	{
		try
		{
			foreach (string item in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.old-*", SearchOption.AllDirectories))
			{
				try
				{
					File.Delete(item);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private void RunUninstall(CommandLineOptions options)
	{
		Services = new MetamorphServices();
		Log.Init(Services.Paths.LogFile);
		Loc.SetLanguage(Services.Settings.Current.Language);
		try
		{
			if (!options.Quiet && MessageBox.Show(Loc.T("Удалить Metamorph? Перед удалением будет возвращён исходный вид Windows."), "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
			{
				Shutdown();
				return;
			}
			IpcClient.TrySend("exit");
			Thread.Sleep(1500);
			try
			{
				Services.Engine.RestoreOriginal();
			}
			catch (Exception ex)
			{
				Log.Error("Восстановление перед удалением", ex);
			}
			TaskbarController.SafeShow();
			if (options.KeepFiles)
			{
				Shutdown();
				return;
			}
			bool flag = options.Purge;
			if (!options.Quiet && !flag)
			{
				flag = MessageBox.Show(Loc.T("Удалить также пользовательские темы, настройки и резервные копии?"), "Metamorph", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
			}
			Installer.Uninstall(flag, Services.Paths.Root);
			if (!options.Quiet)
			{
				MessageBox.Show(Loc.T("Metamorph удалён."), "Metamorph", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
		}
		catch (Exception ex2)
		{
			Log.Error("Удаление", ex2);
			if (!options.Quiet)
			{
				MessageBox.Show(Loc.F("Не удалось удалить: {0}", ex2.Message), "Metamorph", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		Shutdown();
	}

	public void ExitApp()
	{
		if (!_exiting)
		{
			_exiting = true;
			_crashGuard?.MarkHealthy();
			_schedule.Stop();
			_hotkeys?.Dispose();
			_launcher?.Close();
			CloseMascotWindow(savePosition: true);
			ShutdownAssistant();
			Session?.Shutdown();
			_main?.Close();
			_tray?.Dispose();
			_ipc?.Dispose();
			_query?.Dispose();
			Shutdown();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Session?.Shutdown();
		if (_remoteMutex != null)
		{
			try
			{
				_remoteMutex.ReleaseMutex();
			}
			catch
			{
			}
			_remoteMutex.Dispose();
		}
		if (_mutex != null)
		{
			try
			{
				_mutex.ReleaseMutex();
			}
			catch
			{
			}
			_mutex.Dispose();
		}
		base.OnExit(e);
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		Log.Error("Ошибка интерфейса", e.Exception);
		MessageBox.Show(e.Exception.Message, Loc.T("Metamorph — ошибка"), MessageBoxButton.OK, MessageBoxImage.Hand);
		e.Handled = true;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/Metamorph;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.33.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
