"""Source-admission unit cases. Authoring this file executes no product tests."""
from pathlib import Path
import copy
import hashlib
import importlib.util
import json
import os
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'Tools/Validation'))
from ClassicNpr import contract as admission


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def source_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ClassicNprContractTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='classic-npr-source-contract-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.native_path = self.root / 'native-contract.json'
        self.baseline_path = self.root / 'baseline-obligations.json'
        self.base = {'schema': 1, 'profile': 'classic-npr', 'nativeFullnames': {}, 'browser': {}}
        self.native = {'schema': 1, 'profile': 'classic-npr', 'sourceStatus': 'frozen', 'games': {}}
        for game, count in (('FantasyCards', 9), ('PathOfDawn', 15)):
            names = [game + '.SmokeTests.ContractFixture.Retained' + str(index) for index in range(count)]
            self.base['nativeFullnames'][game] = names
            self.base['browser'][game] = {mode: {'requiredCheckpoints': ['retained-screen'], 'steps': 8}
                                          for mode in ('desktop', 'mobile', 'public')}
            source = self.root / ('Games/' + game + '/Assets/SmokeTests/ContractFixture.cs')
            source.parent.mkdir(parents=True)
            source.write_text('// Source-admission fixture only; never compiled or executed.\n')
            Path(str(source) + '.meta').write_text('fileFormatVersion: 2\nguid: fixture\n')
            rows = []
            for path in (source, Path(str(source) + '.meta')):
                raw = path.read_bytes()
                rows.append({'file': str(path.relative_to(self.root)), 'bytes': len(raw),
                             'sha256': hashlib.sha256(raw).hexdigest(),
                             'fullnames': names if path.suffix == '.cs' else []})
            self.native['games'][game] = {'revision': admission.REVISIONS[game], 'count': count,
                'fullnames': names, 'retainedFullnames': list(names), 'sourceFiles': rows,
                'requiredOriginalSuffixes': ['actual-source-fixture-original']}
        write_json(self.baseline_path, self.base)
        self.patch = patch.multiple(admission, ROOT=self.root, BASELINE=self.baseline_path,
                                    BASELINE_SHA=hashlib.sha256(self.baseline_path.read_bytes()).hexdigest(),
                                    NATIVE_CONTRACT=self.native_path)
        self.patch.start()
        self.addCleanup(self.patch.stop)
        self.save()

    def save(self):
        write_json(self.native_path, self.native)

    def plan(self, game='FantasyCards'):
        required = ['retained-screen', *admission.NEW_ORIGINALS[game]]
        return {'revision': admission.REVISIONS[game], 'suite': 'modern-real-input-v1',
            'sourceStatus': 'frozen', 'evidenceRole': admission.browser_role(game),
            'classicNprRouteAdmission': {'sourceStatus': 'frozen', 'inputPlan': 'desktop',
                'ordinaryControlsOnly': True, 'requiredNewOriginals': list(admission.NEW_ORIGINALS[game])},
            'requiredCheckpoints': required,
            'steps': ([{'type': 'audio', 'label': str(index)} for index in range(4)]
                      + [{'type': 'reload'} for _ in range(2)]
                      + [{'type': 'checkpoint', 'name': name} for name in required])}

    def test_both_current_games_require_retained_source_cases(self):
        self.assertEqual(9, len(admission.native_full_names('FantasyCards')))
        self.assertEqual(15, len(admission.native_full_names('PathOfDawn')))
        self.native['games']['PathOfDawn']['fullnames'].pop()
        self.native['games']['PathOfDawn']['count'] -= 1
        self.save()
        with self.assertRaises(ValueError):
            admission.native_full_names('PathOfDawn')

    def test_unreviewed_native_draft_never_admits(self):
        self.native['sourceStatus'] = 'draft'
        self.save()
        with self.assertRaises(ValueError):
            admission.native_full_names('FantasyCards')

    def test_source_bytes_and_exact_metadata_pair_are_mandatory(self):
        original = copy.deepcopy(self.native)
        self.native['games']['FantasyCards']['sourceFiles'].pop()
        self.save()
        with self.assertRaises(ValueError):
            admission.native_full_names('FantasyCards')
        self.native = original
        self.save()
        source = self.root / self.native['games']['FantasyCards']['sourceFiles'][0]['file']
        source.write_text('changed source\n')
        with self.assertRaises(ValueError):
            admission.native_full_names('FantasyCards')

    def test_explicit_additive_case_must_be_assigned_once(self):
        game = self.native['games']['PathOfDawn']
        game['fullnames'] = list(game['fullnames']) + ['PathOfDawn.SmokeTests.ContractFixture.NewCase']
        game['count'] += 1
        game['sourceFiles'][0]['fullnames'] = list(game['fullnames'])
        self.save()
        self.assertEqual(16, len(admission.native_full_names('PathOfDawn')))
        game['sourceFiles'][0]['fullnames'].pop()
        self.save()
        with self.assertRaises(ValueError):
            admission.native_full_names('PathOfDawn')

    def test_current_card_cannot_use_preserved_regression_role(self):
        plan = self.plan()
        admission.require_browser_plan(plan, 'FantasyCards', expected_kind='desktop')
        plan['evidenceRole'] = 'preserved-card-regression'
        with self.assertRaises(ValueError):
            admission.require_browser_plan(plan, 'FantasyCards', expected_kind='desktop')

    def test_all_retained_and_changed_originals_must_remain_required(self):
        for missing in ('retained-screen', admission.NEW_ORIGINALS['PathOfDawn'][0]):
            plan = self.plan('PathOfDawn')
            plan['requiredCheckpoints'].remove(missing)
            with self.assertRaises(ValueError):
                admission.require_browser_plan(plan, 'PathOfDawn', expected_kind='desktop')

    def test_audio_reload_and_geometry_source_cannot_be_shortened(self):
        for kind in ('audio', 'reload'):
            plan = self.plan()
            index = next(i for i, step in enumerate(plan['steps']) if step['type'] == kind)
            plan['steps'].pop(index)
            with self.assertRaises(ValueError):
                admission.require_browser_plan(plan, 'FantasyCards', expected_kind='desktop')
        plan = self.plan()
        plan['classicNprRouteAdmission']['sourceStatus'] = 'draft'
        with self.assertRaises(ValueError):
            admission.require_browser_plan(plan, 'FantasyCards', expected_kind='desktop')

    def test_plan_cannot_select_a_different_profile_with_lower_obligations(self):
        plan = self.plan('PathOfDawn')
        plan['classicNprRouteAdmission']['inputPlan'] = 'mobile'
        with self.assertRaises(ValueError):
            admission.require_browser_plan(plan, 'PathOfDawn', expected_kind='desktop')
        plan = self.plan()
        with self.assertRaises(ValueError):
            admission.require_browser_plan(plan, 'FantasyCards', expected_kind='public')

    def test_retained_obligation_manifest_cannot_change_silently(self):
        self.baseline_path.write_text('{}\n')
        with self.assertRaises(ValueError):
            admission.native_full_names('FantasyCards')


