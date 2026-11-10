# Changelog

All notable changes to Perenos are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [1.6.0] - 2026-11-10

### Добавлено
- Понятная навигация: подписанные этапы и прогресс, «Назад» слева, основное действие справа, явные названия сценариев и текстовая кнопка настроек.
- Отдельные меню фильтров и сортировки вместо переключения по кругу. Кнопка «Выбрать видимые» действует только на текущий фильтр и поисковую выдачу.
- Полное переименование в Perenos: solution, проекты, пространства имён, ресурсы, установщик, логи и пути данных.
- Автоматическая миграция настроек, зашифрованной сессии, записей доверия iPhone и библиотеки. Существующие файлы не перезаписываются, пользовательская папка загрузок не перемещается.

### Изменено
- Встроенный ipatool обновлён до `2.8.0+e0e2908`: официальный v2.8.0 и пять последующих upstream-коммитов с улучшением входа и параллельным скачиванием больших IPA.
- Коммит и SHA-256 архива закреплены; `go generate ./...` и `go test ./...` прошли. История из 206 покупок, получение версий и реальное скачивание VK Музыки 8.30.1 проверены без установки на устройство.

### Проверено
- 143 автоматических теста, Release-сборки x64 и ARM64, полный демонстрационный сценарий на русском и английском в светлой и тёмной темах, браузерные проверки пяти размеров экрана и режима без JavaScript.
- Перенос установки проверен Inno Setup на изолированных папках: прежняя папка переименовывается, существующая новая не перезаписывается. Реальная установка и данные пользователя не затрагиваются.

### Added
- Clear navigation with labeled steps, progress, Back on the left, the primary action on the right, task-based flow names and a labeled Settings button.
- Explicit filter and sort menus instead of cycling. Select Visible applies only to the current filter and search results.
- Complete Perenos renaming across the solution, projects, namespaces, resources, installer, logs and data paths.
- Automatic migration of settings, encrypted sessions, iPhone pair records and the app library. Existing files are preserved, and custom download folders are not moved.

### Changed
- Updated bundled ipatool to `2.8.0+e0e2908`: the official v2.8.0 plus five verified upstream commits improving sign-in and parallel downloads of large IPA files.
- Pinned the commit and archive SHA-256; `go generate ./...` and `go test ./...` pass. Verified 206 purchases, version lookup and a real VK Music 8.30.1 download without installing it on a device.

### Verification
- 143 automated tests, x64 and ARM64 Release builds, the complete demo in Russian and English, light and dark themes, browser checks at five viewport sizes and with JavaScript disabled.
- Inno Setup migration checks on isolated folders: the previous installation is renamed, while an existing new installation is preserved. The real installation and user data are not touched.

## [1.5.0] - 2026-11-08

### Добавлено
- **Все покупки аккаунта** в сценарии «Обновить и восстановить», а не только приложения, отсутствующие в российской витрине. Ничего не выбирается автоматически.
- Фильтры всех покупок, недоступных в РФ приложений и российских сервисов. Поиск учитывает название из истории, семейство сервиса и альтернативное имя. «Выбрать все» действует только в пределах текущего фильтра и поискового запроса.
- Встроенный каталог из 459 приложений, не найденных в российской витрине на дату проверки, включая 243 записи известных российских экосистем. Кандидаты из каталога помечены отдельно; Apple проверяет лицензию при скачивании.
- Последние известные `externalVersionId` из публичной витрины для точного скачивания версий. Проверка IPA учитывает `iTunesMetadata.itemId`, а операции различают приложения по числовому App Store ID.

### Исправлено
- Встроенный ipatool обновлён до проверенного upstream-коммита `72c47d5`: актуальный `User-Agent: Configurator/2.21` восстанавливает получение истории покупок вместо ошибки `DAAP 401`. Источник, Go toolchain и SHA-256 архива закреплены.
- Исправлен вызов удаления приложения через `installation_proxy`: bundle ID передаётся в обязательном поле `ApplicationIdentifier`.
- Пустой ответ истории больше не скрывает кандидатов из каталога. Ошибка истории по-прежнему не подменяется списком установленных приложений.

