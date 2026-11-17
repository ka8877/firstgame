"""Synthetic contract regressions only; Root executes under the owned wrapper."""
import copy
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('sharp_illustration_contract', Path(__file__).with_name('contract.py'))
CONTRACT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONTRACT)


class ActualBackingClarityAdmission(unittest.TestCase):
    def fixture(self, kind='mobile'):
        scale = 1 if kind == 'desktop' else 2
        plan = {'requiredCheckpoints': ['speaker', 'continued'], 'renderingClarity': {
            'deviceScaleFactor': scale, 'minimumBackingScale': scale,
            'maxBackingPixelError': 2, 'requiredNavigations': [0, 1, 2]}}
        samples = []
        for nav in (0, 1, 2):
            samples.append({'shippingCanvas': True, 'ready': 'ready', 'devicePixelRatio': scale,
                'css': {'width': 800, 'height': 450}, 'backing': {'width': 800 * scale, 'height': 450 * scale},
                'drawingBuffer': {'width': 800 * scale, 'height': 450 * scale}, 'contextLost': False,
                'matchWebGLToCanvasSize': True, 'navigation': nav, 'reason': 'loaded'})
        for name in plan['requiredCheckpoints']:
            samples.append({**copy.deepcopy(samples[-1]), 'reason': 'checkpoint', 'checkpoint': name})
        return plan, {'renderingClarity': {'requestedDeviceScaleFactor': scale, 'samples': samples}}

    def reject(self, plan, report, kind='mobile'):
        with self.assertRaises(ValueError):
            CONTRACT.require_browser_clarity(report, plan, expected_kind=kind)

    def test_desktop_one_and_touch_public_two_require_measured_buffers(self):
        for kind in ('desktop', 'mobile', 'public'):
            plan, report = self.fixture(kind)
            self.assertIs(CONTRACT.require_browser_clarity(report, plan, expected_kind=kind), report['renderingClarity'])

    def test_dpr_two_png_or_configuration_does_not_excuse_one_times_backing(self):
        plan, report = self.fixture()
        for sample in report['renderingClarity']['samples']:
            sample['configuredDevicePixelRatio'] = 2
            sample['backing'] = sample['drawingBuffer'] = {'width': 800, 'height': 450}
        self.reject(plan, report)

    def test_old_touch_one_point_two_five_cap_is_rejected(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['backing'] = {'width': 1000, 'height': 563}
        report['renderingClarity']['samples'][0]['drawingBuffer'] = {'width': 1000, 'height': 563}
        self.reject(plan, report)

    def test_actual_browser_scale_must_match_requested_scale(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['devicePixelRatio'] = 1
        self.reject(plan, report)

    def test_drawing_buffer_cannot_be_smaller_than_declared_canvas(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['drawingBuffer']['width'] -= 1
        self.reject(plan, report)

    def test_context_loss_is_not_a_clarity_pass(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['contextLost'] = True
        self.reject(plan, report)

    def test_unrelated_canvas_cannot_substitute_for_shipping_canvas(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['shippingCanvas'] = False
        self.reject(plan, report)

    def test_both_real_reload_measurements_are_required(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'] = [s for s in report['renderingClarity']['samples'] if not (s['reason'] == 'loaded' and s['navigation'] == 2)]
        self.reject(plan, report)

    def test_every_required_gameplay_original_needs_its_own_measurement(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'] = [s for s in report['renderingClarity']['samples'] if s.get('checkpoint') != 'speaker']
        self.reject(plan, report)

    def test_nonfinite_css_dimensions_are_rejected(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['css']['width'] = float('nan')
        self.reject(plan, report)

    def test_disabled_canvas_matching_is_rejected(self):
        plan, report = self.fixture()
        report['renderingClarity']['samples'][0]['matchWebGLToCanvasSize'] = False
        self.reject(plan, report)

    def test_absent_canvas_matching_observation_is_rejected(self):
        plan, report = self.fixture()
        del report['renderingClarity']['samples'][0]['matchWebGLToCanvasSize']
        self.reject(plan, report)

    def test_checkpoint_name_on_unrelated_loaded_sample_is_not_checkpoint_evidence(self):
        plan, report = self.fixture()
        for sample in report['renderingClarity']['samples']:
            if sample.get('checkpoint') == 'speaker':
                sample['reason'] = 'loaded'
        self.reject(plan, report)

    def test_plan_cannot_lower_touch_backing_requirement_to_one(self):
        plan, report = self.fixture()
        plan['renderingClarity']['minimumBackingScale'] = 1
        self.reject(plan, report)


if __name__ == '__main__':
    unittest.main()
