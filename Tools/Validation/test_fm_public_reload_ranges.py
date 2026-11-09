"""Authored contract cases for the narrow Canon public reload range proof.

The timestamps and media identities mirror CardPublic02, but these small
stubs are unit-test inputs, not a replacement for its raw report, observers,
Root originals, full public audit or browser PCM.
"""
import copy
import importlib.util
from pathlib import Path
import unittest


MODULE_PATH = Path(__file__).with_name('fm-local-media-errors.py')
SPEC = importlib.util.spec_from_file_location('canon_public_reload_media_errors', MODULE_PATH)
MEDIA = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MEDIA)

GAME_URL = 'https://ka8877.github.io/firstgame/games/fantasy-cards/'
PREFIX = 'StreamingAssets/Audio/ModernCard/'
SOURCES = {
    'title.ogg': (384043, 'bd9ce5f8920988a893476c0d537501ca7c38af3aac101a5e8535208181e079dc'),
    'lobby.ogg': (681274, '897b2a946584e9e2572d2723b32f040ffba1e7d315d3fab5570b4f3dfafeb85a'),
    'deck.ogg': (420076, '2d86efd77128b4c43c63bc40483cc69ab399935ad7e91e03bb8884e0f21b0040'),
    'battle.ogg': (1359350, '67d70ddc22dc6d768aeedfb6540ebeb04d22c8b6044186c9ecb917669d60fc96'),
}
EVENTS = {
    'title.ogg': (1794250506229.8499, 1794250506153, 1794250506279, 1794250506326),
    'lobby.ogg': (1794250506314.4138, 1794250506279, 1794250506326, 1794250506380),
}


def url(filename):
    return GAME_URL + PREFIX + filename


def player(filename, ready=0):
    return {'source': url(filename), 'currentTime': 0, 'paused': True,
            'pending': False, 'readyState': ready, 'active': False}


def observation(at, navigation, cancelled, *, decoded=False, reason='sample'):
    players = [player(name, ready=4 if name == 'battle.ogg' else 0) for name in SOURCES]
    if decoded:
        next(item for item in players if item['source'] == url(cancelled))['readyState'] = 4
    return {'navigation': navigation, 'epochMs': at, 'reason': reason,
            'state': {'unlocked': False, 'contextState': 'not-created',
                      'actualSource': None, 'actualPaused': True,
                      'sourceKind': 'bundled-instrumental'},
            'players': players}


def fixture(cancelled='title.ogg', navigation=2):
    when, before, after, decoded = EVENTS[cancelled]
    files = [{'file': PREFIX + name, 'bytes': size, 'sha256': sha}
             for name, (size, sha) in SOURCES.items()]
    selected = player('battle.ogg', ready=4)
    selected.update(currentTime=5.709841, paused=False, active=True)
    window = {'navigation': navigation, 'label': 'continued-battle',
              'rawEvidence': f'audio-{navigation}-continued-battle.json',
              'sha256': '36762bb8e1534c56f34897fe44055ad1214f338711380d952da31c1819eb62d2',
              'startedEpochMs': 1794250524363.2102,
              'finishedEpochMs': 1794250526710.2705,
              'sourceUrls': [url('battle.ogg')], 'sampleCount': 16,
              'medianRms': 0.029709452280713797, 'audibleSampleFraction': 1.0,
              'mediaProgressSeconds': 2.278458, 'allContextsRunning': True,
              'isolatedMusicRoute': True, 'sourceIdentities': [[1, 9]],
              'nonfiniteSamples': 0, 'observerErrors': [],
              'rolePlayback': {
                  'state': {'sourceKind': 'bundled-instrumental',
                            'actualSource': url('battle.ogg'), 'actualPaused': False,
                            'contextState': 'running', 'unlocked': True,
                            'muted': False, 'volume': 0.6000000238418579},
                  'players': [selected]}}
    selected_observation = {'navigation': navigation,
                            'epochMs': window['startedEpochMs'] - 100,
                            'reason': 'sample',
                            'state': copy.deepcopy(window['rolePlayback']['state']),
                            'players': [copy.deepcopy(selected)]}
    return {'game': 'FantasyCards', 'revision': 'fm-canon-2026-11-09',
            'suite': 'modern-real-input-v1', 'game_url': GAME_URL,
            'failure': {'url': url(cancelled), 'method': 'GET',
                        'resourceType': 'media', 'error': 'net::ERR_ABORTED',
                        'observedEpochMs': when, 'navigation': navigation},
            'runtime_files': files,
            'observations': [observation(before, navigation, cancelled),
                             observation(after, navigation, cancelled),
                             observation(decoded, navigation, cancelled, decoded=True,
                                         reason='media-loadeddata:fm-local-audio-' + cancelled[:-4]),
                             selected_observation],
            'windows': [window],
            'responses': [{'url': url(cancelled), 'method': 'GET', 'status': 206,
                           'navigation': navigation} for _ in range(3)],
            'delivered': {'status': 'passed', 'gameUrl': GAME_URL,
                          'files': [{**row, 'url': GAME_URL + row['file'], 'status': 200}
                                    for row in files]}}