### Документация и сайт
- Переработаны русский и английский README: три сценария, новая обложка, понятная инструкция восстановления, требования к IPA и ограничения работы без Apple Account.
- Добавлены новые кадры реального интерфейса 1.5.0 в деморежиме и обложка 1200×630 для ссылок.
- Обновлены заголовок, описание, Open Graph, `SoftwareApplication` и `FAQPage`, canonical URL, sitemap и актуальные ссылки на релиз. Описание и темы репозитория GitHub приведены в соответствие с возможностями проекта.

### Проверено
- 139 автоматических тестов, Release-сборка и полный демонстрационный сценарий восстановления.
- На iPhone 17 с iOS 27.2 получены 206 покупок аккаунта. Установлены Авито, VK, VK Видео, Почта Mail.ru и «Цифровой сейф» Газпромбанка. Сбер уже был актуален. Последняя очередь обновлений: 95 установлено, 18 уже актуальны, одно устройство не ответило вовремя.
- Установка подтверждена, но первый запуск и работа приложений вообще без Apple Account на устройстве не проверены. Полная совместимость со всеми моделями iPhone, версиями iOS и приложениями не гарантируется.

### Added
- **All account purchases** in Update & Restore, not just apps missing from the Russian storefront. Nothing is selected automatically.
- Filters for all purchases, apps unavailable in Russia and Russian services. Search includes history names, service families and aliases. Select All is limited to the active filter and search.
- An embedded catalog of 459 apps absent from the Russian storefront at the recorded check date, including 243 known Russian ecosystem entries. Catalog candidates are clearly marked; Apple verifies their licenses when downloading.
- Exact downloads using known public `externalVersionId` values. IPA validation reads `iTunesMetadata.itemId`, and operations distinguish apps by numeric App Store ID.

### Fixed
- Updated bundled ipatool to verified upstream commit `72c47d5`, whose current `Configurator/2.21` user agent restores purchase-history access instead of `DAAP 401`. The source, Go toolchain and archive SHA-256 are pinned.
- Corrected app removal through `installation_proxy` by passing the bundle ID in the required `ApplicationIdentifier` field.
- Empty purchase history no longer hides catalog candidates. History failures still never fall back to installed apps as a substitute.

### Documentation and website
- Redesigned Russian and English READMEs with three workflows, new covers, restoration instructions, IPA requirements and honest Apple Account limitations.
- Added real Perenos 1.5.0 demo screenshots and a 1200×630 social card.
- Updated the title, description, Open Graph metadata, `SoftwareApplication` and `FAQPage` structured data, canonical URL, sitemap and release links. Updated GitHub repository topics and description.

### Verification and known limitations
- 139 automated tests, a Release build and the complete restoration demo pass.
- On an iPhone 17 running iOS 27.2, retrieved 206 account purchases and installed Avito, VK, VK Video, Mail.ru Mail and Gazprombank's Digital Safe. Sber was already current. The latest queue installed 95 updates, found 18 apps current and encountered one device-response timeout.
- Installation is confirmed, but first launch and operation without any Apple Account on the device have not been verified. Compatibility with every iPhone, iOS version and app is not guaranteed.

