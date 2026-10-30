#!/usr/bin/env python3
"""Byte-identically relocate the explicitly approved historical Gospel Web02 player."""
from pathlib import Path
from datetime import datetime,timezone
import json,os,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
source=Path('/tmp/firstgame-quest-builds/Previous/PathOfDawn-Web-before-Web03-20261030T040702178384Z')
target=ROOT/'Artifacts/QuestWork/preservedplayers'/source.name
assert source.is_dir() and not source.is_symlink() and not target.exists()
assert all(not p.is_symlink() for p in source.rglob('*'))
def rows(root):return [{'file':str(p.relative_to(root)),'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(root.rglob('*')) if p.is_file()]
original=rows(source);assert original and any(r['file']=='BuildEvidence.txt' for r in original)
inputs={g:capture(g) for g in ('FantasyCards','PathOfDawn')}
stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');ledger=ROOT/'Artifacts/Validation/Quest/Storage'/('historical-Web02-relocation-'+stamp+'.json')
record={'status':'prepared','source':str(source),'target':str(target),'files':original,'bytes':sum(r['bytes'] for r in original),'scope':'Only the explicitly identified historical generated Gospel Web02 player; current Web03/Card output and all source/originals/ZIPs remain untouched. Exact full file-set/SHA is verified before replacing old directory with an explicit symlink.','sourceFingerprints':{g:i['fingerprint'] for g,i in inputs.items()},'restore':'While all runtime is idle, copy the target regular directory to a temporary sibling of the original path, verify this complete file-set/SHA, remove only the explicit oldpath symlink, and rename the temporary directory into that original path. Retain the workspace preserved target until restored bytes are verified.'}
ledger.write_text(json.dumps(record,indent=2)+'\n')
target.parent.mkdir(parents=True,exist_ok=True);shutil.copytree(source,target)
assert rows(target)==original==rows(source)
shutil.rmtree(source);source.symlink_to(target,target_is_directory=True)
assert source.is_symlink() and source.readlink()==target and rows(source)==original
assert all(verify(i)==i['fingerprint'] for i in inputs.values())
record.update(status='relocated-byte-identical',finishedUtc=datetime.now(timezone.utc).isoformat(),oldPathSymlink=str(source.readlink()),sourceInputsUnchanged=True)
ledger.write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({k:record[k] for k in ('status','bytes','oldPathSymlink','sourceInputsUnchanged')}))
