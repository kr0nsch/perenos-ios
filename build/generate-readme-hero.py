"""Generate editable, dependency-free SVG covers for the Perenos READMEs."""
from html import escape
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
COPY = {
    "ru": {
        "brand": "Перенос", "eyebrow": "ВЕРСИЯ 1.5.0  ·  WINDOWS 10 / 11",
        "line1": "Новый iPhone.", "line2": "Ваши приложения.",
        "caption": "Перенесите. Обновите. Восстановите свои покупки.",
        "meta": "БЕСПЛАТНО  ·  ОТКРЫТЫЙ КОД  ·  БЕЗ ДЖЕЙЛБРЕЙКА",
        "window": "Обновить и восстановить", "count": "0 выбрано",
        "search": "Поиск приложений", "filter": "Все покупки",
        "route": "APPLE ACCOUNT   →   WINDOWS   →   ВАШ IPHONE",
        "badge": "НОВОЕ: ВОССТАНОВЛЕНИЕ ПОКУПОК",
        "apps": (("Bloom Journal", "Ваши покупки App Store"), ("Cloudy", "Последняя доступная версия"), ("Daily Planner", "Установка на ваш iPhone")),
    },
    "en": {
        "brand": "Perenos", "eyebrow": "VERSION 1.5.0  ·  WINDOWS 10 / 11",
        "line1": "New iPhone.", "line2": "Your apps.",
        "caption": "Transfer. Update. Restore your purchases.",
        "meta": "FREE  ·  OPEN SOURCE  ·  NO JAILBREAK",
        "window": "Update & Restore", "count": "0 selected",
        "search": "Search apps", "filter": "All purchases",
        "route": "APPLE ACCOUNT   →   WINDOWS   →   YOUR IPHONE",
        "badge": "NEW: RESTORE YOUR PURCHASES",
        "apps": (("Bloom Journal", "Your App Store purchases"), ("Cloudy", "Latest available version"), ("Daily Planner", "Install on your iPhone")),
    },
}


def logo(x, y, size=48):
    scale = size / 48
    return f'''<g transform="translate({x} {y}) scale({scale})"><rect width="48" height="48" rx="13" fill="#2563eb"/>
    <g fill="none" stroke="white" stroke-width="2.7" stroke-linecap="round" stroke-linejoin="round"><path d="M23 13V9a3 3 0 0 1 3-3h10a3 3 0 0 1 3 3v27a3 3 0 0 1-3 3h-7M17 35v4a3 3 0 0 1-3 3H8a3 3 0 0 1-3-3V16a3 3 0 0 1 3-3h6"/>
    <path d="M15 25h18m-6-6 6 6-6 6"/></g></g>'''


def app_rows(apps, dark):
    rows = []
    colors = ("#4d8ef7", "#32a5b8", "#ee9b43")
    for i, ((name, subtitle), color) in enumerate(zip(apps, colors)):
        y = 238 + i * 59
        rows.append(f'''<g><rect x="708" y="{y}" width="412" height="49" rx="9" fill="var(--row)"/>
        <rect x="721" y="{y+10}" width="28" height="28" rx="8" fill="{color}"/>
        <path d="M729 {y+24}h12m-6-6v12" stroke="white" stroke-width="1.8" stroke-linecap="round"/>
        <text x="761" y="{y+21}" class="app-name">{escape(name)}</text><text x="761" y="{y+36}" class="app-subtitle">{escape(subtitle)}</text>
        <rect x="1081" y="{y+14}" width="20" height="20" rx="6" fill="none" stroke="var(--line)" stroke-width="1.5"/></g>''')
    return "".join(rows)