def initial_after_abort_fixture(navigation=2):
    """Authored Public03 battle projection with no pre-abort observation."""
    case = fixture(navigation=navigation)
    case['failure'].update(url=url('battle.ogg'), observedEpochMs=1794252222425.9592)
    case['responses'] = [{'url': url('battle.ogg'), 'method': 'GET', 'status': 206,
                          'navigation': navigation} for _ in range(3)]
    initial_rows = []
    for index, (at, reason) in enumerate([
            (1794252222469, 'media-loadeddata:fm-local-audio-lobby'),
            (1794252222472, 'media-loadeddata:fm-local-audio-deck'),
            (1794252222515, 'media-loadeddata:fm-local-audio-battle'),
            (1794252222554, 'media-loadeddata:fm-local-audio-title'),
            (1794252222560, 'sample')]):
        row = observation(at, navigation, 'battle.ogg', decoded=index >= 2, reason=reason)
        for item in row['players']:
            filename = item['source'].rsplit('/', 1)[-1]
            item['readyState'] = (4 if filename == 'lobby.ogg' or index >= 3
                                  or filename == 'deck.ogg' and index >= 1
                                  or filename == 'battle.ogg' and index >= 2
                                  else 1 if filename == 'deck.ogg' else 0)
            item.update(restartNeeded=True, restartCount=0, lastStartAtMs=None,
                        lastStartPosition=None, lastStartRoleSerial=None,
                        lastPromotionPosition=None, fadeTarget=0,
                        contextState='not-created')
        initial_rows.append(row)
    window = case['windows'][0]
    window['startedEpochMs'] = 1794252240000.0
    window['finishedEpochMs'] = 1794252242300.0
    selected_row = copy.deepcopy(case['observations'][3])
    selected_row['epochMs'] = window['startedEpochMs'] - 100
    case['observations'] = [*initial_rows, selected_row]
    return case


def cancelled_player(case, row_index):
    return next(item for item in case['observations'][row_index]['players']
                if item['source'] == case['failure']['url'])


def runtime_row(case, filename):
    return next(item for item in case['runtime_files'] if item['file'] == PREFIX + filename)


def delivered_row(case, filename):
    return next(item for item in case['delivered']['files'] if item['file'] == PREFIX + filename)


