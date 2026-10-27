#!/usr/bin/env python3
"""Hash-bound analysis of three recorded detail/mobile local-media aborts.

The automatic network gate remains failed. This script only analyzes the exact
recorded loop/reload requests; it does not launch anything or edit raw evidence.
"""
from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import statistics
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Artifacts/Validation/Detail/Mobile844/FantasyCards/report.json'
PLAN = ROOT / 'Artifacts/Validation/Detail/HarnessHistory/detail-smoke-plans-before-gospel-front-angle.json'
PLAYER = ROOT / 'Artifacts/Builds/FantasyCards/Web'
EXPECTED_REPORT_SHA = '59d45de51fecdefa36c115f99b12254f9cf53a3b5b3a515866113e86b63f7816'
BASE_URL = 'http://127.0.0.1:32791/FantasyCards/Web/'
MEDIA_PREFIX = 'StreamingAssets/Audio/ModernCard/'
GATE = 'No unexpected failed requests; exact proved cache/local-media cancellations retained'
EXPECTED_REQUESTS = (
    (1, 'lobby.ogg', 1793071171522.3462),
    (2, 'battle.ogg', 1793071424994.9963),
    (2, 'title.ogg', 1793071424995.4893),
)


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
    require(path.is_relative_to(ROOT) and path.is_file(), 'Missing/outside evidence')
    return {'file': str(path.relative_to(ROOT)), 'sha256': digest(path)}


def number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def file_for(raw, name):
    relative = MEDIA_PREFIX + name
    rows = [item for item in raw['buildFingerprint']['files'] if item['file'] == relative]
    require(len(rows) == 1, 'Exact bundled media fingerprint missing')
    row = rows[0]
    current = PLAYER / relative
    require(current.is_file() and current.stat().st_size == row['bytes'] and digest(current) == row['sha256'],
            'Current media bytes changed: ' + name)
    return row


def player(row, url):
    matches = [item for item in row.get('players', []) if item.get('source') == url]
    require(len(matches) == 1, 'Exact single local element missing')
    return matches[0]


def playing(row, url):
    state = row.get('state', {})
    item = player(row, url)
    return (state.get('sourceKind') == 'bundled-instrumental' and state.get('actualSource') == url
            and state.get('contextState') == 'running' and state.get('unlocked') is True
            and state.get('actualPaused') is False and state.get('muted') is False
            and number(state.get('volume')) and state['volume'] > .1
            and item.get('active') is True and item.get('paused') is False
            and item.get('pending') is False and item.get('readyState', 0) >= 2
            and number(item.get('currentTime')))


def reaches(context, source, destination):
    """Check the recorded live graph, including zero-gain disconnections."""
    gains = {item['nodeId']: item['value'] for item in context.get('gains', [])}
    edges = {}
    for start, end, _output, _input in context.get('connections', []):
        edges.setdefault(start, []).append(end)
    todo, seen = [source], set()
    while todo:
        node = todo.pop()
        if node == destination:
            return True
        if node in seen:
            continue
        seen.add(node)
        if node in gains and (not number(gains[node]) or gains[node] <= 1e-5):
            continue
        todo.extend(edges.get(node, []))
    return False


