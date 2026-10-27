#!/usr/bin/env python3
"""Verify only the exact recorded Arcana mobile pre-unlock media cancellations.

This is a separate, hash-bound interpretation of one FAILED raw network gate.
It does not launch a browser, change playback or rewrite any original evidence.
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
REPORT = ROOT / 'Artifacts/Validation/Arcana/Mobile844/FantasyCards/report.json'
PLAYER = ROOT / 'Artifacts/Builds/FantasyCards/Web'
EXPECTED_REPORT_SHA = '76bccabe998063f6ea66a10cf49322da87b0f4a1e5dac8a2829ea0ff4d7a9e38'
BASE_URL = 'http://127.0.0.1:42833/FantasyCards/Web/'
PREFIX = 'StreamingAssets/Audio/ModernCard/'
GATE = 'No unexpected failed requests; exact proved cache/local-media cancellations retained'
EXPECTED_REQUESTS = ((1, 'lobby.ogg', 1793091011615.221), (1, 'battle.ogg', 1793091011623.1594),
                     (1, 'lobby.ogg', 1793091011726.5515))
CAPTURE_SNAPSHOTS = {
    'Tools/Validation/revival-smoke.py': 'Artifacts/Validation/Arcana/HarnessHistory/before-gospel-cdp/revival-smoke.py',
}
SOURCE_HASHES = {
    'Tools/Validation/revival-smoke.py': 'ee1c563028a532d93eafa3eb04195afabb5653c97bd04e97a0271aba59bd7ddf',
    'Tools/Validation/arcana-smoke-plans.json': 'bc7f2e15e39ca6f1ac8cfefe408c2d7e4fe2fd5273a14accb0dc96ab15b44aba',
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


def preunlock(row, url):
    state = row.get('state', {})
    return (state.get('scene') == 'login' and state.get('unlocked') is False
            and state.get('contextState') == 'not-created' and state.get('actualSource') is None
            and state.get('actualPaused') is True and inactive(row, url))


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
    levels, positions = [], []
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
    require(len(levels) == window['sampleCount'] and len(levels) >= 6 and statistics.median(levels) == window['medianRms']
            and sum(value > 1e-5 for value in levels) / len(levels) >= .5 and positions[-1] - positions[0] > .5, 'Raw PCM does not support summary')
    return {'label': label, 'navigation': navigation, **reference(path), 'sourceUrl': url,
            'sourceIdentity': [context_id, source_id], 'sampleCount': len(levels), 'medianRms': statistics.median(levels),
            'positionsSeconds': [positions[0], positions[-1]], 'mediaProgressSeconds': positions[-1] - positions[0],
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
    require(path == REPORT and digest(path) == EXPECTED_REPORT_SHA, 'Only the exact recorded Arcana mobile report is supported')
    raw = read(path)
    require(raw['revision'] == 'fm-arcana-2026-10-27' and raw['game'] == 'FantasyCards'
            and raw['profile'] == 'mobile-landscape' and raw['suite'] == 'modern-real-input-v1'
            and raw['status'] == 'failed' and raw['failure'] == GATE, 'Different current raw failure')
    bad = [check for check in raw['checks'] if check.get('passed') is not True]
    require(len(raw['checks']) == 81 and len(bad) == 1 and bad[0]['name'] == GATE, 'Other raw checks failed')
    require(not raw['pageErrors'] and not raw['httpErrors'] and not any(item['type'] == 'error' for item in raw['console']), 'Other runtime errors exist')
    sources = {name: reference(capture_source(name, sha)) for name, sha in SOURCE_HASHES.items()}
    harness = raw['harness']
    require(harness['sha256'] == SOURCE_HASHES[harness['file']]
            and harness['planSha256'] == SOURCE_HASHES['Tools/Validation/arcana-smoke-plans.json']
            and harness['helpersSha256'] == SOURCE_HASHES['Tools/Validation/web-smoke.py'], 'Capture source fingerprints differ')
    for key in ('pageErrorClassifier', 'localMediaClassifier'):
        require(harness[key]['sha256'] == SOURCE_HASHES[harness[key]['file']], 'Capture classifier differs')
    for record in ({'file': raw['audioEvidence']['observer'], 'sha256': raw['audioEvidence']['observerSha256']}, raw['audioEvidence']['localMediaObserver']):
        require(record['sha256'] == SOURCE_HASHES[record['file']], 'Passive observer differs')
    declared_files = raw['buildFingerprint']['files']
    actual_names = {str(item.relative_to(PLAYER)) for item in PLAYER.rglob('*') if item.is_file()
                    and (item.relative_to(PLAYER).as_posix() == 'index.html' or item.relative_to(PLAYER).parts[0] in ('Build', 'StreamingAssets', 'TemplateData'))
                    and not any('DoNotShip' in part or 'DontShip' in part for part in item.relative_to(PLAYER).parts)}
    require(len(declared_files) == len({row['file'] for row in declared_files})
            and actual_names == {row['file'] for row in declared_files}, 'Current runtime file set differs')
    for row in declared_files:
        current = (PLAYER / row['file']).resolve()
        require(current.is_relative_to(PLAYER) and current.is_file() and current.stat().st_size == row['bytes']
                and digest(current) == row['sha256'], 'Current runtime fingerprint differs: ' + row['file'])
    plan = read(capture_source('Tools/Validation/arcana-smoke-plans.json', harness['planSha256']))['FantasyCards']
    require(plan['revision'] == raw['revision'] and plan['suite'] == raw['suite'], 'Wrong current plan')
    approved = [checkpoint for checkpoint in raw['checkpoints'] if checkpoint['status'] == 'approved']
    require(len(approved) == 17 and {checkpoint['name'].split('-adjusted-')[0] for checkpoint in approved} == set(plan['requiredCheckpoints']), 'Current17 approved originals required')
    for checkpoint in approved:
        shot, review = checkpoint['original'], checkpoint['review']
        image = (ROOT / shot['path']).resolve()
        expected = next(step['expected'] for step in plan['steps'] if step.get('type') == 'checkpoint' and step['name'] == checkpoint['name'].split('-adjusted-')[0])
        require(image.is_relative_to(REPORT.parent) and digest(image) == shot['sha256'] == review['frameSha256']
                and review['status'] == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review['expected'] == checkpoint['expected'] == expected, 'Original explicit approval changed')
    timelines, rows = [], {}
    for item in raw['audioEvidence']['localMediaTimelines']:
        timeline = (REPORT.parent / item['rawEvidence']).resolve()
        require(timeline.is_relative_to(REPORT.parent) and digest(timeline) == item['sha256'], 'Passive timeline changed')
        value = read(timeline)
        require(value['navigation'] == item['navigation'] and all(row['navigation'] == item['navigation'] for row in value['observations']), 'Timeline navigation differs')
        rows[item['navigation']] = sorted(value['observations'], key=lambda row: (row['epochMs'], row['atMs']))
        timelines.append({'navigation': item['navigation'], **reference(timeline)})
    require(set(rows) == {0, 1, 2} and all(rows.values()), 'All three actual navigation timelines required')
    diagnostic = raw['networkDiagnostic']
    requests, cached = diagnostic['unexpectedFailures'], diagnostic['verifiedUnityCacheRevalidationAborts']
    require([(item['navigation'], item['url'].rsplit('/', 1)[-1], item['observedEpochMs']) for item in requests] == list(EXPECTED_REQUESTS), 'Different unclassified requests')
    require(len(cached) == 2 and raw['failedRequests'] == sorted(requests + cached, key=lambda row: row['observedEpochMs']), 'Additional failed requests exist')
    require(not any(diagnostic.get(key) for key in ('verifiedInactiveLocalMediaCancellations', 'verifiedInactiveLocalPreloadCancellations', 'verifiedRecoveredPublicPregestureRanges', 'externalMusicServiceFailures')), 'Different classifier result')
    cache_proofs = []
    for failure in cached:
        nav, url = failure['navigation'], failure['url']
        marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
        require(nav in (1, 2) and failure['method'] == 'GET' and failure['resourceType'] == 'fetch' and failure['error'] == 'net::ERR_ABORTED'
                and any(config['navigation'] == nav and config['dataUrl'] == url for config in raw['unityConfigurations'])
                and any(response['navigation'] == nav and response['url'] == url and response['method'] == 'GET' and response['status'] == 304 for response in raw['networkResponses'])
                and any(item['navigation'] == nav and item['text'] == marker for item in raw['console'])
                and any(item['navigation'] == nav and item['name'] == ('continued-profile' if nav == 1 else 'continued-battle') for item in approved), 'Cached data proof differs')
        cache_proofs.append({'request': failure, 'httpStatus': 304, 'unityCacheMessage': marker})
    decoded = {filename: decode_media(raw, filename) for filename in ('title.ogg', 'lobby.ogg', 'deck.ogg', 'battle.ogg')}
    pcm_proofs = {label: pcm(raw, label, filename, 1) for label, filename in (
        ('lobby-instrumental', 'lobby.ogg'), ('deck-role-instrumental', 'deck.ogg'),
        ('deck-same-role-instrumental', 'deck.ogg'), ('lobby-return-instrumental', 'lobby.ogg'), ('battle-original', 'battle.ogg'))}
    previous, first = rows[0][-1], rows[1][0]
    origin = first['epochMs'] - first['atMs']
    require(0 < origin - previous['epochMs'] < 3000 and first['atMs'] < 1000, 'Cancellation is not the recorded reload boundary')
    proofs = []
    for failure in requests:
        nav, filename, when = failure['navigation'], failure['url'].rsplit('/', 1)[-1], failure['observedEpochMs']
        url = BASE_URL + PREFIX + filename
        require(failure['url'] == url and failure['method'] == 'GET' and failure['resourceType'] == 'media'
                and failure['error'] == 'net::ERR_ABORTED' and 0 <= when - origin < 400, 'Not the exact early local media cancellation')
        require(inactive(previous, url) and preunlock(first, url), 'Media was selected/playing at the observed boundary')
        after = next((row for row in rows[1] if when + 50 <= row['epochMs'] <= when + 1000 and preunlock(row, url)), None)
        require(after and all(preunlock(row, url) for row in rows[1] if first['epochMs'] <= row['epochMs'] <= after['epochMs']), 'No decoded inactive pre-unlock recovery')
        responses = [item for item in raw['networkResponses'] if item['url'] == url and item['method'] == 'GET']
        require(any(item['status'] == 200 and item['navigation'] == 0 for item in responses)
                and sum(item['status'] == 206 and item['navigation'] == nav for item in responses) >= 2, 'Complete original response/recovered local ranges missing')
        later = pcm_proofs['lobby-instrumental' if filename == 'lobby.ogg' else 'battle-original']
        active = next((row for row in rows[1] if row['epochMs'] > after['epochMs'] and playing(row, url) and player(row, url)['currentTime'] > .5), None)
        require(active and later['startedEpochMs'] > active['epochMs'], 'Exact recovered source did not later advance and produce PCM')
        proofs.append({'request': failure, 'category': 'navigation-boundary-inactive-preunlock-media-range',
            'millisecondsAfterNavigationClockOrigin': when - origin, 'previousNavigationInactive': previous,
            'decodedBeforeUnlock': first, 'decodedAfterCancellation': after, 'laterSelectedAndAdvancing': active,
            'runtime': decoded[filename]['runtime'], 'successfulResponses': responses, 'subsequentSameNavigationPcm': later})
    visual = {'status': 'approved', 'reviewer': '; '.join(sorted({item['review']['reviewer'] for item in approved})),
        'reviewedUtc': max(item['review']['reviewedUtc'] for item in approved), 'runtimeFiles': raw['buildFingerprint']['files'],
        'screenshots': [item['original'] for item in approved], 'findings': [item['review']['observed'] for item in approved],
        'method': 'Derived aggregation of the17 existing explicit original-frame approvals; no new visual approval or pixel modification.'}
    return {'status': 'verified-exact-arcana-preunlock-cancellations', 'analyzedUtc': datetime.now(timezone.utc).isoformat(),
        'rawReport': {**reference(path), 'status': 'failed'}, 'analysisSource': reference(Path(__file__)),
        'captureSources': sources, 'localMediaTimelines': timelines, 'decodedMedia': decoded,
        'decoderVersion': subprocess.check_output(['ffmpeg', '-version'], text=True).splitlines()[0],
        'requests': proofs, 'alreadyClassifiedCacheRequests': cache_proofs, 'pcmWindows': pcm_proofs,
        'otherRawChecksPassed': 80, 'visualReview': visual,
        'qualifiedReport': {'rawStatus': 'failed', 'rawChecks': 81, 'passedChecks': 80, 'reviewedOriginals': 17,
                            'recordedPcmWindows': 10, 'adjudicatedMediaCancellations': 3, 'alreadyClassifiedCacheRequests': 2},
        'scope': 'The automatic network gate remains FAILED (80 of81 raw checks passed). Only three exact inactive/pre-unlock media cancellations at the recorded navigation boundary are interpreted. Exact current files fully decode; the same recovered lobby/battle sources later advance and produce isolated audible PCM in navigation1. Request events lack initiating-document IDs, so the failed GETs are not assigned to the old or new document. No uninterrupted samples, loop-boundary continuity, physical listening or zero reload gap is claimed. No raw status, original, input, PCM, runtime source or harness was changed.'}


def verify_record(path=REPORT):
    current = analyze(path)
    recorded = read(Path(path).parent / 'media-adjudication.json')
    require({key: value for key, value in current.items() if key != 'analyzedUtc'} ==
            {key: value for key, value in recorded.items() if key != 'analyzedUtc'}, 'Recorded exact Arcana proof changed')
    return recorded


if __name__ == '__main__':
    try:
        result = analyze()
        (REPORT.parent / 'media-adjudication.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({'status': result['status'], 'rawStatus': 'failed', **result['qualifiedReport']}))
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit('Arcana media proof refused: ' + str(error))
