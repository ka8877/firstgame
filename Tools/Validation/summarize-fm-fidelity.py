#!/usr/bin/env python3
"""Summarize the FC-only fidelity checks and explicitly unchanged Gospel bytes.

Reuses the existing checksum/input/original-frame validators. Never launches,
builds, publishes, edits a failed report, or presents prior Gospel checks as new.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'Artifacts/Validation/FmFidelity'
sys.path.insert(0, str(ROOT / 'Tools/Web'))
from release_contract import FIDELITY, manifest_contract


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


BASE = load('modern_validation_contract', ROOT / 'Tools/Validation/summarize-modern-release.py')
BASE.REVISIONS = FIDELITY['revisions']
BASE.CONTENTS = FIDELITY['contents']
BASE.REPORTS = REPORTS
PRESERVE = load('gospel_preservation', ROOT / 'Tools/Validation/preserve-gospel.py')
URL = BASE.URL


def preserved():
    record = PRESERVE.verify()
    return {'revision': record['revision'], 'status': 'unchanged-previously-verified',
            'publicFiles': len(record['publicFiles']), 'sourceFiles': len(record['sourceFiles']),
            'playerFiles': len(record['playerFiles']), 'archives': len(record['archives']),
            'previousPublication': record['previousVerifiedPublication'],
            'scope': record['scope'], **BASE.ref(PRESERVE.RECORD)}


def publication(record, run):
    BASE.require(re.fullmatch(r'https://github.com/ka8877/firstgame/actions/runs/[0-9]+', run or ''),
                 'Use a real completed Pages run URL')
    action = json.loads(subprocess.check_output(
        ['gh', 'run', 'view', run.rsplit('/', 1)[-1], '--json', 'status,conclusion,url,headSha'],
        cwd=ROOT, text=True))
    BASE.require(action.get('status') == 'completed' and action.get('conclusion') == 'success',
                 'Pages workflow must succeed')
    stage = ROOT / 'Artifacts/WebPlaytest'
    manifest = json.loads((stage / 'build-manifest.json').read_text())
    release = manifest_contract(manifest)
    BASE.require(release['name'] == 'fm-fidelity' and manifest['sourceDirty'] is False,
                 'A clean fidelity stage is required')
    p = REPORTS / 'PublicPortal/report.json'
    portal = json.loads(p.read_text()); review = portal.get('visualReview', {})
    BASE.require(portal.get('status') == 'passed' and review.get('status') == 'approved'
                 and review.get('reviewer') and review.get('reviewedUtc')
                 and len(review.get('screenshots', [])) == 2,
                 'Public files and two original portal captures must be verified')
    expected = {**manifest['files'], 'build-manifest.json': BASE.digest(stage / 'build-manifest.json')}
    rows = portal.get('files', [])
    BASE.require(len(rows) == len(expected) and {r['file'] for r in rows} == set(expected),
                 'Public file set differs')
    for row in rows:
        BASE.require(row.get('passed') is True and row.get('sha256') == expected[row['file']]
                     and row.get('bytes') == (stage / row['file']).stat().st_size,
                     'Public bytes differ: ' + row['file'])
    BASE.require(portal['sourceCommit'] == manifest['sourceCommit'] == action['headSha'],
                 'Public manifest and successful Pages source differ')
    originals = {(s['file'], s['sha256']) for s in portal['screenshots']}
    for shot in review['screenshots']:
        name = shot.get('file', shot.get('path', ''))
        BASE.require((name, shot['sha256']) in originals and BASE.digest(ROOT / name) == shot['sha256'],
                     'Portal reviewed original changed')
    frozen = json.loads(PRESERVE.RECORD.read_text())
    for row in frozen['publicFiles']:
        BASE.require(manifest['files'].get(row['file']) == row['sha256'],
                     'Staging altered preserved Gospel: ' + row['file'])
    record['publicWeb'] = {'FantasyCards': BASE.browser(
        'FantasyCards', 'PublicMobile844', record['builds']['FantasyCards']['Web']['files'])}
    return {'status': 'verified', 'url': URL, 'pagesRun': run,
            'sourceCommit': manifest['sourceCommit'], 'verifiedFiles': len(rows),
            'scope': 'New FC public launch and byte verification; Gospel files preserved exactly without re-execution.',
            **BASE.ref(p)}


def markdown(record):
    pub = record['publication']
    text = '# 판타지마스터즈 원작 충실성 개정 검증\n\n'
    text += '`fm-fidelity-2026-10-27`. 이번 실행·빌드·브라우저 검증의 대상은 판타지마스터즈다.\n\n'
    if pub['status'] == 'verified':
        text += f"[공개 테스트]({URL}) · [Pages 실행]({pub['pagesRun']}) · 공개 파일 {pub['verifiedFiles']}개 바이트 일치.\n\n"
    else:
        text += '현재 FC 로컬 검증을 기록했으며 새 공개 배포 확인은 대기 중이다.\n\n'
    text += f"Core 회귀 {record['externalCore']['passed']}개, FC Unity 실행 검사 {record['nativeTests']['FantasyCards']['passed']}개 통과. FC Windows/Web 빌드 성공.\n\n"
    text += '| FC 브라우저 경로 | 통과 검사 | 직접 검토한 원본 | 실제 PCM 구간 |\n|---|---:|---:|---:|\n'
    for profile, games in record['web'].items():
        row = games['FantasyCards']; text += f"| {profile} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    for game, row in record.get('publicWeb', {}).items():
        text += f"| PublicMobile844/{game} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    text += '\n예수님의 길은 소스·기존 플레이어·기존 ZIP·공개 파일을 이전 해시와 대조해 보존했다. 새 Unity 빌드나 Gospel 플레이 검증을 수행한 것으로 표시하지 않는다.\n\n'
    text += '음악의 원본 출처와 발췌·루프 범위는 음악 조사 기록을 따른다. 원곡 복구, 포트폴리오 발췌와 창작 대체곡을 혼동하지 않는다. 전투 연출은 실제 Core 이벤트와 중간 스냅샷을 표시하며 판정 RNG를 소비하지 않는다.\n\n'
    text += '모바일615×346 게임 캔버스에서 중앙 연출 복사본의 보조 이름·수치띠는 읽히지 않는 제한이 확인됐다. 원래 전열과 손패의 이름·AP/DP/HP 및 메인 판정 문구는 읽혔으며, 실제 중앙 코인·직접공격·덱 감소·소각장 증가와 연출 정리를 원본으로 확인했다. 보조띠까지 검증된 것으로 표시하지 않는다.\n\n'
    if any(row.get('adjudicatedPublicMediaRanges') for row in record.get('publicWeb', {}).values()):
        text += '공개 미디어 범위 요청 취소 때문에 실패한 원본 보고서는 보존했다. 별도 해시 기반 분석에서 정확한 파일·디코딩·취소 전후 진행·후속 PCM을 확인한 요청만 구분하며 자동 검사 전체 통과로 바꾸지 않는다.\n\n'
    text += '검증은 소프트웨어 Chromium과 모바일 입력 에뮬레이션을 사용한다. 실물 기기 성능·사람의 물리 스피커 청취·사용자의 미적 승인이나 원작 전체 클라이언트 복원을 뜻하지 않는다.\n'
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-only', action='store_true')
    parser.add_argument('--pages-run')
    args = parser.parse_args()
    builds = {'FantasyCards': {target: BASE.build('FantasyCards', target) for target in ('Windows', 'Web')}}
    record = {'releaseProfile': 'fm-fidelity', 'recordedUtc': datetime.now(timezone.utc).isoformat(),
              'gameRevisions': FIDELITY['revisions'], 'contentVersions': FIDELITY['contents'],
              'sourceCommitAtSummary': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
              'externalCore': BASE.core(), 'nativeTests': {'FantasyCards': BASE.native('FantasyCards')},
              'builds': builds, 'preservedGames': {'PathOfDawn': preserved()},
              'web': {profile: {'FantasyCards': BASE.browser('FantasyCards', profile, builds['FantasyCards']['Web']['files'])}
                      for profile in ('Desktop720', 'Mobile844')},
              'publication': {'status': 'pending', 'url': URL}}
    if args.pages_run:
        record['publication'] = publication(record, args.pages_run)
    if not args.check_only:
        outputs = {'Docs/FmFidelityValidation.md': markdown(record),
                   'Docs/Validation.md': markdown(record), 'Docs/VisualValidation.md': markdown(record),
                   'Tools/Environment/validation.json': json.dumps(record, ensure_ascii=False, indent=2) + '\n',
                   'Artifacts/Validation/FmFidelity/summary.json': json.dumps(record, ensure_ascii=False, indent=2) + '\n',
                   'Artifacts/Builds/FantasyCards/Web/OutputFiles.json': json.dumps(
                       {'revision': FIDELITY['revisions']['FantasyCards'], 'files': builds['FantasyCards']['Web']['files']}, indent=2) + '\n'}
        for name, content in outputs.items():
            path = ROOT / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_text(content)
    print(json.dumps({'status': 'passed', 'checkOnly': args.check_only,
                      'publication': record['publication']['status'],
                      'core': record['externalCore']['passed'],
                      'nativeFC': record['nativeTests']['FantasyCards']['passed'],
                      'preservedGospelPublicFiles': record['preservedGames']['PathOfDawn']['publicFiles']}))


if __name__ == '__main__':
    main()
