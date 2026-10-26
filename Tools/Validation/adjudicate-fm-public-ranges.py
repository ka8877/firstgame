#!/usr/bin/env python3
"""Hash-bound analysis of recorded public HTTP206 media cancellations; never changes a raw report."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,importlib.util,json
ROOT=Path(__file__).resolve().parents[2]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def analyze(report_path):
    p=Path(report_path);r=json.loads(p.read_text())
    bad=[c for c in r['checks'] if c.get('passed') is not True]
    assert r['status']=='failed' and len(bad)==1 and bad[0]['name']=='No unexpected failed requests; exact proved cache/local-media cancellations retained'
    assert not r['pageErrors'] and not r['httpErrors'] and not any(m['type']=='error' for m in r['console'])
    assert r['publicDelivery']['status']=='passed'
    spec=importlib.util.spec_from_file_location('recorded_ranges',ROOT/'Tools/Validation/fm-local-media-errors.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    rows=[];refs=[]
    for ref in r['audioEvidence']['localMediaTimelines']:
        raw=p.parent/ref['rawEvidence'];assert digest(raw)==ref['sha256'];rows.extend(json.loads(raw.read_text())['observations']);refs.append(ref)
    proofs=[]
    for f in r['failedRequests']:
        proof=m.recovered_public_range_cancellation(r['game'],r['revision'],r['suite'],f,r['publicDelivery']['gameUrl'],r['buildFingerprint']['files'],rows,r['audioEvidence']['windows'],r['networkResponses'],r['publicDelivery']['files'])
        assert proof is not None, 'Unproved request failure: '+f['url'];proofs.append(proof)
    assert proofs
    checkpoints=r['checkpoints'];assert len(checkpoints)==2 and all(c['status']=='approved' for c in checkpoints)
    for c in checkpoints:
        assert c['review']['frameSha256']==c['original']['sha256']==digest(ROOT/c['original']['path'])
    return {'status':'passed-recorded-range-recovery','analyzedUtc':datetime.now(timezone.utc).isoformat(),
        'rawReport':{'file':str(p.relative_to(ROOT)),'sha256':digest(p),'status':r['status']},
        'analysisSource':{'file':str(Path(__file__).relative_to(ROOT)),'sha256':digest(Path(__file__))},
        'classifier':{'file':str(Path(m.__file__).relative_to(ROOT)),'sha256':digest(Path(m.__file__))},
        'localMediaTimelines':refs,'requests':proofs,'otherRawChecksPassed':sum(c.get('passed') is True for c in r['checks']),
        'visualReview':{'status':'approved','reviewer':'; '.join(sorted({c['review']['reviewer'] for c in checkpoints})),
            'reviewedUtc':max(c['review']['reviewedUtc'] for c in checkpoints),'runtimeFiles':r['buildFingerprint']['files'],
            'screenshots':[c['original'] for c in checkpoints],'findings':[c['review']['observed'] for c in checkpoints]},
        'scope':'Original automatic network gate remains FAILED. Separate recorded-evidence analysis proves only these exact public same-origin verified-byte HTTP206 media ranges recovered. No raw status, pixels, inputs or PCM were rewritten; no product/runtime or physical-device claim.'}
def main():
    p=ROOT/'Artifacts/Validation/Modern/PublicMobile844/FantasyCards/report.json';result=analyze(p)
    (p.parent/'range-adjudication.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'status':result['status'],'requests':len(result['requests']),'rawStatus':result['rawReport']['status']}))
if __name__=='__main__':main()
