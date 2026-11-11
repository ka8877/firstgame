"""Synthetic isolated source-admission fixtures; no product or approval is run."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import sys
from unittest.mock import patch

validation_path = str(Path(__file__).resolve().parents[1])
if validation_path not in sys.path:
    sys.path.insert(0, validation_path)

from GospelNpr import contract, card_baseline as baseline


class NativeContractTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.target = self.root / 'native-contract.json'
        classes = {
            'GospelNprSmokeTests': 9, 'GospelNativityAudioSmokeTests': 2,
            'GospelNativityMovementAudioSmokeTests': 1, 'GospelNativityStorySmokeTests': 1,
            'GospelScriptureReadingSmokeTests': 2,
        }
        names, rows = [], []
        for class_name, count in classes.items():
            assigned = ['PathOfDawn.SmokeTests.' + class_name + '.FixtureMethod' + str(index) for index in range(count)]
            names.extend(assigned)
            for suffix in ('.cs', '.cs.meta'):
                name = contract.SOURCE_PREFIX + class_name + suffix
                path = self.root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                raw = ('synthetic isolated fixture ' + name + '\n').encode()
                path.write_bytes(raw)
                rows.append({'file': name, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
                             'fullnames': assigned if suffix == '.cs' else []})
        self.value = {'schema': 2, 'game': 'PathOfDawn', 'profile': 'gospel-npr',
            'revision': 'gospel-npr-2026-11-11', 'sourceStatus': 'frozen', 'count': 15,
            'newCaseCount': 9, 'retainedCurrentSourceCaseCount': 6, 'fullnames': names, 'sourceFiles': rows}

    def selected(self, value=None):
        self.target.write_text(json.dumps(self.value if value is None else value))
        with patch.object(contract, 'ROOT', self.root), patch.object(contract, 'NATIVE_CONTRACT', self.target):
            return contract.native_full_names()

    def test_exact15_unique_names_require_all_ten_current_source_originals(self):
        self.assertEqual(self.selected(), tuple(self.value['fullnames']))

    def test_unresolved_legacy_count_duplicate_and_malformed_names_fail_closed(self):
        variants = []
        for key, value in [('schema', 1), ('sourceStatus', 'authored-awaiting-root-selection'),
                           ('count', None), ('count', 14), ('newCaseCount', 8),
                           ('retainedCurrentSourceCaseCount', 7), ('fullnames', None), ('fullnames', [])]:
            row = copy.deepcopy(self.value)
            row[key] = value
            variants.append(row)
        for name in [self.value['fullnames'][1], 'PathOfDawn.Tests.*', ['malformed']]:
            row = copy.deepcopy(self.value)
            row['fullnames'][0] = name
            variants.append(row)
        for index, value in enumerate(variants):
            with self.subTest(index=index), self.assertRaises(ValueError):
                self.selected(value)

    def test_missing_extra_duplicate_and_incorrect_source_identity_fail(self):
        variants = []
        row = copy.deepcopy(self.value); row['sourceFiles'].pop(); variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'].append(copy.deepcopy(row['sourceFiles'][0])); variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'][1] = copy.deepcopy(row['sourceFiles'][0]); variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'][0]['bytes'] += 1; variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'][0]['sha256'] = '0' * 64; variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'][0]['fullnames'].pop(); variants.append(row)
        row = copy.deepcopy(self.value); row['sourceFiles'][1]['fullnames'] = [row['fullnames'][0]]; variants.append(row)
        for index, value in enumerate(variants):
            with self.subTest(index=index), self.assertRaises(ValueError):
                self.selected(value)

    def test_changed_source_bytes_and_symlinks_fail(self):
        source = self.root / self.value['sourceFiles'][0]['file']
        raw = source.read_bytes()
        source.write_bytes(raw + b'changed')
        with self.assertRaises(ValueError):
            self.selected()
        source.write_bytes(raw)
        alias = self.root / 'synthetic-source-copy'
        alias.write_bytes(raw)
        source.unlink()
        source.symlink_to(alias)
        with self.assertRaises(ValueError):
            self.selected()


class BrowserPlanAdmissionTests(unittest.TestCase):
    def frozen_plan(self):
        return {'sourceStatus': 'frozen', 'evidenceRole': 'current-gospel-npr',
            'requiredCheckpoints': list(contract.NPR_BROWSER_ORIGINALS),
            'steps': [{'type': 'checkpoint', 'name': name} for name in contract.NPR_BROWSER_ORIGINALS],
            'nprRouteAdmission': {'sourceStatus': 'frozen', 'ordinaryControlsOnly': True,
                'requiredNewOriginals': list(contract.NPR_BROWSER_ORIGINALS)}}

    def test_native_selection_does_not_admit_an_unresolved_camera_route(self):
        plan = self.frozen_plan()
        plan['nprRouteAdmission']['sourceStatus'] = 'unresolved-camera-relative-route'
        with patch.object(contract, 'native_full_names') as native:
            with self.assertRaises(ValueError):
                contract.require_browser_plan(plan, 'PathOfDawn')
            native.assert_not_called()

    def test_missing_new3d_checkpoint_or_wrong_role_is_rejected(self):
        for change in ('checkpoint', 'role', 'draft'):
            plan = self.frozen_plan()
            if change == 'checkpoint':
                plan['steps'].pop()
            elif change == 'role':
                plan['evidenceRole'] = 'preserved-card-regression'
            else:
                plan['sourceStatus'] = 'draft-awaiting-actual-geometry'
            with self.subTest(change=change), self.assertRaises(ValueError):
                contract.require_browser_plan(plan, 'PathOfDawn')

    def test_frozen_gospel_also_requires_the_admitted_native_source_contract(self):
        with patch.object(contract, 'native_full_names', side_effect=ValueError('native pending')):
            with self.assertRaises(ValueError):
                contract.require_browser_plan(self.frozen_plan(), 'PathOfDawn')
        with patch.object(contract, 'native_full_names', return_value=('fixture',)) as native:
            contract.require_browser_plan(self.frozen_plan(), 'PathOfDawn')
            native.assert_called_once_with()

    def test_preserved_card_uses_its_explicit_role_without_claiming_new_native(self):
        with patch.object(contract, 'native_full_names') as native:
            contract.require_browser_plan({'sourceStatus': 'frozen', 'evidenceRole': 'preserved-card-regression'},
                                          'FantasyCards')
            native.assert_not_called()


class PreservedCardTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.tree = self.root / 'card'
        self.tree.mkdir()
        self.file('card/index.html', b'unchanged card runtime')
        self.file('hero.png', b'unchanged old hero')
        self.inputs = {'game': 'FantasyCards', 'fingerprint': baseline.BASE_CARD_INPUTS,
                       'files': [{'file': 'Games/FantasyCards/Assets/real.cs', 'bytes': 3, 'sha256': 'a' * 64}]}
        self.inputs_ref = self.json('inputs.json', self.inputs)
        self.evidence_ref = self.file('evidence.txt', ('Result: Succeeded\nRevision: ' + baseline.REVISION +
            '\nSourceFingerprint: ' + baseline.BASE_CARD_INPUTS + '\nTarget: WebGL\nErrors: 0\n').encode())
        runtime = baseline.ref(self.root, self.tree / 'index.html')
        self.row = dict(runtime, file='index.html')
        self.proof_ref = self.json('proof.json', {'game': 'FantasyCards', 'revision': baseline.REVISION,
            'status': 'preserved', 'sourceFingerprint': baseline.BASE_CARD_INPUTS, 'reportedBuildExitCode': 0,
            'runtimeByteIdentityBeforeAfter': True, 'completeRuntimeFiles': [self.row]})
        manifest = {'sourceCommit': baseline.BASE_SOURCE, 'gameRevisions': {'FantasyCards': baseline.REVISION},
            'files': {'games/fantasy-cards/index.html': runtime['sha256'], 'art/fantasy-cards.png': baseline.ref(self.root, 'hero.png')['sha256']}}
        self.manifest_ref = self.json('manifest.json', manifest)
        portal = {'sourceCommit': baseline.BASE_SOURCE, 'manifest': {'sha256': self.manifest_ref['sha256']}, 'files': []}
        for name, digest in manifest['files'].items():
            actual = runtime if name.startswith('games/') else baseline.ref(self.root, 'hero.png')
            portal['files'].append({'file': name, 'sha256': digest, 'expectedSha256': digest,
                'bytes': actual['bytes'], 'expectedBytes': actual['bytes'], 'status': 200, 'passed': True})
        self.portal_ref = self.json('portal.json', portal)
        seal = {'status': 'passed', 'releaseProfile': 'client-motion', 'check': 'all', 'evidence': {
            'portal': {'manifest': self.manifest_ref, 'report': self.portal_ref}, 'builds': {'games': {'FantasyCards': {
                'native': {'sourceFingerprint': baseline.BASE_CARD_INPUTS}, 'inputs': self.inputs_ref,
                'evidence': self.evidence_ref, 'proof': self.proof_ref}}}}}
        self.record = {'schema': 1, 'sourceStatus': 'frozen', 'sourceCommit': baseline.BASE_SOURCE,
            'publishedCommit': baseline.BASE_PUBLIC, 'manifest': self.manifest_ref, 'cardBuildInputs': self.inputs_ref,
            'cardBuildEvidence': self.evidence_ref, 'cardBuildProof': self.proof_ref,
            'historicalFinalSeal': self.json('seal.json', seal), 'historicalPortal': self.portal_ref,
            'cardTreeRoot': 'card', 'cardHero': baseline.ref(self.root, 'hero.png')}

    def file(self, name, content):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
        return baseline.ref(self.root, path)

    def json(self, name, value):
        return self.file(name, (json.dumps(value) + '\n').encode())

    def run_baseline(self, current=None):
        self.json('record.json', self.record)
        with patch.object(baseline, 'BASE_MANIFEST', self.manifest_ref['sha256']), \
             patch.object(baseline, 'BASE_SEAL', self.record['historicalFinalSeal']['sha256']), \
             patch.object(baseline, 'BASE_PORTAL', self.portal_ref['sha256']), \
             patch.object(baseline, 'BASE_PROOF', self.proof_ref['sha256']):
            return baseline.verify_card_baseline(self.root / 'record.json', root=self.root,
                capture_current=lambda game: copy.deepcopy(current or self.inputs))

    def test_identical_product_is_separate_from_new_build_or_native(self):
        result = self.run_baseline()
        self.assertFalse(result['freshCardBuildExecuted'])
        self.assertFalse(result['freshCardNativeExecuted'])
        self.assertEqual(result['runtimeFiles'], {'index.html': self.row})

    def test_changed_current_source_missing_extra_and_changed_bytes_are_rejected(self):
        changed = copy.deepcopy(self.inputs)
        changed['files'][0]['sha256'] = 'b' * 64
        with self.assertRaises(ValueError):
            self.run_baseline(changed)
        original = (self.tree / 'index.html').read_bytes()
        (self.tree / 'index.html').write_bytes(b'changed runtime')
        with self.assertRaises(ValueError):
            self.run_baseline()
        (self.tree / 'index.html').unlink()
        with self.assertRaises(ValueError):
            self.run_baseline()
        (self.tree / 'index.html').write_bytes(original)
        self.file('card/extra.js', b'extra')
        with self.assertRaises(ValueError):
            self.run_baseline()

    def test_symlink_in_protected_tree_is_rejected(self):
        (self.tree / 'alias').symlink_to(self.tree / 'index.html')
        with self.assertRaises(ValueError):
            self.run_baseline()


if __name__ == '__main__':
    unittest.main()

