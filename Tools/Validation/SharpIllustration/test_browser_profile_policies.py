"""Synthetic browser-policy regressions; Root executes under the owned wrapper.

These fixtures exercise the actual journey methods without launching a browser
or requesting public files. They do not replace browser PCM or a public audit.
"""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
from types import ModuleType, SimpleNamespace
import unittest
from unittest import mock


VALIDATION = Path(__file__).resolve().parents[1]
if str(VALIDATION) not in sys.path:
    sys.path.insert(0, str(VALIDATION))
SPEC = importlib.util.spec_from_file_location(
    'sharp_browser_profile_policies_smoke', VALIDATION / 'nativity-web-smoke.py')
SMOKE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SMOKE)

GAME_URL = 'https://example.invalid/firstgame/games/fantasy-cards/'
MEDIA_URL = GAME_URL + 'StreamingAssets/Audio/ModernCard/title.ogg'
TITLE_SECONDS = 17.882426
SHARP_REVISION = SMOKE.SHARP_ILLUSTRATION_REVISIONS['FantasyCards']


def sha256(body):
    return hashlib.sha256(body).hexdigest()


def snapshots(count=6, start=0):
    """Mirror the passive observer's media -> gain -> master -> destination."""
    result = []
    for index in range(start, start + count):
        elapsed = 2 * index / 5
        channel = {'samples': 2048, 'rms': .025, 'peak': .08, 'nonfinite': 0}
        media = {'nodeId': 3, 'currentSrc': MEDIA_URL, 'currentTime': .4 + elapsed,
                 'duration': TITLE_SECONDS, 'paused': False, 'ended': False,
                 'loop': True, 'muted': False, 'volume': 1, 'readyState': 4}
        context = {'id': 1, 'state': 'running', 'currentTime': 5 + elapsed,
                   'sampleRate': 44100, 'destinationChannels': 2,
                   'destinationNodeId': 2, 'destinationConnections': 1,
                   'connections': [[3, 4, 0, 0], [4, 1, 0, 0], [1, 2, 0, 0]],
                   'destinationRoutes': [{'sourceNodeId': 1, 'output': 0,
                                          'channels': [channel, copy.deepcopy(channel)]}],
                   'buffers': [], 'media': [media],
                   'gains': [{'nodeId': 4, 'value': 1}, {'nodeId': 1, 'value': .6}]}
        result.append({'wallTimeMs': 1000 + elapsed * 1000,
                       'errors': [], 'contexts': [context]})
    return result


def role_playback():
    return {'state': {'sourceKind': 'bundled-instrumental', 'actualSource': MEDIA_URL,
                      'actualPaused': False, 'contextState': 'running',
                      'unlocked': True, 'muted': False, 'volume': .6},
            'players': [{'source': MEDIA_URL, 'currentTime': 2.4, 'duration': TITLE_SECONDS,
                         'paused': False, 'pending': False, 'readyState': 4, 'active': True}],
            'transitions': [{'scene': 'login', 'source': MEDIA_URL, 'atMs': 500}],
            'observedAtMs': 3100}


class FakePage:
    def __init__(self, batches):
        self.batches = copy.deepcopy(batches)
        self.windows = []
        self.diagnostic_calls = 0
        self.playback = role_playback()

    def evaluate(self, expression, milliseconds=None):
        if 'window.__firstGameAudioEvidence.sampleWindow' in expression:
            self.windows.append(milliseconds)
            return self.batches.pop(0) if self.batches else []
        if all(name in expression for name in
               ('FirstGameLocalMusic.getState()', 'FirstGameLocalMusic.getDiagnostics()',
                'FirstGameLocalMusic.getTransitions()')):
            self.diagnostic_calls += 1
            return copy.deepcopy(self.playback)
        raise AssertionError('Unexpected page evaluation: ' + expression)


