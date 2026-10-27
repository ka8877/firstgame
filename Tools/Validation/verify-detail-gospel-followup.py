#!/usr/bin/env python3
"""Verify two ordinary Gospel desktop sessions after a capture-only failure.

The first report remains failed with zero compositor frames. The second session
starts normally and supplies only the remaining five interaction checkpoints on
identical runtime bytes. This never edits a raw report, image, input or status.
"""
from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
PRIMARY = ROOT / 'Artifacts/Validation/Detail/Desktop720/PathOfDawn/report.json'
FOLLOWUP = ROOT / 'Artifacts/Validation/Detail/Desktop720Followup/PathOfDawn/report.json'
HISTORY = ROOT / 'Artifacts/Validation/Detail/HarnessHistory'
ORIGINAL_PLAN = HISTORY / 'detail-smoke-plans-desktop-gospel-initial.json'
ORIGINAL_HARNESS = HISTORY / 'revival-smoke-before-screencast-warmup.py'
FOLLOWUP_PLAN = HISTORY / 'detail-gospel-followup-plan-launch.json'
FOLLOWUP_HARNESS = HISTORY / 'revival-smoke-gospel-followup-launch.py'
PLAYER = ROOT / 'Artifacts/Builds/PathOfDawn/Web'
EXPECTED_PRIMARY_SHA = '576f19e16a7d8c0da123b1cb686980ebd43793ad9c884e8d6c9684065d4a193a'
CAPTURE_FAILURE = 'Before-input compositor burst has actual original PNG frames'
FIRST_NAMES = {'title', 'temple-idle', 'hero-rear-close', 'hero-front-close', 'status'}
LAST_NAMES = {'hero-walk', 'hero-run', 'gate-reading', 'saved-temple', 'continued-temple'}


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    hasher = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            hasher.update(block)
    return hasher.hexdigest()


def reference(path):
    path = Path(path).resolve()
    require(path.is_file() and path.is_relative_to(ROOT), 'Missing/outside evidence')
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path)}


def normalized(files):
    return sorted(({key: row[key] for key in ('file', 'bytes', 'sha256')} for row in files), key=lambda row: row['file'])


def runtime_files():
    result = []
    for path in sorted(PLAYER.rglob('*')):
        if not path.is_file():
            continue
        relative = path.relative_to(PLAYER)
        if str(relative) != 'index.html' and relative.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in relative.parts) or path.suffix.lower() in ('.log', '.map', '.pdb', '.mdb'):
            continue
        require(not path.is_symlink(), 'Unexpected runtime symlink')
        result.append({'file': str(relative), 'bytes': path.stat().st_size, 'sha256': digest(path)})
    require(len(result) >= 5, 'Incomplete current Gospel player')
    return result


def logical(name):
    return name.split('-adjusted-', 1)[0]


def approvals(report, path, names, authored_plan):
    approved = [row for row in report['checkpoints'] if row['status'] == 'approved']
    require(len(approved) == len(names) and {logical(row['name']) for row in approved} == names,
            'Missing/duplicate logical checkpoints: ' + str(path))
    expected = {step['name']: step['expected'] for step in authored_plan['steps'] if step['type'] == 'checkpoint'}
    reviews = []
    for row in approved:
        shot, review = row['original'], row['review']
        image = (ROOT / shot['path']).resolve()
        require(image.is_relative_to(path.parent) and image.suffix.lower() == '.png'
                and image.is_file() and digest(image) == shot['sha256'] == review.get('frameSha256'),
                'Original screenshot changed')
        require(review.get('status') == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review.get('expected') == row['expected'] == expected[logical(row['name'])],
                'Original approval/expectation differs')
        decision = (ROOT / row['reviewFile']).resolve()
        require(decision.is_relative_to(path.parent) and json.loads(decision.read_text()) == review,
                'Original review decision file changed')
        reviews.append(reference(decision))
        if logical(row['name']) in {'hero-walk', 'hero-run'}:
            candidates = row.get('candidateOriginals', [])
            require(candidates and any(c['path'] == shot['path'] and c['sha256'] == shot['sha256'] for c in candidates)
                    and shot.get('captureKind') == 'original-full-viewport-compositor-screencast-png',
                    'Movement must use a directly reviewed actual compositor original')
    return approved, reviews


