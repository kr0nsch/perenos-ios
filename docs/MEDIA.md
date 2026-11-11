# Скриншоты и демонстрации «Переноса»

В README и на сайте используются **реальные кадры текущего Avalonia-интерфейса в
деморежиме**. Телефоны, аккаунт и приложения вымышлены. Кадры показывают логику
сценариев, а не скорость или совместимость с настоящими устройствами.

## Правила

- Не входите в настоящий Apple Account и не подключайте iPhone для съёмки.
- Не помещайте в кадр пароли, настоящую почту, полные UDID, личные IPA или системные
  окна с личными данными. Не выкладывайте настройки с путями домашних папок.
- Не дорисовывайте курсор, галочки, иконки и состояние приложения. Снимайте окно заново
  после изменения интерфейса. Старые WinUI-кадры и GIF для текущего сайта не используются.
- Каждая серия снимается с одинаковым размером окна. Проверяйте текст, переполнения,
  контраст, видимую галочку, версии, иконки и отсутствие случайных меню.

## Автоматическая серия

Сначала соберите Release на Windows:

```powershell
dotnet build src/Perenos/Perenos.csproj -c Release
```

Затем используйте [`build/Demo-Check.ps1`](../build/Demo-Check.ps1). Он запускает
`Perenos.exe --demo` по конкретному PID и закрывает только этот процесс. Каждый запуск
использует отдельную папку `%TEMP%\PerenosDemo-<PID>`: настройки, журналы, библиотека,
файлы блокировки и сессия установленного приложения не затрагиваются.

```powershell
# Главный экран
./build/Demo-Check.ps1 -Mode Start -Language ru-RU -Theme Light

# Список и видимый выбор приложения
./build/Demo-Check.ps1 -Mode Selection -Language ru-RU -Theme Dark

# Настройки, смена темы и языка без перезапуска
./build/Demo-Check.ps1 -Mode Settings -Language ru-RU -Theme Light

# Полный перенос, восстановление, сохранение и установка из библиотеки
./build/Demo-Check.ps1 -Mode Full -Language en-US -Theme Dark

# Тот же сценарий при минимальном размере окна и компактной навигации
./build/Demo-Check.ps1 -Mode Full -Language en-US -Theme Light -Width 820 -Height 640
```

Допустимые языки: `ru-RU`, `en-US`; темы: `Light`, `Dark`. Для четырёх сопоставимых серий
выполните `-Mode Full` на `ru-RU/Light`, `ru-RU/Dark`, `en-US/Light`, `en-US/Dark`.
Тесты запускайте последовательно: несколько независимых UI Automation-прогонов на одном
рабочем столе могут мешать друг другу.

Файлы попадают в `artifacts/avalonia-review` (или выбранный `-OutDir` внутри `artifacts`;
путь может быть относительным или абсолютным). Для сайта нужны `apps`, `restore`, `saved` на обоих
языках и в обеих темах. Полный сценарий также сохраняет контрольные кадры `devices`, `target`,
`apps-all`, `install-sign-in`, `install-options`, `installing`, `restore-all`, `restore-russian`,
`restore-install`, `restore-done`, `save-apps`, `save-ready` и `library`: они нужны для проверки
компоновки и точных временных меток. После визуальной проверки обновите существующие снимки
в `docs/screenshots/avalonia`:

```powershell
New-Item -ItemType Directory -Force docs/screenshots/avalonia
foreach ($variant in 'ru-light', 'ru-dark', 'en-light', 'en-dark') {
  $source = "artifacts/avalonia-review/$variant"
  Copy-Item "$source/start-*.png", "$source/apps-*.png", "$source/install-*.png",
    "$source/restore-*.png", "$source/saved-*.png" -Destination docs/screenshots/avalonia
}
Copy-Item artifacts/avalonia-review/settings/*.png -Destination docs/screenshots/avalonia
```

Это заменяет только сгенерированные кадры с теми же именами. Сборка сайта не принимает
отсутствующие файлы. `start-*` и `install-*` можно дополнительно использовать в документации.

## Демоданные

Два вымышленных iPhone появляются через несколько секунд. Дождитесь доверия нового телефона
и загрузки иконок. Для продолжения входа используются только вымышленные адрес, пароль и код
`123456`; ничего не отправляется Apple.

Демо запоминает установки до конца запуска. Поэтому восстановление, сохранение и установка
библиотеки влияют на следующие списки. `--demo-saved` дополнительно создаёт 11 сохранённых
файлов, один из которых несовместим с демонстрационной iOS.

## Запись реальных анимаций

Для записи без курсора используйте [`Record-DemoWindow.ps1`](../build/Record-DemoWindow.ps1).
Она работает только с явно изолированной portable-копией `Perenos.exe`, запущенной с `--demo`
или `--demo-saved`, и файлом `portable.txt` рядом с ней. Скрипт проверяет PID и не запускает,
не закрывает и не управляет приложением.

