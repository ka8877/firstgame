#!/usr/bin/env python3
"""Exact current Restoration public range qualification; raw FAILED status is retained.

No browser, playback, HTTP request, original frame or raw evidence is modified.
Startup recovery, observed active progress and unused preloads are distinguished.
"""
from __future__ import annotations
from datetime import datetime, timezone
import argparse
import importlib.util
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/report.json'
PORTAL = ROOT / 'Artifacts/Validation/Restoration/PublicPortal/report.json'
PLAYER = Path('/tmp/firstgame-restoration-builds/FantasyCards/Web')
EXPECTED_REPORT_SHA = 'a9e4f95733d082cafe09c6790ec0183c0c8fdee787f028fabb251885f8e761d7'
EXPECTED_PORTAL_SHA = 'ad9b475486af48f360da0ec69f7157ad45fe063897f683e35ffdf8ea946b254b'
EXPECTED_SOURCE_COMMIT = '540ca407f34c60bd23b9f6b6ec3e9a177f3dc9a0'
BASE_URL = 'https://ka8877.github.io/firstgame/games/fantasy-cards/'
PREFIX = 'StreamingAssets/Audio/ModernCard/'
GATE = 'No unexpected failed requests; exact proved cache/local-media cancellations retained'
EXPECTED_REQUESTS = (('lobby.ogg', 1793123796811.6155), ('battle.ogg', 1793123796812.1223), ('deck.ogg', 1793123796832.1404))
CAPTURE_SNAPSHOTS = {'Tools/Validation/revival-smoke.py': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/revival-smoke.py', 'Tools/Validation/web-smoke.py': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/web-smoke.py', 'Tools/Validation/fm-provider-errors.py': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/fm-provider-errors.py', 'Tools/Validation/fm-local-media-errors.py': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/fm-local-media-errors.py', 'Tools/Validation/webaudio-observer.js': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/webaudio-observer.js', 'Tools/Validation/fm-local-media-observer.js': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/fm-local-media-observer.js', 'Tools/Validation/restoration-public-plans.json': 'Artifacts/Validation/Restoration/PublicMobile844/FantasyCards/RecordedHarness/Tools/Validation/restoration-public-plans.json'}
SOURCE_HASHES = {'Tools/Validation/revival-smoke.py': 'd9355f4c7c59f8a4ac950c86a049003ba4ac7eb3d858b6f62ca0aefd2e638511', 'Tools/Validation/web-smoke.py': '389434c604ce7661405383fb3e2919c410a22ce3d1bc9ce03a7a19c52d64ffc6', 'Tools/Validation/fm-provider-errors.py': 'b2bc8a667a8fb2763eeb8caff28997ff18a3e0b1e04a014e182b7a57adc31b62', 'Tools/Validation/fm-local-media-errors.py': 'ce5b9f769da1047a89bde5d0da6b2c612edd48592f2be2d9523cde0d8b2c83bd', 'Tools/Validation/webaudio-observer.js': 'b6ac9834dd3ad5557453aa7bfd6a0920038dbf4659396a1505bc9e1cc1ddaaba', 'Tools/Validation/fm-local-media-observer.js': 'a419e0958837951ff684afbd959d134d3a48cb7ad90537a25594b61c0212c82f', 'Tools/Validation/restoration-public-plans.json': '87d87ff460aa2b2a82c9dedefc5224463cef0795968be1e5d5299d21372ee825'}


def require(value, message):
    if not value: raise ValueError(message)


def digest(path):
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): value.update(block)
    return value.hexdigest()


def read(path): return json.loads(Path(path).read_text())


def reference(path):
    path = Path(path).resolve()
    require((path.is_relative_to(ROOT) or path.is_relative_to(PLAYER)) and path.is_file(), 'Missing/outside evidence: ' + str(path))
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path), 'sha256': digest(path)}