def common_report(report, path):
    require(report.get('game') == 'PathOfDawn' and report.get('revision') == 'gospel-natural-2026-10-27'
            and report.get('suite') == 'modern-real-input-v1' and report.get('profile') == 'desktop',
            'Different game/revision/profile')
    viewport = report.get('interactionViewport', {})
    require(viewport.get('width') == 1280 and viewport.get('height') == 720, 'Different interaction viewport')
    require(report.get('input') and not report.get('pageErrors') and not report.get('httpErrors')
            and not any(row['type'] == 'error' for row in report.get('console', [])), 'Runtime error or missing ordinary input')
    ordinary_types = {'click', 'key', 'text', 'wheel', 'drag', 'hold', 'touch_hold', 'dom_click'}
    require(all(row.get('type') in ordinary_types for row in report['input']), 'Unknown/nonordinary input record')
    require(all(row.get('navigation') in (0, 1) for row in report['input']), 'Unexpected extra session navigation')
    require(report['harness']['helpersSha256'] == digest(ROOT / 'Tools/Validation/web-smoke.py'),
            'Recorded PCM/check helper changed')
    if not report.get('failedRequests'):
        return []
    # A successful follow-up may record the usual exact IndexedDB revalidation
    # abort. Recheck every predicate instead of ignoring failed requests.
    require(path == FOLLOWUP and report['status'] == 'passed', 'Primary/unknown request failure')
    diagnostic = report.get('networkDiagnostic', {})
    cached = diagnostic.get('verifiedUnityCacheRevalidationAborts', [])
    require(report['failedRequests'] == cached and not diagnostic.get('unexpectedFailures')
            and not diagnostic.get('externalMusicServiceFailures'), 'Unclassified follow-up request failure')
    proofs = []
    for failure in cached:
        nav, url = failure['navigation'], failure['url']
        require(nav == 1 and failure['method'] == 'GET' and failure['error'] == 'net::ERR_ABORTED'
                and failure['resourceType'] == 'fetch'
                and any(c.get('navigation') == nav and c.get('dataUrl') == url for c in report['unityConfigurations']),
                'Not the exact reloaded Unity data request')
        parsed = urlsplit(url)
        require(parsed.scheme == 'http' and parsed.hostname == '127.0.0.1' and parsed.path.endswith('.data.unityweb')
                and not parsed.query and not parsed.fragment, 'Different cached URL')
        require(any(row['navigation'] == nav and row['url'] == url and row['method'] == 'GET' and row['status'] == 304
                    for row in report['networkResponses']), 'Missing exact cache HTTP304')
        marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
        require(any(row['navigation'] == nav and row['text'] == marker for row in report['console']),
                'Missing actual successful Unity cache log')
        require(any(row['status'] == 'approved' and logical(row['name']) == 'continued-temple'
                    and row['navigation'] == nav for row in report['checkpoints']), 'No directly reviewed reloaded journey')
        proofs.append({'request': failure, 'httpStatus': 304, 'unityCacheMessage': marker})
    return proofs


def audio_windows(report, path, labels, helper):
    windows = report['audioEvidence']['windows']
    require(len(windows) == len(labels) and [w['label'] for w in windows] == labels, 'Different audio evidence windows')
    refs, audible = [], 0
    for window in windows:
        evidence = (path.parent / window['rawEvidence']).resolve()
        require(evidence.is_relative_to(path.parent) and digest(evidence) == window['sha256'], 'PCM evidence changed')
        saved = json.loads(evidence.read_text())
        derived = helper.music_audio_summary(saved['snapshots'])
        require(derived == saved['summary'] and all(window.get(key) == value for key, value in derived.items() if key != 'frames'),
                'Recorded PCM summary does not match raw snapshots')
        require(derived['observerErrors'] == [] and derived['nonfiniteSamples'] == 0, 'Invalid PCM observer samples')
        if window['label'] in ('before-first-gesture', 'muted'):
            require(derived['maximumRms'] < 1e-5, 'Silence/mute observation does not hold')
        else:
            require(derived['sampleCount'] >= 6 and derived['allContextsRunning'] is True
                    and derived['isolatedMusicRoute'] is True and len(derived['sourceIdentities']) == 1
                    and derived['audibleSampleFraction'] >= .5 and derived['medianRms'] > 1e-5
                    and derived['contextAdvancedSeconds'] > .5, 'Missing actual advancing isolated looping PCM')
            audible += 1
        refs.append({'label': window['label'], 'navigation': window['navigation'], **reference(evidence)})
    return windows, refs, audible


