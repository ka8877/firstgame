#!/usr/bin/env python3
"""Reclaim one historical expanded Windows player, retaining its exact ZIP."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT / 'Artifacts/PreviousBuilds/2026-10-26-before-classic'
ARCHIVE = OLD / 'FantasyCards-Windows.zip'
PLAYER = OLD / 'FantasyCards/Windows'


def sha(stream):
    result = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b''):
        result.update(block)
    return result.hexdigest()


def file_sha(path):
    with path.open('rb') as stream:
        return sha(stream)


def free():
    value = os.statvfs(ROOT)
    return value.f_bavail * value.f_frsize


def main():
    assert PLAYER.is_dir() and not PLAYER.is_symlink()
    archive_sha = file_sha(ARCHIVE)
    rows, retained = [], []
    with zipfile.ZipFile(ARCHIVE) as archive:
        members = set(archive.namelist())
        for path in sorted(PLAYER.rglob('*')):
            assert not path.is_symlink(), path
            if not path.is_file():
                continue
            member = 'FantasyCards-Windows/' + path.relative_to(PLAYER).as_posix()
            if member not in members or archive.getinfo(member).file_size != path.stat().st_size:
                retained.append(str(path.relative_to(ROOT)))
                continue
            actual = file_sha(path)
            with archive.open(member) as stream:
                expected = sha(stream)
            if actual != expected:
                retained.append(str(path.relative_to(ROOT)))
                continue
            rows.append({'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size,
                         'sha256': actual, 'archive': str(ARCHIVE.relative_to(ROOT)),
                         'archiveSha256': archive_sha, 'member': member})
    assert rows
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    ledger = ROOT / 'Artifacts/Validation/Chronicle/Storage' / ('historical-player-' + stamp + '.json')
    record = {'state': 'prepared', 'scope': 'Only exact retained ZIP-member bytes in the named historical expanded player. All archives, unique metadata and originals remain.',
              'files': rows, 'retained': retained, 'workspaceFreeBytesBefore': free()}
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    for row in rows:
        path = ROOT / row['file']
        assert path.is_file() and not path.is_symlink() and file_sha(path) == row['sha256']
        path.unlink()
    assert file_sha(ARCHIVE) == archive_sha
    record.update(state='reclaimed', workspaceFreeBytesAfter=free(), reclaimedFiles=len(rows),
                  matchedLogicalBytes=sum(row['bytes'] for row in rows), archiveUnchanged=True)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({key: record[key] for key in ('state', 'reclaimedFiles', 'matchedLogicalBytes', 'workspaceFreeBytesAfter', 'archiveUnchanged')}))


if __name__ == '__main__':
    main()
