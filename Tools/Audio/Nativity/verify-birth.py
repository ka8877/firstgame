#!/usr/bin/env python3
"""Verify the newly rendered quiet birth track's actual decoded signal and loop boundary."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import subprocess

import numpy as np

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Artifacts/Validation/Nativity/Audio/birth.json')
    args = parser.parse_args()
    production = json.loads((HERE / 'production.json').read_text())
    clip = ROOT / production['clip']['file']
    assert digest(clip) == production['clip']['sha256']
    assert digest(ROOT / production['generator']['file']) == production['generator']['sha256']
    assert digest(ROOT / production['baseRenderer']['file']) == production['baseRenderer']['sha256']
    assert digest(ROOT / production['midi']['file']) == production['midi']['sha256']
    score = json.loads((ROOT / production['score']['file']).read_text())
    assert digest(ROOT / production['score']['file']) == production['score']['sha256']
    probe = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(clip)]))['streams'][0]
    decoded = np.frombuffer(subprocess.check_output(['ffmpeg', '-nostdin', '-hide_banner', '-loglevel', 'error',
                                                   '-i', str(clip), '-f', 'f32le', '-ac', '2', '-ar', '44100', '-']), dtype='<f4').reshape(-1, 2)
    rate = 44100
    rms = float(np.sqrt(np.mean(decoded.astype(np.float64) ** 2)))
    peak = float(np.max(np.abs(decoded)))
    rms_db = float(20 * np.log10(max(rms, 1e-12)))
    peak_db = float(20 * np.log10(max(peak, 1e-12)))
    seam_jump = float(np.max(np.abs(decoded[-1] - decoded[0])))
    seam = np.concatenate((decoded[-rate:], decoded[:rate]))
    window = round(.020 * rate)
    windows = seam[:len(seam) // window * window].reshape(-1, window, 2)
    seam_levels = np.sqrt(np.mean(windows.astype(np.float64) ** 2, axis=(1, 2)))
    checks = {'sourceAndRecordingHashesMatch': True, 'stereo44100': probe['channels'] == 2 and int(probe['sample_rate']) == rate,
              'expectedCompleteMusicalCycle': abs(len(decoded) / rate - 24 * 4 * 60 / 68) < .06,
              'noPercussionOrBellPrograms': set(score['programs'].values()) == {73, 48, 46, 42} and '9' not in score['programs'],
              'finiteSignal': bool(np.isfinite(decoded).all()), 'noClipping': peak < .999,
              'quietAudibleMusicRms': -31 < rms_db < -19, 'safePeakHeadroom': -30 < peak_db < -2,
              'loopEndpointJumpBelowNoiseFloor': seam_jump < .004,
              'noInsertedTwentyMillisecondSilenceAtLoop': bool(np.min(seam_levels) > .0005),
              'newClipFitsBudget': clip.stat().st_size < 1_300_000}
    report = {'status': 'passed' if all(checks.values()) else 'failed',
              'checkedUtc': datetime.now(timezone.utc).isoformat(),
              'clip': {'file': str(clip.relative_to(ROOT)), 'sha256': digest(clip), 'bytes': clip.stat().st_size},
              'production': {'file': str((HERE / 'production.json').relative_to(ROOT)), 'sha256': digest(HERE / 'production.json')},
              'harness': {'file': str(Path(__file__).relative_to(ROOT)), 'sha256': digest(Path(__file__))},
              'seconds': len(decoded) / rate, 'rmsDbfs': rms_db, 'peakDbfs': peak_db,
              'loopEndpointJump': seam_jump, 'minimumLoopBoundary20msRms': float(np.min(seam_levels)),
              'checks': checks,
              'limits': ['Decoded continuity and source instrumentation do not constitute human listening or historical music reconstruction.',
                         'Actual music selection, narrative ducking and browser output require separate Native/browser evidence.']}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({key: report[key] for key in ['status', 'seconds', 'rmsDbfs', 'peakDbfs', 'loopEndpointJump']}))
    raise SystemExit(0 if report['status'] == 'passed' else 1)


if __name__ == '__main__':
    main()
