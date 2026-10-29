#!/usr/bin/env python3
"""Reclaim only old expanded player files proven identical to retained ZIP members."""
from datetime import datetime, timezone
import hashlib, json, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OLD = Path('/tmp/firstgame-polish-builds')
STAGE = Path('/tmp/firstgame-polish-staged-site')
ARCHIVES = Path('/tmp/firstgame-polish-deliverables')
OUT = ROOT / 'Artifacts/Validation/Chronicle/Storage'

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def main():
    receipt = json.loads((ROOT / 'Artifacts/Validation/Polish/deliverables.json').read_text())
    assert receipt['status'] == 'passed'
    known = {x['archive']: x for x in receipt['archives']}
    rows, retained = [], []
    before = __import__('os').statvfs('/tmp')
    before_free = before.f_bavail * before.f_frsize
    for game, slug in [('FantasyCards', 'fantasy-cards'), ('PathOfDawn', 'path-of-dawn')]:
        for target in ('Windows', 'Web'):
            archive = ARCHIVES / (game + '-' + target + '.zip')
            original = known[archive.name]
            assert archive.is_file() and archive.stat().st_size == original['bytes'] and sha(archive) == original['sha256']
            folders = [OLD / game / target]
            folders += sorted((OLD / 'Previous').glob(game + '-' + target + '-before-*'))
            if target == 'Web': folders.append(STAGE / 'games' / slug)
            with zipfile.ZipFile(archive) as z:
                members = set(z.namelist()); cache = {}
                for folder in folders:
                    assert folder.is_dir() and not folder.is_symlink(), folder
                    for path in sorted(folder.rglob('*')):
                        if not path.is_file(): continue
                        assert not path.is_symlink(), path
                        member = game + '-' + target + '/' + path.relative_to(folder).as_posix()
                        if member not in members or z.getinfo(member).file_size != path.stat().st_size:
                            retained.append({'file': str(path), 'reason': 'No identical-size retained ZIP member'})
                            continue
                        actual = sha(path)
                        if member not in cache:
                            h = hashlib.sha256()
                            with z.open(member) as f:
                                for block in iter(lambda: f.read(1024 * 1024), b''): h.update(block)
                            cache[member] = h.hexdigest()
                        if actual != cache[member]:
                            retained.append({'file': str(path), 'reason': 'Unique bytes retained'})
                            continue
                        rows.append({'file': str(path), 'bytes': path.stat().st_size, 'sha256': actual,
                                     'linksBefore': path.stat().st_nlink, 'archive': str(archive), 'archiveSha256': original['sha256'], 'member': member})
            print(game, target, 'exact members checked', len(rows), flush=True)
    OUT.mkdir(parents=True, exist_ok=True)
    ledger = OUT / ('archive-backed-reclamation-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '.json')
    record = {'state': 'planned', 'scope': 'Only exact previously verified expanded Polish/Previous player and game-stage files. All Polish and older ZIPs, originals, unique bytes and metadata remain. Logical bytes include shared hardlinks; measured free-space change is recorded separately.',
              'createdUtc': datetime.now(timezone.utc).isoformat(), 'files': rows, 'retained': retained}
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    for row in rows:
        path = Path(row['file'])
        assert path.is_file() and not path.is_symlink() and path.stat().st_size == row['bytes']
        path.unlink()
    after = __import__('os').statvfs('/tmp')
    record.update(state='reclaimed', finishedUtc=datetime.now(timezone.utc).isoformat(), matchedLogicalBytes=sum(x['bytes'] for x in rows), tmpFreeBytesBefore=before_free, tmpFreeBytesAfter=after.f_bavail * after.f_frsize, measuredTmpFreeIncrease=after.f_bavail * after.f_frsize-before_free)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({'state': record['state'], 'files': len(rows), 'retainedFiles': len(retained), 'matchedLogicalBytes': record['matchedLogicalBytes'], 'measuredTmpFreeIncrease': record['measuredTmpFreeIncrease'], 'ledger': str(ledger.relative_to(ROOT))}))

if __name__ == '__main__': main()
