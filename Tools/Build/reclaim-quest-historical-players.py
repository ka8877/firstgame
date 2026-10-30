#!/usr/bin/env python3
"""Reclaim only exact ZIP-backed expanded historical player copies.

Never removes a ZIP, metadata, source asset or any file outside PreviousBuilds.
The prepared ledger records the original path and retained archive/member/SHA.
"""
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json
import os
import zipfile

ROOT = Path(__file__).resolve().parents[2]
HISTORY = ROOT / 'Artifacts/PreviousBuilds'
OUT = ROOT / 'Artifacts/Validation/Quest/Storage'
KEEP = {'.zip', '.json', '.txt', '.md', '.xml', '.log', '.meta', '.sha256'}


def digest_stream(stream):
    result = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        result.update(block)
    return result.hexdigest()


def digest(path):
    with path.open('rb') as stream:
        return digest_stream(stream)


def free():
    s = os.statvfs(ROOT)
    return s.f_bavail * s.f_frsize


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reclaim', action='store_true')
    args = parser.parse_args()
    archives = sorted(set((ROOT / 'Artifacts').rglob('*-Windows.zip')) |
                      set((ROOT / 'Artifacts').rglob('*-Web.zip')))
    archives = [p for p in archives if p.is_file() and not p.is_symlink()]
    index = defaultdict(list)
    handles = {}
    for path in archives:
        try:
            zipped = zipfile.ZipFile(path)
        except zipfile.BadZipFile:
            continue
        handles[path] = zipped
        for info in zipped.infolist():
            if not info.is_dir():
                index[(Path(info.filename).name, info.file_size)].append((path, info.filename))
    rows, retained, members, archive_shas = [], [], {}, {}
    before = free()
    for path in sorted(HISTORY.rglob('*')):
        if not path.is_file() or path.suffix.lower() in KEEP:
            continue
        if path.is_symlink() or not path.resolve().is_relative_to(HISTORY):
            retained.append({'file': str(path.relative_to(ROOT)), 'reason': 'Link/outside historical root'})
            continue
        candidates = index.get((path.name, path.stat().st_size), [])
        if not candidates:
            retained.append({'file': str(path.relative_to(ROOT)), 'reason': 'No same-name/size ZIP member'})
            continue
        actual = digest(path)
        for archive, member in candidates:
            key = (archive, member)
            if key not in members:
                with handles[archive].open(member) as stream:
                    members[key] = digest_stream(stream)
            if actual != members[key]:
                continue
            if archive not in archive_shas:
                archive_shas[archive] = digest(archive)
            rows.append({'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size,
                         'sha256': actual, 'linksBefore': path.stat().st_nlink,
                         'archive': str(archive.relative_to(ROOT)),
                         'archiveSha256': archive_shas[archive], 'member': member})
            break
        else:
            retained.append({'file': str(path.relative_to(ROOT)), 'reason': 'Unique bytes retained'})
        if len(rows) % 3000 == 0:
            print('Matched', len(rows), 'historical player files', flush=True)
    for zipped in handles.values():
        zipped.close()
    OUT.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    ledger = OUT / ('workspace-expanded-' + stamp + '.json')
    record = {'state': 'prepared', 'createdUtc': datetime.now(timezone.utc).isoformat(),
              'scope': 'Only exact retained ZIP-member-backed generated historical player copies below Artifacts/PreviousBuilds. ZIPs, metadata, source/originals and unique bytes retained.',
              'workspaceFreeBefore': before, 'files': rows, 'retained': retained,
              'logicalBytes': sum(x['bytes'] for x in rows)}
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({'state': 'prepared', 'files': len(rows), 'retained': len(retained),
                      'logicalBytes': record['logicalBytes'], 'ledger': str(ledger.relative_to(ROOT))}), flush=True)
    if not args.reclaim:
        return
    for row in rows:
        path = ROOT / row['file']
        assert path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(HISTORY)
        assert path.stat().st_size == row['bytes'] and digest(path) == row['sha256']
        path.unlink()
    assert all(digest(path) == value for path, value in archive_shas.items())
    record.update(state='reclaimed', finishedUtc=datetime.now(timezone.utc).isoformat(),
                  workspaceFreeAfter=free(), measuredWorkspaceFreeIncrease=free()-before,
                  archivesUnchanged=True)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({k: record[k] for k in ['state', 'logicalBytes', 'workspaceFreeAfter', 'measuredWorkspaceFreeIncrease', 'archivesUnchanged']}))


if __name__ == '__main__':
    main()
