#!/usr/bin/env python3
"""Fresh public Nativity files and explicit unchanged-image immutable-object proof."""
from concurrent.futures import ThreadPoolExecutor,as_completed
from datetime import datetime,timezone
from pathlib import Path
import argparse
import importlib.util
import json
import os
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from nativity_public_proof import PREFIX,FRESH,REUSED,PRIOR,PRIOR_REVIEW,PRIOR_PUBLIC,HISTORY_FILE,BASELINE_FILE,DEPENDENCY_FILE,REUSE_SCOPE,prior_proof,baseline_publication,historical_dependency_provenance,git_tree,inherited_row,ref,sha,validate_delta,require
from nativity_storage import authorize_output
spec=importlib.util.spec_from_file_location('quest_portal_browser',Path(__file__).with_name('verify-public-portal.py'))
PORTAL=importlib.util.module_from_spec(spec);spec.loader.exec_module(PORTAL)


def now():return datetime.now(timezone.utc).isoformat()


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--stage',type=Path,default=Path('/tmp/firstgame-nativity-staged-site'))
    p.add_argument('--output',type=Path,default=ROOT/'Artifacts/Validation/Nativity/PublicPortal')
    p.add_argument('--base-url',default='https://ka8877.github.io/firstgame/')
    p.add_argument('--workers',type=int,default=4)
    p.add_argument('--timeout',type=int,default=180)
    p.add_argument('--chromium',default='/usr/bin/chromium')
    p.add_argument('--renderer',choices=('x11-mesa','swiftshader'),default='x11-mesa')
    a=p.parse_args();require(1<=a.workers<=8,'Use bounded HTTP workers')
    subprocess.run([sys.executable,str(ROOT/'Tools/Environment/assert-runtime-idle.py')],check=True)
    stage=a.stage.resolve();output=authorize_output(a.output);base=PORTAL.root_url(a.base_url)
    manifest,expected=PORTAL.staged_manifest(stage)
    require(manifest.get('releaseProfile')=='nativity','Only the new Nativity release uses this narrow delta verifier')
    old_path=ROOT/'Artifacts/Validation/Nativity/PublicGitObjects/prior-gh-pages.json'
    require(old_path.is_file(),'Capture the actual previous public Git tree before publication')
    old_tree=json.loads(old_path.read_text());old,old_images=prior_proof()
    baseline=baseline_publication(old,old_images)
    historical_dependency_provenance()
    require(all(old_tree.get(k)==baseline['tree'].get(k) for k in ('kind','repository','branch','commit','files')),
            'Actual before-publication tree must match the latest Root-approved Restage publication')
    current_path=ROOT/'Artifacts/Validation/Nativity/PublicGitObjects/current-gh-pages.json'
    current=git_tree('current-nativity-publication',current_path)
    require(current['commit']!=old_tree['commit'],'New gh-pages deployment required')
    require(set(current['files'])==set(expected),'Deployed Git tree differs from exact current staged file set')
    previous=PORTAL.archive_previous(output)
    report={'status':'running','startedUtc':now(),'baseUrl':base,'deployedCommit':current['commit'],
        'sourceCommit':manifest['sourceCommit'],'gameRevisions':manifest['gameRevisions'],
        'manifest':{'file':str(stage/'build-manifest.json'),'sha256':expected['build-manifest.json']},
        'scope':'Fresh post-deployment HTTPS GET/SHA for every non-image file. Only unchanged15350cardJPEG byte identities inherit the explicitly dated earlier Reprise actual GETs and Root-approved publication through preserved input/producer snapshots plus identical before/new deployed Git blobs. No previous game test result is reused as a new pass, and historical GETs are never labelled as new downloads.',
        'files':[],'checks':[],'visualReview':{'status':'pending','scope':'Root must directly view both current original portal captures.'},
        'imageIdentityReuse':{'status':'pending','scope':REUSE_SCOPE,
            'priorAudit':ref(PRIOR),
            'priorRootReview':ref(PRIOR_REVIEW),
            'priorPublicAudit':ref(PRIOR_PUBLIC),'historySnapshot':ref(HISTORY_FILE),
            'baselinePublication':ref(BASELINE_FILE),
            'historicalDependencySource':ref(DEPENDENCY_FILE),
            'historicalImportedDependencyExecutionBound':False,
            'priorGetWindow':{'startedUtc':old['startedUtc'],'checkedUtc':old['checkedUtc']},
            'priorGitTree':ref(old_path),'currentGitTree':ref(current_path)},
        'harness':ref(Path(__file__)),'limits':['Historical GETs are not relabelled as fresh requests.','Software portal viewports are separate from game input and physical-phone performance.']}
    if previous:report['previousRunArchive']=previous
    PORTAL.save_report(output,report)
    try:
        fresh=[]
        for name,value in expected.items():
            if name.startswith(PREFIX) and name.endswith('.jpg'):
                report['files'].append(inherited_row(name,value,stage,old_images,old_tree,current,report['imageIdentityReuse']['priorAudit']))
            else:fresh.append((name,value))
        context=PORTAL.https_context()
        with ThreadPoolExecutor(max_workers=a.workers) as pool:
            pending=[pool.submit(PORTAL.fetch_file,base,stage,name,value,a.timeout,context) for name,value in fresh]
            for future in as_completed(pending):
                row=future.result();row.update(verificationSource=FRESH,freshGet=True,checkedUtc=now());report['files'].append(row)
        # Retry only concrete failing fresh requests. All first attempts remain
        # separately recorded, and no failed response authorizes a pass.
        failed=[r for r in report['files'] if not r.get('passed')]
        if failed:
            report['freshRetryOriginals']=failed
            for old_row in failed:
                row=PORTAL.fetch_file(base,stage,old_row['file'],expected[old_row['file']],a.timeout,context)
                row.update(verificationSource=FRESH,freshGet=True,checkedUtc=now())
                report['files'][report['files'].index(old_row)]=row
        for session in PORTAL.HTTP_SESSIONS:session.close()
        report['files'].sort(key=lambda r:r['file'])
        report['auditCounts']={'freshHttpsGets':len(fresh),'inheritedImageIdentities':sum(r.get('verificationSource')==REUSED for r in report['files'])}
        report['imageIdentityReuse']['status']='passed'
        validate_delta(report,stage)
        PORTAL.save_report(output,report)
        from playwright.sync_api import sync_playwright
        render=(["--ozone-platform=x11","--use-gl=angle","--use-angle=gl","--ignore-gpu-blocklist"] if a.renderer=='x11-mesa'
                else ["--use-gl=angle","--use-angle=swiftshader","--enable-unsafe-swiftshader"])
        with sync_playwright() as playwright:
            browser=playwright.chromium.launch(executable_path=a.chromium,headless=a.renderer!='x11-mesa',proxy=PORTAL.browser_proxy(),
                args=['--no-sandbox','--disable-dev-shm-usage',*render])
            try:
                for width,height in ((1440,1050),(390,844)):
                    report['checks'].append(PORTAL.inspect_landing(browser,base,output,width,height,a.timeout))
            finally:browser.close()
        report['screenshots']=[c['screenshot'] for c in report['checks'] if c.get('screenshot')]
        report['status']='passed' if len(report['files'])==len(expected) and all(r.get('passed') for r in report['files']) and len(report['checks'])==2 and all(c['passed'] for c in report['checks']) else 'failed'
    except Exception as e:report.update(status='failed',error=PORTAL.safe_error(e))
    finally:report['checkedUtc']=now();PORTAL.save_report(output,report)
    print(json.dumps({'status':report['status'],'auditCounts':report.get('auditCounts'),'viewports':len(report['checks'])}))
    return 0 if report['status']=='passed' else 1


if __name__=='__main__':raise SystemExit(main())
