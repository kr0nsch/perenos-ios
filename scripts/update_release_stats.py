"""Добавляет строку нового релиза в таблицы скачиваний README.md и README.en.md.

Запуск (workflow Release делает это сам после публикации релиза по тегу):
  python scripts/update_release_stats.py --version 1.10.0
Без --version берётся первый выпущенный релиз из CHANGELOG.md; дата — из его
заголовка, иначе текущая дата UTC. Идемпотентно: повторный запуск не дублирует
строку. --selftest проверяет логику на временных копиях, не трогая репозиторий.
"""

import argparse
import re
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SLUG = "kr0nsch/perenos-ios"
REPO = f"https://github.com/{SLUG}"
BADGE = f"https://img.shields.io/github/downloads/{SLUG}"

TABLES = {
    "README.md": ("| Релиз | Дата | Скачивания |", "Скачивания", "%d.%m.%Y"),
    "README.en.md": ("| Release | Date | Downloads |", "Downloads", "%Y-%m-%d"),
}


def changelog_release(text: str, version: str | None = None):
    pattern = rf"^## \[{re.escape(version) if version else r'\d+\.\d+\.\d+'}\] - (\d{{4}}-\d{{2}}-\d{{2}})"
    match = re.search(pattern, text, re.M)
    if not match:
        return None, None
    found = version or re.search(r"^## \[(\d+\.\d+\.\d+)\]", text, re.M).group(1)
    return found, match.group(1)


def add_row(text: str, header: str, alt: str, date_str: str, version: str):
    tag_url = f"{REPO}/releases/tag/v{version}"
    if tag_url in text:
        return text, False
    lines = text.split("\n")
    if header not in lines:
        raise SystemExit(f"statistics table header not found: {header}")
    i = lines.index(header)
    row = (f"| [v{version}]({tag_url}) | {date_str} | "
           f"![{alt} v{version}]({BADGE}/v{version}/total?label=) |")
    lines.insert(i + 2, row)
    return "\n".join(lines), True


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version")
    parser.add_argument("--date", help="ISO date override, e.g. 2026-12-01")
    parser.add_argument("--selftest", action="store_true")
    args = parser.parse_args()
    if args.selftest:
        selftest()
        return

    changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    version, date_iso = changelog_release(changelog, args.version)
    version = args.version or version
    if not version:
        raise SystemExit("no released version found in CHANGELOG.md; pass --version")
    if args.date:
        date_iso = args.date
    if not date_iso:
        date_iso = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    date = datetime.strptime(date_iso, "%Y-%m-%d")

    for name, (header, alt, fmt) in TABLES.items():
        path = ROOT / name
        text = path.read_text(encoding="utf-8")
        updated, changed = add_row(text, header, alt, date.strftime(fmt), version)
        if changed:
            path.write_text(updated, encoding="utf-8")
            print(f"{name}: added v{version} row ({date.strftime(fmt)})")
        else:
            print(f"{name}: v{version} row already present, skipped")


def selftest() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        (root / "CHANGELOG.md").write_text(
            "# Changelog\n\n## [Unreleased]\n\n## [1.10.0] - 2026-12-01\n\n- change\n\n"
            "## [1.9.0] - 2026-11-19\n\n- old\n", encoding="utf-8")
        for name, (header, _, _) in TABLES.items():
            (root / name).write_text(
                f"# T\n\n## Stats\n\n{header}\n|---|---|---|\n"
                f"| [v1.9.0]({REPO}/releases/tag/v1.9.0) | x | badge |\n", encoding="utf-8")

        version, date_iso = changelog_release((root / "CHANGELOG.md").read_text(encoding="utf-8"))
        assert (version, date_iso) == ("1.10.0", "2026-12-01"), "changelog parse"
        assert changelog_release((root / "CHANGELOG.md").read_text(encoding="utf-8"), "1.9.0") == ("1.9.0", "2026-11-19")
        assert changelog_release("# Changelog\n\n## [Unreleased]\n", "2.0.0") == (None, None)

        date = datetime.strptime(date_iso, "%Y-%m-%d")
        for name, (header, alt, fmt) in TABLES.items():
            path = root / name
            text = path.read_text(encoding="utf-8")
            updated, changed = add_row(text, header, alt, date.strftime(fmt), version)
            assert changed, "row inserted"
            lines = updated.split("\n")
            i = lines.index(header)
            assert f"v{version}" in lines[i + 2], "newest row first"
            assert date.strftime(fmt) in lines[i + 2], "date format"
            assert f"{BADGE}/v{version}/total?label=" in lines[i + 2], "badge url"
            again, changed2 = add_row(updated, header, alt, date.strftime(fmt), version)
            assert not changed2 and again == updated, "idempotent"
            path.write_text(updated, encoding="utf-8")
        print("SELFTEST PASS: парсинг CHANGELOG, вставка строки, формат даты, идемпотентность")


if __name__ == "__main__":
    main()