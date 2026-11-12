"""Synthetic three-cycle fixtures only; Root alone reviews and executes later.

These temporary receipts are deliberately synthetic. They neither acquire a
lease nor stand in for an actual command, cleanup, release, or product result.
The chain tests mock only completed_wrapper to isolate receipt identity and
chronology at the separate completed-wrapper boundary.
"""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
from datetime import timedelta
import tempfile
import unittest
from unittest.mock import call, patch

SOURCE = Path(__file__).with_name('preserve-gospel-npr-three-cycle-v3-proposal.py')
SPEC = importlib.util.spec_from_file_location('npr_three_cycle_preservation_source_fixture', SOURCE)
P = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(P)


class PreservationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.out = self.root / 'Artifacts/Validation/GospelNpr-2026-11-11'
        self.origin = self.root / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11'
        self.middle = self.root / 'Artifacts/Validation/Autonomy/GospelNprResume-2026-11-11'
        self.control = self.root / 'Artifacts/Validation/Autonomy/GospelNprCameraResume-2026-11-11'
        self.evidence = self.root / 'Docs/Autonomy/Evidence/GospelNpr20261111'
        self.base = self.evidence / 'Checkpoint01'
        self.base.mkdir(parents=True)
        self.attributes = self.evidence / '.gitattributes'
        self.attributes.write_bytes(P.ATTRIBUTES)
        self.patch = patch.multiple(P, ROOT=self.root, OUT=self.out,
            ORIGIN_CONTROL=self.origin, MIDDLE_CONTROL=self.middle,
            ACTIVE_CONTROL=self.control, CONTROL=self.control,
            CONTROL_ROOTS=(self.origin, self.middle, self.control), PROOF_ROOT=self.origin / 'BuildProof',
            REVIEW_ROOT=self.origin / 'Root', EVIDENCE=self.evidence, DEST=self.base,
            LEASE_CHAIN=None)
        self.patch.start()
        self.addCleanup(self.patch.stop)

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def write_json(self, path, value):
        return self.write(path.relative_to(self.root),
            (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode())

    def row(self):
        return {'file': 'Build/runtime', 'bytes': 3, 'sha256': 'a' * 64}

    def parts(self):
        raw = b'Explicit synthetic stream fixture, not actual product evidence'
        path = self.base / 'completed-originals.tar.gz.part01'
        path.write_bytes(raw)
        return {'parts': [dict(P.reference(path), file=path.name)], 'partBytes': P.PART_BYTES,
            'joinedArchiveSha256': hashlib.sha256(raw).hexdigest(), 'joinedBytes': len(raw),
            'portableAttributes': P.reference(self.attributes)}

    def test_observed_mtime_does_not_replace_stable_complete_byte_identity(self):
        row = self.row()
        observed = dict(row, modifiedUtc='2026-11-11T10:00:00+00:00')
        self.assertEqual(P.runtime_identity([row]), P.runtime_identity([observed]))

    def test_duplicate_and_unsafe_runtime_paths_fail(self):
        for rows in ([self.row(), self.row()], [dict(self.row(), file='../escape')]):
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                P.runtime_identity(rows)

    def test_missing_extra_size_and_sha_runtime_changes_are_unequal(self):
        expected = P.runtime_identity([self.row()])
        variants = [[dict(self.row(), bytes=4)], [dict(self.row(), sha256='b' * 64)],
                    [self.row(), dict(self.row(), file='extra')]]
        for rows in variants:
            self.assertNotEqual(P.runtime_identity(rows), expected)
        with self.assertRaises(ValueError):
            P.runtime_identity([])

    def test_exact_parts_and_raw_attributes_rejoin(self):
        P.verify_parts(self.parts(), self.base)

    def test_changed_part_or_joined_hash_is_rejected(self):
        index = self.parts()
        (self.base / index['parts'][0]['file']).write_bytes(b'changed')
        with self.assertRaises(ValueError):
            P.verify_parts(index, self.base)
        index = self.parts()
        index['joinedArchiveSha256'] = '0' * 64
        with self.assertRaises(ValueError):
            P.verify_parts(index, self.base)

    def test_missing_raw_attributes_or_changed_policy_is_rejected(self):
        index = self.parts()
        self.attributes.write_bytes(b'* text=auto\n')
        with self.assertRaises(ValueError):
            P.verify_parts(index, self.base)

    def test_tree_rejects_symlinks_but_retains_failed_raw_originals(self):
        directory = self.out / 'PrototypeFailed'
        raw = self.write(directory.relative_to(self.root) / 'crash.log', b'synthetic failed frame\n')
        self.assertEqual(P.tree_files(directory), {raw})
        (directory / 'alias').symlink_to(raw)
        with self.assertRaises(ValueError):
            P.tree_files(directory)

    def test_failed_history_has_no_fabricated_guard_after_or_success(self):
        directory = self.origin / 'CommandRuns/FailedPrototype'
        self.write(directory.relative_to(self.root) / 'invocation.json', b'{}\n')
        self.write(directory.relative_to(self.root) / 'summary.json', b'{"status":"command-failed"}\n')
        rows = P.retained_history('ArchiveCurrent')
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]['recordedStatus'], 'command-failed')
        self.assertFalse(rows[0]['guardAfterRecorded'])

    def test_only_active_archive_is_excluded_even_when_origin_label_matches(self):
        for control, label in ((self.control, 'ArchiveCurrent'),
                               (self.origin, 'ArchiveCurrent'),
                               (self.middle, 'ArchiveCurrent'),
                               (self.control, 'OtherInterrupted')):
            self.write((control / 'CommandRuns' / label).relative_to(self.root) / 'invocation.json', b'{}\n')
        rows = P.retained_history('ArchiveCurrent')
        self.assertEqual(len(rows), 3)
        self.assertEqual({row['invocation']['file'] for row in rows}, {
            str((self.origin / 'CommandRuns/ArchiveCurrent/invocation.json').relative_to(self.root)),
            str((self.middle / 'CommandRuns/ArchiveCurrent/invocation.json').relative_to(self.root)),
            str((self.control / 'CommandRuns/OtherInterrupted/invocation.json').relative_to(self.root))})
        self.assertTrue(all(row['recordedStatus'] is None for row in rows))

    def test_single_lease_identity_cannot_substitute_for_three_cycle_facts(self):
        with self.assertRaises(ValueError):
            P.configure_chain({'leaseIdentity': {'schema': 1,
                'controlRoot': str(self.control.relative_to(self.root)), 'acquiredLease': {}}}, 'checkpoint')


