"""Package the Perenos landing page for GitHub Pages using only Python's stdlib."""
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
    assets = output / "assets"
    assets.mkdir(exist_ok=True)
    for name in ("index.html", "styles.css", "app.js"):
        shutil.copy2(SITE / name, output / name)
    image_sources = (
        ("apps", "2-choose-apps.png"),
        ("saved", "later-4-saved.png"),
        ("unavailable", "unavailable-filter.png"),
        ("restore", "restore-purchases.png"),
        ("target", "3-target.png"),
        ("save-done", "save-3-done.png"),
    )
    for theme in ("light", "dark"):
        for name, source in image_sources:
            shutil.copy2(ROOT / "docs" / "screenshots" / f"ru-{theme}" / source, assets / f"{name}-{theme}.png")
        for name in ("demo", "later"):
            shutil.copy2(ROOT / "docs" / "media" / f"{name}-ru-{theme}.gif", assets / f"{name}-{theme}.gif")
    fonts = assets / "fonts"
    fonts.mkdir()
    for source in sorted((SITE / "fonts").iterdir()):
        if source.is_file():
            shutil.copy2(source, fonts / source.name)
    shutil.copy2(ROOT / "assets" / "logo.png", assets / "logo.png")
    shutil.copy2(SITE / "social-card.svg", assets / "social-card.svg")
    social_card = SITE / "social-card.png"
    if not social_card.is_file():
        social_card = ROOT / "docs" / "screenshots" / "ru-light" / "2-choose-apps.png"
    shutil.copy2(social_card, assets / "social.png")
    (output / ".nojekyll").write_text("", encoding="utf-8")
    (output / "robots.txt").write_text(f"User-agent: *\nAllow: /\nSitemap: {CANONICAL}sitemap.xml\n", encoding="utf-8")
    (output / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">'
        f"<url><loc>{CANONICAL}</loc></url></urlset>\n", encoding="utf-8")
    references = LocalReferences()
    references.feed((output / "index.html").read_text(encoding="utf-8"))
    missing = [path for path in references.paths if not (output / path).is_file()]
    missing_anchors = references.anchors - references.ids
    if missing or missing_anchors:
        raise ValueError(f"Broken references: {missing}; anchors: {sorted(missing_anchors)}")
    total = sum(path.stat().st_size for path in output.rglob("*") if path.is_file())
    print(f"GitHub Pages files: {output}")
    print(f"Validated {len(references.paths)} local references and {len(references.anchors)} anchors; {total / 1048576:.1f} MiB including on-demand GIFs")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts" / "site")
    args = parser.parse_args()
    build(args.output)
