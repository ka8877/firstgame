#!/usr/bin/env python3
"""Exact current Arcana public media cancellation proof; raw FAILED status is retained.

No browser, playback, HTTP request, original frame or raw evidence is modified.
Startup recovery, observed active progress and unused preloads are distinguished.
"""
from __future__ import annotations
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Artifacts/Validation/Arcana/PublicMobile844/FantasyCards/report.json'
PORTAL = ROOT / 'Artifacts/Validation/Arcana/PublicPortal/report.json'
PLAYER = ROOT / 'Artifacts/Builds/FantasyCards/Web'
EXPECTED_REPORT_SHA = '72a94115cc5d8a917d067c806a22f3590c35ca1287309c01d9700e996f913bd9'
EXPECTED_PORTAL_SHA = '76aa8936c5c6c8fbc91ed282990807196ccb099bfd14df1b95d4ed7237d4d9f6'
EXPECTED_SOURCE_COMMIT = 'aea33845c209c15deeac22474c6626b3efcb8486'
BASE_URL = 'https://ka8877.github.io/firstgame/games/fantasy-cards/'
PREFIX = 'StreamingAssets/Audio/ModernCard/'
GATE = 'No unexpected failed requests; exact proved cache/local-media cancellations retained'
EXPECTED_REQUESTS = (('title.ogg', 1793098122372.253), ('lobby.ogg', 1793098122375.9612),
                     ('battle.ogg', 1793098122401.165), ('title.ogg', 1793098122519.7605),
                     ('battle.ogg', 1793098122538.7166))
CAPTURE_SNAPSHOTS = {}
SOURCE_HASHES = {
    'Tools/Validation/revival-smoke.py': 'd33d2b01eecea95a8c971707da4223c45cdfb997cb44f770ff6465ba329f8f20',
    'Tools/Validation/arcana-public-plans.json': '1905afd12581184bf26fd50313fbd1ec1be8faff28bf1426d0b884f0d0dfd4c2',
    'Tools/Validation/web-smoke.py': '389434c604ce7661405383fb3e2919c410a22ce3d1bc9ce03a7a19c52d64ffc6',
    'Tools/Validation/fm-provider-errors.py': 'b2bc8a667a8fb2763eeb8caff28997ff18a3e0b1e04a014e182b7a57adc31b62',
    'Tools/Validation/fm-local-media-errors.py': '9c55866bbdd5be9e5cf555794f412386868b9dfff0971f3dd072b6ac7393b849',
    'Tools/Validation/fm-local-media-observer.js': 'a419e0958837951ff684afbd959d134d3a48cb7ad90537a25594b61c0212c82f',
    'Tools/Validation/webaudio-observer.js': 'b6ac9834dd3ad5557453aa7bfd6a0920038dbf4659396a1505bc9e1cc1ddaaba',
}


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
    require(path.is_relative_to(ROOT) and path.is_file(), 'Missing/outside evidence: ' + str(path))
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path)}


