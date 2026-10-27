#!/usr/bin/env python3
"""Package current verified Restoration players/source/evidence in one task-owned generated root."""
from __future__ import annotations
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/Validation/Restoration'
BUILD=Path('/tmp/firstgame-restoration-builds')
DEST=Path('/tmp/firstgame-restoration-deliverables')
spec=importlib.util.spec_from_file_location('restoration_package_base',ROOT/'Tools/Build/package-modern.py')
BASE=importlib.util.module_from_spec(spec);spec.loader.exec_module(BASE)
BASE.ART=DEST
BASE.REVISIONS={'FantasyCards':'fm-restoration-2026-10-27','PathOfDawn':'gospel-toon-2026-10-27'}


def archive_previous(name):
    path=DEST/name
    if path.exists():
        history=DEST/'Previous';history.mkdir(exist_ok=True)
        target=history/(path.stem+'-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')+path.suffix)
        path.rename(target)
BASE.archive_previous=archive_previous


def player_files(game,target):
    folder=BUILD/game/target
    text=(folder/'BuildEvidence.txt').read_text()
    BASE.require('Revision: '+BASE.REVISIONS[game] in text and 'Result: Succeeded' in text,'Current successful build required')
    return {game+'-'+target+'/'+str(p.relative_to(folder)):p for p in sorted(folder.rglob('*')) if p.is_file() and BASE.shipping(p.relative_to(folder))}


def notes(game,target):
    folder=BUILD/game/target
    text=BASE.REVISIONS[game]+'\nUnity6000.3.26f1\n\n'
    text+=(f'모든 파일을 풀고 {game}.exe를 실행하세요.\n' if target=='Windows' else '폴더 전체를 HTTP/HTTPS 서버에서 실행하세요. file:// 직접 실행은 지원하지 않습니다.\n')
    text+=('원본 인쇄 카드와 실제 능력 정보·로컬 시나리오/AI연습·별도 효과음 설정을 제공합니다. 멀티플레이는 준비 화면입니다.\n' if game=='FantasyCards' else '현재 탄생 도입부와 누가복음2장 성전 초반구간을 플레이합니다. WASD/Shift/Space/E/I, 우클릭시점·휠줌. 인물은 미술적 표현입니다.\n')
    (folder/'README.txt').write_text(text+'\n브라우저 음악/효과음은 첫 실제 입력이 필요합니다. 진행은현재기기에저장됩니다.\n')
    for name in ('PlaytestGuide.md','RestorationValidation.md'):
        p=ROOT/'Docs'/name
        if p.is_file():shutil.copyfile(p,folder/name)
    notices=folder/'ThirdParty';notices.mkdir(exist_ok=True)
    paths=[ROOT/'Tools/Audio/MUSIC-NOTICE.txt',ROOT/f'Games/{game}/Assets/Resources/Fonts/OFL.txt',ROOT/f'Games/{game}/Assets/Resources/Fonts/Pretendard-OFL.txt',ROOT/f'Games/{game}/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt']
    paths+=([ROOT/'Tools/Art/ClassicCards/SOURCE-NOTICE.txt',ROOT/'Tools/Research/FmRevival/SOURCE-NOTICE.txt',ROOT/'Tools/Audio/Restoration/SOURCE-NOTICE.txt'] if game=='FantasyCards' else
            [ROOT/'Tools/Art/Gospel3D/SOURCE-NOTICE.txt',ROOT/'Tools/Art/GospelAuthored/SOURCE-NOTICE.txt',ROOT/'Tools/Art/GospelAuthoredProps/SOURCE-NOTICE.txt',ROOT/'Tools/Art/GospelToon/SOURCE-NOTICE.txt',ROOT/'Tools/Art/GospelWell/SOURCE-NOTICE.txt',ROOT/'Tools/Art/GospelTemple/LICENSE-NOTICE.txt',ROOT/'Tools/Data/Gospel/SCRIPTURE-NOTICE.txt',ROOT/'Tools/Voice/VOICE-NOTICE.txt',ROOT/f'Games/{game}/Packages/com.inkle.ink-unity-integration/LICENCE.md'])
    for p in paths:
        BASE.require(p.is_file(),'Missing current required source notice: '+str(p))
        name=p.parent.name+'-'+p.name
        shutil.copyfile(p,notices/name)


