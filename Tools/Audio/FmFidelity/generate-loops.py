#!/usr/bin/env python3
"""Reproduce bundled FM excerpts from hash-checked public portfolio MP3s.

The SOUND-I sources retain their spoken portfolio ident in Originals. Runtime
edits select only the documented instrumental intervals; no source-separation,
speech erasure, replacement melody, or reconstruction is performed. Adjacent
phrases use a 40ms incoming pre-roll crossfade without shortening musical cores.
"""
from __future__ import annotations

import array
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
GAME = ROOT / "Games/FantasyCards/Assets"


def run(*args: str) -> bytes:
    return subprocess.check_output(args, stderr=subprocess.PIPE)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def decode(path: Path, sample_rate: int) -> array.array:
    data = array.array("f")
    data.frombytes(run("ffmpeg", "-v", "error", "-i", str(path), "-ar", str(sample_rate),
                       "-ac", "2", "-f", "f32le", "pipe:1"))
    return data


def instrumental_edit(pcm: array.array, segments: list[list[float]], rate: int,
                      crossfade_seconds: float) -> array.array:
    cores = [(round(a * rate), round(b * rate)) for a, b in segments]
    result = array.array("f")
    offsets = []
    for start, end in cores:
        assert 0 <= start < end <= len(pcm) // 2
        offsets.append(len(result) // 2)
        result.extend(pcm[start * 2:end * 2])
    frames = len(result) // 2
    fade_frames = round(crossfade_seconds * rate)
    for index, offset in enumerate(offsets):
        # Incoming material before its core provides a transition handle while
        # preserving every phrase's beat-grid duration. The first boundary is
        # the circular loop seam. Intro sources with no pre-roll fade to zero.
        source_start = cores[index][0]
        for frame in range(fade_frames):
            t = (frame + .5) / fade_frames
            incoming_frame = source_start - fade_frames + frame
            destination = (offset - fade_frames + frame) % frames
            for channel in range(2):
                incoming = pcm[incoming_frame * 2 + channel] if incoming_frame >= 0 else 0
                result[destination * 2 + channel] = (
                    result[destination * 2 + channel] * (1 - t) + incoming * t)
    return result


def measurements(path: Path, rate: int) -> dict:
    pcm = decode(path, rate)
    peak = max(abs(x) for x in pcm)
    total = sum(x * x for x in pcm)
    window = round(rate * .1) * 2
    rms_windows = [math.sqrt(sum(v * v for v in pcm[i:i + window]) / len(pcm[i:i + window]))
                   for i in range(0, len(pcm), window) if pcm[i:i + window]]
    return {"seconds": len(pcm) / (rate * 2), "sampleRate": rate, "channels": 2,
            "peak": peak, "rms": math.sqrt(total / len(pcm)),
            "clippingSamples": sum(abs(x) >= 1 for x in pcm),
            "minimum100msRms": min(rms_windows)}


def main() -> None:
    spec = json.loads((HERE / "loop-spec.json").read_text())
    sources = json.loads((HERE / "source-files.json").read_text())
    source_by_name = {Path(x["file"]).name: x for x in sources["tracks"]}
    rate = spec["sampleRate"]
    rows = []
    native = GAME / "Resources/Audio/ModernCard"
    web = GAME / "StreamingAssets/Audio/ModernCard"
    with tempfile.TemporaryDirectory(prefix="fm-original-edits-") as directory:
        temporary = Path(directory)
        for track in spec["tracks"]:
            source = HERE / "Originals" / track["source"]
            proof = source_by_name[track["source"]]
            assert digest(source) == proof["sha256"], f"Source bytes changed: {source.name}"
            pcm = decode(source, rate)
            if track["segments"]:
                pcm = instrumental_edit(pcm, track["segments"], rate, spec["joinPreRollSeconds"])
            raw = temporary / (track["scene"] + ".f32")
            raw.write_bytes(pcm.tobytes())
            wav = temporary / (track["scene"] + ".wav")
            run("ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(rate), "-ac", "2",
                "-i", str(raw), "-af",
                f'loudnorm=I={spec["targetLufs"]}:TP={spec["truePeakDb"]}:LRA=11',
                "-ar", str(rate), "-c:a", "pcm_s16le", str(wav))
            ogg = native / (track["scene"] + ".ogg")
            mp3 = web / (track["scene"] + ".mp3")
            run("ffmpeg", "-v", "error", "-y", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "6", str(ogg))
            run("ffmpeg", "-v", "error", "-y", "-i", str(wav), "-c:a", "libmp3lame", "-q:a", "2", str(mp3))
            shutil.copyfile(ogg, web / ogg.name)
            row = dict(track)
            row.update(measurements(ogg, rate))
            row.update({"sha256": digest(ogg), "mp3Sha256": digest(mp3),
                        "sourceSha256": proof["sha256"], "sourceUrl": proof["webpage_url"],
                        "sourceId": proof["id"], "sourceSeconds": proof["duration"]})
            assert row["clippingSamples"] == 0
            rows.append(row)
            print(f'{track["scene"]}: {row["seconds"]:.3f}s, clipping 0, {row["sha256"][:12]}', flush=True)
    manifest = {"revision": spec["revision"], "editMethod": "Documented source excerpts with 40ms pre-roll joins; loudness normalization and codec conversion. Battle retains the complete public S.I.D-Sound recording.",
                "notRestored": "These portfolio recordings do not restore a full original client soundtrack or establish its exact game-era version.",
                "tracks": rows}
    for path in (HERE / "runtime-manifest.json", web / "manifest.json"):
        path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    main()
