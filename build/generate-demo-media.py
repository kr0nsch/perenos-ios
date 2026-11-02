#!/usr/bin/env python3
"""Build readable GIF tours from real Perenos demo screenshots.

Requires Python 3.11+ and Pillow. No UI is drawn, relabelled or fabricated: the only image
operations are proportional downscaling and GIF palette conversion.

    python build/generate-demo-media.py
    python build/generate-demo-media.py --capture-root artifacts/captures-1.2.0
    python build/generate-demo-media.py --flows demo later save --languages ru
    python build/generate-demo-media.py --check

Capture instructions and filenames are documented in docs/MEDIA.md.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
LANGUAGES = ("ru", "en")
THEMES = ("light", "dark")

# Deliberate pauses give readers time to understand each step. These are
# screenshot tours, not real-time performance measurements or screen recordings.
SEQUENCES = {
    "demo": (
        ("later-1-start.png", 2500),
        ("1-connect.png", 3000),
        ("2-choose-apps.png", 4000),
        ("3-target.png", 3000),
        ("4-sign-in.png", 3000),
        ("5-ready.png", 3000),
        ("6-installing.png", 4000),
        ("7-done.png", 4000),
    ),
    "later": (
        ("later-1-start.png", 2500),
        ("later-2-hub.png", 3500),
        ("later-3-new-iphone.png", 3000),
        ("later-4-saved.png", 4500),
        ("later-5-ready.png", 3000),
        ("later-6-done.png", 4000),
    ),
    "save": (
        ("save-1-apps.png", 4000),
        ("save-2-ready.png", 3500),
        ("save-3-done.png", 4000),
    ),
}
CAPTURE_NAMES = tuple(dict.fromkeys(name for sequence in SEQUENCES.values() for name, _ in sequence))


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def normalize_captures(args: argparse.Namespace) -> int:
    """Decode raw capture JPEGs and write real PNGs without resizing or retouching.

    Validate every requested 16-screen series before replacing any screenshot.
    The raw captures remain untouched, including their original compression.
    """
    captures = []
    expected_size = None
    for language in dict.fromkeys(args.languages):
        for theme in dict.fromkeys(args.themes):
            variant = f"{language}-{theme}"
            for name in CAPTURE_NAMES:
                source = args.capture_root / variant / Path(name).with_suffix(".jpg")
                if not source.is_file():
                    source = args.capture_root / variant / name
                if not source.is_file():
                    raise ValueError(f"Missing raw capture: {source}")
                with Image.open(source) as original:
                    if original.format not in ("JPEG", "PNG"):
                        raise ValueError(f"Expected JPEG or PNG capture bytes: {source}")
                    original.load()
                    if expected_size is None:
                        expected_size = original.size
                    elif original.size != expected_size:
                        raise ValueError(
                            f"Capture size mismatch: {source} is {original.size}; "
                            f"expected {expected_size}."
                        )
                    if original.width < 400 or original.height < 300:
                        raise ValueError(f"Capture is too small to read: {source}")
                    frame = original.convert("RGB")
                captures.append((args.screenshots_dir / variant / name, frame))
    if args.check:
        print(f"Validated {len(captures)} raw captures at {expected_size[0]}x{expected_size[1]}; no files written.")
        return len(captures)
    for destination, frame in captures:
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_suffix(".png.tmp")
        frame.save(temporary, format="PNG", optimize=True)
        with Image.open(temporary) as saved:
            if saved.format != "PNG" or saved.convert("RGB").tobytes() != frame.tobytes():
                raise ValueError(f"PNG pixel verification failed: {destination}")
        temporary.replace(destination)
    print(f"Normalized {len(captures)} real PNG screenshots; decoded capture pixels preserved exactly.")
    return len(captures)


def load_frames(folder: Path, sequence: tuple, width: int) -> list[Image.Image]:
    """Reject incomplete/mismatched captures instead of stretching the UI."""
    frames = []
    expected_size = None
    for name, _ in sequence:
        source = folder / name
        if not source.is_file():
            raise ValueError(f"Missing screenshot: {source}")
        with Image.open(source) as original:
            if original.format != "PNG":
                raise ValueError(f"Expected a PNG screenshot: {source}")
            original.load()
            if expected_size is None:
                expected_size = original.size
            elif original.size != expected_size:
                raise ValueError(
                    f"Capture size mismatch: {source} is {original.size}; "
                    f"expected {expected_size}. Capture every screen at one window size."
                )
            if original.width < 400 or original.height < 300:
                raise ValueError(f"Screenshot is too small to read: {source}")
            frame = original.convert("RGB")
        # Never upscale a small capture, and never crop controls or window titles.
        target_width = min(width, frame.width)
        target_height = round(frame.height * target_width / frame.width)
        if frame.size != (target_width, target_height):
            frame = frame.resize((target_width, target_height), Image.Resampling.LANCZOS)
        frames.append(frame)
    return frames


def palette_for(frames: list[Image.Image]) -> Image.Image:
    """One balanced palette for the whole tour avoids colour shifts per step."""
    samples = []
    for frame in frames:
        sample = frame.copy()
        sample.thumbnail((320, 320), Image.Resampling.LANCZOS)
        samples.append(sample)
    sheet = Image.new("RGB", (max(s.width for s in samples), sum(s.height for s in samples)))
    y = 0
    for sample in samples:
        sheet.paste(sample, (0, y))
        y += sample.height
    return sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)


def write_tour(destination: Path, frames: list[Image.Image], durations: list[int]) -> dict:
    palette = palette_for(frames)
    indexed = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    # Keep the global palette and let Pillow store only changed frame rectangles.
    # No fades: they add blurred intermediate text and unnecessary payload.
    indexed[0].save(
        destination,
        format="GIF",
        save_all=True,
        append_images=indexed[1:],
        duration=durations,
        loop=0,
        disposal=1,
        optimize=False,
        comment=b"Perenos demo screenshot tour; simulated devices and apps; not real-time.",
    )
    with Image.open(destination) as result:
        total_duration = 0
        for index in range(result.n_frames):
            result.seek(index)
            result.load()
            total_duration += result.info.get("duration", 0)
        if total_duration != sum(durations):
            raise ValueError(f"GIF duration verification failed: {destination}")
        return {
            "file": destination.name,
            "width": result.width,
            "height": result.height,
            "frames": result.n_frames,
            "duration_ms": total_duration,
            "bytes": destination.stat().st_size,
            "sha256": sha256(destination),
        }


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--capture-root", type=Path, help="Normalize a complete matrix of raw <variant>/<name>.jpg or .png captures before GIF generation.")
    parser.add_argument("--screenshots-dir", type=Path, default=ROOT / "docs" / "screenshots")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "docs" / "media")
    parser.add_argument("--languages", nargs="+", choices=LANGUAGES, default=list(LANGUAGES))
    parser.add_argument("--themes", nargs="+", choices=THEMES, default=list(THEMES))
    parser.add_argument("--flows", nargs="+", choices=tuple(SEQUENCES), default=["demo", "later"])
    parser.add_argument("--width", type=int, default=900, help="Maximum GIF width; captures are never enlarged (default: 900).")
    parser.add_argument("--check", action="store_true", help="Validate required input screenshots without writing files.")
    parser.add_argument("--normalize-only", action="store_true", help="With --capture-root, write PNG screenshots without rebuilding GIFs.")
    args = parser.parse_args()
    if not 400 <= args.width <= 1600:
        parser.error("--width must be between 400 and 1600 pixels")
    if args.normalize_only and not args.capture_root:
        parser.error("--normalize-only requires --capture-root")
    return args


def main() -> int:
    args = arguments()
    if args.capture_root:
        normalize_captures(args)
        if args.check or args.normalize_only:
            return 0
    jobs = []
    # Validate the complete selected matrix before overwriting any published GIF.
    for language in dict.fromkeys(args.languages):
        for theme in dict.fromkeys(args.themes):
            folder = args.screenshots_dir / f"{language}-{theme}"
            for flow in dict.fromkeys(args.flows):
                sequence = SEQUENCES[flow]
                frames = load_frames(folder, sequence, args.width)
                jobs.append((language, theme, flow, folder, sequence, frames))
    if args.check:
        print(f"Validated {len(jobs)} tours; all screenshots exist and each tour has consistent dimensions.")
        return 0
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for language, theme, flow, folder, sequence, frames in jobs:
        destination = args.output_dir / f"{flow}-{language}-{theme}.gif"
        info = write_tour(destination, frames, [duration for _, duration in sequence])
        info["sources"] = [
            {"file": f"{folder.name}/{name}", "duration_ms": duration, "sha256": sha256(folder / name)}
            for name, duration in sequence
        ]
        # Each GIF has its own deterministic manifest, so partial rebuilds cannot
        # leave an aggregate manifest with missing tours or stale output hashes.
        destination.with_suffix(".json").write_text(
            json.dumps(info, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        print(f"{destination.name}: {info['width']}x{info['height']}, "
              f"{info['frames']} frames, {info['duration_ms'] / 1000:g}s, {info['bytes'] / 1024:.0f} KiB")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as exc:
        print(f"Media generation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
