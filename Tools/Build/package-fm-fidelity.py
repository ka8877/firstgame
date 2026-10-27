#!/usr/bin/env python3
"""Refresh FC players/source/evidence; verify and retain the untouched Gospel ZIPs."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Artifacts'
OUT = ART / 'Validation/FmFidelity'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


BASE = module('modern_package_contract', ROOT / 'Tools/Build/package-modern.py')
BASE.REVISIONS = {'FantasyCards': 'fm-fidelity-2026-10-27', 'PathOfDawn': 'gospel-modern-2026-10-26'}
PRESERVE = module('package_gospel_preservation', ROOT / 'Tools/Validation/preserve-gospel.py')


def archive_previous(name):
    path = ART / name
    if path.exists():
        previous = ART / 'PreviousDeliverables/2026-10-27-before-fidelity'
        previous.mkdir(parents=True, exist_ok=True)
        target = previous / name
        if target.exists():
            target = previous / (path.stem + '-' + datetime.now(timezone.utc).strftime('%H%M%S%f') + path.suffix)
        path.rename(target)


BASE.archive_previous = archive_previous


def notes(target):
    folder = ART / 'Builds/FantasyCards' / target
    launch = ('압축을 모두 푼 뒤 FantasyCards.exe를 실행하세요. 데이터 폴더와 DLL도 함께 필요합니다.\n'
              if target == 'Windows' else '폴더 전체를 HTTP 웹 서버에서 실행하세요. file:// 실행은 지원하지 않습니다.\n')
    (folder / 'README.txt').write_text(
        'fm-fidelity-2026-10-27\nUnity 6000.3.26f1\n\n' + launch +
        '게임 시작 → AI 연습방 → 덱 선택 → 대전 시작. 원작 등급/랭크와 소각장 정보를 구분합니다.\n'
        '전투는 실제 판정 이벤트를 순차 재생하며 배속/끝까지 버튼은 결과나 코인을 다시 정하지 않습니다.\n'
        '음악의 원본 출처·발췌 루프 범위는 동봉 음악 조사 기록을 확인하세요. 브라우저 음악에는 첫 클릭/터치가 필요합니다.\n')
    for name in ('PlaytestGuide.md', 'FmFidelityValidation.md', 'FmFidelityBattle.md'):
        path = ROOT / 'Docs' / name
        if path.is_file():
            shutil.copyfile(path, folder / name)
    notices = folder / 'ThirdParty'; notices.mkdir(exist_ok=True)
    for source, name in (
        ('Tools/Audio/MUSIC-NOTICE.txt', 'Music-NOTICE.txt'),
        ('Tools/Art/ClassicCards/SOURCE-NOTICE.txt', 'ClassicCards-SOURCE-NOTICE.txt'),
        ('Games/FantasyCards/Assets/Resources/Fonts/Pretendard-OFL.txt', 'Pretendard-OFL.txt'),
    ):
        path = ROOT / source; BASE.require(path.is_file(), 'Missing player notice: ' + source)
        shutil.copyfile(path, notices / name)


def evidence():
    result = {}
    selected = set()
    for report_path in OUT.rglob('report.json'):
        if any('.previous-' in part for part in report_path.relative_to(OUT).parts):
            continue
        report = json.loads(report_path.read_text())
        for checkpoint in report.get('checkpoints', []):
            if checkpoint.get('status') == 'approved':
                selected.add(checkpoint['original']['path'])
                selected.update(shot['path'] for shot in checkpoint.get('review', {}).get('supportingOriginals', []))
        for shot in report.get('visualReview', {}).get('screenshots', []):
            selected.add(shot.get('path', shot.get('file', '')))
    for path in sorted(OUT.rglob('*')):
        relative = path.relative_to(OUT)
        if ('PreservedPublic' in relative.parts or any('.previous-' in part for part in relative.parts)
                or path.name in {'deliverables.json', 'current-work.json'}):
            continue
        if path.suffix.lower() == '.png' and '-compositor-' in path.name and str(path.relative_to(ROOT)) not in selected:
            # All raw frames remain in the workspace. Compact evidence ships the
            # directly selected originals and the complete timestamp/hash index
            # stored in report.json, not hundreds of duplicate animation frames.
            continue
        if path.is_file() and path.suffix.lower() in {'.json', '.png', '.txt', '.md', '.sha256', '.py', '.js', '.xml'}:
            result[str(path.relative_to(ROOT))] = path
    for relative in (
        'Artifacts/TestResults/Core/core-tests.trx', 'Artifacts/TestResults/FantasyCards/PlayModeUI.xml',
        'Artifacts/Builds/FantasyCards/Windows/BuildEvidence.txt', 'Artifacts/Builds/FantasyCards/Web/BuildEvidence.txt',
        'Docs/FmFidelityValidation.md', 'Docs/FmFidelityBattle.md', 'Docs/FmFidelityUI.md',
        'Docs/Research/FmFidelityMusic.md', 'Docs/Research/FmFidelityRules.md', 'Docs/Research/FmFidelitySources.md',
        'Tools/Validation/fm-fidelity-smoke-plans.json', 'Tools/Validation/fm-fidelity-public-plans.json',
        'Tools/Validation/summarize-fm-fidelity.py', 'Tools/Validation/preserve-gospel.py',
        'Tools/Validation/revival-smoke.py', 'Tools/Validation/fm-local-media-errors.py',
        'Tools/Validation/fm-local-media-observer.js', 'Tools/Validation/webaudio-observer.js',
        'Tools/Validation/adjudicate-fm-public-ranges.py', 'Tools/Build/package-fm-fidelity.py',
        'Tools/Web/release_contract.py', 'Tools/Environment/validation.json',
    ):
        path = ROOT / relative
        if path.is_file():
            result[relative] = path
    return result


def retained_gospel(record):
    rows = []
    for row in record['archives']:
        path = ROOT / row['file']
        BASE.require(path.stat().st_size == row['bytes'] and BASE.digest(path) == row['sha256'],
                     'Untouched Gospel archive differs')
        rows.append({'archive': path.name, 'bytes': row['bytes'], 'sha256': row['sha256'],
                     'status': 'preserved-previously-verified',
                     'scope': 'Previous ZIP retained byte-for-byte; not rebuilt or repackaged.'})
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    summary = json.loads((OUT / 'summary.json').read_text())
    BASE.require(summary.get('releaseProfile') == 'fm-fidelity'
                 and summary['publication']['status'] == 'verified', 'Finish current local/public summary first')
    frozen = PRESERVE.verify()
    if not args.verify:
        for target in ('Windows', 'Web'):
            notes(target)
    sets = {f'FantasyCards-{target}.zip': BASE.player_files('FantasyCards', target) for target in ('Windows', 'Web')}
    sets['FirstGame-UnitySources.zip'] = BASE.source_files()
    sets['FirstGame-ValidationEvidence.zip'] = evidence()
    rows = [BASE.verify(ART / name, entries) if args.verify else BASE.write_archive(name, entries)
            for name, entries in sets.items()]
    rows += retained_gospel(frozen)
    old_checksums = ART / 'Deliverables.sha256'
    checksum_copy = ART / 'PreviousDeliverables/2026-10-27-before-fidelity/Deliverables.sha256'
    if not args.verify and old_checksums.is_file() and not checksum_copy.exists():
        checksum_copy.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(old_checksums, checksum_copy)
    result = {'status': 'passed', 'checkedUtc': datetime.now(timezone.utc).isoformat(),
              'releaseProfile': 'fm-fidelity', 'gameRevisions': BASE.REVISIONS,
              'publication': summary['publication'], 'archives': rows,
              'scope': 'Four refreshed archives verify CRC/current bytes/fileset/LFS/credential paths. Two Gospel archives retain their previous verified SHA without regeneration.'}
    (OUT / 'deliverables.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    (ART / 'Deliverables.sha256').write_text(''.join(row['sha256'] + '  ' + row['archive'] + '\n' for row in rows))
    print(json.dumps({'status': 'passed', 'verifyOnly': args.verify, 'archives': rows}, ensure_ascii=False))


if __name__ == '__main__':
    main()