`Demo-Check.ps1 -ProcessId` может управлять уже запущенным демо: он проверяет путь к
исполняемому файлу, флаг деморежима и portable-маркер. Чужой процесс при этом не закрывается.
Запускайте запись и сценарий с одним PID, а после сценария остановите запись файлом `stop`:

```powershell
$env:PERENOS_LANGUAGE = 'ru-RU'
$env:PERENOS_THEME = 'Light'
$demo = Start-Process <путь-к-portable-Perenos.exe> -ArgumentList '--demo' -PassThru
$recorder = (Resolve-Path ./build/Record-DemoWindow.ps1).Path
$recordDir = 'artifacts/recordings-1.7.2/ru-light/demo'
$recordJob = Start-Job {
  & $using:recorder -ProcessId $using:demo.Id -OutDir $using:recordDir -IntervalMs 90 -MaxSeconds 240
}
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while (-not (Test-Path "$recordDir/f00000.png")) {
  if ($recordJob.State -notin 'NotStarted', 'Running' -or [DateTime]::UtcNow -gt $deadline) {
    Receive-Job $recordJob
    throw 'Запись не началась.'
  }
  Start-Sleep -Milliseconds 200
}
./build/Demo-Check.ps1 -Mode Full -Language ru-RU -Theme Light `
  -ProcessId $demo.Id -ExePath <путь-к-portable-Perenos.exe> -OutDir artifacts/shots-1.7.2/ru-light
New-Item -ItemType File -Path "$recordDir/stop" -Force
Wait-Job $recordJob -Timeout 30
Receive-Job $recordJob
Remove-Job $recordJob
```

Дождитесь `"status": "complete"` в `capture.json`, затем закройте только это демо и удалите
его временную папку. Не завершайте процессы по имени: у пользователя может работать другая копия.
Диалоги Avalonia могут быть отдельными нативными окнами, поэтому для них нужен снимок экранной
области, а не только `PrintWindow` владельца. Не показывайте личные данные и другие окна.

## Сборка GIF и видео

Сначала создайте временные шкалы по меткам снимков `Demo-Check`, затем соберите GIF только
из записанных кадров. Выбор целевого iPhone, готовность, установка и завершение привязаны к
фактическим состояниям интерфейса, а не к предполагаемой длительности операций. Версия берётся
из `Directory.Build.props`.

```bash
python build/New-DemoTimeline.py --recordings-dir artifacts/recordings-1.7.2 --shots-dir artifacts/shots-1.7.2
python build/generate-demo-media.py --recordings-dir artifacts/recordings-1.7.2 --languages ru en --themes light dark --flows demo restore save --width 960 --fps 8
```

Для обработки только одной серии исходников добавьте `--variants ru-light` (или `en-light`).
Существующие временные шкалы и папки `restore`/`save` не перезаписываются: для новой сборки
используйте новый каталог записи. Для сайта нужны все 12 GIF из команды выше.

`New-DemoTimeline.py` не редактирует исходные PNG. Для `restore` и `save` он создаёт
отдельные папки с жёсткими ссылками или копиями кадров и собственной `timeline.json`.
`generate-demo-media.py` проверяет завершённую запись, размеры, версии и хеши всех кадров.
Полученные `docs/media/*.json` содержат версию, длительность, размеры и хеши источника.
Не дорисовывайте интерфейс, не добавляйте курсор или вымышленные состояния.

Для полного MP4 можно использовать исходную последовательность PNG без повторного рендера UI:

```bash
ffmpeg -framerate 12 -i artifacts/recordings-1.7.2/ru-light/demo/f%05d.png -vf scale=960:-2 -c:v libx264 -crf 24 -pix_fmt yuv420p -movflags +faststart docs/media/full-ru-light.mp4
```

Повторите команды для `en-light`, чтобы получить английские материалы. `site/build.py` копирует
GIF и MP4 в публикуемые assets и не принимает отсутствующие файлы. Анимация на сайте имеет
кнопку паузы, а `prefers-reduced-motion` по умолчанию показывает статичный кадр. Без JavaScript
остаются обычные изображения и текст. Полное видео запускается только пользователем.

## Публикация

Перед сборкой `python site/build.py` проверьте PNG в обеих темах и убедитесь, что язык кадра
соответствует имени файла. Сайт берёт русские изображения из `*-ru-RU-*` без суффикса и
английские изображения из `*-en-US-*` с суффиксом `-en`.

Браузерные проверки `npm run test:site` обязательны: они проверяют мобильную вёрстку,
контрастные темы, локальные изображения, переключение языков, клавиатурную навигацию,
паузу анимации и reduced motion, а также работу без JavaScript. Headless-тесты Avalonia на
macOS проверяют логику и компоновку, но не заменяют запуск DMG и проверку USB на настоящем
Mac и iPhone.
