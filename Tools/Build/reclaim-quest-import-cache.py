#!/usr/bin/env python3
"""Reclaim only explicit inactive imported Artifact blobs after its verified player."""
from datetime import datetime,timezone
from pathlib import Path
import argparse,hashlib,json,os,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import verify,digest
p=argparse.ArgumentParser();p.add_argument('game',choices=('FantasyCards','PathOfDawn'));a=p.parse_args()
# Only the other explicitly identified game may be running; no concurrent target
# Unity or unknown primary Unity process can authorize cache reclamation.
active=[]
for line in subprocess.check_output(['ps','-eo','pid=,stat=,comm=,args='],text=True).splitlines():
 parts=line.split(None,3)
 if len(parts)!=4:continue
 pid,state,name,command=parts
 if state.startswith('Z') or name!='Unity':continue
 assert a.game not in command,'Target Unity is still active'
 other='FantasyCards' if a.game=='PathOfDawn' else 'PathOfDawn'
 assert other in command,'Unknown active Unity project'
 active.append({'pid':int(pid),'command':command,'scope':'Unrelated active game; its caches are untouched'})
cache=ROOT/'Artifacts/QuestWork/cache'/a.game/'Artifacts'
assert cache.is_dir() and not cache.is_symlink()
library=ROOT/'Games'/a.game/'Library'
assert (library/'Artifacts').resolve()==cache
native=json.loads((ROOT/'Artifacts/Validation/Quest/Native'/a.game/'inputs-before.json').read_text())
assert verify(native)==native['fingerprint']
player=Path('/tmp/firstgame-quest-builds')/a.game/'Web'
evidence=dict(l.split(': ',1) for l in (player/'BuildEvidence.txt').read_text().splitlines() if ': ' in l)
assert evidence['Result']=='Succeeded' and evidence['Errors']=='0' and evidence['SourceFingerprint']==native['fingerprint']
player_rows=[{'file':str(q.relative_to(player)),'bytes':q.stat().st_size,'sha256':digest(q)} for q in sorted(player.rglob('*')) if q.is_file()]
protected={str(q):digest(q) for q in (library/'SourceAssetDB',library/'ArtifactDB') if q.is_file()}
package=library/'PackageCache';assert package.is_symlink();package_target=str(package.readlink())
rows=[]
for q in sorted(cache.rglob('*')):
 assert not q.is_symlink(),q
 if q.is_file():rows.append({'path':q.relative_to(cache).as_posix(),'bytes':q.stat().st_size,'sha256':digest(q)})
assert rows
v=os.statvfs(ROOT);before=v.f_bavail*v.f_frsize
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');ledger=ROOT/'Artifacts/Validation/Quest/Storage'/('inactive-import-blobs-'+a.game+'-'+stamp+'.json')
d={'status':'prepared','root':str(cache),'scope':'Only listed generated imported blobs in the explicitly inactive verified game. All authored source/meta/originals are reverified against approved Native inputs. SourceAssetDB/ArtifactDB bytes and PackageCache target are preserved; active other game/player/archives untouched. Unity can regenerate these imported blobs from unchanged originals.','sourceFingerprint':native['fingerprint'],'files':rows,'protectedDatabases':protected,'packageCacheSymlinkTarget':package_target,'currentPlayer':player_rows,'unrelatedActiveUnity':active,'workspaceFreeBefore':before};d['recovery']={'automaticRefreshIsNotSufficient':True,'observedFailure':'Retaining ArtifactDB while removing generated blobs previously required explicit synchronous force reimport; normal Refresh left missing artifacts.','preserve':'All original Assets/meta, importer settings, SourceAssetDB, ArtifactDB and PackageCache remain unchanged.','pathOfDawnCommand':'python3 Tools/Build/recover-quest-resource-cache.py <UniqueAttempt>','requiredUnityOptions':'ForceSynchronousImport | ForceUpdate; explicitly include retained Resources/settings roots and dependencies without changing importer settings.','cardScope':'If reopening FantasyCards, explicitly synchronously force reimport the required current Assets and dependencies; the Gospel-only recovery entry must not be used as evidence for Card recovery.','priorObservedRecovery':'Artifacts/Validation/Quest/Storage/import-cache-recovery-supplement-ResourceRecovery01.json'};ledger.write_text(json.dumps(d,indent=2)+'\n')
for r in rows:
 q=cache/r['path'];assert q.is_file() and not q.is_symlink() and q.resolve().is_relative_to(cache) and digest(q)==r['sha256'];q.unlink()
assert verify(native)==native['fingerprint']
assert all(digest(Path(k))==v for k,v in protected.items()) and str(package.readlink())==package_target
assert [{'file':str(q.relative_to(player)),'bytes':q.stat().st_size,'sha256':digest(q)} for q in sorted(player.rglob('*')) if q.is_file()]==player_rows
v=os.statvfs(ROOT);d.update(status='reclaimed',verifiedUtc=datetime.now(timezone.utc).isoformat(),sourceInputsUnchanged=True,databasesAndPackagesUnchanged=True,playerUnchanged=True,workspaceFreeAfter=v.f_bavail*v.f_frsize,reclaimedBytes=sum(r['bytes'] for r in rows));ledger.write_text(json.dumps(d,indent=2)+'\n');print(json.dumps({k:d[k] for k in ('status','reclaimedBytes','workspaceFreeAfter','sourceInputsUnchanged','databasesAndPackagesUnchanged','playerUnchanged')}))
