#!/usr/bin/env python3
"""Fresh current-session HTTPS GET of every immutable original card JPEG.

Run while the executable is being compiled. No browser or Unity is started, no
media is written, and the dated GETs are not called post-deployment downloads.
The later portal proof binds these bytes to equal old/new public Git objects.
"""
from concurrent.futures import ThreadPoolExecutor,as_completed
from datetime import datetime,timezone
from pathlib import Path
import hashlib,importlib.util,json,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import digest,verify
spec=importlib.util.spec_from_file_location('reprise_image_http',ROOT/'Tools/Validation/verify-public-portal.py')
H=importlib.util.module_from_spec(spec);spec.loader.exec_module(H)
BUILD=Path('/tmp/firstgame-reprise-builds/FantasyCards/Web')
OUT=ROOT/'Artifacts/Validation/Reprise/PublicImages'
PREFIX='games/fantasy-cards/'
BASE='https://ka8877.github.io/firstgame/'
def now():return datetime.now(timezone.utc).isoformat()
def save(value):
 p=OUT/'report.json';q=OUT/'report.json.tmp';q.write_text(json.dumps(value,indent=2)+'\n');q.replace(p)
def main():
 OUT.mkdir(parents=True,exist_ok=True);assert not (OUT/'report.json').exists(),'Existing real GET proof is preserved.'
 inputs=json.loads((BUILD/'BuildInputs.json').read_text());fp=verify(inputs)
 expected={str(p.relative_to(BUILD)):digest(p) for p in sorted((BUILD/'StreamingAssets/Cards').glob('*.jpg'))}
 assert len(expected)==15350
 tree=ROOT/'Artifacts/Validation/Reprise/PublicGitObjects/prior-gh-pages.json';assert tree.is_file()
 report={'status':'running','startedUtc':now(),'baseUrl':BASE,'sourceFingerprint':fp,'sourceInputs':{'file':str(BUILD/'BuildInputs.json'),'sha256':digest(BUILD/'BuildInputs.json')},'priorDeploymentTree':{'file':str(tree.relative_to(ROOT)),'sha256':digest(tree)},'harness':{'file':str(Path(__file__).relative_to(ROOT)),'sha256':digest(__file__)},'scope':'Fresh verified HTTPS GET/body SHA of all15350 card JPEGs before the new deployment. These current-session GET timestamps remain explicit. Later publication must independently prove equal old/new deployed Git image blobs and exact staged SHA. No historical missing /tmp proof is reused.','expectedFiles':len(expected),'expectedBytes':sum((BUILD/n).stat().st_size for n in expected),'files':[],'retryOriginals':[]}
 save(report)
 def fetch(pair):
  name,sha=pair
  row=H.fetch_file(BASE+PREFIX,BUILD,name,sha,90,H.https_context())
  row.update(file=PREFIX+name,verificationSource='fresh-https-GET',freshGet=True,checkedUtc=now())
  return row
 with ThreadPoolExecutor(max_workers=8) as pool:
  pending={pool.submit(fetch,pair) for pair in expected.items()}
  for future in as_completed(pending):
   report['files'].append(future.result())
   if len(report['files'])%1000==0:
    save(report);print(json.dumps({'fetched':len(report['files']),'failed':sum(not r['passed'] for r in report['files'])}),flush=True)
 failed=[r for r in report['files'] if not r['passed']]
 if failed:
  report['retryOriginals']=failed;save(report)
  with ThreadPoolExecutor(max_workers=4) as pool:
   pairs=[(r['file'][len(PREFIX):],r['expectedSha256']) for r in failed]
   for row in pool.map(fetch,pairs):
    i=next(i for i,r in enumerate(report['files']) if r['file']==row['file']);report['files'][i]=row
 for session in H.HTTP_SESSIONS:session.close()
 report['files'].sort(key=lambda r:r['file'])
 assert verify(inputs)==fp
 assert all(digest(BUILD/n)==sha for n,sha in expected.items())
 report.update(checkedUtc=now(),status='passed' if len(report['files'])==15350 and all(r['passed'] for r in report['files']) else 'failed')
 save(report)
 print(json.dumps({'status':report['status'],'freshHttpsImages':len(report['files']),'bytes':report['expectedBytes'],'reportSha256':digest(OUT/'report.json')}),flush=True)
 return 0 if report['status']=='passed' else 1
if __name__=='__main__':raise SystemExit(main())
