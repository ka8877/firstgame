#!/usr/bin/env python3
"""Render four bundled, speech-free FirstGame card themes from authored MIDI.

No streamed recording, ad-supported player, voice sample, or original FM score
is used. The old game music and production records are deliberately untouched.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import random
import shutil
import subprocess
import uuid

import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[2]
RENDERER = ROOT / "Tools/Audio/compose-soundtracks.py"
spec = importlib.util.spec_from_file_location("firstgame_music_renderer", RENDERER)
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

CHORDS = [
    (40, [52, 59, 64, 67, 71]), (36, [48, 55, 60, 64, 71]),
    (33, [45, 52, 57, 60, 64]), (35, [47, 54, 59, 63, 69]),
    (43, [55, 59, 62, 66, 69]), (38, [50, 57, 62, 66, 69]),
    (36, [48, 55, 60, 64, 67]), (35, [47, 54, 59, 63, 69]),
]
THEME = [
    [(76, 1.5), (83, .5), (81, 1), (79, 1)],
    [(76, 1), (79, 1), (83, 1), (84, 1)],
    [(81, 1.5), (79, .5), (76, 1), (74, 1)],
    [(78, 1), (79, .5), (78, .5), (75, 2)],
    [(79, 1), (83, 1), (86, 1.5), (83, .5)],
    [(81, 1.5), (78, .5), (76, 1), (74, 1)],
    [(76, 1), (79, 1), (84, 1), (83, 1)],
    [(81, 1), (78, 1), (75, 1), (71, 1)],
]
BRIDGE = [
    [(83, 2), (79, 1), (78, 1)], [(79, 1), (76, 1), (74, 2)],
    [(76, 1.5), (81, .5), (84, 2)], [(83, 1), (81, 1), (78, 2)],
    [(86, 1.5), (83, .5), (81, 1), (79, 1)],
    [(81, 1), (78, 1), (74, 2)], [(79, 1), (76, 1), (72, 2)],
    [(75, 1.5), (78, .5), (83, 2)],
]
SETTINGS = {
    "title": (104, "별문이 열릴 때 / Gates of Starlight"),
    "lobby": (94, "여행자의 정원 / The Wayfarer's Garden"),
    "deck": (90, "빛을 엮는 서고 / The Luminous Archive"),
    "battle": (122, "별의 결투 / Duel beneath the Stars"),
}


def compose(scene):
    tempo, _ = SETTINGS[scene]
    # Zero-based GM instruments. No choir, vocal, voice effects or sound effects.
    programs = {0: 48, 1: 60, 2: 43, 3: 46, 4: 45, 5: 73, 6: 47, 7: 61, 8: 0, 10: 8}
    s = base.Score("modern-card-" + scene, tempo, 32, programs)
    s.rng = random.Random(20261026 + list(SETTINGS).index(scene))
    for bar in range(s.bars):
        section, phrase = divmod(bar, 8)
        root, chord = CHORDS[phrase]
        at = bar * 4
        strength = [1, 1.08, .85, 1.14][section]
        quiet = scene in ("lobby", "deck")
        for voice, pitch in enumerate(chord):
            s.note(0, pitch, at + voice * .018, 3.9, (34 if quiet else 46) * strength)
        s.note(2, root if root >= 36 else root + 12, at, 3.85, (39 if quiet else 51) * strength)
        order = [0, 2, 3, 4, 2, 3, 1, 2]
        for k, index in enumerate(order):
            s.note(3, chord[index] + 12, at + k * .5, .74, (33 if quiet else 36) + (k % 4 == 0) * 7)
        if scene == "deck":
            for k, index in enumerate([0, 2, 3, 2]):
                s.note(8, chord[index] + 12, at + k, .8, 36 + (k == 0) * 7)
            for k in (1, 3):
                s.note(4, chord[2] + 12, at + k, .36, 31)
        elif scene == "lobby":
            for k in (0, 2):
                s.note(8, chord[2] + 12, at + k + .04, 1.7, 30)
        else:
            # A measured ostinato, rather than a dance beat or a vocal pad.
            if scene == "battle" or section in (1, 3):
                for k in range(8):
                    s.note(4, chord[k % 3] + 12, at + k * .5, .28, 37 + (k % 2 == 0) * 9)
            s.note(6, root if root >= 38 else root + 12, at, 1.4, 42 * strength)
            if scene == "battle":
                s.note(6, 47, at + 2, 1.3, 37 * strength)
                s.note(9, 36, at, .6, 48)
                for k in (1, 3):
                    s.note(9, 41, at + k, .4, 32)
            if phrase in (0, 4):
                s.note(9, 49, at, 2.6, 28 if scene == "title" else 37)
        melody = BRIDGE[phrase] if section == 2 else THEME[phrase]
        lead = 10 if scene == "deck" else 5 if scene == "lobby" or section == 2 else 1
        offset = 0
        for pitch, duration in melody:
            shift = -12 if scene == "deck" else 0
            velocity = 47 if quiet else 63
            s.note(lead, pitch + shift, at + offset, duration - .11, velocity * strength)
            if section == 3 and not quiet and duration >= 1.5:
                s.note(7, pitch - 12, at + offset + .03, duration - .19, 35)
            offset += duration
        if quiet and phrase in (1, 5):
            s.note(10, chord[4] + 24, at + 3, .75, 24)
    assert not set(s.programs.values()).intersection({52, 53, 54, 55, *range(96, 128)})
    return s


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, audio=False):
    target = Path(str(path) + ".meta")
    if target.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "firstgame/modern-card-audio/" + str(path.relative_to(ROOT))).hex
    if path.is_dir():
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    elif audio:
        body = (ROOT / "Games/FantasyCards/Assets/Resources/Audio/starlight-overture.ogg.meta").read_text().split("AudioImporter:", 1)[1]
        body = "AudioImporter:" + body
    elif path.suffix in (".json", ".txt"):
        body = "TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        body = "DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target.write_text("fileFormatVersion: 2\nguid: " + guid + "\n" + body)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--soundfont", type=Path, default=Path("/workspace/tools/audio/fluid-r3/usr/share/sounds/sf2/FluidR3_GM.sf2"))
    parser.add_argument("--work", type=Path, default=Path("/tmp/firstgame-modern-card-music"))
    args = parser.parse_args()
    assert digest(args.soundfont) == base.FONT_SHA
    args.work.mkdir(parents=True, exist_ok=True)
    scores = ROOT / "Tools/Audio/ModernCards/Scores"
    resources = ROOT / "Games/FantasyCards/Assets/Resources/Audio/ModernCard"
    streaming = ROOT / "Games/FantasyCards/Assets/StreamingAssets/Audio/ModernCard"
    for folder in (scores, resources, streaming):
        folder.mkdir(parents=True, exist_ok=True)
    for folder in (resources, streaming.parent.parent, streaming.parent, streaming):
        meta(folder)
    report = {"revision": "modern-card-audio-2026-10-26", "composition": "Original FirstGame instrumental arrangements; no FM recording, streamed media, speech or vocal instruments.",
              "renderer": str(RENDERER.relative_to(ROOT)), "rendererSha256": digest(RENDERER), "soundfontSha256": base.FONT_SHA,
              "sampleLicense": "MIT; Tools/Audio/FluidR3-LICENSE.txt", "tracks": []}
    for scene, (_, title) in SETTINGS.items():
        score = compose(scene)
        midi = scores / (scene + ".mid")
        base.write_midi(score, midi)
        score_path = scores / (scene + ".json")
        score_path.write_text(json.dumps({"title": title, "tempo": score.tempo, "bars": score.bars, "programs": score.programs, "notes": score.notes}, ensure_ascii=False, indent=2) + "\n")
        wav = base.render(score, args.soundfont, args.work)
        normalized = args.work / (scene + "-normalized.wav")
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(wav),
                        "-af", "highpass=f=45,loudnorm=I=-18:TP=-1.5:LRA=10", "-ar", "44100", "-c:a", "pcm_f32le", str(normalized)], check=True)
        rate, samples = wavfile.read(normalized)
        ramp = 133
        samples[:ramp] *= np.linspace(0, 1, ramp)[:, None]
        samples[-ramp:] *= np.linspace(1, 0, ramp)[:, None]
        assert np.isfinite(samples).all() and np.max(np.abs(samples)) < .98
        final = args.work / (scene + ".wav")
        wavfile.write(final, rate, samples)
        ogg = resources / (scene + ".ogg")
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(final), "-c:a", "libvorbis", "-q:a", "5",
                        "-metadata", "title=" + title, "-metadata", "artist=FirstGame original instrumental score",
                        "-metadata", "comment=Speech-free original composition. FluidR3 GM samples (MIT).", str(ogg)], check=True)
        meta(ogg, True)
        public = streaming / ogg.name
        shutil.copyfile(ogg, public)
        meta(public, True)
        mp3 = streaming / (scene + ".mp3")
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(final), "-c:a", "libmp3lame", "-q:a", "3",
                        "-metadata", "title=" + title, "-metadata", "artist=FirstGame original instrumental score", str(mp3)], check=True)
        meta(mp3, True)
        decoded = args.work / (scene + "-decoded.wav")
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(ogg), "-c:a", "pcm_f32le", str(decoded)], check=True)
        _, output = wavfile.read(decoded)
        assert np.isfinite(output).all() and np.max(np.abs(output)) < 1
        windows = [float(np.sqrt(np.mean(output[i:i+4410] ** 2))) for i in range(0, len(output)-4410, 4410)]
        entry = {"scene": scene, "title": title, "resource": str(ogg.relative_to(ROOT)), "publicFile": str(public.relative_to(ROOT)),
                 "seconds": round(len(output)/rate, 6), "tempo": score.tempo, "bars": score.bars, "noteCount": len(score.notes),
                 "programs": score.programs, "bytes": ogg.stat().st_size, "sha256": digest(ogg), "publicSha256": digest(public),
                 "mp3Fallback": str(mp3.relative_to(ROOT)), "mp3Sha256": digest(mp3),
                 "midiSha256": digest(midi), "scoreSha256": digest(score_path),
                 "peakDbFS": round(float(20*np.log10(np.max(np.abs(output)))), 3),
                 "rmsDbFS": round(float(20*np.log10(np.sqrt(np.mean(output*output)))), 3),
                 "minimum100msRmsDbFS": round(float(20*np.log10(min(windows))), 3), "clippedSamples": int(np.count_nonzero(np.abs(output)>=1)),
                 "seamSampleDifference": float(np.max(np.abs(output[0]-output[-1]))),
                 "loop": "Second full score cycle retains ambience tails; dominant-to-tonic cadence, no inserted silence, 3ms sample-boundary ramps."}
        assert entry["sha256"] == entry["publicSha256"]
        report["tracks"].append(entry)
        print(json.dumps(entry, ensure_ascii=False), flush=True)
    report["generatorSha256"] = digest(Path(__file__))
    (scores.parent / "manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    public_manifest = {"revision": report["revision"], "tracks": [{k:t[k] for k in ("scene", "title", "seconds", "tempo", "sha256", "mp3Sha256")} for t in report["tracks"]]}
    (streaming / "manifest.json").write_text(json.dumps(public_manifest, ensure_ascii=False, indent=2) + "\n")
    meta(streaming / "manifest.json")


if __name__ == "__main__":
    main()
