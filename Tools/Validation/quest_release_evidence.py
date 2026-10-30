"""Fresh source/build/native/browser evidence for the two-game Quest release."""
from datetime import datetime
from pathlib import Path, PurePosixPath
import hashlib, importlib.util, json, re, subprocess, sys, urllib.parse, xml.etree.ElementTree as ET
from restoration_source_binding import ROOT, verify, digest
from quest_storage import validate_evidence_root
REVISIONS={'FantasyCards':'fm-quest-2026-10-30','PathOfDawn':'gospel-journey-2026-10-30'}
CURRENT_TARGETS={'Web':'WebGL'}
OUT=ROOT/'Artifacts/Validation/Quest'
BUILD=Path('/tmp/firstgame-quest-builds')
STAGE=ROOT/'Artifacts/QuestWork/staged-site'
PUBLIC_BASE='https://ka8877.github.io/firstgame/'
GAME_SLUGS={'FantasyCards':'fantasy-cards','PathOfDawn':'path-of-dawn'}

def require(ok,message):
 if not ok: raise ValueError(message)
def read(path): return json.loads(Path(path).read_text())
def ref(path):
 p=Path(path);return {'file':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),'sha256':digest(p),'bytes':p.stat().st_size}
def timestamp(value,label):
 require(isinstance(value,str) and value,label+' timestamp required')
 try: parsed=datetime.fromisoformat(value.replace('Z','+00:00'))
 except ValueError: raise ValueError('Invalid '+label+' timestamp') from None
 require(parsed.utcoffset() is not None,label+' timestamp must include its timezone')
 return parsed
def file_rows(rows,label):
 require(isinstance(rows,list) and all(isinstance(row,dict) and isinstance(row.get('file'),str) for row in rows),label+' file records required')
 mapped={row['file']:row for row in rows}
 require(len(mapped)==len(rows),label+' contains duplicate paths')
 return mapped
