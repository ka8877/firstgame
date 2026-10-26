#!/usr/bin/env python3
"""Render the private classic card edition's original chamber-orchestra loop.

This composes new notes; it does not transcribe or sample Fantasy Masters music.
The shared offline FluidR3 renderer is reused, but only FantasyCards is written.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import random
import subprocess
import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[2]
BASE_PATH = ROOT / "Tools/Audio/compose-soundtracks.py"
spec = importlib.util.spec_from_file_location("firstgame_score_renderer", BASE_PATH)
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

# E minor with added ninths, a relative-major middle passage and a B7 cadence.
# All pitches and phrase durations are authored here; no source recording/MIDI.
HARMONY = [
    (40, [52, 59, 64, 67, 71]), (36, [52, 55, 60, 64, 67]),
    (33, [52, 57, 60, 64, 67]), (35, [51, 57, 59, 63, 66]),
    (43, [55, 59, 62, 66, 69]), (38, [54, 57, 62, 64, 69]),
    (33, [52, 57, 60, 64, 67]), (35, [51, 57, 59, 63, 66]),
]
MIDDLE_HARMONY = [
    (36, [52, 55, 60, 64, 67]), (38, [54, 57, 62, 66, 69]),
    (35, [54, 57, 59, 62, 66]), (40, [52, 59, 64, 67, 71]),
    (36, [52, 55, 60, 64, 67]), (33, [52, 57, 60, 64, 67]),
    (35, [52, 57, 59, 64, 66]), (35, [51, 57, 59, 63, 66]),
]
THEME = [
    [(76, 1.5), (79, .5), (78, 1), (74, 1)],
    [(76, 1), (74, .5), (72, .5), (71, 2)],
    [(72, 1), (76, 1), (79, 1.5), (76, .5)],
    [(78, 1.5), (75, .5), (71, 1), (78, 1)],
    [(79, 1), (78, .5), (74, .5), (71, 2)],
    [(76, 1.5), (74, .5), (69, 2)],
    [(72, 1), (71, .5), (69, .5), (76, 1), (74, 1)],
    [(75, 2), (78, 1), (71, 1)],
]
MIDDLE = [
    [(79, 2), (76, 1), (74, 1)], [(78, 1), (76, 1), (74, 2)],
    [(74, 1.5), (78, .5), (81, 2)], [(79, 1), (78, 1), (76, 2)],
    [(76, 2), (79, 1), (83, 1)], [(81, 1.5), (79, .5), (76, 2)],
    [(76, 1), (78, 1), (81, 1), (78, 1)], [(75, 2), (71, 2)],
]


def compose():
    score = base.Score("starlight-overture", 88, 40,
                       {0: 48, 1: 73, 2: 42, 3: 46, 4: 52, 5: 0, 6: 68, 7: 47, 8: 8})
    score.rng = random.Random(20261026)
    for bar in range(score.bars):
        section, phrase = divmod(bar, 8)
        root, chord = (MIDDLE_HARMONY if section == 2 else HARMONY)[phrase]
        start = bar * 4
        strength = [.84, .96, .88, 1.05, .88][section]
        # Slow bowed bed; separated voices and a restrained low cello foundation.
        for voice, pitch in enumerate(chord):
            score.note(0, pitch, start + voice * .016, 3.91, (35 + (voice == 3) * 5) * strength)
        score.note(2, root if root >= 36 else root + 12, start, 3.88, 46 * strength)
        # Rolling harp leaves beat four open in each cadence, making the loop breathe.
        order = [0, 2, 3, 4, 2, 3, 1, 2]
        for beat, index in enumerate(order):
            if phrase == 7 and beat >= 6:
                continue
            score.note(3, chord[index] + 12, start + beat * .5, .82,
                       (29 + (beat % 4 == 0) * 9) * strength)
        # Piano answers rather than doubling every melody attack.
        if bar % 2 == 0 or section == 3:
            for voice, pitch in enumerate(chord[1:4]):
                score.note(5, pitch + 12, start + 2.04 + voice * .025, 1.75, 30 * strength)
        melody = MIDDLE[phrase] if section == 2 else THEME[phrase]
        lead = 6 if section in (1, 3) else 1
        offset = 0
        for pitch, duration in melody:
            score.note(lead, pitch, start + offset, duration - .13, 58 * strength)
            if section == 3 and duration >= 1.5:
                score.note(1, pitch + 12, start + offset + .035, duration - .19, 31)
            offset += duration
        # Wordless choir is a soft harmonic layer, never a prominent vocal line.
        if section in (1, 3, 4):
            for pitch in chord[1:4]:
                score.note(4, pitch, start + .06, 3.83, (22 if section == 4 else 27) * strength)
        # A few celesta replies suggest an enchanted card table, without a metronomic bell loop.
        if section in (0, 2, 4) and phrase in (1, 3, 5):
            score.note(8, chord[3] + 24, start + 2.75, .65, 31)
            score.note(8, chord[2] + 24, start + 3.5, .45, 26)
        # One restrained rise in the fourth phrase; no continuous kick/snare beat.
        if section == 3 and phrase % 2 == 0:
            score.note(7, root if root >= 38 else root + 12, start, 1.7, 33)
        if section == 3 and phrase == 7:
            for step in range(4):
                score.note(7, 47, start + 3 + step * .23, .18, 21 + step * 3)
    return score


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--soundfont", type=Path,
                        default=Path("/workspace/tools/audio/fluid-r3/usr/share/sounds/sf2/FluidR3_GM.sf2"))
    parser.add_argument("--work", type=Path, default=Path("/tmp/firstgame-classic-music"))
    args = parser.parse_args()
    assert digest(args.soundfont) == base.FONT_SHA
    args.work.mkdir(parents=True, exist_ok=True)
    dawn = ROOT / "Games/PathOfDawn/Assets/Resources/Audio/dawn-on-galilee.ogg"
    dawn_digest = digest(dawn)
    score = compose()
    midi = ROOT / "Tools/Audio/Scores/starlight-overture.mid"
    base.write_midi(score, midi)
    score_json = midi.with_suffix(".json")
    score_json.write_text(json.dumps({
        "title": "별빛의 회상 / Recollections of Starlight", "revision": "fm-classic-2026-10-26",
        "tempo": score.tempo, "bars": score.bars, "programs": score.programs,
        "structure": ["A · flute", "A′ · oboe and choir", "B · relative major", "A″ · ensemble", "A · quiet return"],
        "notes": score.notes,
    }, ensure_ascii=False, indent=2) + "\n")
    wav = base.render(score, args.soundfont, args.work)
    normalized = args.work / "starlight-overture-normalized.wav"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(wav),
                    "-af", "highpass=f=55,loudnorm=I=-19:TP=-1.5:LRA=10",
                    "-ar", str(base.RATE), "-c:a", "pcm_f32le", str(normalized)], check=True)
    rate, samples = wavfile.read(normalized)
    ramp = 133
    samples[:ramp] *= np.linspace(0, 1, ramp)[:, None]
    samples[-ramp:] *= np.linspace(1, 0, ramp)[:, None]
    assert np.isfinite(samples).all() and np.max(np.abs(samples)) < .95
    finalwav = args.work / "starlight-overture.wav"
    wavfile.write(finalwav, rate, samples)
    target = ROOT / "Games/FantasyCards/Assets/Resources/Audio/starlight-overture.ogg"
    meta_digest = digest(target.with_suffix(".ogg.meta"))
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(finalwav),
                    "-c:a", "libvorbis", "-q:a", "5", "-metadata", "title=Recollections of Starlight",
                    "-metadata", "artist=FirstGame original score", "-metadata",
                    "comment=Original composition; FluidR3 GM sample performance (MIT)", str(target)], check=True)
    preview = args.work / "recollections-of-starlight-preview.ogg"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-ss", "65", "-i", str(target),
                    "-t", "20", "-c:a", "libvorbis", "-q:a", "5", str(preview)], check=True)
    production = ROOT / "Tools/Audio/production.json"
    report = json.loads(production.read_text())
    entry = {
        "game": "FantasyCards", "file": str(target.relative_to(ROOT)),
        "title": "recollections-of-starlight", "displayTitle": "별빛의 회상 / Recollections of Starlight",
        "revision": "fm-classic-2026-10-26", "seconds": round(len(samples) / rate, 6),
        "tempo": score.tempo, "bars": score.bars, "noteCount": len(score.notes),
        "bytes": target.stat().st_size, "sha256": digest(target), "midiSha256": digest(midi),
        "scoreJsonSha256": digest(score_json), "generator": str(Path(__file__).resolve().relative_to(ROOT)),
        "generatorSha256": digest(Path(__file__)), "renderer": str(BASE_PATH.relative_to(ROOT)),
        "rendererSha256": digest(BASE_PATH),
        "peakDbFS": round(float(20 * np.log10(np.max(np.abs(samples)))), 3),
        "rmsDbFS": round(float(20 * np.log10(np.sqrt(np.mean(samples * samples)))), 3),
        "loopMethod": "Second complete cycle retains ambience tails; B7-to-E-minor cadence, no added silence, 3ms endpoint ramps.",
        "normalization": "55Hz high pass; -19 LUFS target, -1.5dB true-peak ceiling, LRA10 target",
        "preview": str(preview), "originalGameAudioUsed": False,
    }
    report["tracks"] = [entry if track["game"] == "FantasyCards" else track for track in report["tracks"]]
    report["classicRevision"] = "fm-classic-2026-10-26"
    production.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    assert digest(dawn) == dawn_digest
    assert digest(target.with_suffix(".ogg.meta")) == meta_digest
    print(json.dumps(entry, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
