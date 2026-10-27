#!/usr/bin/env python3
"""Deterministic original FM remaster effects; no third-party recordings or APIs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import uuid
import wave

import numpy as np
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Games/FantasyCards/Assets/Resources/Audio/FmRestoration"
HERE = Path(__file__).resolve().parent
RATE = 44100
NAMESPACE = uuid.UUID("dc726eef-ccdc-4192-92ef-72d98998bc91")


class Sound:
    def __init__(self, duration: float, seed: int):
        self.t = np.arange(round(duration * RATE)) / RATE
        self.x = np.zeros(len(self.t))
        self.rng = np.random.default_rng(seed)

    def tone(self, hz, onset=0, decay=.12, gain=1, end_hz=None, attack=.002):
        t = self.t - onset
        local = np.maximum(t, 0)
        phase = 2 * np.pi * (hz * local if end_hz is None else
                              hz * local + (end_hz - hz) / max(decay * 3, .01) * local ** 2 / 2)
        env = (1 - np.exp(-local / attack)) * np.exp(-local / decay) * (t >= 0)
        self.x += gain * np.sin(phase) * env

    def noise(self, onset=0, duration=.15, gain=1, low=300, high=6500, sharp=False):
        t = self.t - onset
        p = t / duration
        env = np.maximum(0, np.sin(np.pi * np.clip(p, 0, 1))) ** (.35 if sharp else 1.4)
        env *= (p >= 0) & (p <= 1)
        sos = butter(2, [low, high], btype="bandpass", fs=RATE, output="sos")
        filtered = sosfilt(sos, self.rng.standard_normal(len(self.t)))
        self.x += gain * filtered * env

    def struck(self, hz, onset=0, decay=.12, gain=1):
        for ratio, weight, damping in ((1, 1, 1), (2.74, .33, .55), (5.12, .13, .30)):
            self.tone(hz * ratio, onset, decay * damping, gain * weight)

    def paper(self, onset=0, duration=.13, gain=.7):
        self.noise(onset, duration, gain, 1250, 11000)
        self.noise(onset + .015, duration * .50, gain * .18, 160, 800)


def build(cue: str, index: int) -> tuple[np.ndarray, float]:
    durations = {
        "ui-hover": .095, "click": .16, "confirm": .31, "cancel": .22, "error": .32,
        "card-select": .23, "card-pick": .20, "card-draw": .26, "card-shuffle": 1.45,
        "summon": .62, "terrain": .72, "equip": .52, "spell": .92, "heal": .92,
        "coin": .42, "attack": .37, "counter": .37, "hit": .39, "shield": .52,
        "destroy": .72, "burn": .65, "turn": .66, "victory": 1.80, "loss": 1.50, "save": .49,
    }
    s = Sound(durations[cue], 130271 + index * 817)
    peak = .62
    if cue == "ui-hover":
        s.struck(1120, decay=.021, gain=.34); peak = .18
    elif cue == "click":
        s.noise(duration=.028, gain=.45, low=800, high=6500)
        s.struck(740, decay=.032, gain=.7); s.tone(185, decay=.018, gain=.15); peak = .42
    elif cue == "confirm":
        s.struck(660, decay=.065, gain=.7); s.struck(990, .062, .09, .55)
        s.paper(duration=.05, gain=.08); peak = .49
    elif cue == "cancel":
        s.struck(660, decay=.045, gain=.65); s.struck(440, .05, .055, .42); peak = .44
    elif cue == "error":
        s.struck(294, decay=.055, gain=.55); s.struck(277, .07, .05, .38)
        s.noise(duration=.07, gain=.06, low=300, high=1200); peak = .48
    elif cue in ("card-select", "card-pick", "card-draw"):
        s.paper(duration=.13 if cue != "card-pick" else .09, gain=.45)
        s.struck(330 if cue == "card-select" else 260, .035, .032, .3)
        if cue == "card-draw": s.paper(.075, .095, .20)
        peak = .49 if cue != "card-select" else .43
    elif cue == "card-shuffle":
        for i in range(16):
            onset = .012 + i * .076
            s.paper(onset, .047 + i % 3 * .008, .32 + (i % 4) * .045)
            s.tone(180 + i % 4 * 33, onset, .009, .11)
        s.struck(190, 1.27, .026, .35); s.paper(1.25, .10, .36)
        peak = .59
    elif cue == "summon":
        s.noise(duration=.27, gain=.5, low=160, high=3300)
        s.tone(110, .095, .12, .8, 64)
        s.struck(392, .13, .12, .22); s.struck(588, .16, .13, .13)
    elif cue == "terrain":
        s.noise(duration=.29, gain=.45, low=90, high=1700)
        s.paper(.015, .12, .2); s.tone(78, .08, .16, .8, 50)
        s.struck(294, .17, .16, .21); s.struck(440, .24, .14, .13)
    elif cue == "equip":
        s.paper(duration=.14, gain=.17)
        for hz, onset, gain in ((810, .03, .35), (1390, .085, .23), (2087, .10, .15)):
            s.struck(hz, onset, .08, gain)
        s.tone(150, .08, .06, .38)
    elif cue in ("spell", "heal"):
        notes = (392, 523.25, 783.99, 1046.5) if cue == "spell" else (523.25, 659.25, 783.99, 1046.5)
        s.noise(duration=.43, gain=.24, low=1200, high=9000)
        for i, hz in enumerate(notes):
            s.struck(hz, .02 + i * .063, .19, .29)
            s.tone(hz * 2, .08 + i * .065, .11, .045)
        if cue == "spell": s.tone(147, .17, .12, .4, 73)
        else: s.tone(261.63, .14, .18, .18)
    elif cue == "coin":
        for onset, gain in ((.006, .46), (.088, .21), (.165, .10)):
            for hz, weight in ((1730, 1), (2675, .54), (4250, .21)):
                s.tone(hz, onset, .072, gain * weight)
            s.noise(onset, .018, .15 * gain, 1600, 9000)
        peak = .55
    elif cue in ("attack", "counter"):
        s.noise(duration=.24, gain=.8, low=650, high=8500)
        s.tone(185 if cue == "attack" else 220, .035, .07, .20, 80)
        s.tone(1470, .13, .033, .12)
    elif cue == "hit":
        s.noise(duration=.07, gain=.8, low=160, high=5200, sharp=True)
        s.noise(.012, .15, .38, 90, 1700)
        s.tone(112, decay=.067, gain=.95, end_hz=46)
        s.tone(245, decay=.035, gain=.33)
    elif cue == "shield":
        s.noise(duration=.05, gain=.33, low=800, high=7000)
        for hz, gain in ((575, .8), (972, .35), (1598, .25), (2480, .12)):
            s.tone(hz, decay=.105, gain=gain)
        s.tone(96, decay=.06, gain=.25)
    elif cue == "destroy":
        s.noise(duration=.37, gain=.48, low=70, high=3600)
        s.tone(149, decay=.16, gain=.6, end_hz=33)
        for i in range(6): s.noise(.10 + i * .057, .032, .23 / (1 + i * .13), 900, 10000)
    elif cue == "burn":
        s.noise(duration=.42, gain=.46, low=250, high=9500)
        for i in range(11): s.noise(.02 + i * .033, .009, .27, 1400, 12000, True)
        s.tone(80, .03, .13, .17)
    elif cue == "turn":
        for hz, onset in ((392, 0), (587.33, .095), (784, .19)):
            s.struck(hz, onset, .125, .40)
        s.paper(.04, .11, .075); peak = .52
    elif cue == "victory":
        for i, notes in enumerate(((392, 493.88, 587.33), (440, 554.37, 659.25), (523.25, 659.25, 783.99))):
            for hz in notes:
                s.struck(hz, i * .23, .24 if i < 2 else .38, .21)
        s.tone(130.81, .46, .28, .24); s.noise(.38, .30, .12, 2500, 12000)
    elif cue == "loss":
        for i, hz in enumerate((392, 349.23, 293.66, 261.63)):
            s.struck(hz, i * .18, .25, .32)
        s.tone(65.41, .35, .25, .31)
    elif cue == "save":
        s.struck(660, decay=.09, gain=.5); s.struck(880, .09, .13, .40)
        s.paper(duration=.06, gain=.12); peak = .49
    else:
        raise ValueError(cue)
    # High-pass removes DC/inaudible rumble; a short stereo early reflection gives
    # depth without phase cancellation or a long tail masking the next action.
    dry = sosfilt(butter(2, 55, btype="highpass", fs=RATE, output="sos"), s.x)
    dry = np.tanh(dry * .90)
    left, right = dry.copy(), dry.copy()
    for delay, amount in ((.017, .12), (.043, .055)):
        shift = round(delay * RATE)
        left[shift:] += dry[:-shift] * amount
        shift += round(.007 * RATE)
        right[shift:] += dry[:-shift] * amount
    audio = np.stack((left, right), axis=1)
    # Deterministic silence-safe start/end ramp and per-cue headroom.
    edge = min(round(.006 * RATE), len(audio) // 4)
    audio[:edge] *= np.linspace(0, 1, edge)[:, None]
    tail = min(round(.035 * RATE), len(audio) // 3)
    audio[-tail:] *= np.linspace(1, 0, tail)[:, None]
    audio *= peak / max(np.max(np.abs(audio)), 1e-8)
    return audio, peak


def meta(path: Path, folder=False):
    target = Path(str(path) + ".meta")
    if target.exists(): return
    guid = uuid.uuid5(NAMESPACE, path.relative_to(ROOT).as_posix()).hex
    if folder:
        value = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        value = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: Original deterministic remaster effect. Tools/Audio/Restoration/render-sfx.py
  assetBundleName:
  assetBundleVariant:
"""
    target.write_text(value)


