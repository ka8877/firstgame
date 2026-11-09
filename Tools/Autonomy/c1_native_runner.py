#!/usr/bin/env python3
"""UNEXECUTED C1 draft: isolate each exact-eight Card Native attempt.

Install only after G2 seal and source ownership transfer. The separate opt-in
Card capture-helper patch must be applied and bound before this wrapper runs.
"""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,os,re,subprocess,sys,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
DEFAULT_CONTRACT=ROOT/'Tools/Autonomy/c1-contract.json'
EXACT_NAMES={
 'FantasyCards.SmokeTests.NativitySmokeTests.ActualAttachedItemsCanBeInspectedAndDisposedWithOwnershipPreparationAndHealthRules',
 'FantasyCards.SmokeTests.NativitySmokeTests.AutomaticLethalEquipmentRestorationShowsZeroHealthAndNeverHitsTheOpponent',
 'FantasyCards.SmokeTests.NativitySmokeTests.FantasyTitleAndAllNineteenActualProductsHaveDistinctPackaging',
 'FantasyCards.SmokeTests.NativitySmokeTests.ProductBackSurvivesSealedReloadAndRevealsAllFiveActualDrawSlotsOnce',
 'FantasyCards.SmokeTests.NativitySmokeTests.RealEquipmentHeadsCoinAndDamageChangePhysicalCardNumeralsAtTheirActualImpacts',
 'FantasyCards.Tests.BattleEngineTests.UnityJsonAcceptsPreviousSchemaThreeWithoutGeneratedCardList',
 'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.FidelityGradeGuideAndRankCapabilityFiltersUseActualCards',
 'FantasyCards.SmokeTests.FantasyCardsRevivalSmokeTests.OriginalCapabilityLabelsAndBrowseFiltersPreserveKnownCardsAndSaves'}
def require(ok,message):
 if not ok:raise ValueError(message)
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text())
def reference(p):
 p=Path(p);return {'file':str(p.relative_to(ROOT))if p.is_relative_to(ROOT)else str(p),'sha256':digest(p),'bytes':p.stat().st_size}
def regular(p):
 p=Path(p);return p.is_file() and not p.is_symlink() and not any(v.is_symlink()for v in p.parents)
