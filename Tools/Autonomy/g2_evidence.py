"""Strict G2 source/runtime gates and immutable dated G1 references.

Install beside g1_evidence.py in Tools/Autonomy before execution. Never validate
historical G1 by calling its current_build() against the changed G2 source.
"""
from pathlib import Path
import importlib.util,json,os,sys,urllib.parse,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Validation/Autonomy/G2-2026-11-03'
BUILD=Path(os.environ.get('FIRSTGAME_G2_BUILD_ROOT','/tmp/firstgame-g2-builds'))
STAGE=Path('/tmp/firstgame-g2-staged-site')
PRIOR=Path('/tmp/firstgame-g1-staged-site')
G1_OUT=ROOT/'Artifacts/Validation/Autonomy/G1-2026-11-03'
REVISION='gospel-facing-2026-11-03'
PROFILE='autonomy-g2'
G1_SOURCE='5e5a9dea94870674904b5e0eaf22682644354092'
G1_DEPLOYED='683b0700c8fbeaf2a07172a5f81c5cd0c6a6e6ee'
OLD_AUDIT=G1_OUT/'PublicPortal/report.json'
OLD_TREE=G1_OUT/'PublicGitObjects/current-gh-pages.json'
PUBLIC_BASE='https://ka8877.github.io/firstgame/'
PREFIX='games/fantasy-cards/StreamingAssets/Cards/'
COUNTS={'freshHttpsGets':63,'inheritedImageIdentities':15350}
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import verify,digest
import nativity_release_evidence as OLD
import g1_evidence as G1

# Checkpoint/plan configuration: keep this block in sync with the single README
# configuration section. All old five remain mandatory in every new profile.
G1_CHECKPOINTS={'title','g1-field-dialogue','g1-guidance','g1-saved','g1-continued'}
G2_CHECKPOINTS={'g2-planted-facing','g2-reading-bible','g2-book-return-reading','g2-reading-before-reload','g2-continued-reading','g2-continued-reading-bible','g2-continued-reading-return'}
RECOVERY_CHECKPOINT='continued-g2-reading'
CHECKPOINTS=G1_CHECKPOINTS|G2_CHECKPOINTS|{RECOVERY_CHECKPOINT}
BROWSER_OUTPUT=OUT/'BrowserV2'
PLAN_FILES={'Desktop720':'Tools/Autonomy/g2_desktop_plan_v2.json','Mobile844':'Tools/Autonomy/g2_mobile_plan_v2.json','PublicMobile844':'Tools/Autonomy/g2_public_plan_v2.json'}
PLAN_SCOPE={'Desktop720':(117,48),'Mobile844':(86,34),'PublicMobile844':(86,34)}
PLAN_SHA256={'Desktop720': '671bc149f43d57a477874ce9e78c8c3a08fbd35862423eb60716c8658dcc1ee6', 'Mobile844': '4b3ad486ed81d99a9e1256dda06036a140d4902d8af4b772c7c699943f91258d', 'PublicMobile844': '4b3ad486ed81d99a9e1256dda06036a140d4902d8af4b772c7c699943f91258d'}
NEW_CASES={'AcceptedKeyboardEncounterPlantsFacingBeforeSavingAndKeepsItAcrossBookAndReload','LegacyReadingHeadingPositionAndOriginalBytesSurviveBookAndRepeatedContinue'}
RETAINED_CASES={'CedarSourceAndRawHdrRemainFiniteBeforeThePostGuardAtMaryAndJordan','ChapterVoicesPhysicalCuesAndPreferenceMuteHaveActualSources','DetailedOpeningResumesQuietlyAndTheEntirePhysicalBibleStillAcquiresNineteenTimes','EncounterHudDirectionUsesTheActualCameraAndKeepsInteractionState','ExactNarrationSourcesRejectChangedTextStaleLoadsAndMutedOrDisabledPlayback','FieldConversationPreservesItsPhysicalEncounterAndSaveCursorAcrossBookAndReload','IllustratedBirthStillRetainsItsVoicedLineAcrossTheBible','ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet','NewBirthArrangementAndSilentOpeningCuesDuckOnlyForActualNarration','PaintedChaptersKeepEveryEncounterOnTheContinuousPhysicalFloor','RealPreviousLateralSaveForksWithoutTouchingItsOriginalBytes','RealTouchAxesPlantAtRestAndTheCameraKeepsTheLandscapeSteadyThroughAJump','ReleasedExplorationDashPlantsWhenThePhysicalBurstEnds','TempleGuideHeadRemainsVisibleAndLegacyEncounterSaveKeepsItsPosition'}
EXPECTED_CASES=RETAINED_CASES|NEW_CASES
def require(ok,message):
 if not ok:raise ValueError(message)
