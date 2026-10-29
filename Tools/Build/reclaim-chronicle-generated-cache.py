#!/usr/bin/env python3
"""Reclaim only six idle generated cache trees, preserving roots and archives."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/Validation/Chronicle/Storage'


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def archives():
    return [{'file': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)}
            for folder in ('/tmp/firstgame-renewal-deliverables', '/tmp/firstgame-polish-deliverables')
            for path in sorted(Path(folder).rglob('*.zip'))]


def free():
    stat = os.statvfs('/tmp')
    return stat.f_bavail * stat.f_frsize


def main():
    processes = [line.split(None, 1) for line in
                 subprocess.check_output(['ps', '-eo', 'stat=,comm='], text=True).splitlines()]
    assert not any(not state.startswith('Z') and name in ('Unity', 'UnityShaderComp', 'bee_backend')
                   for state, name in processes), 'Unity or a build worker is active'
    folders = []
    for game in ('FantasyCards', 'PathOfDawn'):
        folders.append(Path('/tmp/firstgame-luminous-generated-cache') / game / 'Library/Bee')
        folders.extend(Path('/tmp/firstgame-arcana-generated-cache') / game / name
                       for name in ('BurstCache', 'ShaderCache'))
    rows = []
    for folder in folders:
        assert folder.is_dir() and not folder.is_symlink(), folder
        for path in sorted(folder.rglob('*')):
            assert not path.is_symlink(), path
            if path.is_file():
                rows.append({'file': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)})
    assert rows
    OUT.mkdir(parents=True, exist_ok=True)
    ledger = OUT / 'inactive-generated-cache-reclamation.json'
    assert not ledger.exists()
    record = {'state': 'prepared', 'createdUtc': datetime.now(timezone.utc).isoformat(),
              'scope': 'Only idle regular generated files in six explicit Bee/BurstCache/ShaderCache folders. All directory/symlink roots, PackageCache, imported Artifacts, source/assets, old evidence and archives preserved.',
              'roots': list(map(str, folders)), 'files': rows,
              'archivesBefore': archives(), 'tmpFreeBytesBefore': free()}
    assert len(record['archivesBefore']) >= 12
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    for row in rows:
        path = Path(row['file'])
        assert path.is_file() and not path.is_symlink() and digest(path) == row['sha256'], path
        path.unlink()
    assert archives() == record['archivesBefore'], 'A protected archive changed'
    assert all(folder.is_dir() and not folder.is_symlink() for folder in folders)
    record.update(state='reclaimed', checkedUtc=datetime.now(timezone.utc).isoformat(),
                  reclaimedFiles=len(rows), reclaimedLogicalBytes=sum(row['bytes'] for row in rows),
                  tmpFreeBytesAfter=free(), allProtectedArchivesUnchanged=True)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({key: record[key] for key in ('state', 'reclaimedFiles', 'reclaimedLogicalBytes', 'tmpFreeBytesAfter', 'allProtectedArchivesUnchanged')}))


if __name__ == '__main__':
    main()