def analyze(primary_path=PRIMARY):
    primary_path = Path(primary_path).resolve()
    require(primary_path == PRIMARY and digest(primary_path) == EXPECTED_PRIMARY_SHA,
            'Only the exact recorded capture-only Gospel desktop failure is supported')
    primary = json.loads(primary_path.read_text())
    require(FOLLOWUP.is_file(), 'Minimal follow-up report is not complete')
    followup = json.loads(FOLLOWUP.read_text())
    require(primary.get('status') == 'failed' and primary.get('failure') == CAPTURE_FAILURE, 'Different primary failure')
    bad = [row for row in primary['checks'] if row.get('passed') is not True]
    require(len(primary['checks']) == 25 and len(bad) == 1 and bad[0]['name'] == CAPTURE_FAILURE
            and bad[0].get('evidence') == {'frames': 0}, 'Different primary capture failure')
    require(not primary.get('compositorBursts') and not primary.get('failedRequests'), 'Unexpected primary burst/request failure')
    require(followup.get('status') == 'passed' and followup.get('checks')
            and all(row.get('passed') is True for row in followup['checks']), 'The separate minimal follow-up did not wholly pass')
    common_report(primary, PRIMARY)
    cache_proofs = common_report(followup, FOLLOWUP)
    require(digest(ORIGINAL_PLAN) == primary['harness']['planSha256']
            and digest(ORIGINAL_HARNESS) == primary['harness']['sha256'], 'Original authored plan/harness changed')
    require(digest(FOLLOWUP_PLAN) == followup['harness']['planSha256']
            and digest(FOLLOWUP_HARNESS) == followup['harness']['sha256'], 'Follow-up authored plan/harness changed')
    original_plan = json.loads(ORIGINAL_PLAN.read_text())['PathOfDawn']
    followup_plan = json.loads(FOLLOWUP_PLAN.read_text())['PathOfDawn']
    require(set(original_plan['requiredCheckpoints']) == FIRST_NAMES | LAST_NAMES
            and set(followup_plan['requiredCheckpoints']) == LAST_NAMES, 'Follow-up scope changed')
    # The second report must begin with ordinary Start New plus all three birth
    # pages, then later reach a real navigation1 continue after full reload.
    gestures = followup['input']
    require(len(gestures) >= 4 and gestures[0].get('type') == 'click'
            and gestures[0].get('navigation') == 0 and gestures[0].get('point') == followup_plan['steps'][0]['point'],
            'Follow-up did not start by its normal title button')
    page_point = followup_plan['steps'][1]['point']
    require(all(row.get('type') == 'click' and row.get('point') == page_point and row.get('navigation') == 0
                for row in gestures[1:4]), 'Follow-up skipped ordinary three-page prologue')
    require(any(row.get('navigation') == 1 and row.get('type') == 'click' and row.get('label') == '이어하기'
                for row in gestures), 'No actual post-reload continue input')
    require({row.get('navigation') for row in followup['unityConfigurations']} == {0, 1}, 'Missing real loader reload')
    current = runtime_files()
    require(normalized(primary['buildFingerprint']['files']) == normalized(followup['buildFingerprint']['files']) == current,
            'Two sessions do not use identical current runtime bytes')
    first, first_reviews = approvals(primary, PRIMARY, FIRST_NAMES, original_plan)
    last, last_reviews = approvals(followup, FOLLOWUP, LAST_NAMES, followup_plan)
    require({logical(row['name']) for row in first + last} == set(original_plan['requiredCheckpoints']),
            'Merged observations do not cover original ten logical checkpoints')
    spec = importlib.util.spec_from_file_location('gospel_followup_pcm_helpers', ROOT / 'Tools/Validation/web-smoke.py')
    helper = importlib.util.module_from_spec(spec); spec.loader.exec_module(helper)
    early_audio, early_refs, early_audible = audio_windows(primary, PRIMARY,
        ['before-first-gesture', 'after-first-gesture', 'muted', 'unmuted'], helper)
    late_audio, late_refs, late_audible = audio_windows(followup, FOLLOWUP,
        ['followup-journey-original', 'after-continue-gesture'], helper)
    approved = first + last
    gate = next(row for row in last if logical(row['name']) == 'gate-reading')
    guide_observation = json.dumps(gate['review']['observed'], ensure_ascii=False).replace(' ', '')
    require('말씀길잡이' in guide_observation and '요셉' in guide_observation
            and 'erratum' in guide_observation, 'The archived guide-name expectation mismatch must be explicit')
    errata = [{'checkpoint': 'gate-reading', 'archivedExpectedName': '요셉',
               'observedVisibleName': '말씀 길잡이', 'observedVisibleRole': '학습 안내·본문 요약',
               'original': gate['original'], 'reviewDecision': reference(ROOT / gate['reviewFile']),
               'scope': 'The archived plan named Joseph; the actual guide is an explicitly labeled learning summary narrator. The reviewed Scripture reference/quotation/source remain separate. This is a plan-name erratum, not a claim of historical spoken dialogue.'}]
    passed_primary = sum(row.get('passed') is True for row in primary['checks'])
    passed_followup = sum(row.get('passed') is True for row in followup['checks'])
    return {'status': 'verified-two-ordinary-sessions', 'verifiedUtc': datetime.now(timezone.utc).isoformat(),
            'rawReport': {**reference(PRIMARY), 'status': primary['status']},
            'followupReport': {**reference(FOLLOWUP), 'status': followup['status']},
            'archivedOriginalPlan': reference(ORIGINAL_PLAN), 'archivedOriginalHarness': reference(ORIGINAL_HARNESS),
            'followupPlan': reference(FOLLOWUP_PLAN), 'followupHarness': reference(FOLLOWUP_HARNESS),
            'analysisSource': reference(Path(__file__)), 'pcmHelpers': reference(ROOT / 'Tools/Validation/web-smoke.py'),
            'reviewFiles': first_reviews + last_reviews, 'audioEvidenceFiles': early_refs + late_refs,
            'approvedCheckpoints': approved, 'audioWindows': early_audio + late_audio,
            'audibleWindows': early_audible + late_audible,
            'evidenceParents': [str(PRIMARY.parent.relative_to(ROOT)), str(FOLLOWUP.parent.relative_to(ROOT))],
            'passedChecks': passed_primary + passed_followup,
            'checksBySession': {'primary': {'passed': passed_primary, 'total': len(primary['checks']), 'rawStatus': primary['status']},
                                'followup': {'passed': passed_followup, 'total': len(followup['checks']), 'rawStatus': followup['status']}},
            'ordinarySessions': 2, 'classifiedFollowupCacheRequests': cache_proofs, 'observationErrata': errata,
            'visualReview': {'status': 'approved', 'reviewer': '; '.join(sorted({row['review']['reviewer'] for row in approved})),
                'reviewedUtc': max(row['review']['reviewedUtc'] for row in approved),
                'runtimeFiles': primary['buildFingerprint']['files'], 'screenshots': [row['original'] for row in approved],
                'findings': [row['review']['observed'] for row in approved]},
            'scope': 'Two separate ordinary-start browser sessions on identical current runtime. The first raw automatic run remains FAILED solely for zero compositor PNG frames, retaining its five earlier reviewed observations and four audio windows. The separately PASSED follow-up supplies only the five remaining movement/reading/save/continue observations plus two audio windows, after normal Start New and three birth pages. This is not one continuous run or a retroactive automatic all-pass. It uses ordinary inputs without direct game-state, storage or camera injection; this verification tool does not modify runtime source, raw reports, images, inputs or PCM. The archived gate-reading expectation named Joseph, while the directly reviewed actual label is the learning guide; that plan-name erratum is explicit rather than claimed as historical spoken dialogue. Six audio windows include initial silence and mute; four windows verify audible isolated looping PCM.'}


def verify_record(primary_path=PRIMARY):
    current = analyze(primary_path)
    recorded = json.loads((Path(primary_path).parent / 'followup-verification.json').read_text())
    keys = set(current) - {'verifiedUtc'}
    require(keys == set(recorded) - {'verifiedUtc'}, 'Recorded follow-up schema changed')
    for key in keys:
        require(current[key] == recorded[key], 'Recorded two-session verification changed: ' + key)
    return recorded


if __name__ == '__main__':
    result = analyze()
    (PRIMARY.parent / 'followup-verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': result['status'], 'primaryRawStatus': result['rawReport']['status'],
                      'followupStatus': result['followupReport']['status'], 'ordinarySessions': result['ordinarySessions'],
                      'approvedOriginals': len(result['approvedCheckpoints']), 'audioWindows': len(result['audioWindows']),
                      'checksBySession': result['checksBySession']}))
