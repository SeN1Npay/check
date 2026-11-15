# TECHNICAL_NOTES

- XAML утерян; окно — BAML (`Baml/mainwindow.baml`), UI добавляется только из C#. Корень окна: Grid (шапка, `TabControl Tabs`, строка статуса), обёрнут `TitleBar.Apply`.
- Ресурсы стилей: BaseBrush, PanelBrush, CardBrush, CardHoverBrush, BorderBrush, TextBrush, SubTextBrush, AccentBrush, AccentTextBrush, DangerBrush; стили PrimaryButton, SectionHeader. Хелперы — `Views/Ui.cs`.
- Локализация: русские строки + `Loc.T`, `Loc.AddEnglish`.
- Главное окно прячется при закрытии и пересоздаётся при смене языка → глобальное состояние держать в `App`.
- Данные: `%AppData%\Metamorph\`; секреты — DPAPI (`Secrets`). Музыку планируется хранить в `music\`.
- Зависимости: ManagedShell 0.0.374, NAudio.Wasapi 2.2.1 (есть `MediaFoundationReader`, `WasapiOut`). Новых пакетов не требуется: WinRT `MediaPlayer` доступен через TFM 10.0.19041.
- Сборка: нужен официальный .NET 8 SDK с WindowsDesktop; `MM_AGENT=<имя>` выносит bin/obj в /tmp.
- Яндекс ID: Authorization Code + PKCE S256, client_secret не нужен; redirect_uri должен точно совпадать с указанным в приложении на oauth.yandex.ru. Официального API Яндекс Музыки для сторонних приложений нет.