def pcm(raw, label, url, navigation):
    matches = [w for w in raw['audioEvidence']['windows'] if w['label'] == label and w['navigation'] == navigation]
    require(len(matches) == 1, 'Exact PCM window missing')
    window = matches[0]
    path = REPORT.parent / window['rawEvidence']
    require(digest(path) == window['sha256'], 'PCM evidence changed')
    saved = json.loads(path.read_text())
    require(all(window.get(key) == value for key, value in saved['summary'].items() if key != 'frames'),
            'Recorded PCM summary differs')
    require(window['observerErrors'] == [] and window['nonfiniteSamples'] == 0
            and window['allContextsRunning'] is True and window['isolatedMusicRoute'] is True
            and window['sourceUrls'] == [url] and window['sampleCount'] >= 6
            and window['audibleSampleFraction'] >= .5 and window['medianRms'] > 1e-5
            and window['mediaProgressSeconds'] > .5 and len(window['sourceIdentities']) == 1,
            'PCM source/isolation/progression failed')
    context_id, source_id = window['sourceIdentities'][0]
    levels, positions, durations = [], [], []
    for snapshot in saved['snapshots']:
        require(snapshot['errors'] == [], 'PCM observer errors')
        contexts = [c for c in snapshot['contexts'] if c['id'] == context_id]
        require(len(contexts) == 1, 'Recorded music context missing')
        context = contexts[0]
        sources = [m for m in context['media'] if m['nodeId'] == source_id and m['currentSrc'] == url]
        require(context['state'] == 'running' and len(sources) == 1, 'Music graph/source changed')
        source = sources[0]
        require(source['paused'] is False and source['ended'] is False and source['loop'] is True
                and source['muted'] is False and source['volume'] > .1 and source['readyState'] >= 2,
                'PCM media element is not playing')
        audible = [m for m in context['media'] if not m['paused'] and not m['muted']
                   and reaches(context, m['nodeId'], context['destinationNodeId'])]
        require([m['nodeId'] for m in audible] == [source_id], 'Other music source shares PCM route')
        require(context['destinationConnections'] == 1 and len(context['destinationRoutes']) == 1,
                'Music destination is not isolated')
        route = context['destinationRoutes'][0]
        require(reaches(context, source_id, route['sourceNodeId']), 'PCM analyser is not on actual selected route')
        channels = route['channels']
        require(channels and all(c['nonfinite'] == 0 and c['samples'] > 0 and number(c['rms']) for c in channels),
                'Invalid raw PCM samples')
        levels.append(math.sqrt(sum(c['rms'] ** 2 for c in channels) / len(channels)))
        positions.append(source['currentTime']); durations.append(source['duration'])
    require(len(levels) == window['sampleCount'] and len(levels) >= 6
            and statistics.median(levels) > 1e-5 and sum(x > 1e-5 for x in levels) / len(levels) >= .5
            and positions[-1] - positions[0] > .5, 'Raw PCM/progression does not support summary')
    return {'label': label, 'navigation': navigation, **reference(path),
            'sourceUrl': url, 'sourceIdentity': [context_id, source_id], 'sampleCount': len(levels),
            'medianRms': statistics.median(levels), 'mediaProgressSeconds': positions[-1] - positions[0],
            'sourceDurationSeconds': durations[-1],
            'startedEpochMs': window['startedEpochMs'], 'finishedEpochMs': window['finishedEpochMs']}


