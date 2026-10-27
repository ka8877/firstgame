#!/usr/bin/env python3
"""Package the verified two-game arcana revision and exact compact evidence."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[2]; ART=ROOT/'Artifacts'; OUT=ART/'Validation/Arcana'
spec=importlib.util.spec_from_file_location('arcana_package_contract',ROOT/'Tools/Build/package-modern.py')
BASE=importlib.util.module_from_spec(spec); spec.loader.exec_module(BASE)
BASE.REVISIONS={'FantasyCards':'fm-arcana-2026-10-27','PathOfDawn':'gospel-authored-2026-10-27'}


def archive_previous(name):
    path=ART/name
    if path.exists():
        folder=ART/'PreviousDeliverables/2026-10-27-before-arcana'; folder.mkdir(parents=True,exist_ok=True)
        target=folder/name
        if target.exists(): target=folder/(path.stem+'-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+path.suffix)
        path.rename(target)


BASE.archive_previous=archive_previous


def notes(game,target):
    folder=ART/'Builds'/game/target
    launch=(f'압축을 모두 푼 뒤 {game}.exe를 실행하세요. DLL과 데이터 폴더도 함께 필요합니다.\n' if target=='Windows' else
            '폴더 전체를 HTTP 웹 서버에서 실행하세요. file:// 직접 실행은 지원하지 않습니다.\n')
    controls=('게임 시작 → AI 연습방 → 덱 선택 → 대전 시작. 수록 카드·등급은 실제 도감에서 확인합니다.\n'
              '로컬 프로필은 이 기기의 기록이며 온라인 계정·화폐가 아닙니다. 지형은 손패에서 지형 띠로 끌어놓습니다.\n'
              'SOUND-I 원곡의 말소리 없는 발췌 루프와 S.I.D-Sound 공개 원작 전투곡을 사용합니다.\n'
              if game=='FantasyCards' else
              '탄생 말씀 세 페이지 뒤 소년 예수님으로 성전 첫 구간을 탐색합니다.\n'
              'WASD 이동, Shift 달리기, Space 점프, 우클릭 시점, 휠 줌, I 상태, E 대화. 모바일은 화면 버튼을 사용합니다.\n'
              '새 인물은 미술적 해석입니다. 다른 생애 장은 준비 중이며 동작 연습은 성경 진행과 별개입니다.\n')
    (folder/'README.txt').write_text(BASE.REVISIONS[game]+'\nUnity6000.3.26f1\n\n'+launch+controls+
                                  '\n브라우저 음악은 첫 실제 클릭/터치가 필요합니다. 진행은 현재 기기에 저장됩니다.\n')
    for name in ('PlaytestGuide.md','ArcanaValidation.md'):
        path=ROOT/'Docs'/name
        if path.is_file(): shutil.copyfile(path,folder/name)
    notices=folder/'ThirdParty'; notices.mkdir(exist_ok=True)
    sources={'FirstGameKorean-OFL.txt':f'Games/{game}/Assets/Resources/Fonts/OFL.txt',
             'Pretendard-OFL.txt':f'Games/{game}/Assets/Resources/Fonts/Pretendard-OFL.txt',
             'Music-NOTICE.txt':'Tools/Audio/MUSIC-NOTICE.txt'}
    sources.update({'ClassicCards-SOURCE-NOTICE.txt':'Tools/Art/ClassicCards/SOURCE-NOTICE.txt'} if game=='FantasyCards' else
                   {'Gospel3D-SOURCE-NOTICE.txt':'Tools/Art/Gospel3D/SOURCE-NOTICE.txt',
                    'TempleMaterials-NOTICE.txt':'Tools/Art/GospelTemple/LICENSE-NOTICE.txt',
                    'Scripture-NOTICE.txt':'Tools/Data/Gospel/SCRIPTURE-NOTICE.txt',
                    'AuthoredProps-NOTICE.txt':'Tools/Art/GospelAuthoredProps/SOURCE-NOTICE.txt'})
    for name,relative in sources.items():
        path=ROOT/relative; BASE.require(path.is_file(),'Missing notice: '+relative); shutil.copyfile(path,notices/name)
    extra=ROOT/('Tools/Art/FmArcana/SOURCE-NOTICE.txt' if game=='FantasyCards' else 'Tools/Art/GospelAuthored/SOURCE-NOTICE.txt')
    if extra.is_file(): shutil.copyfile(extra,notices/('TitleArt-SOURCE-NOTICE.txt' if game=='FantasyCards' else 'NaturalActors-SOURCE-NOTICE.txt'))
    license=ROOT/'Tools/Art/GospelNatural/Source/LICENSE.ASSETS.md'
    if game=='PathOfDawn' and license.is_file(): shutil.copyfile(license,notices/'NaturalAssets-CC0.md')


def evidence():
    selected=set(); result={}
    native=json.loads((OUT/'Nativecritical-review.json').read_text())
    for screenshot in native['screenshots']:
        path=ROOT/screenshot['path']
        BASE.require(path.is_file() and BASE.digest(path)==screenshot['sha256'],'Native reviewed original changed')
        result[screenshot['path']]=path
    for p in OUT.rglob('report.json'):
        if any('.previous-' in part for part in p.relative_to(OUT).parts): continue
        report=json.loads(p.read_text())
        for checkpoint in report.get('checkpoints',[]):
            if checkpoint.get('status')=='approved':
                selected.add(checkpoint['original']['path'])
                selected.update(s['path'] for s in checkpoint.get('review',{}).get('supportingOriginals',[]))
        for shot in report.get('visualReview',{}).get('screenshots',[]): selected.add(shot.get('path',shot.get('file','')))
    for p in sorted(OUT.rglob('*')):
        relative=p.relative_to(OUT)
        if (any('.previous-' in part for part in relative.parts) or p.name in {'deliverables.json','current-work.json'}): continue
        if p.suffix.lower()=='.png' and '-compositor-' in p.name and str(p.relative_to(ROOT)) not in selected: continue
        if p.is_file() and p.suffix.lower() in {'.json','.png','.txt','.md','.sha256','.xml','.py'}: result[str(p.relative_to(ROOT))]=p
    for game in BASE.REVISIONS:
        p=ART/f'TestResults/{game}/PlayModeUI.xml'; result[str(p.relative_to(ROOT))]=p
        for target in ('Windows','Web'):
            p=ART/f'Builds/{game}/{target}/BuildEvidence.txt'; result[str(p.relative_to(ROOT))]=p
            if game == 'PathOfDawn':
                p=ART/f'Builds/{game}/{target}/LegacyResourceExclusions.json'; result[str(p.relative_to(ROOT))]=p
    for relative in ('Artifacts/TestResults/Core/core-tests.trx','Docs/ArcanaValidation.md','Docs/FmArcanaUI.md',
                     'Docs/GospelAuthoredArt.md','Docs/ArcanaBuildBudget.md','Docs/GospelNpcConversations.md','Docs/FmArcanaCards.md',
                     'Tools/Environment/validation.json','Tools/Validation/arcana-smoke-plans.json',
                     'Tools/Validation/arcana-public-plans.json','Tools/Validation/summarize-arcana-release.py',
                     'Tools/Validation/revival-smoke.py','Tools/Validation/webaudio-observer.js',
                     'Tools/Validation/fm-local-media-observer.js','Tools/Validation/fm-local-media-errors.py',
                     'Tools/Validation/adjudicate-fm-public-ranges.py','Tools/Web/release_contract.py',
                     'Tools/Build/package-arcana.py','Docs/FmArcanaMusic.md','Docs/FmArcanaArt.md',
                     'Tools/Audio/Arcana/verify-native-lifecycle.py','Tools/Validation/test-fm-local-music.cjs',
                     'Tools/Art/GospelAuthoredProps/material-spec.json','Tools/Validation/verify-resource-isolation.py'):
        p=ROOT/relative
        if p.is_file(): result[relative]=p
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--part',choices=('all','players','source','evidence','verify'),default='all')
    args=parser.parse_args(); record=json.loads((OUT/'summary.json').read_text())
    BASE.require(record.get('releaseProfile')=='arcana' and record.get('gameRevisions')==BASE.REVISIONS
                 and record.get('publication',{}).get('status')=='verified','Finish current local/public arcana summary')
    if args.part in ('all','players'):
        for game in BASE.REVISIONS:
            for target in ('Windows','Web'): notes(game,target)
    sets={f'{game}-{target}.zip':BASE.player_files(game,target) for game in BASE.REVISIONS for target in ('Windows','Web')}
    sets['FirstGame-UnitySources.zip']=BASE.source_files(); sets['FirstGame-ValidationEvidence.zip']=evidence()
    selected=(list(sets) if args.part in ('all','verify') else
              [name for name in sets if '-Windows.zip' in name or '-Web.zip' in name] if args.part=='players' else
              ['FirstGame-UnitySources.zip'] if args.part=='source' else ['FirstGame-ValidationEvidence.zip'])
    if args.part=='all':
        old=ART/'Deliverables.sha256'; copy=ART/'PreviousDeliverables/2026-10-27-before-arcana/Deliverables.sha256'
        if old.is_file() and not copy.exists(): copy.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(old,copy)
    rows=[BASE.verify(ART/name,sets[name]) if args.part=='verify' else BASE.write_archive(name,sets[name]) for name in selected]
    if args.part in ('all','verify'):
        result={'status':'passed','releaseProfile':'arcana','checkedUtc':datetime.now(timezone.utc).isoformat(),
                'gameRevisions':BASE.REVISIONS,'publication':record['publication'],'archives':rows,
                'scope':'Exact current ZIP bytes, CRC, fileset, resolved LFS and credential-path checks. Hardware execution is separate.'}
        (OUT/'deliverables.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
        (ART/'Deliverables.sha256').write_text(''.join(row['sha256']+'  '+row['archive']+'\n' for row in rows))
    print(json.dumps({'status':'passed','part':args.part,'archives':rows},ensure_ascii=False))


if __name__=='__main__': main()