def capture_source(name, sha):
    if name in CAPTURE_SNAPSHOTS:
        snapshot = ROOT / CAPTURE_SNAPSHOTS[name]
        require(snapshot.is_file() and digest(snapshot) == sha, 'Explicit original Restoration harness snapshot changed')
        return snapshot
    path = ROOT / name
    if path.is_file() and digest(path) == sha: return path
    for parent in (REPORT.parents[2] / 'HarnessHistory', REPORT.parent / 'HarnessHistory'):
        if parent.exists():
            for candidate in parent.rglob('*'):
                if candidate.is_file() and candidate.suffix == path.suffix and digest(candidate) == sha: return candidate
    raise ValueError('Exact Restoration capture source/snapshot changed: ' + name)


def number(value): return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def player(row, url):
    matches = [item for item in row.get('players', []) if item.get('source') == url]
    require(len(matches) == 1, 'Exact single local element missing')
    return matches[0]


def inactive(row, url, decoded=True):
    item = player(row, url)
    return (item.get('active') is False and item.get('paused') is True and item.get('pending') is False
            and item.get('currentTime') == 0 and (not decoded or item.get('readyState', 0) >= 2))


def playing(row, url):
    state, item = row.get('state', {}), player(row, url)
    return (state.get('sourceKind') == 'bundled-instrumental' and state.get('actualSource') == url
            and state.get('contextState') == 'running' and state.get('unlocked') is True
            and state.get('actualPaused') is False and state.get('muted') is False
            and number(state.get('volume')) and state['volume'] > .1
            and item.get('active') is True and item.get('paused') is False and item.get('pending') is False
            and item.get('readyState', 0) >= 2 and number(item.get('currentTime')))


def reaches(context, source, destination):
    gains = {item['nodeId']: item['value'] for item in context.get('gains', [])}
    edges = {}
    for start, end, _output, _input in context.get('connections', []): edges.setdefault(start, []).append(end)
    todo, seen = [source], set()
    while todo:
        node = todo.pop()
        if node == destination: return True
        if node in seen: continue
        seen.add(node)
        if node in gains and (not number(gains[node]) or gains[node] <= 1e-5): continue
        todo.extend(edges.get(node, []))
    return False