def verify_portal_review(report=None):
 """Bind the persisted Root-reviewed portal audit to every current staged byte."""
 path=OUT/'PublicPortal/report.json';current=read(path)
 require(report is None or report==current,'Portal evidence must be the current persisted report')
 report=current;review=report.get('visualReview',{})
 require(report.get('status')=='passed' and report.get('baseUrl')==PUBLIC_BASE,'Successful verification of the existing public portal required')
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root'),'Explicit current portal Root review required')
 require(timestamp(review.get('reviewedUtc'),'Root portal review')>=timestamp(report.get('checkedUtc'),'Portal completion')>=timestamp(report.get('startedUtc'),'Portal start'),'Root portal review must follow the completed current audit')
 manifest_path=STAGE/'build-manifest.json'
 require(STAGE.is_dir() and not STAGE.is_symlink() and manifest_path.is_file() and not manifest_path.is_symlink(),'Current regular staged manifest required')
 manifest=read(manifest_path);manifest_ref=ref(manifest_path)
 require(manifest.get('releaseProfile')=='quest' and manifest.get('currentTargets')==list(CURRENT_TARGETS) and manifest.get('publishReady') is True and manifest.get('sourceDirty') is False and manifest.get('gameRevisions')==REVISIONS and manifest.get('contentVersions')=={'FantasyCards':'fm-classic-0.1.0','PathOfDawn':'gospel-0.1.0'},'Current clean Web-only Quest manifest required')
 require(re.fullmatch(r'[0-9a-f]{40}',manifest.get('sourceCommit','')) and report.get('sourceCommit')==manifest['sourceCommit'] and report.get('gameRevisions')==REVISIONS,'Portal source/revisions differ from current stage')
 require(report.get('manifest',{}).get('file')=='Artifacts/QuestWork/staged-site/build-manifest.json' and report['manifest'].get('sha256')==manifest_ref['sha256'],'Portal manifest differs from current stage')
 files=manifest.get('files',{})
 require(isinstance(files,dict) and 'index.html' in files and 'site.css' in files and 'build-manifest.json' not in files,'Complete staged public file map required')
 from quest_public_proof import validate_delta
 validate_delta(report,STAGE)
 expected={**files,'build-manifest.json':manifest_ref['sha256']};audited=file_rows(report.get('files',[]),'Public audit')
 require(set(audited)==set(expected),'Public audit must cover the exact complete staged file set')
 for name,sha in expected.items():
  require(isinstance(name,str) and name and '\\' not in name and not PurePosixPath(name).is_absolute() and '..' not in PurePosixPath(name).parts and str(PurePosixPath(name))==name,'Invalid staged public file path: '+str(name))
  local=STAGE/name;row=audited[name];url=urllib.parse.urljoin(PUBLIC_BASE,urllib.parse.quote(name,safe='/'))
  require(local.resolve().is_relative_to(STAGE) and local.is_file() and not local.is_symlink() and digest(local)==sha,'Current staged bytes differ: '+name)
  require(row.get('passed') is True and row.get('status')==200 and row.get('url')==url and row.get('resolvedUrl')==url and row.get('sha256')==sha and row.get('expectedSha256')==sha and row.get('bytes')==local.stat().st_size and row.get('expectedBytes')==row['bytes'] and str(row.get('contentEncoding','identity')).lower() in ('','identity'),'Complete public GET proof differs: '+name)
 viewports={1440:{'width':1440,'height':1050},390:{'width':390,'height':844}}
 def captures(rows,label):
  require(isinstance(rows,list) and len(rows)==2,label+' must contain both portal originals')
  result={}
  for shot in rows:
   viewport=shot.get('viewport',{});width=viewport.get('width');name=shot.get('file',shot.get('path',''))
   require(width in viewports and width not in result and viewport==viewports[width] and shot.get('captureKind')=='original-full-page' and name==str((path.parent/f'landing-{width}.png').relative_to(ROOT)),label+' must identify exact desktop/mobile full-page originals')
   original=ROOT/name
   require(original.resolve().parent==path.parent.resolve() and original.is_file() and not original.is_symlink() and digest(original)==shot.get('sha256'),label+' original changed: '+name)
   result[width]={'file':name,'sha256':shot['sha256'],'captureKind':shot['captureKind'],'viewport':viewport}
  return result
 originals=captures(report.get('screenshots',[]),'Portal captures');approved=captures(review.get('screenshots',[]),'Root portal review')
 require(approved==originals,'Root portal review differs from the current report captures')
 checks=report.get('checks',[])
 require(isinstance(checks,list) and len(checks)==2 and all(check.get('passed') is True and check.get('status')==200 and check.get('assertions') and all(value is True for value in check['assertions'].values()) for check in checks),'Both current portal viewport checks must pass')
 require(captures([check.get('screenshot',{}) for check in checks],'Portal check captures')==originals and all(check.get('viewport')==check['screenshot']['viewport'] and check.get('profile')==('desktop' if check['viewport']['width']==1440 else 'mobile-portrait') for check in checks),'Portal checks must bind both reviewed original viewports')
 return {'report':ref(path),'manifest':manifest_ref,'baseUrl':PUBLIC_BASE,'verifiedFiles':len(audited),'approvedOriginals':list(approved.values()),'reviewedUtc':review['reviewedUtc']}
def runtime(player):
 rows={}
 for p in sorted(player.rglob('*')):
  if not p.is_file():continue
  rel=p.relative_to(player)
  if rel.as_posix()!='index.html' and rel.parts[0] not in ('Build','StreamingAssets','TemplateData'):continue
  if any('DoNotShip' in s or 'DontShip' in s for s in rel.parts) or p.suffix in ('.map','.pdb','.mdb','.log') or '.symbols.' in p.name:continue
  require(not p.is_symlink(),'Linked runtime file: '+str(p))
  rows[rel.as_posix()]=p
 require('index.html' in rows and len(rows)>=5,'Complete runtime missing')
 return rows
def verify_native_review(game,binding,native):
 name='gospel-native-review.json' if game=='PathOfDawn' else 'cards-native-review.json'
 path=OUT/'Root'/name;review=read(path)
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc'),'Explicit current native Root review required: '+game)
 require(review.get('sourceFingerprint')==binding['sourceFingerprint'] and review.get('nativeXml',{}).get('sha256')==digest(native/'native.xml'),'Root native review source/results differ: '+game)
 shots=review.get('screenshots',[]);require(len(shots)>=2,'Root native review needs actual title/gameplay originals')
 for shot in shots:
  p=ROOT/shot.get('file',shot.get('path',''))
  require(p.resolve().is_relative_to((OUT/'Screenshots'/game).resolve()) and p.is_file() and not p.is_symlink() and digest(p)==shot['sha256'],'Root native original changed: '+game)
 return ref(path)
