#!/usr/bin/env python3
"""Run meaningful Core tests with immutable source/command/results binding."""
import argparse,hashlib,json,os,subprocess,sys,xml.etree.ElementTree as ET
from datetime import datetime,timezone
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def inputs():
 files=set()
 for folder in ['Games/FantasyCards/Assets/Game/Core','Games/PathOfDawn/Assets/Game/Core','Tools/Validation/Tests','Games/PathOfDawn/Packages/com.inkle.ink-unity-integration/InkLibs/InkCompiler','Games/PathOfDawn/Packages/com.inkle.ink-unity-integration/InkLibs/InkRuntime']:
  files.update((ROOT/folder).rglob('*.cs'))
 for name in ['Games/FantasyCards/Assets/Game/Tests/BattleEngineTests.cs','Games/FantasyCards/Assets/Shared/SafeSaveService.cs','Games/PathOfDawn/Assets/Game/Tests/EditMode/NarrativeTests.cs','Games/PathOfDawn/Assets/Game/Resources/Story/galilee.ink','Tools/Validation/CoreValidation.csproj','Tools/Validation/packages.lock.json']:
  files.add(ROOT/name)
 return [{'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(files)]
def verify_current(record):return record['inputs']==inputs()
def main():
 p=argparse.ArgumentParser();p.add_argument('--verify',action='store_true');a=p.parse_args();out=ROOT/'Artifacts/Validation/Polish/Core';out.mkdir(parents=True,exist_ok=True)
 if a.verify:
  r=json.loads((out/'binding.json').read_text());assert r['status']=='passed' and verify_current(r) and sha(out/'core-tests.trx')==r['results']['sha256'];print('Current Core source/results binding verified');return
 stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');attempt=out/'Attempts'/stamp;attempt.mkdir(parents=True)
 before=inputs();record={'status':'running','startedUtc':datetime.now(timezone.utc).isoformat(),'inputs':before,'command':['dotnet','test','Tools/Validation/CoreValidation.csproj','--no-restore','--configuration','Release','--logger','trx;LogFileName=core-tests.trx','--results-directory',str(attempt)]}
 (attempt/'inputs-before.json').write_text(json.dumps(before,indent=2)+'\n')
 command='source Tools/Environment/activate-shell.sh\nexport PATH_OF_DAWN_INK="$PWD/Games/PathOfDawn/Assets/Game/Resources/Story/galilee.ink"\nexec "$DOTNET_ROOT/dotnet" "$@"'
 with (attempt/'core.log').open('w')as log:
  value=subprocess.run(['bash','-c',command,'renewal-core',*record['command'][1:]],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
 print((attempt/'core.log').read_text())
 record.update(exitCode=value.returncode,sourceChangedDuringRun=before!=inputs(),finishedUtc=datetime.now(timezone.utc).isoformat())
 trx=attempt/'core-tests.trx'
 if trx.exists():
  c=ET.parse(trx).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters');record['counts']=dict(c.attrib);record['results']={'file':str(trx.relative_to(ROOT)),'sha256':sha(trx),'bytes':trx.stat().st_size}
  if value.returncode==0 and not record['sourceChangedDuringRun'] and c.get('failed')=='0' and c.get('notExecuted')=='0' and int(c.get('passed','0'))>=170:record['status']='passed'
 if record['status']!='passed':record['status']='failed'
 (attempt/'binding.json').write_text(json.dumps(record,indent=2)+'\n')
 if record['status']=='passed':
  for name in ['binding.json','core.log','core-tests.trx']:(out/name).write_bytes((attempt/name).read_bytes())
 print(json.dumps({k:v for k,v in record.items() if k!='inputs'}));sys.exit(0 if record['status']=='passed'else 1)
if __name__=='__main__':main()