def pcm(raw, label, filename, navigation):
    url = BASE_URL + PREFIX + filename
    matches = [window for window in raw['audioEvidence']['windows'] if window['label'] == label and window['navigation'] == navigation]
    require(len(matches) == 1, 'Exact current PCM window missing: ' + label)
    window = matches[0]
    path = (REPORT.parent / window['rawEvidence']).resolve()
    require(path.is_relative_to(REPORT.parent) and digest(path) == window['sha256'], 'PCM evidence changed')
    saved = read(path)
    require(all(window.get(key) == value for key, value in saved['summary'].items() if key != 'frames'), 'PCM summary differs')
    require(window['observerErrors'] == [] and window['nonfiniteSamples'] == 0
            and window['allContextsRunning'] is True and window['isolatedMusicRoute'] is True
            and window['sourceUrls'] == [url] and window['sampleCount'] >= 6
            and window['audibleSampleFraction'] >= .5 and window['medianRms'] > 1e-5
            and window['mediaProgressSeconds'] > .5 and len(window['sourceIdentities']) == 1, 'PCM predicates failed')
    context_id, source_id = window['sourceIdentities'][0]
    levels, positions, durations = [], [], []
    for snapshot in saved['snapshots']:
        require(snapshot['errors'] == [], 'PCM observer errors')
        contexts = [context for context in snapshot['contexts'] if context['id'] == context_id]
        require(len(contexts) == 1, 'Recorded music context missing')
        context = contexts[0]
        sources = [source for source in context['media'] if source['nodeId'] == source_id and source['currentSrc'] == url]
        require(context['state'] == 'running' and len(sources) == 1, 'PCM graph/source changed')
        source = sources[0]
        require(source['paused'] is False and source['ended'] is False and source['loop'] is True
                and source['muted'] is False and source['volume'] > .1 and source['readyState'] >= 2, 'PCM media is not playing')
        audible = [item for item in context['media'] if not item['paused'] and not item['muted']
                   and reaches(context, item['nodeId'], context['destinationNodeId'])]
        require([item['nodeId'] for item in audible] == [source_id], 'Another music source shares PCM route')
        require(context['destinationConnections'] == 1 and len(context['destinationRoutes']) == 1, 'Music destination not isolated')
        route = context['destinationRoutes'][0]
        require(reaches(context, source_id, route['sourceNodeId']), 'Analyser not on selected source route')
        channels = route['channels']
        require(channels and all(channel['nonfinite'] == 0 and channel['samples'] > 0 and number(channel['rms']) for channel in channels), 'Invalid raw PCM')
        levels.append(math.sqrt(sum(channel['rms'] ** 2 for channel in channels) / len(channels)))
        positions.append(source['currentTime'])
        durations.append(source['duration'])
    require(all(number(duration) and duration > 18 for duration in durations), 'Invalid sampled source duration')
    sampled_progress = sum((current - previous) % duration for previous, current, duration in zip(positions, positions[1:], durations[1:]))
    require(len(levels) == window['sampleCount'] and len(levels) >= 6 and statistics.median(levels) == window['medianRms']
            and sum(value > 1e-5 for value in levels) / len(levels) >= .5 and sampled_progress > .5
            and math.isclose(sampled_progress, window['mediaProgressSeconds'], rel_tol=0, abs_tol=1e-6), 'Raw PCM does not support summary')
    return {'label': label, 'navigation': navigation, **reference(path), 'sourceUrl': url,
            'sourceIdentity': [context_id, source_id], 'sampleCount': len(levels), 'medianRms': statistics.median(levels),
            'positionsSeconds': [positions[0], positions[-1]], 'mediaProgressSeconds': sampled_progress,
            'sampledPlayheadWraps': sum(current < previous for previous, current in zip(positions, positions[1:])),
            'startedEpochMs': window['startedEpochMs'], 'finishedEpochMs': window['finishedEpochMs']}


def decode_media(raw, filename):
    relative = PREFIX + filename
    expected = next(row for row in raw['buildFingerprint']['files'] if row['file'] == relative)
    path = PLAYER / relative
    require(path.stat().st_size == expected['bytes'] and digest(path) == expected['sha256'], 'Exact media bytes changed')
    metadata = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'a:0',
        '-show_entries', 'stream=codec_name,sample_rate,channels:format=duration', '-of', 'json', str(path)], text=True))
    require(len(metadata.get('streams', [])) == 1 and float(metadata['format']['duration']) > 18, 'Invalid audio stream')
    completed = subprocess.run(['ffmpeg', '-hide_banner', '-v', 'error', '-xerror', '-i', str(path),
        '-map', '0:a:0', '-f', 'null', '-'], capture_output=True, text=True, check=False)
    require(completed.returncode == 0 and not completed.stderr, 'Full FFmpeg decode failed: ' + filename)
    return {'runtime': expected, 'metadata': metadata, 'fullDecodeExitCode': completed.returncode,
            'decode': 'ffmpeg -hide_banner -v error -xerror -i <exact SHA256 file> -map 0:a:0 -f null -'}


