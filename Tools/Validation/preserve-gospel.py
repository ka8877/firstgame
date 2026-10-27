#!/usr/bin/env python3
"""Freeze and verify the unchanged Gospel inputs for the FC-only fidelity release.

No Unity or browser process is started. The previous public bytes and evidence
remain historical; this record explicitly verifies preservation, not a new test.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/Validation/FmFidelity'
RECORD = OUT / 'preserved-gospel.json'


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def fingerprint(paths):
    return [{'file': str(p.relative_to(ROOT)), 'bytes': p.stat().st_size,
             'sha256': digest(p)} for p in sorted(paths) if p.is_file()]


def initialize():
    require(not RECORD.exists(), 'Preservation record already exists; use --verify')
    stage = ROOT / 'Artifacts/WebPlaytest'
    manifest_path = stage / 'build-manifest.json'
    manifest = json.loads(manifest_path.read_text())
    previous = ROOT / 'Artifacts/Validation/Modern/summary.json'
    summary = json.loads(previous.read_text())
    require(summary['publication']['status'] == 'verified', 'Previous publication was not verified')
    require(manifest['gameRevisions']['PathOfDawn'] == 'gospel-modern-2026-10-26', 'Unexpected preserved game revision')
    names = sorted(name for name in manifest['files']
                   if name.startswith('games/path-of-dawn/') or name == 'art/path-of-dawn.png')
    require(len(names) >= 6, 'Complete previous Gospel public files are required')
    files = []
    manifest_copy = OUT / 'PreservedPublic/build-manifest.json'
    manifest_copy.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(manifest_path, manifest_copy)
    for name in names:
        source = stage / name
        require(digest(source) == manifest['files'][name], 'Previous staged Gospel bytes differ: ' + name)
        target = OUT / 'PreservedPublic' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        files.append({'file': name, 'bytes': source.stat().st_size, 'sha256': digest(source)})
    tracked = subprocess.check_output(['git', 'ls-files', '-z', 'Games/PathOfDawn'], cwd=ROOT).decode().split('\0')
    source_files = fingerprint(ROOT / p for p in tracked if p)
    players = fingerprint(p for p in (ROOT / 'Artifacts/Builds/PathOfDawn').rglob('*') if p.is_file())
    archive_files = fingerprint([ROOT / 'Artifacts/PathOfDawn-Windows.zip', ROOT / 'Artifacts/PathOfDawn-Web.zip'])
    record = {
        'status': 'frozen', 'createdUtc': datetime.now(timezone.utc).isoformat(),
        'game': 'PathOfDawn', 'revision': 'gospel-modern-2026-10-26',
        'sourceCommitAtFreeze': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'previousVerifiedPublication': summary['publication'],
        'previousSummary': {'file': str(previous.relative_to(ROOT)), 'sha256': digest(previous)},
        'previousPublicManifest': {'file': str(manifest_copy.relative_to(ROOT)),
                                   'originalStagingPath': str(manifest_path.relative_to(ROOT)), 'sha256': digest(manifest_path)},
        'publicFiles': files, 'sourceFiles': source_files, 'playerFiles': players, 'archives': archive_files,
        'scope': 'Byte preservation of the previously verified Gospel game. No new Gospel build, browser run or performance claim.'
    }
    OUT.mkdir(parents=True, exist_ok=True)
    RECORD.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
    return record


def verify():
    record = json.loads(RECORD.read_text())
    for row in record['publicFiles']:
        path = OUT / 'PreservedPublic' / row['file']
        require(path.is_file() and path.stat().st_size == row['bytes'] and digest(path) == row['sha256'],
                'Preserved Gospel public bytes changed: ' + row['file'])
    for category in ('sourceFiles', 'playerFiles', 'archives'):
        for row in record[category]:
            path = ROOT / row['file']
            require(path.is_file() and path.stat().st_size == row['bytes'] and digest(path) == row['sha256'],
                    'Unchanged Gospel ' + category + ' changed: ' + row['file'])
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    record = verify() if args.verify else initialize()
    print(json.dumps({'status': 'preserved', 'publicFiles': len(record['publicFiles']),
                      'sourceFiles': len(record['sourceFiles']), 'playerFiles': len(record['playerFiles']),
                      'archives': len(record['archives'])}))


if __name__ == '__main__':
    main()
