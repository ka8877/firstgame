"""Pure runtime-row schema regression tests; no evidence is changed or executed."""
import copy
import importlib.util
from pathlib import Path
import unittest

SOURCE = Path(__file__).with_name('preserve-checkpoint-proposal.py')
SPEC = importlib.util.spec_from_file_location('client_preservation_source', SOURCE)
PRESERVE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PRESERVE)


class RuntimeIdentityTests(unittest.TestCase):
    def setUp(self):
        self.proof = [
            {'file': 'index.html', 'bytes': 103, 'sha256': 'a' * 64},
            {'file': 'Build/current.data.unityweb', 'bytes': 2048, 'sha256': 'b' * 64},
        ]

    def compare(self, browser):
        expected = PRESERVE.runtime_identity(self.proof)
        PRESERVE.need(len(browser) == len(expected) and PRESERVE.runtime_identity(browser) == expected,
                      'Every browser suite must bind the complete exact successful runtime')

    def test_actual_browser_modified_time_metadata_preserves_exact_byte_identity(self):
        browser = [dict(row, modifiedUtc='2026-11-11T07:35:12.123456+00:00') for row in reversed(self.proof)]
        original = copy.deepcopy(browser)
        self.compare(browser)
        self.assertEqual(browser, original, 'Original metadata must not be rewritten or removed')
        self.compare(copy.deepcopy(self.proof))

    def test_missing_extra_and_duplicate_files_are_rejected(self):
        for rows in (self.proof[:-1], self.proof + [dict(self.proof[0], file='extra.js')],
                     [self.proof[0], self.proof[0]]):
            with self.subTest(files=[row['file'] for row in rows]):
                with self.assertRaises(ValueError):
                    self.compare(rows)

    def test_size_and_sha_mismatches_are_rejected(self):
        for change in ({'bytes': 104}, {'sha256': 'c' * 64}):
            rows = copy.deepcopy(self.proof)
            rows[0].update(change)
            with self.subTest(change=change):
                with self.assertRaises(ValueError):
                    self.compare(rows)

    def test_duplicate_proof_files_are_rejected(self):
        with self.assertRaises(ValueError):
            PRESERVE.runtime_identity([self.proof[0], self.proof[0]])

    def test_invalid_optional_modified_timestamp_is_rejected(self):
        for timestamp in ('not-a-time', '2026-11-11T07:35:12', None, 123):
            rows = copy.deepcopy(self.proof)
            rows[0]['modifiedUtc'] = timestamp
            with self.subTest(timestamp=timestamp):
                with self.assertRaises(ValueError):
                    self.compare(rows)

    def test_malformed_byte_identity_is_rejected(self):
        for change in ({'bytes': True}, {'bytes': -1}, {'sha256': 'short'}, {'file': '../escape'},
                       {'file': '/absolute'}, {'file': 'Build\\escape'}, {'file': 'Build/./alias'}):
            rows = copy.deepcopy(self.proof)
            rows[0].update(change)
            with self.subTest(change=change):
                with self.assertRaises(ValueError):
                    self.compare(rows)


if __name__ == '__main__':
    unittest.main()
