# AppBridge

**Ваши приложения из App Store — на новом iPhone. Бесплатная программа для Windows: перенесите сейчас или сохраните на потом.**

<p>
  <a href="https://github.com/kr0nsch/AppBridge/releases/latest"><img src="https://img.shields.io/github/v/release/kr0nsch/AppBridge?label=%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C%20%D0%B4%D0%BB%D1%8F%20Windows&color=007aff&style=for-the-badge" alt="Скачать AppBridge для Windows"></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/README-English-lightgrey?style=for-the-badge" alt="English README"></a>
</p>

[![CI](https://github.com/kr0nsch/AppBridge/actions/workflows/ci.yml/badge.svg)](https://github.com/kr0nsch/AppBridge/actions/workflows/ci.yml)
![Windows 10 и 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4)
[![Лицензия MIT](https://img.shields.io/github/license/kr0nsch/AppBridge?color=34c759)](LICENSE)

Выберите приложения на старом iPhone и установите их на другой по USB. AppBridge скачает оригинальные приложения у Apple с вашим Apple Account. Нового телефона ещё нет? Сохраните приложения на компьютер сейчас и установите позже. Без джейлбрейка, подписки, рекламы и телеметрии.

> **AppBridge устанавливает приложения, но не переносит их данные.** Переписка, документы и настройки восстанавливаются через iCloud, резервную копию или синхронизацию самого приложения. Скачать получится только то, что Apple ещё выдаёт вашему аккаунту: AppBridge не гарантирует восстановление удалённых приложений и не обходит защиту Apple.

## Скачать и начать

Понадобятся **Windows 10 версии 2004 или новее либо Windows 11**, USB-кабель с передачей данных и **[«Устройства Apple»](https://apps.microsoft.com/detail/9NP83LWLPZ9K)** или iTunes: в них есть драйвер для подключения iPhone. Готовый релиз — для x64; на компьютерах с Arm он работает через эмуляцию Windows 11. .NET и Windows App SDK уже включены.

1. **[Скачайте последний релиз](https://github.com/kr0nsch/AppBridge/releases/latest)** и запустите `AppBridge-<версия>-setup-x64.exe`. Для портативной версии распакуйте ZIP и откройте `AppBridge.exe`.
2. Выберите **«Перенести сейчас»**, подключите и разблокируйте старый iPhone. На его экране нажмите **«Доверять»**.
3. Отметьте нужные приложения и подключите другой iPhone. Можно подключать телефоны по очереди одним кабелем.
4. Войдите в Apple Account в AppBridge и нажмите **«Установить»**. Используйте аккаунт, которому принадлежат приложения; при необходимости войдите с ним в App Store на новом iPhone.

**Нового iPhone ещё нет?** Выберите **«Перенести потом → Сохранить приложения»**, подключите старый телефон, отметьте приложения и войдите в Apple Account, чтобы скачать их. Когда новый телефон появится, откройте **«Перенести потом → Установить на новый iPhone»**: сохранённые `.ipa` можно устанавливать без входа в Apple Account на компьютере.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/demo-ru-dark.gif">
    <img src="docs/media/demo-ru-light.gif" width="760" alt="Демонстрация AppBridge: выбор iPhone и приложений, вход в аккаунт, установка">
  </picture>
  <br>
  <sub>Запись демонстрационного режима: телефоны и приложения смоделированы.</sub>
</p>

<details>
<summary><b>Установщик, портативная версия и проверка загрузки</b></summary>

| Файл релиза | Для чего |
|---|---|
| `AppBridge-<версия>-setup-x64.exe` | Обычная установка для текущего пользователя с ярлыком в меню «Пуск». Права администратора не нужны. |
| `AppBridge-<версия>-portable-x64.zip` | Запуск из распакованной папки. Настройки и сохранённые приложения остаются в `Data` рядом с `AppBridge.exe`. |
| `SHA256SUMS.txt` | Контрольные суммы SHA-256 для проверки скачанных файлов. |

Релиз собирается из исходного кода соответствующего тега в GitHub Actions. Процесс сборки создаёт подпись происхождения Sigstore. Для проверки файла через GitHub CLI:

```powershell
gh attestation verify <скачанный-файл> -R kr0nsch/AppBridge
```

Подпись происхождения сборки и подпись издателя Windows — разные вещи. Если SmartScreen показывает неизвестного издателя, перед запуском проверьте, что файл скачан со страницы Releases этого репозитория. Подробнее — в [SECURITY.md](SECURITY.md).

</details>

## Возможности

| | Как помогает AppBridge |
|---|---|
| **Перенос выбранных приложений** | Поиск по приложениям из App Store на старом iPhone и выбор того, что установить на другой. |
| **Сохранение на потом** | Сохранение `.ipa` с лицензией на компьютере, пока старый телефон под рукой. Библиотека проверяет требования к iOS и выбирает совместимую сохранённую версию, если она есть. |
| **Выбор версии** | Последняя из App Store или версия со старого iPhone, пока Apple ещё позволяет её скачать. |
| **Прогресс и повтор** | Скачивание и установка идут параллельно. По каждому приложению виден результат; неудачные установки можно повторить с экрана результатов. |
| **Получение бесплатных приложений** | Можно разрешить AppBridge добавлять бесплатные приложения в покупки вашего Apple Account. Платные должны быть уже куплены. |
| **Удобный интерфейс** | Русский и английский язык, светлая и тёмная тема, установщик и портативная версия. |

AppBridge скачивает приложения у Apple, а не извлекает установленные пакеты из старого iPhone. Удалённое приложение получится скачать, только пока Apple выдаёт его вашему аккаунту. TestFlight и сборки разработчиков не входят в этот сценарий. Сохранённые приложения тоже сохраняют ограничения Apple по аккаунту и совместимости устройства.

<details>
<summary><b>«Перенести потом» и скриншоты</b></summary>

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/later-ru-dark.gif">
    <img src="docs/media/later-ru-light.gif" width="760" alt="Перенести потом: выбор сохранённых приложений и установка на новый iPhone">
  </picture>
  <br>
  <sub>Запись демонстрационного режима: телефоны и приложения смоделированы.</sub>
</p>

| Выбор приложений | Прогресс установки | Сохранённые приложения |
|:---:|:---:|:---:|
| ![Выбор приложений](docs/screenshots/ru-light/2-choose-apps.png) | ![Прогресс установки](docs/screenshots/ru-light/6-installing.png) | ![Сохранённые приложения](docs/screenshots/ru-light/later-4-saved.png) |

Другие скриншоты: [светлая тема](docs/screenshots/ru-light) · [тёмная тема](docs/screenshots/ru-dark).

</details>

## Конфиденциальность и Apple Account

- Вход выполняет встроенный инструмент с открытым кодом [ipatool](https://github.com/majd/ipatool): он обращается к Apple. AppBridge не сохраняет ваш пароль.
- Сохранённая сессия App Store зашифрована. Её случайный ключ защищён Windows DPAPI для вашего пользователя Windows. После переноса портативной копии на другой компьютер или в другую учётную запись потребуется войти заново.
- У AppBridge нет своего сервера, рекламы и телеметрии. Приложения скачиваются у Apple и остаются привязанными к аккаунту покупателя. Защита FairPlay не обходится.
- В скачанных `.ipa` может находиться адрес почты аккаунта покупателя. Не публикуйте эти файлы.

В [SECURITY.md](SECURITY.md) описаны работа с секретами и приватная отправка сообщений об уязвимостях. Там же объяснено ограничение: во время входа пароль виден в командной строке ipatool другим процессам того же пользователя Windows.

<details>
<summary><b>Где хранятся файлы</b></summary>

| Данные | Установщик | Портативная версия |
|---|---|---|
| Настройки, логи, сессия App Store | `%LOCALAPPDATA%\AppBridge` | `Data\` рядом с `AppBridge.exe` |
| Сохранённые приложения (`.ipa`) | `Загрузки\AppBridge` | `Data\Downloads` |
| Кэш иконок | `%LOCALAPPDATA%\AppBridge\Icons` | `Data\Icons` |

Папку сохранённых приложений можно изменить в настройках. Для установки сохранённого приложения не нужен вход в App Store на компьютере; на новом iPhone нужен аккаунт с правом запуска этого приложения.

</details>

## Если что-то не работает

| Проблема | Что попробовать |
|---|---|
| iPhone не виден | Установите «Устройства Apple» или iTunes, разблокируйте телефон, проверьте кабель с передачей данных и другой USB-порт. |
| Ожидание доверия | Не блокируйте iPhone и подтвердите запрос «Доверять». Если отказали, нажмите «Повторить» или подключите кабель заново. |
| Приложения нет в покупках | Войдите в аккаунт, которому оно принадлежит, или разрешите получение бесплатных приложений. |
| Приложение недоступно или несовместимо | Проверьте доступность для аккаунта и версию iOS нового телефона. Сохранение файла не снимает ограничения Apple. |
| Другая ошибка | Откройте **«Настройки → Диагностические логи → Показать»** и [сообщите о баге](https://github.com/kr0nsch/AppBridge/issues/new/choose). Укажите версию AppBridge, модели iPhone, версии iOS и шаги воспроизведения. Перед публикацией проверьте вложения на личные данные. |

В репозитории зафиксирован такой тест на реальных устройствах. Совместимость со всеми моделями, версиями iOS и приложениями пока не проверена:

| Откуда | Куда | Приложения | Результат |
|---|---|---|---|
| iPhone 16 Pro | iPhone 14 | Сбер, VK, Почта Mail.ru | Перенесены |

Проверили другую комбинацию? [Отправьте результат проверки совместимости](https://github.com/kr0nsch/AppBridge/issues/new?template=compatibility_report.yml) с версиями iOS обоих телефонов и версией AppBridge — это поможет расширить список проверенных устройств.

## Сборка и участие в проекте

Нужны Windows и [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), указанный в [global.json](global.json).

```powershell
git clone https://github.com/kr0nsch/AppBridge.git
cd AppBridge

# Скачать закреплённую версию ipatool с проверкой контрольной суммы.
./build/Get-Ipatool.ps1

dotnet test tests/AppBridge.Core.Tests
dotnet run --project src/AppBridge -p:Platform=x64

# Посмотреть интерфейс без iPhone и Apple Account.
dotnet run --project src/AppBridge -p:Platform=x64 -- --demo
```

Параметр `--demo-saved` добавляет демонстрационную библиотеку сохранённых приложений. Для сборки релиза установите [Inno Setup 6](https://jrsoftware.org/isinfo.php) и запустите `./build/build.ps1 -Version 1.1.0`. Установщик, портативный ZIP и контрольные суммы появятся в `artifacts/release`.

Проекту полезны воспроизводимые отчёты об ошибках, [результаты проверок iPhone/iOS](https://github.com/kr0nsch/AppBridge/issues/new?template=compatibility_report.yml), улучшения текстов ошибок, доступности и переводов. Начните с [CONTRIBUTING.md](CONTRIBUTING.md), посмотрите [открытые задачи](https://github.com/kr0nsch/AppBridge/issues), [предложите улучшение](https://github.com/kr0nsch/AppBridge/issues/new?template=feature_request.yml) или прочитайте [историю изменений](CHANGELOG.md).

<details>
<summary><b>Структура проекта и зависимости</b></summary>

| Путь | Содержимое |
|---|---|
| `src/AppBridge` | Интерфейс WinUI, модели представления, темы и переводы |
| `src/AppBridge.Core` | Доступ к iPhone по USB, работа с App Store, библиотека приложений и перенос |
| `tests/AppBridge.Core.Tests` | Тесты ядра и интеграции с ipatool |
| `build`, `installer` | Сборка, закреплённые версии зависимостей и установщик Inno Setup |
| `legacy/python` | Исходный прототип на Python, больше не поддерживается |

Используются [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml), [Netimobiledevice](https://github.com/artehe/Netimobiledevice) и [ipatool](https://github.com/majd/ipatool). Их лицензии — в [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

</details>

**Если AppBridge помог перенести приложения, поставьте [звезду на GitHub](https://github.com/kr0nsch/AppBridge) — так о нём узнают другие.** Отчёт о баге или проверка на своих устройствах тоже очень помогают.

[Лицензия MIT](LICENSE). AppBridge — независимый проект, не связанный с Apple Inc. и не одобренный ею. iPhone и App Store — товарные знаки Apple Inc. Используйте программу только с приложениями, на которые у вас есть право.
