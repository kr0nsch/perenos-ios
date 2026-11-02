#!/usr/bin/env python3
"""Build cursor-free GIF recordings and exact PNG stills of Perenos.

Requires Python 3.11+, Pillow and FFmpeg. Inputs are lossless PrintWindow PNG
recordings, not JPEG screenshots. The compositor samples recorded frames at
12 fps, optionally shortens documented pauses, and never draws or retouches UI.

    python build/generate-demo-media.py --recordings-dir artifacts/recordings-1.2.0 --check
    python build/generate-demo-media.py --recordings-dir artifacts/recordings-1.2.0
    python build/generate-demo-media.py --recordings-dir artifacts/recordings-1.2.0 --flows save --stills-only

Each <language>-<theme>/<flow> directory contains f00000.png..., frames.csv
(index, Unix timestamp in milliseconds), window.txt (x,y,width,height), and
timeline.json. A completed capture.json is mandatory; active or failed captures
are rejected. See docs/MEDIA.md for the capture procedure and timeline schema.
"""
from __future__ import annotations

import argparse
import bisect
import csv
from dataclasses import dataclass
import hashlib
import io
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
LANGUAGES = ("ru", "en")
THEMES = ("light", "dark")
STILLS = {
    "demo": (
        "1-connect.png", "2-choose-apps.png", "3-target.png", "4-sign-in.png",
        "5-ready.png", "6-installing.png", "7-done.png",
    ),
    "later": (
        "later-1-start.png", "later-2-hub.png", "later-3-new-iphone.png",
        "later-4-saved.png", "later-5-ready.png", "later-6-done.png",
    ),
    "save": ("save-1-apps.png", "save-2-ready.png", "save-3-done.png"),
}


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def integer(value, label: str, minimum: int = 0) -> int:
    if type(value) is not int or value < minimum:
        raise ValueError(f"{label} must be an integer >= {minimum}")
    return value


@dataclass(frozen=True)
class Frame:
    index: int
    timestamp_ms: int
    path: Path
    sha256: str


@dataclass(frozen=True)
class Segment:
    start_ms: int
    end_ms: int
    output_duration_ms: int

    def as_dict(self) -> dict:
        return {
            "start_ms": self.start_ms,
            "end_ms": self.end_ms,
            "output_duration_ms": self.output_duration_ms,
        }


