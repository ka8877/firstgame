#!/usr/bin/env python3
"""Render an original quiet Nativity cradle arrangement with the existing MIT SoundFont.

Source generation only; run by the release owner, never during a Unity session.
This modern illustrative score is not a claim about music heard in ancient Judea.
"""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import subprocess
import uuid

import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
NS = uuid.UUID('524ec079-47f6-4261-90e4-b628ecf13682')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, folder=False, text=False):
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = uuid.uuid5(NS, str(path.relative_to(ROOT))).hex
    if folder:
        body = 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    elif text:
        body = 'TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    else:
        body = '''AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.55
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData: Original quiet Nativity score. Tools/Audio/Nativity; FluidR3 GM MIT samples.
  assetBundleName:
  assetBundleVariant:
'''
    target.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + body)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--soundfont', type=Path, default=Path('/workspace/tools/audio/fluid-r3/usr/share/sounds/sf2/FluidR3_GM.sf2'))
    parser.add_argument('--work', type=Path, default=Path('/tmp/firstgame-nativity-audio'))
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('nativity_score_base', HERE.parent / 'compose-soundtracks.py')
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    assert digest(args.soundfont) == base.FONT_SHA
    args.work.mkdir(parents=True, exist_ok=True)
    target_dir = ROOT / 'Games/PathOfDawn/Assets/Resources/Audio/GospelNativity'
    target_dir.mkdir(parents=True, exist_ok=True)
    score = base.Score('the-quiet-of-bethlehem', 68, 24, {0: 73, 1: 48, 2: 46, 3: 42})
    chords = [(38, [50, 57, 62, 65]), (36, [48, 55, 60, 64]), (43, [50, 55, 62, 67]), (38, [50, 57, 62, 65]),
              (43, [50, 58, 62, 67]), (41, [53, 60, 65, 69]), (36, [52, 55, 60, 64]), (38, [50, 57, 62, 65])]
    melody = [[(74, 1.5), (77, .5), (76, 2)], [(72, 2), (76, 1), (74, 1)],
              [(74, 1), (79, 1), (77, 2)], [(76, 1.5), (74, 2.5)],
              [(74, 2), (77, 1), (79, 1)], [(77, 2), (76, 1), (72, 1)],
              [(72, 1), (74, 1), (76, 2)], [(74, 3), (69, 1)]]
    for bar in range(score.bars):
        phrase = bar % 8
        section = bar // 8
        start = bar * 4
        root, chord = chords[phrase]
        strength = (0.92, 1, 0.89)[section]
        for j, note in enumerate(chord):
            score.note(1, note, start + j * .025, 3.90, (31 + j * 2) * strength)
        score.note(3, root, start, 3.85, 34 * strength)
        for at, offset in [(0, 0), (1.5, 1), (2.5, 2)]:
            score.note(2, chord[offset], start + at, 1.40, (32 if at == 0 else 26) * strength)
        if section == 1:
            score.note(2, chord[-1] + 12, start + 3.3, .60, 23)
        cursor = 0
        for note, duration in melody[phrase]:
            score.note(0, note if section != 2 else note - 12, start + cursor, max(.12, duration - .13), 40 * strength)
            cursor += duration
    midi = HERE / 'the-quiet-of-bethlehem.mid'
    base.write_midi(score, midi)
    (HERE / 'the-quiet-of-bethlehem.json').write_text(json.dumps({
        'title': score.name, 'tempo': score.tempo, 'bars': score.bars,
        'meter': '4/4', 'programs': score.programs, 'notes': score.notes,
        'intent': 'Original restrained flute, bowed strings, low cello and sparse harp; no bells, reward tones, percussion or quoted carol.',
    }, ensure_ascii=False, indent=2) + '\n')
    rendered = base.render(score, args.soundfont, args.work)
    normalized = args.work / 'birth-normalized.wav'
    subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(rendered),
                    '-af', 'loudnorm=I=-23:TP=-3:LRA=8', '-ar', str(base.RATE), '-c:a', 'pcm_f32le', str(normalized)], check=True)
    rate, samples = wavfile.read(normalized)
    edge = round(.003 * rate)
    samples[:edge] *= np.linspace(0, 1, edge)[:, None]
    samples[-edge:] *= np.linspace(1, 0, edge)[:, None]
    assert np.isfinite(samples).all() and np.max(np.abs(samples)) < .99
    final_wav = args.work / 'birth-final.wav'
    wavfile.write(final_wav, rate, samples)
    target = target_dir / 'music-birth.ogg'
    subprocess.run(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(final_wav),
                    '-c:a', 'libvorbis', '-q:a', '3', '-metadata', 'title=The Quiet of Bethlehem',
                    '-metadata', 'artist=FirstGame original score', '-metadata', 'comment=Original modern illustrative score; FluidR3 GM MIT samples', str(target)], check=True)
    assert target.stat().st_size < 1_300_000, 'Keep the new Nativity score within the reviewed site budget.'
    notice = target_dir / 'SOURCE-NOTICE.txt'
    notice.write_text((HERE / 'SOURCE-NOTICE.txt').read_text())
    meta(target_dir, folder=True); meta(target); meta(notice, text=True)
    report = {'formatVersion': 1, 'revision': 'gospel-nativity-2026-11-02', 'title': 'The Quiet of Bethlehem',
              'provider': 'Original composition rendered locally with FluidR3 GM MIT instrument samples',
              'historicalScope': 'Modern illustrative Nativity music, not reconstructed ancient music or a borrowed Christmas recording.',
              'soundfontSha256': base.FONT_SHA, 'sampleLicense': 'Tools/Audio/FluidR3-LICENSE.txt',
              'generator': {'file': str(Path(__file__).relative_to(ROOT)), 'sha256': digest(Path(__file__))},
              'baseRenderer': {'file': str((HERE.parent / 'compose-soundtracks.py').relative_to(ROOT)), 'sha256': digest(HERE.parent / 'compose-soundtracks.py')},
              'seconds': len(samples) / rate, 'sampleRate': rate, 'channels': 2, 'loop': True,
              'loopMethod': 'Second complete musical cycle retains reverb tails; no inserted silence; three-millisecond endpoint ramps.',
              'peakDbfs': float(20 * np.log10(np.max(np.abs(samples)))),
              'rmsDbfs': float(20 * np.log10(np.sqrt(np.mean(samples.astype(np.float64) ** 2)))),
              'clip': {'file': str(target.relative_to(ROOT)), 'bytes': target.stat().st_size, 'sha256': digest(target)},
              'midi': {'file': str(midi.relative_to(ROOT)), 'sha256': digest(midi)},
              'score': {'file': str((HERE / 'the-quiet-of-bethlehem.json').relative_to(ROOT)), 'sha256': digest(HERE / 'the-quiet-of-bethlehem.json')},
              'limits': ['Decoded signal/loop verification is separate from human listening and acting approval.']}
    (HERE / 'production.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'file': report['clip']['file'], 'bytes': report['clip']['bytes'], 'seconds': report['seconds'], 'sha256': report['clip']['sha256']}))


if __name__ == '__main__':
    main()
