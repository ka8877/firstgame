"""CLI admission regressions. Forbidden Card paths must fail before any run."""
from pathlib import Path
import os
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[3]


class NprScopeTests(unittest.TestCase):
    def reject(self, script, *arguments):
        label = 'UnitForbiddenCardAdmission'
        locations = [
            ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11/NativeRuns/FantasyCards' / label,
            ROOT / 'Artifacts/Validation/GospelNpr-2026-11-11/WebRuns/FantasyCards' / label,
        ]
        self.assertTrue(all(not path.exists() for path in locations), 'Use isolated unused test labels')
        environment = dict(os.environ, FIRSTGAME_RELEASE_PROFILE='gospel-npr',
                           PYTHONDONTWRITEBYTECODE='1', PYTHONOPTIMIZE='0')
        result = subprocess.run([sys.executable, str(ROOT / script), *arguments],
                                cwd=ROOT, env=environment, capture_output=True, text=True, timeout=15)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('NPR preserves Card', result.stderr)
        self.assertTrue(all(not path.exists() for path in locations), 'Forbidden admission created a run')

    def test_card_native_is_rejected_before_contract_or_restore(self):
        self.reject('Tools/Validation/run-canon-native.py', 'FantasyCards', 'UnitForbiddenCardAdmission')

    def test_card_web_is_rejected_before_native_selection_or_build(self):
        self.reject('Tools/Validation/run-canon-web.py', 'FantasyCards', 'UnitForbiddenCardAdmission',
                    '--native-run', str(ROOT / 'does-not-exist'))


if __name__ == '__main__':
    unittest.main()