def read(p):return json.loads(Path(p).read_text())
def ref(p):
 p=Path(p);return {'file':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),'sha256':digest(p),'bytes':p.stat().st_size}
def identity(row):return {k:row.get(k) for k in ('file','bytes','sha256')}
def rows(folder):return [{'file':name,'bytes':p.stat().st_size,'sha256':digest(p)} for name,p in OLD.runtime(folder).items()]
def checked(reference):
 p=ROOT/reference['file'];require(p.is_file() and not p.is_symlink() and digest(p)==reference['sha256'] and ('bytes' not in reference or p.stat().st_size==reference['bytes']),'Immutable referenced regular leaf changed: '+str(p));return p
def sealed_g1_receipt():
 p=G1_OUT/'release-receipt.json';review_path=G1_OUT/'Root/g1-release-receipt-review.json'
 require(digest(p)=='d9e8a370fb3c04dd164241c0dccc2638f17dc7779f153c190e8ed11dbc0006ab' and p.stat().st_size==32782,'Exact sealed G1 release receipt required')
 require(digest(review_path)=='9008854997f7df326197917bf9d6ae47ce5b51f363d5fcf5d41f754e213b8e9a','Exact sealed G1 Root release review required')
 r=read(p);review=read(review_path)
 require(r.get('status')=='verified-published' and r.get('workId')=='G1' and r.get('sourceCommit')==review.get('sourceCommit')==G1_SOURCE and r.get('deployedCommit')==review.get('deployedCommit')==G1_DEPLOYED,'Sealed G1 source/public identity differs')
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc') and review.get('receipt')==ref(p) and review.get('manifest')==r.get('manifest'),'Sealed G1 Root approval differs')
 return r
