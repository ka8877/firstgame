#!/usr/bin/env python3
"""UNEXECUTED C1 Card adapter; common driver and every old artifact stay intact.

G2 final pins intentionally remain null in c1-contract.template.json. Fill only
from the actual sealed G2 receipt/Root review before installing or executing.
"""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,importlib.util,json,re,shutil,sys,time,urllib.parse,urllib.request,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
BASE_URL='https://ka8877.github.io/firstgame/'
PREFIX='games/fantasy-cards/StreamingAssets/Cards/'
REVISION='fm-capability-2026-11-03'
def require(ok,message):
 if not ok:raise ValueError(message)
def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text())
def ref(p):
 p=Path(p);return {'file':str(p.relative_to(ROOT))if p.is_relative_to(ROOT)else str(p),'sha256':digest(p),'bytes':p.stat().st_size}
def checked(r):
 p=ROOT/r['file'];require(re.fullmatch('[0-9a-f]{64}',r.get('sha256')or'')and isinstance(r.get('bytes'),int)and p.is_file()and not p.is_symlink()and digest(p)==r['sha256']and p.stat().st_size==r['bytes'],'Required actual sealed regular-leaf reference is missing/changed: '+str(p));return p
def timestamp(s):
 t=datetime.fromisoformat(s.replace('Z','+00:00'));require(t.utcoffset()is not None,'Timezone-bound actual timestamp required');return t
def mapped(values):
 result={v['file']:v for v in values};require(len(result)==len(values),'Unique complete file rows required');return result
def stage_files(stage,manifest):
 require(stage.is_dir()and not stage.is_symlink()and not any(p.is_symlink()for p in stage.rglob('*')),'Complete regular stage required')
 expected={**manifest['files'],'build-manifest.json':digest(stage/'build-manifest.json')};actual={p.relative_to(stage).as_posix():p for p in stage.rglob('*')if p.is_file()and p.name!='.firstgame-web-output'}
 require(set(actual)==set(expected),'Exact full stage file set required')
 for name,h in expected.items():
  p=actual[name];require(p.resolve().is_relative_to(stage.resolve())and digest(p)==h,'Actual immutable staged bytes differ: '+name)
 require(manifest.get('siteBudgetBytesIncludingManifest')==985000000 and sum(p.stat().st_size for p in actual.values())<985000000,'Whole site including manifest must remain strictly below985MB')
 return expected,actual
def portal_originals(path,audit,stage):
 review=audit['visualReview'];manifest=read(stage/'build-manifest.json')
 require(review.get('status')=='approved'and review.get('reviewer')in('root','/root')and review.get('sourceCommit')==audit['sourceCommit']==manifest['sourceCommit']and review.get('deployedCommit')==audit['deployedCommit']and review.get('manifest')==audit['manifest']==ref(stage/'build-manifest.json'),'Current source/public/manifest-bound Root portal approval required')
 require(review.get('auditCounts')==audit.get('auditCounts')and review.get('fileCount')==len(audit['files'])and review.get('gameRevisions')==audit.get('gameRevisions')==manifest['gameRevisions'],'Current Root portal count/revisions required')
 require(timestamp(review['reviewedUtc'])>=timestamp(audit['checkedUtc'])>=timestamp(audit['startedUtc']),'Root portal review must follow the completed actual audit')
 def capture(s):return {k:s.get(k)for k in('file','sha256','captureKind','viewport')}
 originals=audit.get('screenshots',[]);approved=review.get('screenshots',[]);checks=audit.get('checks',[])
 require(len(originals)==len(approved)==len(checks)==2 and {s['file']:capture(s)for s in originals}=={s['file']:capture(s)for s in approved}=={v['screenshot']['file']:capture(v['screenshot'])for v in checks},'Both current actual portal originals/checks must be Root reviewed')
 require({(s['viewport']['width'],s['viewport']['height'])for s in originals}=={(1440,1050),(390,844)},'Exact portal viewports required')
 for shot in originals:
  p=ROOT/shot['file'];require(p.is_file()and not p.is_symlink()and digest(p)==shot['sha256']and p.resolve().parent==path.parent.resolve()and shot.get('captureKind')=='original-full-page','Exact current regular original portal leaf required')
 for v in checks:require(v.get('passed')is True and v.get('status')==200 and v.get('assertions')and all(x is True for x in v['assertions'].values())and v.get('viewport')==v['screenshot']['viewport']and v.get('profile')==('desktop'if v['viewport']['width']==1440 else'mobile-portrait'),'Both actual portal checks and all old conditions must pass')
 raw=read(checked(review['mechanicalReport']));raw['visualReview']=review;require(raw==audit,'Root attachment must preserve every raw mechanical portal observation')
