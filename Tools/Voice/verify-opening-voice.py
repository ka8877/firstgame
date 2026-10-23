#!/usr/bin/env python3
"""Verify exact Ink mapping, decoded speech signals, and independent Korean ASR.

ASR is an intelligibility check, not human listening or acting approval.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import re
import subprocess
import unicodedata
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--model', type=Path, default=Path('/workspace/tools/voice-models/faster-whisper-small'))
parser.add_argument('--skip-asr', action='store_true')
args = parser.parse_args()
os.environ.setdefault('HF_HOME', str(args.model.parent / 'hf-cache'))
production = json.loads((ROOT / 'Tools/Voice/production.json').read_text())
manifest = json.loads((ROOT / 'Games/PathOfDawn/Assets/Resources/Voice/opening-voice.json').read_text())
story = (ROOT / production['sourceFile']).read_text()
assert len(manifest['lines']) == len(production['lines']) == 20
assert len({x['id'] for x in manifest['lines']}) == 20
assert len({x['text'] for x in manifest['lines']}) == 20
assert hashlib.sha256(story.encode()).hexdigest() == production['sourceSha256']


def normalize(text):
    return ''.join(ch for ch in unicodedata.normalize('NFC', text) if ch.isalnum())


def edit_distance(a, b):
    row = list(range(len(b) + 1))
    for i, left in enumerate(a, 1):
        next_row = [i]
        for j, right in enumerate(b, 1):
            next_row.append(min(next_row[-1] + 1, row[j] + 1, row[j - 1] + (left != right)))
        row = next_row
    return row[-1]


model = None
if not args.skip_asr:
    from faster_whisper import WhisperModel
    model = WhisperModel(str(args.model), device='cpu', compute_type='int8', cpu_threads=2, local_files_only=True)

records = []
for line, made in zip(manifest['lines'], production['lines']):
    path = ROOT / made['file']
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert line['id'] == made['id'] and line['text'] == made['text']
    assert line['text'] in story.splitlines()
    assert hashlib.sha256(line['text'].encode()).hexdigest() == line['textSha256']
    assert line['speechText'] == line['text'].split(': ', 1)[-1]
    assert digest == made['sha256']
    assert Path(str(path) + '.meta').is_file()
    decoded = subprocess.check_output(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error',
                                       '-i', str(path), '-f', 'f32le', '-ac', '1', '-ar', '44100', '-'])
    samples = np.frombuffer(decoded, dtype='<f4')
    assert np.isfinite(samples).all()
    duration = len(samples) / 44100
    peak = float(np.max(np.abs(samples)))
    rms = float(np.sqrt(np.mean(samples.astype(np.float64) ** 2)))
    clipping = int(np.sum(np.abs(samples) >= 0.999))
    window = 4410
    windows = samples[:len(samples) // window * window].reshape(-1, window)
    levels = np.sqrt(np.mean(windows.astype(np.float64) ** 2, axis=1))
    active_fraction = float(np.mean(levels > 0.004))
    peak_db = float(20 * np.log10(max(peak, 1e-12)))
    rms_db = float(20 * np.log10(max(rms, 1e-12)))
    checks = {'durationBetween2And30Seconds': 2 < duration < 30,
              'noClipping': clipping == 0, 'audibleSpeechRms': -35 < rms_db < -9,
              'decodedSamplePeakWithinRange': -20 < peak_db < -0.5, 'activeSpeechWindows': active_fraction > 0.3,
              'mono44100': made['sampleRate'] == 44100 and made['channels'] == 1}
    result = {'id': line['id'], 'file': made['file'], 'sha256': digest, 'durationSeconds': duration,
              'peakDbfs': peak_db, 'rmsDbfs': rms_db, 'clippingSamples': clipping,
              'active100msFraction': active_fraction, 'checks': checks}
    if model:
        speech_16k = np.frombuffer(subprocess.check_output([
            'ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-i', str(path),
            '-f', 'f32le', '-ac', '1', '-ar', '16000', '-']), dtype='<f4').copy()
        segments, info = model.transcribe(speech_16k, language='ko', beam_size=5,
                                          vad_filter=False, condition_on_previous_text=False)
        transcript = ''.join(segment.text for segment in segments).strip()
        expected, actual = normalize(line['speechText']), normalize(transcript)
        errors = edit_distance(expected, actual)
        cer = errors / len(expected)
        result['asr'] = {'model': 'Systran/faster-whisper-small', 'language': 'ko', 'beamSize': 5,
                         'prompt': None, 'expectedSpokenText': line['speechText'], 'transcript': transcript,
                         'normalizedExpectedCharacters': len(expected), 'characterEdits': errors,
                         'characterErrorRate': cer,
                         'normalization': 'Unicode NFC; remove whitespace/punctuation only; no lexical correction'}
        checks['independentAsrCerBelow20Percent'] = cer <= 0.20
        print(line['id'], f'CER={cer:.3f}', transcript, flush=True)
    else:
        print(line['id'], 'decoded/mapping checked', flush=True)
    result['status'] = 'passed' if all(checks.values()) else 'needs-review'
    records.append(result)

overall = None
if model:
    overall = sum(x['asr']['characterEdits'] for x in records) / sum(x['asr']['normalizedExpectedCharacters'] for x in records)
passed = all(x['status'] == 'passed' for x in records) and (overall is None or overall <= 0.10)
report = {'checkedUtc': datetime.now(timezone.utc).isoformat(), 'status': 'passed' if passed else 'needs-review',
          'scope': 'Offline source text and decoded final Ogg plus independent ASR; not Unity/browser playback or physical listening.',
          'lineCount': len(records), 'totalSeconds': sum(x['durationSeconds'] for x in records),
          'totalBytes': sum(x['bytes'] for x in production['lines']), 'asrPerformed': model is not None,
          'overallCharacterErrorRate': overall, 'lines': records}
target = ROOT / ('Tools/Voice/verification.json' if model else 'Artifacts/Voice/file-verification.json')
target.parent.mkdir(parents=True, exist_ok=True)
if target.exists():
    previous = ROOT / 'Artifacts/Voice' / (target.stem + '.previous-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '.json')
    previous.parent.mkdir(parents=True, exist_ok=True)
    previous.write_bytes(target.read_bytes())
target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print('Voice verification:', report['status'], 'overall CER:', overall)
raise SystemExit(0 if passed else 1)