class CanonPublicReloadRangesTests(unittest.TestCase):
    def prove(self, case):
        return MEDIA.recovered_public_reload_pregesture_range_cancellation(**case)

    def rejects(self, mutate, *, cancelled='title.ogg'):
        case = fixture(cancelled)
        mutate(case)
        self.assertIsNone(self.prove(case))

    def test_both_recorded_nav2_shapes_have_specific_proof_without_mutating_inputs(self):
        for cancelled in EVENTS:
            with self.subTest(cancelled=cancelled):
                case = fixture(cancelled)
                original = copy.deepcopy(case)
                proof = self.prove(case)
                self.assertIsInstance(proof, dict)
                self.assertEqual(proof['request'], case['failure'])
                self.assertEqual(proof['category'], 'public-reload-pregesture-decoded-range')
                self.assertEqual(proof['beforeEpochMs'], EVENTS[cancelled][1])
                self.assertEqual(proof['afterEpochMs'], EVENTS[cancelled][2])
                self.assertEqual(proof['decodedBeforeUnlockEpochMs'], EVENTS[cancelled][3])
                self.assertEqual(proof['verifiedPublicSha256'], SOURCES[cancelled][1])
                self.assertEqual(proof['verifiedPublicBytes'], SOURCES[cancelled][0])
                self.assertEqual(proof['verifiedSelectedPublicSha256'], SOURCES['battle.ogg'][1])
                self.assertEqual(proof['http206Responses'], 3)
                self.assertEqual(proof['http206Evidence'], case['responses'])
                self.assertEqual(proof['subsequentPcm']['rawEvidence'], case['windows'][0]['rawEvidence'])
                self.assertEqual(proof['subsequentPcm']['sha256'], case['windows'][0]['sha256'])
                self.assertEqual(case, original)

    def test_same_contract_accepts_first_real_reload(self):
        case = fixture(navigation=1)
        proof = self.prove(case)
        self.assertIsInstance(proof, dict)
        self.assertEqual(proof['request']['navigation'], 1)

    def test_minimum_six_samples_and_audible_fraction_are_sufficient(self):
        case = fixture()
        case['windows'][0].update(sampleCount=6, audibleSampleFraction=0.5)
        self.assertIsNotNone(self.prove(case))

    def test_current_full_get_image_audit_rows_preserve_complete_delivery(self):
        case = fixture()
        image = {'file': 'StreamingAssets/Cards/fm_10010624537.jpg', 'bytes': 45375,
                 'sha256': '3877f31fa6643c5d9c8b1c384ca0c15e033f53676baf39c4c294af3441ffb87e'}
        case['runtime_files'].append(image)
        case['delivered']['files'].append({**image, 'url': GAME_URL + image['file'],
                                           'status': 200,
                                           'verificationSource': 'current-canon-full-GET-audit'})
        self.assertIsNotNone(self.prove(case))

    def test_an_image_runtime_row_without_matching_delivery_is_not_a_complete_set(self):
        case = fixture()
        case['runtime_files'].append({
            'file': 'StreamingAssets/Cards/fm_10010624537.jpg', 'bytes': 45375,
            'sha256': '3877f31fa6643c5d9c8b1c384ca0c15e033f53676baf39c4c294af3441ffb87e'})
        self.assertIsNone(self.prove(case))

    def test_other_games_revisions_suites_and_origins_are_not_qualified(self):
        mutations = [
            ('game', 'PathOfDawn'), ('revision', 'fm-nativity-2026-11-02'),
            ('revision', 'fm-canon-2026-11-10'), ('suite', 'classic-real-input-v1'),
            ('game_url', GAME_URL.replace('https:', 'http:')),
            ('game_url', GAME_URL.replace('ka8877.github.io', 'other.invalid')),
            ('game_url', GAME_URL.rstrip('/')), ('game_url', GAME_URL + '?v=1'),
            ('game_url', GAME_URL + '#x'),
            ('game_url', GAME_URL.replace('/fantasy-cards/', '/path-of-dawn/')),
        ]
        for key, value in mutations:
            with self.subTest(key=key, value=value):
                self.rejects(lambda case: case.update({key: value}))

    def test_only_exact_reload_media_get_abort_is_supported(self):
        mutations = [('navigation', 0), ('navigation', 3), ('navigation', -1),
                     ('navigation', True), ('navigation', 2.0), ('navigation', '2'),
                     ('method', 'POST'), ('resourceType', 'fetch'),
                     ('error', 'net::ERR_CONNECTION_RESET'),
                     ('observedEpochMs', float('nan')), ('observedEpochMs', True)]
        for key, value in mutations:
            with self.subTest(key=key, value=value):
                self.rejects(lambda case: case['failure'].update({key: value}))
        for value in [url('title.ogg') + '?v=1', url('title.ogg') + '#x',
                      url('title.ogg').replace('ka8877.github.io', 'other.invalid'),
                      url('title.ogg').replace('/ModernCard/', '/Other/'),
                      url('title.ogg').replace('title.ogg', 'unknown.ogg')]:
            with self.subTest(url=value):
                self.rejects(lambda case: case['failure'].update(url=value))

    def test_missing_old_late_or_cross_navigation_brackets_are_not_proof(self):
        mutations = [
            ('no-observations', lambda case: case.update(observations=[])),
            ('no-before', lambda case: case['observations'].pop(0)),
            ('no-after', lambda case: case.update(observations=case['observations'][:1])),
            ('old-before', lambda case: case['observations'][0].update(
                epochMs=case['failure']['observedEpochMs'] - 1001)),
            ('late-after', lambda case: [case['observations'][index].update(
                epochMs=case['failure']['observedEpochMs'] + 1001 + index) for index in (1, 2)]),
            ('wrong-before-nav', lambda case: case['observations'][0].update(navigation=1)),
            ('wrong-after-nav', lambda case: [case['observations'][index].update(navigation=1)
                                             for index in (1, 2)]),
            ('wrong-decode-nav', lambda case: case['observations'][2].update(navigation=1)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_every_observed_state_through_decode_stays_before_unlock(self):
        for index in range(3):
            for key, value in [('unlocked', True), ('contextState', 'running'),
                               ('actualSource', url('title.ogg')), ('actualPaused', False),
                               ('sourceKind', 'provider'), ('unlocked', 0)]:
                with self.subTest(index=index, key=key, value=value):
                    self.rejects(lambda case: case['observations'][index]['state'].update({key: value}))
            for key, value in [('active', True), ('paused', False), ('pending', True),
                               ('currentTime', 0.1), ('currentTime', float('nan'))]:
                with self.subTest(index=index, player_key=key, value=value):
                    self.rejects(lambda case: cancelled_player(case, index).update({key: value}))

    def test_intermediate_unlock_cannot_be_hidden_by_valid_boundary_samples(self):
        case = fixture()
        middle = copy.deepcopy(case['observations'][1])
        middle['epochMs'] = case['failure']['observedEpochMs'] + 10
        middle['state']['unlocked'] = True
        case['observations'].insert(1, middle)
        self.assertIsNone(self.prove(case))

    def test_decode_requires_matching_loadeddata_event_for_cancelled_player(self):
        mutations = [
            ('no-decode', lambda case: case['observations'].pop(2)),
            ('not-loaded', lambda case: cancelled_player(case, 2).update(readyState=0)),
            ('wrong-source', lambda case: case['observations'][2].update(
                reason='media-loadeddata:fm-local-audio-battle')),
            ('sample-only', lambda case: case['observations'][2].update(reason='sample')),
            ('at-abort', lambda case: case['observations'][2].update(
                epochMs=case['failure']['observedEpochMs'])),
            ('late-decode', lambda case: case['observations'][2].update(
                epochMs=case['failure']['observedEpochMs'] + 2001)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_complete_delivery_outer_status_origin_and_rows_are_required(self):
        mutations = [
            ('missing', lambda case: case.update(delivered={})),
            ('files-list-only', lambda case: case.update(delivered=case['delivered']['files'])),
            ('failed', lambda case: case['delivered'].update(status='failed')),
            ('wrong-origin', lambda case: case['delivered'].update(gameUrl='https://other.invalid/')),
            ('missing-files', lambda case: case['delivered'].pop('files')),
            ('empty-files', lambda case: case['delivered'].update(files=[])),
            ('extra-file', lambda case: case['delivered']['files'].append(
                {'file': 'extra.json', 'url': GAME_URL + 'extra.json',
                 'status': 200, 'bytes': 1, 'sha256': 'a' * 64})),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_cancelled_and_selected_sources_need_exact_fresh_full_byte_delivery(self):
        for filename in ('title.ogg', 'battle.ogg'):
            for key, value in [('status', 206), ('status', 503), ('bytes', 1),
                               ('sha256', '0' * 64), ('url', 'https://other.invalid/audio.ogg'),
                               ('file', PREFIX + 'unknown.ogg'), ('error', 'decode failed'),
                               ('inheritedGet', True), ('verificationSource', 'historical-proof')]:
                with self.subTest(filename=filename, key=key, value=value):
                    self.rejects(lambda case: delivered_row(case, filename).update({key: value}))
            self.rejects(lambda case: case['delivered']['files'].remove(delivered_row(case, filename)))
            for key, value in [('bytes', 0), ('bytes', True), ('bytes', 1.0), ('sha256', 'not-a-sha256'),
                               ('sha256', 'g' * 64), ('file', PREFIX + '../title.ogg')]:
                with self.subTest(runtime=filename, key=key, value=value):
                    self.rejects(lambda case: runtime_row(case, filename).update({key: value}))

    def test_conflicting_duplicate_runtime_or_delivery_rows_are_rejected(self):
        for collection in ('runtime', 'delivery'):
            case = fixture()
            rows = case['runtime_files'] if collection == 'runtime' else case['delivered']['files']
            duplicate = copy.deepcopy(rows[0])
            duplicate['sha256'] = '0' * 64
            rows.append(duplicate)
            with self.subTest(collection=collection):
                self.assertIsNone(self.prove(case))

    def test_matching_malformed_source_metadata_is_not_a_valid_identity(self):
        for filename in ('title.ogg', 'battle.ogg'):
            for key, value in [('bytes', 0), ('bytes', -1), ('bytes', True), ('bytes', 1.0),
                               ('sha256', 'not-a-sha256'), ('sha256', 'g' * 64)]:
                case = fixture()
                runtime_row(case, filename)[key] = value
                delivered_row(case, filename)[key] = value
                with self.subTest(filename=filename, key=key, value=value):
                    self.assertIsNone(self.prove(case))

    def test_repeated_ranges_need_same_exact_source_navigation_and_get_status(self):
        mutations = [
            ('missing', lambda case: case.update(responses=[])),
            ('single', lambda case: case.update(responses=case['responses'][:1])),
            ('other-navigation', lambda case: [row.update(navigation=1) for row in case['responses']]),
            ('boolean-navigation', lambda case: [row.update(navigation=True) for row in case['responses']]),
            ('other-url', lambda case: [row.update(url=url('lobby.ogg')) for row in case['responses']]),
            ('other-method', lambda case: [row.update(method='POST') for row in case['responses']]),
            ('not-range', lambda case: [row.update(status=200) for row in case['responses']]),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_real_http_errors_are_rejected_even_with_repeated_successful_ranges(self):
        for filename in ('title.ogg', 'battle.ogg'):
            case = fixture()
            case['responses'].append({'url': url(filename), 'method': 'GET',
                                      'status': 503, 'navigation': 2})
            with self.subTest(filename=filename):
                self.assertIsNone(self.prove(case))

    def test_boolean_navigation_does_not_equal_first_reload_evidence(self):
        for evidence in ('before', 'responses', 'window'):
            case = fixture(navigation=1)
            if evidence == 'before':
                case['observations'][0]['navigation'] = True
            elif evidence == 'responses':
                for row in case['responses']:
                    row['navigation'] = True
            else:
                case['windows'][0]['navigation'] = True
            with self.subTest(evidence=evidence):
                self.assertIsNone(self.prove(case))

    def test_later_pcm_must_be_same_navigation_selected_and_after_decode(self):
        mutations = [
            ('missing', lambda case: case.update(windows=[])),
            ('other-navigation', lambda case: case['windows'][0].update(navigation=1)),
            ('before-decode', lambda case: case['windows'][0].update(
                startedEpochMs=case['observations'][2]['epochMs'] - 1)),
            ('unknown-source', lambda case: case['windows'][0].update(sourceUrls=[GAME_URL + 'private.ogg'])),
            ('multiple-sources', lambda case: case['windows'][0].update(sourceUrls=[url('battle.ogg'), url('title.ogg')])),
            ('no-role-playback', lambda case: case['windows'][0].pop('rolePlayback')),
            ('wrong-selected', lambda case: case['windows'][0]['rolePlayback']['state'].update(actualSource=url('title.ogg'))),
            ('context-not-running', lambda case: case['windows'][0]['rolePlayback']['state'].update(contextState='suspended')),
            ('selected-paused', lambda case: case['windows'][0]['rolePlayback']['state'].update(actualPaused=True)),
            ('player-inactive', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(active=False)),
            ('player-paused', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(paused=True)),
            ('player-pending', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(pending=True)),
            ('player-not-decoded', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(readyState=0)),
            ('player-nonfinite-position', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(currentTime=float('nan'))),
            ('player-negative-position', lambda case: case['windows'][0]['rolePlayback']['players'][0].update(currentTime=-1)),
            ('state-not-unlocked', lambda case: case['windows'][0]['rolePlayback']['state'].update(unlocked=False)),
            ('wrong-source-kind', lambda case: case['windows'][0]['rolePlayback']['state'].update(sourceKind='provider')),
            ('muted', lambda case: case['windows'][0]['rolePlayback']['state'].update(muted=True)),
            ('zero-volume', lambda case: case['windows'][0]['rolePlayback']['state'].update(volume=0)),
            ('nonfinite-volume', lambda case: case['windows'][0]['rolePlayback']['state'].update(volume=float('nan'))),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_selected_source_needs_a_same_navigation_passive_sample_before_pcm(self):
        mutations = [
            ('missing-selected', lambda case: case['observations'].pop(3)),
            ('wrong-navigation', lambda case: case['observations'][3].update(navigation=1)),
            ('after-pcm', lambda case: case['observations'][3].update(
                epochMs=case['windows'][0]['startedEpochMs'] + 1)),
            ('wrong-source', lambda case: case['observations'][3]['state'].update(actualSource=url('title.ogg'))),
            ('selected-paused', lambda case: case['observations'][3]['players'][0].update(paused=True)),
            ('selected-inactive', lambda case: case['observations'][3]['players'][0].update(active=False)),
            ('selected-pending', lambda case: case['observations'][3]['players'][0].update(pending=True)),
            ('selected-not-decoded', lambda case: case['observations'][3]['players'][0].update(readyState=0)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_silent_nonfinite_shared_or_error_pcm_cannot_qualify_request_failures(self):
        updates = [{'sampleCount': 5}, {'sampleCount': True}, {'medianRms': 0},
                   {'medianRms': 1e-5}, {'medianRms': float('nan')},
                   {'audibleSampleFraction': 0.49}, {'audibleSampleFraction': 1.01},
                   {'mediaProgressSeconds': 0.5},
                   {'mediaProgressSeconds': float('inf')}, {'allContextsRunning': False},
                   {'isolatedMusicRoute': False}, {'sourceIdentities': []},
                   {'sourceIdentities': [[1, 9], [1, 10]]}, {'sourceIdentities': [[True, 9]]},
                   {'sourceIdentities': [[1, 0]]}, {'sourceIdentities': [[1]]}, {'nonfiniteSamples': 1},
                   {'observerErrors': ['audio context error']},
                   {'rawEvidence': '../outside.json'}, {'rawEvidence': '/tmp/audio.json'},
                   {'rawEvidence': 'audio.txt'}, {'sha256': 'not-a-sha256'},
                   {'maximumRms': 0}, {'maximumRms': float('nan')},
                   {'finishedEpochMs': 0}, {'finishedEpochMs': float('inf')}]
        for update in updates:
            with self.subTest(update=update):
                self.rejects(lambda case: case['windows'][0].update(update))


class CanonInitialPublicReloadRangesTests(unittest.TestCase):
    def prove(self, case):
        return MEDIA.recovered_public_reload_pregesture_range_cancellation(**case)

    def rejects(self, mutate):
        case = initial_after_abort_fixture()
        mutate(case)
        self.assertIsNone(self.prove(case))

    def test_actual_initial_after_abort_shape_has_explicit_observation_limit(self):
        case = initial_after_abort_fixture()
        original = copy.deepcopy(case)
        proof = self.prove(case)
        self.assertIsInstance(proof, dict)
        self.assertEqual(proof['category'], 'public-reload-initial-pregesture-decoded-range')
        self.assertEqual(proof['request'], case['failure'])
        self.assertIs(proof['observedBeforeAbort'], False)
        self.assertIsNone(proof['beforeEpochMs'])
        self.assertIsNone(proof['beforeState'])
        self.assertIsNone(proof['beforePlayer'])
        self.assertEqual(proof['initialObservedEpochMs'], 1794252222469)
        self.assertEqual(proof['decodedBeforeUnlockEpochMs'], 1794252222515)
        self.assertEqual(proof['verifiedPublicSha256'], SOURCES['battle.ogg'][1])
        self.assertEqual(proof['subsequentPcm']['navigation'], 2)
        self.assertEqual(case, original)

    def test_first_reload_and_an_older_other_navigation_do_not_invent_a_prior_sample(self):
        case = initial_after_abort_fixture(navigation=1)
        unrelated = copy.deepcopy(case['observations'][0])
        unrelated.update(navigation=0, epochMs=case['failure']['observedEpochMs'] - 5000)
        case['observations'].insert(0, unrelated)
        proof = self.prove(case)
        self.assertIsInstance(proof, dict)
        self.assertEqual(proof['category'], 'public-reload-initial-pregesture-decoded-range')
        self.assertIs(proof['observedBeforeAbort'], False)
        self.assertEqual(proof['subsequentPcm']['navigation'], 1)

    def test_every_initial_cancelled_player_needs_all_never_played_fields(self):
        fields = ['restartNeeded', 'restartCount', 'lastStartAtMs', 'lastStartPosition',
                  'lastStartRoleSerial', 'lastPromotionPosition', 'fadeTarget', 'contextState']
        for index in range(3):
            for field in fields:
                with self.subTest(index=index, missing=field):
                    self.rejects(lambda case: cancelled_player(case, index).pop(field))

    def test_restart_or_any_prior_start_marker_is_not_an_initial_unused_preload(self):
        changes = [('restartNeeded', False), ('restartCount', 1), ('restartCount', True),
                   ('lastStartAtMs', 0), ('lastStartPosition', 0), ('lastStartRoleSerial', 0),
                   ('lastPromotionPosition', 0), ('fadeTarget', 1), ('fadeTarget', False),
                   ('contextState', 'running'), ('currentTime', 0.1),
                   ('pending', True), ('active', True), ('paused', False)]
        for index in range(3):
            for key, value in changes:
                with self.subTest(index=index, key=key, value=value):
                    self.rejects(lambda case: cancelled_player(case, index).update({key: value}))

    def test_old_or_exact_abort_time_same_navigation_observation_blocks_the_new_branch(self):
        for age in (0, 1001, 5000):
            case = initial_after_abort_fixture()
            earlier = copy.deepcopy(case['observations'][0])
            earlier['epochMs'] = case['failure']['observedEpochMs'] - age
            case['observations'].insert(0, earlier)
            with self.subTest(age=age):
                self.assertIsNone(self.prove(case))

    def test_unknown_timestamp_cannot_hide_an_earlier_current_navigation_observation(self):
        for timestamp in (None, True, float('nan')):
            case = initial_after_abort_fixture()
            unknown = copy.deepcopy(case['observations'][0])
            unknown['epochMs'] = timestamp
            case['observations'].insert(0, unknown)
            with self.subTest(timestamp=timestamp):
                self.assertIsNone(self.prove(case))

    def test_initial_and_decode_observations_must_be_prompt_and_same_navigation(self):
        mutations = [
            ('no-initial-rows', lambda case: case.update(observations=case['observations'][5:])),
            ('late-first-observation', lambda case: [row.update(epochMs=row['epochMs'] + 1100)
                                                    for row in case['observations'][:5]]),
            ('late-target-decode', lambda case: case['observations'][2].update(
                epochMs=case['failure']['observedEpochMs'] + 2001)),
            ('target-decode-at-abort', lambda case: case['observations'][2].update(
                epochMs=case['failure']['observedEpochMs'])),
            ('other-navigation', lambda case: [row.update(navigation=1)
                                               for row in case['observations'][:5]]),
            ('target-decode-other-navigation', lambda case: case['observations'][2].update(navigation=1)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)

    def test_initial_state_through_decode_cannot_unlock_or_select_any_source(self):
        for index in range(3):
            for key, value in [('unlocked', True), ('contextState', 'running'),
                               ('actualSource', url('battle.ogg')), ('actualPaused', False)]:
                with self.subTest(index=index, key=key, value=value):
                    self.rejects(lambda case: case['observations'][index]['state'].update({key: value}))

    def test_already_loaded_or_sample_rows_do_not_replace_the_exact_target_loadeddata(self):
        for ready_state in (2, 4):
            case = initial_after_abort_fixture()
            cancelled_player(case, 0)['readyState'] = ready_state
            with self.subTest(initial_ready_state=ready_state):
                self.assertIsNone(self.prove(case))
        for loaded_initial in (False, True):
            case = initial_after_abort_fixture()
            case['observations'].pop(2)
            if loaded_initial:
                for index in (0, 1):
                    cancelled_player(case, index)['readyState'] = 4
            with self.subTest(loaded_initial=loaded_initial):
                self.assertIsNone(self.prove(case))
        self.rejects(lambda case: case['observations'][2].update(reason='sample'))
        self.rejects(lambda case: case['observations'][2].update(
            reason='media-loadeddata:fm-local-audio-title'))

    def test_initial_branch_still_requires_full_delivery_ranges_and_selected_pcm(self):
        mutations = [
            ('no-delivery', lambda case: case['delivered'].update(files=[])),
            ('wrong-delivery-hash', lambda case: delivered_row(case, 'battle.ogg').update(sha256='0' * 64)),
            ('no-ranges', lambda case: case.update(responses=[])),
            ('cross-navigation-ranges', lambda case: [row.update(navigation=1) for row in case['responses']]),
            ('http-error', lambda case: case['responses'].append(
                {'url': url('battle.ogg'), 'method': 'GET', 'status': 503, 'navigation': 2})),
            ('no-pcm', lambda case: case.update(windows=[])),
            ('cross-navigation-pcm', lambda case: case['windows'][0].update(navigation=1)),
            ('silent-pcm', lambda case: case['windows'][0].update(medianRms=0)),
            ('no-passive-selected-source', lambda case: case['observations'].pop(5)),
        ]
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.rejects(mutate)


if __name__ == '__main__':
    unittest.main()