class Recording:
    def __init__(self, folder: Path, language: str, theme: str, flow: str, stills_only: bool):
        self.folder = folder
        self.variant = f"{language}-{theme}"
        self.flow = flow
        self.timeline_path = folder / "timeline.json"
        self.csv_path = folder / "frames.csv"
        self.window_path = folder / "window.txt"
        self.capture_path = folder / "capture.json"
        # Parse and hash exactly the same bytes. Reading these files again later
        # could silently describe a different recording in the output manifest.
        capture_bytes = self.capture_path.read_bytes()
        capture = json.loads(capture_bytes.decode("utf-8-sig"))
        if not isinstance(capture, dict) or type(capture.get("schema_version")) is not int or capture["schema_version"] != 1:
            raise ValueError(f"Unsupported capture schema: {self.capture_path}")
        if capture.get("status") != "complete":
            raise ValueError(f"Recording is not complete (status={capture.get('status')!r}): {self.capture_path}")
        self.capture_method = capture.get("method")
        if (self.capture_method != "PrintWindow(PW_RENDERFULLCONTENT)"
                or capture.get("format") != "PNG" or capture.get("cursor_captured") is not False):
            raise ValueError(f"A cursor-free PrintWindow PNG capture is required: {self.capture_path}")
        capture_frames = integer(capture.get("frames"), "capture.frames", 2)
        capture_width = integer(capture.get("width"), "capture.width", 400)
        capture_height = integer(capture.get("height"), "capture.height", 300)
        metadata = {"capture.json": capture_bytes}
        for path in (self.timeline_path, self.csv_path, self.window_path):
            metadata[path.name] = path.read_bytes()
        self.metadata_hashes = {name: hashlib.sha256(data).hexdigest() for name, data in metadata.items()}
        timeline = json.loads(metadata["timeline.json"].decode("utf-8-sig"))
        if not isinstance(timeline, dict) or timeline.get("schema_version") != 1:
            raise ValueError(f"Unsupported timeline schema: {self.timeline_path}")
        for key, expected in (("language", language), ("theme", theme), ("flow", flow)):
            if timeline.get(key) != expected:
                raise ValueError(f"{self.timeline_path}: {key} must be {expected!r}")
        self.version = timeline.get("version")
        if not isinstance(self.version, str) or not re.fullmatch(r"\d+\.\d+\.\d+", self.version):
            raise ValueError(f"{self.timeline_path}: version must have form 1.2.0")

        window = metadata["window.txt"].decode("utf-8-sig").strip().split(",")
        if len(window) != 4:
            raise ValueError(f"Expected x,y,width,height: {self.window_path}")
        _, _, self.width, self.height = (int(value) for value in window)
        if self.width < 400 or self.height < 300:
            raise ValueError(f"Recording is too small to read: {folder}")
        if (capture_width, capture_height) != (self.width, self.height):
            raise ValueError(f"capture.json and window.txt dimensions disagree: {folder}")

        self.frames: list[Frame] = []
        aggregate = hashlib.sha256()
        previous_time = -1
        with io.StringIO(metadata["frames.csv"].decode("utf-8-sig"), newline="") as stream:
            for row in csv.reader(stream):
                if len(row) != 2:
                    raise ValueError(f"Expected index,timestamp_ms in {self.csv_path}: {row}")
                index, timestamp = (int(value) for value in row)
                if index != len(self.frames) or timestamp <= previous_time or timestamp < 0:
                    raise ValueError(f"Non-contiguous frame indexes or unordered timestamps: {self.csv_path}")
                path = folder / f"f{index:05d}.png"
                data = path.read_bytes()
                with Image.open(io.BytesIO(data)) as image:
                    if image.format != "PNG":
                        raise ValueError(f"Lossless PNG input required; JPEG is not accepted: {path}")
                    if image.size != (self.width, self.height):
                        raise ValueError(f"Frame size differs from window.txt: {path}")
                    image.verify()
                digest = hashlib.sha256(data).hexdigest()
                self.frames.append(Frame(index, timestamp, path, digest))
                # Canonical entries make the aggregate independent of machine
                # paths and frames.csv newline/encoding conventions.
                aggregate.update(f"{index},{timestamp},{digest}\n".encode("ascii"))
                previous_time = timestamp
        if len(self.frames) < 2:
            raise ValueError(f"At least two recorded PNG frames are required: {folder}")
        if capture_frames != len(self.frames):
            raise ValueError(f"capture.json frame count differs from frames.csv: {folder}")
        listed = {frame.path.name for frame in self.frames}
        actual = {path.name for path in folder.glob("f*.png")}
        if actual != listed:
            raise ValueError(f"PNG files and frames.csv disagree: {folder}")
        self.raw_sha256 = aggregate.hexdigest()
        self.times = [frame.timestamp_ms for frame in self.frames]

        raw_segments = timeline.get("segments")
        if not isinstance(raw_segments, list) or (not raw_segments and not stills_only):
            raise ValueError(f"A GIF timeline needs at least one segment: {self.timeline_path}")
        self.segments: list[Segment] = []
        for item in raw_segments:
            if not isinstance(item, dict):
                raise ValueError(f"A timeline segment must be an object: {self.timeline_path}")
            start = integer(item.get("start_ms"), "start_ms")
            end = integer(item.get("end_ms"), "end_ms")
            if end <= start:
                raise ValueError(f"Segment end must be after start: {self.timeline_path}")
            duration = integer(item.get("output_duration_ms", end - start), "output_duration_ms", 1)
            if self.segments and start < self.segments[-1].end_ms:
                raise ValueError(f"Segments overlap or run backwards: {self.timeline_path}")
            self.require_timestamp(start)
            self.require_timestamp(end)
            self.segments.append(Segment(start, end, duration))

        stills = timeline.get("stills")
        if not isinstance(stills, dict) or set(stills) != set(STILLS[flow]):
            raise ValueError(f"Expected exactly the {flow} stills {STILLS[flow]}: {self.timeline_path}")
        self.stills: dict[str, tuple[int, Frame]] = {}
        for name in STILLS[flow]:
            timestamp = integer(stills[name], f"stills.{name}")
            self.require_timestamp(timestamp)
            self.stills[name] = (timestamp, self.frame_at(timestamp))
        self.verify_unchanged(include_frames=False)

    def verify_unchanged(self, include_frames: bool = True) -> None:
        for name, digest in self.metadata_hashes.items():
            if sha256(self.folder / name) != digest:
                raise ValueError(f"Recording metadata changed during the build: {self.folder / name}")
        if include_frames:
            if {path.name for path in self.folder.glob("f*.png")} != {frame.path.name for frame in self.frames}:
                raise ValueError(f"Recorded frame set changed during the build: {self.folder}")
            for frame in self.frames:
                if sha256(frame.path) != frame.sha256:
                    raise ValueError(f"Recorded PNG changed during the build: {frame.path}")

    def require_timestamp(self, timestamp: int) -> None:
        if not self.times[0] <= timestamp <= self.times[-1]:
            raise ValueError(
                f"Timestamp {timestamp} is outside recorded coverage "
                f"[{self.times[0]}, {self.times[-1]}]: {self.folder}"
            )

    def frame_at(self, timestamp: float) -> Frame:
        return self.frames[bisect.bisect_right(self.times, timestamp) - 1]

    def schedule(self, fps: int) -> list[Frame]:
        """Sample real frames; no optical-flow interpolation or fabricated UI."""
        boundaries = [0]
        for segment in self.segments:
            boundaries.append(boundaries[-1] + segment.output_duration_ms)
        count = math.ceil(boundaries[-1] * fps / 1000)
        result = []
        for index in range(count):
            output_time = index * 1000 / fps
            segment_index = min(bisect.bisect_right(boundaries, output_time) - 1, len(self.segments) - 1)
            segment = self.segments[segment_index]
            fraction = (output_time - boundaries[segment_index]) / segment.output_duration_ms
            timestamp = segment.start_ms + fraction * (segment.end_ms - segment.start_ms)
            result.append(self.frame_at(timestamp))
        return result

    def source_manifest(self) -> dict:
        return {
            "recording": f"{self.variant}/{self.flow}",
            "format": "lossless PNG",
            "capture_method": self.capture_method,
            "cursor_captured": False,
            "width": self.width,
            "height": self.height,
            "frames": len(self.frames),
            "first_timestamp_ms": self.times[0],
            "last_timestamp_ms": self.times[-1],
            "capture_json_sha256": self.metadata_hashes["capture.json"],
            "frames_csv_sha256": self.metadata_hashes["frames.csv"],
            "window_txt_sha256": self.metadata_hashes["window.txt"],
            "timeline_sha256": self.metadata_hashes["timeline.json"],
            "raw_frames_sha256": self.raw_sha256,
            "raw_frames_hash_format": "SHA-256 of UTF-8 index,timestamp_ms,png_sha256\\n entries in frame order",
        }