class ThreeCycleChainTests(unittest.TestCase):
    """Real adapter functions over synthetic temporary byte-bound originals."""
    write = PreservationTests.write
    write_json = PreservationTests.write_json

    def setUp(self):
        # Reuse fixture setup without inheriting the ten history/parts tests:
        # this class adds synthetic chain files that those tests do not expect.
        PreservationTests.setUp(self)
        self.old = self.acquired(self.origin, '11111111-1111-4111-8111-111111111111',
            '1' * 40, '2026-11-11T10:00:00+00:00', '2026-11-11T16:00:00+00:00')
        self.middle_lease = self.acquired(self.middle, '22222222-2222-4222-8222-222222222222',
            '2' * 40, '2026-11-11T15:57:00+00:00', '2026-11-11T21:57:00+00:00')
        self.new = self.acquired(self.control, '33333333-3333-4333-8333-333333333333',
            '3' * 40, '2026-11-11T21:57:00+00:00', '2026-11-12T03:57:00+00:00')
        self.controls = {'origin': self.origin, 'middle': self.middle, 'active': self.control}
        self.acquisitions = {'origin': self.old, 'middle': self.middle_lease, 'active': self.new}
        self.snapshots = {}
        self.invocations = {}
        self.facts = {'leaseChain': {'schema': 1, 'origin': {}, 'middle': {}, 'active': {}}}
        for role in ('origin', 'middle', 'active'):
            self.refresh_acquisition(role)
        self.released = copy.deepcopy(self.old)
        self.released.update(state='released', releasedUtc='2026-11-11T15:55:00+00:00')
        self.middle_released = copy.deepcopy(self.middle_lease)
        self.middle_released.update(state='released', releasedUtc='2026-11-11T21:55:00+00:00')
        self.idle_paths = {}
        for role in ('origin', 'middle'):
            self.refresh_release(role)
            control = self.controls[role]
            status = self.write_json(control / 'Release/status.json', {
                'status': 'available', 'repository': 'ka8877/firstgame',
                'ref': 'refs/heads/codex/autonomy-lock'})
            self.facts['leaseChain'][role]['statusStdout'] = P.reference(status)
            self.idle_paths[role] = self.write_json(control / 'CommandRuns/FinalIdle/summary.json',
                {'syntheticFixture': 'not an actual execution receipt'})
            self.facts['leaseChain'][role]['finalIdleWrapper'] = P.reference(self.idle_paths[role])
        self.idle_path = self.idle_paths['origin']
        self.idle = {'finishedUtc': '2026-11-11T15:54:00+00:00'}
        self.idle_invocation = {'requestedArgv': ['python3', 'Tools/Environment/assert-runtime-idle.py'],
            'environmentOverrides': {'FIRSTGAME_RELEASE_PROFILE': 'gospel-npr'}}
        self.middle_idle = {'finishedUtc': '2026-11-11T21:54:00+00:00'}
        self.middle_idle_invocation = copy.deepcopy(self.idle_invocation)

    def acquired(self, control, owner, commit, started, deadline):
        return {'state': 'acquired', 'commitSha': commit, 'lease': {
            'schema': 1, 'repository': 'ka8877/firstgame',
            'ref': 'refs/heads/codex/autonomy-lock', 'runId': control.name,
            'baseMain': 'a' * 40, 'owner': owner, 'startedUtc': started,
            'deadlineUtc': deadline, 'executionAuthorization': {
                'mode': 'explicit-user-authorized',
                'userInstruction': '한도없이 해도된다. 무슨한도때문에멈춘거니?',
                'scope': 'One owned firstgame development cycle with a fixed deadline; no renewal, stale-lock takeover, foreign release or background continuation.',
                'maximumSeconds': 21600}}}

    def refresh_acquisition(self, role):
        control, acquired = self.controls[role], self.acquisitions[role]
        path = self.write_json(control / 'CommandRuns/AcquisitionSnapshot/owned-lease.executed.json', acquired)
        invocation = {'scope': 'generic-owned-command-only', 'liveLeasePath': str(control / 'lease.json'),
            'ownedLeaseSnapshot': P.reference(path)}
        self.write_json(path.parent / 'invocation.json', invocation)
        self.snapshots[role], self.invocations[role] = path, invocation
        self.facts['leaseChain'][role].update(controlRoot=str(control.relative_to(self.root)),
            acquiredLease=P.reference(path))
        self.write_json(control / 'lease.json', acquired)

    def refresh_release(self, role='origin'):
        control = self.controls[role]
        released = self.released if role == 'origin' else self.middle_released
        live = self.write_json(control / 'lease.json', released)
        stdout = self.write_json(control / 'Release/release.json', released)
        self.facts['leaseChain'][role].update(releasedLease=P.reference(live),
            releaseStdout=P.reference(stdout))

    def admit(self, phase='checkpoint', idle=None, invocation=None, lease=None, historical_role='origin'):
        # Only the completed-wrapper boundary is mocked. All snapshot, path,
        # bytes, UUID, release identity and acquisition chronology checks run.
        def completed_fixture(path, kind, current_sources=True):
            role = next(role for role in ('origin', 'middle') if path == self.idle_paths[role])
            defaults = ((self.idle, self.idle_invocation, self.old) if role == 'origin'
                else (self.middle_idle, self.middle_idle_invocation, self.middle_lease))
            if role != historical_role:
                return defaults
            return (defaults[0] if idle is None else idle,
                defaults[1] if invocation is None else invocation,
                defaults[2] if lease is None else lease)
        with patch.object(P, 'completed_wrapper', side_effect=completed_fixture) as completed:
            result = P.configure_chain(self.facts, phase)
            self.assertEqual(completed.call_args_list, [
                call(self.idle_paths['origin'], 'generic', current_sources=False),
                call(self.idle_paths['middle'], 'generic', current_sources=False)])
            return result

    def test_three_independent_acquisitions_bind_both_releases_and_active_snapshot(self):
        result = self.admit()
        self.assertEqual(set(P.LEASE_CHAIN), {'origin', 'middle', 'active'})
        self.assertEqual(result['originAcquired'], P.reference(self.snapshots['origin']))
        self.assertEqual(result['middleAcquired'], P.reference(self.snapshots['middle']))
        self.assertEqual(result['activeAcquired'], P.reference(self.snapshots['active']))
        self.assertEqual(P.LEASE_CHAIN['origin']['releasedUtc'], P.stamp(self.released['releasedUtc']))
        self.assertEqual(P.LEASE_CHAIN['middle']['releasedUtc'], P.stamp(self.middle_released['releasedUtc']))

    def test_reusing_origin_owner_is_not_a_new_cycle(self):
        self.new['lease']['owner'] = self.old['lease']['owner']
        self.refresh_acquisition('active')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_reusing_origin_lease_commit_is_not_a_new_cycle(self):
        self.new['commitSha'] = self.old['commitSha']
        self.refresh_acquisition('active')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_foreign_control_root_cannot_be_admitted(self):
        self.facts['leaseChain']['active']['controlRoot'] = 'Artifacts/Validation/Autonomy/Foreign'
        with self.assertRaisesRegex(ValueError, 'three exact reviewed NPR control roots'):
            self.admit()

    def test_extra_chain_role_cannot_be_silently_admitted(self):
        self.facts['leaseChain']['fourth'] = {}
        with self.assertRaisesRegex(ValueError, 'exact three-cycle NPR facts'):
            self.admit()

    def test_origin_requires_normal_released_record(self):
        self.released = copy.deepcopy(self.old)
        self.refresh_release()
        with self.assertRaisesRegex(ValueError, 'normal release'):
            self.admit()

    def test_origin_release_cannot_change_acquired_owner_or_deadline(self):
        self.released['lease']['owner'] = '33333333-3333-4333-8333-333333333333'
        self.refresh_release()
        with self.assertRaisesRegex(ValueError, 'Release changed owner'):
            self.admit()

    def test_origin_release_stdout_must_equal_its_original(self):
        stdout = self.write_json(self.origin / 'Release/release.json', self.old)
        self.facts['leaseChain']['origin']['releaseStdout'] = P.reference(stdout)
        with self.assertRaisesRegex(ValueError, 'release stdout and live released record differ'):
            self.admit()

    def test_available_observation_cannot_gain_an_invented_timestamp(self):
        status = self.write_json(self.origin / 'Release/status.json', {
            'status': 'available', 'repository': 'ka8877/firstgame',
            'ref': 'refs/heads/codex/autonomy-lock', 'observedUtc': '2026-11-11T15:56:00+00:00'})
        self.facts['leaseChain']['origin']['statusStdout'] = P.reference(status)
        with self.assertRaisesRegex(ValueError, 'untimestamped post-release available'):
            self.admit()

    def test_active_start_must_follow_middle_normal_release(self):
        self.new['lease'].update(startedUtc='2026-11-11T21:54:00+00:00',
            deadlineUtc='2026-11-12T03:54:00+00:00')
        self.refresh_acquisition('active')
        with self.assertRaisesRegex(ValueError, 'precedes middle normal release'):
            self.admit()

    def test_origin_final_idle_must_finish_before_its_release(self):
        with self.assertRaisesRegex(ValueError, 'completed independent final idle'):
            self.admit(idle={'finishedUtc': '2026-11-11T15:56:00+00:00'})

    def test_origin_final_idle_must_be_the_independent_idle_command(self):
        with self.assertRaisesRegex(ValueError, 'completed independent final idle'):
            self.admit(invocation={'requestedArgv': ['python3', 'other-command.py'],
                'environmentOverrides': {'FIRSTGAME_RELEASE_PROFILE': 'gospel-npr'}})

    def test_origin_final_idle_cannot_belong_to_active_owner(self):
        with self.assertRaisesRegex(ValueError, 'completed independent final idle'):
            self.admit(lease=self.new)

    def test_checkpoint_requires_unchanged_current_active_acquisition(self):
        changed = copy.deepcopy(self.new)
        changed['lease']['owner'] = '44444444-4444-4444-8444-444444444444'
        self.write_json(self.control / 'lease.json', changed)
        with self.assertRaisesRegex(ValueError, 'unchanged active acquisition'):
            self.admit()

    def test_closure_keeps_the_same_chain_after_active_normal_release(self):
        checkpoint = self.admit()
        released_new = copy.deepcopy(self.new)
        released_new.update(state='released', releasedUtc='2026-11-12T03:50:00+00:00')
        self.write_json(self.control / 'lease.json', released_new)
        self.assertEqual(self.admit('closure'), checkpoint)
        # Active release identity/chronology is checked separately by the full
        # closure_preconditions; configure_chain reuses immutable acquisitions.

    def test_each_recorded_cycle_may_own_a_selected_execution(self):
        self.admit()
        for role, control in self.controls.items():
            with self.subTest(role=role):
                cycle = P.cycle_for_invocation(self.invocations[role])
                self.assertEqual(cycle['control'], control)

    def test_execution_live_path_cannot_cross_acquisition_owner(self):
        self.admit()
        crossed = dict(self.invocations['origin'], liveLeasePath=str(self.control / 'lease.json'))
        with self.assertRaisesRegex(ValueError, 'crosses the admitted acquisition'):
            P.cycle_for_invocation(crossed)

    def test_browser_schema_may_omit_live_path_but_generic_cannot(self):
        self.admit()
        browser = {'scope': 'current-canon-browser-only',
            'ownedLeaseSnapshot': P.reference(self.snapshots['origin'])}
        self.assertEqual(P.cycle_for_invocation(browser)['control'], self.origin)
        with self.assertRaisesRegex(ValueError, 'Only the actual browser schema'):
            P.cycle_for_invocation(dict(browser, scope='generic-owned-command-only'))

    def test_archive_cannot_use_origin_invocation_as_active_ownership(self):
        self.admit()
        with self.assertRaisesRegex(ValueError, 'exact active acquisition'):
            P.active_invocation(self.invocations['origin'])
        self.assertEqual(P.active_invocation(self.invocations['active'])['control'], self.control)

    def test_origin_execution_may_not_continue_past_normal_release(self):
        self.admit()
        event = {'startedUtc': '2026-11-11T15:53:00+00:00',
            'finishedUtc': '2026-11-11T15:54:00+00:00'}
        P.event_window(event, '2026-11-11T15:55:00+00:00', P.LEASE_CHAIN['origin'])
        with self.assertRaisesRegex(ValueError, 'continued after its normal release'):
            P.event_window(event, '2026-11-11T15:56:00+00:00', P.LEASE_CHAIN['origin'])

    def test_active_execution_may_not_exceed_its_own_fixed_deadline(self):
        self.admit()
        event = {'startedUtc': '2026-11-12T03:56:00+00:00',
            'finishedUtc': '2026-11-12T03:57:01+00:00'}
        with self.assertRaisesRegex(ValueError, 'outside its actual acquisition window'):
            P.event_window(event, event['finishedUtc'], P.LEASE_CHAIN['active'])

    def test_native_guard_must_bind_its_embedded_acquisition_control(self):
        self.admit()
        for role, acquired in self.acquisitions.items():
            control = self.controls[role]
            guard = {'argv': ['python3', str(self.root / 'Tools/Autonomy/cycle_guard.py'),
                'check', '--record', str(control / 'lease.json')]}
            self.assertEqual(P.native_cycle({'lease': acquired}, guard)['control'], control)
        crossed = {'argv': ['python3', str(self.root / 'Tools/Autonomy/cycle_guard.py'),
            'check', '--record', str(self.control / 'lease.json')]}
        with self.assertRaisesRegex(ValueError, 'Native guard and embedded lease control differ'):
            P.native_cycle({'lease': self.old}, crossed)

    def test_middle_owner_must_be_distinct_from_origin(self):
        self.middle_lease['lease']['owner'] = self.old['lease']['owner']
        self.refresh_acquisition('middle')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_active_owner_must_be_distinct_from_middle(self):
        self.new['lease']['owner'] = self.middle_lease['lease']['owner']
        self.refresh_acquisition('active')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_middle_lease_commit_must_be_distinct_from_origin(self):
        self.middle_lease['commitSha'] = self.old['commitSha']
        self.refresh_acquisition('middle')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_active_lease_commit_must_be_distinct_from_middle(self):
        self.new['commitSha'] = self.middle_lease['commitSha']
        self.refresh_acquisition('active')
        with self.assertRaisesRegex(ValueError, 'unique owners and lease commits'):
            self.admit()

    def test_middle_start_must_follow_origin_normal_release(self):
        self.middle_lease['lease'].update(startedUtc='2026-11-11T15:54:00+00:00',
            deadlineUtc='2026-11-11T21:54:00+00:00')
        self.refresh_acquisition('middle')
        # Keep the released original internally coherent so the ordering gate,
        # rather than a stale release/ref mismatch, rejects this synthetic chain.
        self.middle_released = copy.deepcopy(self.middle_lease)
        self.middle_released.update(state='released', releasedUtc='2026-11-11T21:55:00+00:00')
        self.refresh_release('middle')
        with self.assertRaisesRegex(ValueError, 'Middle acquisition precedes origin normal release'):
            self.admit()

    def test_middle_idle_must_be_the_independent_idle_command(self):
        with self.assertRaisesRegex(ValueError, 'completed independent final idle: middle'):
            self.admit(historical_role='middle', invocation={
                'requestedArgv': ['python3', 'other-command.py'],
                'environmentOverrides': {'FIRSTGAME_RELEASE_PROFILE': 'gospel-npr'}})

    def test_middle_release_cannot_extend_its_acquired_deadline(self):
        self.middle_released['lease']['deadlineUtc'] = '2026-11-11T22:57:00+00:00'
        self.refresh_release('middle')
        with self.assertRaisesRegex(ValueError, 'Release changed owner'):
            self.admit()

    def test_middle_available_status_cannot_gain_an_invented_timestamp(self):
        status = self.write_json(self.middle / 'Release/status.json', {
            'status': 'available', 'repository': 'ka8877/firstgame',
            'ref': 'refs/heads/codex/autonomy-lock', 'observedUtc': '2026-11-11T21:56:00+00:00'})
        self.facts['leaseChain']['middle']['statusStdout'] = P.reference(status)
        with self.assertRaisesRegex(ValueError, 'untimestamped post-release available'):
            self.admit()

    def test_middle_execution_may_not_continue_past_its_release(self):
        self.admit()
        event = {'startedUtc': '2026-11-11T21:53:00+00:00',
            'finishedUtc': '2026-11-11T21:54:00+00:00'}
        P.event_window(event, '2026-11-11T21:55:00+00:00', P.LEASE_CHAIN['middle'])
        with self.assertRaisesRegex(ValueError, 'continued after its normal release'):
            P.event_window(event, '2026-11-11T21:56:00+00:00', P.LEASE_CHAIN['middle'])

    def test_each_acquisition_retains_its_own_six_hour_authorization(self):
        for role, original in self.acquisitions.items():
            with self.subTest(role=role):
                changed = copy.deepcopy(original)
                changed['lease']['deadlineUtc'] = (
                    P.stamp(changed['lease']['startedUtc']) + timedelta(seconds=21601)).isoformat()
                with self.assertRaisesRegex(ValueError, 'deadline exceeds'):
                    P.acquired_identity(changed, self.controls[role])


