#!/usr/bin/env python3
"""Summarize fresh modern builds and directly reviewed input checks; never launches or publishes."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
REVISIONS = {'FantasyCards':'fm-modern-2026-10-26','PathOfDawn':'gospel-modern-2026-10-26'}
CONTENTS = {'FantasyCards':'fm-classic-0.1.0','PathOfDawn':'gospel-0.1.0'}
REPORTS = ROOT/'Artifacts/Validation/Modern'
URL = 'https://ka8877.github.io/firstgame/'


def require(value, message):
    if not value: raise ValueError(message)


def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''): h.update(block)
    return h.hexdigest()


def ref(path):
    p=Path(path).resolve();require(p.is_relative_to(ROOT) and p.is_file(),f'Missing/outside evidence: {p}')
    return {'file':str(p.relative_to(ROOT)),'sha256':digest(p)}


def timestamp(value):
    return datetime.fromisoformat(value.replace('Z','+00:00')).replace(tzinfo=timezone.utc).timestamp()


def fresh(finished, directories, label):
    sources=[p for d in directories for p in (ROOT/d).rglob('*') if p.is_file() and p.suffix in {'.cs','.shader','.png','.fbx','.otf','.ttf','.html','.ogg','.mp3','.jslib','.json'}]
    require(sources and timestamp(finished)+1 >= max(p.stat().st_mtime for p in sources),f'{label} predates current source/assets')


def core():
    p=ROOT/'Artifacts/TestResults/Core/core-tests.trx';r=ET.parse(p).getroot()
    nodes=lambda name:[n for n in r.iter() if n.tag.split('}')[-1]==name]
    tests=nodes('UnitTestResult');c=nodes('Counters')
    require(tests and len(c)==1 and int(c[0].get('total',0))==int(c[0].get('passed',-1))==len(tests) and all(t.get('outcome')=='Passed' for t in tests),'Core tests must wholly pass')
    ended=max(t.get('endTime','') for t in tests)
    fresh(ended,['Tools/Validation/Tests','Games/FantasyCards/Assets/Game/Core','Games/PathOfDawn/Assets/Game/Core','Games/FantasyCards/Assets/Shared'],'Core tests')
    return {'passed':len(tests),'finishedUtc':ended,**ref(p)}


def native(game):
    p=ROOT/f'Artifacts/TestResults/{game}/PlayModeUI.xml';r=ET.parse(p).getroot();tests=r.findall('.//test-case')
    require(tests and r.get('result')=='Passed' and int(r.get('failed',-1))==0 and int(r.get('skipped',-1))==0 and all(t.get('result')=='Passed' for t in tests),'Native tests must wholly pass: '+game)
    fresh(r.get('end-time',''),[f'Games/{game}/Assets/Game',f'Games/{game}/Assets/SmokeTests',f'Games/{game}/Assets/Shared'],'Native '+game)
    return {'passed':len(tests),'finishedUtc':r.get('end-time'),**ref(p)}


def runtime(path):
    return str(path)=='index.html' or path.parts[0] in ('Build','StreamingAssets','TemplateData')


def normalized(files):
    return sorted(({key:item[key] for key in ('file','bytes','sha256')} for item in files),key=lambda item:item['file'])


def files_for(game,target):
    folder=ROOT/f'Artifacts/Builds/{game}/{target}'
    return [{'file':str(p.relative_to(folder)),'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(folder.rglob('*'))
            if p.is_file() and (target!='Web' or runtime(p.relative_to(folder))) and not any('DoNotShip' in x or 'DontShip' in x for x in p.relative_to(folder).parts)
            and p.name!='OutputFiles.json']


def build(game,target):
    p=ROOT/f'Artifacts/Builds/{game}/{target}/BuildEvidence.txt'
    values=dict(line.split(': ',1) for line in p.read_text().splitlines() if ': ' in line)
    require(values.get('Revision')==REVISIONS[game] and values.get('Content')==CONTENTS[game] and values.get('Result')=='Succeeded' and values.get('Errors')=='0' and values.get('Target')==('WebGL' if target=='Web' else 'StandaloneWindows64'),'Current successful build required: '+game+'/'+target)
    fresh(datetime.fromtimestamp(p.stat().st_mtime,timezone.utc).isoformat(),[f'Games/{game}/Assets/Game',f'Games/{game}/Assets/Resources',f'Games/{game}/Assets/Shared',f'Games/{game}/Assets/WebGLTemplates',f'Games/{game}/Assets/StreamingAssets',f'Games/{game}/Assets/Plugins',f'Games/{game}/Assets/Editor'],'Build '+game+'/'+target)
    files=files_for(game,target);require(len(files)>=5,'Incomplete player '+game+'/'+target)
    return {**values,**ref(p),'files':files}


def browser(game,profile,files):
    p=REPORTS/profile/game/'report.json';r=json.loads(p.read_text())
    adjudication=None
    if game=='FantasyCards' and profile=='PublicMobile844' and r.get('status')=='failed':
        import importlib.util
        spec=importlib.util.spec_from_file_location('public_range_analysis',ROOT/'Tools/Validation/adjudicate-fm-public-ranges.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        adjudication=module.analyze(p)
        recorded=json.loads((p.parent/'range-adjudication.json').read_text())
        require(recorded['rawReport']==adjudication['rawReport'] and recorded['requests']==adjudication['requests'] and recorded['classifier']==adjudication['classifier'],'Recorded range adjudication changed')
    require((r.get('status')=='passed' or adjudication is not None) and r.get('revision')==REVISIONS[game] and r.get('suite')=='modern-real-input-v1','Current modern browser pass required: '+str(p))
    require(r.get('checks') and (all(c.get('passed') is True for c in r['checks']) or adjudication is not None) and r.get('input'),'Missing real input/checks: '+str(p))
    require(normalized(r.get('buildFingerprint',{}).get('files',[]))==normalized(files),'Runtime bytes changed after browser run: '+str(p))
    require(not r.get('pageErrors') and not r.get('httpErrors'),'Runtime page/HTTP errors: '+str(p))
    review=adjudication['visualReview'] if adjudication else r.get('visualReview',{});require(review.get('status')=='approved' and review.get('reviewer') and review.get('reviewedUtc') and normalized(review.get('runtimeFiles',[]))==normalized(files),'Direct current original review required: '+str(p))
    approved=[c for c in r.get('checkpoints',[]) if c.get('status')=='approved'];require(approved,'No original gameplay checkpoints')
    for c in approved:
        s,d=c['original'],c['review'];shot=ROOT/s['path']
        require(shot.resolve().is_relative_to(p.parent.resolve()) and digest(shot)==s['sha256'] and d.get('frameSha256')==s['sha256'] and d.get('expected')==c['expected'] and d.get('observed'),'Invalid current original approval')
    audio=r.get('audioEvidence',{}).get('windows',[])
    require(audio and any(a.get('medianRms',0)>1e-5 for a in audio),'No actual audible PCM evidence: '+str(p))
    if game=='FantasyCards':
        require(any('/StreamingAssets/Audio/ModernCard/' in u for a in audio for u in a.get('sourceUrls',[])),'No bundled instrumental PCM source')
    return {'checks':sum(c.get('passed') is True for c in r['checks']),'approvedOriginals':len(approved),'screenshots':[c['original'] for c in approved],'audioWindows':len(audio),**ref(p),**({'rawStatus':'failed','adjudicatedPublicMediaRanges':len(adjudication['requests']),'adjudication':ref(p.parent/'range-adjudication.json')} if adjudication else {})}


def public(record,run):
    require(re.fullmatch(r'https://github.com/ka8877/firstgame/actions/runs/[0-9]+',run or ''),'Use a real successful Pages run URL')
    raw=subprocess.check_output(['gh','run','view',run.rsplit('/',1)[-1],'--json','status,conclusion,url,headSha'],cwd=ROOT,text=True);action=json.loads(raw)
    require(action.get('status')=='completed' and action.get('conclusion')=='success','Pages run has not succeeded')
    stage=ROOT/'Artifacts/WebPlaytest';manifest=json.loads((stage/'build-manifest.json').read_text())
    p=REPORTS/'PublicPortal/report.json';portal=json.loads(p.read_text());review=portal.get('visualReview',{})
    require(portal.get('status')=='passed' and review.get('status')=='approved' and review.get('reviewer') and review.get('reviewedUtc') and len(review.get('screenshots',[]))==2,'Public portal + both original reviews must pass')
    expected={**manifest['files'],'build-manifest.json':digest(stage/'build-manifest.json')}
    require(manifest.get('gameRevisions')==REVISIONS and manifest.get('sourceDirty') is False and portal.get('sourceCommit')==manifest.get('sourceCommit') and action.get('headSha')==manifest.get('sourceCommit'),'Current clean public manifest and Pages source required')
    actual=portal.get('files',[]);require(len(actual)==len(expected) and {f['file'] for f in actual}==set(expected),'Public file set differs')
    for f in actual:require(f.get('passed') is True and f.get('sha256')==expected.get(f['file']) and f.get('bytes')==(stage/f['file']).stat().st_size,'Public bytes differ: '+f['file'])
    originals={(s['file'],s['sha256']) for s in portal.get('screenshots',[])}
    for s in review['screenshots']:require((s.get('file',s.get('path','')),s['sha256']) in originals and digest(ROOT/s.get('file',s.get('path','')))==s['sha256'],'Public original changed')
    record['publicWeb']={g:browser(g,'PublicMobile844',record['builds'][g]['Web']['files']) for g in REVISIONS}
    return {'status':'verified','url':URL,'pagesRun':run,'sourceCommit':manifest['sourceCommit'],'verifiedFiles':len(actual),'scope':'Every public file is byte-exact to locally tested runtime; public title/game launch sanity, not duplicated full gameplay.',**ref(p)}


def markdown(record):
    pub=record['publication'];rows=[]
    for profile,games in record['web'].items():
        for game,r in games.items():rows.append(f"| {profile}/{game} | {r['checks']} | {r['approvedOriginals']} | {r['audioWindows']} |")
    for game,r in record.get('publicWeb',{}).items():rows.append(f"| PublicMobile844/{game} | {r['checks']} | {r['approvedOriginals']} | {r['audioWindows']} |")
    text='# 현대 UI·인물·로컬 음악 개정 검증\n\n'
    text+=f"기록일 {record['date']} (Asia/Seoul). `fm-modern-2026-10-26` / `gospel-modern-2026-10-26`. 이전 remaster 통과를 새 빌드 근거로 재사용하지 않는다.\n\n"
    text+=(f"[공개 테스트]({URL}) · [Pages 실행]({pub['pagesRun']}) · 공개 파일 {pub['verifiedFiles']}개 SHA-256 일치.\n\n" if pub['status']=='verified' else '현재 로컬 검증은 통과했으며 새 공개 배포 검증은 대기 중이다.\n\n')
    text+=f"Core {record['externalCore']['passed']}개, Unity 실행 검사 "+', '.join(f"{g} {r['passed']}개" for g,r in record['nativeTests'].items())+' 통과. 두 게임 Windows/Web 빌드 성공, 오류0.\n\n'
    text+='| 실제 브라우저 실행 | 통과 검사 | 직접 검토한 원본 | 실제 PCM 구간 |\n|---|---:|---:|---:|\n'+'\n'.join(rows)+'\n\n'
    text+='카드게임은 외부 음악 플레이어를 제거하고 번들 연주곡을 사용한다. 음소거·음악 재생과 외부 플레이어 요청 부재는 실제 PCM·소스 경로·요청 기록으로 구분하며 물리 스피커 청취나 사용자 미적 승인을 뜻하지 않는다. 예수님의 길은 실제 관절 모델과 휠/버튼 줌·상태창을 검토한다. 성경 본문·저장 schema는 유지한다.\n\n'
    if any(r.get('adjudicatedPublicMediaRanges') for r in record.get('publicWeb',{}).values()):text+='공개 카드게임의 자동 네트워크 검사는 HTTP206 음원 범위 요청 취소 5건 때문에 실패한 원본을 보존했다. 별도 해시 기반 분석으로 정확한 공개 파일·실제 디코딩·취소 전후 재생 진행·후속 PCM을 확인했다. 표의 공개 카드 검사 수는 원본 통과 항목만 센다. 다른 오류를 무시하거나 원본 실패를 통과로 수정하지 않았다.\n\n'
    text+='검증 환경은 소프트웨어 Chromium과 모바일 입력 에뮬레이션이다. 실물 휴대폰·Safari·Windows 하드웨어 성능 검증은 포함하지 않는다. 예수님의 생애 전체나 전투 피해 시스템이 구현되었다고 표시하지 않는다.\n'
    return text


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check-only',action='store_true');parser.add_argument('--pages-run');args=parser.parse_args()
    builds={g:{t:build(g,t) for t in ('Windows','Web')} for g in REVISIONS}
    record={'date':datetime.now(ZoneInfo('Asia/Seoul')).date().isoformat(),'recordedUtc':datetime.now(timezone.utc).isoformat(),'dateTimezone':'Asia/Seoul','gameRevisions':REVISIONS,'contentVersions':CONTENTS,'sourceCommitAtSummary':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'externalCore':core(),'nativeTests':{g:native(g) for g in REVISIONS},'builds':builds,'web':{p:{g:browser(g,p,builds[g]['Web']['files']) for g in REVISIONS} for p in ('Desktop720','Mobile844')},'publication':{'status':'pending','url':URL}}
    if args.pages_run:record['publication']=public(record,args.pages_run)
    if not args.check_only:
        history=ROOT/'Docs/ValidationHistory/2026-10-26-before-modern';history.mkdir(parents=True,exist_ok=True)
        for old in ('Docs/Validation.md','Docs/VisualValidation.md','Tools/Environment/validation.json'):
            original=ROOT/old;backup=history/Path(old).name
            if original.is_file() and not backup.exists():shutil.copyfile(original,backup)
        outputs={'Docs/ModernValidation.md':markdown(record),'Docs/Validation.md':markdown(record),'Docs/VisualValidation.md':markdown(record),'Tools/Environment/validation.json':json.dumps(record,ensure_ascii=False,indent=2)+'\n','Artifacts/Validation/Modern/summary.json':json.dumps(record,ensure_ascii=False,indent=2)+'\n'}
        for g in REVISIONS:outputs[f'Artifacts/Builds/{g}/Web/OutputFiles.json']=json.dumps({'revision':REVISIONS[g],'files':builds[g]['Web']['files']},indent=2)+'\n'
        for name,text in outputs.items():p=ROOT/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text)
    print(json.dumps({'status':'passed','checkOnly':args.check_only,'publication':record['publication']['status'],'core':record['externalCore']['passed'],'native':{g:r['passed'] for g,r in record['nativeTests'].items()}},ensure_ascii=False))


if __name__=='__main__':
    try:main()
    except (ValueError,KeyError,OSError,ET.ParseError,subprocess.CalledProcessError) as e:print('Modern summary refused: '+str(e),file=sys.stderr);sys.exit(1)
