"""Source-only regression proposal; Root alone may execute it after review.

All fixtures are synthetic and confined to TemporaryDirectory. They are never
written to production OUT/CONTROL and never represent actual ownership/results.
No subprocess, network, Git, acquire, release, browser or game is invoked.
"""
import ast
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SOURCE = Path(__file__).with_name('preserve-two-lease-proposal.py')
SPEC = importlib.util.spec_from_file_location('two_lease_preserver_proposal', SOURCE)
P = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(P)


class TwoLeasePreserverTests(unittest.TestCase):
    def write(self, path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
        return path

    def acquire(self, control, owner, commit, start, end):
        return {'state': 'acquired', 'commitSha': commit * 40, 'lease': {
            'schema': 1, 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock',
            'runId': control.name, 'owner': owner, 'baseMain': 'a' * 40,
            'startedUtc': start, 'deadlineUtc': end}}

    def make_run(self, control, acquisition, label, start='2026-11-11T01:10:00+00:00',
            kind='generic', game='PathOfDawn', profile='PublicMobile844'):
        # This is a synthetic on-disk wrapper fixture, not an executed command.
        base = (control / 'CommandRuns' / label if kind == 'generic' else
                self.out / 'BrowserRuns' / game / profile / label if kind == 'browser' else
                self.out / 'WebRuns' / game / label)
        contained = (control / 'ContainedCommands' / label if kind == 'generic' else
                     control / 'ContainedBrowser' / game / profile / label if kind == 'browser' else
                     control / 'ContainedWeb' / game / label)
        snapshot = self.write(base / 'owned-lease.executed.json', acquisition)
        recorded = base / 'RecordedHarness/source.py'
        recorded.parent.mkdir(parents=True, exist_ok=True)
        recorded.write_bytes(self.source.read_bytes())
        scope = {'generic': 'generic-owned-command-only', 'browser': 'current-canon-browser-only',
                 'web': 'current-canon-web-build-only'}[kind]
        invocation = {'scope': scope, 'label': label, 'game': game, 'profile': profile, 'attempt': label,
            'liveLeasePath': str(control / 'lease.json'), 'ownedLeaseSnapshot': P.reference(snapshot),
            'requestedArgv': ['/synthetic/python', 'Tools/Environment/assert-runtime-idle.py'],
            'executionSources': [{'source': P.reference(self.source), 'recorded': P.reference(recorded)}]}
        if kind == 'browser': invocation.pop('liveLeasePath')
        invocation_path = self.write(base / 'invocation.json', invocation)
        summary = {'scope': scope, 'status': 'command-completed' if kind == 'generic' else 'passed',
            'label': label, 'game': game, 'profile': profile, 'attempt': label,
            'invocation': P.reference(invocation_path), 'finishedUtc': start}
        keys = {'generic': ('guardBefore', 'idleBefore', 'command', 'guardAfter'),
                'browser': ('guardBefore', 'idleBefore', 'execution', 'guardAfter'),
                'web': ('guardBefore', 'idleBefore', 'build', 'proof', 'guardAfter')}[kind]
        for key in keys:
            argv = (['/synthetic/python', 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(control / 'lease.json')]
                    if key in ('guardBefore', 'guardAfter') else ['/synthetic/python', 'synthetic-only'])
            log = base / (key + '.stdout.log')
            log.parent.mkdir(parents=True, exist_ok=True)
            log.write_text('synthetic unit fixture only\n')
            receipt_path = contained / key / 'supervisor-result.json'
            receipt = {'helperSha256': P.HELPER_SHA, 'mode': 'production-caller', 'argv': argv,
                'outcomeExitCode': 0, 'root': {'exitCode': 0}, 'cleanup': {
                    'complete': True, 'kernelEchild': True, 'heldPidfdsExited': True,
                    'subreaperVerifiedAtCompletion': True, 'liveOwnedIdentities': [], 'violations': []}}
            self.write(receipt_path, receipt)
            self.write(receipt_path.parent / 'command-spec.json', {'argv': argv, 'cwd': str(self.root),
                'stdoutPath': str(log), 'helperSha256': P.HELPER_SHA})
            event = {'argv': argv, 'exitCode': 0, 'containmentProvedEmpty': True, 'supervisorStillAlive': False,
                'processContainment': 'owned-descendants-v1', 'containmentReceipt': P.reference(receipt_path),
                'startedUtc': start, 'finishedUtc': start, 'log': P.reference(log)}
            event_file = self.write(base / 'Processes' / (key + '.finished.json'), event)
            summary[key] = {'event': event, 'receipt': P.reference(event_file), 'stdout': P.reference(log)}
        return self.write(base / 'summary.json', summary)

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.old = self.root / 'Artifacts/Validation/Autonomy/ClientMotion-2026-11-11'
        self.new = self.root / 'Artifacts/Validation/Autonomy/ClientMotionResume-2026-11-11'
        self.out = self.root / 'Artifacts/Validation/ClientMotion-2026-11-11'
        self.source = self.root / 'Tools/synthetic_source.py'
        self.source.parent.mkdir(parents=True)
        self.source.write_text('# synthetic source fixture, never executed\n')
        self.patches = patch.multiple(P, ROOT=self.root, ORIGIN_CONTROL=self.old, ACTIVE_CONTROL=self.new,
            CONTROL=self.new, CONTROL_ROOTS=(self.old, self.new), OUT=self.out,
            PROOF_ROOT=self.old / 'BuildProof', REVIEW_ROOT=self.old / 'Root', LEASE_CHAIN=None)
        self.patches.start()
        self.addCleanup(self.patches.stop)
        self.old_acq = self.acquire(self.old, '11111111-1111-4111-8111-111111111111', '1',
            '2026-11-11T01:00:00+00:00', '2026-11-11T01:45:00+00:00')
        self.new_acq = self.acquire(self.new, '22222222-2222-4222-8222-222222222222', '2',
            '2026-11-11T01:31:00+00:00', '2026-11-11T02:16:00+00:00')
        self.old_idle = self.make_run(self.old, self.old_acq, 'FinalIdle', '2026-11-11T01:25:00+00:00')
        self.new_idle = self.make_run(self.new, self.new_acq, 'ResumeIdle', '2026-11-11T01:32:00+00:00')
        released = copy.deepcopy(self.old_acq)
        released.update(state='released', releasedUtc='2026-11-11T01:30:00+00:00')
        release = self.write(self.old / 'lease.json', released)
        stdout = self.write(self.old / 'Release/release.json', released)
        status = self.write(self.old / 'Release/status.json', {
            'status': 'available', 'repository': 'ka8877/firstgame', 'ref': 'refs/heads/codex/autonomy-lock'})
        self.write(self.new / 'lease.json', self.new_acq)
        self.facts = {'leaseChain': {'schema': 1,
            'origin': {'controlRoot': str(self.old.relative_to(self.root)),
                'acquiredLease': P.reference(self.old_idle.parent / 'owned-lease.executed.json'),
                'releasedLease': P.reference(release), 'releaseStdout': P.reference(stdout),
                'statusStdout': P.reference(status), 'finalIdleWrapper': P.reference(self.old_idle)},
            'active': {'controlRoot': str(self.new.relative_to(self.root)),
                'acquiredLease': P.reference(self.new_idle.parent / 'owned-lease.executed.json')}}}

    def invocation(self, summary):
        return P.read(P.checked(P.read(summary)['invocation']))

    def chain(self):
        return P.configure_chain(self.facts, 'checkpoint')

    def test_real_shaped_two_cycle_chain_and_mixed_old_web_new_browser_are_admitted(self):
        self.chain()
        old_web = self.make_run(self.old, self.old_acq, 'Web01', kind='web')
        new_browser = self.make_run(self.new, self.new_acq, 'Public02', '2026-11-11T01:40:00+00:00', kind='browser')
        self.assertEqual(P.completed_wrapper(old_web, 'web', 'PathOfDawn')[2], self.old_acq)
        self.assertEqual(P.completed_wrapper(new_browser, 'browser', 'PathOfDawn', 'PublicMobile844')[2], self.new_acq)

    def test_execution_owner_run_and_commit_substitutions_are_rejected_independently(self):
        self.chain()
        for field, value in (('owner', '33333333-3333-4333-8333-333333333333'), ('runId', self.old.name), ('commitSha', '3'*40)):
            with self.subTest(field=field):
                run = self.make_run(self.new, self.new_acq, 'Changed' + field, '2026-11-11T01:40:00+00:00')
                inv = self.invocation(run)
                changed = copy.deepcopy(self.new_acq)
                (changed if field == 'commitSha' else changed['lease'])[field] = value
                path = self.write(run.parent / 'owned-lease.executed.json', changed)
                inv['ownedLeaseSnapshot'] = P.reference(path)
                with self.assertRaises(ValueError): P.cycle_for_invocation(inv)

    def test_similar_control_prefix_is_not_an_admitted_live_lease(self):
        self.chain()
        inv = self.invocation(self.new_idle)
        inv['liveLeasePath'] = str(self.new.with_name(self.new.name + '-evil') / 'lease.json')
        with self.assertRaises(ValueError): P.cycle_for_invocation(inv)

    def test_third_control_in_facts_is_rejected(self):
        self.facts['leaseChain']['active']['controlRoot'] += '-evil'
        with self.assertRaises(ValueError): self.chain()

    def test_narrative_permission_cannot_replace_exact_chain_originals(self):
        self.facts['leaseChain']['origin']['continuationApproved'] = True
        with self.assertRaises(ValueError): self.chain()

    def test_missing_live_path_is_allowed_only_for_actual_browser_schema(self):
        self.chain(); inv = self.invocation(self.new_idle); inv.pop('liveLeasePath')
        with self.assertRaises(ValueError): P.cycle_for_invocation(inv)

    def test_same_owner_cannot_be_relabelled_as_a_new_cycle(self):
        changed = copy.deepcopy(self.new_acq)
        changed['lease']['owner'] = self.old_acq['lease']['owner']
        snapshot = self.write(self.new_idle.parent / 'owned-lease.executed.json', changed)
        inv = self.invocation(self.new_idle); inv['ownedLeaseSnapshot'] = P.reference(snapshot)
        self.write(self.new_idle.parent / 'invocation.json', inv)
        self.facts['leaseChain']['active']['acquiredLease'] = P.reference(snapshot)
        self.write(self.new / 'lease.json', changed)
        with self.assertRaises(ValueError): self.chain()

    def test_release_may_not_change_owner_commit_or_deadline(self):
        for field in ('owner', 'commitSha', 'deadlineUtc'):
            with self.subTest(field=field):
                released = P.read(self.old / 'lease.json')
                if field == 'commitSha': released[field] = '3'*40
                else: released['lease'][field] = ('33333333-3333-4333-8333-333333333333' if field == 'owner' else '2026-11-11T03:00:00+00:00')
                with self.assertRaises(ValueError): P.released_identity(released, self.old_acq)

    def test_release_stdout_must_equal_actual_released_record(self):
        path = self.write(self.old / 'Release/release.json', {'state': 'released'})
        self.facts['leaseChain']['origin']['releaseStdout'] = P.reference(path)
        with self.assertRaises(ValueError): self.chain()

    def test_new_acquire_before_old_release_is_rejected(self):
        changed = copy.deepcopy(self.new_acq); changed['lease']['startedUtc'] = '2026-11-11T01:29:00+00:00'
        # Keep the allowed 45 minute duration so only chronology is invalid.
        changed['lease']['deadlineUtc'] = '2026-11-11T02:14:00+00:00'
        snapshot = self.write(self.new_idle.parent / 'owned-lease.executed.json', changed)
        inv = self.invocation(self.new_idle); inv['ownedLeaseSnapshot'] = P.reference(snapshot)
        self.write(self.new_idle.parent / 'invocation.json', inv)
        self.facts['leaseChain']['active']['acquiredLease'] = P.reference(snapshot)
        self.write(self.new / 'lease.json', changed)
        with self.assertRaises(ValueError): self.chain()

    def test_old_idle_after_normal_release_is_rejected(self):
        for path in (self.old / 'lease.json', self.old / 'Release/release.json'):
            record = P.read(path); record['releasedUtc'] = '2026-11-11T01:24:00+00:00'; self.write(path, record)
        origin = self.facts['leaseChain']['origin']
        origin['releasedLease'] = P.reference(self.old / 'lease.json')
        origin['releaseStdout'] = P.reference(self.old / 'Release/release.json')
        with self.assertRaises(ValueError): self.chain()

    def test_current_live_acquisition_bytes_may_not_change(self):
        self.write(self.new / 'lease.json', dict(self.new_acq, unexpected=True))
        with self.assertRaises(ValueError): self.chain()

    def test_released_execution_snapshot_is_never_treated_as_acquired(self):
        self.chain(); inv = self.invocation(self.new_idle)
        changed = copy.deepcopy(self.new_acq); changed['state'] = 'released'
        path = self.write(self.new_idle.parent / 'owned-lease.executed.json', changed)
        inv['ownedLeaseSnapshot'] = P.reference(path)
        with self.assertRaises(ValueError): P.cycle_for_invocation(inv)

    def test_origin_cannot_own_final_archive_or_idle(self):
        self.chain()
        with self.assertRaises(ValueError): P.active_invocation(self.invocation(self.old_idle))
        self.assertEqual(P.active_invocation(self.invocation(self.new_idle))['control'], self.new)

    def test_source_change_is_not_hidden_by_a_new_lease(self):
        self.chain(); self.source.write_text('# changed synthetic source\n')
        with self.assertRaises(ValueError): P.completed_wrapper(self.new_idle, 'generic')

    def test_guard_live_path_cannot_be_crossed_between_cycles(self):
        self.chain(); summary = P.read(self.new_idle); row = summary['guardBefore']; event = row['event']
        event['argv'][-1] = str(self.old / 'lease.json')
        receipt_path = P.checked(event['containmentReceipt']); receipt = P.read(receipt_path)
        receipt['argv'] = event['argv']; self.write(receipt_path, receipt)
        spec_path = receipt_path.parent / 'command-spec.json'; spec = P.read(spec_path)
        spec['argv'] = event['argv']; self.write(spec_path, spec)
        event['containmentReceipt'] = P.reference(receipt_path)
        event_path = self.write(self.new_idle.parent / 'Processes/guardBefore.finished.json', event)
        row['receipt'] = P.reference(event_path); self.write(self.new_idle, summary)
        with self.assertRaises(ValueError): P.completed_wrapper(self.new_idle, 'generic')

    def test_browser_without_live_field_still_rejects_cross_cycle_guard(self):
        self.chain()
        run = self.make_run(self.new, self.new_acq, 'BrowserCross', '2026-11-11T01:40:00+00:00', kind='browser')
        self.assertNotIn('liveLeasePath', self.invocation(run))
        summary = P.read(run); row = summary['guardAfter']; event = row['event']
        event['argv'][-1] = str(self.old / 'lease.json')
        receipt_path = P.checked(event['containmentReceipt']); receipt = P.read(receipt_path)
        receipt['argv'] = event['argv']; self.write(receipt_path, receipt)
        spec_path = receipt_path.parent / 'command-spec.json'; spec = P.read(spec_path)
        spec['argv'] = event['argv']; self.write(spec_path, spec)
        event['containmentReceipt'] = P.reference(receipt_path)
        event_path = self.write(run.parent / 'Processes/guardAfter.finished.json', event)
        row['receipt'] = P.reference(event_path); self.write(run, summary)
        with self.assertRaises(ValueError): P.completed_wrapper(run, 'browser', 'PathOfDawn', 'PublicMobile844')

    def test_exact_explicit_six_hour_authorization_does_not_allow_renewal(self):
        record = copy.deepcopy(self.new_acq)
        record['lease']['executionAuthorization'] = {
            'mode': 'explicit-user-authorized', 'userInstruction': '한도없이 해도된다. 무슨한도때문에멈춘거니?',
            'scope': 'One owned firstgame development cycle with a fixed deadline; no renewal, stale-lock takeover, foreign release or background continuation.',
            'maximumSeconds': 21600}
        record['lease']['deadlineUtc'] = '2026-11-11T07:31:00+00:00'
        self.assertEqual(P.acquired_identity(record, self.new), record)
        record['lease']['deadlineUtc'] = '2026-11-11T07:31:01+00:00'
        with self.assertRaises(ValueError): P.acquired_identity(record, self.new)
        record['lease']['deadlineUtc'] = '2026-11-11T07:31:00+00:00'
        record['lease']['executionAuthorization']['scope'] = 'unlimited renewal'
        with self.assertRaises(ValueError): P.acquired_identity(record, self.new)

    def test_event_window_rejects_deadline_and_post_release_execution(self):
        self.chain(); cycle = P.LEASE_CHAIN['origin']
        for time in ('2026-11-11T01:46:00+00:00', '2026-11-11T01:30:01+00:00', '2026-11-11T00:59:59+00:00'):
            with self.subTest(time=time), self.assertRaises(ValueError):
                P.event_window({'startedUtc': time, 'finishedUtc': time}, time, cycle)

    def test_native_embedded_lease_and_guard_must_belong_to_same_cycle(self):
        self.chain()
        guard = {'argv': ['/synthetic/python', str(self.root / 'Tools/Autonomy/cycle_guard.py'), 'check', '--record', str(self.old / 'lease.json')]}
        self.assertEqual(P.native_cycle({'lease': self.old_acq}, guard)['control'], self.old)
        with self.assertRaises(ValueError): P.native_cycle({'lease': self.new_acq}, guard)
        changed = copy.deepcopy(self.old_acq); changed['commitSha'] = 'f'*40
        with self.assertRaises(ValueError): P.native_cycle({'lease': changed}, guard)

    def test_containment_sibling_prefix_is_not_accepted(self):
        self.chain(); event = P.read(self.new_idle)['guardBefore']['event']
        with self.assertRaises(ValueError):
            P.completed_event(event, self.new / 'ContainedCommands' / 'ResumeIdle-evil')

    def test_both_control_trees_collected_and_only_exact_active_archive_excluded(self):
        self.chain()
        old_file = self.old / 'CommandRuns/Archive/raw.txt'; old_file.parent.mkdir(parents=True); old_file.write_text('old raw')
        active_file = self.new / 'CommandRuns/Archive/live.txt'; active_file.parent.mkdir(parents=True); active_file.write_text('active ongoing')
        rows = P.control_originals({self.new / 'CommandRuns/Archive', self.new / 'ContainedCommands/Archive'})
        self.assertIn(old_file, rows); self.assertNotIn(active_file, rows)
        self.assertIn(self.old / 'lease.json', rows); self.assertIn(self.new / 'lease.json', rows)

    def test_active_normal_release_keeps_the_same_acquired_identity(self):
        self.chain(); released = copy.deepcopy(self.new_acq)
        released.update(state='released', releasedUtc='2026-11-11T02:00:00+00:00')
        self.assertEqual(P.released_identity(released, self.new_acq).isoformat(), released['releasedUtc'])
        released['lease']['owner'] = self.old_acq['lease']['owner']
        with self.assertRaises(ValueError): P.released_identity(released, self.new_acq)

    def test_closure_chain_keeps_acquired_snapshots_and_origin_release_frozen(self):
        before = self.chain()
        released = copy.deepcopy(self.new_acq)
        released.update(state='released', releasedUtc='2026-11-11T02:00:00+00:00')
        self.write(self.new / 'lease.json', released)
        self.assertEqual(P.configure_chain(self.facts, 'closure'), before)
        P.released_identity(P.read(self.new / 'lease.json'), self.new_acq)
        changed_origin = P.read(self.old / 'lease.json')
        changed_origin['releasedUtc'] = '2026-11-11T01:30:01+00:00'
        self.write(self.old / 'lease.json', changed_origin)
        with self.assertRaises(ValueError): P.configure_chain(self.facts, 'closure')

    def public_media_guard(self, argv, stdout):
        # Evaluate the actual V3 guard AST, rather than restating its intended
        # condition in a test. Only synthetic stdout is written in this TempDir.
        tree = ast.parse(SOURCE.read_text(encoding='utf-8'))
        matches = [node for node in ast.walk(tree) if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Name) and node.func.id == 'need' and len(node.args) >= 2
            and isinstance(node.args[1], ast.Constant)
            and node.args[1].value == 'Actual unchanged strict Canon/Living/Client public media123 unit results required']
        self.assertEqual(len(matches), 1)
        path = self.root / 'synthetic-public-media.stdout.log'; path.write_bytes(stdout)
        expression = ast.fix_missing_locations(ast.Expression(matches[0].args[0]))
        return eval(compile(expression, str(SOURCE), 'eval'), {
            'unit_invocation': {'requestedArgv': argv}, 'units': {'command': {'stdout': {'syntheticPath': str(path)}}},
            'checked': lambda row: Path(row['syntheticPath']), 're': P.re, 'PUBLIC_MEDIA_TESTS': P.PUBLIC_MEDIA_TESTS})

    def test_actual_public_media_tools_path_and_123_ok_stdout_are_accepted(self):
        self.assertTrue(self.public_media_guard(['python3', 'Tools/Validation/test_fm_public_reload_ranges.py'],
            b'................................................................\nRan 123 tests in 0.123s\n\nOK\n'))

    def test_public_media_basename_alias_extra_args_and_other_suites_are_rejected(self):
        for argv in (['python3', 'test_fm_public_reload_ranges.py'],
                     ['python3', 'Tools/Validation/test_fm_public_reload_ranges.py.bak'],
                     ['python3', '/tmp/Tools/Validation/test_fm_public_reload_ranges.py'],
                     ['python3', 'Tools/Validation/test_two_lease_preserver.py'],
                     ['python3', 'Tools/Validation/test_fm_public_reload_ranges.py', '--filter', 'subset']):
            with self.subTest(argv=argv):
                self.assertFalse(self.public_media_guard(argv, b'Ran 123 tests in 0.123s\n\nOK\n'))

    def test_public_media_exact_path_still_requires_123_completed_ok_results(self):
        argv = ['python3', 'Tools/Validation/test_fm_public_reload_ranges.py']
        for stdout in (b'Ran 24 tests in 0.123s\n\nOK\n', b'Ran 123 tests in 0.123s\n\nFAILED (failures=1)\n',
                       b'Ran 123 tests in 0.123s\n\n', b'planned 123 tests\nOK\n'):
            with self.subTest(stdout=stdout): self.assertFalse(self.public_media_guard(argv, stdout))


if __name__ == '__main__':
    unittest.main()