def link_or_copy(source: Path, destination: Path) -> None:
    try:
        os.link(source, destination)
    except OSError:
        shutil.copyfile(source, destination)


def run_ffmpeg(command: list[str]) -> None:
    result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise ValueError(f"FFmpeg failed ({result.returncode}): {result.stderr.strip()}")


def encode(recording: Recording, work: Path, args: argparse.Namespace) -> tuple[Path, dict]:
    scheduled = recording.schedule(args.fps)
    if len(scheduled) < 2:
        raise ValueError(f"Timeline is shorter than two output frames: {recording.folder}")
    frames_dir = work / "frames"
    frames_dir.mkdir()
    snapshots_dir = work / "snapshots"
    snapshots_dir.mkdir()
    snapshots = {}
    for index, frame in enumerate(scheduled):
        if frame.index not in snapshots:
            snapshot = snapshots_dir / frame.path.name
            shutil.copyfile(frame.path, snapshot)
            if sha256(snapshot) != frame.sha256:
                raise ValueError(f"Recorded PNG changed before encoding: {frame.path}")
            snapshots[frame.index] = snapshot
        # Link only to our private verified snapshot, never to mutable raw input.
        link_or_copy(snapshots[frame.index], frames_dir / f"c{index:06d}.png")
    pattern = str(frames_dir / "c%06d.png")
    palette = work / "palette.png"
    width = min(args.width, recording.width)
    height = round(recording.height * width / recording.width)
    scale = f"scale={width}:{height}:flags=lanczos"
    common = [args.ffmpeg, "-y", "-loglevel", "error", "-framerate", str(args.fps), "-i", pattern]
    # Two passes avoid buffering a whole high-resolution recording in memory.
    run_ffmpeg(common + ["-vf", f"{scale},palettegen=max_colors=256:stats_mode=diff", "-frames:v", "1", "-update", "1", str(palette)])
    output = work / f"{recording.flow}-{recording.variant}.gif"
    run_ffmpeg(common + [
        "-i", str(palette), "-filter_complex",
        f"[0:v]{scale}[video];[video][1:v]paletteuse=dither=sierra2_4a:diff_mode=rectangle",
        "-loop", "0", str(output),
    ])
    with Image.open(output) as image:
        if image.format != "GIF" or image.size != (width, height) or image.info.get("loop") != 0:
            raise ValueError(f"Invalid GIF dimensions or loop mode: {output}")
        durations = []
        for index in range(image.n_frames):
            image.seek(index)
            image.load()
            durations.append(image.info.get("duration", 0))
        if image.n_frames != len(scheduled) or not all(duration > 0 for duration in durations):
            raise ValueError(f"GIF output frame count or timings differ from the schedule: {output}")
        duration_ms = sum(durations)
        if abs(duration_ms - len(scheduled) * 1000 / args.fps) > 20:
            raise ValueError(f"GIF duration differs from the schedule: {output}")
        manifest = {
            "schema_version": 2,
            "version": recording.version,
            "file": output.name,
            "width": image.width,
            "height": image.height,
            "frames": image.n_frames,
            "fps": args.fps,
            "duration_ms": duration_ms,
            "bytes": output.stat().st_size,
            "sha256": sha256(output),
            "cursor": "none",
            "encoding": {"scaling": "lanczos", "max_colors": 256, "palette_stats": "diff", "dither": "sierra2_4a"},
            "source": recording.source_manifest(),
            "segments": [segment.as_dict() for segment in recording.segments],
            "sampled_frames_sha256": hashlib.sha256(
                "".join(f"{frame.index},{frame.sha256}\n" for frame in scheduled).encode("ascii")
            ).hexdigest(),
        }
    return output, manifest


