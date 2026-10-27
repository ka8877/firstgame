#!/usr/bin/env python3
"""Verify exact fresh Restoration tests/builds/reviewed browser evidence; never launches or publishes."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from zoneinfo import ZoneInfo

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Validation/Restoration'
BUILD=Path('/tmp/firstgame-restoration-builds')
sys.path.insert(0,str(ROOT/'Tools/Web'))
from release_contract import RESTORATION, manifest_contract
from restoration_source_binding import files as binding_files, verify as verify_binding, fingerprint
REVISIONS=RESTORATION['revisions']


def require(ok,message):
    if not ok: raise ValueError(message)


def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def read(path):return json.loads(Path(path).read_text())
def ref(path):
    p=Path(path)
    # Cloud-generated players live in one explicit task-owned /tmp root. Source
    # and evidence stay under the repository; arbitrary external paths are refused.
    real=p.resolve()
    require(real.is_file() and (real.is_relative_to(ROOT) or real.is_relative_to(BUILD)), 'Unexpected evidence path: '+str(p))
    return {'file':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(real),'bytes':p.stat().st_size,'sha256':digest(p)}


def source_files():
    return binding_files('FantasyCards')+binding_files('PathOfDawn')


def freeze():
    OUT.mkdir(parents=True,exist_ok=True)
    p=OUT/'source-freeze.json'
    value={'releaseProfile':'restoration','gameRevisions':REVISIONS,'recordedUtc':datetime.now(timezone.utc).isoformat(),'files':source_files()}
    p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
    return {'status':'recorded','files':len(value['files']),**ref(p)}


def current_sources():
    p=OUT/'source-freeze.json';value=read(p)
    require(value['gameRevisions']==REVISIONS and value['files']==source_files(),'Sources/assets changed after current freeze')
    return ref(p)


def xml_result(path,kind):
    tree=ET.parse(path).getroot()
    if kind=='core':
        cases=[n for n in tree.iter() if n.tag.split('}')[-1]=='UnitTestResult']
        require(cases and all(n.get('outcome')=='Passed' for n in cases),'Core suite did not wholly pass')
        counters=[n for n in tree.iter() if n.tag.split('}')[-1]=='Counters']
        require(len(counters)==1 and int(counters[0].get('total',0))==int(counters[0].get('passed',-1))==len(cases),'Core result count is incomplete')
        starts=[datetime.fromisoformat(n.get('startTime').replace('Z','+00:00')).timestamp() for n in cases]
        inputs=[p for directory in ('Games/FantasyCards/Assets/Game/Core','Games/PathOfDawn/Assets/Game/Core','Games/FantasyCards/Assets/Shared','Games/PathOfDawn/Assets/Shared','Tools/Validation/Tests') for p in (ROOT/directory).rglob('*') if p.is_file() and p.suffix in {'.cs','.json'}]
        require(inputs and min(starts)+1>=max(p.stat().st_mtime for p in inputs),'Core source changed after the executed complete suite')
    else:
        cases=tree.findall('.//test-case')
        require(cases and tree.get('result')=='Passed' and tree.get('failed')=='0' and tree.get('skipped')=='0' and all(n.get('result')=='Passed' for n in cases),'Native suite did not wholly pass: '+str(path))
    require(len(cases)>=(142 if kind=='core' else 9 if 'FantasyCards' in str(path) else 8),'Current complete suite required')
    return {'passed':len(cases),**ref(path)}


def runtime(relative):return str(relative)=='index.html' or relative.parts[0] in ('Build','StreamingAssets','TemplateData')
def files_for(game,target):
    folder=BUILD/game/target
    return [dict(file=str(p.relative_to(folder)),bytes=p.stat().st_size,sha256=digest(p)) for p in sorted(folder.rglob('*'))
            if p.is_file() and (target!='Web' or runtime(p.relative_to(folder)))
            and not any('DoNotShip' in s or 'DontShip' in s for s in p.relative_to(folder).parts)
            and p.suffix not in {'.log','.pdb','.mdb','.map'} and p.name!='OutputFiles.json']


def build(game,target):
    p=BUILD/game/target/'BuildEvidence.txt';v=dict(line.split(': ',1) for line in p.read_text().splitlines() if ': ' in line)
    require(v.get('Revision')==REVISIONS[game] and v.get('Content')==RESTORATION['contents'][game] and v.get('Target')==('WebGL' if target=='Web' else 'StandaloneWindows64') and v.get('Errors')=='0' and v.get('Result')=='Succeeded','Current successful build required: '+game+'/'+target)
    files=files_for(game,target);require(len(files)>=5,'Incomplete player: '+game+'/'+target)
    inputs=read(p.parent/'BuildInputs.json');fingerprint=verify_binding(inputs)
    require(inputs['game']==game and v.get('SourceFingerprint')==fingerprint,'Build is not bound to current per-game source inputs')
    return {**v,**ref(p),'inputs':ref(p.parent/'BuildInputs.json'),'files':files}


def native(game):
    folder=OUT/'Native'/game
    bound=read(folder/'binding.json');inputs=read(folder/'inputs-before.json')
    require(bound.get('status')=='passed' and bound.get('sourceChangedDuringRun') is False,'Unbound or changed native attempt')
    require(bound['inputManifestSha256']==digest(folder/'inputs-before.json') and bound['xmlSha256']==digest(folder/'PlayModeUI.xml'),'Native binding file bytes changed')
    require(bound['sourceFingerprint']==verify_binding(inputs) and inputs['game']==game,'Native result does not match current per-game source inputs')
    return {**xml_result(folder/'PlayModeUI.xml','native'),'inputs':ref(folder/'inputs-before.json'),'binding':ref(folder/'binding.json')}


def card_authoring():
    p=OUT/'card-authoring.json';value=read(p)
    bound=read(OUT/'card-authoring-source-binding.json')
    require(value.get('status')=='passed' and value.get('cards')==605
            and value.get('completeDefinitions')==605 and value.get('originalArtworkBindings')==605
            and value.get('fullCardRectangles')==605 and value.get('generatorReproducedWithoutWrites') is True,
            'Complete current original-card authoring audit required')
    require(bound.get('fingerprint')==fingerprint(binding_files('FantasyCards'))
            and bound.get('report')==str(p.relative_to(ROOT)),
            'Card authoring audit does not match current FantasyCards source')
    return {**ref(p),'binding':ref(OUT/'card-authoring-source-binding.json')}


def normalized(files):return sorted(({k:row[k] for k in ('file','bytes','sha256')} for row in files),key=lambda r:r['file'])
def browser(game,profile,files):
    p=OUT/profile/game/'report.json';v=read(p)
    qualification=None
    if game=='PathOfDawn' and profile=='Desktop720' and v.get('status')=='failed':
        spec=importlib.util.spec_from_file_location('restoration_summary_gospel_followup',ROOT/'Tools/Validation/verify-restoration-gospel-followup.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        qualification=module.verify_record(p)
    require(v.get('revision')==REVISIONS[game] and v.get('game')==game and v.get('suite')=='modern-real-input-v1' and (v.get('status')=='passed' or qualification),'Current passed browser report or exact qualified two-session Gospel evidence required: '+str(p))
    require(v.get('checks') and all(c.get('passed') is True for c in v['checks']) and v.get('input'),'Actual browser checks/input missing')
    require(normalized(v['buildFingerprint']['files'])==normalized(files),'Browser runtime differs from current build')
    review=qualification['visualReview'] if qualification else v.get('visualReview',{})
    require(review.get('status')=='approved' and review.get('reviewer') and review.get('reviewedUtc') and normalized(review['runtimeFiles'])==normalized(files),'Current original screenshot review missing')
    expected=(844,390) if profile in ('Mobile844','PublicMobile844') else (1280,720)
    require(tuple(v['interactionViewport'][k] for k in ('width','height'))==expected,'Wrong current browser viewport')
    plan=read(v['harness']['plan'])[game]
    require(digest(v['harness']['plan'])==v['harness']['planSha256'] and plan['revision']==REVISIONS[game],'Plan changed after browser run')
    captured=v['harness'].get('capturedSources',[])
    require(len(captured)>=7,'Executed browser harness copies missing')
    for source in captured:
        recorded=ROOT/source['file']
        require(recorded.resolve().is_relative_to((p.parent/'RecordedHarness').resolve())
                and digest(recorded)==source['sha256'],'Executed browser harness copy changed')
    captured_by_source={source['source']:source['sha256'] for source in captured}
    require(captured_by_source.get(v['harness']['file'])==v['harness']['sha256']
            and captured_by_source.get('Tools/Validation/web-smoke.py')==v['harness']['helpersSha256']
            and captured_by_source.get(str(Path(v['harness']['plan']).relative_to(ROOT)))==v['harness']['planSha256'],
            'Recorded browser harness identity differs from the executed report')
    checkpoint_rows=qualification['approvedCheckpoints'] if qualification else v['checkpoints']
    checkpoints={c['name'].split('-adjusted-')[0]:c for c in checkpoint_rows if c.get('status')=='approved'}
    require(set(plan['requiredCheckpoints']).issubset(checkpoints),'Current required visible checkpoints missing')
    originals={(s['path'],s['sha256']) for s in v['screenshots']}
    evidence_parents=[p.parent.resolve()]
    if qualification:
        originals.update((row['original']['path'],row['original']['sha256']) for row in qualification['approvedCheckpoints'])
        evidence_parents=[(ROOT/relative).resolve() for relative in qualification['evidenceParents']]
    for shot in review['screenshots']:
        image=ROOT/shot['path']
        require((shot['path'],shot['sha256']) in originals and any(image.resolve().is_relative_to(parent) for parent in evidence_parents) and digest(image)==shot['sha256'],'Reviewed original changed')
    for window in v['audioEvidence']['windows']+v['audioEvidence'].get('sfxWindows',[]):
        raw=p.parent/window['rawEvidence']
        require(raw.resolve().is_relative_to(p.parent.resolve()) and digest(raw)==window['sha256'],'PCM evidence changed')
    if game=='FantasyCards' and profile!='PublicMobile844':
        windows=v['audioEvidence'].get('sfxWindows',[])
        require(any(w.get('audibleShortCueFrames',0)>=3 for w in windows),'Actual effect-only PCM evidence missing')
        require(any(w.get('maximumDestinationRms',1)<1e-5 for w in windows),'Actual independent SFX mute silence missing')
    result={'checks':qualification['totalChecks'] if qualification else len(v['checks']),'reviewedOriginals':len(review['screenshots']),'pcmWindows':qualification['pcmWindows'] if qualification else len(v['audioEvidence']['windows']),**ref(p)}
    if qualification:
        result.update(rawStatus=v['status'],ordinarySessions=2,qualification=ref(p.parent/'followup-verification.json'),suffixReport=qualification['suffixReport'],scope=qualification['scope'])
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--freeze',action='store_true');parser.add_argument('--public',action='store_true');a=parser.parse_args()
    if a.freeze:print(json.dumps(freeze()));return
    sources=current_sources()
    builds={g:{t:build(g,t) for t in ('Windows','Web')} for g in REVISIONS}
    # A release can finish after midnight. Keep the release date bound to the
    # actual frozen inputs so packaging verification remains byte reproducible.
    release_date=datetime.fromisoformat(read(OUT/'source-freeze.json')['recordedUtc']).astimezone(ZoneInfo('Asia/Seoul')).date().isoformat()
    record={'releaseProfile':'restoration','date':release_date,'gameRevisions':REVISIONS,'contentVersions':RESTORATION['contents'],'sourceFreeze':sources,
      'core':xml_result(OUT/'core-tests.trx','core'),
      'cardAuthoring':card_authoring(),
      'native':{g:native(g) for g in REVISIONS},'builds':builds,
      'web':{p:{g:browser(g,p,builds[g]['Web']['files']) for g in REVISIONS} for p in ('Desktop720','Mobile844')},
      'publication':{'status':'not-verified'},
      'limits':['Software Chromium/mobile input emulation; no physical mobile/Windows performance or human speaker listening claim.','Original asset and UI reference reconstruction; no recovered proprietary client/source or full original game claim.','Gospel birth prologue and temple opening only; artwork is an original interpretation.']}
    if a.public:
        portal=read(OUT/'PublicPortal/report.json')
        require(portal['status']=='passed' and portal['visualReview']['status']=='approved','Public portal exact files and direct review required')
        manifest=read(ROOT/'Artifacts/RestorationWebPlaytest/build-manifest.json')
        require(manifest_contract(manifest)['name']=='restoration' and manifest['sourceDirty'] is False,'Current clean published manifest required')
        require(portal['manifest']['sha256']==digest(ROOT/'Artifacts/RestorationWebPlaytest/build-manifest.json'),'Public portal manifest changed')
        record['publicWeb']={g:browser(g,'PublicMobile844',builds[g]['Web']['files']) for g in REVISIONS}
        record['publication']={'status':'verified','url':'https://ka8877.github.io/firstgame/','sourceCommit':manifest['sourceCommit'],'verifiedFiles':len(portal['files']),**ref(OUT/'PublicPortal/report.json')}
    OUT.mkdir(parents=True,exist_ok=True);(OUT/'summary.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
    (ROOT/'Tools/Environment/validation.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'status':'verified','core':record['core']['passed'],'native':{g:r['passed'] for g,r in record['native'].items()},'publication':record['publication']['status']}))

if __name__=='__main__':
    try:main()
    except (ValueError,KeyError,OSError,ET.ParseError) as error:print('Restoration verification refused: '+str(error),file=sys.stderr);sys.exit(1)
