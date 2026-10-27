#!/usr/bin/env python3
"""Summarize fresh two-game detail builds, real input and original visual reviews.

Uses the established immutable runtime/original/PCM checks. Does not build,
launch, publish or change failed reports. Earlier releases remain history.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'Artifacts/Validation/Detail'
sys.path.insert(0, str(ROOT / 'Tools/Web'))
from release_contract import DETAIL
spec = importlib.util.spec_from_file_location('detail_validation_contract', ROOT / 'Tools/Validation/summarize-modern-release.py')
BASE = importlib.util.module_from_spec(spec); spec.loader.exec_module(BASE)
BASE.REVISIONS = DETAIL['revisions']; BASE.CONTENTS = DETAIL['contents']; BASE.REPORTS = REPORTS


def markdown(record):
    pub = record['publication']
    text = '# 카드 리마스터 세부·자연스러운 성전 그래픽 검증\n\n'
    text += f"기록일 {record['date']} (Asia/Seoul). `fm-detail-2026-10-27` / `gospel-natural-2026-10-27`. 두 게임 모두 새 빌드와 실제 입력을 확인한다.\n\n"
    if pub['status'] == 'verified':
        text += f"[공개 테스트]({BASE.URL}) · [Pages 실행]({pub['pagesRun']}) · 공개 파일 {pub['verifiedFiles']}개 바이트 일치.\n\n"
    else:
        text += '현재 로컬 검증을 기록했으며 새 공개 배포 확인은 대기 중이다.\n\n'
    text += f"Core 회귀 {record['externalCore']['passed']}개, Unity 실행 검사 " + ', '.join(f"{game} {row['passed']}개" for game,row in record['nativeTests'].items()) + ' 통과. 두 게임 Windows/Web 빌드 성공.\n\n'
    text += '| 실제 브라우저 경로 | 통과 검사 | 직접 검토한 원본 | 실제 PCM 구간 |\n|---|---:|---:|---:|\n'
    for profile,games in record['web'].items():
        for game,row in games.items():
            sessions = ' · 정상 시작2회' if row.get('ordinarySessions') == 2 else ''
            text += f"| {profile}/{game}{sessions} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    for game,row in record.get('publicWeb',{}).items():
        text += f"| PublicMobile844/{game} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    text += '\n카드의 원작 카드·등급·저장 호환과 실제 전투 이벤트 재생을 유지한다. 새 타이틀 그림은 새 제작 원화이며, 추가 카드의 원본과 기술 근거는 별도 조사 기록을 따른다. 지형과 로컬 프로필은 실제 실행 상태와 구분해 검토한다.\n\n'
    text += '음악은 SOUND-I 원작 공개 녹음의 말소리 없는 발췌 루프와 S.I.D-Sound 공개 원작 전투곡을 유지한다. 원곡 전체를 새로 복구했다고 표시하지 않는다. 실제 브라우저 PCM과 물리 스피커 청취는 다르다.\n\n'
    if any(row.get('adjudicatedCacheRevalidations') for games in record['web'].values() for row in games.values()):
        text += '카드 PC 실행의 자동 네트워크 검사는 새 프로필 복원 체크포인트의 옛 이름 때문에 캐시 요청 하나를 미분류해 실패한 원본을 보존했다. 원래 계획 SHA·현재 런타임·정확한 데이터 URL·HTTP304·Unity 캐시 성공 기록과 직접 본 프로필/전장 복원을 별도 해시 기반 분석으로 대조했다. 자동 검사 전체를 통과했다고 변경하지 않는다.\n\n'
    if any(row.get('adjudicatedLocalMediaRequests') for games in record['web'].values() for row in games.values()):
        text += '카드 모바일 자동 네트워크 검사도 정확한 로컬 음원 요청 취소3건 때문에 실패한 원본을 보존했다. 별도 분석은 로비의 같은 소스 루프 재시작·디코딩 진행, 재로드 경계의 미리 디코딩된 음원과 이후 전투 PCM을 파일 해시·원본 계획·타임라인으로 대조한다. 해당 루프 직후의 PCM 연속성이나 재로드 뒤 타이틀의 별도 PCM을 측정했다고 주장하지 않는다.\n\n'
    text += '예수님의 길은 실제 관절·스킨 모델과 입체 성전의 새 인물·재질·조명을 검토한다. 얼굴은 미술적 해석이며 실제 역사적 얼굴 복원이 아니다. 성경 본문·진행·저장 버전을 유지하고 탄생 도입과 소년 성전 구간을 확인한다.\n\n'
    text += '작은 모바일 화면은 인물의 전체 형태·머리/목 경계를 검토하고 미세 눈꺼풀·피부 표현은 PC 정면 원본으로 보완한다. 뒤쪽 벽에 가까워질 때 카메라가 가까워지는 장면은 원본에 기록했다. 저장은 인물 위치·향하는 방향·목표·본문을 복원하며 카메라 방향은 저장된 인물 방향에서 시작한다. 모바일 위치 비교에는 실제 시점 회전만 사용했다.\n\n'
    if any(row.get('ordinarySessions') == 2 for games in record['web'].values() for row in games.values()):
        text += '예수님 게임 PC 검증은 같은 런타임의 두 번의 정상 새 시작으로 구성한다. 첫 실행은 타이틀·성전·후면·정면 얼굴·상태·음악을 직접 확인한 뒤 짧은 화면 캡처 준비 시간에 PNG0장으로 중단했다. 실패 원본을 보존하고, 캡처 첫 프레임을 기다린 후 별도 정상 새 시작에서 걷기·달리기·대화·저장·이어하기만 이어서 확인했다. 상태나 저장을 주입하거나 한 번의 연속 실행으로 표시하지 않는다.\n\n'
    if any(row.get('adjudicatedPublicMediaRanges') for row in record.get('publicWeb',{}).values()):
        text += '공개 음원 범위 요청 취소로 실패한 원본 보고서는 보존한다. 정확한 파일 해시·디코딩·전후 재생 진행·후속 PCM 근거가 있는 요청만 별도 분석하며 원본 실패를 통과로 변경하지 않는다.\n\n'
    text += '검증 환경은 소프트웨어 Chromium과 모바일 입력 에뮬레이션이다. 실물 기기·Safari·Windows 하드웨어 성능이나 사용자의 미적 승인을 뜻하지 않는다. 판마 전체 클라이언트·모든 카드 예외·예수님 생애 전체를 복원한 것으로 표시하지 않는다.\n'
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-only', action='store_true'); parser.add_argument('--pages-run')
    args = parser.parse_args()
    builds = {game:{target:BASE.build(game,target) for target in ('Windows','Web')} for game in DETAIL['revisions']}
    record = {'releaseProfile':'detail', 'date':datetime.now(ZoneInfo('Asia/Seoul')).date().isoformat(),
              'recordedUtc':datetime.now(timezone.utc).isoformat(), 'dateTimezone':'Asia/Seoul',
              'gameRevisions':DETAIL['revisions'], 'contentVersions':DETAIL['contents'],
              'sourceCommitAtSummary':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
              'externalCore':BASE.core(), 'nativeTests':{game:BASE.native(game) for game in DETAIL['revisions']},
              'builds':builds, 'web':{profile:{game:BASE.browser(game,profile,builds[game]['Web']['files']) for game in DETAIL['revisions']} for profile in ('Desktop720','Mobile844')},
              'publication':{'status':'pending','url':BASE.URL}}
    if args.pages_run:
        record['publication'] = BASE.public(record,args.pages_run)
    if not args.check_only:
        history = ROOT/'Docs/ValidationHistory/2026-10-27-before-detail'; history.mkdir(parents=True,exist_ok=True)
        for old in ('Docs/Validation.md','Docs/VisualValidation.md','Tools/Environment/validation.json'):
            original=ROOT/old; backup=history/Path(old).name
            if original.is_file() and not backup.exists(): shutil.copyfile(original,backup)
        outputs = {'Docs/DetailValidation.md':markdown(record), 'Docs/Validation.md':markdown(record),
                   'Docs/VisualValidation.md':markdown(record),
                   'Tools/Environment/validation.json':json.dumps(record,ensure_ascii=False,indent=2)+'\n',
                   'Artifacts/Validation/Detail/summary.json':json.dumps(record,ensure_ascii=False,indent=2)+'\n'}
        for game in DETAIL['revisions']:
            outputs[f'Artifacts/Builds/{game}/Web/OutputFiles.json']=json.dumps({'revision':DETAIL['revisions'][game],'files':builds[game]['Web']['files']},indent=2)+'\n'
        for name,content in outputs.items():
            path=ROOT/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_text(content)
    print(json.dumps({'status':'passed','checkOnly':args.check_only,'publication':record['publication']['status'],
                      'core':record['externalCore']['passed'],'native':{game:row['passed'] for game,row in record['nativeTests'].items()}},ensure_ascii=False))


if __name__=='__main__': main()
