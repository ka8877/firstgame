#!/usr/bin/env python3
"""Package the current modern players, sources and compact evidence; verify every ZIP byte/CRC.

Never builds, launches, commits or publishes. Old archives are moved intact to
PreviousDeliverables once before replacement. Modern evidence excludes unrelated
historical screenshot trees instead of duplicating gigabytes of past captures.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys
import zipfile

ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'Artifacts'
REVISIONS={'FantasyCards':'fm-modern-2026-10-26','PathOfDawn':'gospel-modern-2026-10-26'}
EXCLUDED={'.git','Artifacts','Library','Temp','Logs','UserSettings','obj','bin','__pycache__','.aws','.ssh','.gnupg','.config','.credentials','.codex','.agents'}
SECRET_NAMES={'auth.json','credentials','credentials.json','id_rsa','id_ed25519'}


def require(ok,message):
    if not ok:raise ValueError(message)


def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()


def shipping(path):
    return not any('DoNotShip' in p or 'DontShip' in p for p in path.parts) and path.suffix.lower() not in {'.log','.map','.pdb','.mdb'}


def safe(path):
    require(not path.is_symlink(),'Review symlink before packaging: '+str(path))
    require(not any(p in {'.git','.aws','.ssh','.credentials','.codex'} for p in path.parts),'Credential directory in archive')
    require(path.name.lower() not in SECRET_NAMES and not path.name.startswith('.env') and path.suffix.lower() not in {'.ulf','.alf','.pem','.key'},'Credential/license artifact in archive')
    require(not(path.stat().st_size<512 and path.read_bytes().startswith(b'version https://git-lfs.github.com/spec/v1')),'Unresolved LFS pointer: '+str(path))


def source_files():
    result={}
    for directory,folders,names in os.walk(ROOT):
        folders[:]=sorted(f for f in folders if f not in EXCLUDED)
        for f in folders:require(not(Path(directory)/f).is_symlink(),'Review source symlink')
        for name in sorted(names):
            p=Path(directory)/name
            if p.suffix.lower() in {'.log','.pyc','.ulf','.alf','.pem','.key'} or name.lower() in SECRET_NAMES or name.startswith('.env'):continue
            safe(p);result['FirstGame/'+str(p.relative_to(ROOT))]=p
    return result


def evidence_files():
    result={}
    for parent in [ART/'Validation/Modern',ROOT/'Tools/Art/ModernRemaster',ROOT/'Tools/Art/Gospel3D/Modern',ROOT/'Tools/Audio/ModernCards']:
        if parent.exists():
            for p in sorted(parent.rglob('*')):
                if p.is_file() and p.suffix.lower() in {'.json','.png','.txt','.md','.sha256','.py','.js'} and p.name not in {'deliverables.json','current-work.json'}:result[str(p.relative_to(ROOT))]=p
    for game in REVISIONS:
        for p in sorted((ART/f'TestResults/{game}').glob('PlayModeUI.xml')):result[str(p.relative_to(ROOT))]=p
        for target in ('Windows','Web'):
            p=ART/f'Builds/{game}/{target}/BuildEvidence.txt';result[str(p.relative_to(ROOT))]=p
    for rel in ['Artifacts/TestResults/Core/core-tests.trx','Tools/Environment/validation.json','Docs/ModernValidation.md','Docs/ModernTypography.md','Docs/AudioModernization.md','Tools/Validation/modern-smoke-plans.json','Tools/Validation/modern-public-plans.json','Tools/Validation/summarize-modern-release.py','Tools/Validation/revival-smoke.py','Tools/Validation/webaudio-observer.js','Tools/Validation/fm-local-media-observer.js','Tools/Validation/fm-local-media-errors.py','Tools/Validation/test-fm-local-media-errors.py','Tools/Build/package-modern.py']:
        p=ROOT/rel
        if p.is_file():result[rel]=p
    return result


def player_files(game,target):
    folder=ART/f'Builds/{game}/{target}'
    evidence=folder/'BuildEvidence.txt';s=evidence.read_text()
    require('Result: Succeeded' in s and 'Revision: '+REVISIONS[game] in s,'Current successful player required')
    return {game+'-'+target+'/'+str(p.relative_to(folder)):p for p in sorted(folder.rglob('*')) if p.is_file() and shipping(p.relative_to(folder))}


def player_notes(game,target):
    folder=ART/f'Builds/{game}/{target}'
    launch=(f'압축을 모두 푼 뒤 {game}.exe를 실행하세요. DLL과 데이터 폴더도 함께 필요합니다.\n' if target=='Windows' else
            '폴더 전체를 HTTP 웹 서버에서 실행하세요. file:// 직접 실행은 지원하지 않습니다.\n')
    controls=('게임 시작 → 연습방 → 덱 선택 → 대전 시작. 161종 카드와 네 개의 기본 덱을 제공합니다.\n음악은 번들 연주곡이며 외부 광고·안내 음성 플레이어를 사용하지 않습니다. 최초 사용자 클릭/터치가 필요합니다.\n' if game=='FantasyCards' else
              '탄생 말씀 세 페이지 뒤 소년 예수님으로 성전 첫 구간을 탐색합니다.\nWASD 이동, Shift 달리기, Space 점프, 우클릭 시점, 휠 줌, I 상태, E 대화. 모바일은 화면 버튼을 사용합니다.\n나머지 생애 장은 준비 중이며 동작 연습은 전투 피해/성경 진행과 별개입니다.\n')
    (folder/'README.txt').write_text(REVISIONS[game]+'\nUnity 6000.3.26f1\n\n'+launch+controls+'\n진행은 현재 기기에 저장됩니다. 상세 범위와 실제 검증은 동봉 문서를 확인하세요.\n')
    for name in ('PlaytestGuide.md','ModernValidation.md'):
        p=ROOT/'Docs'/name
        if p.is_file():shutil.copyfile(p,folder/name)
    notices=folder/'ThirdParty';notices.mkdir(exist_ok=True)
    inputs={
        'FirstGameKorean-OFL.txt':ROOT/f'Games/{game}/Assets/Resources/Fonts/OFL.txt',
        'Pretendard-OFL.txt':ROOT/f'Games/{game}/Assets/Resources/Fonts/Pretendard-OFL.txt',
        'Music-NOTICE.txt':ROOT/'Tools/Audio/MUSIC-NOTICE.txt',
    }
    if game=='FantasyCards':inputs['ClassicCards-SOURCE-NOTICE.txt']=ROOT/'Tools/Art/ClassicCards/SOURCE-NOTICE.txt'
    else:
        inputs['Gospel3D-SOURCE-NOTICE.txt']=ROOT/'Tools/Art/Gospel3D/SOURCE-NOTICE.txt'
        inputs['Scripture-NOTICE.txt']=ROOT/'Tools/Data/Gospel/SCRIPTURE-NOTICE.txt'
        inputs['TempleMaterials-NOTICE.txt']=ROOT/'Tools/Art/GospelTemple/LICENSE-NOTICE.txt'
    for name,p in inputs.items():require(p.is_file(),'Missing player notice '+str(p));shutil.copyfile(p,notices/name)


def archive_previous(name):
    p=ART/name
    if p.exists():
        previous=ART/'PreviousDeliverables/2026-10-26-before-modern';previous.mkdir(parents=True,exist_ok=True)
        target=previous/name
        if target.exists():
            # Retain earlier attempts too; do not destroy a user's existing archive.
            target=previous/(p.stem+'-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+p.suffix)
        p.rename(target)


def write_archive(name,entries):
    archive_previous(name);output=ART/name
    for path in entries.values():require(path.is_file(),'Missing package input '+str(path));safe(path)
    with zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for name,path in sorted(entries.items()):z.write(path,name)
    return verify(output,entries)


def verify(archive,entries):
    require(archive.is_file(),'Missing archive '+str(archive))
    with zipfile.ZipFile(archive) as z:
        require(z.testzip() is None,'ZIP CRC failed: '+archive.name)
        require(set(z.namelist())==set(entries) and len(z.namelist())==len(entries),'Archive file set differs: '+archive.name)
        for name,path in entries.items():
            safe(path);h=hashlib.sha256()
            with z.open(name) as f:
                for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
            require(h.hexdigest()==digest(path),'Archive differs from current bytes: '+name)
    return {'archive':archive.name,'files':len(entries),'bytes':archive.stat().st_size,'sha256':digest(archive),'status':'passed'}


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--part',choices=('all','players','source','evidence','verify'),default='all');args=parser.parse_args()
    record=json.loads((ROOT/'Tools/Environment/validation.json').read_text())
    require(record.get('gameRevisions')==REVISIONS and record.get('publication',{}).get('status')=='verified','Finish current local/public summary before release packaging')
    if args.part in ('all','players'):
        for g in REVISIONS:
            for t in ('Windows','Web'):player_notes(g,t)
    sets={f'{g}-{t}.zip':player_files(g,t) for g in REVISIONS for t in ('Windows','Web')}
    sets['FirstGame-UnitySources.zip']=source_files();sets['FirstGame-ValidationEvidence.zip']=evidence_files()
    # This verifier result is outside the evidence selection, avoiding self-hash cycles.
    selected=list(sets) if args.part in ('all','verify') else [n for n in sets if ('-Windows.zip' in n or '-Web.zip' in n)] if args.part=='players' else ['FirstGame-UnitySources.zip'] if args.part=='source' else ['FirstGame-ValidationEvidence.zip']
    rows=[verify(ART/name,sets[name]) if args.part=='verify' else write_archive(name,sets[name]) for name in selected]
    if args.part in ('all','verify'):
        result={'status':'passed','checkedUtc':datetime.now(timezone.utc).isoformat(),'gameRevisions':REVISIONS,'publication':record['publication'],'archives':rows,'scope':'Exact current bytes, CRC, file set, LFS and credential-path checks; hardware execution is separate.'}
        (ART/'Validation/Modern/deliverables.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
        (ART/'Deliverables.sha256').write_text(''.join(r['sha256']+'  '+r['archive']+'\n' for r in rows))
    print(json.dumps({'status':'passed','part':args.part,'archives':rows},ensure_ascii=False))


if __name__=='__main__':
    try:main()
    except (ValueError,OSError,KeyError,zipfile.BadZipFile) as e:print('Modern package refused: '+str(e),file=sys.stderr);sys.exit(1)