def sealed_g2(c):
 pins=c['sealedG2'];paths={k:checked(pins[k])for k in('receipt','rootReview','manifest','publicAudit','gitTree')};receipt=read(paths['receipt']);review=read(paths['rootReview']);manifest=read(paths['manifest']);audit=read(paths['publicAudit']);tree=read(paths['gitTree']);stage=paths['manifest'].parent
 require(re.fullmatch('[0-9a-f]{40}',pins.get('deployedCommit')or'')and receipt.get('status')=='verified-published'and receipt.get('workId')=='G2'and receipt.get('sourceCommit')==pins['sourceCommit']and receipt.get('deployedCommit')==pins['deployedCommit']==tree['commit'],'Actual completed sealed G2 source/public identity required')
 require(review.get('status')=='approved'and review.get('reviewer')in('root','/root')and review.get('receipt')==pins['receipt']and review.get('manifest')==receipt.get('manifest')==pins['manifest']and review.get('sourceCommit')==pins['sourceCommit']and review.get('deployedCommit')==pins['deployedCommit'],'Actual separate Root seal must bind the exact G2 receipt')
 require(receipt.get('publicPortal')==pins['publicAudit']and receipt.get('publicGitTree')==pins['gitTree'],'Sealed G2 receipt must bind its actual full audit and deployed tree')
 require(manifest.get('releaseProfile')=='autonomy-g2'and manifest.get('publishReady')is True and manifest.get('sourceDirty')is False and manifest.get('currentTargets')==['Web']and manifest.get('gameRevisions')=={'FantasyCards':'fm-nativity-2026-11-02','PathOfDawn':c['gospelRevision']},'Exact clean complete prior G2 stage required')
 expected,actual=stage_files(stage,manifest);require(len(expected)==receipt['fileCountIncludingManifest']and sum(p.stat().st_size for p in actual.values())==receipt['siteBytesIncludingManifest']and set(tree['files'])==set(expected),'Complete sealed G2 site bytes/file set/tree required')
 require(audit.get('status')=='passed'and audit.get('sourceCommit')==pins['sourceCommit']and audit.get('deployedCommit')==pins['deployedCommit']and audit.get('manifest')==pins['manifest'],'Exact sealed actual G2 public audit required');portal_originals(paths['publicAudit'],audit,stage)
 # Only unchanged Gospel source/runtime is checked against current source. Old
 # G2 Card authored-source/native consumers must never run after C1 changes.
 target=receipt['currentBuild']['targets']['Web'];inputs=read(checked(target['inputs']));evidence=checked(target['evidence']);lines=dict(v.split(': ',1)for v in evidence.read_text().splitlines()if ': 'in v)
 sys.path.insert(0,str(ROOT/'Tools/Validation'));from restoration_source_binding import verify
 require(verify(inputs)==target['sourceFingerprint']==pins['gospelFingerprint']==lines.get('SourceFingerprint')and lines.get('Revision')==c['gospelRevision']and lines.get('Result')=='Succeeded'and lines.get('Errors')=='0'and lines.get('LegacyResourcesRestored')=='true','Unchanged current Gospel authored inputs and sealed successful build must remain exact')
 gospel=manifest['currentBrowserReports']['PathOfDawn'];require(len(gospel)==2,'Sealed G2 must retain actual local Gospel browser proof')
 for r in receipt['browserReports']:checked(r['report'])
 prior_browser=read(checked(receipt['browserReports'][0]['report']));runtime=prior_browser['buildFingerprint']['files'];gospel_root=Path(c['gospelBuildRoot'])/'PathOfDawn/Web';require(gospel_root.resolve()==evidence.parent.resolve(),'Current preserved Gospel runtime must use the exact sealed build root')
 require(len(runtime)==5 and len(mapped(runtime))==5,'Exact sealed G2 Gospel current5 runtime required')
 for row in runtime:
  p=gospel_root/row['file'];require(p.is_file()and not p.is_symlink()and digest(p)==row['sha256']==manifest['files']['games/path-of-dawn/'+row['file']]and p.stat().st_size==row['bytes'],'Current unchanged Gospel runtime must equal sealed G2 build/site bytes')
 return {'pins':pins,'receipt':receipt,'manifest':manifest,'audit':audit,'tree':tree,'stage':stage,'expected':expected}