def analyze(path=REPORT):
    path = Path(path).resolve()
    require(path == REPORT and digest(path) == EXPECTED_REPORT_SHA, 'Only the exact recorded detail mobile run is supported')
    raw = json.loads(path.read_text())
    bad = [c for c in raw['checks'] if c.get('passed') is not True]
    require(raw['game'] == 'FantasyCards' and raw['revision'] == 'fm-detail-2026-10-27'
            and raw['suite'] == 'modern-real-input-v1' and raw['profile'] == 'mobile-landscape'
            and raw['status'] == 'failed' and raw['failure'] == GATE and len(bad) == 1 and bad[0]['name'] == GATE,
            'Different raw failure')
    require(len(raw['checks']) == 50 and sum(c.get('passed') is True for c in raw['checks']) == 49,
            'Different raw check results')
    require(not raw['pageErrors'] and not raw['httpErrors'] and not any(c['type'] == 'error' for c in raw['console']),
            'Other runtime errors are not supported')
    require(digest(PLAN) == raw['harness']['planSha256'], 'Original mobile authored plan changed')
    plan = json.loads(PLAN.read_text())['FantasyCards']
    approved = [c for c in raw['checkpoints'] if c['status'] == 'approved']
    require(len(approved) == 9 and {c['name'].split('-adjusted-')[0] for c in approved} == set(plan['requiredCheckpoints']),
            'Nine directly reviewed interactions are required')
    for checkpoint in approved:
        shot, review = checkpoint['original'], checkpoint['review']
        image = (ROOT / shot['path']).resolve()
        require(image.is_relative_to(path.parent) and digest(image) == shot['sha256'] == review['frameSha256']
                and review['status'] == 'approved' and review.get('reviewer') and review.get('reviewedUtc')
                and review.get('observed') and review['expected'] == checkpoint['expected'], 'Original review changed')
    rows, timelines = [], []
    for ref in raw['audioEvidence']['localMediaTimelines']:
        source = path.parent / ref['rawEvidence']
        require(digest(source) == ref['sha256'], 'Media timeline changed')
        data = json.loads(source.read_text())
        require(data['navigation'] == ref['navigation'] and all(r['navigation'] == ref['navigation'] for r in data['observations']),
                'Timeline navigation differs')
        rows.extend(data['observations']); timelines.append({'navigation': ref['navigation'], **reference(source)})
    rows.sort(key=lambda row: row['epochMs'])
    requests = raw['networkDiagnostic']['unexpectedFailures']
    require([(f['navigation'], f['url'].rsplit('/', 1)[-1], f['observedEpochMs']) for f in requests] == list(EXPECTED_REQUESTS),
            'Different unclassified requests')
    cached = raw['networkDiagnostic']['verifiedUnityCacheRevalidationAborts']
    require(len(cached) == 2 and len(raw['failedRequests']) == 5
            and raw['failedRequests'] == sorted(requests + cached, key=lambda f: f['observedEpochMs']), 'Other failed requests exist')
    cache_proofs = []
    for failure in cached:
        nav, url = failure['navigation'], failure['url']
        require(nav in (1, 2) and failure['method'] == 'GET' and failure['resourceType'] == 'fetch'
                and failure['error'] == 'net::ERR_ABORTED'
                and any(c['navigation'] == nav and c.get('dataUrl') == url for c in raw['unityConfigurations']), 'Different cached data failure')
        require(any(r['navigation'] == nav and r['url'] == url and r['method'] == 'GET' and r['status'] == 304
                    for r in raw['networkResponses']), 'Missing exact cached HTTP304')
        marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
        require(any(m['navigation'] == nav and m['text'] == marker for m in raw['console']), 'Missing Unity successful cache marker')
        restored = next((c for c in approved if c['navigation'] == nav
                         and c['name'] == ('continued-profile' if nav == 1 else 'continued-battle')), None)
        require(restored is not None, 'Reloaded state was not directly reviewed')
        relative = 'Build/' + url.rsplit('/', 1)[-1]
        file = next((f for f in raw['buildFingerprint']['files'] if f['file'] == relative), None)
        require(file and (PLAYER / relative).stat().st_size == file['bytes'] and digest(PLAYER / relative) == file['sha256'],
                'Cached data runtime bytes changed')
        cache_proofs.append({'request': failure, 'httpStatus': 304, 'unityCacheMessage': marker,
                             'runtime': file, 'restoredOriginal': restored['original']})
    proofs = []
    for failure in requests:
        nav, url, when = failure['navigation'], failure['url'], failure['observedEpochMs']
        name = url.rsplit('/', 1)[-1]
        parsed = urlsplit(url)
        require(url == BASE_URL + MEDIA_PREFIX + name and parsed.scheme == 'http' and parsed.hostname == '127.0.0.1'
                and failure['method'] == 'GET' and failure['resourceType'] == 'media' and failure['error'] == 'net::ERR_ABORTED',
                'Not an exact local bundled media GET abort')
        runtime = file_for(raw, name)
        responses = [r for r in raw['networkResponses'] if r['url'] == url and r['method'] == 'GET']
        require(any(r['status'] == 200 and r['navigation'] == 0 for r in responses)
                and sum(r['status'] == 206 and r['navigation'] == nav for r in responses) >= 2,
                'Original complete response and recovered media ranges missing')
        history = [r for r in rows if r['navigation'] == nav]
        proof = {'request': failure, 'runtime': runtime, 'successfulResponses': responses}
        if nav == 1:
            before = [r for r in history if when - 1000 <= r['epochMs'] < when - 10 and playing(r, url)]
            restarted = [r for r in history if abs(r['epochMs'] - when) < 100 and playing(r, url)
                         and r['reason'] == 'media-playing:fm-local-audio-lobby' and player(r, url)['currentTime'] <= .1]
            after = [r for r in history if when + 1000 <= r['epochMs'] <= when + 4000 and playing(r, url)]
            require(before and len(restarted) == 1 and after, 'No exact same-source loop restart/progression')
            prior, restart, resumed = before[-1], restarted[0], after[-1]
            earlier_pcm = pcm(raw, 'lobby-original', url, 1)
            duration = earlier_pcm['sourceDurationSeconds']
            require(earlier_pcm['finishedEpochMs'] < when and duration - .5 <= player(prior, url)['currentTime'] <= duration + 1
                    and player(resumed, url)['currentTime'] - player(restart, url)['currentTime'] > 2,
                    'The playhead did not restart near its enabled loop boundary')
            require(all(playing(r, url) for r in history if prior['epochMs'] <= r['epochMs'] <= resumed['epochMs']),
                    'Playback/context/mute/source changed across the loop')
            proof.update(category='same-source-enabled-loop-restart', before=prior, restart=restart, resumed=resumed,
                         earlierSameNavigationPcm=earlier_pcm,
                         scope='Earlier isolated PCM verifies this source/graph; the abort coincides with a browser playing event and an enabled-loop playhead restart, followed by decoded same-source progression. No PCM window was sampled after this exact loop boundary; no claim of uninterrupted sample-by-sample sound is made.')
        else:
            first = history[0]; first_player = player(first, url); state = first['state']
            require(0 <= first['epochMs'] - when <= 1000 and state['unlocked'] is False
                    and state['contextState'] == 'not-created' and state['actualSource'] is None and state['actualPaused'] is True
                    and first_player['active'] is False and first_player['paused'] is True and first_player['pending'] is False
                    and first_player['currentTime'] == 0 and first_player['readyState'] >= 2,
                    'Reload-boundary abort did not recover into an already decoded paused element before unlock')
            continued = pcm(raw, 'continued-original', BASE_URL + MEDIA_PREFIX + 'battle.ogg', 2)
            require(continued['startedEpochMs'] > first['epochMs'], 'No subsequent selected battle PCM')
            proof.update(category='reload-boundary-decoded-local-range', decodedBeforeUnlock=first,
                         subsequentBattlePcm=continued)
            if name == 'battle.ogg':
                resumed = [r for r in history if r['epochMs'] > first['epochMs'] and playing(r, url)
                           and player(r, url)['currentTime'] > 1]
                require(resumed, 'Reloaded battle source did not advance')
                proof.update(resumed=resumed[-1], scope='The exact reloaded battle element was decoded before unlock and later selected, advanced and produced isolated audible PCM in the same navigation.')
            else:
                brief = [r for r in history if r['epochMs'] > first['epochMs'] and playing(r, url)
                         and player(r, url)['currentTime'] > .1]
                retired = [r for r in history if r['epochMs'] > first['epochMs']
                           and r['state']['actualSource'] == BASE_URL + MEDIA_PREFIX + 'battle.ogg'
                           and player(r, url)['paused'] is True and player(r, url)['active'] is False
                           and player(r, url)['pending'] is False and player(r, url)['readyState'] >= 2
                           and player(r, url)['currentTime'] > .1]
                require(brief and retired and retired[-1]['epochMs'] > brief[0]['epochMs'], 'Decoded title did not briefly progress then retire normally')
                proof.update(briefTitleProgress=brief[-1], retiredTitle=retired[-1],
                             earlierTitlePcm=pcm(raw, 'title-original', url, 0),
                             scope='The title was decoded before unlock, then briefly advanced and retired when battle became selected. Title PCM was verified in navigation0; no isolated title PCM window was sampled after this reload. The selected battle route has subsequent isolated audible PCM.')
        proofs.append(proof)
    return {'status': 'passed-recorded-local-media-recovery', 'analyzedUtc': datetime.now(timezone.utc).isoformat(),
            'rawReport': {**reference(path), 'status': raw['status']}, 'originalPlan': reference(PLAN),
            'analysisSource': reference(Path(__file__)), 'localMediaTimelines': timelines,
            'requests': proofs, 'alreadyClassifiedCacheRequests': cache_proofs, 'otherRawChecksPassed': 49,
            'visualReview': {'status': 'approved', 'reviewer': '; '.join(sorted({c['review']['reviewer'] for c in approved})),
                'reviewedUtc': max(c['review']['reviewedUtc'] for c in approved),
                'runtimeFiles': raw['buildFingerprint']['files'], 'screenshots': [c['original'] for c in approved],
                'findings': [c['review']['observed'] for c in approved]},
            'scope': 'The automatic network gate remains FAILED. This separate analysis is restricted to the three exact recorded local loop/reload GET aborts and verifies current file hashes, archived plan, timelines, decoded playhead progression and precisely identified PCM windows. Loop continuity and post-reload title audibility are not claimed beyond the recorded evidence. No raw report, pixel, input, PCM, runtime source or pass status was changed.'}


def verify_record(path=REPORT):
    current = analyze(path)
    recorded = json.loads((Path(path).parent / 'media-adjudication.json').read_text())
    for key in ('rawReport', 'originalPlan', 'analysisSource', 'localMediaTimelines', 'requests',
                'alreadyClassifiedCacheRequests', 'otherRawChecksPassed', 'visualReview', 'scope'):
        require(current[key] == recorded[key], 'Recorded mobile media analysis changed: ' + key)
    return recorded


if __name__ == '__main__':
    result = analyze()
    (REPORT.parent / 'media-adjudication.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': result['status'], 'rawStatus': result['rawReport']['status'],
                      'requests': len(result['requests']), 'otherRawChecksPassed': result['otherRawChecksPassed']}))