def stage_stills(recording: Recording, work: Path) -> tuple[list[tuple[Path, str]], list[dict]]:
    folder = work / "stills"
    folder.mkdir()
    files, manifest = [], []
    for name, (timestamp, frame) in recording.stills.items():
        destination = folder / name
        shutil.copyfile(frame.path, destination)
        if sha256(destination) != frame.sha256:
            raise ValueError(f"PNG still is not an exact copy of its recorded frame: {destination}")
        files.append((destination, name))
        manifest.append({
            "file": f"{recording.variant}/{name}",
            "requested_timestamp_ms": timestamp,
            "source_frame": frame.path.name,
            "source_timestamp_ms": frame.timestamp_ms,
            "sha256": frame.sha256,
        })
    return files, manifest


def publish(source: Path, destination: Path) -> None:
    """Replace one verified output atomically; never delete an output directory."""
    destination.parent.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(prefix=f".{destination.name}.", suffix=".tmp", dir=destination.parent)
    os.close(descriptor)
    temporary = Path(name)
    try:
        shutil.copyfile(source, temporary)
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--recordings-dir", type=Path, required=True)
    parser.add_argument("--screenshots-dir", type=Path, default=ROOT / "docs" / "screenshots")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "docs" / "media")
    parser.add_argument("--languages", nargs="+", choices=LANGUAGES, default=list(LANGUAGES))
    parser.add_argument("--themes", nargs="+", choices=THEMES, default=list(THEMES))
    parser.add_argument("--flows", nargs="+", choices=tuple(STILLS), default=["demo", "later"])
    parser.add_argument("--width", type=int, default=900, help="Maximum GIF width; never upscale (default: 900).")
    parser.add_argument("--fps", type=int, default=12)
    parser.add_argument("--ffmpeg", default="ffmpeg", help="FFmpeg executable name or path.")
    parser.add_argument("--check", action="store_true", help="Validate the selected recordings without writing files.")
    parser.add_argument("--stills-only", action="store_true", help="Extract lossless stills and manifests without encoding GIFs.")
    args = parser.parse_args()
    if not 400 <= args.width <= 1600:
        parser.error("--width must be between 400 and 1600 pixels")
    if not 6 <= args.fps <= 30:
        parser.error("--fps must be between 6 and 30")
    return args


