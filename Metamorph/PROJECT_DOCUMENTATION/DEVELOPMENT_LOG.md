# DEVELOPMENT_LOG — журнал разработки Metamorph

Хронологический журнал. Старые записи не удаляются и не переписываются; новые добавляются в конец.

---

### Этап 1 — Изучение архива и распаковка проекта

- Дата и время: 2026-11-15 08:19 (UTC).
- Цель: найти целевой проект в прикреплённом архиве.
- Изученные файлы: `check-hoplite-ioulis-ef667424.zip` (432 файла).
- Созданные файлы: каталог `Metamorph/` в корне репозитория (копия `Metamorph/` из архива без изменений).
- Изменённые файлы: нет.
- Что сделано: архив распакован во временный каталог. В корне архива — копия репозитория (`README.md`, `LICENSE`,
  `my_memory_card.py` — PyQt-викторина, НЕ целевой проект) и каталог `Metamorph/` — целевое WPF-приложение.
  Каталогов `bin/`, `obj/`, `artifacts/` в архиве нет. В репозитории до этого Metamorph отсутствовал.
- Решения: работать только в `Metamorph/`; `my_memory_card.py` не трогать.
- Проверки: сверка содержимого (`diff` README/py с репозиторием — идентичны).
- Результат: успешно.
- Ошибки: нет.
- Следующие действия: изучить главное окно.

### Этап 2 — Главное окно, навигация, стили

- Дата и время: 2026-11-15 08:25.
- Изученные файлы: `src/Metamorph.App/Metamorph.App/MainWindow.cs`, `App.cs`, `Metamorph.App.Views/Ui.cs`,
  `SettingsView.cs`, `Metamorph.App.Infrastructure/TitleBar.cs`, `ModernStyles.cs`, `Localizer.cs`,
  `Baml/mainwindow.baml`, `Baml/styles/appstyles.baml` (через `strings`).
- Выводы:
  - Исходный XAML утерян (код декомпилирован ILSpy). Разметка окна — скомпилированный BAML; новые элементы
    интерфейса создаются только из C# (или XAML-строк через `XamlReader.Parse`, как в `ModernStyles`).
  - Корень окна — Grid из 3 строк: шапка, `TabControl Tabs`, нижняя строка с кнопками и `StatusText`.
    Вкладки (`WallpaperTab`, `MascotTab`, `EditorTab`, `SettingsTab`, `GalleryTab`) получают `Content` из кода.
  - `TitleBar.Apply` оборачивает `Window.Content` в свою шапку.
  - Ключи ресурсов: `BaseBrush`, `PanelBrush`, `CardBrush`, `CardHoverBrush`, `BorderBrush`, `TextBrush`,
    `SubTextBrush`, `AccentBrush`, `AccentTextBrush`, `DangerBrush`, стили `PrimaryButton`, `SectionHeader`.
  - Главное окно при закрытии прячется в трей, а при смене языка **пересоздаётся** (`App.OnLanguageChanged`) —
    поэтому воспроизведение нельзя хранить в окне, а представления должны отписываться от событий.
  - Локализация: исходные строки на русском, английский — через `Loc.T` / `Loc.AddEnglish`.

### Этап 3 — Библиотеки, зависимости, хранение данных

- Изученные файлы: `*.csproj`, `Directory.Build.props`, `build.sh`, `THIRD-PARTY-NOTICES.txt`,
  `Metamorph.App.Infrastructure/MediaInfo.cs`.
- Выводы: App — `net8.0-windows10.0.19041.0` (доступны WinRT API: `Windows.Media.*`), пакеты ManagedShell 0.0.374,
  NAudio.Wasapi 2.2.1 (+NAudio.Core). Core — `net8.0-windows`, без пакетов. Тесты — xUnit, только Core.
  `MM_AGENT=<имя>` выносит bin/obj в `/tmp/mmbuild/<имя>/` (используется, чтобы не засорять репозиторий).
- Проверки: базовая сборка до изменений.
  - Системный `dotnet` 8.0.131 (сборка Ubuntu) НЕ содержит `Microsoft.NET.Sdk.WindowsDesktop` → ошибка MSB4019.
  - Установлен официальный SDK 8.0.428 в `/tmp/dotnet8` (`dotnet-install.sh --channel 8.0`).
    Сборка App: 0 ошибок, 37 предупреждений. Тесты: 180/180 пройдено.

### Этап 4 — Механизмы сохранения настроек

- Изученные файлы: `AppPaths.cs`, `AppSettingsStore.cs`, `AppStateStore.cs`, `JsoncFile.cs`, `Log.cs`, `Secrets.cs`.
- Выводы: данные в `%AppData%\Metamorph\`; настройки — `settings.json` (запись через `.tmp` + `File.Move`),
  секреты шифруются DPAPI (`Secrets.Protect/Unprotect`, CurrentUser). Журнал — `Log.Info/Warn/Error`.
- Решение: музыкальные данные хранить отдельно в `%AppData%\Metamorph\music\` (не раздувать `settings.json`),
  атомарная запись (tmp + flush + replace с `.bak`), чтение с откатом на `.bak` при повреждении.

### Этап 5 — Технический план

- Документация Яндекс ID (yandex.ru/dev/id/doc/ru/codes/code-url) сверена: Authorization Code + PKCE (S256),
  `client_secret` не требуется при передаче `code_verifier`; redirect_uri должен точно совпадать с настройками приложения.
- План: Core/`Metamorph.Core.Music` — модели, хранилище, очередь, библиотека, плейлисты, избранное, история,
  PlayerService поверх интерфейса `IAudioEngine`, OAuth-клиент Яндекс ID; App/`Metamorph.App.Music` — движок
  воспроизведения (WinRT `MediaPlayer`, запасной NAudio), вкладка «Музыка», мини-плеер, полный плеер.

### Этап 6 — Документация
- Дата и время: 2026-11-15.
- Созданы PROJECT_STATE.md, TODO.md, TECHNICAL_NOTES.md, CHANGELOG.md.
- Исходный код не изменялся. Точка остановки: перед Этапом 7 (создание Metamorph.Core.Music).