def current_card_build(c):
 selected=c.get('nativeRunRoot');require(isinstance(selected,str)and selected,'Select one actual isolated exact8 C1 Native run before real Web input')
 run=ROOT/selected;out=ROOT/c['output'];require(run.resolve().is_relative_to((out/'NativeRuns').resolve())and not run.is_symlink(),'Explicit selected current C1 Native attempt required')
 summary=read(run/'c1-native-exact-contract.json');native=run/'Native/FantasyCards';binding=read(native/'binding.json');player=Path(c['cardBuildRoot'])/'FantasyCards/Web';inputs=read(player/'BuildInputs.json');e=dict(v.split(': ',1)for v in(player/'BuildEvidence.txt').read_text().splitlines()if ': 'in v)
 contract=read(ROOT/'Tools/Autonomy/c1-native-contract.json')
 require(digest(ROOT/'Tools/Autonomy/c1-native-contract.json')==c['nativeContractSha256'],'Exact frozen C1 Native contract required')
 require(summary.get('status')=='passed'and summary.get('expectedFullNames')==sorted(contract['expectedFullNames'])and len(summary['expectedFullNames'])==8 and binding.get('status')=='passed'and binding.get('sourceChangedDuringRun')is False and summary.get('binding')==ref(native/'binding.json')and summary.get('nativeXml')==ref(native/'native.xml'),'Selected actual exact8 source/XML/contract result required')
 xml=ET.parse(native/'native.xml').getroot();names=[v.get('fullname')for v in xml.findall('.//test-case')];require(xml.get('result')=='Passed'and xml.get('passed')=='8'and xml.get('failed')=='0'and xml.get('skipped')=='0'and len(names)==8 and set(names)==set(contract['expectedFullNames'])and binding.get('xmlSha256')==digest(native/'native.xml'),'Exact8 actual XML cases must pass without subsets, duplicates or skips')
 from restoration_source_binding import verify
 require(verify(read(native/'inputs-before.json'))==verify(inputs)==binding['sourceFingerprint']==e.get('SourceFingerprint')and e.get('Revision')==REVISION and e.get('Target')=='WebGL'and e.get('Content')=='fm-classic-0.1.0'and e.get('Result')=='Succeeded'and e.get('Errors')=='0','Fresh current C1 Native/build source fingerprints and confirmed Web revision must match')
 return {'nativeSummary':ref(run/'c1-native-exact-contract.json'),'binding':ref(native/'binding.json'),'inputs':ref(player/'BuildInputs.json'),'evidence':ref(player/'BuildEvidence.txt'),'sourceFingerprint':binding['sourceFingerprint']}
def config(path):
 c=read(path);require(c.get('revisionConfirmedByRoot')is True and c.get('revision')==REVISION and c.get('profile')=='autonomy-c1','Confirmed exact singular C1 revision/profile required')
 for name,h in c['sharedSourceSha256'].items():require(digest(ROOT/name)==h,'Frozen shared source changed: '+name)
 c['_sealed']=sealed_g2(c);c['_currentCard']=current_card_build(c);c['_contractFile']=str(path);return c