class ClassicNprProfileTests(unittest.TestCase):
    def profile(self, name):
        with patch.dict(os.environ, FIRSTGAME_RELEASE_PROFILE=name):
            return source_module(ROOT / 'Tools/Validation/LivingRpgClassic/profile.py',
                                 'source_only_profile_' + name.replace('-', '_'))

    def test_new_mode_selects_both_current_games_and_workspace_roots(self):
        profile = self.profile('classic-npr')
        self.assertEqual(('FantasyCards', 'PathOfDawn'), profile.CURRENT_GAMES)
        self.assertEqual((), profile.PRESERVED_GAMES)
        self.assertFalse(profile.IS_GOSPEL_NPR)
        self.assertTrue(profile.USES_NPR_GOSPEL)
        self.assertEqual(ROOT / 'Artifacts/ClassicNprBuilds', profile.BUILD)
        self.assertEqual(ROOT / 'Artifacts/ClassicNprWebPlaytest', profile.STAGE)

    def test_old_gospel_mode_retains_its_preserved_card_boundary(self):
        profile = self.profile('gospel-npr')
        self.assertEqual(('PathOfDawn',), profile.CURRENT_GAMES)
        self.assertEqual(('FantasyCards',), profile.PRESERVED_GAMES)
        self.assertTrue(profile.IS_GOSPEL_NPR)
        self.assertFalse(profile.IS_CLASSIC_NPR)

    def test_two_game_stage_contract_matches_exact_profile_revisions(self):
        profile = self.profile('classic-npr')
        contracts = source_module(ROOT / 'Tools/Web/release_contract.py', 'classic_npr_stage_contract_test')
        release = contracts.contract('classic-npr')
        self.assertEqual(profile.REVISIONS, release['revisions'])
        self.assertEqual([], release['preservedGames'])
        self.assertEqual(['Web'], release['currentTargets'])


if __name__ == '__main__':
    unittest.main()
