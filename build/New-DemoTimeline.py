#!/usr/bin/env python3
"""Create media timelines from a full Demo-Check recording and its screenshots.

The recording must contain <language>-<theme>/demo with capture.json, frames.csv,
window.txt and lossless PNG frames. Screenshots use the matching Demo-Check output
folder. Raw frames are never edited; restore and save receive linked copies.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import re
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"


def inside_artifacts(path: Path) -> Path:
    resolved = path.resolve()
    if not resolved.is_relative_to(ARTIFACTS):
        raise ValueError(f"Expected a path inside artifacts: {path}")
    return resolved


def screenshot_times(folder: Path) -> dict[str, int]:
    result = {}
    for path in folder.glob("*.png"):
        name = re.sub(r"-(?:ru-RU|en-US)-(?:Light|Dark)\.png$", "", path.name, flags=re.IGNORECASE)
        result[name] = path.stat().st_mtime_ns // 1_000_000
    return result


def timeline(flow: str, version: str, language: str, theme: str,
             frames: list[int], shots: dict[str, int]) -> dict:
    first, last = frames[0], frames[-1]
    if flow == "demo":
        start, end = shots["start"], shots["install"]
        stills = {
            "1-connect.png": shots["devices"],
            "2-choose-apps.png": shots["apps"],
            "3-target.png": shots["target"],
            "4-sign-in.png": shots["install-sign-in"],
            "5-ready.png": shots["install-options"],
            "6-installing.png": shots["installing"],
            "7-done.png": shots["install"],
        }
    elif flow == "restore":
        start, end = shots["restore-all"] - 1000, shots["restore-done"]
        stills = {
            "restore-1-apps.png": shots["restore-all"],
            "restore-2-filter.png": shots["restore-russian"],
            "restore-3-selected.png": shots["restore"],
            "restore-4-install.png": shots["restore-install"],
            "restore-5-done.png": shots["restore-done"],
        }
    elif flow == "save":
        start, end = shots["library"] - 500, last
        stills = {
            "save-1-apps.png": shots["save-apps"],
            "save-2-ready.png": shots["save-ready"],
            "save-3-done.png": shots["saved"],
        }
    else:
        raise ValueError(f"Unsupported flow: {flow}")
    if not first <= start < end <= last:
        raise ValueError(f"Invalid {flow} segment for {language}-{theme}: {start}..{end} outside {first}..{last}")
    if any(not first <= timestamp <= last for timestamp in stills.values()):
        raise ValueError(f"A {flow} still is outside the recording for {language}-{theme}")
    return {
        "schema_version": 1,
        "version": version,
        "language": language,
        "theme": theme,
        "flow": flow,
        "segments": [{"start_ms": start, "end_ms": end}],
        "stills": stills,
    }


def clone_recording(source: Path, destination: Path) -> None:
    if destination.exists():
        raise FileExistsError(f"Refusing to overwrite an existing recording: {destination}")
    destination.mkdir(parents=True)
    for path in source.iterdir():
        if path.name == "timeline.json" or not path.is_file():
            continue
        target = destination / path.name
        try:
            os.link(path, target)
        except OSError:
            shutil.copy2(path, target)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--recordings-dir", type=Path, required=True)
    parser.add_argument("--shots-dir", type=Path, required=True)
    parser.add_argument("--version")
    parser.add_argument("--variants", nargs="+", help="Optional language-theme folders to rebuild")
    parser.add_argument("--flows", nargs="+", choices=("demo", "restore", "save"), default=["demo", "restore", "save"])
    args = parser.parse_args()
    recordings_dir = inside_artifacts(args.recordings_dir)
    shots_dir = inside_artifacts(args.shots_dir)
    version = args.version or ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
    if version:
        version = version.strip()
    if not version:
        raise ValueError("The application version is missing")

    variants = args.variants or sorted(path.parent.parent.name for path in recordings_dir.glob("*-*/demo/capture.json"))
    if not variants:
        raise ValueError(f"No completed demo recordings found in {recordings_dir}")
    for variant in variants:
        language, theme = variant.split("-", 1)
        source = recordings_dir / variant / "demo"
        capture = json.loads((source / "capture.json").read_text(encoding="utf-8-sig"))
        if capture.get("status") != "complete":
            raise ValueError(f"Recording is not complete: {source}")
        with (source / "frames.csv").open(newline="", encoding="utf-8-sig") as stream:
            frames = [int(row[1]) for row in csv.reader(stream)]
        if len(frames) < 2 or frames != sorted(frames):
            raise ValueError(f"Invalid frame timestamps: {source}")
        shots = screenshot_times(shots_dir / variant)
        for flow in dict.fromkeys(args.flows):
            destination = source if flow == "demo" else recordings_dir / variant / flow
            if flow != "demo":
                clone_recording(source, destination)
            output = destination / "timeline.json"
            if output.exists():
                raise FileExistsError(f"Refusing to overwrite an existing timeline: {output}")
            content = timeline(flow, version, language, theme, frames, shots)
            output.write_text(json.dumps(content, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            print(f"Created {output.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError) as error:
        print(f"Timeline creation failed: {error}", file=os.sys.stderr)
        raise SystemExit(1)