def render(lang, dark):
    c = COPY[lang]
    colors = {
        "bg": "#101828" if dark else "#f5f8fc", "card": "#1d2939" if dark else "#ffffff",
        "ink": "#f2f4f7" if dark else "#101828", "muted": "#aab6c8" if dark else "#526078",
        "line": "#344054" if dark else "#d9e2ef", "wash": "#162c4b" if dark else "#eaf2ff",
        "row": "#26364b" if dark else "#f8faff", "soft": "#26364b" if dark else "#edf2f8",
    }
    variables = ";".join(f"--{key}:{value}" for key, value in colors.items())
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="500" viewBox="0 0 1200 500" role="img" aria-labelledby="title desc">
<title id="title">{escape(c['brand'])} — {escape(c['line1'])} {escape(c['line2'])}</title><desc id="desc">{escape(c['caption'])} {escape(c['eyebrow'])}</desc>
<style>:root{{{variables}}}text{{font-family:Manrope,'Segoe UI',Arial,sans-serif;fill:var(--ink)}}.mono{{font-family:Consolas,monospace;font-size:11px;letter-spacing:1.1px;fill:var(--muted)}}.app-name{{font-size:12px;font-weight:650}}.app-subtitle{{font-size:9px;fill:var(--muted)}}</style>
<rect width="1200" height="500" rx="24" fill="var(--bg)"/><circle cx="1150" cy="20" r="250" fill="#2563eb" opacity=".055"/>
{logo(52, 42)}<text x="113" y="76" font-size="30" font-weight="750" letter-spacing="-1">{escape(c['brand'])}</text>
<rect x="885" y="49" width="265" height="29" rx="14.5" fill="var(--wash)"/><text x="1017.5" y="68" text-anchor="middle" class="mono" style="fill:#3b82f6;font-size:9px;letter-spacing:.5">{escape(c['badge'])}</text>
<text x="54" y="146" class="mono" style="fill:#3b82f6">{escape(c['eyebrow'])}</text>
<text x="51" y="211" font-size="49" font-weight="700" letter-spacing="-2">{escape(c['line1'])}</text><text x="51" y="269" font-size="49" font-weight="700" letter-spacing="-2" style="fill:#2563eb">{escape(c['line2'])}</text>
<text x="54" y="315" font-size="17" style="fill:var(--muted)">{escape(c['caption'])}</text>
<rect x="54" y="354" width="194" height="45" rx="9" fill="#2563eb"/><text x="151" y="382" text-anchor="middle" font-size="13" font-weight="700" style="fill:white">{escape('Скачать для Windows' if lang == 'ru' else 'Download for Windows')}</text>
<text x="54" y="447" class="mono" style="font-size:10px;letter-spacing:.6">{escape(c['meta'])}</text>
<g><rect x="674" y="108" width="478" height="345" rx="18" fill="var(--wash)"/><rect x="691" y="125" width="444" height="311" rx="11" fill="var(--card)" stroke="var(--line)"/>
<circle cx="713" cy="146" r="3" fill="#ff7c72"/><circle cx="724" cy="146" r="3" fill="#f5c45b"/><circle cx="735" cy="146" r="3" fill="#56c89a"/>
<text x="713" y="177" font-size="15" font-weight="700">{escape(c['window'])}</text><text x="1109" y="176" text-anchor="end" class="mono" style="font-size:9px;letter-spacing:0;fill:#3b82f6">{escape(c['count'])}</text>
<rect x="708" y="191" width="229" height="31" rx="7" fill="var(--soft)"/><text x="724" y="211" class="app-subtitle">{escape(c['search'])}</text>
<rect x="946" y="191" width="174" height="31" rx="7" fill="var(--wash)"/><text x="1033" y="211" text-anchor="middle" class="app-subtitle" style="fill:#3b82f6">{escape(c['filter'])}</text>
{app_rows(c['apps'], dark)}<rect x="990" y="406" width="130" height="17" rx="5" fill="#2563eb"/></g>
<text x="913" y="479" text-anchor="middle" class="mono" style="font-size:9px;letter-spacing:.8">{escape(c['route'])}</text>
</svg>'''


if __name__ == "__main__":
    for lang in COPY:
        for theme in ("light", "dark"):
            path = ROOT / "docs" / "media" / f"hero-{lang}-{theme}.svg"
            path.write_text(render(lang, theme == "dark") + "\n", encoding="utf-8")
            print(path.relative_to(ROOT))