def sealed_g1_public():
 """Check sealed historical bytes only; no old Gospel current-source verifier."""
 r=sealed_g1_receipt();manifest_path=checked(r['manifest'])
 require(manifest_path==PRIOR/'build-manifest.json' and r['manifest']['sha256']=='177b1809f9dad9e4bbe00a6397300db719f30f03b5accf9c48c0ff556e6b1560','Exact G1 predecessor manifest required')
 require(checked(r['publicPortal'])==OLD_AUDIT and r['publicPortal']['sha256']=='d1b19ae80483e461f2f284cd7665fedd147e1a13d0d727c5afae07510387ef9d','Exact G1 public portal required')
 require(checked(r['publicGitTree'])==OLD_TREE and r['publicGitTree']['sha256']=='4eb6a2b070a9a52131a413a039127357470f3492fb0014e56d2deaec7b6f00fe','Exact G1 full deployed tree required')
 touch=[v['report']for v in r['browserReports']if v['profile']=='PublicMobile844'];require(len(touch)==1 and touch[0]['sha256']=='5a72a4bc736b54dded50ae74b8a0c5fcf9c6405b11344b43adf69db3cea514b5','Exact historical G1 public-touch proof required');checked(touch[0])
 prior=read(manifest_path);audit=read(OLD_AUDIT);tree=read(OLD_TREE)
 require(PRIOR.is_dir() and not PRIOR.is_symlink() and not any(p.is_symlink()for p in PRIOR.rglob('*')),'G1 predecessor must remain a regular complete stage')
 require(prior.get('releaseProfile')=='autonomy-g1' and prior.get('currentTargets')==['Web'] and prior.get('publishReady') is True and prior.get('sourceDirty') is False and prior.get('sourceCommit')==G1_SOURCE,'Sealed clean complete G1 stage required')
 require(audit.get('status')=='passed' and audit.get('sourceCommit')==G1_SOURCE and audit.get('deployedCommit')==tree.get('commit')==G1_DEPLOYED and audit.get('manifest')==r['manifest'] and audit.get('auditCounts')==COUNTS,'Historical G1 audit/tree identity differs')
 expected={**prior['files'],'build-manifest.json':r['manifest']['sha256']};actual={p.relative_to(PRIOR).as_posix():p for p in PRIOR.rglob('*')if p.is_file() and p.name!='.firstgame-web-output'}
 require(set(actual)==set(expected)==set(tree['files']) and len(actual)==r['fileCountIncludingManifest']==15413,'Exact sealed G1 full file set required')
 audited=OLD.file_rows(audit['files'],'Sealed G1 public audit');require(set(audited)==set(expected),'Complete sealed G1 public rows required')
 for name,h in expected.items():
  p=actual[name];row=audited[name];require(p.is_file() and not p.is_symlink() and digest(p)==h==row.get('sha256')==row.get('expectedSha256') and p.stat().st_size==row.get('bytes')==row.get('expectedBytes') and row.get('passed') is True and row.get('status')==200,'Sealed G1 staged/public bytes changed: '+name)
  require(row.get('freshGet') is not(name.startswith(PREFIX) and name.endswith('.jpg')),'G1 historical fresh/inherited distinction changed')
 require(sum(p.stat().st_size for p in actual.values())==r['siteBytesIncludingManifest']==981801334,'Exact sealed G1 whole-site bytes required')
 _portal_originals(OLD_AUDIT,audit,PRIOR,G1_SOURCE,G1_DEPLOYED)
 ledger=G1_OUT/'public-portal-relocation-ledger.json';require(digest(ledger)=='9b7c58dd0fea85499f6a2887635f94a88445f29c9ada36f59f94d2131a6a7d03' and ledger.stat().st_size==1845,'Exact dated G1 portal relocation ledger required');relocation=read(ledger)
 require(relocation.get('logicalRoot')==str(OLD_AUDIT.parent) and relocation.get('physicalRoot')=='/tmp/firstgame-g1-public-portal-originals' and relocation.get('completeFileSetAndRegularLeavesVerified') is True,'Pinned G1 portal relocation ledger required')
 for entry in relocation['files']:checked({'file':str(OLD_AUDIT.parent/entry['file']),'bytes':entry['bytes'],'sha256':entry['sha256']})
 return {'manifest':ref(manifest_path),'audit':ref(OLD_AUDIT),'gitTree':ref(OLD_TREE),'receipt':ref(G1_OUT/'release-receipt.json'),'rootReview':ref(G1_OUT/'Root/g1-release-receipt-review.json'),'publicTouch':touch[0],'relocationLedger':ref(ledger),'sourceCommit':G1_SOURCE,'deployedCommit':G1_DEPLOYED,'scope':'Immutable completed G1 stage/publication, checked by sealed identities; no historical Gospel run is re-executed or relabelled current.'}