def current_public(c):
 out=ROOT/c['output'];stage=Path(c['currentStage']);path=out/'PublicPortal/report.json';tree_path=out/'PublicGitObjects/current-gh-pages.json';audit=read(path);tree=read(tree_path);manifest=read(stage/'build-manifest.json');old=c['_sealed'];pins=old['pins']
 revisions={'FantasyCards':REVISION,'PathOfDawn':c['gospelRevision']}
 require(manifest.get('releaseProfile')==c['profile']and manifest.get('gameRevisions')==revisions and manifest.get('contentVersions')=={'FantasyCards':'fm-classic-0.1.0','PathOfDawn':'gospel-0.1.0'}and manifest.get('currentTargets')==['Web']and manifest.get('publishReady')is True and manifest.get('sourceDirty')is False,'New source-bound clean C1 full stage with unchanged Gospel required')
 card=manifest['currentBuilds']['games']['FantasyCards'];gospel=manifest['currentBuilds']['games']['PathOfDawn']
 require(card.get('native')==c['_currentCard']['binding']and card['targets']['Web']['inputs']==c['_currentCard']['inputs']and card['targets']['Web']['evidence']==c['_currentCard']['evidence']and card['targets']['Web']['sourceFingerprint']==c['_currentCard']['sourceFingerprint'],'Current C1 complete audit must bind this exact selected Native/current Web source')
 require(gospel['targets']['Web']==old['receipt']['currentBuild']['targets']['Web'],'Current C1 site must preserve the exact sealed G2 Gospel build/input/source proof')
 require(re.fullmatch('[0-9a-f]{40}',manifest.get('sourceCommit',''))and audit.get('sourceCommit')==manifest['sourceCommit']and audit.get('gameRevisions')==revisions and audit.get('status')=='passed'and audit.get('baseUrl')==BASE_URL and audit.get('manifest')==ref(stage/'build-manifest.json')and audit.get('deployedCommit')==tree['commit']and tree['commit']!=pins['deployedCommit'],'New exact C1 source/manifest/actual deployed tree required')
 predecessor=manifest['preservedPublic']
 for key,pin in [('receipt','receipt'),('rootReview','rootReview'),('manifest','manifest'),('audit','publicAudit'),('gitTree','gitTree')]:require(predecessor.get(key)==pins[pin],'C1 stage must pin actual sealed G2 predecessor: '+key)
 require(audit.get('priorAudit')==pins['publicAudit']and audit.get('priorManifest')==pins['manifest']and audit.get('priorGitTree')==pins['gitTree']and audit.get('currentGitTree')==ref(tree_path),'New C1 audit must pin exact old/new full tree and G2 proof')
 expected,actual=stage_files(stage,manifest);rows=mapped(audit['files']);oldrows=mapped(old['audit']['files']);require(set(rows)==set(expected)==set(tree['files']),'Every current staged/public/Git path must be covered exactly')
 require(all(v.get('mode')=='100644'and re.fullmatch('[0-9a-f]{40}',v.get('gitBlob',''))for v in tree['files'].values()),'Every actual complete deployed entry must be an ordinary Git blob')
 images={n for n in expected if n.startswith(PREFIX)and n.endswith('.jpg')};counts={'freshHttpsGets':len(expected)-len(images),'inheritedImageIdentities':len(images)};require(counts=={'freshHttpsGets':63,'inheritedImageIdentities':15350}and audit.get('auditCounts')==counts,'Complete current63 fresh files and15350 historical image identities required')
 for name,h in expected.items():
  row=rows[name];url=urllib.parse.urljoin(BASE_URL,urllib.parse.quote(name,safe='/'));image=name in images
  require(row.get('passed')is True and row.get('status')==200 and row.get('url')==row.get('resolvedUrl')==url and row.get('sha256')==row.get('expectedSha256')==h and row.get('bytes')==row.get('expectedBytes')==actual[name].stat().st_size and str(row.get('contentEncoding','identity')).lower()in('','identity')and row.get('freshGet')is(not image),'Exact actual current public HTTP/body/identity proof differs: '+name)
  if image:
   previous=oldrows[name];require(h==old['manifest']['files'][name]==previous['sha256']and row['bytes']==previous['bytes']and old['tree']['files'][name]==tree['files'][name]and row.get('priorGitBlob')==row.get('currentGitBlob')==tree['files'][name]['gitBlob'],'Every JPEG must retain exact old/new source bytes and Git blobs')
   for key in('checkedUtc','receivedUtc','responseStartedUtc','responseCompletedUtc','inheritedGet'):require(row.get(key)==previous.get(key),'Historical response timestamps/flags must remain historical: '+name)
 for name,h in old['manifest']['files'].items():
  if not(name.startswith('games/fantasy-cards/')and'/ThirdParty/'not in name):require(manifest['files'].get(name)==h,'Unchanged G2 Gospel/site/ThirdParty byte differs: '+name)
 captured=audit.get('capturedSources',[]);require(captured,'Actual current C1 public audit sources must be preserved')
 for v in captured:require(digest(ROOT/v['file'])==digest(ROOT/v['preservedFile'])==v['sha256'],'Actual current C1 public audit producer changed')
 portal_originals(path,audit,stage)
 return {'reportPath':path,'report':audit,'stage':stage,'manifest':manifest,'rows':rows,'tree':tree}