def preflight(path):
 c=read(path);require(c.get('revisionConfirmedByRoot')is True and c.get('revision')=='fm-capability-2026-11-03','Confirmed singular C1 revision required')
 for key in ('receipt','rootReview','manifest','publicAudit','gitTree'):
  r=c['sealedG2'][key];p=ROOT/r['file'];require(re.fullmatch('[0-9a-f]{64}',r.get('sha256')or'')and isinstance(r.get('bytes'),int)and p.is_file()and not p.is_symlink()and digest(p)==r['sha256']and p.stat().st_size==r['bytes'],'Fill and match every actual sealed G2 regular-leaf pin before C1 execution: '+key)
 receipt=read(ROOT/c['sealedG2']['receipt']['file']);review=read(ROOT/c['sealedG2']['rootReview']['file'])
 require(receipt.get('status')=='verified-published'and receipt.get('workId')=='G2'and review.get('status')=='approved'and review.get('reviewer')in('root','/root')and review.get('receipt')==c['sealedG2']['receipt'],'Actual completed Root-sealed G2 receipt required')
 pins=c['sealedG2'];tree=read(ROOT/pins['gitTree']['file']);require(re.fullmatch('[0-9a-f]{40}',pins.get('deployedCommit')or'')and receipt.get('sourceCommit')==review.get('sourceCommit')==pins['sourceCommit']and receipt.get('deployedCommit')==review.get('deployedCommit')==pins['deployedCommit']==tree['commit']and receipt.get('manifest')==review.get('manifest')==pins['manifest']and receipt.get('publicPortal')==pins['publicAudit']and receipt.get('publicGitTree')==pins['gitTree'],'Every actual final G2 source/public/manifest/audit/tree identity must be filled and Root-sealed')
 for name,h in c['sharedSourceSha256'].items():require(regular(ROOT/name)and digest(ROOT/name)==h,'Frozen common source changed: '+name)
 helper=c['captureHelper'];require(regular(ROOT/helper['file'])and digest(ROOT/helper['file'])==helper['requiredPatchedSha256'],'Apply and bind the separate C1 opt-in capture helper before any Native execution')
 approved=c.get('approvedC1Sources',{});required={'Games/FantasyCards/Assets/Game/Presentation/FmFidelityUi.cs','Games/FantasyCards/Assets/SmokeTests/FantasyCardsFidelitySmokeTests.cs','Games/FantasyCards/Assets/SmokeTests/FantasyCardsCapabilitySmokeTests.cs','Games/FantasyCards/Assets/SmokeTests/FantasyCardsCapabilitySmokeTests.cs.meta'}
 require(set(approved)==required,'Root source freeze must bind product, corrected/rerouted existing fixture and exact new test/meta')
 for name,h in approved.items():require(re.fullmatch('[0-9a-f]{64}',h or'')and regular(ROOT/name)and digest(ROOT/name)==h,'Fill actual approved final C1 source SHA: '+name)
 return c
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('attempt');p.add_argument('--contract',type=Path,default=DEFAULT_CONTRACT);a=p.parse_args()
 require(re.fullmatch('[A-Za-z0-9][A-Za-z0-9_-]{0,63}',a.attempt),'Unique safe attempt required');c=preflight(a.contract)
 contract_path=ROOT/'Tools/Autonomy/c1-native-contract.json';contract=read(contract_path)
 require(digest(contract_path)==c['nativeContractSha256']and set(contract['expectedFullNames'])==EXACT_NAMES and len(contract['expectedFullNames'])==8,'Exact old6 + corrected existing1 + new1 source-based contract required')
 out=ROOT/c['output'];run=out/'NativeRuns'/a.attempt
 require(out.parent==ROOT/'Artifacts/Validation/Autonomy'and out.name.startswith('C1-')and not out.is_symlink()and not any(v.is_symlink()for v in out.parents),'Regular dedicated C1 output under Autonomy required')
 require(not run.exists()and not run.is_symlink(),'Every C1 Native attempt has its own absent validation root');run.mkdir(parents=True)
 captures=run/'Screenshots/FantasyCards'/a.attempt
 env=dict(os.environ,FIRSTGAME_VALIDATION_ROOT=str(run),FIRSTGAME_BUILD_ROOT=str(Path(c['cardBuildRoot'])),FIRSTGAME_NATIVE_FILTER=';'.join(sorted(EXACT_NAMES)),FIRSTGAME_C1_NATIVE_CAPTURE_ROOT=str(captures),FIRSTGAME_NATIVE_ATTEMPT=a.attempt)
 source=Path(__file__);recorded=run/'RecordedHarness/Tools/Autonomy/c1_native_runner.py';recorded.parent.mkdir(parents=True);recorded.write_bytes(source.read_bytes())
 (run/'c1-contract.executed.json').write_bytes(a.contract.read_bytes());(run/'native-contract.executed.json').write_bytes(contract_path.read_bytes())
 result=subprocess.run([sys.executable,str(ROOT/'Tools/Validation/run-nativity-native.py'),'FantasyCards',a.attempt],cwd=ROOT,env=env)
 native=run/'Native/FantasyCards';xml=native/'native.xml';binding=native/'binding.json'
 summary={'status':'failed','finishedUtc':datetime.now(timezone.utc).isoformat(),'exitCode':result.returncode,'revision':c['revision'],'runRoot':str(run.relative_to(ROOT)),'captureRoot':str(captures.relative_to(ROOT)),'expectedFullNames':sorted(EXACT_NAMES),'wrapper':reference(recorded),'contract':reference(run/'c1-contract.executed.json'),'exactContract':reference(run/'native-contract.executed.json'),'scope':'Fresh exact8 C1 Card execution only if all unchanged common input/result gates and added exact-set gate pass. Earlier Nativity6 evidence remains historical.'}
 try:
  require(result.returncode==0 and xml.is_file()and binding.is_file(),'Actual successful common Native run required')
  b=read(binding);t=ET.parse(xml).getroot();cases=t.findall('.//test-case');names=[v.get('fullname')for v in cases]
  require(b.get('status')=='passed'and b.get('sourceChangedDuringRun')is False and b.get('xmlSha256')==digest(xml)and b.get('inputManifestSha256')==digest(native/'inputs-before.json')and b.get('extraFixtureInputs')==[],'Actual Card input/XML binding must remain exact; no Gospel voice fixture is claimed')
  require(t.get('result')=='Passed'and t.get('passed')=='8'and t.get('failed')=='0'and t.get('skipped')=='0'and len(names)==8 and set(names)==EXACT_NAMES and all(v.get('result')=='Passed'for v in cases),'All exact retained6 + corrected existing1 + new1 must pass without duplicates, skips or subsets')
  require(captures.is_dir()and not any(v.is_symlink()for v in run.rglob('*')),'Actual captures and complete attempt evidence must remain regular')
  originals=[reference(v)for v in sorted(captures.iterdir())if v.is_file()];require(originals and all(v['file'].endswith(('.png','.png.txt'))for v in originals),'Actual isolated PNG/sidecar originals required')
  summary.update(status='passed',binding=reference(binding),nativeXml=reference(xml),sourceFingerprint=b['sourceFingerprint'],originals=originals)
 except Exception as error:summary['error']=str(error)
 (run/'c1-native-exact-contract.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n');print(json.dumps(summary,ensure_ascii=False));return 0 if summary['status']=='passed'else 1
if __name__=='__main__':raise SystemExit(main())
