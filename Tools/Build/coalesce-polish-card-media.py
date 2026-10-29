#!/usr/bin/env python3
"""Share immutable generated card JPEG bytes between current Windows/Web players."""
from datetime import datetime, timezone
import hashlib, json, os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BUILD = Path('/tmp/firstgame-polish-builds/FantasyCards')

def sha(p):
    h = hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def main():
    win = BUILD / 'Windows/FantasyCards_Data/StreamingAssets/Cards'
    web = BUILD / 'Web/StreamingAssets/Cards'
    assert win.is_dir() and web.is_dir() and not win.is_symlink() and not web.is_symlink()
    left, right = {p.name: p for p in win.glob('*.jpg')}, {p.name: p for p in web.glob('*.jpg')}
    assert set(left) == set(right) and len(left) == 15350
    rows = []
    for name in sorted(left):
        a, b = left[name], right[name]
        assert not a.is_symlink() and not b.is_symlink() and a.stat().st_size == b.stat().st_size
        digest = sha(a); assert digest == sha(b), name
        rows.append({'source': str(a), 'shared': str(b), 'bytes': a.stat().st_size, 'sha256': digest,
                     'alreadyShared': os.path.samefile(a, b)})
    folder = ROOT / 'Artifacts/Validation/Polish/Storage';folder.mkdir(parents=True,exist_ok=True)
    p = folder / ('current-media-sharing-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '.json')
    r = {'state': 'planned', 'scope': 'Only verified identical current generated JPEGs. No source assets, ZIPs or original evidence changed.', 'files': rows}
    p.write_text(json.dumps(r,indent=2)+'\n')
    saved = 0
    for row in rows:
        if row['alreadyShared']: continue
        source, target = Path(row['source']), Path(row['shared'])
        temporary = target.with_suffix('.jpg.polish-link')
        assert not temporary.exists()
        os.link(source,temporary)
        os.replace(temporary,target)
        assert os.path.samefile(source,target) and sha(target) == row['sha256']
        saved += row['bytes']
    r.update(state='shared', finishedUtc=datetime.now(timezone.utc).isoformat(), avoidedDuplicateBytes=saved)
    p.write_text(json.dumps(r,indent=2)+'\n')
    print(json.dumps({'state':'shared','files':len(rows),'avoidedDuplicateBytes':saved,'ledger':str(p.relative_to(ROOT))}))

if __name__ == '__main__':main()
