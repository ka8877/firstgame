#!/usr/bin/env python3
"""Verify current exact Nativity narration mapping, decoded speech and independent offline Korean ASR."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import subprocess
import unicodedata

import numpy as np

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalize(text):
    return ''.join(ch for ch in unicodedata.normalize('NFC', text) if ch.isalnum())


def edit_distance(left, right):
    row = list(range(len(right) + 1))
    for index, ch in enumerate(left, 1):
        following = [index]
        for other_index, other in enumerate(right, 1):
            following.append(min(following[-1] + 1, row[other_index] + 1, row[other_index - 1] + (ch != other)))
        row = following
    return row[-1]


def decode(path, rate):
    return np.frombuffer(subprocess.check_output([
        'ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error', '-i', str(path),
        '-f', 'f32le', '-ac', '1', '-ar', str(rate), '-']), dtype='<f4').copy()


def preserved_base22():
    """Only reuse the closed actual22 ASR records for identical text AND clip bytes."""
    closure_path = ROOT / 'Artifacts/Validation/Nativity/AudioVoice/base22-closure.json'
    assert digest(closure_path) == '3a748c3102cb7ed2d0128340a484c8a17494afb250a04d1b032cd97a2942b2f5'
    closure = json.loads(closure_path.read_text())
    assert closure['status'] == 'passed' and closure['sourceChangedDuringGenerationAndAsr'] is False and closure['lines'] == 22
    files = {item['file']: item for item in closure['files']}
    for item in files.values():
        saved = ROOT / item['preservedFile']
        assert saved.is_file() and not saved.is_symlink() and digest(saved) == item['sha256'] and saved.stat().st_size == item['bytes']
    report_ref = files['Artifacts/Validation/Nativity/Audio/voice.json']
    prior = json.loads((ROOT / report_ref['preservedFile']).read_text())
    made_ref = files['Tools/Voice/Nativity/production.json']
    made = json.loads((ROOT / made_ref['preservedFile']).read_text())
    assert prior['status'] == 'passed' and prior['totalLines'] == 22 and len(prior['lines']) == 22
    assert prior['production']['sha256'] == made_ref['sha256']
    assert prior['manifest']['sha256'] == files[made['manifest']['file']]['sha256']
    snapshot = ROOT / 'Artifacts/Validation/Nativity/AudioVoice/Source'
    assert digest(snapshot / prior['harness']['file']) == prior['harness']['sha256']
    assert digest(snapshot / made['generator']['file']) == made['generator']['sha256']
    assert digest(snapshot / made['input']['file']) == made['input']['sha256']
    assert datetime.fromisoformat(prior['checkedUtc']) <= datetime.fromisoformat(closure['finishedUtc'])
    original = {item['key']: item for item in made['lines']}
    for item in prior['lines']:
        old = original[item['key']]
        assert item['passed'] is True and all(item['checks'].values())
        assert item['file'] == old['file'] and item['sha256'] == old['sha256'] == files[old['file']]['sha256']
        assert item['asr']['expected'] == old['text'] and item['asr']['prompt'] is None
        assert item['asr']['model'] == 'faster-whisper-small' and item['asr']['language'] == 'ko'
    return {item['key']: item for item in prior['lines']}, original, {
        'closure': {'file': str(closure_path.relative_to(ROOT)), 'sha256': digest(closure_path)},
        'report': {'file': report_ref['preservedFile'], 'sha256': report_ref['sha256']},
        'sourceHarness': {'file': str((snapshot / prior['harness']['file']).relative_to(ROOT)), 'sha256': prior['harness']['sha256']},
        'checkedUtc': prior['checkedUtc'],
        'scope': 'Only22 exact same-text/same-key/same-speaker/same-clip-SHA ASR inferences retain their original dated result. All28 signals/mappings/hashes are freshly checked; only new six lines receive new independent inference. Historical ASR is not relabelled as new inference.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, default=Path('/workspace/tools/voice-models/faster-whisper-small'))
    parser.add_argument('--output', type=Path, default=ROOT / 'Artifacts/Validation/Nativity/Audio/voice.json')
    parser.add_argument('--reuse-base22-asr', action='store_true', help='Reuse only the exact explicitly dated closed22 ASR inferences; decode all28 and infer the new6.')
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    from faster_whisper import WhisperModel
    production = json.loads((HERE / 'production.json').read_text())
    manifest_path = ROOT / production['manifest']['file']
    manifest = json.loads(manifest_path.read_text())
    selected = json.loads((HERE / 'dialogue-lines.json').read_text())['lines']
    assert digest(manifest_path) == production['manifest']['sha256']
    assert digest(ROOT / production['input']['file']) == production['input']['sha256']
    for item in production.get('storySources', [production.get('storySource')]):
        if item is not None:
            assert digest(ROOT / item['file']) == item['sha256']
    assert len(selected) == len(manifest['lines']) == len(production['lines'])
    assert production['speakerCount'] == 1 and production['voiceCloning'] is False
    inherited, inherited_made, inheritance = {}, {}, None
    if args.reuse_base22_asr:
        inherited, inherited_made, inheritance = preserved_base22()
        assert len(selected) == 28 and production['reusedLineCount'] == 22 and production['newlySynthesizedLineCount'] == 6
        assert set(inherited) <= {item['key'] for item in selected}
        assert production['baseGeneration']['sha256'] == inheritance['closure']['sha256']
    model = WhisperModel(str(args.model), device='cpu', compute_type='int8', cpu_threads=2, local_files_only=True)
    results, total_edits, total_characters, fresh_count = [], 0, 0, 0
    for expected, line, made in zip(selected, manifest['lines'], production['lines']):
        assert expected['key'] == line['key'] == made['key']
        assert expected['text'] == line['text'] == line['speechText'] == made['text']
        assert expected['speaker'] == line['speaker'] == made['speaker']
        assert hashlib.sha256(line['text'].encode()).hexdigest() == line['textSha256']
        path = ROOT / made['file']
        assert digest(path) == made['sha256'] and Path(str(path) + '.meta').is_file()
        samples = decode(path, 32000)
        rms = float(np.sqrt(np.mean(samples.astype(np.float64) ** 2)))
        peak = float(np.max(np.abs(samples)))
        rms_db = float(20 * np.log10(max(rms, 1e-12)))
        peak_db = float(20 * np.log10(max(peak, 1e-12)))
        width = 3200
        frames = samples[:len(samples) // width * width].reshape(-1, width)
        activity = np.sqrt(np.mean(frames.astype(np.float64) ** 2, axis=1)) > .004
        active = np.flatnonzero(activity)
        leading_quiet = float(active[0] * .1) if len(active) else len(samples) / 32000
        trailing_quiet = float((len(activity) - 1 - active[-1]) * .1) if len(active) else len(samples) / 32000
        reused = line['key'] in inherited
        if reused:
            old, old_made = inherited[line['key']], inherited_made[line['key']]
            assert old['sha256'] == made['sha256'] and old['file'] == made['file']
            assert all(old_made[field] == line[field] for field in ['key', 'speaker', 'text', 'questId', 'beatIndex'])
            actual = old['asr']['transcript']
            asr_source = {'verificationSource': 'preserved-independent-asr-same-text-and-clip',
                          'checkedUtc': inheritance['checkedUtc'], 'priorReport': inheritance['report']}
        else:
            segments, _ = model.transcribe(decode(path, 16000), language='ko', beam_size=5,
                                          vad_filter=False, condition_on_previous_text=False)
            actual = ''.join(segment.text for segment in segments).strip()
            fresh_count += 1
            asr_source = {'verificationSource': 'fresh-local-independent-asr',
                          'checkedUtc': datetime.now(timezone.utc).isoformat()}
        left, right = normalize(line['text']), normalize(actual)
        edits = edit_distance(left, right)
        cer = edits / max(1, len(left))
        if reused:
            assert abs(cer - inherited[line['key']]['asr']['characterErrorRate']) < 1e-12
        total_edits += edits; total_characters += len(left)
        checks = {'exactCurrentManifestMapping': True, 'finiteDecodedSpeech': bool(np.isfinite(samples).all()),
                  'durationOneToSixtySeconds': 1 < len(samples) / 32000 < 60,
                  'noClipping': bool(np.max(np.abs(samples)) < .999),
                  'audibleSpeechRms': -35 < rms_db < -10, 'peakHeadroom': -20 < peak_db < -.5,
                  'mostlyActiveSpeech': float(np.mean(activity)) > .3,
                  'boundedLeadingAndTrailingSilence': leading_quiet < 1.5 and trailing_quiet < 1.5,
                  'mono32000': made['channels'] == 1 and made['sampleRate'] == 32000,
                  'independentAsrLineCER': cer <= .42}
        result = {'key': line['key'], 'file': made['file'], 'sha256': made['sha256'],
                  'seconds': len(samples) / 32000, 'rmsDbfs': rms_db, 'peakDbfs': peak_db,
                  'leadingQuietSeconds': leading_quiet, 'trailingQuietSeconds': trailing_quiet,
                  'asr': {'model': 'faster-whisper-small', 'prompt': None, 'language': 'ko',
                          'expected': line['text'], 'transcript': actual, 'characterErrorRate': cer, **asr_source},
                  'checks': checks, 'passed': all(checks.values())}
        results.append(result)
        print(json.dumps({'key': line['key'], 'passed': result['passed'], 'cer': cer}, ensure_ascii=False), flush=True)
    aggregate = total_edits / max(1, total_characters)
    passed = all(item['passed'] for item in results) and aggregate <= .20 and production['totalBytes'] <= 4_000_000
    report = {'status': 'passed' if passed else 'failed', 'checkedUtc': datetime.now(timezone.utc).isoformat(),
              'manifest': {'file': str(manifest_path.relative_to(ROOT)), 'sha256': digest(manifest_path)},
              'production': {'file': str((HERE / 'production.json').relative_to(ROOT)), 'sha256': digest(HERE / 'production.json')},
              'harness': {'file': str(Path(__file__).relative_to(ROOT)), 'sha256': digest(Path(__file__))},
              'totalLines': len(results), 'weightedCharacterErrorRate': aggregate,
              'asrInferenceCounts': {'fresh': fresh_count, 'preservedIdenticalClip': len(inherited)},
              'inheritedAsr': inheritance,
              'totalBytes': production['totalBytes'], 'lines': results,
              'limits': ['ASR is an independent intelligibility check, not human listening, pronunciation or acting approval.',
                         'Runtime Native/browser must separately verify actual clip playback, navigation stop and mix ducking.']}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': report['status'], 'lines': len(results), 'weightedCER': aggregate}))
    raise SystemExit(0 if passed else 1)


if __name__ == '__main__':
    main()
