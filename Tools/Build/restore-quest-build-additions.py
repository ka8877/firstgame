#!/usr/bin/env python3
"""Retain and remove only newly generated Unity build resources/empty holding metas."""
from pathlib import Path
import argparse,json,shutil,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import files,verify,digest
p=argparse.ArgumentParser();p.add_argument('game',choices=('FantasyCards','PathOfDawn'));p.add_argument('attempt');a=p.parse_args()
reference=json.loads((ROOT/'Artifacts/Validation/Quest/Native'/a.game/'inputs-before.json').read_text())
old={r['file']:r for r in reference['files']};current={r['file']:r for r in files(a.game)}
changed=[k for k in old if old[k]!=current.get(k)]
assert not changed,('Authored inputs changed; no restoration permitted',changed)
extra={k:v for k,v in current.items() if k not in old}
base='Games/'+a.game+'/Assets/'
holding=base+'Art/Archive/BuildExcludedLegacy'
allowed={'PerformanceTestRunInfo.json','PerformanceTestRunInfo.json.meta','PerformanceTestRunSettings.json','PerformanceTestRunSettings.json.meta'}
assert all((k.startswith(holding) and k.endswith('.meta')) or (k.startswith(base+'Resources/PerformanceTestRun') and k.rsplit('/',1)[1] in allowed) for k in extra),list(extra)
out=ROOT/'Artifacts/Validation/Quest'/a.game/a.attempt/'GeneratedProjectAdditions';assert not out.exists();out.mkdir(parents=True)
record={'status':'prepared','scope':'Only newly generated performance resources and empty resource-isolation holding metas absent from approved Native inputs. Every byte retained here; authored inputs remain exact.','files':list(extra.values())}
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
for k,row in extra.items():
 source=ROOT/k;target=out/k;assert digest(source)==row['sha256'];target.parent.mkdir(parents=True,exist_ok=True);shutil.move(source,target);assert digest(target)==row['sha256']
if holding+'.meta' in extra:
 directory=ROOT/holding
 if directory.exists():
  for q in sorted(directory.rglob('*'),key=lambda q:len(q.parts),reverse=True):
   assert q.is_dir() and not q.is_symlink(),q;q.rmdir()
  directory.rmdir()
record.update(status='restored-approved-inputs',sourceFingerprint=verify(reference))
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
print('Retained Unity build-generated additions:',len(extra),'approved source',record['sourceFingerprint'])
