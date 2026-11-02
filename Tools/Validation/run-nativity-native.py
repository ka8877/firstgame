#!/usr/bin/env python3
"""Run one current native suite, retaining each attempt and binding exact per-game inputs."""
import argparse,json,shutil,subprocess,sys
from pathlib import Path
from datetime import datetime,timezone
import xml.etree.ElementTree as ET
from restoration_source_binding import ROOT,capture,verify,digest
p=argparse.ArgumentParser();p.add_argument('game',choices=('FantasyCards','PathOfDawn'));p.add_argument('attempt');a=p.parse_args()
out=ROOT/'Artifacts/Validation/Nativity';stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');attempt=out/'NativeAttempts'/a.game/stamp;attempt.mkdir(parents=True)
def fonts():
 return [{'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':digest(p)} for name in ('Korean','Display') for p in [ROOT/f'Games/{a.game}/Assets/Resources/Fonts/{name}.asset']]
font_before=fonts()
extra_fixture_inputs=[]
if a.game=='PathOfDawn':
 for relative in ('Tools/Voice/Nativity/production.json',):
  source=ROOT/relative;preserved=attempt/'FixtureInputs'/relative;preserved.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,preserved)
  extra_fixture_inputs.append({'file':relative,'preservedFile':str(preserved.relative_to(ROOT)),'sha256':digest(source),'bytes':source.stat().st_size})
shutil.copyfile(Path(__file__),attempt/'executed-runner.py')
inputs=capture(a.game);(attempt/'inputs-before.json').write_text(json.dumps(inputs,indent=2)+'\n')
value=subprocess.run(['bash',str(ROOT/'Tools/Build/nativity-task.sh'),a.game,'native',a.attempt],cwd=ROOT)
for source in (out/a.game/a.attempt/'native.xml',out/a.game/a.attempt/'native.log'):
 if source.is_file():shutil.copyfile(source,attempt/source.name)
changed=False
try:verify(inputs)
except AssertionError:changed=True
for row in extra_fixture_inputs:
 source=ROOT/row['file']
 if not source.is_file() or digest(source)!=row['sha256'] or source.stat().st_size!=row['bytes']:changed=True
record={'game':a.game,'status':'failed','exitCode':value.returncode,'sourceChangedDuringRun':changed,'startedInputs':str((attempt/'inputs-before.json').relative_to(ROOT)),'sourceFingerprint':inputs['fingerprint'],'inputManifestSha256':digest(attempt/'inputs-before.json')}
record['extraFixtureInputs']=extra_fixture_inputs
record['runner']={'file':str((attempt/'executed-runner.py').relative_to(ROOT)),'sha256':digest(attempt/'executed-runner.py')}
xml=attempt/'native.xml'
if xml.is_file():
 tree=ET.parse(xml).getroot();record.update(result=tree.get('result'),passed=tree.get('passed'),failed=tree.get('failed'),finishedUtc=tree.get('end-time'),xmlSha256=digest(xml))
 if value.returncode==0 and tree.get('result')=='Passed' and tree.get('skipped')=='0' and not changed:record['status']='passed'
(attempt/'generated-font-cache.json').write_text(json.dumps({'scope':'Generated TMP atlas/kerning cache assets; origin OTFs, stable metadata and generator are in the authored-input fingerprint. Runtime font visibility is checked in actual captures.','before':font_before,'after':fonts()},indent=2)+'\n')
(attempt/'binding.json').write_text(json.dumps(record,indent=2)+'\n')
if record['status']=='passed':
 target=out/'Native'/a.game
 if target.exists():
  history=out/'NativeHistory'/a.game/stamp;history.parent.mkdir(parents=True,exist_ok=True)
  old_rows=[{'file':str(q.relative_to(target)),'bytes':q.stat().st_size,'sha256':digest(q)} for q in sorted(target.rglob('*')) if q.is_file()]
  assert old_rows and not history.exists()
  target.rename(history)
  prior_review=out/'Root'/('gospel-native-review.json' if a.game=='PathOfDawn' else 'cards-native-review.json')
  if prior_review.is_file():shutil.copyfile(prior_review,history/'root-approved-review-snapshot.json')
  (history/'relocation-ledger.json').write_text(json.dumps({'scope':'Exact former canonical Native binding/results retained before replacement; approved old source/results resolve through this history.','previousRoot':str(target.relative_to(ROOT)),'archivedRoot':str(history.relative_to(ROOT)),'files':old_rows},indent=2)+'\n')
 target.mkdir(parents=True,exist_ok=True)
 for name in ('native.xml','inputs-before.json','binding.json','native.log','generated-font-cache.json'):shutil.copyfile(attempt/name,target/name)
print(json.dumps(record));sys.exit(0 if record['status']=='passed'else 1)
