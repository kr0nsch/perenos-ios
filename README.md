<p align="center"><img src="assets/logo.png" width="76" alt="Перенос"></p>
<h1 align="center">Перенос</h1>
<p align="center"><b>Привычные приложения. Новый iPhone.</b><br>
Бесплатная программа для Windows и macOS с открытым исходным кодом.<br>
Переносите приложения между iPhone, восстанавливайте свои покупки App Store<br>
и храните оригинальные IPA в личной библиотеке.</p>

<p align="center">
  <a href="https://github.com/kr0nsch/perenos-ios/releases/latest"><b>Скачать</b></a> ·
  <a href="https://kr0nsch.github.io/perenos-ios/">Сайт проекта</a> ·
  <a href="README.en.md">English</a>
</p>

<p align="center">
  <a href="https://github.com/kr0nsch/perenos-ios/releases/latest"><img src="https://img.shields.io/github/v/release/kr0nsch/perenos-ios?label=%D0%92%D0%B5%D1%80%D1%81%D0%B8%D1%8F" alt="Последний релиз"></a>
  <a href="https://github.com/kr0nsch/perenos-ios/actions/workflows/ci.yml"><img src="https://github.com/kr0nsch/perenos-ios/actions/workflows/ci.yml/badge.svg" alt="Сборка и тесты"></a>
  <img src="https://img.shields.io/badge/.NET%2010-Avalonia-7053d9" alt=".NET 10 и Avalonia">
  <a href="LICENSE"><img src="https://img.shields.io/badge/MIT-open%20source-27765e" alt="Открытый код по лицензии MIT"></a>
</p>

## Три сценария

| Сценарий | Что получится |
|---|---|
| **Новый iPhone** | Выберите приложения на старом телефоне и установите только нужные на новый. Телефоны можно подключать вместе или по очереди. |
| **Обновить или восстановить** | Просмотрите покупки своего Apple Account и установите последние доступные версии на один iPhone. |
| **Библиотека на компьютере** | Сохраните приложения сейчас, а установите их позже. Проверяйте версии, размер и совместимость с iOS. |

> [!IMPORTANT]
> «Перенос» скачивает **оригинальные приложения с серверов Apple**, а не копии с файлообменников. Доступ зависит от лицензии аккаунта и того, выдаёт ли Apple пакет. FairPlay не обходится, чужие покупки не разблокируются, аккаунт на iPhone не меняется.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/avalonia/restore-ru-RU-Dark.png">
    <img src="docs/screenshots/avalonia/restore-ru-RU-Light.png" width="1000" alt="Восстановление приложений в новом интерфейсе Переноса">
  </picture><br>
  <sub>Актуальный интерфейс Avalonia в деморежиме. Аккаунт, устройства и приложения вымышленные.</sub>
</p>

## Восстановить VK Музыку, Авито и другие свои приложения

1. Подключите iPhone по USB, разблокируйте его и нажмите **«Доверять»**.
2. Откройте **«Восстановление»** и войдите в Apple Account покупателя **на компьютере**. Менять аккаунт на телефоне не нужно.
3. Дождитесь истории покупок. Ничего не выбирается автоматически.
4. Найдите приложение поиском. В меню фильтра доступны **все покупки**, **недоступные в РФ** и **российские сервисы**. «Выбрать видимые» действует только на текущий фильтр и поисковую выдачу.
5. Отметьте нужное и нажмите **«Продолжить» → «Установить»**.

Если приложения нет в ответе истории, поищите его среди российских сервисов. Отметка «Из каталога» — кандидат, а не подтверждение владения: Apple проверит лицензию при скачивании. История после смены региона может быть неполной, а Apple может прекратить выдачу приложения или старой версии.

Каталог [`catalog/russian-app-catalog.json`](catalog/russian-app-catalog.json) содержит 459 записей, не найденных в российской витрине на дату проверки, включая 243 записи известных экосистем. Это не официальный реестр удалений Apple и не гарантия восстановления.

## Библиотека и установка IPA

Выберите **«Библиотека» → «Добавить приложения с iPhone»**, чтобы сохранить свои приложения. Для установки позже откройте **«Установить сохранённые на iPhone»**. Можно добавить собственные `.ipa` в папку библиотеки из настроек.

Поддерживаются пакеты от Apple и ваши IPA из доверенных источников **с действующей лицензией и подписью**. «Перенос» читает метаданные, проверяет требования к iOS и не превращает чужой или расшифрованный IPA в самостоятельное приложение.

**Установка без повторного входа на компьютере не означает работу без Apple Account.** Приложения остаются привязанными к покупателю; iOS может запросить его при первом запуске или обновлении. Переносятся приложения, а не переписка, документы и настройки — данные восстанавливайте через iCloud, резервную копию или синхронизацию приложения.

## Системные требования

