#!/usr/bin/env python3
"""Sharp revision contracts using the unchanged bundled-media fixture suites.

Only prior valid fixture revisions are projected to Sharp. Intentionally wrong
revisions and all lifecycle/HTTP/PCM negatives stay unchanged. These are isolated
classifier inputs, not browser evidence or a revision of a failed public report.
"""
import copy
import importlib.util
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
SHARP = 'fm-sharp-client-2026-11-17'


def repository_root():
    for parent in Path(__file__).resolve().parents:
        if ((parent / 'Tools/Validation/fm-local-media-errors.py').is_file()
                and (parent / 'Games/FantasyCards/Assets').is_dir()):
            return parent
    raise RuntimeError('Could not locate the actual firstgame repository')


def load_source(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


ROOT = repository_root()
VALIDATION = ROOT / 'Tools/Validation'
MEDIA = load_source(VALIDATION / 'fm-local-media-errors.py', 'sharp_media_classifier_contract')
LEGACY = load_source(VALIDATION / 'test-fm-local-media-errors.py', 'sharp_media_legacy_fixture_contract')
RELOAD = load_source(VALIDATION / 'test_fm_public_reload_ranges.py', 'sharp_media_reload_fixture_contract')
LEGACY.m = MEDIA
RELOAD.MEDIA = MEDIA
BASE_FIXTURE = LEGACY.fixture


def sharp_base_fixture():
    case = BASE_FIXTURE()
    if case.get('revision') == 'fm-modern-2026-10-26':
        case['revision'] = SHARP
    return case


class SharpLegacyProjection:
    def setUp(self):
        super().setUp()
        fixture_patch = patch.object(LEGACY, 'fixture', sharp_base_fixture)
        fixture_patch.start()
        self.addCleanup(fixture_patch.stop)
        if hasattr(self, 'raw') and self.raw.get('revision') in MEDIA.SUPPORTED_LOCAL_REVISIONS:
            self.raw = copy.deepcopy(self.raw)
            self.raw['revision'] = SHARP


class SharpInactive(SharpLegacyProjection, LEGACY.InactiveMediaCancellationTests):
    pass


class SharpInactivePreload(SharpLegacyProjection, LEGACY.ActualInactivePreloadTests):
    pass


class SharpPublicPregesture(SharpLegacyProjection, LEGACY.ActualPublicPregestureTests):
    pass


class SharpPublicRecovered(SharpLegacyProjection, LEGACY.ActualPublicRecoveredRanges):
    pass


class SharpRenewalRange(SharpLegacyProjection, LEGACY.ActualRenewalPublicRange):
    pass


class SharpLoopbackReload(SharpLegacyProjection, LEGACY.AuthoredLoopbackReloadPregestureTests):
    pass


class SharpPublicProjection:
    def prove(self, case):
        projected = copy.deepcopy(case)
        if projected.get('revision') == 'fm-canon-2026-11-09':
            projected['revision'] = SHARP
        before = copy.deepcopy(projected)
        result = super().prove(projected)
        self.assertEqual(projected, before)
        return result


class SharpPublicReload(SharpPublicProjection, RELOAD.CanonPublicReloadRangesTests):
    pass


class SharpInitialReload(SharpPublicProjection, RELOAD.CanonInitialPublicReloadRangesTests):
    pass


class SharpPendingSelection(SharpPublicProjection, RELOAD.CanonPendingSelectionRangesTests):
    pass


class SharpDecodedPreload(SharpPublicProjection, RELOAD.CanonDecodedPregestureRangesTests):
    pass


def six_positive_cases():
    cases = [('inactive_local_media_cancellation', BASE_FIXTURE())]
    for name, cls in (
        ('inactive_local_media_preload_cancellation', LEGACY.ActualInactivePreloadTests),
        ('recovered_public_pregesture_range_cancellation', LEGACY.ActualPublicPregestureTests),
        ('recovered_public_range_cancellation', LEGACY.ActualPublicRecoveredRanges),
        ('recovered_loopback_reload_pregesture_range_cancellation', LEGACY.AuthoredLoopbackReloadPregestureTests),
    ):
        instance = cls()
        instance.setUp()
        case = (instance.args() if name.startswith('recovered_loopback_')
                else instance.args(instance.raw['failures'][0]))
        cases.append((name, copy.deepcopy(case)))
    cases.append(('recovered_public_reload_pregesture_range_cancellation', RELOAD.fixture()))
    return cases


class SharpRevisionAdmission(unittest.TestCase):
    def test_all_six_prior_valid_shapes_admit_sharp_without_mutating_evidence(self):
        for name, case in six_positive_cases():
            with self.subTest(classifier=name):
                before = copy.deepcopy(case)
                self.assertIsNotNone(getattr(MEDIA, name)(**case))
                self.assertEqual(case, before)
                case['revision'] = SHARP
                before = copy.deepcopy(case)
                self.assertIsNotNone(getattr(MEDIA, name)(**case))
                self.assertEqual(case, before)

                case['revision'] = 'fm-sharp-client-2026-11-18'
                before = copy.deepcopy(case)
                self.assertIsNone(getattr(MEDIA, name)(**case))
                self.assertEqual(case, before)


if __name__ == '__main__':
    unittest.main(verbosity=2)
