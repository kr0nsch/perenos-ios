"""Generate editable, dependency-free SVG covers for the Perenos READMEs."""
from pathlib import Path
from html import escape

ROOT = Path(__file__).resolve().parent.parent
COPY = {
    "ru": ("Перенос", "Новый iPhone.", "Знакомые приложения.", "Перенесите сейчас. Или сохраните на потом.", "ДЛЯ WINDOWS", "БЕСПЛАТНО · ОТКРЫТЫЙ КОД", "ВАШИ ПРИЛОЖЕНИЯ", "НОВЫЙ IPHONE"),
    "en": ("Perenos", "New iPhone.", "Familiar apps.", "Transfer now. Or save for later.", "FOR WINDOWS", "FREE · OPEN SOURCE", "YOUR APPS", "NEW IPHONE"),
}


def phone(x, y, label, target=False):
    icons = ""
    colors = ["#3096ff", "#a387ff", "#ffb34e", "#f976a9", "#53c9b4", "#6b8fff"]
    for i, color in enumerate(colors):
        cx, cy = x + 25 + (i % 2) * 58, y + 72 + (i // 2) * 58
        icons += f'<rect x="{cx}" y="{cy}" width="43" height="43" rx="12" fill="{color}" opacity="{0.5 if target and i > 3 else 1}"/>'
        icons += f'<path d="M{cx+14} {cy+22}h15m-7-7v14" stroke="white" stroke-width="3" stroke-linecap="round" opacity=".8"/>'
    return f'''<g><rect x="{x}" y="{y}" width="151" height="282" rx="29" fill="var(--phone)" stroke="var(--line)" stroke-width="2"/>
      <rect x="{x+52}" y="{y+14}" width="47" height="9" rx="5" fill="var(--line)"/>{icons}
      <path d="M{x+57} {y+264}h37" stroke="var(--muted)" stroke-width="3" stroke-linecap="round"/>
      <text x="{x+75}" y="{y+314}" text-anchor="middle" class="label">{escape(label)}</text></g>'''


def render(lang, dark):
    title, line1, line2, caption, platform, meta, source, target = COPY[lang]
    colors = ("#0d1324", "#f4f7ff", "#b1bed8", "#19243c", "#354563", "#72b4ff") if dark else ("#f4f7fd", "#14233c", "#536684", "#ffffff", "#cbd8ec", "#1767d6")
    bg, ink, muted, card, stroke, accent = colors
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="470" viewBox="0 0 1200 470" role="img" aria-labelledby="title desc">
  <title id="title">{title} — {line1} {line2}</title>
  <desc id="desc">{caption} Windows 10 / 11.</desc>
  <style>:root{{--bg:{bg};--ink:{ink};--muted:{muted};--phone:{card};--line:{stroke};--accent:{accent}}}
    text{{font-family:'Segoe UI',Arial,sans-serif;fill:var(--ink)}}
    .label{{font-size:11px;font-weight:600;letter-spacing:1.5px;fill:var(--muted)}}
  </style>
  <defs>
    <linearGradient id="glow" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#248aff" stop-opacity=".19"/><stop offset="1" stop-color="#8a62ee" stop-opacity=".05"/></linearGradient>
    <linearGradient id="blue" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#11b4ed"/><stop offset=".55" stop-color="#087bff"/><stop offset="1" stop-color="#5749dd"/></linearGradient>
  </defs>
  <rect width="1200" height="470" rx="24" fill="var(--bg)"/>
  <path d="M660 0h516a24 24 0 0 1 24 24v422a24 24 0 0 1-24 24H574z" fill="url(#glow)"/>
  <circle cx="934" cy="244" r="197" fill="none" stroke="var(--line)" opacity=".35"/>
  <circle cx="934" cy="244" r="165" fill="none" stroke="var(--line)" opacity=".25"/>
  <rect x="56" y="48" width="56" height="56" rx="16" fill="url(#blue)"/>
  <g fill="none" stroke="white" stroke-width="3.3" stroke-linecap="round" stroke-linejoin="round">
    <path d="M82 75V63a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v24a3 3 0 0 1-3 3H77m5-5-5 5 5 5"/>
    <path d="M75 68h-6a3 3 0 0 0-3 3v22a3 3 0 0 0 3 3h4"/>
  </g>
  <text x="128" y="86" font-size="35" font-weight="650" letter-spacing="-.7">{title}</text>
  <text x="58" y="177" class="label" fill="var(--accent)">{platform}</text>
  <text x="54" y="238" font-size="49" font-weight="650" letter-spacing="-1.5">{line1}</text>
  <text x="54" y="297" font-size="49" font-weight="650" letter-spacing="-1.5">{line2}</text>
  <text x="57" y="344" font-size="19" style="fill:var(--muted)">{caption}</text>
  <path d="M57 380h40" stroke="var(--accent)" stroke-width="3" stroke-linecap="round"/>
  <text x="57" y="414" class="label">{meta}</text>
  {phone(723, 95, source)}
  {phone(978, 57, target, True)}
  <circle cx="925" cy="219" r="29" fill="url(#blue)"/>
  <path d="M912 219h26m-9-9 10 9-10 9" fill="none" stroke="white" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"/>
</svg>'''


if __name__ == "__main__":
    for lang in COPY:
        for theme in ("light", "dark"):
            path = ROOT / "docs" / "media" / f"hero-{lang}-{theme}.svg"
            path.write_text(render(lang, theme == "dark") + "\n", encoding="utf-8")
            print(path.relative_to(ROOT))
