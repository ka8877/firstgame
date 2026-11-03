"""Strict source/runtime/original-frame gates for the changed Gospel and retained Cards."""
from datetime import datetime,timezone
from pathlib import Path
import hashlib,importlib.util,json,sys,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Validation/Autonomy/G1-2026-11-03'
BUILD=Path('/tmp/firstgame-g1-builds')
STAGE=Path('/tmp/firstgame-g1-staged-site')
PRIOR=Path('/tmp/firstgame-nativity-staged-site')
REVISION='gospel-encounter-2026-11-03'
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify,digest
import nativity_release_evidence as OLD
NEW_CASES={'IllustratedBirthStillRetainsItsVoicedLineAcrossTheBible','FieldConversationPreservesItsPhysicalEncounterAndSaveCursorAcrossBookAndReload','EncounterHudDirectionUsesTheActualCameraAndKeepsInteractionState'}
CHECKPOINTS={'title','g1-field-dialogue','g1-guidance','g1-saved','g1-continued'}
def require(ok,message):
 if not ok:raise ValueError(message)
def read(p):return json.loads(Path(p).read_text())
def ref(p):
 p=Path(p);return {'file':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),'sha256':digest(p),'bytes':p.stat().st_size}
def identity(row):return {k:row.get(k) for k in ('file','bytes','sha256')}
def rows(folder):return [{'file':name,'bytes':p.stat().st_size,'sha256':digest(p)} for name,p in OLD.runtime(folder).items()]
def current_build():
 native=OUT/'Native/PathOfDawn';binding=read(native/'binding.json');inputs=read(native/'inputs-before.json')
 require(binding['status']=='passed' and binding['sourceChangedDuringRun'] is False and verify(inputs)==binding['sourceFingerprint'],'Native must match all current Gospel inputs')
 require(binding.get('inputManifestSha256')==digest(native/'inputs-before.json') and binding.get('xmlSha256')==digest(native/'native.xml'),'Actual Native input manifest/results identity must remain exact')
 extra=binding.get('extraFixtureInputs',[])
 require(len(extra)==1 and extra[0].get('file')=='Tools/Voice/Nativity/production.json','Retained actual voice-duration fixture input must remain bound')
 recording=extra[0];source=ROOT/recording['file'];preserved=ROOT/recording['preservedFile']
 require(source.is_file() and preserved.is_file() and digest(source)==digest(preserved)==recording['sha256'] and source.stat().st_size==preserved.stat().st_size==recording['bytes'],'Current/imported Native voice-duration production input changed')
 xml=ET.parse(native/'native.xml').getroot();names=[c.get('name') for c in xml.findall('.//test-case')]
 expected=set(OLD.REQUIRED_NATIVE_TESTS['PathOfDawn'])|NEW_CASES
 require(xml.get('result')=='Passed' and xml.get('failed')=='0' and xml.get('skipped')=='0' and int(xml.get('passed','0'))==len(expected) and len(names)==len(expected) and set(names)==expected,'All retained10 and new3 Native cases must pass; targeted subsets cannot replace them')
 review_path=OUT/'Root/gospel-native-review.json';review=read(review_path)
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc') and review.get('sourceFingerprint')==binding['sourceFingerprint'] and review.get('nativeXml',{}).get('sha256')==digest(native/'native.xml'),'Explicit current native Root source/result review required')
 require(len(review.get('screenshots',[]))>=2,'Native Root must view current original captures')
 for shot in review['screenshots']:
  path=ROOT/shot.get('file',shot.get('path',''));require(path.is_relative_to(OUT/'Screenshots') and path.is_file() and not path.is_symlink() and digest(path)==shot['sha256'],'Native reviewed original changed')
 player=BUILD/'PathOfDawn/Web';evidence=player/'BuildEvidence.txt';e=dict(line.split(': ',1) for line in evidence.read_text().splitlines() if ': ' in line);build_inputs=read(player/'BuildInputs.json')
 require(e.get('Revision')==REVISION and e.get('Content')=='gospel-0.1.0' and e.get('Target')=='WebGL' and e.get('Result')=='Succeeded' and e.get('Errors')=='0' and e.get('LegacyResourcesRestored')=='true','Successful current original Web with restored legacy assets required')
 require(verify(build_inputs)==e.get('SourceFingerprint')==binding['sourceFingerprint'],'Current/native/build fingerprints must be identical')
 core=read(OUT/'Core/binding.json');trx=OUT/'Core/core-tests.trx';counter=ET.parse(trx).getroot().find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
 spec=importlib.util.spec_from_file_location('g1_core_source',ROOT/'Tools/Validation/run-nativity-core.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 require(core['status']=='passed' and core['sourceChangedDuringRun'] is False and m.verify_current(core) and core['results']['sha256']==digest(trx) and counter.get('failed')=='0' and counter.get('notExecuted')=='0' and int(counter.get('passed','0'))>=285,'Current complete285+ Core source/results required')
 return {'native':ref(native/'binding.json'),'nativeResults':ref(native/'native.xml'),'nativeRootReview':ref(review_path),'targets':{'Web':{'evidence':ref(evidence),'inputs':ref(player/'BuildInputs.json'),'sourceFingerprint':binding['sourceFingerprint']}},'core':ref(trx),'audioAssets':OLD.verify_audio_assets()}
def browser(profile):
 p=OUT/profile/'PathOfDawn/report.json';r=read(p);mode,width,height={'Desktop720':('desktop',1280,720),'Mobile844':('mobile-landscape',844,390),'PublicMobile844':('mobile-landscape',844,390)}[profile]
 require(r.get('game')=='PathOfDawn' and r.get('revision')==REVISION and r.get('releaseProfile')=='autonomy-g1' and r.get('suite')=='modern-real-input-v1' and r.get('status')=='passed' and r.get('checks') and all(c.get('passed') is True for c in r['checks']),'Every fresh G1 real-input check must pass')
 caps=r.get('browserInputCapabilities',{});require(r.get('profile')==mode and r.get('interactionViewport')=={'width':width,'height':height} and caps.get('width')==width and caps.get('height')==height,'Exact actual interaction viewport required')
 require((caps.get('maxTouchPoints',0)>0 and caps.get('coarse') is True) if mode=='mobile-landscape' else (caps.get('maxTouchPoints')==0 and caps.get('coarse') is False),'Actual desktop/touch context required')
 checkpoints={c.get('name'):c for c in r.get('checkpoints',[])};require(CHECKPOINTS.issubset(checkpoints),'G1 field/dialogue/guidance/save/reconnect checkpoint set required')
 require(any(c.get('navigation',0)>0 for c in r.get('unityConfigurations',[])) and checkpoints['g1-continued'].get('navigation',0)>0,'Actual full browser reconnect must precede continued capture')
 current=rows(BUILD/'PathOfDawn/Web');require([identity(x)for x in r['buildFingerprint']['files']]==[identity(x)for x in current],'Every current runtime byte must match the actual browser')
 from nativity_narration_evidence import narration_evidence
 require(r.get('narrationEvidence')==narration_evidence(r.get('console',[])),'Retained actual narration source start/book stop/next key required')
 wrapper=r.get('harness',{}).get('g1Wrapper',{});require(wrapper.get('file')=='Tools/Autonomy/g1_web_smoke.py' and digest(ROOT/wrapper['file'])==digest(ROOT/wrapper['preservedFile'])==wrapper.get('sha256'),'Exact executed G1 revision adapter must be preserved')
 review=r.get('visualReview',{});require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc'),'Fresh direct Root review required')
 require([identity(x)for x in review.get('runtimeFiles',[])]==[identity(x)for x in current],'Root review must bind complete current runtime')
 originals=r.get('screenshots',[]);approved=review.get('screenshots',[]);approved_names={s.get('name') for s in approved}
 require({'title','g1-field-dialogue','g1-guidance','g1-continued'}.issubset(approved_names),'Root must view title, changed dialogue/guidance and continued originals')
 for shot in approved:
  original=ROOT/shot.get('file',shot.get('path',''));require(original.resolve().is_relative_to(p.parent.resolve()) and original.is_file() and not original.is_symlink() and digest(original)==shot['sha256'] and any(s.get('sha256')==shot['sha256'] and s.get('path',s.get('file'))==shot.get('path',shot.get('file')) for s in originals),'Review must name exact current unchanged original capture')
 if profile=='PublicMobile844':
  delivery=r.get('publicDelivery',{});require(delivery.get('status')=='passed' and delivery.get('gameUrl')=='https://ka8877.github.io/firstgame/games/path-of-dawn/' and [identity(x)for x in delivery.get('files',[])]==[identity(x)for x in current],'Every current public runtime must have actual HTTPS delivery proof')
 return {'game':'PathOfDawn','profile':profile,'report':ref(p),'approvedOriginals':approved,'scope':'Fresh changed-game actual input and exact-source runtime; physical device and human audio approval are separate.'}
def preserved_cards():
 native=ROOT/'Artifacts/Validation/Nativity/Native/FantasyCards';binding=read(native/'binding.json')
 require(binding['status']=='passed' and verify(read(native/'inputs-before.json'))==binding['sourceFingerprint'],'Retained Cards authored source changed; cannot reuse')
 OLD.verify_native_review('FantasyCards',binding,native)
 player=Path('/tmp/firstgame-nativity-builds/FantasyCards/Web');e=dict(line.split(': ',1)for line in (player/'BuildEvidence.txt').read_text().splitlines()if ': 'in line)
 require(e.get('Revision')=='fm-nativity-2026-11-02' and e.get('SourceFingerprint')==verify(read(player/'BuildInputs.json'))==binding['sourceFingerprint'],'Retained Card player/source binding differs')
 reports=[OLD.verify_browser('FantasyCards',p)for p in ('Desktop720','Mobile844')]
 portal=OLD.verify_portal_review();old=read(PRIOR/'build-manifest.json')
 require(old['releaseProfile']=='nativity' and old['publishReady'] is True and old['sourceDirty'] is False and digest(PRIOR/'build-manifest.json')==portal['manifest']['sha256'],'Exact previously published Root-reviewed complete site required')
 for name,h in old['files'].items():
  p=PRIOR/name;require(p.is_file() and not p.is_symlink() and digest(p)==h,'Preserved site bytes changed: '+name)
 return {'preserved':True,'revision':'fm-nativity-2026-11-02','sourceFingerprint':binding['sourceFingerprint'],'native':ref(native/'binding.json'),'targets':{'Web':{'evidence':ref(player/'BuildEvidence.txt'),'inputs':ref(player/'BuildInputs.json'),'sourceFingerprint':binding['sourceFingerprint']}},'browserReports':reports,'priorPublicProof':portal,'scope':'Actual prior Cards results and byte-identical last successful runtime, explicitly retained; not a new Cards execution.'}
