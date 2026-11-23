"""Публикует пост о релизе в официальный канал @perenos_ios.

Запуск (workflow Release делает это сам после публикации релиза по тегу):
  python scripts/telegram/publish_release_post.py --version 1.10.0
Без --version берётся первый выпущенный релиз из CHANGELOG.md. Токен:
TELEGRAM_BOT_TOKEN или локальный «Текстовый документ.txt»; без токена скрипт
мягко пропускает публикацию. Идемпотентно по published-releases.json (CI
коммитит этот маркер вместе со строкой статистики). --dry-run печатает пост
без отправки, --selftest проверяет разбор CHANGELOG на примере.
"""

import argparse
import json
import re
import sys
from datetime import datetime
from pathlib import Path

from publish_posts import release_post
from tglib import ROOT, api

HERE = Path(__file__).resolve().parent
MARKER = HERE / "published-releases.json"
CHANNEL = "@perenos_ios"
MONTHS = ["января", "февраля", "марта", "апреля", "мая", "июня",
          "июля", "августа", "сентября", "октября", "ноября", "декабря"]
RU_HEADERS = {"Изменено", "Добавлено", "Исправлено", "Проверено",
              "Документация и сайт", "Что исправлено", "Имя «Перенос»", "Важно"}
EN_HEADERS = {"Changed", "Added", "Fixed", "Verified", "Note",
              "Documentation and website", "Verification and known limitations"}


def release_body(text: str, version: str):
    match = re.search(
        rf"^## \[{re.escape(version)}\] - (\d{{4}}-\d{{2}}-\d{{2}})\n(.*?)(?=^## \[|^\[\d|\Z)",
        text, re.M | re.S)
    if not match:
        return None, None
    return match.group(1), match.group(2)


def ru_bullets(body: str, limit: int = 8, width: int = 350) -> list[str]:
    bullets, in_ru = [], False
    for line in body.split("\n"):
        if line.startswith("### "):
            header = line[4:].strip()
            in_ru = header in RU_HEADERS
            continue
        if line.startswith("## "):
            break
        if in_ru and line.startswith("- "):
            item = " ".join(line[2:].split())
            bullets.append(item if len(item) <= width else item[:width - 1] + "…")
            if len(bullets) >= limit:
                break
    return bullets


def build_post(text: str, version: str) -> str:
    date_iso, body = release_body(text, version)
    if not date_iso:
        raise SystemExit(f"version {version} not found in CHANGELOG.md")
    date = datetime.strptime(date_iso, "%Y-%m-%d")
    date_ru = f"{date.day} {MONTHS[date.month - 1]} {date.year}"
    emoji = "🎉" if version == "1.0.0" else ("🚀" if version.endswith(".0") else "🔧")
    bullets = ru_bullets(body) or ["Подробности — в полном changelog релиза."]
    return release_post(version, date_ru, emoji, bullets)["text"]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--selftest", action="store_true")
    args = parser.parse_args()
    if args.selftest:
        selftest()
        return

    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    version = args.version or re.search(r"^## \[(\d+\.\d+\.\d+)\]", text, re.M).group(1)
    marker = json.loads(MARKER.read_text(encoding="utf-8")) if MARKER.exists() else []
    if f"v{version}" in marker:
        print(f"v{version}: Telegram post already published, skipped")
        return
    post = build_post(text, version)
    if args.dry_run:
        print(post)
        return
    try:
        from tglib import token
        token()
    except (OSError, ValueError):
        print("TELEGRAM_BOT_TOKEN not available; skipping Telegram post")
        return
    chat_id = api("getChat", chat_id=CHANNEL)["id"]
    result = api("sendMessage", chat_id=chat_id, text=post, parse_mode="HTML",
                 link_preview_options={"is_disabled": True})
    marker.append(f"v{version}")
    MARKER.write_text(json.dumps(marker, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"v{version}: Telegram post published, message_id={result['message_id']}")


def selftest() -> None:
    sample = (
        "# Changelog\n\n## [Unreleased]\n\n## [1.10.0] - 2026-12-01\n\n"
        "### Добавлено\n- Новая функция переноса.\n- Ещё одна функция.\n\n"
        "### Changed\n- English only bullet.\n\n"
        "## [1.9.0] - 2026-11-19\n\n### Изменено\n- Старое изменение.\n\n"
        "[1.10.0]: https://github.com/kr0nsch/perenos-ios/releases/tag/v1.10.0\n"
    )
    date_iso, body = release_body(sample, "1.10.0")
    assert date_iso == "2026-12-01", "date parse"
    bullets = ru_bullets(body)
    assert bullets == ["Новая функция переноса.", "Ещё одна функция."], f"RU-only bullets: {bullets}"
    assert ru_bullets("### Изменено\n- " + "длинный " * 100)[0].endswith("…"), "width cap"
    post = build_post(sample, "1.10.0")
    assert "#v1_10_0" in post and "1 декабря 2026" in post and "🚀" in post, "post format"
    assert "English only" not in post, "no EN bullets"
    assert release_body(sample, "9.9.9") == (None, None), "missing version"
    print("SELFTEST PASS: разбор секции, только RU-пункты, обрезка, формат поста")


if __name__ == "__main__":
    main()