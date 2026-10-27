#!/usr/bin/env python3
"""Qualify only the exact Gospel desktop reviewer-key typo using two ordinary sessions.

The original report remains FAILED and its unapproved running frame stays
unapproved. A separately PASSED ordinary-start session must supply every missing
checkpoint, real save/reload/continue and fresh output PCM on identical bytes.
This tool never launches a player, approves an image or changes raw evidence.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
PREFIX = ROOT / 'Artifacts/Validation/Restoration/Desktop720/PathOfDawn/report.json'
SUFFIX = ROOT / 'Artifacts/Validation/Restoration/Desktop720Followup/PathOfDawn/report.json'
PLAYER = Path('/tmp/firstgame-restoration-builds/PathOfDawn/Web')
FOLLOWUP_PLAN = ROOT / 'Tools/Validation/restoration-gospel-followup-plans.json'
EXPECTED_PREFIX_SHA = 'a92bfab0f181f643045bf0060c11b30763bbea8051d5b5a2d6c555675ef6b4c3'
EXACT_FAILURE = 'Keyboard.down: Unknown key: "shift"'
FIRST = {'title', 'temple-idle', 'hero-rear-close', 'hero-front-close', 'status',
         'inventory', 'equipped-inventory', 'live-courtyard-map', 'journey-progress', 'hero-walk'}
MISSING = {'hero-run', 'npc-topics', 'npc-study-scripture', 'gate-reading',
           'saved-temple', 'continued-temple', 'modeled-stone-well'}
EXTRA = 'followup-temple-start'
SOURCES = {'Tools/Validation/revival-smoke.py', 'Tools/Validation/web-smoke.py',
           'Tools/Validation/fm-provider-errors.py', 'Tools/Validation/fm-local-media-errors.py',
           'Tools/Validation/webaudio-observer.js', 'Tools/Validation/fm-local-media-observer.js'}


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def reference(path):
    path = Path(path).resolve()
    require(path.is_file() and (path.is_relative_to(ROOT) or path.is_relative_to(PLAYER)),
            'Missing/outside qualification evidence: ' + str(path))
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'sha256': digest(path)}


def contained_file(parent, relative):
    path = (ROOT / relative).resolve()
    require(path.is_file() and path.is_relative_to(parent.resolve()), 'Outside/missing original evidence: ' + str(relative))
    return path


def logical(name):
    return name.split('-adjusted-', 1)[0]


def normalized(rows):
    result = sorted(({key: row[key] for key in ('file', 'bytes', 'sha256')} for row in rows), key=lambda row: row['file'])
    require(len(result) == len({row['file'] for row in result}), 'Duplicate runtime file identity')
    return result


def runtime_files():
    rows = []
    for path in sorted(PLAYER.rglob('*')):
        if not path.is_file():
            continue
        local = path.relative_to(PLAYER)
        if str(local) != 'index.html' and local.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in local.parts) or path.suffix.lower() in {'.log', '.map', '.pdb', '.mdb'}:
            continue
        require(not path.is_symlink(), 'Unexpected runtime-file symlink')
        rows.append({'file': str(local), 'bytes': path.stat().st_size, 'sha256': digest(path)})
    require(len(rows) >= 5 and any(row['file'].endswith('.wasm.unityweb') for row in rows), 'Incomplete current Gospel Web player')
    # The actual build must still correspond to the current per-game inputs.
    spec = importlib.util.spec_from_file_location('restoration_followup_source_binding',
                                                 ROOT / 'Tools/Validation/restoration_source_binding.py')
    binding = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(binding)
    inputs = read(PLAYER / 'BuildInputs.json')
    fields = dict(line.split(': ', 1) for line in (PLAYER / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line)
    require(inputs['game'] == 'PathOfDawn' and fields.get('SourceFingerprint') == binding.verify(inputs)
            and fields.get('Revision') == 'gospel-toon-2026-10-27' and fields.get('Content') == 'gospel-0.1.0'
            and fields.get('Result') == 'Succeeded' and fields.get('Errors') == '0' and fields.get('Target') == 'WebGL',
            'Current Gospel build/source binding does not hold')
    return normalized(rows)


def captured_sources(report, path, plan_relative):
    harness = report['harness']
    rows = harness.get('capturedSources', [])
    require(len(rows) == 7 and {row['source'] for row in rows} == SOURCES | {plan_relative},
            'Exact seven executed harness/plan copies required')
    by_source, references = {}, []
    for row in rows:
        source = contained_file(path.parent / 'RecordedHarness', row['file'])
        require(digest(source) == row['sha256'], 'Executed harness copy changed: ' + row['source'])
        require(source == (path.parent / 'RecordedHarness' / row['source']).resolve(), 'Unexpected harness copy identity')
        by_source[row['source']] = source
        references.append({'source': row['source'], **reference(source)})
    require(harness.get('file') == 'Tools/Validation/revival-smoke.py'
            and digest(by_source[harness['file']]) == harness['sha256']
            and digest(by_source['Tools/Validation/web-smoke.py']) == harness['helpersSha256']
            and Path(harness['plan']).resolve() == (ROOT / plan_relative).resolve()
            and digest(by_source[plan_relative]) == harness['planSha256'], 'Recorded harness/plan identity differs')
    classifier = harness['pageErrorClassifier']
    require(classifier.get('file') == 'Tools/Validation/fm-provider-errors.py'
            and classifier['sha256'] == digest(by_source[classifier['file']]), 'Page-error classifier copy differs')
    audio = report['audioEvidence']
    require(audio.get('observer') == 'Tools/Validation/webaudio-observer.js'
            and audio.get('observerSha256') == digest(by_source[audio['observer']]), 'Actual PCM observer copy differs')
    if path == SUFFIX:
        require(FOLLOWUP_PLAN.is_file() and digest(FOLLOWUP_PLAN) == harness['planSha256'], 'Authored suffix plan changed after execution')
    plan = read(by_source[plan_relative])['PathOfDawn']
    require(plan.get('revision') == report['revision'] and plan.get('suite') == report['suite'], 'Captured plan is for another revision/suite')
    return plan, by_source, references


def common_report(report, path):
    require(report.get('game') == 'PathOfDawn' and report.get('revision') == 'gospel-toon-2026-10-27'
            and report.get('suite') == 'modern-real-input-v1' and report.get('profile') == 'desktop'
            and report.get('interactionViewport') == {'width': 1280, 'height': 720}, 'Different game/revision/desktop viewport')
    require(report.get('input') and not report.get('pageErrors') and not report.get('httpErrors')
            and not any(row.get('type') == 'error' for row in report.get('console', [])), 'Runtime error or absent ordinary input')
    kinds = {'click', 'key', 'text', 'wheel', 'drag', 'hold', 'touch_hold', 'dom_click', 'key-release-after-original-capture'}
    require(all(row.get('type') in kinds and row.get('navigation') in (0, 1) for row in report['input']),
            'Unknown/nonordinary input or unexpected session navigation')
    require(report.get('checks') and all(row.get('passed') is True for row in report['checks']), 'Not every executed interaction check passed')


def prefix_failure(prefix):
    require(prefix.get('status') == 'failed' and prefix.get('failure') == EXACT_FAILURE
            and len(prefix['checks']) == 35 and not prefix.get('failedRequests'), 'Different prefix failure/checks/request error')
    pending = prefix.get('pendingReview', {})
    require(pending.get('name') == 'hero-run' and pending.get('status') == 'needs-additional-real-input',
            'Prefix did not stop at the exact unapproved running adjustment')
    actions = pending.get('adjustment', {}).get('actions', [])
    require(len(actions) == 2 and actions[0].get('type') == 'click'
            and actions[1].get('type') == 'hold' and actions[1].get('keys') == ['a', 'shift']
            and actions[1].get('releaseAfterCapture') is True and actions[1].get('captureCompositorBurst') is True,
            'Different reviewer correction/key typo')
    decision = contained_file(PREFIX.parent, pending['reviewFile'])
    require(read(decision) == pending['adjustment'], 'Original unapproved reviewer adjustment changed')


def preserved_prefix():
    path = PREFIX.parent / 'prefix-preservation.json'
    value = read(path)
    rows = value.get('files', [])
    require(value.get('status') == 'preserved-exact' and value.get('rawStatus') == 'failed'
            and value.get('rawFailure') == EXACT_FAILURE and value.get('rawReportSha256') == EXPECTED_PREFIX_SHA
            and len(rows) == len({row['file'] for row in rows}) == 44
            and sum(row['bytes'] for row in rows) == 21890713,
            'Different immutable original prefix ledger')
    for row in rows:
        original = (PREFIX.parent / row['file']).resolve()
        require(original.is_file() and original.is_relative_to(PREFIX.parent)
                and original.stat().st_size == row['bytes'] and digest(original) == row['sha256'],
                'Preserved first-session evidence changed: ' + row['file'])
    return reference(path)


def approvals(report, path, names, plan):
    approved = [row for row in report['checkpoints'] if row.get('status') == 'approved']
    require(len(approved) == len(names) and {logical(row['name']) for row in approved} == names,
            'Missing/duplicate approved logical checkpoints: ' + str(path))
    expected = {step['name']: step['expected'] for step in plan['steps'] if step['type'] == 'checkpoint'}
    screenshots = {(shot['name'], shot['path'], shot['sha256']) for shot in report['screenshots']}
    review_files = []
    for row in approved:
        shot, review = row['original'], row['review']
        image = contained_file(path.parent, shot['path'])
        require((shot['name'], shot['path'], shot['sha256']) in screenshots and image.suffix.lower() == '.png'
                and digest(image) == shot['sha256'] == review.get('frameSha256')
                and shot.get('navigation') == row.get('navigation'), 'Original screenshot identity/bytes changed')
        require(review.get('status') == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review.get('expected') == row['expected'] == expected[logical(row['name'])],
                'Original approval or authored expectation differs')
        decision = contained_file(path.parent, row['reviewFile'])
        require(read(decision) == review, 'Original review decision file changed')
        review_files.append(reference(decision))
        if logical(row['name']) in {'hero-walk', 'hero-run'}:
            require(shot.get('captureKind') == 'original-full-viewport-compositor-screencast-png'
                    and any(candidate['path'] == shot['path'] and candidate['sha256'] == shot['sha256']
                            for candidate in row.get('candidateOriginals', [])), 'Movement lacks its reviewed actual compositor original')
    return approved, review_files


def cache_requests(suffix):
    failures = suffix.get('failedRequests', [])
    if not failures:
        return []
    diagnostic = suffix.get('networkDiagnostic', {})
    require(failures == diagnostic.get('verifiedUnityCacheRevalidationAborts')
            and not diagnostic.get('unexpectedFailures') and not diagnostic.get('externalMusicServiceFailures'),
            'A follow-up request failure is not exact proved Unity reload revalidation')
    proofs = []
    for failure in failures:
        url = failure['url']
        parsed = urlsplit(url)
        require(failure.get('navigation') == 1 and failure.get('method') == 'GET'
                and failure.get('error') == 'net::ERR_ABORTED' and failure.get('resourceType') == 'fetch'
                and parsed.scheme == 'http' and parsed.hostname == '127.0.0.1'
                and parsed.path.endswith('.data.unityweb') and not parsed.query and not parsed.fragment
                and any(row.get('navigation') == 1 and row.get('dataUrl') == url for row in suffix['unityConfigurations']),
                'Different cached reload request')
        require(any(row.get('navigation') == 1 and row.get('url') == url and row.get('method') == 'GET'
                    and row.get('status') == 304 for row in suffix['networkResponses']), 'No exact cache HTTP304')
        marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
        require(any(row.get('navigation') == 1 and row.get('text') == marker for row in suffix['console'])
                and any(row.get('navigation') == 1 and row.get('status') == 'approved'
                        and logical(row['name']) == 'continued-temple' for row in suffix['checkpoints']),
                'No exact successful cache log and reviewed reloaded journey')
        proofs.append({'request': failure, 'httpStatus': 304, 'unityCacheMessage': marker})
    return proofs


def pcm_windows(report, path, labels, captured):
    windows = report['audioEvidence']['windows']
    require([row['label'] for row in windows] == labels, 'Different/missing actual PCM windows')
    spec = importlib.util.spec_from_file_location('restoration_followup_pcm_' + path.parent.parent.name,
                                                 captured['Tools/Validation/web-smoke.py'])
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    references = []
    for window in windows:
        evidence = (path.parent / window['rawEvidence']).resolve()
        require(evidence.is_file() and evidence.is_relative_to(path.parent)
                and digest(evidence) == window['sha256'], 'Raw PCM evidence changed/outside session')
        raw = read(evidence)
        require(raw.get('label') == window['label'] and raw.get('snapshots'), 'PCM label/snapshots absent')
        derived = helper.music_audio_summary(raw['snapshots'])
        require(derived == raw['summary'] and all(window.get(key) == value for key, value in derived.items() if key != 'frames'),
                'PCM summary no longer matches exact original snapshots')
        require(not derived['observerErrors'] and derived['nonfiniteSamples'] == 0, 'Invalid/nonfinite PCM observations')
        if window['label'] in {'before-first-gesture', 'muted'}:
            require(derived['maximumRms'] < 1e-5, 'Initial silence/music mute does not hold')
            if window['label'] == 'muted':
                require(derived['sampleCount'] > 0 and derived['allContextsRunning']
                        and derived['isolatedMusicRoute'] and derived['contextAdvancedSeconds'] > .5
                        and abs(derived['medianDestinationGain']) < 1e-7, 'Muted loop was not actually observed running silently')
        else:
            require(derived['sampleCount'] >= 6 and derived['allContextsRunning'] is True
                    and derived['isolatedMusicRoute'] is True and len(derived['sourceIdentities']) == 1
                    and derived['audibleSampleFraction'] >= .5 and derived['medianRms'] > 1e-5
                    and derived['contextAdvancedSeconds'] > .5, 'Missing actual advancing isolated music output PCM')
        if window['label'] == 'after-continue-gesture':
            require(window.get('navigation') == 1, 'Reload/continue PCM is not from the new real navigation')
        elif path == PREFIX:
            require(window.get('navigation') == 0, 'Prefix PCM came from another navigation')
        references.append({'label': window['label'], 'navigation': window['navigation'], **reference(evidence)})
    return windows, references


def analyze(prefix_path=PREFIX):
    prefix_path = Path(prefix_path).resolve()
    require(prefix_path == PREFIX and digest(prefix_path) == EXPECTED_PREFIX_SHA,
            'Only the exact recorded reviewer-key typo prefix is eligible')
    prefix = read(prefix_path)
    common_report(prefix, PREFIX)
    prefix_failure(prefix)
    preservation = preserved_prefix()
    original_plan, prefix_copies, prefix_copy_refs = captured_sources(prefix, PREFIX, 'Tools/Validation/restoration-smoke-plans.json')
    require(set(original_plan['requiredCheckpoints']) == FIRST | MISSING, 'Original seventeen-checkpoint scope differs')
    first, first_reviews = approvals(prefix, PREFIX, FIRST, original_plan)
    early, early_refs = pcm_windows(prefix, PREFIX, ['before-first-gesture', 'after-first-gesture', 'muted', 'unmuted'], prefix_copies)
    require(early[1]['sourceIdentities'] == early[2]['sourceIdentities'] == early[3]['sourceIdentities'], 'Prefix mute/unmute changed the observed music source')

    require(SUFFIX.is_file(), 'The separate ordinary-start follow-up is not complete')
    suffix = read(SUFFIX)
    common_report(suffix, SUFFIX)
    require(suffix.get('status') == 'passed' and not suffix.get('failure') and not suffix.get('pendingReview'),
            'Follow-up must independently pass every executed check and visible review')
    followup_plan, suffix_copies, suffix_copy_refs = captured_sources(suffix, SUFFIX, str(FOLLOWUP_PLAN.relative_to(ROOT)))
    require(set(followup_plan['requiredCheckpoints']) == MISSING | {EXTRA}, 'Follow-up must cover every missing gate plus a fresh temple start')
    original_expectations = {step['name']: step['expected'] for step in original_plan['steps'] if step['type'] == 'checkpoint'}
    followup_expectations = {step['name']: step['expected'] for step in followup_plan['steps'] if step['type'] == 'checkpoint'}
    require(all(followup_expectations.get(name) == original_expectations[name] for name in MISSING),
            'Follow-up weakened/changed an original missing checkpoint expectation')
    last, last_reviews = approvals(suffix, SUFFIX, MISSING | {EXTRA}, followup_plan)
    require({logical(row['name']) for row in first + last} - {EXTRA} == FIRST | MISSING,
            'Two sessions do not cover the original seventeen gates')
    require(all(row.get('navigation') == 0 for row in first)
            and next(row for row in last if logical(row['name']) == 'continued-temple')['navigation'] == 1,
            'Session checkpoint navigation differs')
    current = runtime_files()
    require(normalized(prefix['buildFingerprint']['files']) == normalized(suffix['buildFingerprint']['files']) == current,
            'Prefix/follow-up/current runtime bytes differ')
    review = suffix.get('visualReview', {})
    require(review.get('status') == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
            and normalized(review['runtimeFiles']) == current
            and {(s['path'], s['sha256']) for s in review['screenshots']} == {(row['original']['path'], row['original']['sha256']) for row in last},
            'Follow-up aggregate review does not bind its actual current originals')

    # The new process must take the normal title route, acknowledge the three
    # birth pages, equip through actual UI, then later load a real saved journey.
    gestures = suffix['input']
    start = next(step for step in original_plan['steps'] if step.get('label') == '새 여정')
    birth = next(step for step in original_plan['steps'] if step.get('label') == '다음 탄생 구절')
    require(len(gestures) >= 4 and gestures[0].get('type') == 'click' and gestures[0].get('point') == start['point']
            and gestures[0].get('navigation') == 0 and all(row.get('type') == 'click' and row.get('point') == birth['point']
                and row.get('navigation') == 0 for row in gestures[1:4]), 'Follow-up skipped ordinary new journey/three birth pages')
    require(any(row.get('navigation') == 0 and row.get('type') == 'hold'
                and 'Shift' in row.get('keys', []) and any(key in row.get('keys', []) for key in ('w', 'a', 's', 'd')) for row in gestures),
            'No actual correctly spelled running key input in follow-up')
    require(any(row.get('navigation') == 0 and row.get('type') == 'click' and '등잔' in row.get('label', '') and '장착' in row.get('label', '') for row in gestures),
            'No actual lamp-equipping UI input in fresh follow-up')
    require(any(row.get('navigation') == 0 and row.get('type') == 'click' and '저장' in row.get('label', '') for row in gestures)
            and any(row.get('navigation') == 1 and row.get('type') == 'click' and '이어하기' in row.get('label', '') for row in gestures)
            and {row.get('navigation') for row in suffix['unityConfigurations']} == {0, 1}, 'No real save/full reload/continue route')
    labels = [step['label'] for step in followup_plan['steps'] if step['type'] == 'audio']
    require(labels.count('after-continue-gesture') == 1, 'Follow-up plan must request fresh after-continue output')
    late, late_refs = pcm_windows(suffix, SUFFIX, labels, suffix_copies)
    cache = cache_requests(suffix)
    approved = first + last
    return {'status': 'verified', 'verifiedUtc': datetime.now(timezone.utc).isoformat(), 'ordinarySessions': 2,
            'rawReport': reference(PREFIX), 'suffixReport': reference(SUFFIX),
            'rawStatuses': {'prefix': 'failed', 'suffix': 'passed'},
            'prefixFailure': EXACT_FAILURE, 'prefixSha256': EXPECTED_PREFIX_SHA,
            'prefixPreservation': preservation,
            'prefixPlan': reference(prefix_copies['Tools/Validation/restoration-smoke-plans.json']),
            'suffixPlan': reference(suffix_copies[str(FOLLOWUP_PLAN.relative_to(ROOT))]),
            'capturedSources': {'prefix': prefix_copy_refs, 'suffix': suffix_copy_refs},
            'analysisSource': reference(Path(__file__)),
            'sourceBindingTool': reference(ROOT / 'Tools/Validation/restoration_source_binding.py'),
            'buildInputs': reference(PLAYER / 'BuildInputs.json'), 'buildEvidence': reference(PLAYER / 'BuildEvidence.txt'),
            'approvedCheckpoints': approved, 'reviewFiles': first_reviews + last_reviews,
            'audioWindows': early + late, 'audioEvidenceFiles': early_refs + late_refs,
            'totalChecks': len(prefix['checks']) + len(suffix['checks']), 'pcmWindows': len(early) + len(late),
            'checksBySession': {'prefix': len(prefix['checks']), 'suffix': len(suffix['checks'])},
            'classifiedFollowupCacheRequests': cache,
            'evidenceParents': [str(PREFIX.parent.relative_to(ROOT)), str(SUFFIX.parent.relative_to(ROOT))],
            'visualReview': {'status': 'approved', 'reviewer': '; '.join(sorted({row['review']['reviewer'] for row in approved})),
                'reviewedUtc': max(row['review']['reviewedUtc'] for row in approved), 'runtimeFiles': current,
                'screenshots': [row['original'] for row in approved], 'findings': [row['review']['observed'] for row in approved]},
            'scope': 'Two separate ordinary-start desktop browser sessions on identical current Gospel runtime bytes. '
                'The exact prefix remains raw FAILED solely for Keyboard.down Unknown key shift in the unapproved running-frame reviewer adjustment; '
                'all 35 earlier executed checks remain true and only its ten actually approved gates are retained. '
                'The independently PASSED fresh follow-up supplies all seven unreached original gates plus its own temple-start original, '
                'after normal Start New, three birth pages and actual lamp equip; it includes actual save/full reload/continue and advancing isolated after-continue PCM. '
                'Every approval is an existing original decision, never created or copied by this verifier. '
                'The original running frame is not retroactively approved. This is NOT one uninterrupted run, '
                'not a rewritten automatic all-pass and not proof of physical-device performance or human speaker listening. '
                'No game-state, save-payload, camera or runtime injection is used; raw reports/images/inputs/PCM remain unchanged.'}


def verify_record(prefix_path=PREFIX):
    current = analyze(prefix_path)
    recorded = read(Path(prefix_path).parent / 'followup-verification.json')
    keys = set(current) - {'verifiedUtc'}
    require(keys == set(recorded) - {'verifiedUtc'} and recorded.get('verifiedUtc'), 'Recorded two-session proof schema changed')
    for key in keys:
        require(current[key] == recorded[key], 'Recorded two-session qualification changed: ' + key)
    return recorded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record', action='store_true', help='Record once after all fresh follow-up evidence is complete')
    args = parser.parse_args()
    path = PREFIX.parent / 'followup-verification.json'
    if args.record and not path.exists():
        result = analyze()
        # The proof itself is the only output. Existing proof bytes are never overwritten.
        with path.open('x') as output:
            output.write(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    else:
        result = verify_record()
    print(json.dumps({'status': result['status'], 'ordinarySessions': result['ordinarySessions'],
                      'rawStatuses': result['rawStatuses'], 'totalChecks': result['totalChecks'],
                      'approvedOriginals': len(result['approvedCheckpoints']), 'pcmWindows': result['pcmWindows']}))


if __name__ == '__main__':
    try:
        main()
    except (AssertionError, ValueError, KeyError, OSError, TypeError) as error:
        print('Restoration two-session qualification refused: ' + str(error), file=sys.stderr)
        sys.exit(1)
