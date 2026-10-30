#!/usr/bin/env python3
"""Reclaim approved inactive Splash cache and exact installed-SDK sidecar copies."""
from pathlib import Path
from datetime import datetime,timezone
import json,os,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
from quest_release_evidence import BUILD,REVISIONS,runtime
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
cache=ROOT/'Artifacts/QuestWork/cache/PathOfDawn/Library/SplashScreenCache'
refs=ROOT/'Artifacts/ReferenceCompile/netstandard2.1'
installed=Path('/workspace/tools/unity-editors/6000.3.26f1/Editor/Data/Managed/UnityEngine')
rows=[]
assert cache.is_dir() and not cache.is_symlink()
for p in sorted(cache.rglob('*')):
 assert not p.is_symlink()
 if p.is_file():rows.append({'file':str(p),'bytes':p.stat().st_size,'sha256':digest(p),'recovery':'Regenerate only this generated Unity SplashScreenCache from unchanged project/installed Editor on the next build.'})
for p in sorted(refs.iterdir()):
 if p.suffix not in ('.pdb','.xml') or not p.is_file() or p.is_symlink():continue
 q=installed/p.name
 if q.is_file() and q.stat().st_size==p.stat().st_size and digest(q)==digest(p):
  rows.append({'file':str(p),'bytes':p.stat().st_size,'sha256':digest(p),'installedOriginal':str(q),'recovery':'Copy this exact retained installedOriginal back to file after verifying its recorded SHA/size. No project/runtime DLL is removed.'})
assert sum('installedOriginal' in r for r in rows)==162
inputs={g:capture(g) for g in REVISIONS}
databases={str(p):digest(p) for g in REVISIONS for n in ('SourceAssetDB','ArtifactDB') for p in [ROOT/'Games'/g/'Library'/n] if p.is_file()}
packages={g:str((ROOT/'Games'/g/'Library/PackageCache').readlink()) for g in REVISIONS}
dlls={str(p):digest(p) for p in refs.glob('*.dll')}
players={g:{n:digest(p) for n,p in runtime(BUILD/g/'Web').items()} for g in REVISIONS}
def free(r):
 s=os.statvfs(r);return s.f_bavail*s.f_frsize
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');out=ROOT/'Artifacts/Validation/Quest/Storage'/('sdk-sidecar-splash-'+stamp+'.json')
j={'status':'prepared','scope':'Only the approved inactive generated Gospel Splash cache and duplicate reference PDB/XML with exact same-name/size/SHA installed original. Installed SDK originals, all DLLs, project source, validation metadata, DB, PackageCache, successful players and all ZIPs are preserved.','files':rows,'sourceFingerprints':{g:i['fingerprint'] for g,i in inputs.items()},'protectedDatabases':databases,'packageCacheTargets':packages,'referenceDlls':dlls,'successfulPlayers':players,'beforeFree':{r:free(r) for r in ('/tmp','/workspace')}}
out.write_text(json.dumps(j,indent=2)+'\n')
for r in rows:
 p=Path(r['file']);assert p.is_file() and not p.is_symlink() and digest(p)==r['sha256']
 if 'installedOriginal' in r:assert digest(Path(r['installedOriginal']))==r['sha256']
 p.unlink()
assert all(verify(i)==i['fingerprint'] for i in inputs.values())
assert all(digest(Path(k))==v for k,v in databases.items())
assert all(str((ROOT/'Games'/g/'Library/PackageCache').readlink())==v for g,v in packages.items())
assert all(digest(Path(k))==v for k,v in dlls.items())
assert all('installedOriginal' not in r or digest(Path(r['installedOriginal']))==r['sha256'] for r in rows)
assert players=={g:{n:digest(p) for n,p in runtime(BUILD/g/'Web').items()} for g in REVISIONS}
j.update(status='reclaimed',finishedUtc=datetime.now(timezone.utc).isoformat(),allProtectedBytesUnchanged=True,reclaimedBytes=sum(r['bytes'] for r in rows),afterFree={r:free(r) for r in ('/tmp','/workspace')})
out.write_text(json.dumps(j,indent=2)+'\n')
print(json.dumps({k:j[k] for k in ('status','reclaimedBytes','afterFree','allProtectedBytesUnchanged')}))
