#!/usr/bin/env python3
"""Temporarily park an inactive imported cache, preserving every byte and symlink route."""
from datetime import datetime,timezone
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('game',choices=('FantasyCards','PathOfDawn'));p.add_argument('action',choices=('park','restore'));a=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
work=ROOT/'Artifacts/QuestWork/cache'/a.game/'Artifacts';park=Path('/tmp/firstgame-quest-parked-import-cache')/a.game/'Artifacts'
source,destination=(work,park) if a.action=='park' else (park,work)
assert source.is_dir() and not source.is_symlink()
if a.action=='park':assert not destination.exists() or (destination.is_symlink() and destination.readlink()==source)
else:assert destination.is_symlink() and destination.readlink()==park

def sha(q):
 h=hashlib.sha256()
 with q.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def rows(d):
 result=[]
 for q in sorted(d.rglob('*')):
  assert not q.is_symlink(),q
  if q.is_file():result.append({'path':q.relative_to(d).as_posix(),'bytes':q.stat().st_size,'sha256':sha(q)})
 return result
files=rows(source);size=sum(r['bytes'] for r in files)
probe=destination.parent
while not probe.exists():probe=probe.parent
stat=os.statvfs(probe);assert stat.f_bavail*stat.f_frsize>size+200*1024*1024
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');ledger=ROOT/'Artifacts/Validation/Quest/Storage'/('cache-'+a.action+'-'+a.game+'-'+stamp+'.json')
d={'status':'prepared','source':str(source),'destination':str(destination),'scope':'Only inactive explicitly named generated imported cache is copied byte-for-byte then its old path becomes recovery symlink; all source, PackageCache, AssetDB, archives and original art untouched. This is reversible capacity balancing, not cache deletion.','files':files,'bytes':size};ledger.write_text(json.dumps(d,indent=2)+'\n')
copy=destination.with_name('Artifacts.quest-copy');assert not copy.exists();copy.parent.mkdir(parents=True,exist_ok=True);shutil.copytree(source,copy);assert rows(copy)==files
if destination.is_symlink():destination.unlink()
copy.rename(destination)
holding=source.with_name('Artifacts.quest-migrated-original');assert not holding.exists();source.rename(holding);source.symlink_to(destination,target_is_directory=True);assert rows(destination)==files
for q in sorted(holding.rglob('*'),key=lambda q:len(q.parts),reverse=True):
 if q.is_file():q.unlink()
 else:q.rmdir()
holding.rmdir();d.update(status=a.action+'-complete',oldPathSymlink=str(source.readlink()),verifiedUtc=datetime.now(timezone.utc).isoformat());ledger.write_text(json.dumps(d,indent=2)+'\n');print(json.dumps({'status':d['status'],'bytes':size,'files':len(files),'ledger':str(ledger.relative_to(ROOT))}))
