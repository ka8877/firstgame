#!/usr/bin/env python3
"""Source-only Native recovery evidence; this runner can never authorize publication.

The original C1 release wrapper and its missing historical prerequisites remain
unchanged. A separate current public-baseline recovery is required for release.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import re
import signal
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

def require(value, message):
    if not value:
        raise ValueError(message)

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def reference(path):
    path = Path(path)
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path), 'bytes': path.stat().st_size}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--contract', type=Path, required=True)
    parser.add_argument('--lease-record', type=Path, required=True)
    args = parser.parse_args()
    contract = json.loads(args.contract.read_text())
    require(contract['scope'] == 'product-native-only' and contract['publishReady'] is False,
            'A product-only contract that explicitly refuses publication is required')
    require(contract['game'] == 'FantasyCards' and contract['revision'] == 'fm-capability-2026-11-03',
            'Only the current Card product recovery is supported')
    attempt = contract['attempt']
    require(re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', attempt), 'Safe unique attempt required')
    exact_path = ROOT / 'Tools/Autonomy/c1-native-contract.json'
    require(digest(exact_path) == contract['nativeContractSha256'], 'The unchanged exact-eight contract differs')
    exact = json.loads(exact_path.read_text())['expectedFullNames']
    require(len(exact) == len(set(exact)) == 8 and exact == contract['expectedFullNames'],
            'All retained six, corrected Fidelity and new capability cases are mandatory')
    for name, expected in contract['sourceFiles'].items():
        path = ROOT / name
        require(path.is_file() and not path.is_symlink() and digest(path) == expected,
                'Frozen recovery source differs: ' + name)
    guard = [sys.executable, str(ROOT / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(args.lease_record)]
    subprocess.run(guard, cwd=ROOT, check=True)
    subprocess.run([sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], cwd=ROOT, check=True)
    lease = json.loads(args.lease_record.read_text())['lease']
    seconds = int((datetime.fromisoformat(lease['deadlineUtc']) - datetime.now(timezone.utc)).total_seconds()) - 60
    require(seconds >= 120, 'Insufficient owned lease time; checkpoint and use another bounded batch')
    seconds = min(seconds, 900)
    run = ROOT / contract['runRoot']
    require(run.is_relative_to(ROOT / 'Artifacts/Validation/Autonomy/C1-2026-11-03/NativeRuns'),
            'Native recovery stays in the dedicated C1 evidence tree')
    require(not run.exists() and not run.is_symlink(), 'Existing evidence is immutable; choose a new attempt')
    run.mkdir(parents=True)
    captures = run / 'Screenshots/FantasyCards' / attempt
    recorded = run / 'RecordedHarness/Tools/Autonomy/recovery-product-native.py'
    recorded.parent.mkdir(parents=True)
    recorded.write_bytes(Path(__file__).read_bytes())
    (run / 'product-contract.executed.json').write_bytes(args.contract.read_bytes())
    (run / 'native-contract.executed.json').write_bytes(exact_path.read_bytes())
    env = dict(os.environ, FIRSTGAME_VALIDATION_ROOT=str(run),
               FIRSTGAME_BUILD_ROOT=contract['buildRoot'], FIRSTGAME_NATIVE_FILTER=';'.join(exact),
               FIRSTGAME_C1_NATIVE_CAPTURE_ROOT=str(captures), FIRSTGAME_NATIVE_ATTEMPT=attempt)
    process = subprocess.Popen([sys.executable, str(ROOT / 'Tools/Validation/run-nativity-native.py'),
                                'FantasyCards', attempt], cwd=ROOT, env=env, start_new_session=True)
    exit_code = 1
    try:
        exit_code = process.wait(timeout=seconds)
    except subprocess.TimeoutExpired:
        exit_code = 124
    finally:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=15)
    summary = {'status': 'failed', 'scope': 'product-native-only', 'publishReady': False,
               'finishedUtc': datetime.now(timezone.utc).isoformat(), 'exitCode': exit_code,
               'historicalReleasePrerequisitesRestored': False,
               'contract': reference(run / 'product-contract.executed.json'),
               'harness': reference(recorded), 'expectedFullNames': exact}
    try:
        require(exit_code == 0, 'Actual Native execution did not succeed')
        native = run / 'Native/FantasyCards'
        binding = json.loads((native / 'binding.json').read_text())
        xml = native / 'native.xml'
        tree = ET.parse(xml).getroot()
        cases = tree.findall('.//test-case')
        names = [case.get('fullname') for case in cases]
        require(binding['status'] == 'passed' and binding['sourceChangedDuringRun'] is False
                and binding['xmlSha256'] == digest(xml)
                and binding['inputManifestSha256'] == digest(native / 'inputs-before.json')
                and binding['extraFixtureInputs'] == [], 'Current full authored-source/XML binding failed')
        require(tree.get('result') == 'Passed' and tree.get('passed') == '8'
                and tree.get('failed') == '0' and tree.get('skipped') == '0'
                and len(names) == len(set(names)) == 8 and set(names) == set(exact)
                and all(case.get('result') == 'Passed' for case in cases), 'Every exact-eight case must pass')
        for name, expected in contract['sourceFiles'].items():
            require(digest(ROOT / name) == expected, 'Frozen execution input changed: ' + name)
        require(captures.is_dir() and not any(path.is_symlink() for path in run.rglob('*')),
                'Every actual capture and evidence leaf must remain regular')
        originals = [reference(path) for path in sorted(captures.iterdir()) if path.is_file()]
        require(originals and all(row['file'].endswith(('.png', '.png.txt')) for row in originals),
                'Actual original PNG/sidecar evidence is required')
        summary.update(status='passed', sourceFingerprint=binding['sourceFingerprint'],
                       binding=reference(native / 'binding.json'), nativeXml=reference(xml), originals=originals)
    except Exception as error:
        summary['error'] = str(error)
    (run / 'product-native-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(summary, ensure_ascii=False))
    return 0 if summary['status'] == 'passed' else 1

if __name__ == '__main__':
    raise SystemExit(main())
