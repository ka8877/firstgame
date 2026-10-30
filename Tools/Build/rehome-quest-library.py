#!/usr/bin/env python3
"""Byte-preserving rehome of an inactive generated Library; preserve linked caches."""
from datetime import datetime,timezone
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify
p=argparse.ArgumentParser();p.add_argument('game',choices=('FantasyCards','PathOfDawn'));a=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
source=Path('/tmp/firstgame-luminous-generated-cache')/a.game/'Library';destination=ROOT/'Artifacts/QuestWork/cache'/a.game/'Library'
assert source.is_dir() and not source.is_symlink() and not destination.exists()
assert (ROOT/'Games'/a.game/'Library').is_symlink() and (ROOT/'Games'/a.game/'Library').resolve()==source
before=capture(a.game)
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def inventory(folder):
 rows=[]
 for q in sorted(folder.rglob('*')):
  n=q.relative_to(folder).as_posix()
  if q.is_symlink():rows.append({'path':n,'kind':'symlink','target':str(q.readlink())})
  elif q.is_file():rows.append({'path':n,'kind':'file','bytes':q.stat().st_size,'sha256':sha(q)})
  else:assert q.is_dir(),q
 return rows
rows=inventory(source);size=sum(r.get('bytes',0) for r in rows)
v=os.statvfs(ROOT);assert v.f_bavail*v.f_frsize>size+200*1024*1024,'Insufficient destination headroom'
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');ledger=ROOT/'Artifacts/Validation/Quest/Storage'/('library-rehome-'+a.game+'-'+stamp+'.json')
record={'status':'prepared','source':str(source),'destination':str(destination),'scope':'Byte-preserving inactive generated Library relocation. PackageCache/Artifacts links retain exactly their original targets; SourceAssetDB and ArtifactDB contents are copied and fully verified, not purged. Authored Assets/meta/source untouched. Root path becomes explicit recovery symlink so Unity-created Bee remains on workspace.','files':rows,'bytes':size,'sourceFingerprint':before['fingerprint']}
ledger.write_text(json.dumps(record,indent=2)+'\n')
shutil.copytree(source,destination,symlinks=True)
assert inventory(destination)==rows
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
holding=source.with_name('Library.quest-rehomed-original');assert not holding.exists();source.rename(holding);source.symlink_to(destination,target_is_directory=True)
assert inventory(holding)==rows and verify(before)==before['fingerprint']
for q in sorted(holding.rglob('*'),key=lambda q:len(q.parts),reverse=True):
 if q.is_symlink() or q.is_file():q.unlink()
 else:q.rmdir()
holding.rmdir();assert inventory(destination)==rows
record.update(status='rehomed',oldPathSymlink=str(source.readlink()),sourceInputsUnchanged=True,verifiedUtc=datetime.now(timezone.utc).isoformat())
ledger.write_text(json.dumps(record,indent=2)+'\n');print(json.dumps({'status':'rehomed','bytes':size,'files':len(rows),'sourceInputsUnchanged':True,'ledger':str(ledger.relative_to(ROOT))}))
