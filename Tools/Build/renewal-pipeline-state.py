#!/usr/bin/env python3
"""Retain authored URP bytes around a player build's generated strip caches.

The Unity process must have exited before restore. Source/runtime inputs are
never filtered or rebound: both prior and generated files are retained as proof.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
GOSPEL_FILES = (
    'Games/PathOfDawn/Assets/Settings/LuminousDesktopURP.asset',
    'Games/PathOfDawn/Assets/Settings/LuminousWebURP.asset',
    'Games/PathOfDawn/Assets/UniversalRenderPipelineGlobalSettings.asset',
)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('snapshot', 'restore'))
    parser.add_argument('game', choices=('FantasyCards', 'PathOfDawn'))
    parser.add_argument('attempt')
    parser.add_argument('profile', choices=('windows', 'web'))
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.attempt):
        parser.error('A task-owned unique attempt is required.')
    FILES = GOSPEL_FILES if args.game == 'PathOfDawn' else tuple('Games/FantasyCards/' + part for part in ('Assets/Settings/DesktopURP.asset', 'Assets/Settings/WebURP.asset', 'Assets/UniversalRenderPipelineGlobalSettings.asset'))
    folder = ROOT / 'Artifacts/Validation/Chronicle' / args.game / args.attempt / 'PipelineState' / args.profile
    record = folder / 'record.json'
    if args.action == 'snapshot':
        if folder.exists():
            raise SystemExit('Existing pipeline evidence is preserved; choose a new attempt.')
        (folder / 'Before').mkdir(parents=True)
        rows = []
        for name in FILES:
            source = ROOT / name
            target = folder / 'Before' / source.name
            if source.is_symlink() or not source.is_file():
                raise SystemExit('Expected regular authored pipeline asset: ' + name)
            target.write_bytes(source.read_bytes())
            rows.append({'file': name, 'sha256': sha(target), 'bytes': target.stat().st_size})
        record.write_text(json.dumps({'status': 'snapshotted', 'files': rows,
            'scope': 'Exact authored URP assets; Unity build-generated prefilter/active-resource mutations are restored only after Editor exit.'}, indent=2) + '\n')
    else:
        value = json.loads(record.read_text())
        if value['status'] != 'snapshotted' or [row['file'] for row in value['files']] != list(FILES):
            raise SystemExit('Unexpected or already restored pipeline record.')
        (folder / 'After').mkdir()
        for row in value['files']:
            source = ROOT / row['file']
            before = folder / 'Before' / source.name
            if sha(before) != row['sha256'] or before.stat().st_size != row['bytes']:
                raise SystemExit('Authored snapshot changed: ' + row['file'])
            after = folder / 'After' / source.name
            after.write_bytes(source.read_bytes())
            row['generatedAfterSHA256'] = sha(after)
            row['generatedChanged'] = row['generatedAfterSHA256'] != row['sha256']
        # Validation of every snapshot completes before any file is restored.
        for row in value['files']:
            source = ROOT / row['file']
            source.write_bytes((folder / 'Before' / source.name).read_bytes())
            if sha(source) != row['sha256']:
                raise SystemExit('Pipeline restoration failed: ' + row['file'])
        value['status'] = 'restored'
        record.write_text(json.dumps(value, indent=2) + '\n')
    print('Renewal authored pipeline state: ' + args.action + ' ' + args.attempt)


if __name__ == '__main__':
    main()
