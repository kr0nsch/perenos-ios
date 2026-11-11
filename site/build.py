"""Package the bilingual Perenos website for GitHub Pages using Python's stdlib."""
from __future__ import annotations

import argparse
from html.parser import HTMLParser
from pathlib import Path
import shutil
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parent.parent
SITE = ROOT / "site"
CANONICAL = "https://kr0nsch.github.io/perenos-ios/"


class LocalReferences(HTMLParser):
    def __init__(self):
        super().__init__()
        self.paths: set[str] = set()
        self.ids: set[str] = set()
        self.anchors: set[str] = set()

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "id" in attrs:
            if attrs["id"] in self.ids:
                raise ValueError(f"Duplicate HTML id: {attrs['id']}")
            self.ids.add(attrs["id"])
        for attr in ("href", "src", "srcset"):
            value = attrs.get(attr, "")
            if not value:
                continue
            if value.startswith("#"):
                if len(value) > 1:
                    self.anchors.add(value[1:])
                continue
            if not urlsplit(value).scheme:
                self.paths.add(value)


def build(output: Path) -> None:
    output = output.resolve()
    artifacts = ROOT / "artifacts"
    if artifacts.resolve() != artifacts or output == artifacts or not output.is_relative_to(artifacts):
        raise ValueError("Output must be strictly inside the repository's artifacts directory")
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)
    (output / "en").mkdir()
    shutil.copy2(SITE / "index.html", output / "index.html")
    shutil.copy2(SITE / "en.html", output / "en" / "index.html")
    for name in ("styles.css", "app.js"):
        shutil.copy2(SITE / name, output / name)
    assets = output / "assets"
    assets.mkdir()
    for name in ("apps", "restore", "saved"):
        for language, suffix in (("ru-RU", ""), ("en-US", "-en")):
            for theme in ("light", "dark"):
                source = ROOT / "docs" / "screenshots" / "avalonia" / f"{name}-{language}-{theme.capitalize()}.png"
                if not source.is_file():
                    raise FileNotFoundError(f"Missing current Avalonia screenshot: {source}")
                shutil.copy2(source, assets / f"{name}{suffix}-{theme}.png")
    fonts = assets / "fonts"
    fonts.mkdir()
    for source in sorted((SITE / "fonts").iterdir()):
        if source.is_file():
            shutil.copy2(source, fonts / source.name)
    shutil.copy2(ROOT / "assets" / "logo.png", assets / "logo.png")
    shutil.copy2(SITE / "social-card.svg", assets / "social-card.svg")
    shutil.copy2(SITE / "social-card.png", assets / "social.png")
    (output / ".nojekyll").write_text("", encoding="utf-8")
    (output / "robots.txt").write_text(f"User-agent: *\nAllow: /\nSitemap: {CANONICAL}sitemap.xml\n", encoding="utf-8")
    (output / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">'
        f"<url><loc>{CANONICAL}</loc></url><url><loc>{CANONICAL}en/</loc></url></urlset>\n", encoding="utf-8")
    for relative in (Path("index.html"), Path("en/index.html")):
        references = LocalReferences()
        references.feed((output / relative).read_text(encoding="utf-8"))
        missing = []
        for path in references.paths:
            candidate = output / relative.parent / path
            if candidate.is_dir():
                candidate = candidate / "index.html"
            if not candidate.is_file():
                missing.append(path)
        missing_anchors = references.anchors - references.ids
        if missing or missing_anchors:
            raise ValueError(f"Broken references in {relative}: {missing}; anchors: {sorted(missing_anchors)}")
        print(f"Validated {relative}: {len(references.paths)} local references, {len(references.anchors)} anchors")
    total = sum(path.stat().st_size for path in output.rglob("*") if path.is_file())
    print(f"GitHub Pages files: {output} ({total / 1048576:.1f} MiB)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts" / "site")
    args = parser.parse_args()
    build(args.output)