[1.6.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.6.0
[1.5.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.5.0

## [1.4.0] - 2026-11-08

### Добавлено
- Новый сценарий **«Обновить и восстановить»** для одного iPhone: вход в аккаунт покупателя, чтение всех страниц истории покупок iPhone и проверка текущей российской витрины.
- В список попадают приложения, недоступные в российском App Store: установленные можно обновить, а отсутствующие на устройстве — установить заново, если Apple ещё выдаёт их аккаунту. Используются последние доступные версии; новые приложения не приобретаются.
- При смене аккаунта история загружается заново. Неполный или противоречивый ответ истории не выдаётся за полный список.

### Исправлено
- Увеличен до 10 минут тайм-аут ответа `installation_proxy`: большие официальные IPA больше не прерываются преждевременно во время установки на iPhone.
- Тайм-аут установки теперь отображается как ошибка установки, а не как неизвестный сбой.

### Проверено
- 132 автоматических теста, Release-сборка и полный демонстрационный сценарий восстановления.
- На iPhone 16 Pro с iOS 27.2 успешно установлены 19 из 20 последних версий приложений, недоступных в российской витрине. Red Shield VPN не обновлён: iPhone не ответил на установку за 10 минут.

### Известное ограничение
- На тестовой учётной записи Apple отклоняет запрос полной истории с `DAAP 401`. В этом случае Perenos показывает ошибку и не подменяет историю списком установленных приложений. Восстановление таких покупок, удалённых с устройства, требует дальнейшей работы.

### Added
- New **Update & Restore** flow for one iPhone: purchaser sign-in, complete paged iPhone purchase history and a check against the current Russian storefront.
- Updates installed apps or reinstalls owned apps missing from the device when Apple still supplies them, always using the latest available versions and never acquiring new apps.
- Reloads history after account changes and rejects incomplete or inconsistent purchase-history responses.

### Fixed
- Increases the `installation_proxy` response timeout to ten minutes so large official IPA packages can finish installing.
- Reports installation timeouts as installation failures instead of unknown errors.

### Verification and known limitations
- 132 automated tests, a Release build and the complete restoration demo pass.
- On an iPhone 16 Pro running iOS 27.2, 19 of 20 unavailable apps were installed at their latest versions. Red Shield VPN remained unchanged because the iPhone did not answer within ten minutes.
- Apple rejected the test account's complete purchase-history request with `DAAP 401`. Perenos reports this without substituting installed apps for account history; restoring purchases removed from that device remains unresolved.

[1.4.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.4.0

## [1.3.1] - 2026-11-08

### Исправлено
- Обновлён встроенный ipatool до upstream-коммита `001d8898`: восстановлены вход в Apple Account и скачивание приложений из App Store, включая повторное скачивание снятых с публикации приложений, если Apple выдаёт их аккаунту.
- Исправленная версия собирается из проверенного снапшота исходников ipatool с закреплёнными коммитом, toolchain Go и SHA-256.

### Важно
- При первом входе ipatool однократно загружает и кэширует компоненты Apple для локальной проверки подлинности (около 200 МБ на диске). Это может занять несколько минут.

### Fixed
- Updated bundled ipatool to upstream commit `001d8898`, restoring Apple Account sign-in and App Store downloads, including redownloads of delisted apps when Apple still grants the account access.
- The fixed version is built from a checksum-verified source snapshot with a pinned commit, Go toolchain and SHA-256.

### Note
- On first sign-in, ipatool downloads and caches Apple components for local authentication (approximately 200 MB on disk). This can take several minutes.

[1.3.1]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.3.1

## [1.3.0] - 2026-11-06

### Добавлено
- На подключённом iPhone автоматически находятся приложения текущего Apple Account, которых сейчас нет в российском App Store. Они отмечаются в общем списке, а фильтр «Только недоступные» показывает их отдельно.
- Проверка повторяется после смены аккаунта. Публичному каталогу Apple передаются только числовые ID приложений, без Apple ID и учётных данных.
- Приложения можно сортировать по имени или размеру; поиск также находит аккаунт покупателя.

### Исправлено
- Поздние ответы отменённой загрузки, повторного переноса или сброса больше не изменяют новый список и текущий прогресс.
- Недопустимые символы в версиях и именах приложений больше не создают ошибки путей; заблокированный временный файл после успешной установки не помечает её как неудачную.

### Added
- Automatically finds installed apps owned by the current Apple Account but currently absent from the Russian App Store, marks them and offers an unavailable-only filter.
- Rechecks after account changes and sends only numeric app IDs to Apple's public catalog, never account credentials.
- Adds name/size sorting and purchaser-account search.

### Fixed
- Prevents stale results from canceled loads, retried transfers or resets from changing a newer list or transfer run.
- Sanitizes app names and versions used in paths and no longer reports a successful install as failed when a temporary file is locked.

[1.3.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.3.0

## [1.2.0] - 2026-11-03

### Имя «Перенос»
- Версия 1.2.0 представила текущее название «Перенос» (Perenos). Репозиторий: `kr0nsch/perenos-ios`.
- Обновлены название в интерфейсе, установщик, ярлыки и имена файлов релиза. Запуск программы — через `Perenos.exe`.
- Установщик обновляет прежнюю установку. Настройки, библиотека приложений и зашифрованная сессия Apple Account используют прежние папки и остаются совместимыми.
- Новый README: обложка для светлой и тёмной темы, два сценария переноса, галерея, быстрый старт и ответы на вопросы. Основной язык — русский.

### Changed
- Introduced the Perenos name and moved the repository to `perenos-ios`.
- Release packages now use `Perenos-<version>-setup-x64.exe` and `Perenos-<version>-portable-x64.zip`.
- Preserved the installer identity, existing data folders and encrypted account keys for upgrades.
- Redesigned the Russian and English READMEs with editable SVG covers and theme-aware media.

[1.2.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.2.0

## [1.1.1] - 2026-11-01

### Что исправлено
- Кнопка «Остановить» работает уже во время подготовки приложений; незавершённые приложения можно повторить.
- При ошибке выхода из Apple Account программа показывает сообщение и позволяет повторить выход, сохраняя текущий статус аккаунта.
- Повторный запуск больше не зависает, если первая копия приложения закрылась или не отвечает.
- После смены папки загрузок приложения сохраняются в выбранную папку; конфликт длинных имён файлов больше не вызывает зависание.

### Fixed
- Stop now cancels preparation as well as the transfer, keeping unfinished apps available for retry.
- Failed App Store sign-out now keeps the account visible and shows a localized error instead of reporting success.
- Forwarding a second launch now has a bounded wait and safely releases its event handle, including when the running app exits or stops responding.
- Saving apps after changing the downloads folder now writes to the selected folder instead of reusing a cached file from the previous library.
- Saving an app with a long name no longer hangs when its filename collides with a different saved app; both packages are preserved with distinct names.

### Changed
- Added automated view-model regression tests to CI and release builds.
- The Russian main README and the separate English README have a shorter quick start and clearer requirements and transfer limits.
- Added forms for compatibility reports and feature requests, plus contributor development priorities.

[1.1.1]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.1.1

## [1.1.0] - 2026-10-27

### Added
- A start screen with two choices: **Transfer Now** (both iPhones connected, as before) and **Transfer Later**.
- **Transfer Later** works in two steps that can be weeks apart:
  - **Save Apps** downloads the apps from the old iPhone to this PC (the downloads folder).
  - **Install on New iPhone** installs saved apps over USB later, without signing in to the App Store on the PC.
- Saved apps are listed with their icons and home-screen names, including `.ipa` files kept by 1.0 or copied into the folder.
  - For each app, Perenos picks the newest saved version that the new iPhone can run.
  - Apps the iPhone can't run (iOS too old, iPad only) are listed separately with the reason.
  - Apps already on the iPhone are marked and left unticked; older installed versions are updated.
- Saved files get readable names such as `Telegram 12.1.ipa`, and different versions of an app are kept side by side.
- Icons read from an iPhone are cached, so saved apps keep them after the phone is unplugged.

### Changed
- Perenos opens on the start screen. Back from the first step and Done return to it.

[1.1.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.1.0

## [1.0.0] - 2026-10-26

First release of the Windows app.

### Added
- New WinUI 3 app with a four-step flow: connect the source iPhone, choose apps, connect the target iPhone, install.
- Apple-style design with light and dark mode; English and Russian. Respects Windows high-contrast themes and the "Animation effects" setting.
- Built-in USB support for iPhones (no Python or extra tools): detection, the "Trust This Computer" prompt, reading installed apps with their icons, and installing with progress.
- App Store sign-in with two-factor authentication, using the bundled open-source ipatool.
- Downloads run while the previous app installs; per-app progress and clear reasons when an app can't be installed; retry for failed apps.
- Options to install the latest version or the same version as the source iPhone, to get free apps not yet owned, and to keep downloaded `.ipa` files.
- Installer (per-user, no administrator rights) and portable ZIP for x64.
- Diagnostic logs, single-instance window, keeps the PC awake during installation.

### Changed
- The original Python/Tkinter prototype moved to `legacy/python`.

[1.0.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.0.0
