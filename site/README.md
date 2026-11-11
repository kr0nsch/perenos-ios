# Сайт «Переноса»

Двуязычная статическая страница для GitHub Pages: русская `index.html`, английская
`en.html`, общие `styles.css` и небольшой `app.js`. Никакого серверного кода,
обязательной аналитики, внешних шрифтов или скриптов.

## Локальная сборка и проверка

Нужны Python 3.11+, Node.js и npm. Из корня репозитория:

```powershell
npm ci
npm run dev
```

Vite покажет адрес (обычно `http://localhost:5173/`). Сборка помещает готовый сайт
в `artifacts/site` и подпапку `perenos-ios` для проверки базового пути GitHub Pages.
Предпросмотр под этим путём: `python site/build.py --output artifacts/preview/perenos-ios`
и `npx vite artifacts/preview`. Не собирайте повторно в каталог, который уже обслуживает Vite.

`site/build.py` использует только стандартную библиотеку Python. Он копирует HTML,
CSS, JavaScript, локальные шрифты и актуальные снимки Avalonia из
`docs/screenshots/avalonia`, проверяет локальные ссылки и якоря, создаёт `robots.txt`,
`sitemap.xml` и `.nojekyll`. Старые GIF не используются.

## Проверки

```powershell
npm run test:site
```

Тесты запускаются в установленном Microsoft Edge и проверяют пять ширин, обе темы,
переключение сценариев с клавиатуры, русские и английские изображения, переходы между
языками, JSON-LD, ссылки загрузки и работу без JavaScript. Скриншоты попадают в
`artifacts/site-check`, JSON-отчёт — в `artifacts/site-test-results.json`.

Ссылки загрузки по умолчанию ведут на Releases. Если GitHub API доступен, кнопки Windows
получают прямые URL последнего релиза. Кнопки macOS Apple Silicon и Intel остаются с
честной пометкой подготовки бета-версии, пока в релизе нет соответствующих DMG.
Принимаются только HTTPS URL файлов из `github.com/kr0nsch/perenos-ios/releases/download/`.

## Превью ссылки

`social-card.svg` — исходник обложки 1200 × 630, `social-card.png` — готовое изображение.
После изменения SVG перерендерите PNG в браузере с масштабом 1 и проверьте текст и поля.
Публикацией управляет `.github/workflows/pages.yml`.
