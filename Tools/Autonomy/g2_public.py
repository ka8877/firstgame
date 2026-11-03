#!/usr/bin/env python3
"""Fresh G2 HTTPS bodies and retained15350 historical JPEG identities.

Old JPEG response timestamps remain old. Actual new public game input is a
separate g2_web_smoke.py run. After direct Root review, closure must consume
g2_evidence.public_portal() and browser('PublicMobile844').
"""
from concurrent.futures import ThreadPoolExecutor,as_completed
from datetime import datetime,timezone
from pathlib import Path
import argparse,copy,importlib.util,json,shutil,subprocess,sys
from g2_evidence import ROOT,OUT,STAGE,PRIOR,PROFILE,OLD_AUDIT,OLD_TREE,G1_DEPLOYED,require,read,ref,digest,sealed_g1_public
sys.path.insert(0,str(ROOT/'Tools/Validation'));sys.path.insert(0,str(ROOT/'Tools/Web'))
spec=importlib.util.spec_from_file_location('g2_portal_driver',ROOT/'Tools/Validation/verify-public-portal.py');PORTAL=importlib.util.module_from_spec(spec);spec.loader.exec_module(PORTAL)
PREFIX='games/fantasy-cards/StreamingAssets/Cards/'
URL='https://ka8877.github.io/firstgame/'
def now():return datetime.now(timezone.utc).isoformat()
def current_tree():
 head=json.loads(subprocess.check_output(['gh','api','repos/ka8877/firstgame/git/ref/heads/gh-pages'],cwd=ROOT,text=True))['object']['sha']
 data=json.loads(subprocess.check_output(['gh','api','repos/ka8877/firstgame/git/trees/'+head+'?recursive=1'],cwd=ROOT,text=True))
 require(data.get('truncated') is False,'Full current deployed Git tree required')
 files={}
 for row in data['tree']:
  if row['type']=='tree':continue
  require(row['type']=='blob' and row['mode']=='100644','Unexpected private/link Git entry')
  require(row['path'] not in files,'Duplicate remote Git path');files[row['path']]={'gitBlob':row['sha'],'mode':row['mode']}
 result={'kind':'actual-remote-gh-pages-git-tree','repository':'https://github.com/ka8877/firstgame.git','branch':'gh-pages','commit':head,'files':files}
 target=OUT/'PublicGitObjects/current-gh-pages.json';target.parent.mkdir(parents=True,exist_ok=True);require(not target.exists(),'Keep prior actual Git proof');target.write_text(json.dumps(result,indent=2)+'\n');return result
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--timeout',type=int,default=180);p.add_argument('--workers',type=int,default=4);p.add_argument('--renderer',choices=('swiftshader','x11-mesa'),default='swiftshader');a=p.parse_args()
 require(1<=a.workers<=4 and 1<=a.timeout<=180,'Bounded worker/time settings required')
 subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],cwd=ROOT,check=True)
 output=OUT/'PublicPortal';require(not output.exists(),'Keep existing public attempt');output.mkdir(parents=True)
 manifest,expected=PORTAL.staged_manifest(STAGE);require(manifest.get('releaseProfile')==PROFILE,'Explicit G2 stage required')
 require(sum((STAGE/n).stat().st_size for n in expected)<985000000,'Whole staged site budget including manifest required')
 predecessor=sealed_g1_public();prior=read(OLD_AUDIT);prior_tree=read(OLD_TREE);prior_manifest=read(PRIOR/'build-manifest.json')
 require(prior['status']=='passed' and prior['visualReview']['status']=='approved' and prior['visualReview']['reviewer'] in ('root','/root') and prior['deployedCommit']==prior_tree['commit']==G1_DEPLOYED,'Exact prior Root-approved sealed actual G1 publication required')
 require(manifest['preservedPublic']==predecessor and manifest['preservedPublic']['audit']==ref(OLD_AUDIT) and manifest['preservedPublic']['manifest']==ref(PRIOR/'build-manifest.json') and prior['manifest']['sha256']==digest(PRIOR/'build-manifest.json'),'Current stage must pin exact prior G1 audit/manifest/receipt/tree')
 current=current_tree();require(current['commit']!=prior_tree['commit'] and set(current['files'])==set(expected),'New exact complete deployed tree required')
 prior_rows={r['file']:r for r in prior['files']};require(len(prior_rows)==len(prior['files']),'Unique complete previous public proof required')
 report={'status':'running','startedUtc':now(),'baseUrl':URL,'sourceCommit':manifest['sourceCommit'],'deployedCommit':current['commit'],'gameRevisions':manifest['gameRevisions'],'manifest':ref(STAGE/'build-manifest.json'),'files':[],'checks':[],'visualReview':{'status':'pending'},'priorAudit':ref(OLD_AUDIT),'priorManifest':ref(PRIOR/'build-manifest.json'),'priorGitTree':ref(OLD_TREE),'priorReleaseReceipt':predecessor['receipt'],'priorReleaseRootReview':predecessor['rootReview'],'currentGitTree':ref(OUT/'PublicGitObjects/current-gh-pages.json'),'harness':ref(Path(__file__)),'scope':'Fresh actual HTTPS body SHA/bytes for every63 non-image file after this G2 publication. Only15350 identical JPEG rows inherit the exact dated G1 previous response evidence plus equal prior/new deployed Git blob identity; no historical GET is labelled fresh and no previous game execution is reused as a fresh pass.','limits':['Software portal viewports are not game input or physical-device tests.','Historical imported helper execution remains unproved as stated in the pinned prior audit.','Current changed-game public input/visual checks are separate reports.']}
 def save():PORTAL.save_report(output,report)
 captured=[]
 for source in (Path(__file__),Path(PORTAL.__file__),ROOT/'Tools/Autonomy/g2_evidence.py',ROOT/'Tools/Web/release_contract.py'):
  target=output/'RecordedHarness'/source.relative_to(ROOT);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target);captured.append({'file':str(source.relative_to(ROOT)),'preservedFile':str(target.relative_to(ROOT)),'sha256':digest(target)})
 report['capturedSources']=captured;save()
 try:
  fresh=[]
  for name,h in expected.items():
   if name.startswith(PREFIX) and name.endswith('.jpg'):
    old=prior_rows[name]
    require(old.get('passed') is True and old.get('status')==200 and old['sha256']==old['expectedSha256']==prior_manifest['files'][name]==h==digest(STAGE/name) and old['bytes']==old['expectedBytes']==(STAGE/name).stat().st_size,'Identical historical actual image bytes required')
    require(prior_tree['files'][name]==current['files'][name],'Preserved image remote Git object changed')
    row=copy.deepcopy(old);row.update(freshGet=False,verificationSource='prior-root-reviewed-g1-response-and-equal-current-git-blob',previousVerificationSource=old.get('verificationSource'),priorAudit=report['priorAudit'],priorGitBlob=prior_tree['files'][name]['gitBlob'],currentGitBlob=current['files'][name]['gitBlob']);report['files'].append(row)
   else:fresh.append((name,h))
  require(sum(name.startswith(PREFIX) and name.endswith('.jpg')for name in expected)==15350 and len(fresh)==63,'Whole preserved15350 image archive and63 fresh non-image GETs required')
  context=PORTAL.https_context()
  with ThreadPoolExecutor(max_workers=a.workers)as pool:
   jobs=[pool.submit(PORTAL.fetch_file,URL,STAGE,name,h,a.timeout,context)for name,h in fresh]
   for f in as_completed(jobs):
    row=f.result();row.update(freshGet=True,verificationSource='fresh-post-g2-https-GET',checkedUtc=now());report['files'].append(row)
  report['files'].sort(key=lambda r:r['file']);report['auditCounts']={'freshHttpsGets':len(fresh),'inheritedImageIdentities':15350};save()
  require(len(report['files'])==len(expected) and {r['file']for r in report['files']}==set(expected) and all(r.get('passed')is True for r in report['files']),'Every exact current public file must pass; failed attempts are retained')
  from playwright.sync_api import sync_playwright
  render=['--ozone-platform=x11','--use-gl=angle','--use-angle=gl','--ignore-gpu-blocklist']if a.renderer=='x11-mesa'else['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader']
  with sync_playwright()as playwright:
   browser=playwright.chromium.launch(executable_path='/usr/bin/chromium',headless=a.renderer!='x11-mesa',proxy=PORTAL.browser_proxy(),args=['--no-sandbox','--disable-dev-shm-usage',*render])
   try:
    for width,height in ((1440,1050),(390,844)):
     report['checks'].append(PORTAL.inspect_landing(browser,URL,output,width,height,a.timeout));save()
   finally:browser.close()
  report['screenshots']=[c['screenshot']for c in report['checks']if c.get('screenshot')]
  require(len(report['checks'])==2 and all(c.get('passed')is True for c in report['checks']),'Both fresh public portal viewport checks must pass')
  report['status']='passed'
 except Exception as error:report.update(status='failed',error=PORTAL.safe_error(error))
 finally:
  for session in PORTAL.HTTP_SESSIONS:session.close()
  report['checkedUtc']=now();save()
 print(json.dumps({'status':report['status'],'auditCounts':report.get('auditCounts'),'report':str((output/'report.json').relative_to(ROOT))}));return 0 if report['status']=='passed'else 1
if __name__=='__main__':raise SystemExit(main())
