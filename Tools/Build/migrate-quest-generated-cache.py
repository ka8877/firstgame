#!/usr/bin/env python3
"""Move explicit inactive generated caches byte-for-byte onto the task workspace."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Artifacts/QuestWork/cache'
OUT = ROOT / 'Artifacts/Validation/Quest/Storage'


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def free(path):
    v = os.statvfs(path)
    return v.f_bavail * v.f_frsize


def main():
    subprocess.run(['python3', str(ROOT/'Tools/Environment/assert-runtime-idle.py')], check=True)
    OUT.mkdir(parents=True,exist_ok=True)
    moves=[]
    for game in ('FantasyCards','PathOfDawn'):
        moves.append((Path('/tmp/firstgame-arcana-generated-cache')/game/'Artifacts', DEST/game/'Artifacts'))
        for kind in ('Bee','BurstCache','ShaderCache','PlayerDataCache'):
            source=ROOT/'Games'/game/'Library'/kind
            if source.exists():
                moves.append((source.resolve(),DEST/game/kind))
    for source,destination in moves:
        if destination.is_dir() and source.resolve()==destination.resolve():
            continue
        if source.is_symlink():
            assert source.resolve()==destination.resolve()
            continue
        assert source.is_dir() and not destination.exists()
        before_tmp,before_workspace=free('/tmp'),free(ROOT)
        rows=[]
        for p in sorted(source.rglob('*')):
            assert not p.is_symlink(),p
            if p.is_file():
                rows.append({'path':p.relative_to(source).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)})
        stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        ledger=OUT/('cache-migration-'+source.parent.name+'-'+source.name+'-'+stamp+'.json')
        record={'status':'prepared','source':str(source),'destination':str(destination),'createdUtc':datetime.now(timezone.utc).isoformat(),
                'scope':'Explicit inactive generated cache migration only. Source Assets, metadata, PackageCache, AssetDB, originals and all ZIPs untouched. Every copied byte is compared before the old path becomes an explicit symlink.',
                'files':rows,'tmpFreeBefore':before_tmp,'workspaceFreeBefore':before_workspace}
        ledger.write_text(json.dumps(record,indent=2)+'\n')
        destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copytree(source,destination)
        assert len([p for p in destination.rglob('*') if p.is_file()])==len(rows)
        for row in rows:
            a,b=source/row['path'],destination/row['path']
            assert a.stat().st_size==b.stat().st_size==row['bytes'] and sha(a)==sha(b)==row['sha256']
        subprocess.run(['python3',str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
        holding=source.with_name(source.name+'.quest-migrated-original')
        assert not holding.exists()
        source.rename(holding)
        source.symlink_to(destination,target_is_directory=True)
        for row in rows:
            a=holding/row['path']
            assert a.is_file() and sha(a)==row['sha256'] and sha(source/row['path'])==row['sha256']
            a.unlink()
        for p in sorted(holding.rglob('*'),key=lambda p:len(p.parts),reverse=True):
            if p.is_dir():p.rmdir()
        holding.rmdir()
        record.update(status='migrated',finishedUtc=datetime.now(timezone.utc).isoformat(),filesVerified=len(rows),bytesVerified=sum(x['bytes'] for x in rows),tmpFreeAfter=free('/tmp'),workspaceFreeAfter=free(ROOT),oldPathSymlink=str(source.readlink()))
        ledger.write_text(json.dumps(record,indent=2)+'\n')
        print(json.dumps({k:record[k] for k in ('status','source','filesVerified','bytesVerified','tmpFreeAfter','workspaceFreeAfter')}),flush=True)


if __name__=='__main__':main()
