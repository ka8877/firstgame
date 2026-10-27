#!/usr/bin/env python3
"""Prove only the recorded detail desktop profile-reload cache naming mismatch.

Never edits a raw report, pixels, checks or inputs. The normal gate recognized
continued-battle but not the old profile-continued checkpoint name. The original
HTTP304, exact loader URL, Unity successful cache log and reviewed restored
profile are all mandatory. This is not a general request-error exception.
"""
from __future__ import annotations
from datetime import datetime,timezone
import hashlib,json
from pathlib import Path
from urllib.parse import urlsplit
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Artifacts/Validation/Detail/Desktop720/FantasyCards/report.json'
PLAN=ROOT/'Artifacts/Validation/Detail/HarnessHistory/detail-smoke-plans-before-profile-cache-name.json'
GATE='No unexpected failed requests; exact proved cache/local-media cancellations retained'


def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def require(value,message):
    if not value: raise ValueError(message)


def analyze(path=REPORT):
    path=Path(path).resolve(); require(path==REPORT,'Only the recorded detail desktop profile reload is supported')
    raw=json.loads(path.read_text()); bad=[c for c in raw['checks']if c.get('passed')is not True]
    require(raw.get('game')=='FantasyCards' and raw.get('revision')=='fm-detail-2026-10-27'
            and raw.get('suite')=='modern-real-input-v1' and raw.get('status')=='failed'
            and raw.get('failure')==GATE and len(bad)==1 and bad[0]['name']==GATE,'Different raw failure')
    require(not raw['pageErrors'] and not raw['httpErrors']
            and not any(c['type']=='error'for c in raw['console']),'Other runtime errors are not supported')
    require(PLAN.is_file() and digest(PLAN)==raw['harness']['planSha256'],'Original authored plan changed')
    plan=json.loads(PLAN.read_text())['FantasyCards']; require('profile-continued'in plan['requiredCheckpoints'],'Not the original profile plan')
    approved=[c for c in raw['checkpoints']if c['status']=='approved']
    names={c['name'].split('-adjusted-')[0]for c in approved}
    require(names==set(plan['requiredCheckpoints']) and len(approved)==9,'All nine original interactions must be directly reviewed')
    for c in approved:
        s=c['original']; image=(ROOT/s['path']).resolve(); review=c['review']
        require(image.is_relative_to(path.parent) and digest(image)==s['sha256']==review.get('frameSha256')
                and review.get('status')=='approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review.get('expected')==c['expected'],'Original review changed')
    require(any(c['name']=='profile-continued' and c['navigation']==1 for c in approved),'Restored profile was not reviewed')
    diagnostic=raw['networkDiagnostic']; unexpected=diagnostic['unexpectedFailures']
    require(len(unexpected)==1 and unexpected[0]['navigation']==1,'Unexpected failures differ')
    require(len(raw['failedRequests'])==2 and all(f['navigation']in (1,2)for f in raw['failedRequests']),'Different request set')
    proofs=[]
    for f in raw['failedRequests']:
        nav=f['navigation'];url=f['url'];parsed=urlsplit(url)
        require(parsed.scheme=='http' and parsed.hostname=='127.0.0.1' and not parsed.query and not parsed.fragment
                and f['method']=='GET' and f['resourceType']=='fetch' and f['error']=='net::ERR_ABORTED','Not a local cached Unity data GET')
        require(parsed.path.endswith('.data.unityweb') and any(c.get('navigation')==nav and c.get('dataUrl')==url for c in raw['unityConfigurations']),'Exact loader data URL differs')
        require(any(r['navigation']==nav and r['url']==url and r['status']==304 and r['method']=='GET'for r in raw['networkResponses']),'No exact HTTP304')
        marker="[UnityCache] '"+url+"' successfully revalidated and served from the browser cache"
        require(any(m['navigation']==nav and m['text']==marker for m in raw['console']),'No exact Unity cache success')
        restored=next((c for c in approved if c['navigation']==nav and c['name']==('profile-continued'if nav==1 else'continued-battle')),None)
        require(restored is not None,'The exact reloaded navigation did not restore visible state')
        relative='Build/'+parsed.path.rsplit('/',1)[-1]
        file=next((r for r in raw['buildFingerprint']['files']if r['file']==relative),None)
        current=ROOT/'Artifacts/Builds/FantasyCards/Web'/relative
        require(file and current.is_file() and current.stat().st_size==file['bytes'] and digest(current)==file['sha256'],'Current data bytes changed')
        proofs.append({'request':f,'httpStatus':304,'unityCacheMessage':marker,'runtime':file,
                       'restoredOriginal':restored['original'],'reviewer':restored['review']['reviewer']})
    return {'status':'passed-recorded-cache-recovery','analyzedUtc':datetime.now(timezone.utc).isoformat(),
            'rawReport':{'file':str(path.relative_to(ROOT)),'sha256':digest(path),'status':raw['status']},
            'originalPlan':{'file':str(PLAN.relative_to(ROOT)),'sha256':digest(PLAN)},
            'analysisSource':{'file':str(Path(__file__).relative_to(ROOT)),'sha256':digest(Path(__file__))},
            'requests':proofs,'otherRawChecksPassed':sum(c.get('passed')is True for c in raw['checks']),
            'visualReview':{'status':'approved','reviewer':'; '.join(sorted({c['review']['reviewer']for c in approved})),
                'reviewedUtc':max(c['review']['reviewedUtc']for c in approved),'runtimeFiles':raw['buildFingerprint']['files'],
                'screenshots':[c['original']for c in approved],'findings':[c['review']['observed']for c in approved]},
            'scope':'Raw automatic network gate remains FAILED. Only the exact local data URL/304/Unity successful cache marker and separately reviewed profile/battle restoration are proved. No runtime source, input, pixel, PCM or raw pass status was rewritten.'}


def verify_record(path=REPORT):
    current=analyze(path);recorded=json.loads((Path(path).parent/'cache-adjudication.json').read_text())
    for key in ('rawReport','originalPlan','analysisSource','requests','visualReview'):
        require(current[key]==recorded[key],'Recorded detail cache proof changed: '+key)
    return recorded


if __name__=='__main__':
    result=analyze();(REPORT.parent/'cache-adjudication.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'status':result['status'],'rawStatus':'failed','requests':len(result['requests']),'otherRawChecksPassed':result['otherRawChecksPassed']}))