def build_adapter(c):
 sys.path.insert(0,str(ROOT/'Tools/Validation'))
 spec=importlib.util.spec_from_file_location('c1_frozen_nativity_driver',ROOT/'Tools/Validation/nativity-web-smoke.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 m.QUEST_REVISIONS={'FantasyCards':REVISION,'PathOfDawn':c['gospelRevision']}
 m.CURRENT_GUARDED_REVISIONS=(*m.CURRENT_GUARDED_REVISIONS,*m.QUEST_REVISIONS.values())
 m.LOCAL_ERRORS.SUPPORTED_LOCAL_REVISIONS={*m.LOCAL_ERRORS.SUPPORTED_LOCAL_REVISIONS,REVISION}
 BASE=m.BASE;media_audio_summary=m.media_audio_summary
 class C1Smoke(m.JourneySmoke):
  def __init__(self,*args,**kwargs):
   super().__init__(*args,**kwargs);require(self.args.game=='FantasyCards','Only changed C1 Cards may execute')
   plan=c['plans'][self.args.profile];require(self.plan.get('revision')==REVISION and len(self.plan['steps'])==plan['steps']and len(self.plan['requiredCheckpoints'])==plan['required']and digest(self.args.plan)==plan['sha256']and self.args.plan.resolve()==(ROOT/plan['file']).resolve(),'Exact complete authored C1 PC/touch plan required')
   require([s['label']for s in self.plan['steps']if s['type']=='audio']==plan['pcmLabels']and sum(s['type']=='reload'for s in self.plan['steps'])==2,'All four unchanged PCM roles and both full reloads required')
   source=Path(__file__);target=self.output/'RecordedHarness/Tools/Autonomy/c1_web_smoke.py';target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
   contract=Path(c['_contractFile']);saved=self.output/'RecordedHarness/c1-contract.executed.json';shutil.copyfile(contract,saved)
   self.report['releaseProfile']=c['profile'];self.report['harness']['c1Wrapper']={'file':str(source.relative_to(ROOT)),'preservedFile':str(target.relative_to(ROOT)),'sha256':digest(source),'contract':ref(saved),'audioRevisionAdmission':'Same inherited audio method/predicates; C1 literal added to each of the three existing revision lists.','localMediaRevisionAdmission':'Existing supported-revision set extended only by exact C1 revision; every existing classifier predicate remains unchanged.'}
   self.report['preservedG2']={k:c['sealedG2'][k]for k in('receipt','rootReview','manifest','publicAudit','gitTree')};self.report['currentC1SourceBuild']=c['_currentCard'];self.persist()
  # Exact inherited PCM method; only three revision-list admissions differ.
  def audio(self, label):
      started_epoch_ms = time.time() * 1000
      # The software renderer can delay main-thread timer snapshots. Keep the
      # six-sample PCM predicate and observe longer instead of lowering it.
      # Opening narration needs a bounded PCM window shorter than the11.1s
      # first line so ordinary book input can prove interruption while playing.
      # The existing six-sample signal predicate remains unchanged.
      window_ms = 3500 if label.startswith('narration-') else (8000 if self.modern and self.args.game == "PathOfDawn" and label not in ("before-first-gesture", "muted") else 2200)
      snapshots = self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", window_ms)
      local_media = self.modern and self.args.game == "FantasyCards"
      minimum_duration = 30
      if local_media and self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03'):
          # The verified publisher excerpts are18.53/19.55s, not the earlier
          # invented60s loops. Their exact URLs, looping source and advancing
          # isolated PCM still have to pass the normal source checks below.
          manifest = json.loads((ROOT / 'Tools/Audio/FmFidelity/runtime-manifest.json').read_text())
          minimum_duration = min(track['seconds'] for track in manifest['tracks']) - .1
      summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
      acquisition_windows = [window_ms]
      # Keep every sample and every predicate. A busy software renderer can
      # produce fewer than six observations in eight seconds; extend passive
      # acquisition, bounded to 24 requested seconds, rather than infer sound.
      if not label.startswith('narration-') and self.modern and (self.args.game == "PathOfDawn" or self.report['revision'] in ('fm-fidelity-2026-10-27', 'fm-detail-2026-10-27', 'fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03')) and label not in ("before-first-gesture", "muted"):
          while summary["sampleCount"] < 6 and sum(acquisition_windows) < 24000:
              snapshots.extend(self.page.evaluate("ms => window.__firstGameAudioEvidence.sampleWindow(ms)", 4000))
              acquisition_windows.append(4000)
              summary = media_audio_summary(snapshots, minimum_duration) if local_media else BASE.music_audio_summary(snapshots)
      path = self.output / ("audio-" + str(self.navigation) + "-" + label + ".json")
      path.write_text(json.dumps({"label": label, "requestedWindowMs": sum(acquisition_windows),
          "acquisitionWindowsMs": acquisition_windows, "snapshots": snapshots, "summary": summary}, indent=2) + "\n")
      evidence = {k: v for k, v in summary.items() if k != "frames"}
      evidence.update(label=label, rawEvidence=path.name, sha256=digest(path), navigation=self.navigation,
                      startedEpochMs=started_epoch_ms, finishedEpochMs=time.time() * 1000)
      if local_media and self.report['revision'] in ('fm-arcana-2026-10-27', 'fm-restoration-2026-10-27', 'fm-polish-2026-10-29', 'fm-chronicle-2026-10-29', 'fm-nativity-2026-11-02', 'fm-capability-2026-11-03'):
          evidence["rolePlayback"] = self.page.evaluate("""() => ({
              state: window.FirstGameLocalMusic.getState(),
              players: window.FirstGameLocalMusic.getDiagnostics(),
              transitions: window.FirstGameLocalMusic.getTransitions(), observedAtMs:performance.now()
          })""")
      self.report["audioEvidence"]["windows"].append(evidence)
      self.check(not evidence["observerErrors"] and evidence["nonfiniteSamples"] == 0,
                 "Passive audio observer captured valid PCM: " + label, evidence)
      if label == "before-first-gesture":
          self.check(evidence["maximumRms"] < 1e-5, "No music output before first user gesture", evidence)
      elif label == "muted":
          self.check(evidence["maximumRms"] < 1e-5, "Actual mute control silences output", evidence)
      else:
          self.check(evidence["sampleCount"] >= 6 and evidence["allContextsRunning"]
                     and evidence["isolatedMusicRoute"] and len(evidence["sourceIdentities"]) == 1
                     and evidence["audibleSampleFraction"] >= .5 and evidence["medianRms"] > 1e-5,
                     "Actual looping BGM is audible after user input: " + label, evidence)
          if local_media:
              self.check(evidence["mediaProgressSeconds"] > .5 and evidence["sourceUrls"]
                         and all("/StreamingAssets/Audio/ModernCard/" in url for url in evidence["sourceUrls"]),
                         "Audible BGM comes from advancing bundled instrumental media: " + label, evidence)
  def public_bytes(self,url):
   current=current_public(c);audit=current['report'];stage=current['stage'];files=[];expected=list(self.report['buildFingerprint']['files']);images=[r for r in expected if r['file'].startswith('StreamingAssets/Cards/')]
   require(len(images)==15350 and url==BASE_URL+'games/fantasy-cards/','Exact current Card game URL and complete image archive required')
   request=urllib.request.Request(BASE_URL+'build-manifest.json',headers={'Accept-Encoding':'identity','Cache-Control':'no-cache'})
   with urllib.request.urlopen(request,timeout=self.args.timeout)as response:
    body=response.read();require(response.status==200 and response.url==BASE_URL+'build-manifest.json'and response.headers.get('Content-Encoding','identity').lower()in('identity','')and hashlib.sha256(body).hexdigest()==digest(stage/'build-manifest.json'),'Fresh live C1 manifest must exactly match the new Root-approved C1 audit, not Nativity/G2 manifest')
   for row in images:
    name='games/fantasy-cards/'+row['file'];seen=current['rows'][name];require(row['sha256']==seen['sha256']==current['manifest']['files'][name]and row['bytes']==seen['bytes'],'Actual unchanged image identity differs from current C1 audit')
    files.append({'file':row['file'],'url':seen['resolvedUrl'],'status':200,'bytes':seen['bytes'],'sha256':seen['sha256'],'verificationSource':'current-root-reviewed-public-byte-identity-audit','freshGet':False,'inheritedGet':seen.get('inheritedGet')})
   self.report['completePublicImageAudit']={'file':str(current['reportPath'].relative_to(ROOT)),'sha256':digest(current['reportPath']),'manifestSha256':digest(stage/'build-manifest.json'),'images':15350,'preservedG2':self.report['preservedG2'],'scope':'Exact new Root-reviewed C1 complete audit with sealed G2 predecessor. Every JPEG remains historical actual response evidence plus equal G2/C1 Git blobs. The live C1 manifest is freshly fetched; each current non-image Card runtime is fetched below.'}
   self.check(True,'All streamed card images match the current Root-reviewed public byte audit and unchanged live manifest',self.report['completePublicImageAudit'])
   for expected in self.report['buildFingerprint']['files']:
    if expected['file'].startswith('StreamingAssets/Cards/'):continue
    request=urllib.request.Request(urllib.parse.urljoin(url,urllib.parse.quote(expected['file'],safe='/')),headers={'Accept-Encoding':'identity','Cache-Control':'no-cache'});hasher=hashlib.sha256();size=0
    with urllib.request.urlopen(request,timeout=self.args.timeout)as response:
     require(response.headers.get('Content-Encoding','identity').lower()in('identity',''),'Cannot compare encoded public bytes')
     for block in iter(lambda:response.read(1024*1024),b''):hasher.update(block);size+=len(block)
     actual={'file':expected['file'],'url':response.url,'status':response.status,'bytes':size,'sha256':hasher.hexdigest()}
    files.append(actual);self.check(actual['status']==200 and actual['url']==urllib.parse.urljoin(url,urllib.parse.quote(expected['file'],safe='/'))and actual['bytes']==expected['bytes']and actual['sha256']==expected['sha256'],'Public runtime matches the final local build: '+expected['file'],actual)
   self.report['publicDelivery']={'status':'passed','files':files,'gameUrl':url}
  def run(self,url):
   result=super().run(url)
   if result.get('status')=='passed':
    try:
     require(all(v.get('passed')is True for v in result['checks'])and set(self.plan['requiredCheckpoints']).issubset({v['name']for v in result['checkpoints']}),'Every old/new C1 real-input check and required checkpoint must actually complete')
     require(any(v.get('name')=='Current runtime files did not change during the browser session'and v.get('passed')is True for v in result['checks']),'Final actual complete runtime gate must execute and pass')
     require([v['label']for v in result['audioEvidence']['windows']]==c['plans'][self.args.profile]['pcmLabels']and all(v.get('rolePlayback')for v in result['audioEvidence']['windows']),'All four actual PCM windows and advancing-source role diagnostics must remain present')
     caps=result['browserInputCapabilities'];width,height=(1280,720)if self.args.profile=='desktop'else(844,390)
     require(result['interactionViewport']=={'width':width,'height':height}and caps['width']==width and caps['height']==height and ((caps['maxTouchPoints']==0 and caps['coarse']is False)if self.args.profile=='desktop'else(caps['maxTouchPoints']>0 and caps['coarse']is True)),'Exact actual PC/touch viewport/capabilities required')
     cp={v['name']:v for v in result['checkpoints']};require(cp['continued-sealed-pack']['navigation']==1 and cp['continued-quest-battle']['navigation']==2,'Both preserved independent full browser reloads must complete')
     result['c1Scope']={'authoredSteps':len(self.plan['steps']),'requiredCheckpoints':len(self.plan['requiredCheckpoints']),'pcmWindows':4,'completeReloads':2,'semanticReview':'Pending direct Root review of actual original source IDs, descriptions, filters, membership and return; checkpoint.expected is not an automatic text assertion.'}
    except Exception as error:result.update(status='failed',failure=str(error))
    self.persist()
   return result
 m.JourneySmoke=C1Smoke;return m
def main():
 contract=ROOT/'Tools/Autonomy/c1-contract.json'
 if '--c1-contract'in sys.argv:
  index=sys.argv.index('--c1-contract');contract=Path(sys.argv[index+1]);del sys.argv[index:index+2]
 c=config(contract)
 def argument(name):return sys.argv[sys.argv.index(name)+1]if name in sys.argv else None
 require(argument('--game')=='FantasyCards'and argument('--release-profile')=='nativity','Explicit Card-only inherited nativity CLI profile required; adapter records autonomy-c1')
 require(argument('--plan')and argument('--output')and argument('--build-root')and Path(argument('--build-root')).resolve()==Path(c['cardBuildRoot']).resolve(),'Explicit current C1 plan/output/new Card build root required')
 out=ROOT/c['output'];require(out.parent==ROOT/'Artifacts/Validation/Autonomy'and out.name.startswith('C1-'),'Dedicated C1 output only')
 output=Path(argument('--output'));require(output.resolve().is_relative_to(out.resolve())and not output.is_symlink()and not(output/'FantasyCards').exists(),'New absent C1 game output only; never overwrite Nativity/G2 or an earlier C1 attempt')
 return build_adapter(c).main()
if __name__=='__main__':raise SystemExit(main())