def core_evidence():
 # A partial or failed new G2 Core attempt never silently falls back to G1.
 fresh=OUT/'Core';reuse=not fresh.exists()or(fresh.is_dir() and not any(fresh.iterdir()));folder=G1_OUT/'Core'if reuse else fresh
 core=read(folder/'binding.json');trx=folder/'core-tests.trx';counter=ET.parse(trx).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
 spec=importlib.util.spec_from_file_location('g2_core_source',ROOT/'Tools/Validation/run-nativity-core.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 require(core['status']=='passed' and core['sourceChangedDuringRun'] is False and m.verify_current(core) and core['results']['sha256']==digest(trx) and counter is not None and counter.get('failed')=='0' and counter.get('notExecuted')=='0' and int(counter.get('passed','0'))>=285,'Complete285+ Core actual result and identical current inputs required')
 result={'results':ref(trx),'binding':ref(folder/'binding.json'),'reusedPreviousActualRun':reuse,'scope':'Actual earlier G1 run with byte-identical current Core inputs; not a new G2 execution.'if reuse else'Fresh G2 Core execution with current identical inputs.'}
 if reuse:require(sealed_g1_receipt()['currentBuild']['core']==ref(trx) and digest(folder/'binding.json')=='68714a90f78eb2080342130137b45438a31efd8c14d053eddfb7431dda916726' and (folder/'binding.json').stat().st_size==37132,'Reused actual G1 Core binding/result must remain sealed')
 return result
def current_build():
 require(BUILD.resolve() not in {Path('/tmp/firstgame-g1-builds'),Path('/tmp/firstgame-g1-fix01-builds'),Path('/tmp/firstgame-nativity-builds')} and not BUILD.is_symlink(),'New regular G2 build root required')
 native=OUT/'Native/PathOfDawn';binding=read(native/'binding.json');inputs=read(native/'inputs-before.json')
 require(binding['status']=='passed' and binding['sourceChangedDuringRun'] is False and verify(inputs)==binding['sourceFingerprint'],'Native must match all current Gospel inputs')
 require(binding.get('inputManifestSha256')==digest(native/'inputs-before.json') and binding.get('xmlSha256')==digest(native/'native.xml'),'Actual Native input manifest/results identity must remain exact')
 extra=binding.get('extraFixtureInputs',[]);require(len(extra)==1 and extra[0].get('file')=='Tools/Voice/Nativity/production.json','Retained actual voice-duration fixture input must remain bound')
 recording=extra[0];source=ROOT/recording['file'];preserved=ROOT/recording['preservedFile']
 require(source.is_file() and preserved.is_file() and digest(source)==digest(preserved)==recording['sha256'] and source.stat().st_size==preserved.stat().st_size==recording['bytes'],'Current/imported Native voice-duration production input changed')
 xml=ET.parse(native/'native.xml').getroot();names=[c.get('name')for c in xml.findall('.//test-case')]
 require(set(OLD.REQUIRED_NATIVE_TESTS['PathOfDawn'])|G1.NEW_CASES==RETAINED_CASES and len(EXPECTED_CASES)==16,'Exact retained14 plus new2 Native contract differs')
 require(xml.get('result')=='Passed' and xml.get('failed')=='0' and xml.get('skipped')=='0' and int(xml.get('passed','0'))==16 and len(names)==16 and set(names)==EXPECTED_CASES,'All retained14 and new2 exact Native cases must pass; targeted subsets cannot replace them')
 review_path=OUT/'Root/gospel-native-review.json';review=read(review_path)
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc') and review.get('sourceFingerprint')==binding['sourceFingerprint'] and review.get('nativeXml',{}).get('sha256')==digest(native/'native.xml'),'Explicit current native Root source/result review required')
 require(len(review.get('screenshots',[]))>=2,'Native Root must view current original captures')
 for shot in review['screenshots']:
  path=ROOT/shot.get('file',shot.get('path',''));require(path.is_relative_to(OUT/'Screenshots') and path.is_file() and not path.is_symlink() and digest(path)==shot['sha256'],'Native reviewed original changed')
 player=BUILD/'PathOfDawn/Web';evidence=player/'BuildEvidence.txt';e=dict(line.split(': ',1)for line in evidence.read_text().splitlines()if ': 'in line);build_inputs=read(player/'BuildInputs.json')
 require(e.get('Revision')==REVISION and e.get('Content')=='gospel-0.1.0' and e.get('Target')=='WebGL' and e.get('Result')=='Succeeded' and e.get('Errors')=='0' and e.get('LegacyResourcesRestored')=='true','Successful current original Web with restored legacy assets required')
 require(verify(build_inputs)==e.get('SourceFingerprint')==binding['sourceFingerprint'],'Current/native/build fingerprints must be identical')
 core=core_evidence()
 return {'native':ref(native/'binding.json'),'nativeResults':ref(native/'native.xml'),'nativeRootReview':ref(review_path),'targets':{'Web':{'evidence':ref(evidence),'inputs':ref(player/'BuildInputs.json'),'sourceFingerprint':binding['sourceFingerprint']}},'core':core['results'],'coreEvidence':core,'audioAssets':OLD.verify_audio_assets()}
def browser(profile):
 p=BROWSER_OUTPUT/profile/'PathOfDawn/report.json';r=read(p);mode,width,height={'Desktop720':('desktop',1280,720),'Mobile844':('mobile-landscape',844,390),'PublicMobile844':('mobile-landscape',844,390)}[profile]
 require(r.get('game')=='PathOfDawn' and r.get('revision')==REVISION and r.get('releaseProfile')==PROFILE and r.get('suite')=='modern-real-input-v1' and r.get('status')=='passed' and r.get('checks') and all(c.get('passed') is True for c in r['checks']),'Every fresh G2 real-input check must pass')
 caps=r.get('browserInputCapabilities',{});require(r.get('profile')==mode and r.get('interactionViewport')=={'width':width,'height':height} and caps.get('width')==width and caps.get('height')==height,'Exact actual interaction viewport required')
 require((caps.get('maxTouchPoints',0)>0 and caps.get('coarse') is True)if mode=='mobile-landscape'else(caps.get('maxTouchPoints')==0 and caps.get('coarse') is False),'Actual desktop/touch context required')
 checkpoints={c.get('name'):c for c in r.get('checkpoints',[])};require(len(checkpoints)==len(r.get('checkpoints',[])) and CHECKPOINTS.issubset(checkpoints),'Old five and complete new Reading/Book/reconnect facing checkpoints required')
 require(any(c.get('navigation',0)>0 for c in r.get('unityConfigurations',[])) and checkpoints['g1-continued'].get('navigation',0)>0,'Actual full browser reconnect must precede retained continued capture')
 late_saved=checkpoints['g1-saved'].get('navigation');late_continued=checkpoints['g1-continued'].get('navigation')
 require(isinstance(late_saved,int) and isinstance(late_continued,int) and late_continued>late_saved,'The retained field manual Save must have its own subsequent full browser reload, independently of early Reading reconnect')
 before=checkpoints['g2-reading-before-reload'].get('navigation');after=checkpoints['g2-continued-reading'].get('navigation')
 require(isinstance(before,int) and isinstance(after,int) and after>before and all(checkpoints[n].get('navigation')==after for n in ('g2-continued-reading-bible','g2-continued-reading-return')),'Fresh complete reload must precede same-Reading post-reconnect Book originals')
 require(checkpoints[RECOVERY_CHECKPOINT].get('navigation')==after,'Actual continued-g2-reading recovery original must belong to the early restored Reading navigation')
 current=rows(BUILD/'PathOfDawn/Web');require([identity(x)for x in r['buildFingerprint']['files']]==[identity(x)for x in current],'Every current runtime byte must match the actual browser')
 from nativity_narration_evidence import narration_evidence
 classifier=ROOT/'Tools/Validation/nativity_narration_evidence.py';harness=r.get('harness',{})
 require(r.get('narrationEvidence')==narration_evidence(r.get('console',[])) and harness.get('narrationClassifier')=={'file':str(classifier.relative_to(ROOT)),'sha256':digest(classifier)},'Retained exact narration source start/Book stop/next key and classifier SHA required')
 wrapper=harness.get('g2Wrapper',{});require(wrapper.get('file')=='Tools/Autonomy/g2_web_smoke.py' and digest(ROOT/wrapper['file'])==digest(ROOT/wrapper['preservedFile'])==wrapper.get('sha256'),'Exact executed G2 revision adapter must be preserved')
 plan_path=ROOT/PLAN_FILES[profile];require(Path(harness.get('plan','')).resolve()==plan_path.resolve() and digest(plan_path)==harness.get('planSha256')==PLAN_SHA256[profile],'Exact current authored G2 plan must match actual execution')
 plan=read(plan_path)['PathOfDawn'];require(plan.get('revision')==REVISION and plan.get('suite')=='modern-real-input-v1' and CHECKPOINTS.issubset(plan['requiredCheckpoints']) and set(plan['requiredCheckpoints']).issubset(checkpoints),'Every complete authored G2 checkpoint must remain captured')
 require((len(plan['steps']),len(plan['requiredCheckpoints']))==PLAN_SCOPE[profile],'Complete retained G1 and additional G2 authored scope required')
 captured=harness.get('capturedSources',[]);required_sources={'Tools/Validation/nativity-web-smoke.py','Tools/Validation/web-smoke.py','Tools/Validation/fm-provider-errors.py','Tools/Validation/fm-local-media-errors.py','Tools/Validation/webaudio-observer.js','Tools/Validation/fm-local-media-observer.js','Tools/Environment/assert-runtime-idle.py','Tools/Validation/nativity_narration_evidence.py',PLAN_FILES[profile]}
 require(len(captured)==len(required_sources) and required_sources=={v.get('source')for v in captured},'Every executed inherited harness/observer/classifier/plan copy required')
 for v in captured:require(digest(ROOT/v['source'])==digest(ROOT/v['file'])==v['sha256'],'Actually recorded current harness/plan source changed: '+v['source'])
 require(harness.get('file')=='Tools/Validation/nativity-web-smoke.py' and harness.get('sha256')==digest(ROOT/harness['file']),'Exact actual inherited browser driver required')
 require(harness.get('helpersSha256')==digest(ROOT/'Tools/Validation/web-smoke.py') and harness.get('pageErrorClassifier')=={'file':'Tools/Validation/fm-provider-errors.py','sha256':digest(ROOT/'Tools/Validation/fm-provider-errors.py')} and r['audioEvidence'].get('observerSha256')==digest(ROOT/'Tools/Validation/webaudio-observer.js'),'Actual current browser helpers/error classifier/PCM observer identities required')
 require(r.get('input') and any(v.get('label')=='현재 여정 저장'for v in r['input']),'Retained ordinary manual Save input required')
 labels={v.get('label')for v in r.get('audioEvidence',{}).get('windows',[])};require({'narration-initial-current-line','narration-book-stopped','narration-after-book-next-line'}.issubset(labels),'Retained actual PCM narration windows required')
 for v in r['audioEvidence']['windows']:
  raw=p.parent/v['rawEvidence'];require(raw.resolve().is_relative_to(p.parent.resolve()) and raw.is_file() and not raw.is_symlink() and digest(raw)==v['sha256'],'Actual unchanged PCM-window original required')
 require(not r.get('pageErrorDiagnostic',{}).get('gameRuntimeErrors') and not r.get('networkDiagnostic',{}).get('unexpectedFailures'),'Runtime errors and unexpected failed requests must remain absent')
 review=r.get('visualReview',{});require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc'),'Fresh direct Root review required')
 require([identity(x)for x in review.get('runtimeFiles',[])]==[identity(x)for x in current],'Root review must bind complete current runtime')
 originals=r.get('screenshots',[]);approved=review.get('screenshots',[]);approved_names={s.get('name')for s in approved}
 require(({'title','g1-field-dialogue','g1-guidance','g1-continued'}|G2_CHECKPOINTS|{RECOVERY_CHECKPOINT}).issubset(approved_names),'Root must view retained originals and every changed Reading/facing/Book/reconnect original')
 for shot in approved:
  original=ROOT/shot.get('file',shot.get('path',''));require(original.resolve().is_relative_to(p.parent.resolve()) and original.is_file() and not original.is_symlink() and digest(original)==shot['sha256'] and any(s.get('sha256')==shot['sha256'] and s.get('path',s.get('file'))==shot.get('path',shot.get('file'))for s in originals),'Review must name exact current unchanged original capture')
 if profile=='PublicMobile844':
  delivery=r.get('publicDelivery',{});url=PUBLIC_BASE+'games/path-of-dawn/';require(delivery.get('status')=='passed' and delivery.get('gameUrl')==url and [identity(x)for x in delivery.get('files',[])]==[identity(x)for x in current],'Every current public runtime must have actual HTTPS delivery proof')
  for v in delivery['files']:require(v.get('status')==200 and v.get('url')==urllib.parse.urljoin(url,urllib.parse.quote(v['file'],safe='/')),'Actual current public runtime URL/status must remain exact')
 return {'game':'PathOfDawn','profile':profile,'report':ref(p),'approvedOriginals':approved,'scope':'Fresh G2 actual input and exact current runtime. Browser originals show facing; Native separately proves exact yaw/XYZ/cursor/save bytes. Physical-device and human audio approval are separate.'}
def preserved_cards():
 # Deliberately preserve the unchanged function's Nativity PRIOR and all gates.
 cards=G1.preserved_cards();require(cards['sourceFingerprint']=='f906b1fe5a68e6ac7dac106af82940056631a451b513def6c42dfbaea06bb66c','Exact retained Card source/runtime identity required');return cards
def _portal_originals(path,report,stage,source,deployed):
 review=report.get('visualReview',{});require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('sourceCommit')==source and review.get('deployedCommit')==deployed and review.get('manifest')==ref(stage/'build-manifest.json') and review.get('auditCounts')==COUNTS,'Exact source/public/manifest-bound Root portal approval required')
 manifest=read(stage/'build-manifest.json');require(review.get('fileCount')==len(manifest['files'])+1==len(report['files']) and review.get('gameRevisions')==report.get('gameRevisions')==manifest['gameRevisions'],'Root portal file count/revisions must match exact current site')
 require(OLD.timestamp(review.get('reviewedUtc'),'Root portal review')>=OLD.timestamp(report.get('checkedUtc'),'Portal completion')>=OLD.timestamp(report.get('startedUtc'),'Portal start'),'Root portal review must follow completed current audit')
 require(len(report.get('checks',[]))==2 and all(c.get('passed') is True and c.get('status')==200 and c.get('assertions') and all(v is True for v in c['assertions'].values())for c in report['checks']),'Both actual public portal viewport checks must pass')
 originals=report.get('screenshots',[]);approved=review.get('screenshots',[])
 def capture_id(s):return {k:s.get(k)for k in ('file','sha256','captureKind','viewport')}
 require(len(originals)==len(approved)==2 and {s['file']:capture_id(s)for s in originals}=={s['file']:capture_id(s)for s in approved},'Both exact portal originals require Root review')
 require({c['screenshot']['file']:capture_id(c['screenshot'])for c in report['checks']}=={s['file']:capture_id(s)for s in originals} and all(c.get('viewport')==c['screenshot']['viewport'] and c.get('profile')==('desktop'if c['viewport']['width']==1440 else'mobile-portrait')for c in report['checks']),'Actual portal checks must bind both reviewed original viewports')
 require({(s.get('viewport',{}).get('width'),s.get('viewport',{}).get('height'))for s in originals}=={(1440,1050),(390,844)},'Exact desktop/mobile portal viewports required')
 for s in originals:
  original=checked(s);require(original.resolve().parent==path.parent.resolve() and s.get('captureKind')=='original-full-page' and s.get('file')==str((path.parent/('landing-'+str(s['viewport']['width'])+'.png')).relative_to(ROOT)),'Actual unchanged full-page portal original required')
 raw_path=checked(review['mechanicalReport']);raw=read(raw_path);raw['visualReview']=review;require(raw==report,'Root approval must preserve every raw mechanical portal observation')