def verify_builds():
 validate_evidence_root()
 subprocess.run([sys.executable,str(ROOT/'Tools/Data/Renewal/verify-media.py')],cwd=ROOT,check=True)
 authoring_path=OUT/'card-authoring-final.json'
 authoring=json.loads(subprocess.check_output([sys.executable,str(ROOT/'Tools/Validation/verify-card-authoring.py')],cwd=ROOT,text=True))
 require(authoring.get('status')=='passed' and authoring.get('completeDefinitions')==read(ROOT/'Tools/Data/ClassicCards/selection.json')['expectedCount'] and authoring.get('streamedOriginals')==read(ROOT/'Tools/Data/Renewal/supported-records.json')['count'] and read(authoring_path)==authoring,'Current complete generated CardAsset semantics differ from final authoring verification')
 result={}
 for game,revision in REVISIONS.items():
  native=OUT/'Native'/game
  binding=read(native/'binding.json');inputs=read(native/'inputs-before.json')
  require(binding['status']=='passed' and binding['sourceChangedDuringRun'] is False and verify(inputs)==binding['sourceFingerprint'],'Fresh unchanged native inputs required: '+game)
  xml=ET.parse(native/'native.xml').getroot()
  require(xml.get('result')=='Passed' and int(xml.get('passed','0'))>0 and xml.get('skipped')=='0' and xml.get('failed')=='0','Fresh native tests must pass without skip')
  targets={}
  for target,platform in CURRENT_TARGETS.items():
   folder=BUILD/game/target
   ep,ip=folder/'BuildEvidence.txt',folder/'BuildInputs.json'
   e=dict(l.split(': ',1) for l in ep.read_text().splitlines() if ': ' in l);i=read(ip)
   require(e.get('Revision')==revision and e.get('Result')=='Succeeded' and e.get('Target')==platform and e.get('Errors')=='0','Current successful player missing: '+game+'/'+target)
   require(i['game']==game and verify(i)==e.get('SourceFingerprint')==binding['sourceFingerprint'],'Native/current/player source inputs differ: '+game+'/'+target)
   if game=='PathOfDawn':require(e.get('LegacyResourcesRestored')=='true','Gospel legacy originals must be restored')
   targets[target]={'evidence':ref(ep),'inputs':ref(ip),'sourceFingerprint':i['fingerprint']}
  result[game]={'native':ref(native/'binding.json'),'nativeResults':ref(native/'native.xml'),'nativeRootReview':verify_native_review(game,binding,native),'targets':targets}
 core=OUT/'Core/core-tests.trx';t=ET.parse(core).getroot();c=t.find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
 require(c is not None and int(c.get('passed','0'))>=170 and c.get('failed')=='0' and c.get('notExecuted')=='0','Current Core results must pass without skipped tests')
 binding=read(OUT/'Core/binding.json')
 require(binding.get('status')=='passed' and binding.get('sourceChangedDuringRun') is False and binding['results']['sha256']==digest(core),'Current immutable Core result binding required')
 spec=importlib.util.spec_from_file_location('renewal_core_inputs',ROOT/'Tools/Validation/run-quest-core.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
 require(module.verify_current(binding),'Core source changed since tested inputs')
 return {'currentTargets':list(CURRENT_TARGETS),'games':result,'core':ref(core),'authoring':ref(authoring_path)}
def verify_browser(game,profile):
 p=OUT/profile/game/'report.json';r=read(p)
 require(r.get('game')==game and r.get('revision')==REVISIONS[game] and r.get('status')=='passed' and r.get('checks') and all(c.get('passed') is True for c in r['checks']),'Fresh successful actual browser report required: '+game+'/'+profile)
 profiles={'Desktop720':('desktop',1280,720),'Mobile844':('mobile-landscape',844,390),'PublicMobile844':('mobile-landscape',844,390)}
 require(profile in profiles,'Unknown current browser profile: '+profile)
 mode,width,height=profiles[profile];viewport={'width':width,'height':height};capabilities=r.get('browserInputCapabilities',{})
 require(r.get('profile')==mode and r.get('interactionViewport')==viewport and all(capabilities.get(key)==value for key,value in viewport.items()),'Actual browser profile/viewport differs: '+game+'/'+profile)
 require((capabilities.get('maxTouchPoints',0)>0 and capabilities.get('coarse') is True) if mode=='mobile-landscape' else (capabilities.get('maxTouchPoints')==0 and capabilities.get('coarse') is False),'Actual desktop/touch browser context differs: '+game+'/'+profile)
 expected={k:{'file':k,'bytes':v.stat().st_size,'sha256':digest(v)} for k,v in runtime(BUILD/game/'Web').items()}
 rows=r.get('buildFingerprint',{}).get('files',[])
 def identity(row):return {k:row.get(k) for k in ('file','bytes','sha256')}
 require(len(rows)==len(expected) and {v['file'] for v in rows}==set(expected) and all(identity(v)==expected[v['file']] for v in rows),'Complete current runtime binding required')
 if profile=='PublicMobile844':
  delivery=r.get('publicDelivery',{});url=urllib.parse.urljoin(PUBLIC_BASE,'games/'+GAME_SLUGS[game]+'/')
  require(delivery.get('status')=='passed' and delivery.get('gameUrl')==url,'Actual current HTTPS game delivery required: '+game)
  delivered=file_rows(delivery.get('files',[]),'Public game delivery')
  require(set(delivered)==set(expected),'Public game delivery must cover the complete current runtime: '+game)
  urls={name:urllib.parse.urljoin(url,urllib.parse.quote(name,safe='/')) for name in expected}
  for name,row in delivered.items():
   require(identity(row)==expected[name] and row.get('status')==200 and row.get('url')==urls[name],'Public runtime bytes/URL differ: '+game+'/'+name)
  configurations=r.get('unityConfigurations',[]);runtime_urls=set(urls.values())
  require(configurations and all(all(configuration.get(key) in runtime_urls for key in ('dataUrl','frameworkUrl','codeUrl')) for configuration in configurations),'Actual public Unity loader must use the verified HTTPS runtime: '+game)
  reused=[row for row in delivered.values() if row.get('verificationSource')=='current-root-reviewed-public-byte-identity-audit']
  audit=r.get('completePublicImageAudit')
  require(not reused or isinstance(audit,dict),'Reused public image proof must reference its complete current audit')
  if audit is not None:
   portal=verify_portal_review();images={name for name in expected if name.startswith('StreamingAssets/Cards/')}
   require(audit.get('file')==portal['report']['file'] and audit.get('sha256')==portal['report']['sha256'] and audit.get('manifestSha256')==portal['manifest']['sha256'] and audit.get('images')==len(images) and {row['file'] for row in reused}==images,'Public image reuse must bind the exact current Root-reviewed complete portal audit')
 else:
  require(r.get('completePublicImageAudit') is None,'Local browser evidence must not reuse a public image audit')
 review=r.get('visualReview',{})
 require(review.get('status')=='approved' and review.get('reviewer') in ('root','/root') and review.get('reviewedUtc'),'Explicit current Root review required: '+game+'/'+profile)
 reviewed=review.get('runtimeFiles',[])
 require(len(reviewed)==len(rows) and {v['file'] for v in reviewed}==set(expected) and all(identity(v)==expected[v['file']] for v in reviewed),'Root review must bind complete runtime')
 originals=r.get('screenshots',[]);approved=review.get('screenshots',[])
 require(len(approved)>=2,'At least title and gameplay originals must be reviewed')
 def path(s):return ROOT/s.get('path',s.get('file'))
 original_keys={(path(s),s['sha256']) for s in originals}
 for shot in approved:
  f=path(shot);require(f.resolve().is_relative_to(p.parent.resolve()) and f.is_file() and not f.is_symlink() and digest(f)==shot['sha256'] and (f,shot['sha256']) in original_keys,'Review must name exact unchanged original')
 return {'game':game,'profile':profile,'report':ref(p),'approvedOriginals':[{'file':str(path(s).relative_to(ROOT)),'sha256':s['sha256']} for s in approved]}
