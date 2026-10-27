#!/usr/bin/env python3
"""Summarize current Arcana evidence without launching, building or publishing.

Historical reports remain immutable. A current failed report is refused unless a
separate, current-revision proof is explicitly integrated; old Detail/Fidelity
qualifier branches are never inherited merely because their file names exist.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'Artifacts/Validation/Arcana'
sys.path.insert(0, str(ROOT / 'Tools/Web'))
from release_contract import ARCANA, manifest_contract


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


BASE = module('arcana_base_validation', ROOT / 'Tools/Validation/summarize-modern-release.py')
BASE.REVISIONS = ARCANA['revisions']
BASE.CONTENTS = ARCANA['contents']
BASE.REPORTS = REPORTS
require, digest, ref, normalized = BASE.require, BASE.digest, BASE.ref, BASE.normalized
ROLES = {'login', 'lobby', 'deck', 'battle'}
PROFILES = {'Desktop720': 'desktop', 'Mobile844': 'mobile-landscape', 'PublicMobile844': 'mobile-landscape'}
NATIVE_CARD_FEATURE = 'ArcanaPortraitCardsAndActualRoleChangesRetainTheirIdentity'
NATIVE_NPC_FEATURE = 'AuthoredNearbyNpcTopicsPreserveProgressAndLinkExactScripture'
RESTORED_STATUSES = {'restored', 'recovered-after-interruption', 'restored-after-exclusion-error'}
CARD_CHECKPOINTS = {'title', 'profile', 'continued-profile', 'lobby', 'seven-mono-decks', 'mono-decks-last-page',
                    'dual-decks', 'eighteen-decks-last-page', 'portrait-card-gallery', 'new-legend-card',
                    'original-printed-card', 'opening-portrait-hand', 'backfield-terrain', 'portrait-hand-and-field',
                    'terrain-detail', 'saved-battle', 'continued-battle'}
CARD_AUDIO_LABELS = {'title-original', 'after-first-gesture', 'muted', 'unmuted', 'lobby-instrumental',
                     'deck-role-instrumental', 'deck-same-role-instrumental', 'lobby-return-instrumental',
                     'battle-original', 'continued-original'}
ROLE_AUDIO_LABELS = ('lobby-instrumental', 'deck-role-instrumental', 'deck-same-role-instrumental', 'lobby-return-instrumental')


def read(path):
    return json.loads(Path(path).read_text())


def inside(path, parent=None):
    path = Path(path).resolve()
    require(path.is_relative_to(Path(parent or ROOT).resolve()), 'Evidence outside expected root: ' + str(path))
    return path


def source_rows(record, label):
    rows = record.get('files', [])
    require(rows, 'Missing source fingerprints: ' + label)
    for row in rows:
        path = inside(ROOT / row.get('file', row.get('path', '')))
        require(path.is_file() and digest(path) == row['sha256'], 'Source changed after ' + label + ': ' + str(path))


def core():
    result = BASE.core()
    require(result['passed'] >= 122, 'Current full Core suite (at least122) required')
    copy = REPORTS / 'core-tests.trx'
    require(copy.is_file() and digest(copy) == result['sha256'], 'Arcana full Core copy differs from canonical fresh run')
    return {**result, 'arcanaCopy': ref(copy)}


def native(game):
    result = BASE.native(game)
    require(result['passed'] >= 7, 'Current Arcana feature/native suite required: ' + game)
    root = ET.parse(ROOT / result['file']).getroot()
    names = [test.get('fullname', test.get('name', '')) for test in root.findall('.//test-case')]
    if game == 'FantasyCards':
        require(any(NATIVE_CARD_FEATURE in name for name in names), 'Native portrait-card/music-role feature test missing')
    else:
        require(any(NATIVE_NPC_FEATURE in name for name in names), 'Native actual NPC conversation/Scripture feature test missing')
    BASE.fresh(result['finishedUtc'], [f'Games/{game}/Assets/Resources', f'Games/{game}/Assets/Editor'], 'Current native assets ' + game)
    return {**result, 'testNames': names, 'startedUtc': root.get('start-time')}


def cards():
    # This existing verifier is read-only and compares complete definitions,
    # importer policy, full-card art rectangles and source/GUID bindings.
    verifier = module('arcana_card_authoring', ROOT / 'Tools/Validation/verify-card-authoring.py')
    authoring = verifier.verify_authoring()
    for key in ('cards', 'completeDefinitions', 'compatibilityMirrors', 'originalArtworkBindings', 'cardAssetImporters', 'fullCardRectangles'):
        require(authoring.get(key) == 600, 'Current600-card semantic audit missing: ' + key)
    expansion_path = REPORTS / 'CoreCards/summary.json'
    expansion = read(expansion_path)
    require(expansion.get('status') == 'passed' and expansion.get('cardCount') == 600
            and expansion.get('starterDecks') == 18 and expansion.get('monoDecks') == 7
            and expansion.get('distinctDualDecks') == 11, 'Current600-card/18-preset source audit required')
    require(set(expansion.get('preservedOldDeckIds', [])) == {'forest', 'knights', 'tides', 'stars', 'fidelity'}, 'Existing five deck IDs not preserved')
    source_rows(expansion, 'Arcana card expansion')
    trx = inside(ROOT / expansion['filteredCoreTrx'])
    require(digest(trx) == expansion['filteredCoreTrxSha256'], 'Filtered card Core result changed')
    return {'authoring': authoring, 'sourceAudit': ref(expansion_path), 'cardCount': 600,
            'starterDecks': 18, 'monoDecks': 7, 'distinctDualDecks': 11}


def native_music(native_result):
    xml = inside(ROOT / native_result['file'])
    require(digest(xml) == native_result['sha256'], 'Current native XML changed after its successful verification')
    # Unity can reset the suite clock across PlayMode domain reloads. The music
    # ledger belongs to this exact feature case, whose own interval is stable;
    # the aggregate root start/end timestamps can begin after that case ended.
    feature_cases = [case for case in ET.parse(xml).getroot().findall('.//test-case')
                     if case.get('methodname', case.get('name')) == NATIVE_CARD_FEATURE]
    require(len(feature_cases) == 1 and feature_cases[0].get('result') == 'Passed', 'Exactly one passed current native music feature case required')
    feature = feature_cases[0]
    path = REPORTS / 'native-music-transitions.json'
    ledger = read(path)
    starts = ledger.get('starts', [])
    require(3 <= len(starts) <= 24, 'Actual bounded native music start ledger required')
    require(BASE.timestamp(feature.get('start-time', '')) - 1 <= path.stat().st_mtime
            <= BASE.timestamp(feature.get('end-time', '')) + 2, 'Native music ledger is not from the current passed feature case')
    for entry in starts:
        require(entry.get('scene') in ROLES and isinstance(entry.get('transition'), int)
                and math.isfinite(entry.get('dspTime', float('nan')))
                and 0 <= entry.get('sourceTime', -1) < .1, 'Invalid native real source start record')
    triple = next((starts[index:index+3] for index in range(len(starts)-2)
                   if [entry['scene'] for entry in starts[index:index+3]] == ['lobby', 'deck', 'lobby']), None)
    require(triple and triple[0]['transition'] < triple[1]['transition'] < triple[2]['transition']
            and triple[0]['dspTime'] < triple[1]['dspTime'] < triple[2]['dspTime'], 'Actual native lobby-deck-lobby restart sequence missing')
    models = {}
    for name, count_key, count in (('MusicWeb', 'passed', 18), ('MusicNative', 'assertions', 29)):
        candidate = REPORTS / name / 'report.json'
        row = read(candidate)
        require(row.get('status') == 'passed' and row.get(count_key, 0) >= count, 'Current music lifecycle model checks missing: ' + name)
        source_rows(row, name)
        models[name] = {**ref(candidate), count_key: row[count_key], 'scope': row.get('scope')}
    return {'actualNative': {**ref(path), 'starts': starts, 'verifiedReturn': triple,
                             'featureCase': {'name': NATIVE_CARD_FEATURE, 'startedUtc': feature.get('start-time'),
                                             'finishedUtc': feature.get('end-time'), 'nativeXml': ref(xml)}},
            'modelChecks': models, 'scope': 'Native real AudioSource/DSP starts plus explicitly separate deterministic lifecycle checks; physical listening is not claimed.'}


def legacy_restoration(target, build=None, required_status=None):
    folder = ROOT / 'Games/PathOfDawn'
    path = ROOT / f'Artifacts/Builds/PathOfDawn/{target}/LegacyResourceExclusions.json'
    record = read(path)
    require(record.get('revision') == ARCANA['revisions']['PathOfDawn'] and record.get('target') == target
            and record.get('status') in RESTORED_STATUSES and record.get('moves') and record.get('files'), 'Current restored legacy resource ledger required: ' + target)
    if required_status:
        require(record['status'] == required_status, 'Wrong exercised restoration path: ' + target)
    require(BASE.timestamp(record['startedUtc']) <= BASE.timestamp(record['restoredUtc']), 'Legacy restoration time predates mutation')
    evidence_path = Path(record['evidencePath'])
    if not evidence_path.is_absolute(): evidence_path = folder / evidence_path
    require(evidence_path.resolve() == path.resolve(), 'Legacy ledger points at another build')
    if build is not None:
        require(BASE.timestamp(record['startedUtc']) <= (ROOT / build['file']).stat().st_mtime + .01, 'Build result predates this isolation scope')
        require(int(build.get('ExcludedLegacyAssets', -1)) == len(record['moves']), 'Build/legacy move count differs')
        declared = Path(build.get('LegacyResourceExclusions', ''))
        if not declared.is_absolute(): declared = folder / declared
        require(declared.resolve() == path.resolve(), 'Build references another legacy restoration record')
        if record['status'] == 'restored':
            require(build.get('LegacyResourcesRestored') == 'true', 'Successful scope restoration marker missing')
    else:
        helper = folder / 'Assets/Editor/LegacyResourceIsolation.cs'
        require(BASE.timestamp(record['startedUtc']) + 1 >= helper.stat().st_mtime, 'Lifecycle run predates the current recovery helper')
    require(not (folder / 'Library/FirstGameLegacyResourceBuild.json').exists(), 'Unresolved legacy recovery journal remains')
    require(not (folder / 'Assets/Art/Archive/BuildExcludedLegacy').exists(), 'Legacy exclusion holding directory remains')
    expected_paths = set()
    for move in record['moves']:
        source = inside(folder / move['source'], folder / 'Assets/Resources')
        destination = inside(folder / move['destination'], folder / 'Assets/Art/Archive/BuildExcludedLegacy')
        require(source.exists() and not destination.exists(), 'Legacy move has not been fully restored: ' + move['source'])
        meta = Path(str(source) + '.meta')
        match = re.search(r'^guid:\s*([0-9a-f]{32})\s*$', meta.read_text(), re.M)
        require(match and match.group(1) == move['guid'], 'Restored legacy GUID differs: ' + move['source'])
        if source.is_dir():
            expected_paths.update(str(item.relative_to(folder)) for item in source.rglob('*') if item.is_file())
        else:
            expected_paths.add(move['source'])
        expected_paths.add(str(meta.relative_to(folder)))
    rows = record['files']
    require(len(rows) == len({row['path'] for row in rows}) and {row['path'] for row in rows} == expected_paths, 'Legacy restored file set differs')
    for row in rows:
        source = inside(folder / row['path'], folder / 'Assets/Resources')
        require(source.is_file() and source.stat().st_size == row['bytes'] and digest(source) == row['sha256'], 'Legacy bytes not restored: ' + row['path'])
    return {**ref(path), 'status': record['status'], 'moves': len(record['moves']), 'files': len(rows),
            'restoredUtc': record['restoredUtc'], 'hashVerified': True, 'guidVerified': True,
            'scope': 'Exact current original bytes/GUIDs restored; temporary exclusion is not source deletion.'}


def legacy_lifecycle():
    records = {}
    for target, status in (('ArcanaLifecycleNormal', 'restored'), ('ArcanaLifecycleException', 'restored'),
                           ('ArcanaLifecycleRecovery', 'recovered-after-interruption')):
        row = legacy_restoration(target, required_status=status)
        packaged = REPORTS / 'LegacyResources' / target / 'LegacyResourceExclusions.json'
        require(packaged.is_file() and digest(packaged) == row['sha256'], 'Packaged lifecycle ledger differs: ' + target)
        records[target] = {**row, 'packagedCopy': ref(packaged)}
    return {'records': records, 'scope': 'Actual Unity asset moves with normal disposal, exception disposal, and explicit journal recovery; no process-kill test is claimed.'}


def captured_source(original, sha, report_parent, suffixes):
    path = Path(original)
    if not path.is_absolute(): path = ROOT / path
    path = inside(path)
    if path.is_file() and digest(path) == sha: return path
    # A capture-only follow-up may preserve a pre-change harness/plan. Only an
    # exact recorded digest inside this release's explicit history is accepted.
    for history in (REPORTS / 'HarnessHistory', report_parent / 'HarnessHistory'):
        if history.exists():
            for candidate in history.rglob('*'):
                if candidate.is_file() and candidate.suffix in suffixes and digest(candidate) == sha: return candidate
    raise ValueError('Current capture source/snapshot missing or changed: ' + str(path))


def checked_browser(game, profile, files):
    path = REPORTS / profile / game / 'report.json'
    record = read(path)
    require(record.get('status') == 'passed', 'Current raw browser run did not pass; preserve it and integrate a specific current proof: ' + str(path))
    require(record.get('revision') == ARCANA['revisions'][game] and record.get('game') == game
            and record.get('profile') == PROFILES[profile] and record.get('suite') == 'modern-real-input-v1', 'Wrong browser revision/profile: ' + str(path))
    require(record.get('checks') and all(item.get('passed') is True for item in record['checks']) and record.get('input'), 'Missing real input/current checks')
    require(not record.get('pageErrors') and not record.get('httpErrors'), 'Runtime page/HTTP errors')
    require(normalized(record.get('buildFingerprint', {}).get('files', [])) == normalized(files), 'Browser runtime differs from current player')
    harness = record.get('harness', {})
    harness_path = captured_source(harness['file'], harness['sha256'], path.parent, {'.py'})
    plan_path = captured_source(harness['plan'], harness['planSha256'], path.parent, {'.json'})
    plan = read(plan_path)[game]
    require(plan.get('revision') == ARCANA['revisions'][game] and plan.get('suite') == record['suite'] and plan.get('requiredCheckpoints'), 'Wrong captured current plan')
    review = record.get('visualReview', {})
    require(review.get('status') == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
            and normalized(review.get('runtimeFiles', [])) == normalized(files), 'Current directly reviewed originals required')
    approved = [item for item in record.get('checkpoints', []) if item.get('status') == 'approved']
    for checkpoint in approved:
        original, decision = checkpoint['original'], checkpoint['review']
        image = inside(ROOT / original['path'], path.parent)
        require(image.is_file() and digest(image) == original['sha256'] == decision.get('frameSha256')
                and decision.get('status') == 'approved' and decision.get('reviewer') and decision.get('reviewedUtc')
                and decision.get('expected') == checkpoint['expected'] and decision.get('observed'), 'Original image/review changed')
        for supporting in decision.get('supportingOriginals', []):
            other = inside(ROOT / supporting['path'], REPORTS)
            require(other.is_file() and digest(other) == supporting['sha256'], 'Supporting original changed')
    for name in plan['requiredCheckpoints']:
        expected = next((step.get('expected') for step in plan['steps'] if step.get('type') == 'checkpoint' and step.get('name') == name), None)
        require(expected and any((item['name'] == name or item['name'].startswith(name + '-adjusted-'))
                                 and item['expected'] == expected for item in approved), 'Required current original/expectation missing: ' + name)
    if profile != 'PublicMobile844' and game == 'PathOfDawn':
        require({'npc-topics', 'npc-study-scripture'}.issubset(set(plan['requiredCheckpoints'])), 'Current NPC conversation originals are not in the captured plan')
    windows = record.get('audioEvidence', {}).get('windows', [])
    require(windows and any(item.get('medianRms', 0) > 1e-5 for item in windows), 'No actual audible PCM')
    if profile != 'PublicMobile844' and game == 'FantasyCards':
        require(CARD_CHECKPOINTS.issubset(set(plan['requiredCheckpoints'])), 'Current portrait-card/18-deck originals are not in the captured plan')
        authored_audio = {step.get('label') for step in plan['steps'] if step.get('type') == 'audio'}
        require(CARD_AUDIO_LABELS.issubset(authored_audio) and CARD_AUDIO_LABELS.issubset({window.get('label') for window in windows}), 'Current exact FM music-control/role windows are missing')
    for window in windows:
        raw = inside(path.parent / window['rawEvidence'], path.parent)
        require(raw.is_file() and digest(raw) == window['sha256'], 'Raw PCM evidence changed')
        raw_record = read(raw)
        actual = raw_record.get('summary', {})
        require(actual and all(window.get(key) == value for key, value in actual.items() if key != 'frames'), 'PCM summary differs from its raw record')
        if game == 'FantasyCards':
            for snapshot in raw_record.get('snapshots', []):
                media_contexts = [context for context in snapshot.get('contexts', []) if context.get('media')]
                require(not media_contexts or (len(media_contexts) == 1 and len(media_contexts[0]['media']) == 4), 'Fixed four media elements/one music graph not retained')
    if game == 'FantasyCards':
        require(any('/StreamingAssets/Audio/ModernCard/' in url for item in windows for url in item.get('sourceUrls', [])), 'No current bundled source PCM')
    result = {'checks': len(record['checks']), 'approvedOriginals': len(approved), 'screenshots': [item['original'] for item in approved],
              'audioWindows': len(windows), **ref(path), 'rawStatus': 'passed',
              'capturePlan': ref(plan_path), 'captureHarness': ref(harness_path)}
    if game == 'FantasyCards' and profile != 'PublicMobile844': result['musicRoles'] = web_music_roles(windows)
    return result


def web_music_roles(windows):
    snapshots = [window for window in windows if window.get('rolePlayback')]
    require(snapshots, 'Actual Web role playback snapshots missing')
    grouped = {}
    for window in snapshots:
        observed = window['rolePlayback']
        players = observed.get('players', [])
        require(len(players) == 4 and {player.get('scene') for player in players} == ROLES, 'Music elements differ from four fixed roles')
        grouped.setdefault(window.get('navigation'), []).append({**observed, 'label': window.get('label')})
    sequence = None
    same_role = None
    for navigation, rows in grouped.items():
        rows.sort(key=lambda row: row['observedAtMs'])
        by_label = {row['label']: row for row in rows}
        if not all(label in by_label for label in ROLE_AUDIO_LABELS): continue
        role_rows = [by_label[label] for label in ROLE_AUDIO_LABELS]
        require([row['state'].get('scene') for row in role_rows] == ['lobby', 'deck', 'deck', 'lobby']
                and all(a['observedAtMs'] < b['observedAtMs'] for a, b in zip(role_rows, role_rows[1:])), 'Named role windows do not follow lobby-deck-redraw-lobby on one navigation')
        promoted = {}
        for row in rows:
            for event in row.get('transitions', []):
                if event.get('event') == 'promoted': promoted[(event.get('serial'), event.get('scene'))] = event
        events = sorted(promoted.values(), key=lambda event: (event['serial'], event['atMs']))
        for index in range(len(events) - 2):
            triple = events[index:index+3]
            if [event['scene'] for event in triple] != ['lobby', 'deck', 'lobby']: continue
            if [event['serial'] for event in triple] != [role_rows[role_index]['state'].get('sceneTransitionCount') for role_index in (0, 1, 3)]: continue
            require(triple[0]['serial'] < triple[1]['serial'] < triple[2]['serial'], 'Role transition serials do not advance')
            require(all(0 <= event.get('startPosition', -1) < .1 and math.isfinite(event.get('position', float('nan')))
                        and event['position'] >= 0 for event in triple), 'A-B-A did not start from the beginning')
            require(all('/StreamingAssets/Audio/ModernCard/' in event.get('source', '') for event in triple), 'Role return used another source')
            require(triple[0]['source'] == triple[2]['source'] and triple[2]['restartCount'] > triple[0]['restartCount'], 'Returned role did not create a fresh start of its own source')
            sequence = {'navigation': navigation, 'promoted': triple}
        for before, after in zip(rows, rows[1:]):
            if (before['label'], after['label']) != ROLE_AUDIO_LABELS[1:3]: continue
            if after['state'].get('sameRoleMessages', 0) <= before['state'].get('sameRoleMessages', 0): continue
            if (before['state'].get('scene') != after['state'].get('scene')
                    or before['state'].get('sceneTransitionCount') != after['state'].get('sceneTransitionCount')): continue
            old = {entry['scene']: entry for entry in before['players']}
            for current in after['players']:
                previous = old[current['scene']]
                if (current['scene'] == after['state'].get('scene') and previous.get('active') and current.get('active')
                        and not previous.get('paused') and not current.get('paused')
                        and previous.get('source') == current.get('source')
                        and current.get('currentTime') != previous.get('currentTime')
                        and previous.get('restartCount', 0) > 0 and current.get('restartCount') == previous.get('restartCount')
                        and current.get('lastStartAtMs') == previous.get('lastStartAtMs')
                        and current.get('lastStartRoleSerial') == previous.get('lastStartRoleSerial')):
                    same_role = {'navigation': navigation, 'scene': current['scene'],
                                 'beforeObservedMs': before['observedAtMs'], 'afterObservedMs': after['observedAtMs'],
                                 'sameRoleMessagesBefore': before['state'].get('sameRoleMessages', 0),
                                 'sameRoleMessagesAfter': after['state'].get('sameRoleMessages', 0),
                                 'restartCount': current['restartCount'], 'lastStartAtMs': current['lastStartAtMs']}
    require(sequence, 'Actual Web lobby-deck-lobby promoted restart sequence missing')
    require(same_role, 'Actual same-role redraw without another start is not recorded')
    return {'returnStartsAtZero': sequence, 'sameRoleRetainsStart': same_role,
            'scope': 'Actual controller role/time ledger plus separately verified PCM, not physical speaker listening.'}


# BASE.public resolves this global function at call time; no old-revision
# exception branch from BASE.browser is inherited.
BASE.browser = checked_browser


def markdown(record):
    pub = record['publication']
    text = '# 카드·덱·음악 전환과 인물·NPC 대화 개정 검증\n\n'
    text += f"기록일 {record['date']} (Asia/Seoul). `fm-arcana-2026-10-27` / `gospel-authored-2026-10-27`. 이전 판의 통과를 새 화면·빌드의 근거로 대신하지 않는다.\n\n"
    text += (f"[공개 테스트]({BASE.URL}) · [Pages 실행]({pub['pagesRun']}) · 공개 파일 {pub['verifiedFiles']}개 바이트 일치.\n\n" if pub['status'] == 'verified' else '현재 새 로컬 검증을 기록했으며 공개 배포 검증은 대기 중이다.\n\n')
    text += f"Core {record['externalCore']['passed']}개, Unity 실행 검사 " + ', '.join(f"{game} {row['passed']}개" for game, row in record['nativeTests'].items()) + ' 통과. 카드600종의 전체 정의·원화·GUID와18덱(단일7/복합11)을 대조했고 두 게임 Windows/Web 빌드가 성공했다.\n\n'
    text += '| 실제 브라우저 경로 | 통과 검사 | 직접 검토한 원본 | 실제 PCM 구간 |\n|---|---:|---:|---:|\n'
    for profile, games in record['web'].items():
        for game, row in games.items(): text += f"| {profile}/{game} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    for game, row in record.get('publicWeb', {}).items(): text += f"| PublicMobile844/{game} | {row['checks']} | {row['approvedOriginals']} | {row['audioWindows']} |\n"
    text += '\n카드의 원작 인쇄 그림은 보존하고 새 세로 카드 프레임·등급 색·재질 표현과 구분한다. 새 타이틀 인물 원화는 새 제작물이며 원작의 고해상도 복원으로 소개하지 않는다. 단일7속성 덱과복합11덱은 실제50장 구성·카드 제한·생산 속성과 원작 기술 지원 여부를 검사한다.\n\n'
    text += '음악은 확인된 원작 발췌/공개 전투곡을 유지한다. 실제 역할 변경의0초 시작과 같은 역할 재표시의 위치 유지를 Native AudioSource/DSP 기록과 Web 역할 기록으로 확인하며 PCM·물리 스피커 청취를 구분한다. 짧은 발췌를 전체 OST 복구로 소개하지 않는다.\n\n'
    text += 'NPC 학습 대화는 해설과 직접 성경 인용을 구분하고 아래에 연결 본문을 표시한다. 보조 주제는 본편 순서·수집 구절을 변경하지 않으며 현재 목표에 맞는 인물만 본편 대화를 시작한다. 실제 클릭/E/터치·대화·저장 검증은 위의 현재 실행 원본에 한정한다.\n\n'
    text += '현재 Boot가 쓰지 않는 이전 관찰자 그림·음성은 빌드 중에만 Resources 밖으로 옮겼다. 두 대상의 복구 기록과 현재 원본 전체 바이트·GUID를 다시 대조했으며, 복구 저널이 남아 있으면 검증을 거부한다. 원본 삭제나 과거 제작 이력의 폐기를 뜻하지 않는다.\n\n'
    text += '검증은 소프트웨어 Chromium과 모바일 입력 에뮬레이션이다. 실물 휴대폰·Safari·Windows 하드웨어 성능·사람의 청취·사용자 미적 승인은 별개다. 판마 전체 클라이언트/모든 카드 예외나 예수님 생애 전체의 완성을 뜻하지 않는다. 역사적 얼굴·건축의 정확한 복원도 주장하지 않는다.\n'
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check-only', action='store_true')
    parser.add_argument('--pages-run')
    args = parser.parse_args()
    builds = {game: {target: BASE.build(game, target) for target in ('Windows', 'Web')} for game in ARCANA['revisions']}
    native_results = {game: native(game) for game in ARCANA['revisions']}
    record = {'releaseProfile': 'arcana', 'date': datetime.now(ZoneInfo('Asia/Seoul')).date().isoformat(),
              'recordedUtc': datetime.now(timezone.utc).isoformat(), 'dateTimezone': 'Asia/Seoul',
              'gameRevisions': ARCANA['revisions'], 'contentVersions': ARCANA['contents'],
              'sourceCommitAtSummary': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
              'externalCore': core(), 'nativeTests': native_results, 'cardAuthoring': cards(),
              'nativeMusic': native_music(native_results['FantasyCards']),
              'legacyResources': {target: legacy_restoration(target, builds['PathOfDawn'][target]) for target in ('Windows', 'Web')},
              'legacyRecoveryExercise': legacy_lifecycle(),
              'builds': builds,
              'web': {profile: {game: checked_browser(game, profile, builds[game]['Web']['files']) for game in ARCANA['revisions']} for profile in ('Desktop720', 'Mobile844')},
              'publication': {'status': 'pending', 'url': BASE.URL}}
    if args.pages_run:
        manifest = read(ROOT / 'Artifacts/WebPlaytest/build-manifest.json')
        require(manifest_contract(manifest)['name'] == 'arcana', 'Current Arcana public manifest required')
        record['publication'] = BASE.public(record, args.pages_run)
    if not args.check_only:
        history = ROOT / 'Docs/ValidationHistory/2026-10-27-before-arcana'
        history.mkdir(parents=True, exist_ok=True)
        for name in ('Docs/Validation.md', 'Docs/VisualValidation.md', 'Tools/Environment/validation.json'):
            current, saved = ROOT / name, history / Path(name).name
            if current.is_file() and not saved.exists(): shutil.copyfile(current, saved)
        text = markdown(record)
        outputs = {'Docs/ArcanaValidation.md': text, 'Docs/Validation.md': text, 'Docs/VisualValidation.md': text,
                   'Tools/Environment/validation.json': json.dumps(record, ensure_ascii=False, indent=2) + '\n',
                   'Artifacts/Validation/Arcana/summary.json': json.dumps(record, ensure_ascii=False, indent=2) + '\n'}
        for game in ARCANA['revisions']:
            outputs[f'Artifacts/Builds/{game}/Web/OutputFiles.json'] = json.dumps({'revision': ARCANA['revisions'][game], 'files': builds[game]['Web']['files']}, indent=2) + '\n'
        for name, content in outputs.items():
            path = ROOT / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content)
    print(json.dumps({'status': 'passed', 'checkOnly': args.check_only, 'publication': record['publication']['status'],
                      'core': record['externalCore']['passed'], 'native': {game: row['passed'] for game, row in native_results.items()}}, ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError, ET.ParseError, subprocess.CalledProcessError) as error:
        print('Arcana summary refused: ' + str(error), file=sys.stderr)
        sys.exit(1)
