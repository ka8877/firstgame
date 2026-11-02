#!/usr/bin/env python3
"""Author Reprise impact layers and three original Gospel chapter arrangements.

No third-party recording, existing melody, voice or commercial-game sample is
used. Orchestral samples are the existing MIT-licensed FluidR3 GM instrument
library. All pseudo-randomness is offline and fixed; gameplay RNG is untouched.
"""
from pathlib import Path
import hashlib, importlib.util, json, subprocess, uuid, wave
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
WORK = Path('/tmp/firstgame-reprise-audio')
RATE = 44100
NS = uuid.UUID('ff9ca0c0-79af-4eaf-b795-71ef6449d88c')
FONT = Path('/workspace/tools/audio/fluid-r3/usr/share/sounds/sf2/FluidR3_GM.sf2')


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


base = load('reprise_sfx_base', HERE.parent / 'Restoration/render-sfx.py')
score_base = load('reprise_score_base', HERE.parent / 'compose-soundtracks.py')


def meta(path, folder=False, compressed=False):
    relative = str(path.relative_to(ROOT))
    target = Path(str(path) + '.meta')
    if target.exists():
        return
    guid = uuid.uuid5(NS, relative).hex
    if folder:
        target.write_text(f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    else:
        target.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {1 if compressed else 0}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: {1 if compressed else 0}
    quality: {0.8 if compressed else 1}
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: {1 if compressed else 0}
  ambisonic: 0
  3D: 0
  userData: Original authored Reprise audio. Tools/Audio/Reprise/render-audio.py; FluidR3 samples MIT for scores only.
  assetBundleName:
  assetBundleVariant:
''')


def surround(dry, peak=.60, short=False):
    dry = sosfilt(butter(2, 45, btype='highpass', fs=RATE, output='sos'), dry)
    dry -= dry.mean()
    out = np.stack([dry.copy(), dry.copy()], axis=1)
    for delay, gain in ((.011, .10), (.031, .065), (.067, .037), (.109, .02)):
        if short and delay > .04:
            continue
        left = round(delay * RATE)
        right = left + round(.004 * RATE)
        out[left:, 0] += dry[:-left] * gain
        out[right:, 1] += dry[:-right] * gain
    edge, tail = round(.002 * RATE), round(.075 * RATE)
    out[:edge] *= np.linspace(0, 1, edge)[:, None]
    out[-tail:] *= np.linspace(1, 0, tail)[:, None]
    out *= peak / max(abs(out).max(), 1e-8)
    return out


def impact(cue, index):
    durations = {'summon': .95, 'terrain': .86, 'equip': .54, 'spell': 1.04,
                 'heal': 1.09, 'attack': .34, 'counter': .34, 'hit': .46,
                 'hit-heavy': .61, 'shield': .58, 'destroy': .96, 'burn': .73}
    s = base.Sound(durations[cue], 20261102 + index * 1193)
    if cue in ('attack', 'counter'):
        s.noise(.0, .17, .56, 450, 9500)
        s.noise(.06, .13, .34, 120, 2100)
        s.tone(370 if cue == 'attack' else 440, .01, .07, .18, 96)
        s.tone(1900, .09, .027, .06)
    elif cue in ('hit', 'hit-heavy'):
        heavy = cue == 'hit-heavy'
        s.noise(.0, .038, .94, 180, 11000, True)
        s.noise(.017, .13, .44, 70, 1800, True)
        s.tone(138 if heavy else 177, 0, .055, .80, 49)
        s.tone(68 if heavy else 86, .009, .105, .65, 43)
        for hz, weight in ((388, .22), (713, .13), (1234, .06)):
            s.tone(hz, .005, .039, weight)
        if heavy:
            s.noise(.055, .16, .24, 180, 3400)
            s.tone(51, .032, .14, .31)
    elif cue == 'shield':
        s.noise(.0, .038, .52, 1600, 11000, True)
        for hz, weight in ((423, .72), (693, .43), (1087, .27), (1719, .15), (2761, .04)):
            s.tone(hz, .004, .095, weight)
        s.tone(112, .006, .06, .32)
    elif cue in ('summon', 'terrain'):
        land = cue == 'terrain'
        s.paper(.0, .11, .31)
        s.noise(.02, .27, .44, 130 if land else 380, 2500 if land else 7500)
        s.tone(72 if land else 116, .12, .14, .69, 48)
        s.noise(.12, .07, .4, 80, 2400, True)
        for i, hz in enumerate((293.66, 440, 587.33)):
            s.struck(hz, .14 + i * .047, .15, .12 if land else .23)
        if not land:
            s.tone(1174.66, .31, .15, .07)
    elif cue == 'equip':
        s.paper(.0, .09, .20)
        for onset, hz, gain in ((.035, 695, .55), (.073, 1149, .35), (.116, 1768, .16)):
            s.struck(hz, onset, .075, gain)
        s.tone(142, .04, .04, .28)
    elif cue in ('spell', 'heal'):
        s.noise(.0, .42, .28, 700, 9500)
        notes = (587.33, 739.99, 880, 1174.66) if cue == 'heal' else (293.66, 440, 659.25, 1174.66)
        for i, hz in enumerate(notes):
            s.struck(hz, .045 + i * .062, .21, .22)
            s.tone(hz * 2, .09 + i * .07, .14, .035)
        s.tone(146.83 if cue == 'heal' else 73.41, .19, .19, .21)
    elif cue == 'destroy':
        s.noise(.0, .072, .53, 70, 6500, True)
        s.tone(174, .0, .18, .69, 30)
        s.noise(.075, .43, .33, 110, 4200)
        for i in range(8):
            s.noise(.11 + i * .061, .018, .20 / (1 + i * .18), 1600, 12000, True)
    elif cue == 'burn':
        s.paper(.0, .10, .3)
        s.noise(.035, .43, .37, 160, 10000)
        for i in range(10):
            s.noise(.05 + i * .043, .008, .15, 1300, 14000, True)
        s.tone(75, .02, .11, .19)
    return surround(s.x, .66 if cue == 'hit-heavy' else .59)


def gospel_effect(cue, index):
    durations = {'footstep': .18, 'jump': .28, 'land': .28, 'page': .53,
                 'dialogue': .31, 'scripture': 1.52, 'ui': .15, 'dash': .36}
    s = base.Sound(durations[cue], 731102 + index * 2017)
    if cue in ('footstep', 'land'):
        gain = .8 if cue == 'land' else .45
        s.noise(.0, .065, gain, 90, 1800, True)
        s.noise(.018, .08, gain * .27, 1400, 7000)
        s.tone(119 if cue == 'land' else 187, .0, .028, gain * .34, 68)
    elif cue == 'jump':
        s.noise(.0, .15, .31, 650, 6500)
        s.tone(190, .015, .065, .15, 440)
    elif cue == 'dash':
        s.noise(.0, .24, .40, 380, 7600)
        s.noise(.065, .16, .16, 130, 1700)
        s.tone(220, .0, .071, .11, 115)
    elif cue == 'page':
        s.paper(.0, .20, .35)
        s.paper(.15, .18, .22)
        s.noise(.31, .035, .13, 200, 2800)
    elif cue == 'dialogue':
        s.struck(587.33, .0, .075, .25)
        s.struck(880, .055, .08, .11)
    elif cue == 'scripture':
        for i, hz in enumerate((293.66, 440, 587.33, 739.99, 1174.66)):
            s.struck(hz, .04 + i * .105, .26, .19)
            s.tone(hz * 2, .10 + i * .11, .23, .025)
        s.noise(.12, .57, .06, 1300, 9500)
    else:
        s.noise(.0, .021, .28, 500, 4800)
        s.struck(440, .006, .028, .22)
    return surround(s.x, .38 if cue == 'scripture' else .31, short=cue in ('footstep', 'land', 'ui'))


def write_pcm(path, samples):
    pcm = np.rint(np.clip(samples, -1, 1) * 32767).astype('<i2')
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(pcm.tobytes())
    meta(path)
    assert np.isfinite(samples).all() and abs(samples).max() < .9
    assert abs(samples.mean()) < .001 and (pcm[0] == 0).all() and (pcm[-1] == 0).all()
    return measurements(path, samples)


def measurements(path, samples):
    return {'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'seconds': len(samples) / RATE, 'rms': float(np.sqrt(np.mean(samples * samples))),
            'peak': float(abs(samples).max()), 'dc': float(samples.mean()),
            'clippedSamples': int(np.sum(abs(samples) >= 1))}


def chapter_score(chapter):
    # One original motif links the three distinct instrumental arrangements.
    # Sparse phrasing deliberately leaves space for the spoken/story dialogue.
    tempo = {'birth': 72, 'temple': 80, 'jordan': 76}[chapter]
    s = score_base.Score('storybook-' + chapter, tempo, 16,
                         {0: 73, 1: 0, 2: 48, 3: 46, 4: 42, 5: 68, 6: 88, 7: 11})
    harmony = [(38, [50, 57, 61, 66]), (35, [54, 57, 62, 66]),
               (31, [50, 55, 59, 66]), (33, [52, 57, 61, 64]),
               (38, [50, 54, 57, 64]), (35, [50, 54, 59, 66]),
               (31, [50, 55, 59, 62]), (33, [52, 57, 61, 66])]
    motif = [[(74, 1.5), (78, .5), (81, 2)], [(78, 2), (76, 1), (74, 1)],
             [(71, 1.5), (74, .5), (78, 2)], [(76, 2.5), (73, 1.5)],
             [(74, 2), (81, 1), (78, 1)], [(78, 1.5), (76, .5), (74, 2)],
             [(71, 2), (74, 2)], [(73, 2), (69, 2)]]
    for bar in range(16):
        root, chord = harmony[bar % 8]
        start = bar * 4
        for i, pitch in enumerate(chord):
            s.note(2, pitch, start + i * .025, 3.88, 30 if chapter == 'birth' else 34)
        s.note(4, root, start, 3.85, 35)
        order = [0, 2, 1, 3, 2, 1, 3, 2] if chapter == 'jordan' else [0, 2, 3, 1]
        step = .5 if chapter == 'jordan' else 1
        for i, chord_index in enumerate(order):
            s.note(3, chord[chord_index] + 12, start + i * step, .95, 31 + (i % 4 == 0) * 6)
        for i, pitch in enumerate(chord[:3]):
            s.note(1, pitch, start + .035 * i, 2.3, 34)
        if bar % 2 == 0 or chapter == 'jordan':
            offset = 0
            for pitch, duration in motif[bar % 8]:
                lead = 7 if chapter == 'birth' else 5 if chapter == 'temple' else 0
                s.note(lead, pitch, start + offset, max(.18, duration - .12), 43 if chapter == 'birth' else 49)
                offset += duration
        if chapter == 'jordan' and bar >= 8:
            s.note(6, chord[-1], start + 2, 1.6, 23)
    return s


def music(chapter, output):
    s = chapter_score(chapter)
    midi = HERE / (s.name + '.mid')
    score_base.write_midi(s, midi)
    (HERE / (s.name + '.json')).write_text(json.dumps({'title': s.name,
        'tempo': s.tempo, 'bars': s.bars, 'programs': s.programs, 'notes': s.notes}, indent=2) + '\n')
    source = score_base.render(s, FONT, WORK)
    rate, samples = wavfile.read(source)
    assert rate == RATE
    # Preserve the complete second musical cycle and its reverb carry. Tiny
    # endpoint ramps remove a discontinuity without adding a silence interval.
    samples -= samples.mean(axis=0)
    samples *= .42 / max(abs(samples).max(), 1e-8)
    edge = 133
    samples[:edge] *= np.linspace(0, 1, edge)[:, None]
    samples[-edge:] *= np.linspace(1, 0, edge)[:, None]
    wav = WORK / (s.name + '-master.wav')
    wavfile.write(wav, RATE, samples)
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(wav),
                    '-c:a', 'libvorbis', '-q:a', '5', '-metadata', 'title=' + s.name,
                    '-metadata', 'artist=FirstGame original composition', str(output)], check=True)
    meta(output, compressed=True)
    decoded = WORK / (s.name + '-decoded.wav')
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(output),
                    '-c:a', 'pcm_f32le', str(decoded)], check=True)
    _, actual = wavfile.read(decoded)
    row = measurements(output, actual)
    row.update({'bars': s.bars, 'tempo': s.tempo, 'noteCount': len(s.notes),
                'scoreFile': str(midi.relative_to(ROOT)), 'loop': 'Complete second musical cycle; retained reverb; 3ms seam ramps'})
    assert row['rms'] > .015 and row['peak'] < .65 and row['clippedSamples'] == 0
    return row


def ambience(chapter, output):
    seconds = 18
    n = RATE * seconds
    rng = np.random.default_rng(20161102 + len(chapter))
    # Periodic FFT filtering gives continuous end-to-start wind/water texture.
    frequencies = np.fft.rfftfreq(n, 1 / RATE)
    noise = np.fft.rfft(rng.normal(size=n))
    curve = 1 / (1 + (frequencies / (2200 if chapter == 'jordan' else 950)) ** 2)
    curve *= np.minimum(1, frequencies / 90)
    air = np.fft.irfft(noise * curve, n=n)
    air /= max(abs(air).max(), 1e-8)
    t = np.arange(n) / RATE
    air *= .65 + .12 * np.sin(2 * np.pi * t / seconds) + .08 * np.sin(6 * np.pi * t / seconds)
    out = np.stack([air, np.roll(air, 739)], axis=1) * (.21 if chapter == 'jordan' else .15)
    # Distant small bird phrases, synthesized oscillators rather than recordings.
    if chapter != 'temple':
        for onset in (3.1, 8.6, 14.2):
            local = t - onset
            env = np.sin(np.pi * np.clip(local / .21, 0, 1)) ** 2 * ((local >= 0) & (local <= .21))
            phrase = np.sin(2 * np.pi * (2150 * local + 350 * local * local)) * env * .034
            out[:, 0] += phrase; out[:, 1] += np.roll(phrase, 161)
    wav = WORK / ('ambience-' + chapter + '.wav')
    wavfile.write(wav, RATE, out.astype(np.float32))
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(wav),
                    '-c:a', 'libvorbis', '-q:a', '3', str(output)], check=True)
    meta(output, compressed=True)
    return measurements(output, out)


def main():
    WORK.mkdir(parents=True, exist_ok=True)
    assert hashlib.sha256(FONT.read_bytes()).hexdigest() == score_base.FONT_SHA
    card = ROOT / 'Games/FantasyCards/Assets/Resources/Audio/FmReprise'
    gospel = ROOT / 'Games/PathOfDawn/Assets/Resources/Audio/GospelStorybook'
    for folder in (card, gospel):
        folder.mkdir(parents=True, exist_ok=True); meta(folder, folder=True)
    rows = []
    for i, cue in enumerate(('summon', 'terrain', 'equip', 'spell', 'heal', 'attack', 'counter', 'hit', 'hit-heavy', 'shield', 'destroy', 'burn')):
        row = write_pcm(card / (cue + '.wav'), impact(cue, i)); row['cue'] = cue; rows.append(row)
    for i, cue in enumerate(('footstep', 'jump', 'land', 'page', 'dialogue', 'scripture', 'ui', 'dash')):
        row = write_pcm(gospel / (cue + '.wav'), gospel_effect(cue, i)); row['cue'] = cue; rows.append(row)
    for chapter in ('birth', 'temple', 'jordan'):
        rows.append(music(chapter, gospel / ('music-' + chapter + '.ogg')))
        rows.append(ambience(chapter, gospel / ('ambience-' + chapter + '.ogg')))
    result = {'revision': 'reprise-2026-11-02', 'sampleRate': RATE,
              'composition': 'Original authored melodies and deterministic offline synthesis. No existing commercial-game recording or voice.',
              'scoreSamples': 'FluidR3 GM MIT; existing Tools/Audio/FluidR3-LICENSE.txt',
              'soundfontSha256': score_base.FONT_SHA, 'generator': str(Path(__file__).relative_to(ROOT)),
              'scope': 'File/decoded PCM analysis only. Actual Unity/browser playback is separately checked by release owner.', 'files': rows}
    (HERE / 'manifest.json').write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n')
    print(json.dumps({'files': len(rows), 'bytes': sum(r['bytes'] for r in rows), 'status': 'authored'}))


if __name__ == '__main__':
    main()
