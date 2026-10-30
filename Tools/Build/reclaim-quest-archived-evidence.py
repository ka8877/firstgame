#!/usr/bin/env python3
"""Ledger exact archived unapproved screenshot expansions before bounded reclaim."""
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json
import os
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Artifacts/Validation/Quest/Storage'
IMAGES = {'.png', '.jpg', '.jpeg'}
FOLDERS = [ROOT / 'Artifacts' / name for name in ('Validation', 'ValidationHistory', 'Screenshots')]


def sha_stream(stream):
    h = hashlib.sha256()
    for b in iter(lambda: stream.read(1024 * 1024), b''):
        h.update(b)
    return h.hexdigest()


def sha(path):
    with path.open('rb') as f:
        return sha_stream(f)


def free():
    v = os.statvfs(ROOT)
    return v.f_bavail * v.f_frsize


def approved_paths():
    protected = set()
    def collect(value):
        if isinstance(value, dict):
            for key in ('file', 'path'):
                name = value.get(key)
                if isinstance(name, str) and Path(name).suffix.lower() in IMAGES:
                    p = Path(name)
                    protected.add((p if p.is_absolute() else ROOT / p).resolve())
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)
    def scan(value):
        if isinstance(value, dict):
            if value.get('status') == 'approved' and value.get('reviewer') in ('root', '/root'):
                collect(value.get('screenshots', []))
            for child in value.values():
                scan(child)
        elif isinstance(value, list):
            for child in value:
                scan(child)
    roots = set(FOLDERS)
    roots.update(p.resolve() for folder in FOLDERS if folder.is_dir() for p in folder.iterdir() if p.is_dir())
    seen = set()
    for folder in roots:
        for p in folder.rglob('*.json'):
            actual = p.resolve()
            if actual in seen:
                continue
            seen.add(actual)
            try:
                value = json.loads(p.read_text())
            except (OSError, ValueError):
                continue
            scan(value)
            if 'root' in p.name.lower() and isinstance(value, dict) and value.get('status') == 'approved':
                collect(value.get('screenshots', []))
    return protected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reclaim-plan', type=Path)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    if args.reclaim_plan:
        ledger = args.reclaim_plan.resolve()
        assert ledger.parent == OUT.resolve()
        record = json.loads(ledger.read_text())
        assert record['state'] == 'prepared'
        protected = approved_paths()
        for row in record['files']:
            p = ROOT / row['file']
            assert p.is_file() and not p.is_symlink() and p.resolve() not in protected
            assert p.suffix.lower() in IMAGES and any(p.resolve().is_relative_to(f) for f in FOLDERS)
            assert p.stat().st_size == row['bytes'] and sha(p) == row['sha256']
        archives = {ROOT / row['archive']: row['archiveSha256'] for row in record['files']}
        assert all(sha(p) == digest for p, digest in archives.items())
        before = free()
        for row in record['files']:
            (ROOT / row['file']).unlink()
        assert all(p.is_file() and sha(p) == digest for p, digest in archives.items())
        record.update(state='reclaimed', workspaceFreeBeforeReclaim=before, workspaceFreeAfter=free(), measuredWorkspaceFreeIncrease=free()-before, archivesUnchanged=True)
        ledger.write_text(json.dumps(record, indent=2)+'\n')
        print(json.dumps({k:record[k] for k in ('state','measuredWorkspaceFreeIncrease','workspaceFreeAfter','archivesUnchanged')}))
        return
    protected = approved_paths()
    archive_paths = [p for p in (ROOT/'Artifacts').rglob('*.zip') if p.is_file() and not p.is_symlink() and 'Sources' not in p.name]
    index = defaultdict(list)
    handles = {}
    for archive in sorted(archive_paths):
        try:
            zipped = zipfile.ZipFile(archive)
        except zipfile.BadZipFile:
            continue
        handles[archive] = zipped
        for info in zipped.infolist():
            if Path(info.filename).suffix.lower() in IMAGES:
                index[(Path(info.filename).name, info.file_size)].append((archive, info.filename))
    rows, retained, hashes, archives = [], [], {}, {}
    for folder in FOLDERS:
        for p in sorted(folder.rglob('*')):
            if not p.is_file() or p.suffix.lower() not in IMAGES:
                continue
            if p.is_symlink() or not p.resolve().is_relative_to(folder) or p.resolve() in protected or 'Quest' in p.parts:
                continue
            actual = sha(p)
            for archive, member in index.get((p.name,p.stat().st_size),[]):
                key=(archive,member)
                if key not in hashes:
                    with handles[archive].open(member) as stream:
                        hashes[key]=sha_stream(stream)
                if hashes[key]!=actual:
                    continue
                if archive not in archives:
                    archives[archive]=sha(archive)
                rows.append({'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':actual,'archive':str(archive.relative_to(ROOT)),'archiveSha256':archives[archive],'member':member})
                break
            else:
                retained.append(str(p.relative_to(ROOT)))
    for h in handles.values():
        h.close()
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    ledger=OUT/('workspace-evidence-'+stamp+'.json')
    record={'state':'prepared','scope':'Only regular expanded historical screenshot copies with exact retained evidence ZIP members; every detected Root-approved original and all metadata/source/user/imagegen assets stay direct.','createdUtc':datetime.now(timezone.utc).isoformat(),'protectedOriginals':len(protected),'files':rows,'retained':retained,'logicalBytes':sum(x['bytes'] for x in rows),'workspaceFreeBefore':free()}
    ledger.write_text(json.dumps(record,indent=2)+'\n')
    print(json.dumps({k:record[k] for k in ('state','protectedOriginals','logicalBytes')},ensure_ascii=False))
    print('files',len(rows),'retained',len(retained),'ledger',str(ledger.relative_to(ROOT)))


if __name__=='__main__':
    main()