def public_portal():
 """Final closure gate; call only after the new audit and direct Root review."""
 path=OUT/'PublicPortal/report.json';report=read(path);manifest=read(STAGE/'build-manifest.json');tree_path=OUT/'PublicGitObjects/current-gh-pages.json';tree=read(tree_path)
 prior=sealed_g1_public();expected={**manifest['files'],'build-manifest.json':digest(STAGE/'build-manifest.json')};audited=OLD.file_rows(report.get('files',[]),'G2 complete public audit')
 require(manifest.get('releaseProfile')==PROFILE and manifest.get('publishReady') is True and manifest.get('sourceDirty') is False and manifest.get('preservedPublic')==prior,'Exact complete G2 manifest with sealed G1 predecessor required')
 require(report.get('status')=='passed' and report.get('baseUrl')==PUBLIC_BASE and report.get('sourceCommit')==manifest['sourceCommit'] and report.get('deployedCommit')==tree['commit'] and tree['commit']!=G1_DEPLOYED and report.get('manifest')==ref(STAGE/'build-manifest.json') and report.get('auditCounts')==COUNTS,'Exact new public source/runtime/tree/counts required')
 require(report.get('priorAudit')==prior['audit'] and report.get('priorManifest')==prior['manifest'] and report.get('priorGitTree')==prior['gitTree'] and report.get('currentGitTree')==ref(tree_path),'G2 public proof must pin exact old/new complete tree and audit')
 require(not STAGE.is_symlink() and not any(p.is_symlink()for p in STAGE.rglob('*')) and set(audited)==set(expected)==set(tree['files'])=={p.relative_to(STAGE).as_posix()for p in STAGE.rglob('*')if p.is_file() and p.name!='.firstgame-web-output'},'Exact complete regular G2 site/public/tree file set required')
 require(manifest.get('siteBudgetBytesIncludingManifest')==985000000 and sum((STAGE/n).stat().st_size for n in expected)<985000000,'Actual whole-site budget including manifest required')
 old_tree=read(OLD_TREE)
 for name,h in expected.items():
  p=STAGE/name;row=audited[name];url=urllib.parse.urljoin(PUBLIC_BASE,urllib.parse.quote(name,safe='/'));image=name.startswith(PREFIX) and name.endswith('.jpg')
  require(p.is_file() and not p.is_symlink() and digest(p)==h==row.get('sha256')==row.get('expectedSha256') and p.stat().st_size==row.get('bytes')==row.get('expectedBytes') and row.get('status')==200 and row.get('passed') is True and row.get('url')==row.get('resolvedUrl')==url and str(row.get('contentEncoding','identity')).lower()in('','identity'),'Actual complete HTTPS byte/URL/encoding proof differs: '+name)
  require(row.get('freshGet') is not image,'Historical image response must never become a fresh GET')
  if image:require(old_tree['files'][name]==tree['files'][name] and row.get('priorGitBlob')==row.get('currentGitBlob')==tree['files'][name]['gitBlob'],'Every unchanged JPEG must retain equal exact old/new Git blobs')
 require(sum(n.startswith(PREFIX) and n.endswith('.jpg')for n in expected)==15350 and len(expected)-15350==63,'Exact fresh63/inherited15350 full site required')
 captured=report.get('capturedSources',[]);require({v.get('file')for v in captured}=={'Tools/Autonomy/g2_public.py','Tools/Autonomy/g2_evidence.py','Tools/Validation/verify-public-portal.py','Tools/Web/release_contract.py'},'Complete actual G2 public harness copies required')
 for v in captured:require(digest(ROOT/v['file'])==digest(ROOT/v['preservedFile'])==v['sha256'],'Actual current public harness changed')
 _portal_originals(path,report,STAGE,manifest['sourceCommit'],tree['commit'])
 return {'report':ref(path),'manifest':ref(STAGE/'build-manifest.json'),'gitTree':ref(tree_path),'approvedOriginals':report['visualReview']['screenshots'],'priorPublic':prior,'scope':'Fresh63 actual HTTPS GETs and two fresh Root-reviewed portal originals;15350 identical JPEG responses remain dated prior evidence with equal old/new Git blobs. New Gospel public touch is a separate fresh browser report.'}