def analyze(path=REPORT):
    path = Path(path).resolve()
    require(path == REPORT and digest(path) == EXPECTED_REPORT_SHA, 'Only this exact Restoration public raw report is supported')
    raw = read(path)
    require(raw['revision'] == 'fm-restoration-2026-10-27' and raw['game'] == 'FantasyCards'
            and raw['suite'] == 'modern-real-input-v1' and raw['profile'] == 'mobile-landscape'
            and raw['status'] == 'failed' and raw['failure'] == GATE, 'Different public raw failure')
    bad = [check for check in raw['checks'] if check.get('passed') is not True]
    require(len(raw['checks']) == 34 and len(bad) == 1 and bad[0]['name'] == GATE, 'Other current public checks failed')
    require(not raw['pageErrors'] and not raw['httpErrors'] and not any(item['type'] == 'error' for item in raw['console']), 'Other public runtime errors')
    sources = {name: reference(capture_source(name, sha)) for name, sha in SOURCE_HASHES.items()}
    harness = raw['harness']
    copies = harness.get('capturedSources', [])
    require(len(copies) == 7 and {item['source']: item['sha256'] for item in copies} == SOURCE_HASHES,
            'Different executed seven-source capture set')
    for item in copies:
        require((ROOT / item['file']).resolve() == capture_source(item['source'], item['sha256']).resolve(),
                'Executed capture path differs from the exact recorded source copy')
    require(harness['sha256'] == SOURCE_HASHES[harness['file']]
            and harness['planSha256'] == SOURCE_HASHES['Tools/Validation/restoration-public-plans.json']
            and harness['helpersSha256'] == SOURCE_HASHES['Tools/Validation/web-smoke.py'], 'Current capture sources differ')
    for key in ('pageErrorClassifier', 'localMediaClassifier'):
        require(harness[key]['sha256'] == SOURCE_HASHES[harness[key]['file']], 'Capture classifier changed')
    for record in ({'file': raw['audioEvidence']['observer'], 'sha256': raw['audioEvidence']['observerSha256']}, raw['audioEvidence']['localMediaObserver']):
        require(record['sha256'] == SOURCE_HASHES[record['file']], 'Passive observer changed')
    plan = read(capture_source('Tools/Validation/restoration-public-plans.json', harness['planSha256']))['FantasyCards']
    require(plan['revision'] == raw['revision'] and plan['suite'] == raw['suite'] and set(plan['requiredCheckpoints']) == {'title', 'lobby'}, 'Wrong public plan')
    approved = [item for item in raw['checkpoints'] if item['status'] == 'approved']
    require(len(approved) == 2 and {item['name'] for item in approved} == {'title', 'lobby'}, 'Exactly two existing approved originals required')
    for item in approved:
        shot, review = item['original'], item['review']; image = (ROOT / shot['path']).resolve()
        expected = next(step['expected'] for step in plan['steps'] if step.get('type') == 'checkpoint' and step['name'] == item['name'])
        require(image.is_relative_to(REPORT.parent) and digest(image) == shot['sha256'] == review['frameSha256']
                and review['status'] == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review['expected'] == item['expected'] == expected, 'Original existing approval changed')
        decision = (ROOT / item['reviewFile']).resolve()
        require(decision.is_relative_to(REPORT.parent) and read(decision) == review
                and any(original['path'] == shot['path'] and original['sha256'] == shot['sha256']
                        for original in raw['screenshots']), 'Existing original review decision changed')
    require(digest(PORTAL) == EXPECTED_PORTAL_SHA, 'Exact public full-byte portal report changed')
    portal, stage = read(PORTAL), ROOT / 'Artifacts/RestorationWebPlaytest'
    manifest = read(stage / 'build-manifest.json')
    require(portal['status'] == 'passed' and portal['visualReview']['status'] == 'approved'
            and portal['sourceCommit'] == manifest['sourceCommit'] == EXPECTED_SOURCE_COMMIT
            and manifest['releaseProfile'] == 'restoration' and manifest['sourceDirty'] is False
            and manifest['gameRevisions'] == {'FantasyCards': 'fm-restoration-2026-10-27', 'PathOfDawn': 'gospel-toon-2026-10-27'}
            and portal['manifest']['sha256'] == digest(stage / 'build-manifest.json'), 'Current public manifest/source proof differs')
    expected_public = {**manifest['files'], 'build-manifest.json': digest(stage / 'build-manifest.json')}
    require(len(portal['files']) == 46 and {item['file'] for item in portal['files']} == set(expected_public), 'Complete current public file set required')
    for item in portal['files']:
        current = stage / item['file']
        require(item['status'] == 200 and item['passed'] is True and item['sha256'] == expected_public[item['file']] == digest(current)
                and item['bytes'] == current.stat().st_size, 'Public portal full-file bytes differ')
    delivery = raw['publicDelivery']
    require(delivery['status'] == 'passed' and delivery['gameUrl'] == BASE_URL, 'Current full HTTPS game delivery required')
    fingerprint = raw['buildFingerprint']['files']
    require(len(fingerprint) == len({item['file'] for item in fingerprint}) == len(delivery['files'])
            and {item['file'] for item in fingerprint} == {item['file'] for item in delivery['files']}, 'Full game delivery set differs')
    for item in fingerprint:
        current = (PLAYER / item['file']).resolve()
        delivered = next(row for row in delivery['files'] if row['file'] == item['file'])
        public = next(row for row in portal['files'] if row['file'] == 'games/fantasy-cards/' + item['file'])
        require(current.is_relative_to(PLAYER) and current.is_file() and current.stat().st_size == item['bytes']
                and digest(current) == item['sha256'] == delivered['sha256'] == public['sha256']
                and delivered['bytes'] == public['bytes'] == item['bytes'] and delivered['status'] == 200
                and delivered['url'] == BASE_URL + item['file'], 'Delivered/current/portal game bytes differ')
    current_names = {str(path.relative_to(PLAYER)) for path in PLAYER.rglob('*') if path.is_file()
        and (path.relative_to(PLAYER).as_posix() == 'index.html'
             or path.relative_to(PLAYER).parts[0] in ('Build', 'StreamingAssets', 'TemplateData'))
        and not any('DoNotShip' in part or 'DontShip' in part for part in path.relative_to(PLAYER).parts)
        and path.suffix.lower() not in {'.log', '.map', '.pdb', '.mdb'}}
    require(current_names == {item['file'] for item in fingerprint}, 'Current runtime file set differs')
    spec = importlib.util.spec_from_file_location('restoration_public_range_source_binding', ROOT / 'Tools/Validation/restoration_source_binding.py')
    binding = importlib.util.module_from_spec(spec); spec.loader.exec_module(binding)
    inputs = read(PLAYER / 'BuildInputs.json')
    evidence = dict(line.split(': ', 1) for line in (PLAYER / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line)
    require(inputs['game'] == 'FantasyCards' and evidence.get('SourceFingerprint') == binding.verify(inputs)
            and evidence.get('Revision') == 'fm-restoration-2026-10-27' and evidence.get('Result') == 'Succeeded'
            and evidence.get('Errors') == '0' and evidence.get('Target') == 'WebGL'
            and evidence.get('Content') == 'fm-classic-0.1.0', 'Current source/build input binding differs')
    decoded = {filename: decode_media(raw, filename) for filename in ('title.ogg', 'lobby.ogg', 'deck.ogg', 'battle.ogg')}
    title_pcm = pcm(raw, 'title-instrumental', 'title.ogg', 0)
    lobby_pcm = pcm(raw, 'public-lobby-instrumental', 'lobby.ogg', 0)
    require(len(raw['audioEvidence']['windows']) == 2, 'Different public PCM coverage')
    refs = raw['audioEvidence']['localMediaTimelines']
    require(len(refs) == 1 and refs[0]['navigation'] == 0, 'Single current public navigation timeline required')
    timeline = (REPORT.parent / refs[0]['rawEvidence']).resolve()
    require(timeline.is_relative_to(REPORT.parent) and digest(timeline) == refs[0]['sha256'], 'Public passive timeline changed')
    saved = read(timeline)
    rows = sorted(saved['observations'], key=lambda row: (row['epochMs'], row['atMs']))
    require(saved['navigation'] == 0 and rows and all(row['navigation'] == 0 and len(row['players']) == 4 for row in rows), 'Different public timeline navigation/elements')
    first = rows[0]; origin = first['epochMs'] - first['atMs']
    requests = raw['failedRequests']
    require([(item['url'].rsplit('/', 1)[-1], item['observedEpochMs']) for item in requests] == list(EXPECTED_REQUESTS), 'Different exact public requests')
    diagnostic = raw['networkDiagnostic']
    require(diagnostic['unexpectedFailures'] == requests and not any(diagnostic.get(key) for key in
        ('verifiedUnityCacheRevalidationAborts', 'verifiedInactiveLocalMediaCancellations',
         'verifiedInactiveLocalPreloadCancellations', 'verifiedRecoveredPublicPregestureRanges',
         'externalMusicServiceFailures')), 'Other classifier result or failures')
    proofs = []
    for failure in requests:
        filename, when = failure['url'].rsplit('/', 1)[-1], failure['observedEpochMs']
        url = BASE_URL + PREFIX + filename
        require(failure['url'] == url and failure['navigation'] == 0 and failure['method'] == 'GET'
                and failure['resourceType'] == 'media' and failure['error'] == 'net::ERR_ABORTED'
                and 0 <= when - origin < 400, 'Not the exact early public OGG range cancellation')
        responses = [item for item in raw['networkResponses'] if item['navigation'] == 0
                     and item['url'] == url and item['method'] == 'GET' and item['status'] == 206]
        require(len(responses) >= 2, 'Repeated same-navigation public HTTP206 observations missing')
        require(0 < first['epochMs'] - when < 100 and first['state']['actualSource'] is None
                and first['state']['actualPaused'] is True and inactive(first, url, decoded=False),
                'First post-cancellation observation differs')
        loaded = next((row for row in rows if when < row['epochMs'] <= when + 1000
                       and player(row, url)['readyState'] >= 2 and inactive(row, url)), None)
        ready = next((row for row in rows if loaded and row['epochMs'] >= loaded['epochMs']
                      and player(row, url)['readyState'] >= 4 and inactive(row, url)), None)
        loaded_event = next((row for row in rows if row['reason'] == 'media-loadeddata:fm-local-audio-' + filename[:-4]
                             and when < row['epochMs'] <= when + 1000 and inactive(row, url)), None)
        require(loaded and ready and loaded_event, 'Cancelled element did not subsequently decode through actual browser loadeddata/ready state')
        proof = {'request': failure, 'category': 'early-cancellation-followed-by-observed-browser-decode',
            'runtime': decoded[filename]['runtime'], 'http206Responses': len(responses),
            'millisecondsAfterNavigationClockOrigin': when - origin,
            'millisecondsUntilFirstObservation': first['epochMs'] - when,
            'firstPostCancellationObservation': first, 'subsequentlyDecoded': loaded,
            'subsequentlyReady': ready, 'actualLoadeddataEvent': loaded_event,
            'limitation': 'No passive sample precedes this cancellation; its pre-cancellation element state and pre-gesture timing are not measured. HTMLMediaElement.error and media error events were not recorded.'}
        if filename == 'lobby.ogg':
            require(lobby_pcm['startedEpochMs'] > ready['epochMs'], 'No later same-lobby actual PCM')
            proof['subsequentSameSourcePcm'] = lobby_pcm
        else:
            require(all(inactive(row, url) for row in rows if row['epochMs'] >= ready['epochMs']),
                    'Unused decoded source subsequently became active/unready')
            require(title_pcm['startedEpochMs'] > ready['epochMs'], 'No subsequent selected-title PCM')
            proof['subsequentSelectedTitlePcm'] = title_pcm
            proof['unusedSourceLimit'] = 'This source remained a decoded paused zero-position preload. Public selection/playback audibility of this source was not tested.'
        proofs.append(proof)
    visual = {'status': 'approved', 'reviewer': '; '.join(sorted({item['review']['reviewer'] for item in approved})),
        'reviewedUtc': max(item['review']['reviewedUtc'] for item in approved), 'runtimeFiles': fingerprint,
        'screenshots': [item['original'] for item in approved], 'findings': [item['review']['observed'] for item in approved],
        'method': 'Derived aggregation of the two existing explicit original-frame approvals; no new approval or pixel edit.'}
    return {'status': 'verified', 'analyzedUtc': datetime.now(timezone.utc).isoformat(),
        'rawStatus': 'failed', 'checks': 34, 'passedChecks': 33,
        'qualificationFile': str((REPORT.parent / 'public-range-verification.json').relative_to(ROOT)),
        'rawReport': {**reference(path), 'status': 'failed'}, 'analysisSource': reference(Path(__file__)), 'captureSources': sources,
        'publicPortal': reference(PORTAL), 'publicManifest': portal['manifest'], 'publicSourceCommit': EXPECTED_SOURCE_COMMIT,
        'publicDelivery': delivery, 'decodedMedia': decoded,
        'buildInputs': reference(PLAYER / 'BuildInputs.json'), 'buildEvidence': reference(PLAYER / 'BuildEvidence.txt'),
        'sourceBindingTool': reference(ROOT / 'Tools/Validation/restoration_source_binding.py'),
        'decoderVersion': subprocess.check_output(['ffmpeg', '-version'], text=True).splitlines()[0],
        'localMediaTimeline': reference(timeline), 'requests': proofs,
        'pcmWindows': {'title': title_pcm, 'lobby': lobby_pcm}, 'otherRawChecksPassed': 33,
        'visualReview': visual,
        'qualifiedReport': {'rawStatus': 'failed', 'rawChecks': 34, 'passedChecks': 33,
            'reviewedOriginals': 2, 'recordedPcmWindows': 2, 'exactMediaCancellations': 3},
        'scope': 'The automatic public network gate remains raw FAILED (33 of 34 checks passed). This separate proof is restricted to three exact same-origin OGG GET ERR_ABORTED events on navigation 0. '
            'All three precede the first passive media observation by 64 to 85 ms, so pre-cancellation state, exact pre-gesture timing and uninterrupted playback across them are not claimed. '
            'Actual subsequent browser loadeddata/ready-state recovery is recorded for lobby, battle and deck. The observer did not capture HTMLMediaElement.error or media error events; absence of those errors is not inferred. '
            'No page/HTTP/console errors were recorded. Current full HTTPS bytes, all four complete OGG decodes, and later isolated advancing title/lobby PCM in the same session are verified. '
            'Deck and battle stayed unused decoded paused preloads, with no public deck/battle audibility claim. No loop/reload continuity, zero gap, physical speaker listening, new visual approval or blanket network exemption is claimed. '
            'The raw failed report, original approvals, source, input, images, PCM and captured harness remain unchanged.'}


def verify_record(path=REPORT):
    current = analyze(path)
    recorded = read(Path(path).parent / 'public-range-verification.json')
    require({key: value for key, value in current.items() if key != 'analyzedUtc'} ==
            {key: value for key, value in recorded.items() if key != 'analyzedUtc'}, 'Recorded exact public Restoration range proof changed')
    return recorded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record', action='store_true', help='Record this exact qualification once after all checks')
    args = parser.parse_args()
    output = REPORT.parent / 'public-range-verification.json'
    if args.record and not output.exists():
        result = analyze()
        with output.open('x') as stream:
            stream.write(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    else:
        result = verify_record()
    print(json.dumps({'status': result['status'], 'rawStatus': result['rawReport']['status'],
        'qualificationFile': str(output.relative_to(ROOT)), **result['qualifiedReport']}))


if __name__ == '__main__':
    try:
        main()
    except (AssertionError, ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit('Restoration public range proof refused: ' + str(error))
