# PROJECT_STATE — Metamorph

1. **Приложение:** Metamorph 1.6 — WPF-приложение для «райсинга» Windows 10 (темы, панель, док, остров, обои, маскот).
2. **Технологии:** C#, WPF, .NET 8 (App: net8.0-windows10.0.19041.0; Core: net8.0-windows), NAudio.Wasapi 2.2.1, ManagedShell 0.0.374, xUnit.
3. **Структура:** `src/Metamorph.Core` (логика), `src/Metamorph.App` (WPF, BAML вместо утерянного XAML), `tests/Metamorph.Tests`, `themes/`.
4. **Архитектура:** см. TECHNICAL_NOTES.md.
5. **Реализовано (музыка):** ничего — исходный код ещё не изменялся.
6. **Не реализовано:** весь музыкальный плеер (вкладка «Музыка», мини-плеер, полный плеер, библиотека, плейлисты, избранное, история, очередь, сохранение состояния, Яндекс ID).
7. **Частично:** только анализ и план (DEVELOPMENT_LOG, этапы 1–5).
8. **Известные ошибки:** нет новых.
9. **Сборка:** исходный проект собирается официальным .NET SDK 8.0.428 (0 ошибок, 37 предупреждений), тесты 180/180. Системный Ubuntu-SDK 8.0.131 не подходит (нет WindowsDesktop SDK).
10. **Последний этап:** Этап 5 — технический план.

### NEXT AGENT — С ЧЕГО ПРОДОЛЖАТЬ
- Стадия: анализ завершён, разработка не начата. Начинать с Этапа 7.
- Читать первым: `src/Metamorph.App/Metamorph.App/MainWindow.cs` (конструктор, вкладки), `App.cs` (`OnStartup`, `ExitApp`, `OnLanguageChanged` — окно пересоздаётся!), `Metamorph.App.Views/Ui.cs`, `Metamorph.App.Infrastructure/Secrets.cs`, `AppSettingsStore.cs`.
- Следующая задача: создать `src/Metamorph.Core/Metamorph.Core.Music/` (модели Track/Playlist/PlayerState, атомарное JSON-хранилище в `%AppData%\Metamorph\music\`, PlaybackQueue с shuffle/repeat, Library/Playlist/Favorites/History сервисы, PlayerService над интерфейсом `IAudioEngine`), затем тесты xUnit для них.
- Затем App: `Metamorph.App.Music/` — движок на WinRT `Windows.Media.Playback.MediaPlayer` (метаданные/обложки — `Windows.Storage.FileProperties`/`StorageFile`), `MusicView` (новая `TabItem` добавляется в `Tabs.Items` из кода), `MiniPlayerView` (вставить в корневой Grid окна, после `TitleBar.Apply`), `FullPlayerView` (оверлей). Сервис плеера держать в `App`, не в окне.
- Яндекс ID: OAuth Authorization Code + PKCE (S256), `https://oauth.yandex.ru/authorize`, `POST https://oauth.yandex.ru/token`, профиль `https://login.yandex.ru/info?format=json`; loopback-redirect через HttpListener; токен — через `Secrets.Protect`. Client ID задаёт пользователь. Музыкальный каталог Яндекса НЕ интегрировать (нет официального API) — только ссылка на music.yandex.ru и объяснение.
- Уже проверено, не повторять: структура архива, базовая сборка и тесты, ключи стилей (см. DEVELOPMENT_LOG этап 2).
- Сборка: `export PATH=/tmp/dotnet8:$PATH DOTNET_ROOT=/tmp/dotnet8 MM_AGENT=x; dotnet build src/Metamorph.App/Metamorph.App.csproj -c Release` (SDK ставить `dotnet-install.sh --channel 8.0 --install-dir /tmp/dotnet8`).
