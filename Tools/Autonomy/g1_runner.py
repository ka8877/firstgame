#!/usr/bin/env python3
"""Bound G1 output paths around the existing Unity runners; never publish."""
from datetime import datetime, timezone
import argparse, importlib.util, json, os, re, shutil, subprocess, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Validation/Autonomy/G1-2026-11-03'
BUILD=Path(os.environ.get('FIRSTGAME_G1_BUILD_ROOT','/tmp/firstgame-g1-builds'))
FILTER=';'.join('PathOfDawn.SmokeTests.'+name for name in ('GospelNativityStorySmokeTests','GospelNativityVisualSmokeTests','GospelNativityCharacterSmokeTests','GospelNativityAudioSmokeTests','GospelNativityMovementAudioSmokeTests','GospelNativityFiniteRenderingTests','GospelEncounterSmokeTests'))

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('action',choices=('prepare','native','web','core'));p.add_argument('attempt');a=p.parse_args()
 if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}',a.attempt):p.error('Unique safe attempt required')
 if OUT.is_symlink() or BUILD.is_symlink():p.error('Regular owned output roots required')
 OUT.mkdir(parents=True,exist_ok=True)
 env=dict(os.environ,FIRSTGAME_VALIDATION_ROOT=str(OUT),FIRSTGAME_BUILD_ROOT=str(BUILD),FIRSTGAME_NATIVE_FILTER=FILTER)
 if a.action=='native':command=[sys.executable,str(ROOT/'Tools/Validation/run-nativity-native.py'),'PathOfDawn',a.attempt]
 elif a.action in ('prepare','web'):command=['bash',str(ROOT/'Tools/Build/nativity-task.sh'),'PathOfDawn',a.action,a.attempt]
 else:return core(a.attempt,env)
 if a.action=='web' and (BUILD/'PathOfDawn/Web/BuildEvidence.txt').exists():p.error('Prior successful Web output is retained; use a new run/build root')
 return subprocess.run(command,cwd=ROOT,env=env).returncode

def core(attempt,env):
 spec=importlib.util.spec_from_file_location('g1_unchanged_core_inputs',ROOT/'Tools/Validation/run-nativity-core.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 folder=OUT/'Core'/attempt
 if folder.exists():raise SystemExit('Preserve prior Core attempt')
 folder.mkdir(parents=True);before=m.inputs();(folder/'inputs-before.json').write_text(json.dumps(before,indent=2)+'\n')
 command=['test','Tools/Validation/CoreValidation.csproj','--no-restore','--configuration','Release','--logger','trx;LogFileName=core-tests.trx','--results-directory',str(folder)]
 shell='source Tools/Environment/activate-shell.sh\nexport PATH_OF_DAWN_INK="$PWD/Games/PathOfDawn/Assets/Game/Resources/Story/galilee.ink"\nexec "$DOTNET_ROOT/dotnet" "$@"'
 with (folder/'core.log').open('w') as log:r=subprocess.run(['bash','-c',shell,'g1-core',*command],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
 import xml.etree.ElementTree as ET
 trx=folder/'core-tests.trx';record={'status':'failed','exitCode':r.returncode,'inputs':before,'sourceChangedDuringRun':before!=m.inputs(),'finishedUtc':datetime.now(timezone.utc).isoformat()}
 if trx.exists():
  c=ET.parse(trx).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters');record['counts']=dict(c.attrib);record['results']={'file':str(trx.relative_to(ROOT)),'sha256':m.sha(trx),'bytes':trx.stat().st_size}
  if r.returncode==0 and not record['sourceChangedDuringRun'] and c.get('failed')=='0' and c.get('notExecuted')=='0' and int(c.get('passed','0'))>=285:record['status']='passed'
 (folder/'binding.json').write_text(json.dumps(record,indent=2)+'\n')
 if record['status']=='passed':
  for name in ('binding.json','core.log','core-tests.trx'):shutil.copyfile(folder/name,OUT/'Core'/name)
 print(json.dumps({k:v for k,v in record.items() if k!='inputs'}));return 0 if record['status']=='passed'else 1

if __name__=='__main__':raise SystemExit(main())