def main():
    OUT.mkdir(parents=True, exist_ok=True); meta(OUT, folder=True)
    cues = ["ui-hover", "click", "confirm", "cancel", "error", "card-select", "card-pick", "card-draw", "card-shuffle",
            "summon", "terrain", "equip", "spell", "heal", "coin", "attack", "counter", "hit", "shield",
            "destroy", "burn", "turn", "victory", "loss", "save"]
    entries = []
    for index, cue in enumerate(cues):
        audio, peak = build(cue, index)
        pcm = np.rint(audio * 32767).astype("<i2")
        path = OUT / (cue + ".wav")
        with wave.open(str(path), "wb") as output:
            output.setnchannels(2); output.setsampwidth(2); output.setframerate(RATE); output.writeframes(pcm.tobytes())
        meta(path)
        actual = pcm.astype(float) / 32768
        assert np.isfinite(actual).all() and abs(actual).max() < .63
        assert not np.any((pcm == -32768) | (pcm == 32767))
        assert abs(actual.mean()) < .001
        assert (pcm[0] == 0).all() and (pcm[-1] == 0).all()
        assert np.sqrt(np.mean(actual ** 2)) > .012
        entries.append({"cue": cue, "path": path.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "bytes": path.stat().st_size, "sampleRate": RATE, "channels": 2, "frames": len(pcm),
                        "durationSeconds": len(pcm) / RATE, "peak": float(abs(actual).max()),
                        "rms": float(np.sqrt(np.mean(actual ** 2))), "dc": float(actual.mean()), "clippedSamples": 0})
    result = {"version": "fm-restoration-2026-10-27", "source": "Original offline synthesis; no recordings or third-party samples.",
              "generator": "Tools/Audio/Restoration/render-sfx.py", "numpy": np.__version__, "cues": entries,
              "totalBytes": sum(item["bytes"] for item in entries), "checks": "finite, PCM bounds/headroom, DC, endpoints, non-silent RMS passed for each cue",
              "scope": "File analysis only. Unity/browser playback, actual output and listening require separate verification."}
    (HERE / "sfx-manifest.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"cues": len(entries), "bytes": result["totalBytes"], "minRms": min(item["rms"] for item in entries), "maxPeak": max(item["peak"] for item in entries)}))


if __name__ == "__main__": main()
