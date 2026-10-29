#!/usr/bin/env python3
"""Scoped development preview while streamed media is incomplete; never a release gate.

Temporarily moves generated CardAssets+their stable metadata with a durable
recovery journal. The unchanged shipping App uses its normal zero-authored-data
Core fallback. All original art stays in place. Restores exact bytes in finally.
Production Prepare/Validate/full media gates remain unchanged and strict.
"""
import argparse,hashlib,json,os,shutil,subprocess,sys
from pathlib import Path
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify
SOURCE=ROOT/'Games/FantasyCards/Assets/Resources/Data/Cards'
STORE=Path('/tmp/firstgame-renewal-draft-authoring')
JOURNAL=ROOT/'Artifacts/Validation/Polish/CardDraftRelocation.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rows(folder):return [{'file':str(p.relative_to(folder)),'sha256':sha(p),'bytes':p.stat().st_size}for p in sorted(folder.rglob('*'))if p.is_file()]
def save(r):JOURNAL.parent.mkdir(parents=True,exist_ok=True);temporary=JOURNAL.with_suffix('.tmp');temporary.write_text(json.dumps(r,indent=2)+'\n');temporary.replace(JOURNAL)
def restore():
 r=json.loads(JOURNAL.read_text());assert r['state'] in ('planned','moved','restoring','restored')
 if r['state']=='restored':return
 r['state']='restoring';save(r)
 if STORE.joinpath('Cards').exists():
  assert not SOURCE.exists(),'Refuse overwriting newly authored files'
  assert rows(STORE/'Cards')==r['files'],'Retained originals changed'
  shutil.move(str(STORE/'Cards'),str(SOURCE))
 meta=SOURCE.with_suffix('.meta')
 if (STORE/'Cards.meta').exists():
  assert not meta.exists();shutil.move(str(STORE/'Cards.meta'),str(meta))
 assert rows(SOURCE)==r['files'] and sha(meta)==r['folderMetaSha256']
 r['state']='restored';r['restoredUtc']=datetime.now(timezone.utc).isoformat();save(r)
def main():
 p=argparse.ArgumentParser();p.add_argument('attempt',nargs='?');p.add_argument('--restore',action='store_true');a=p.parse_args()
 if a.restore:restore();return
 subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
 assert a.attempt and a.attempt.replace('-','').replace('_','').isalnum()
 if JOURNAL.exists():assert json.loads(JOURNAL.read_text())['state']=='restored','Restore previous attempt first'
 out=ROOT/'Artifacts/Validation/Polish/FantasyCards'/a.attempt;out.mkdir(parents=True,exist_ok=True);assert not(out/'native.log').exists()
 STORE.mkdir(exist_ok=True);assert not(STORE/'Cards').exists()
 before=capture('FantasyCards');(out/'inputs-before-relocation.json').write_text(json.dumps(before,indent=2)+'\n')
 r={'state':'planned','attempt':a.attempt,'source':str(SOURCE),'temporary':str(STORE),'files':rows(SOURCE),'folderMetaSha256':sha(SOURCE.with_suffix('.meta')),'scope':'Development-only current Core/UI with incomplete media. No generated authoring, shipping build, full archive, or final validation claim.'};save(r)
 shutil.move(str(SOURCE),str(STORE/'Cards'));shutil.move(str(SOURCE.with_suffix('.meta')),str(STORE/'Cards.meta'));r['state']='moved';save(r)
 filter='FantasyCards.SmokeTests.RenewalSmokeTests.RemasterTitleLocalRoomsDeckChoiceShuffleAndContinueUseLiveCallbacks;FantasyCards.SmokeTests.RenewalSmokeTests.FidelityRealDragAndOrderedPlaybackPreserveBattleAndRandomness;FantasyCards.SmokeTests.RenewalSmokeTests.KnownLateEraSecretAndLegendOriginalsLoadThroughSearch;FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList'
 command=['/workspace/tools/unity-editors/6000.3.26f1/Editor/Unity','-batchmode','-force-glcore','-projectPath','Games/FantasyCards','-runTests','-testPlatform','EditMode','-testFilter',filter,'-testResults',str(out/'native.xml'),'-logFile',str(out/'native.log')]
 (out/'scope.json').write_text(json.dumps({**r,'command':command},indent=2)+'\n')
 try:
  env=dict(os.environ);env['FIRSTGAME_NATIVE_ATTEMPT']=a.attempt
  value=subprocess.run(command,cwd=ROOT,env=env)
 finally:restore()
 shutil.copyfile(JOURNAL,out/'restoration.json')
 unchanged=True
 try:verify(before)
 except AssertionError:unchanged=False
 (out/'source-restoration.json').write_text(json.dumps({'sourceFingerprintBefore':before['fingerprint'],'currentSourceMatchesBeforeAfterRestoration':unchanged,'scope':'Development-only fallback run; full authored CardAssets temporarily absent during runtime, originals restored exactly.'},indent=2)+'\n')
 sys.exit(value.returncode if unchanged else 3)
if __name__=='__main__':main()