- **Windows 10 версии 2004+ или Windows 11**, x64. Для связи с iPhone нужны [«Устройства Apple»](https://apps.microsoft.com/detail/9NP83LWLPZ9K) или iTunes.
- **macOS 15+**, Apple Silicon или Intel. Разрешите подключение iPhone в Finder. Готовые DMG публикуются только после подписи Developer ID и нотаризации Apple; для локальной сборки достаточно инструментов ниже.

В релизах есть установщик `Perenos-<версия>-setup-x64.exe` и portable ZIP. Данные portable-версии хранятся в папке `Data` рядом с приложением. Настройки, сессия и библиотека прежних версий мигрируют автоматически без перезаписи новых файлов; пользовательская папка загрузок не перемещается.

## Приватность

У проекта нет своего сервера, рекламы и скрытой телеметрии. Вход выполняет открытый **ipatool**, собранный из закреплённого исходного кода с проверкой SHA-256. Пароль не сохраняется «Переносом». Ключ сессии защищён **Windows DPAPI** для текущего пользователя или хранится в **macOS Keychain**.

Первый вход может занять несколько минут: ipatool подготавливает компоненты Apple для локальной проверки подлинности. Файл IPA может содержать почту покупателя — не публикуйте его. Подробнее: [SECURITY.md](SECURITY.md).

В релизах публикуются `SHA256SUMS.txt` и Sigstore-аттестации происхождения сборки:

```bash
gh attestation verify <скачанный-файл> -R kr0nsch/perenos-ios
```

Sigstore подтверждает происхождение сборки, но не является подписью издателя Windows Authenticode.

## Сборка из исходников

Нужны **.NET 10 SDK**, **Go 1.26.5** и PowerShell. Для установщика Windows также нужен Inno Setup 6.

```powershell
git clone https://github.com/kr0nsch/perenos-ios.git
cd perenos-ios
./build/Get-Ipatool.ps1
dotnet run --project src/Perenos
```

Деморежим с вымышленными устройствами и аккаунтом:

```powershell
dotnet run --project src/Perenos -- --demo
dotnet run --project src/Perenos -- --demo-saved
```

На macOS 15+ дополнительно нужны Xcode Command Line Tools и PowerShell 7. CGO включается нативно для Apple Keychain; ipatool для macOS не собирается из Windows.

```bash
brew install dotnet go
brew install --cask powershell
pwsh build/Get-Ipatool.ps1 -RuntimeIdentifier osx-arm64
dotnet run --project src/Perenos -- --demo
bash build/build-macos.sh 1.7.0 osx-arm64
```

Для Intel замените `osx-arm64` на `osx-x64`. Результат: `artifacts/release/Perenos-1.7.0-macos-<архитектура>.dmg`. Без `MACOS_SIGN_IDENTITY` сборка подписывается ad-hoc для разработки. Для публичного распространения задайте Developer ID и профиль `MACOS_NOTARY_PROFILE`, чтобы включить нотаризацию.

Проект разделён на `Perenos.Core` (USB, Apple и IPA), `Perenos.Presentation` (общие MVVM, локализация и демо) и `Perenos` (Avalonia-интерфейс).

## Проверки

```bash
dotnet test Perenos.sln
npm ci
npm run test:site
```

Есть тесты Core, ViewModel/Platform и настоящие headless-тесты Avalonia. `build/Demo-Check.ps1` дополнительно проверяет Windows UI через UI Automation: сценарии, выбор приложений, сохранение, установку и настройки. Демо всегда использует отдельные временные данные, а не ваши настройки и аккаунт.

**Проверенные демосценарии не заменяют проверку настоящего USB и Apple Account.** Более ранние версии были проверены на реальных iPhone 16 Pro, iPhone 14 и iPhone 17. Для новой кроссплатформенной версии нужна отдельная проверка USB на Windows и macOS, а подпись и первый запуск DMG — на чистой учётной записи Mac. Текущие результаты CI и ограничения указаны в [плане развития](docs/DEVELOPMENT.md).

## Участие

[Сообщить об ошибке](https://github.com/kr0nsch/perenos-ios/issues/new?template=bug_report.yml) · [Проверить совместимость](https://github.com/kr0nsch/perenos-ios/issues/new?template=compatibility_report.yml) · [Руководство участника](CONTRIBUTING.md) · [История изменений](CHANGELOG.md)

Основа: [.NET 10](https://dotnet.microsoft.com/), [Avalonia](https://avaloniaui.net/), [Netimobiledevice](https://github.com/artehe/Netimobiledevice), [ipatool](https://github.com/majd/ipatool). Лицензии: [MIT](LICENSE) и [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Независимый проект, не связанный с Apple Inc. iPhone и App Store — товарные знаки Apple Inc. Используйте программу только с приложениями, на которые у вас есть право.