class FakeResponse(io.BytesIO):
    def __init__(self, body, url):
        super().__init__(body)
        self.status = 200
        self.headers = {'Content-Encoding': 'identity'}
        self.url = url


class BrowserProfilePolicyTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='sharp-browser-policy-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def journey(self, batches=None, revision=SHARP_REVISION):
        journey = SMOKE.JourneySmoke.__new__(SMOKE.JourneySmoke)
        journey.args = SimpleNamespace(game='FantasyCards', timeout=3)
        journey.modern = True
        journey.navigation = 0
        journey.output = self.root / ('journey-' + str(len(list(self.root.iterdir()))))
        journey.output.mkdir()
        journey.page = FakePage(batches if batches is not None else [snapshots()])
        journey.report = {'revision': revision, 'checks': [], 'audioEvidence': {'windows': []}}
        return journey

    def raw_audio(self, journey):
        evidence = journey.report['audioEvidence']['windows'][-1]
        raw_path = journey.output / evidence['rawEvidence']
        self.assertEqual(evidence['sha256'], sha256(raw_path.read_bytes()))
        return evidence, json.loads(raw_path.read_text())

    def test_sharp_publisher_excerpt_passes_and_retains_raw_pcm_and_role_diagnostics(self):
        journey = self.journey()
        journey.audio('title-started')

        evidence, raw = self.raw_audio(journey)
        self.assertEqual(journey.page.windows, [2200])
        self.assertEqual(raw['snapshots'], snapshots())
        self.assertEqual(raw['acquisitionWindowsMs'], [2200])
        self.assertEqual(raw['requestedWindowMs'], 2200)
        self.assertEqual(evidence['sampleCount'], 6)
        self.assertEqual(evidence['sourceIdentities'], [[1, 3]])
        self.assertEqual(evidence['sourceUrls'], [MEDIA_URL])
        self.assertAlmostEqual(evidence['mediaProgressSeconds'], 2)
        self.assertTrue(evidence['allContextsRunning'])
        self.assertTrue(evidence['isolatedMusicRoute'])
        self.assertEqual(evidence['nonfiniteSamples'], 0)
        self.assertEqual(evidence['rolePlayback'], journey.page.playback)
        self.assertEqual(journey.page.diagnostic_calls, 1)
        self.assertEqual(len(journey.report['checks']), 3)
        self.assertTrue(all(check['passed'] for check in journey.report['checks']))
        persisted = json.loads((journey.output / 'report.json').read_text())
        self.assertEqual(persisted['audioEvidence']['windows'][0], evidence)

    def test_sharp_waits_for_six_real_samples_and_keeps_both_acquisition_batches(self):
        first, later = snapshots(4), snapshots(2, start=4)
        journey = self.journey([first, later])
        journey.audio('title-started')

        evidence, raw = self.raw_audio(journey)
        self.assertEqual(journey.page.windows, [2200, 4000])
        self.assertEqual(raw['acquisitionWindowsMs'], [2200, 4000])
        self.assertEqual(raw['requestedWindowMs'], 6200)
        self.assertEqual(raw['snapshots'], first + later)
        self.assertEqual(len(raw['summary']['frames']), 6)
        self.assertEqual(evidence['sampleCount'], 6)

    def test_unlisted_legacy_modern_revision_keeps_the_thirty_second_filter(self):
        journey = self.journey(revision='fm-modern-2026-10-26')
        with self.assertRaisesRegex(AssertionError, 'Actual looping BGM is audible'):
            journey.audio('title-started')

        evidence, raw = self.raw_audio(journey)
        self.assertEqual(evidence['sampleCount'], 0)
        self.assertEqual(raw['snapshots'], snapshots())
        self.assertEqual(journey.page.windows, [2200])
        self.assertNotIn('rolePlayback', evidence)
        self.assertEqual(journey.page.diagnostic_calls, 0)
        self.assertFalse(journey.report['checks'][-1]['passed'])

    def test_sharp_does_not_suppress_invalid_or_insufficient_audio_evidence(self):
        for fault in ('silence', 'disconnected', 'nonloop', 'paused', 'suspended',
                      'shared-buffer', 'nonfinite', 'insufficient-samples'):
            with self.subTest(fault=fault):
                frames = snapshots(5 if fault == 'insufficient-samples' else 6)
                for frame in frames:
                    context = frame['contexts'][0]
                    if fault == 'silence':
                        for channel in context['destinationRoutes'][0]['channels']:
                            channel['rms'] = channel['peak'] = 0
                    elif fault == 'disconnected':
                        context['connections'] = context['connections'][1:]
                    elif fault == 'nonloop':
                        context['media'][0]['loop'] = False
                    elif fault == 'paused':
                        context['media'][0]['paused'] = True
                    elif fault == 'suspended':
                        context['state'] = 'suspended'
                    elif fault == 'shared-buffer':
                        context['connections'].append([5, 1, 0, 0])
                        context['buffers'].append({'nodeId': 5, 'loop': True,
                            'startedAtContextTime': 0, 'endedAtContextTime': None,
                            'stoppedAtContextTime': None, 'startArguments': [0]})
                    elif fault == 'nonfinite':
                        context['destinationRoutes'][0]['channels'][0]['nonfinite'] = 1
                journey = self.journey([frames])
                with self.assertRaises(AssertionError):
                    journey.audio('title-started')
                evidence, raw = self.raw_audio(journey)
                self.assertEqual(raw['snapshots'], frames)
                self.assertFalse(journey.report['checks'][-1]['passed'])
                self.assertEqual(evidence['rolePlayback'], journey.page.playback)
                if fault == 'insufficient-samples':
                    self.assertEqual(evidence['sampleCount'], 5)
                    self.assertGreater(len(journey.page.windows), 1)
                    self.assertEqual(raw['acquisitionWindowsMs'], journey.page.windows)

    def public_fixture(self):
        journey = self.journey()
        output, stage = self.root / 'audit-output', self.root / 'stage'
        (output / 'PublicPortal').mkdir(parents=True)
        stage.mkdir()
        prefix = 'games/fantasy-cards/'
        images = [('StreamingAssets/Cards/alpha.png', b'alpha image bytes'),
                  ('StreamingAssets/Cards/nested/beta.png', b'beta image bytes')]
        expected = [{'file': name, 'bytes': len(body), 'sha256': sha256(body)}
                    for name, body in images]
        journey.report['buildFingerprint'] = {'files': copy.deepcopy(expected)}
        manifest = {'files': {prefix + row['file']: row['sha256'] for row in expected}}
        manifest_path = stage / 'build-manifest.json'
        manifest_path.write_text(json.dumps(manifest) + '\n')
        manifest_body = manifest_path.read_bytes()
        base_url = 'https://example.invalid/firstgame/'
        manifest_url = base_url + 'build-manifest.json'
        rows = [{'file': prefix + row['file'], 'status': 200, 'passed': True,
                 'bytes': row['bytes'], 'sha256': row['sha256'],
                 'resolvedUrl': GAME_URL + row['file']} for row in expected]
        rows.append({'file': 'build-manifest.json', 'status': 200, 'passed': True,
                     'bytes': len(manifest_body), 'sha256': sha256(manifest_body),
                     'resolvedUrl': manifest_url})
        audit = {'baseUrl': base_url, 'status': 'passed', 'files': rows,
                 'manifest': {'sha256': sha256(manifest_body)}}
        audit_path = output / 'PublicPortal/report.json'
        audit_path.write_text(json.dumps(audit) + '\n')
        canon = ModuleType('canon_release_evidence')
        canon.OUT, canon.STAGE = output, stage
        canon.verify_portal_review = mock.Mock(
            return_value={'report': {'sha256': sha256(audit_path.read_bytes())}})
        return SimpleNamespace(journey=journey, canon=canon, audit=audit,
                               audit_path=audit_path, expected=expected, manifest=manifest,
                               manifest_path=manifest_path, manifest_body=manifest_body,
                               manifest_url=manifest_url)

    def public_bytes(self, case, body=None):
        response = FakeResponse(case.manifest_body if body is None else body, case.manifest_url)
        with mock.patch.dict(sys.modules, {'canon_release_evidence': case.canon}), \
                mock.patch.object(SMOKE, 'ROOT', self.root), \
                mock.patch.object(SMOKE.urllib.request, 'urlopen', return_value=response) as fetch:
            case.journey.public_bytes(GAME_URL)
        return fetch

    def test_sharp_public_images_use_the_current_complete_audit_and_one_live_manifest_get(self):
        case = self.public_fixture()
        fetch = self.public_bytes(case)

        case.canon.verify_portal_review.assert_called_once_with(case.audit)
        self.assertEqual(fetch.call_count, 1)
        request = fetch.call_args.args[0]
        self.assertEqual(request.full_url, case.manifest_url)
        self.assertEqual(request.get_header('Accept-encoding'), 'identity')
        self.assertEqual(request.get_header('Cache-control'), 'no-cache')
        self.assertEqual(fetch.call_args.kwargs, {'timeout': 3})
        delivery = case.journey.report['publicDelivery']
        self.assertEqual(delivery['status'], 'passed')
        self.assertEqual(delivery['gameUrl'], GAME_URL)
        self.assertEqual(delivery['files'], [
            {**row, 'url': GAME_URL + row['file'], 'status': 200,
             'verificationSource': 'current-canon-full-GET-audit'} for row in case.expected])
        complete = case.journey.report['completePublicImageAudit']
        self.assertEqual(complete['images'], len(case.expected))
        self.assertEqual(complete['manifestSha256'], sha256(case.manifest_body))
        self.assertEqual(complete['sha256'], sha256(case.audit_path.read_bytes()))

    def test_sharp_public_audit_requires_each_images_exact_bytes_sha_and_manifest_entry(self):
        for fault in ('bytes', 'sha256', 'manifest-sha256', 'missing-image', 'resolved-url'):
            with self.subTest(fault=fault):
                # Each subcase needs separate audit/stage paths inside the patched root.
                with tempfile.TemporaryDirectory(dir=self.root) as subdirectory:
                    original_root = self.root
                    self.root = Path(subdirectory)
                    try:
                        case = self.public_fixture()
                        if fault == 'bytes':
                            case.audit['files'][1]['bytes'] += 1
                        elif fault == 'sha256':
                            case.audit['files'][1]['sha256'] = '0' * 64
                        elif fault == 'manifest-sha256':
                            name = 'games/fantasy-cards/' + case.expected[1]['file']
                            case.manifest['files'][name] = '0' * 64
                            case.manifest_path.write_text(json.dumps(case.manifest) + '\n')
                            case.manifest_body = case.manifest_path.read_bytes()
                        elif fault == 'missing-image':
                            del case.audit['files'][1]
                        elif fault == 'resolved-url':
                            case.audit['files'][1]['resolvedUrl'] += '?old-revision'
                        case.audit_path.write_text(json.dumps(case.audit) + '\n')
                        with self.assertRaisesRegex(AssertionError, 'public image byte audit differs'):
                            self.public_bytes(case)
                        self.assertNotIn('publicDelivery', case.journey.report)
                    finally:
                        self.root = original_root

    def test_sharp_rejects_a_live_manifest_changed_since_the_complete_get_audit(self):
        case = self.public_fixture()
        with self.assertRaisesRegex(AssertionError, 'Live canon manifest changed'):
            self.public_bytes(case, body=case.manifest_body + b' ')
        case.canon.verify_portal_review.assert_called_once_with(case.audit)
        self.assertNotIn('publicDelivery', case.journey.report)


if __name__ == '__main__':
    unittest.main()
