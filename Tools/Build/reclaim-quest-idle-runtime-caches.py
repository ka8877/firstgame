#!/usr/bin/env python3
"""Reclaim only explicitly approved inactive compiler/runtime copies, with SHA receipts."""
from datetime import datetime,timezone
from pathlib import Path
import json,os,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
from quest_release_evidence import BUILD,REVISIONS,runtime
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
roots=[Path('/tmp/firstgame-luminous-runtime-cache/unity3d/bee'),
 Path('/tmp/firstgame-restoration-runtime-cache/unity3d/bee'),
 ROOT/'Artifacts/QuestWork/cache/PathOfDawn/Library/ScriptAssemblies',
 ROOT/'Artifacts/QuestWork/cache/PathOfDawn/PlayerDataCache',
 ROOT/'Artifacts/QuestWork/cache/PathOfDawn/Library/BuildPlayerData',
 ROOT/'Tools/Validation/bin',ROOT/'Tools/Validation/obj']
inputs={g:capture(g) for g in REVISIONS}
players={g:{n:{'bytes':p.stat().st_size,'sha256':digest(p)} for n,p in runtime(BUILD/g/'Web').items()} for g in REVISIONS}
protected={str(p):digest(p) for g in REVISIONS for n in ('SourceAssetDB','ArtifactDB') for p in [ROOT/'Games'/g/'Library'/n] if p.is_file()}
packages={g:str((ROOT/'Games'/g/'Library/PackageCache').readlink()) for g in REVISIONS}
rows=[]
for r in roots:
 assert r.is_dir() and not r.is_symlink(),r
 for p in sorted(r.rglob('*')):
  assert not p.is_symlink(),p
  if p.is_file():rows.append({'file':str(p),'bytes':p.stat().st_size,'sha256':digest(p)})
def free(root):
 s=os.statvfs(root);return s.f_bavail*s.f_frsize
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
p=ROOT/'Artifacts/Validation/Quest/Storage'/('idle-runtime-compiler-'+stamp+'.json')
record={'status':'prepared','scope':'Only exact listed inactive historical Unity Bee caches, inactive Gospel generated managed/player caches and generated validation .NET bin/obj. No source, PackageCache, AssetDB, original, player or ZIP is removed. Generated .NET outputs recover from unchanged project/packages; Unity outputs regenerate from unchanged source/import settings.','roots':list(map(str,roots)),'files':rows,'sourceFingerprints':{g:i['fingerprint'] for g,i in inputs.items()},'databases':protected,'packageCacheTargets':packages,'currentPlayers':players,'beforeFree':{r:free(r) for r in ('/tmp','/workspace')}}
p.write_text(json.dumps(record,indent=2)+'\n')
for row in rows:
 q=Path(row['file']);assert q.is_file() and not q.is_symlink() and digest(q)==row['sha256'];q.unlink()
assert all(verify(i)==i['fingerprint'] for i in inputs.values())
assert all(digest(Path(k))==v for k,v in protected.items())
assert all(str((ROOT/'Games'/g/'Library/PackageCache').readlink())==v for g,v in packages.items())
assert players=={g:{n:{'bytes':q.stat().st_size,'sha256':digest(q)} for n,q in runtime(BUILD/g/'Web').items()} for g in REVISIONS}
record.update(status='reclaimed',finishedUtc=datetime.now(timezone.utc).isoformat(),allOriginalInputsDatabasesPackagesAndPlayersUnchanged=True,afterFree={r:free(r) for r in ('/tmp','/workspace')},reclaimedBytes=sum(r['bytes'] for r in rows))
p.write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({k:record[k] for k in ('status','reclaimedBytes','afterFree','allOriginalInputsDatabasesPackagesAndPlayersUnchanged')}))