class HistoricalWrapperSourceTests(unittest.TestCase):
    """Synthetic complete wrappers through real pins/events/cleanup functions.

    Unlike the32 chain-isolation fixtures, these tests never mock
    completed_wrapper. Every receipt is synthetic temporary test data.
    """
    setUp = ThreeCycleChainTests.setUp
    write = PreservationTests.write
    write_json = PreservationTests.write_json
    acquired = ThreeCycleChainTests.acquired
    refresh_acquisition = ThreeCycleChainTests.refresh_acquisition
    refresh_release = ThreeCycleChainTests.refresh_release

    def complete_idle(self, role='origin', label='FinalIdle'):
        control, acquired = self.controls[role], self.acquisitions[role]
        run = control / 'CommandRuns' / label
        snapshot = self.write_json(run / 'owned-lease.executed.json', acquired)
        live_source = self.write('Tools/' + role + '-source-fixture.py', b'# Synthetic recorded source, not product code\n')
        recorded = self.write((run / 'RecordedHarness' / live_source.relative_to(self.root)).relative_to(self.root), live_source.read_bytes())
        command = ['python3', 'Tools/Environment/assert-runtime-idle.py']
        invocation = {'scope': 'generic-owned-command-only', 'label': label,
            'liveLeasePath': str(control / 'lease.json'), 'ownedLeaseSnapshot': P.reference(snapshot),
            'environmentOverrides': {'FIRSTGAME_RELEASE_PROFILE': 'gospel-npr'},
            'requestedArgv': command, 'executedArgv': command,
            'executionSources': [{'source': P.reference(live_source), 'recorded': P.reference(recorded)}],
            'supervisorAcceptance': {}}
        invocation_path = self.write_json(run / 'invocation.json', invocation)
        start = P.stamp({'origin': '2026-11-11T15:50:00+00:00',
            'middle': '2026-11-11T21:50:00+00:00', 'active': '2026-11-11T22:00:00+00:00'}[role])
        summary = {'scope': 'generic-owned-command-only', 'label': label, 'status': 'command-completed',
            'invocation': P.reference(invocation_path), 'finishedUtc': (start + timedelta(seconds=8)).isoformat()}
        for number, key in enumerate(('guardBefore', 'idleBefore', 'command', 'guardAfter')):
            argv = (['python3', 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(control / 'lease.json')]
                if key in ('guardBefore', 'guardAfter') else command)
            log = self.write((run / (key + '.stdout.log')).relative_to(self.root), b'Synthetic completed fixture; not a real execution\n')
            contained = control / 'ContainedCommands' / label / (str(number + 1) + '-' + key)
            containment = self.write_json(contained / 'supervisor-result.json', {
                'helperSha256': P.HELPER_SHA, 'mode': 'production-caller', 'argv': argv,
                'outcomeExitCode': 0, 'root': {'exitCode': 0}, 'cleanup': {
                    'complete': True, 'kernelEchild': True, 'heldPidfdsExited': True,
                    'subreaperVerifiedAtCompletion': True, 'liveOwnedIdentities': [], 'violations': []}})
            self.write_json(contained / 'command-spec.json', {'argv': argv, 'cwd': str(self.root),
                'helperSha256': P.HELPER_SHA, 'stdoutPath': str(log)})
            event = {'argv': argv, 'cwd': str(self.root), 'exitCode': 0,
                'containmentProvedEmpty': True, 'supervisorStillAlive': False,
                'processContainment': 'owned-descendants-v1', 'containmentReceipt': P.reference(containment),
                'startedUtc': (start + timedelta(seconds=number * 2)).isoformat(),
                'finishedUtc': (start + timedelta(seconds=number * 2 + 1)).isoformat(), 'log': P.reference(log)}
            receipt = self.write_json(run / 'Processes' / (key + '.finished.json'), event)
            summary[key] = {'event': event, 'receipt': P.reference(receipt), 'stdout': P.reference(log)}
        wrapper = self.write_json(run / 'summary.json', summary)
        if role in ('origin', 'middle'):
            self.idle_paths[role] = wrapper
            self.facts['leaseChain'][role]['finalIdleWrapper'] = P.reference(wrapper)
            if role == 'origin': self.idle_path = wrapper
        return wrapper, live_source, recorded

    def complete_histories(self):
        # Rebuilding both fixtures for each subtest also resets any intentional
        # prior corruption, so a middle failure cannot be an origin failure.
        return {role: self.complete_idle(role) for role in ('origin', 'middle')}

    def update_summary(self, path, value):
        self.write_json(path, value)
        for role in ('origin', 'middle'):
            if path == self.idle_paths[role]:
                self.facts['leaseChain'][role]['finalIdleWrapper'] = P.reference(path)

    def update_event(self, wrapper, key, change):
        summary = P.read(wrapper)
        row = summary[key]
        change(row['event'])
        receipt = self.root / row['receipt']['file']
        self.write_json(receipt, row['event'])
        row['receipt'] = P.reference(receipt)
        self.update_summary(wrapper, summary)

    def update_invocation(self, wrapper, change):
        summary = P.read(wrapper)
        path = self.root / summary['invocation']['file']
        invocation = P.read(path)
        change(invocation)
        self.write_json(path, invocation)
        summary['invocation'] = P.reference(path)
        self.update_summary(wrapper, summary)

    def test_both_historical_wrappers_allow_later_live_source_drift(self):
        histories = self.complete_histories()
        originals = {role: recorded.read_bytes() for role, (_, _, recorded) in histories.items()}
        for _, live_source, _ in histories.values():
            live_source.write_bytes(b'# Synthetic newer authorized source\n')
        result = P.configure_chain(self.facts, 'checkpoint')
        for role, (wrapper, _, recorded) in histories.items():
            with self.subTest(role=role):
                self.assertEqual(result[role + 'FinalIdleWrapper'], P.reference(wrapper))
                self.assertEqual(recorded.read_bytes(), originals[role])
                # Each historical wrapper would still fail current-proof policy.
                with self.assertRaises(ValueError):
                    P.completed_wrapper(wrapper, 'generic')

    def test_either_historical_recorded_copy_tamper_stays_rejected(self):
        for role in ('origin', 'middle'):
            with self.subTest(role=role):
                wrapper, live_source, recorded = self.complete_histories()[role]
                live_source.write_bytes(b'# Synthetic newer source\n')
                recorded.write_bytes(b'# Synthetic corrupted recorded original\n')
                with self.assertRaises(ValueError):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_failed_guard_in_either_history_is_not_completed_idle(self):
        for role in ('origin', 'middle'):
            with self.subTest(role=role):
                wrapper, _, _ = self.complete_histories()[role]
                self.update_event(wrapper, 'guardBefore', lambda event: event.update(exitCode=1))
                with self.assertRaisesRegex(ValueError, 'successful owned cleanup'):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_nonempty_owned_cleanup_in_either_history_is_rejected(self):
        def corrupt_cleanup(event):
            path = self.root / event['containmentReceipt']['file']
            result = P.read(path)
            result['cleanup']['liveOwnedIdentities'] = [{'syntheticFixture': 'not an actual process'}]
            self.write_json(path, result)
            event['containmentReceipt'] = P.reference(path)
        for role in ('origin', 'middle'):
            with self.subTest(role=role):
                wrapper, _, _ = self.complete_histories()[role]
                self.update_event(wrapper, 'guardAfter', corrupt_cleanup)
                with self.assertRaisesRegex(ValueError, 'Owned descendants not proved empty'):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_neither_historical_wrapper_can_borrow_active_acquisition(self):
        def cross_owner(invocation):
            path = self.root / invocation['ownedLeaseSnapshot']['file']
            self.write_json(path, self.new)
            invocation['ownedLeaseSnapshot'] = P.reference(path)
        for role in ('origin', 'middle'):
            with self.subTest(role=role):
                wrapper, _, _ = self.complete_histories()[role]
                self.update_invocation(wrapper, cross_owner)
                with self.assertRaisesRegex(ValueError, 'crosses the admitted acquisition'):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_each_historical_wrapper_must_finish_before_its_normal_release(self):
        for role, late in (('origin', '2026-11-11T15:56:00+00:00'),
                           ('middle', '2026-11-11T21:56:00+00:00')):
            with self.subTest(role=role):
                wrapper, _, _ = self.complete_histories()[role]
                summary = P.read(wrapper)
                summary['finishedUtc'] = late
                self.update_summary(wrapper, summary)
                with self.assertRaisesRegex(ValueError, 'continued after its normal release'):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_both_histories_retain_explicit_npr_environment_requirement(self):
        for role in ('origin', 'middle'):
            with self.subTest(role=role):
                wrapper, _, _ = self.complete_histories()[role]
                self.update_invocation(wrapper, lambda invocation:
                    invocation['environmentOverrides'].update(FIRSTGAME_RELEASE_PROFILE='other'))
                with self.assertRaisesRegex(ValueError, 'completed independent final idle: ' + role):
                    P.configure_chain(self.facts, 'checkpoint')

    def test_active_current_wrapper_still_rejects_live_source_drift(self):
        self.complete_histories()
        P.configure_chain(self.facts, 'checkpoint')
        wrapper, live_source, recorded = self.complete_idle('active', 'ActualCurrentIdleFixture')
        P.completed_wrapper(wrapper, 'generic')
        live_source.write_bytes(b'# Synthetic changed current source must fail\n')
        self.assertNotEqual(live_source.read_bytes(), recorded.read_bytes())
        with self.assertRaises(ValueError):
            P.completed_wrapper(wrapper, 'generic')


if __name__ == '__main__':
    unittest.main()