def capture_source(name, sha):
    if name in CAPTURE_SNAPSHOTS:
        snapshot = ROOT / CAPTURE_SNAPSHOTS[name]
        require(snapshot.is_file() and digest(snapshot) == sha, 'Explicit original Arcana harness snapshot changed')
        return snapshot
    path = ROOT / name
    if path.is_file() and digest(path) == sha: return path
    for parent in (REPORT.parents[2] / 'HarnessHistory', REPORT.parent / 'HarnessHistory'):
        if parent.exists():
            for candidate in parent.rglob('*'):
                if candidate.is_file() and candidate.suffix == path.suffix and digest(candidate) == sha: return candidate
    raise ValueError('Exact Arcana capture source/snapshot changed: ' + name)


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
    require(path == REPORT and digest(path) == EXPECTED_REPORT_SHA, 'Only this exact Arcana public raw report is supported')
    raw = read(path)
    require(raw['revision'] == 'fm-arcana-2026-10-27' and raw['game'] == 'FantasyCards'
            and raw['suite'] == 'modern-real-input-v1' and raw['profile'] == 'mobile-landscape'
            and raw['status'] == 'failed' and raw['failure'] == GATE, 'Different public raw failure')
    bad = [check for check in raw['checks'] if check.get('passed') is not True]
    require(len(raw['checks']) == 34 and len(bad) == 1 and bad[0]['name'] == GATE, 'Other current public checks failed')
    require(not raw['pageErrors'] and not raw['httpErrors'] and not any(item['type'] == 'error' for item in raw['console']), 'Other public runtime errors')
    sources = {name: reference(capture_source(name, sha)) for name, sha in SOURCE_HASHES.items()}
    harness = raw['harness']
    require(harness['sha256'] == SOURCE_HASHES[harness['file']]
            and harness['planSha256'] == SOURCE_HASHES['Tools/Validation/arcana-public-plans.json']
            and harness['helpersSha256'] == SOURCE_HASHES['Tools/Validation/web-smoke.py'], 'Current capture sources differ')
    for key in ('pageErrorClassifier', 'localMediaClassifier'):
        require(harness[key]['sha256'] == SOURCE_HASHES[harness[key]['file']], 'Capture classifier changed')
    for record in ({'file': raw['audioEvidence']['observer'], 'sha256': raw['audioEvidence']['observerSha256']}, raw['audioEvidence']['localMediaObserver']):
        require(record['sha256'] == SOURCE_HASHES[record['file']], 'Passive observer changed')
    plan = read(capture_source('Tools/Validation/arcana-public-plans.json', harness['planSha256']))['FantasyCards']
    require(plan['revision'] == raw['revision'] and plan['suite'] == raw['suite'] and set(plan['requiredCheckpoints']) == {'title', 'lobby'}, 'Wrong public plan')
    approved = [item for item in raw['checkpoints'] if item['status'] == 'approved']
    require(len(approved) == 2 and {item['name'] for item in approved} == {'title', 'lobby'}, 'Exactly two existing approved originals required')
    for item in approved:
        shot, review = item['original'], item['review']; image = (ROOT / shot['path']).resolve()
        expected = next(step['expected'] for step in plan['steps'] if step.get('type') == 'checkpoint' and step['name'] == item['name'])
        require(image.is_relative_to(REPORT.parent) and digest(image) == shot['sha256'] == review['frameSha256']
                and review['status'] == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review['expected'] == item['expected'] == expected, 'Original existing approval changed')
    require(digest(PORTAL) == EXPECTED_PORTAL_SHA, 'Exact public full-byte portal report changed')
    portal, stage = read(PORTAL), ROOT / 'Artifacts/WebPlaytest'
    manifest = read(stage / 'build-manifest.json')
    require(portal['status'] == 'passed' and portal['visualReview']['status'] == 'approved'
            and portal['sourceCommit'] == manifest['sourceCommit'] == EXPECTED_SOURCE_COMMIT
            and manifest['releaseProfile'] == 'arcana' and manifest['sourceDirty'] is False
            and manifest['gameRevisions'] == {'FantasyCards': 'fm-arcana-2026-10-27', 'PathOfDawn': 'gospel-authored-2026-10-27'}
            and portal['manifest']['sha256'] == digest(stage / 'build-manifest.json'), 'Current public manifest/source proof differs')
    expected_public = {**manifest['files'], 'build-manifest.json': digest(stage / 'build-manifest.json')}
    require(len(portal['files']) == 41 and {item['file'] for item in portal['files']} == set(expected_public), 'Complete current public file set required')
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
    diagnostic = raw['networkDiagnostic']; classified = diagnostic['verifiedInactiveLocalPreloadCancellations']
    require(diagnostic['unexpectedFailures'] == requests[:4] and len(classified) == 1 and classified[0]['request'] == requests[4]
            and not any(diagnostic.get(key) for key in ('verifiedUnityCacheRevalidationAborts', 'verifiedInactiveLocalMediaCancellations', 'verifiedRecoveredPublicPregestureRanges', 'externalMusicServiceFailures')), 'Other classifier results or failures')
    proofs = []
    for index, failure in enumerate(requests):
        filename, when = failure['url'].rsplit('/', 1)[-1], failure['observedEpochMs']; url = BASE_URL + PREFIX + filename
        require(failure['url'] == url and failure['navigation'] == 0 and failure['method'] == 'GET'
                and failure['resourceType'] == 'media' and failure['error'] == 'net::ERR_ABORTED'
                and 0 <= when - origin < 600, 'Not an exact early public OGG range cancellation')
        responses = [item for item in raw['networkResponses'] if item['navigation'] == 0 and item['url'] == url and item['method'] == 'GET' and item['status'] == 206]
        require(len(responses) >= 2, 'Repeated public HTTP206 observations missing')
        proof = {'request': failure, 'runtime': decoded[filename]['runtime'], 'http206Responses': len(responses),
                 'millisecondsAfterNavigationClockOrigin': when - origin}
        if index < 3:
            require(0 < first['epochMs'] - when < 100 and first['state']['actualSource'] is None
                    and first['state']['actualPaused'] is True and player(first, url)['active'] is False
                    and player(first, url)['currentTime'] == 0
                    and (player(first, url)['paused'] is True or player(first, url)['pending'] is True), 'Startup observation does not support this cancellation')
            loaded = next((row for row in rows if when <= row['epochMs'] <= when + 1000 and player(row, url)['readyState'] >= 2), None)
            later = next((row for row in rows if row['epochMs'] > loaded['epochMs'] and player(row, url)['readyState'] >= 4), None) if loaded else None
            require(loaded and later, 'Cancelled startup source did not become browser-decoded')
            proof.update(category='early-startup-followed-by-observed-decode', firstObservation=first, decoded=loaded, subsequentlyReady=later)
            if filename == 'title.ogg':
                require(title_pcm['startedEpochMs'] > later['epochMs'], 'No subsequent same-title PCM')
                proof.update(subsequentSameSourcePcm=title_pcm)
            elif filename == 'lobby.ogg':
                require(lobby_pcm['startedEpochMs'] > later['epochMs'], 'No subsequent same-lobby PCM')
                proof.update(subsequentSameSourcePcm=lobby_pcm)
            else:
                require(inactive(later, url), 'Unused battle source did not remain decoded and inactive')
                proof.update(subsequentSelectedTitlePcm=title_pcm,
                    limitation='Battle browser decode/paused preload and complete-file decode are verified; battle was not selected or audibly sampled in this public run.')
        else:
            before = [row for row in rows if when - 500 <= row['epochMs'] <= when]
            after = [row for row in rows if when < row['epochMs'] <= when + 500]
            require(before and after, 'Cancellation bracketing samples missing')
            prior, following = before[-1], after[0]
            title_url = BASE_URL + PREFIX + 'title.ogg'
            require(playing(prior, title_url) and playing(following, title_url)
                    and 0 < player(prior, title_url)['currentTime'] < player(following, title_url)['currentTime']
                    and title_pcm['startedEpochMs'] > following['epochMs'], 'Observed selected title did not advance and later produce PCM')
            if filename == 'title.ogg':
                proof.update(category='selected-title-advances-in-bracketing-samples', before=prior, after=following, subsequentSameSourcePcm=title_pcm)
            else:
                require(inactive(prior, url) and inactive(following, url), 'Classified battle preload was active')
                require(classified[0]['beforeEpochMs'] == prior['epochMs'] and classified[0]['afterEpochMs'] == following['epochMs'], 'Existing strict preload proof differs')
                proof.update(category='already-classified-inactive-preload', before=prior, after=following,
                             originalClassification=classified[0], subsequentSelectedTitlePcm=title_pcm)
        proofs.append(proof)
    visual = {'status': 'approved', 'reviewer': '; '.join(sorted({item['review']['reviewer'] for item in approved})),
        'reviewedUtc': max(item['review']['reviewedUtc'] for item in approved), 'runtimeFiles': fingerprint,
        'screenshots': [item['original'] for item in approved], 'findings': [item['review']['observed'] for item in approved],
        'method': 'Derived aggregation of the two existing explicit original-frame approvals; no new approval or pixel edit.'}
    return {'status': 'verified-exact-arcana-public-media-recovery', 'analyzedUtc': datetime.now(timezone.utc).isoformat(),
        'rawReport': {**reference(path), 'status': 'failed'}, 'analysisSource': reference(Path(__file__)), 'captureSources': sources,
        'publicPortal': reference(PORTAL), 'publicManifest': portal['manifest'], 'publicSourceCommit': EXPECTED_SOURCE_COMMIT,
        'publicDelivery': delivery, 'decodedMedia': decoded, 'decoderVersion': subprocess.check_output(['ffmpeg', '-version'], text=True).splitlines()[0],
        'localMediaTimeline': reference(timeline), 'requests': proofs, 'pcmWindows': {'title': title_pcm, 'lobby': lobby_pcm},
        'otherRawChecksPassed': 33, 'visualReview': visual,
        'qualifiedReport': {'rawStatus': 'failed', 'rawChecks': 34, 'passedChecks': 33, 'reviewedOriginals': 2,
                            'recordedPcmWindows': 2, 'exactMediaCancellations': 5, 'newlyQualifiedMediaCancellations': 4, 'alreadyClassifiedInactivePreloads': 1},
        'scope': 'The automatic public network gate remains FAILED (33 of34 raw checks passed). This proof is restricted to five exact same-origin OGG GET cancellations in the first600ms of navigation0, four unclassified and one already-classified inactive preload. Three precede the first passive observation, so their pre-cancellation state and pre-gesture timing are not claimed; subsequent browser decode is recorded. Selected title samples advance across a later cancellation; uninterrupted sample playback is not measured. Later title/lobby isolated audible PCM and full HTTPS/current-file bytes plus full4-OGG decode are verified. Battle remains an unused browser-decoded preload, with no public battle audibility claim. No loop/reload continuity, zero gap, physical listening, new visual approval or blanket network exemption is claimed. Raw report, source, input, originals, PCM and harness are unchanged.'}


def verify_record(path=REPORT):
    current = analyze(path)
    recorded = read(Path(path).parent / 'public-media-adjudication.json')
    require({key: value for key, value in current.items() if key != 'analyzedUtc'} ==
            {key: value for key, value in recorded.items() if key != 'analyzedUtc'}, 'Recorded exact public Arcana proof changed')
    return recorded


if __name__ == '__main__':
    try:
        result = analyze()
        (REPORT.parent / 'public-media-adjudication.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({'status': result['status'], **result['qualifiedReport']}))
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit('Arcana public media proof refused: ' + str(error))