def evidence_files():
    result={}
    for folder in (OUT,ROOT/'Tools/Art/GospelToon',ROOT/'Tools/Art/GospelWell',ROOT/'Tools/Audio/Restoration'):
        if folder.exists():
            for p in sorted(folder.rglob('*')):
                if p.is_file() and p.suffix.lower() in {'.json','.png','.md','.txt','.sha256','.trx','.xml','.py','.js','.cjs','.controller'} and p.name not in {'deliverables.json'}:
                    result[str(p.relative_to(ROOT))]=p
    for relative in ('Tools/Environment/validation.json','Docs/RestorationValidation.md','Tools/Validation/summarize-restoration-release.py','Tools/Validation/verify-restoration-gospel-followup.py','Tools/Validation/verify-restoration-fm-public-ranges.py','Tools/Validation/restoration-gospel-followup-plans.json','Tools/Validation/restoration-smoke-plans.json','Tools/Validation/restoration-mobile-plans.json','Tools/Validation/restoration-public-plans.json','Tools/Validation/revival-smoke.py','Tools/Validation/webaudio-observer.js','Tools/Build/package-restoration.py'):
        p=ROOT/relative
        if p.is_file():result[relative]=p
    for game in BASE.REVISIONS:
        for target in ('Windows','Web'):
            p=BUILD/game/target/'BuildEvidence.txt';result[f'Builds/{game}/{target}/BuildEvidence.txt']=p
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--part',choices=('all','players','source','evidence','verify'),default='all');a=parser.parse_args()
    if a.part in ('all','players'):
        for game in BASE.REVISIONS:
            for target in ('Windows','Web'):notes(game,target)
    subprocess.run([sys.executable,str(ROOT/'Tools/Validation/summarize-restoration-release.py'),'--public'],cwd=ROOT,check=True)
    record=json.loads((OUT/'summary.json').read_text())
    BASE.require(record.get('releaseProfile')=='restoration' and record['gameRevisions']==BASE.REVISIONS and record['publication']['status']=='verified','Finish current local/public verification first')
    DEST.mkdir(parents=True,exist_ok=True)
    marker=DEST/'.firstgame-restoration-deliverables';marker.write_text('Generated by Tools/Build/package-restoration.py\n')
    link=ROOT/'Artifacts/RestorationDeliverables'
    BASE.require(not link.exists() or link.is_symlink() and link.resolve()==DEST,'Unexpected deliverables link')
    if not link.is_symlink():link.symlink_to(DEST,target_is_directory=True)
    sets={f'{game}-{target}.zip':player_files(game,target) for game in BASE.REVISIONS for target in ('Windows','Web')}
    sets['FirstGame-UnitySources.zip']=BASE.source_files();sets['FirstGame-ValidationEvidence.zip']=evidence_files()
    selected=list(sets) if a.part in ('all','verify') else [n for n in sets if '-Windows.zip' in n or '-Web.zip' in n] if a.part=='players' else ['FirstGame-UnitySources.zip'] if a.part=='source' else ['FirstGame-ValidationEvidence.zip']
    rows=[BASE.verify(DEST/name,sets[name]) if a.part=='verify' else BASE.write_archive(name,sets[name]) for name in selected]
    if a.part in ('all','verify'):
        value={'status':'passed','releaseProfile':'restoration','gameRevisions':BASE.REVISIONS,'actualRoot':str(DEST),'artifactPath':str(link),'checkedUtc':datetime.now(timezone.utc).isoformat(),'archives':rows,'scope':'Exact file set/current bytes/CRC/LFS/credential-path check; physical hardware performance not claimed.'}
        (OUT/'deliverables.json').write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
        (DEST/'Deliverables.sha256').write_text(''.join(row['sha256']+'  '+row['archive']+'\n' for row in rows))
    print(json.dumps({'status':'passed','part':a.part,'actualRoot':str(DEST),'archives':rows}))

if __name__=='__main__':main()