def main() -> int:
    args = arguments()
    recordings = []
    # No destination changes until every selected raw recording is validated.
    for language in dict.fromkeys(args.languages):
        for theme in dict.fromkeys(args.themes):
            for flow in dict.fromkeys(args.flows):
                folder = args.recordings_dir / f"{language}-{theme}" / flow
                recordings.append(Recording(folder, language, theme, flow, args.stills_only))
    if len({(recording.width, recording.height) for recording in recordings}) != 1:
        raise ValueError("Record every requested language, theme and flow at the same window size")
    if len({recording.version for recording in recordings}) != 1:
        raise ValueError("Do not mix application versions in one media build")
    frame_count = sum(len(recording.frames) for recording in recordings)
    print(f"Validated {len(recordings)} recordings with {frame_count} lossless PNG frames.")
    if args.check:
        for recording in recordings:
            recording.verify_unchanged()
        return 0
    if not args.stills_only and not shutil.which(args.ffmpeg):
        raise ValueError("FFmpeg was not found; install it or provide --ffmpeg <path>")
    artifacts = ROOT / "artifacts"
    if artifacts.resolve() != artifacts:
        raise ValueError("The repository artifacts directory must not be a link outside the workspace")
    artifacts.mkdir(exist_ok=True)
    pending = []
    summaries = []
    # Only this uniquely created scratch directory is recursively removed.
    with tempfile.TemporaryDirectory(prefix="media-compose-", dir=artifacts) as temporary:
        for recording in recordings:
            work = Path(temporary) / f"{recording.flow}-{recording.variant}"
            work.mkdir()
            stills, still_manifest = stage_stills(recording, work)
            for path, name in stills:
                pending.append((path, args.screenshots_dir / recording.variant / name))
            if args.stills_only:
                manifest = {
                    "schema_version": 2, "version": recording.version,
                    "source": recording.source_manifest(), "stills": still_manifest,
                }
                manifest_name = f"{recording.flow}-{recording.variant}-stills.json"
            else:
                output, manifest = encode(recording, work, args)
                manifest["stills"] = still_manifest
                pending.append((output, args.output_dir / output.name))
                manifest_name = output.with_suffix(".json").name
                summaries.append(f"{output.name}: {manifest['width']}x{manifest['height']}, "
                                 f"{manifest['frames']} frames, {manifest['duration_ms'] / 1000:g}s, "
                                 f"{manifest['bytes'] / 1048576:.2f} MiB")
            manifest_path = work / manifest_name
            manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            pending.append((manifest_path, args.output_dir / manifest_name))
        # A complete recording must remain the same throughout the build. No
        # destination is replaced if its metadata or any raw PNG has changed.
        for recording in recordings:
            recording.verify_unchanged()
        for source, destination in pending:
            publish(source, destination)
    for summary in summaries:
        print(summary)
    print(f"Published {sum(len(recording.stills) for recording in recordings)} exact PNG stills.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError) as error:
        print(f"Media generation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
