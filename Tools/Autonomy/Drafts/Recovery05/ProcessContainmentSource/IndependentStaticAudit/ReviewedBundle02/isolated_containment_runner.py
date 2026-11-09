#!/usr/bin/env python3
"""UNEXECUTED proposed nine-case runner. Only the actual owner may run it.

No production Controller is constructed. Every signal target is either the
actual new supervisor or a direct Python dummy launched by this runner, pinned
by a held pidfd plus unchanged startTicks/UID. No name/PGID/session kill exists.
"""
import sys
sys.dont_write_bytecode = True
from pathlib import Path
from datetime import datetime, timedelta, timezone
import argparse
import hashlib
import json
import os
import signal
import subprocess
import threading
import time
import traceback
import types
import uuid


REQUIRED_CASES = ('normal-exit', 'nonzero-exit', 'setsid-timeout', 'doublefork-timeout',
                  'root-exit-orphan', 'ignore-term', 'interrupt', 'bystander', 'stale-identity')
COMMANDS = {
    'normal-exit': ('normal', 0), 'nonzero-exit': ('exit-seven', 7),
    'setsid-timeout': ('spawn-setsid', 124), 'doublefork-timeout': ('doublefork', 124),
    'root-exit-orphan': ('parent-exit-orphan', 0),
    'ignore-term': ('spawn-ignore-term', 124), 'interrupt': ('sleep', 130),
    'bystander': ('spawn-setsid', 124),
}
LOAD_POLICY = 'compile-exec-exact-hashed-bytes-no-pyc'


def need(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def regular(path):
    path = Path(path)
    return path.is_file() and not any(p.is_symlink() for p in (path, *path.parents))


def canonical(value):
    path = Path(value)
    need(path.is_absolute() and str(path) == os.path.normpath(str(path)) and
         '\\' not in str(path) and '\0' not in str(path), 'Canonical absolute path required')
    need(not any(p.is_symlink() for p in (path, *path.parents)), 'Symlinks are forbidden')
    return path


def raw_digest(raw):
    return hashlib.sha256(raw).hexdigest()


def fullref(path, repo):
    path = Path(path)
    need(regular(path), 'Missing/nonregular original: ' + str(path))
    raw = path.read_bytes()
    return {'file': str(path.relative_to(repo)), 'sha256': raw_digest(raw), 'bytes': len(raw)}


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x') as stream:
        json.dump(value, stream, sort_keys=True, indent=2)
        stream.write('\n')


def atomic_json(path, value):
    path = Path(path)
    temporary = path.with_name('.' + path.name + '.pending')
    write_json(temporary, value)
    os.link(temporary, path)
    temporary.unlink()


def read_events(path):
    if not path.exists():
        return []
    rows = []
    for line in path.read_text().splitlines():
        if line:
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                # A writer may be between write() and close(); post-run reads
                # use strict parsing instead of this observation-only helper.
                continue
    return rows


def alive(helper, identity, fd):
    return (not helper.pidfd_exited(fd) and
            helper.identity_key(helper.process_identity(identity['pid'])) == helper.identity_key(identity))


class Lane:
    def __init__(self, controller, cap):
        self.controller = controller
        self.events, self.logs, self.run = [], [], None
        self.env = dict(os.environ)
        self.env.update(PYTHONDONTWRITEBYTECODE='1', PYTHONUNBUFFERED='1')
        # The case execution cap stays .5..2 seconds. The shared Lane envelope
        # also contains cleanup10 + outer-report2, plus three seconds of setup
        # margin; Controller deducts that reserve before admitting a command.
        shared_seconds = float(cap) + 12 + 3
        self.safe_end = time.monotonic() + shared_seconds
        self.deadline = datetime.now(timezone.utc) + timedelta(seconds=shared_seconds + 60)

    def remaining(self):
        return max(0., self.safe_end - time.monotonic())

    def child(self, label, argv, cap, log):
        return self.controller.child(self, label, argv, cap=cap, log=log)


class Observer(threading.Thread):
    """Observe real ready files while Controller.child owns its exact waits."""
    def __init__(self, helper, name, folder, token, containment, parent, cap, interrupt_phase=None):
        super().__init__(name='isolated-ready-' + name, daemon=False)
        self.helper, self.name_case, self.folder, self.token = helper, name, folder, token
        self.containment, self.parent = containment, parent
        self.interrupt_phase = interrupt_phase
        self.stop_requested = threading.Event()
        self.end = time.monotonic() + cap + 2
        self.observation = None
        self.error = None

    def run(self):
        fd, escaped_fd = None, None
        try:
            ready_path = self.folder / 'escape-ready.json'
            while not self.stop_requested.is_set() and time.monotonic() < self.end:
                if ready_path.exists():
                    break
                self.stop_requested.wait(.005)
            need(ready_path.exists(), 'No actual fixture readiness before teardown')
            ready = json.loads(ready_path.read_bytes())
            need(ready.get('token') == self.token, 'Ready token differs')
            root, announced = ready['rootIdentity'], ready['identity']
            current_root = self.helper.process_identity(root['pid'])
            current = self.helper.process_identity(announced['pid'])
            need(self.helper.identity_key(current_root) == self.helper.identity_key(root), 'Ready root changed')
            need(self.helper.identity_key(current) == self.helper.identity_key(announced), 'Ready leaf changed')
            need(current['uids'] == self.parent['uids'] and current_root['uids'] == self.parent['uids'],
                 'Ready dummy UID vector differs')
            escaped_fd = self.helper.open_identity_pidfd(current)
            need(alive(self.helper, current, escaped_fd), 'Escaped dummy is not actually alive')
            supervisor = self.helper.process_identity(current_root['ppid'])
            need(supervisor['ppid'] == os.getpid() and supervisor['uids'] == self.parent['uids'],
                 'Ready supervisor is not this runner\'s direct child')
            # There is exactly one Controller invocation in each per-case root.
            candidates = list(self.containment.glob('01-*/command-spec.json'))
            need(len(candidates) == 1, 'One actual command spec required')
            spec_path = candidates[0]
            spec = json.loads(spec_path.read_bytes())
            need(self.helper.identity_key(spec['parentIdentity']) == self.helper.identity_key(self.parent)
                 and spec['mode'] == 'isolation-test' and spec['argv'][2] == COMMANDS[self.name_case][0]
                 and spec['argv'][3:5] == [str(self.folder), self.token],
                 'Observer command spec/runner parent differs')
            supervisor_path = spec_path.parent / 'supervisor-identity.json'
            need(regular(supervisor_path), 'Actual supervisor identity original required')
            supervisor_original = json.loads(supervisor_path.read_bytes())
            pinned = supervisor_original.get('identity', supervisor_original.get('supervisorIdentity', supervisor_original))
            need(self.helper.identity_key(pinned) == self.helper.identity_key(supervisor),
                 'Actual supervisor identity original differs')
            need(supervisor_original['helperSha256'] == spec['helperSha256']
                 and self.helper.identity_key(supervisor_original['parentIdentity']) == self.helper.identity_key(self.parent),
                 'Actual supervisor source/parent binding differs')
            journal = spec_path.parent / 'lineage-events.jsonl'
            need(not any(row.get('kind') == 'owned-signal' and row.get('sent') is True
                         for row in read_events(journal)), 'Readiness arrived after a teardown signal')
            fd = self.helper.open_identity_pidfd(supervisor)
            need(alive(self.helper, supervisor, fd), 'Supervisor exited before readiness observation')
            if self.name_case in ('setsid-timeout', 'ignore-term', 'bystander'):
                need(ready['kind'] == 'setsid' and current['pid'] == current['sid'] == current['pgid']
                     and current['sid'] != root['sid'] and current['ppid'] == root['pid'],
                     'Actual live setsid escape was not established')
                if self.name_case == 'ignore-term':
                    need(ready['ignoresSigterm'] is True, 'SIGTERM-ignore installation was not recorded')
            elif self.name_case in ('doublefork-timeout', 'root-exit-orphan'):
                initial = ready['initialIdentity']
                middle_exit = json.loads((self.folder / 'intermediate-exit.json').read_bytes())
                middle = middle_exit['identity']
                need(ready['kind'] == 'doublefork' and initial['ppid'] == middle['pid']
                     and middle_exit['childPid'] == current['pid']
                     and current['sid'] == middle['pid'] == current['pgid']
                     and current['pid'] != current['sid'] and current['sid'] != root['sid']
                     and current['ppid'] == supervisor['pid'],
                     'Actual second fork, setsid and subreaper adoption were not established')
            else:
                need(self.name_case == 'interrupt' and ready['kind'] == 'root-sleep'
                     and current['pid'] == root['pid'], 'Unexpected readiness observation')
            observation = {'token': self.token, 'ready': ready, 'observedUtc': utc(),
                           'observedMonotonic': time.monotonic(), 'observedAlive': True,
                           'rootIdentity': current_root, 'escapedIdentity': current,
                           'supervisorIdentity': supervisor, 'beforeAnyOwnedSignal': True,
                           'commandSpecPath': str(spec_path), 'pidfdIdentityChecked': True,
                           'escapedPidfdAlive': True,
                           'supervisorIdentityOriginal': str(supervisor_path),
                           'interruptPhase': self.interrupt_phase}
            atomic_json(self.folder / 'observer-ack.json', observation)
            if self.name_case == 'interrupt':
                if self.interrupt_phase == 'after-timeout-during-cleanup':
                    cleanup_event = None
                    while not self.stop_requested.is_set() and time.monotonic() < self.end:
                        cleanup_event = next((row for row in read_events(journal)
                                              if row.get('kind') == 'cleanup-began'), None)
                        if cleanup_event is not None:
                            break
                        self.stop_requested.wait(.003)
                    need(cleanup_event is not None and cleanup_event.get('timedOut') is True
                         and cleanup_event.get('interrupted') is False
                         and time.monotonic() >= spec['executionEndMonotonic'],
                         'Actual timeout cleanup did not begin before after-timeout SIGINT')
                    observation['cleanupBeganEvent'] = cleanup_event
                else:
                    need(time.monotonic() < spec['executionEndMonotonic'],
                         'Before-deadline interrupt missed its actual execution deadline')
                # This is an actual SIGINT to the exact newly spawned supervisor,
                # never to the runner, a group, or a process found by a name.
                observation['sigintRequestedUtc'] = utc()
                observation['sigintRequestedMonotonic'] = time.monotonic()
                observation['executionEndMonotonic'] = spec['executionEndMonotonic']
                result = self.helper.signal_identity(supervisor, fd, signal.SIGINT,
                    self.parent['uids'][0], {self.helper.identity_key(supervisor)}, set())
                need(result.get('sent') is True, 'Actual supervisor SIGINT was not delivered')
                observation['supervisorSigint'] = result
                observation['interruptSignum'] = int(signal.SIGINT)
            self.observation = observation
            write_json(self.folder.parent / 'ready-observation.json', observation)
        except BaseException as error:
            self.error = type(error).__name__ + ': ' + str(error)
            write_json(self.folder.parent / 'ready-observation-error.json',
                       {'error': self.error, 'observedUtc': utc(), 'traceback': traceback.format_exc()})
        finally:
            if fd is not None:
                os.close(fd)
            if escaped_fd is not None:
                os.close(escaped_fd)


class OwnedDummy:
    """An exact direct dummy owned by this runner; never adopted global work."""
    def __init__(self, helper, fixture, folder, token, env, parent):
        self.helper, self.folder, self.token = helper, folder, token
        self.fixture, self.env, self.parent = fixture, env, parent
        self.stdout = folder.parent / 'owner-dummy.stdout.log'
        self.fd, self.process, self.identity = None, None, None
        self.cleanup = None

    def launch(self):
        # Assign the object before launch so a capture failure is still recorded
        # and receives the same exact-owned cleanup attempt in its caller.
        self.folder.mkdir(parents=True, exist_ok=False)
        with self.stdout.open('xb') as stream:
            self.process = subprocess.Popen([sys.executable, str(self.fixture), 'sentinel', str(self.folder), self.token],
                cwd=self.folder.parent, env=self.env, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
        self.identity = self.helper.process_identity(self.process.pid)
        need(self.identity['ppid'] == os.getpid() and self.identity['uids'] == self.parent['uids'],
             'Owner dummy direct-child identity differs')
        self.fd = self.helper.open_identity_pidfd(self.identity)

    def wait_ready(self):
        end = time.monotonic() + 2
        ready_path = self.folder / 'escape-ready.json'
        while not ready_path.exists() and time.monotonic() < end:
            time.sleep(.005)
        need(ready_path.exists(), 'Owner dummy not ready')
        ready = json.loads(ready_path.read_bytes())
        need(ready['kind'] == 'sentinel' and ready['token'] == self.token and
             self.helper.identity_key(ready['identity']) == self.helper.identity_key(self.identity)
             and alive(self.helper, self.identity, self.fd), 'Owner dummy readiness/identity differs')
        return ready

    def finish(self):
        result = {'scope': 'separate-actual-owner-exact-direct-dummy-cleanup',
                  'identity': self.identity, 'signals': [], 'cleanupComplete': False}
        try:
            if self.process is not None and self.fd is None:
                current = self.helper.process_identity(self.process.pid)
                need(current['ppid'] == os.getpid() and current['uids'] == self.parent['uids'],
                     'Uncaptured owner dummy no longer has the exact direct-child proof')
                need(self.identity is None or self.helper.identity_key(current) == self.helper.identity_key(self.identity),
                     'Uncaptured owner dummy identity changed')
                self.identity = current
                result['identity'] = current
                self.fd = self.helper.open_identity_pidfd(current)
            need(self.fd is not None and self.identity is not None, 'No exact owned dummy handle')
            for signum, seconds in ((signal.SIGTERM, .5), (signal.SIGKILL, 2.)):
                if self.helper.pidfd_exited(self.fd):
                    break
                sent = self.helper.signal_identity(self.identity, self.fd, signum,
                    self.identity['uids'][0], {self.helper.identity_key(self.identity)}, set())
                result['signals'].append(sent)
                end = time.monotonic() + seconds
                while not self.helper.pidfd_exited(self.fd) and time.monotonic() < end:
                    time.sleep(.005)
            need(self.helper.pidfd_exited(self.fd), 'Exact owned dummy remains alive')
            # Exact Popen child only. No global wait competes with Controller.
            result['exitCode'] = self.process.wait(timeout=.5)
            result['cleanupComplete'] = True
        except BaseException as error:
            result['error'] = type(error).__name__ + ': ' + str(error)
        finally:
            if self.fd is not None:
                os.close(self.fd)
                self.fd = None
            self.cleanup = result
            write_json(self.folder.parent / 'owner-cleanup.json', result)
        return result


def originals(paths, repo):
    seen = set()
    rows = []
    for root in paths:
        candidates = [root] if root.is_file() else sorted(root.rglob('*'))
        for path in candidates:
            if path.is_file() and path not in seen:
                rows.append(fullref(path, repo))
                seen.add(path)
    return rows


def verify_receipt(helper, receipt, spec, journal, expected, name, observer):
    need(receipt.get('mode') == spec.get('mode') == 'isolation-test' and
         receipt.get('productionInvocation') is False, 'Production invocation is forbidden')
    need(receipt['outcomeExitCode'] == expected, 'Actual outcome differs from expected ' + str(expected))
    cleanup = receipt['cleanup']
    need(cleanup.get('complete') is True and cleanup.get('kernelEchild') is True
         and cleanup.get('heldPidfdsExited') is True and cleanup.get('subreaperVerifiedAtCompletion') is True
         and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'),
         'Actual kernel/identity cleanup proof incomplete')
    baseline = {helper.identity_key(row) for row in receipt['baselineIdentities']}
    owned = {helper.identity_key(row['identity']) for row in receipt['ownedIdentities']}
    signals = [row for row in journal if row.get('kind') == 'owned-signal' and row.get('sent') is True]
    need(all(helper.identity_key(row['target']) in owned and
             helper.identity_key(row['target']) not in baseline for row in signals),
         'An unowned/baseline identity was signalled')
    root_pid = receipt['root']['pid']
    root_reaped = [row for row in receipt['reaped'] if row.get('root') is True and row['pid'] == root_pid]
    need(len(root_reaped) == 1 and root_reaped[0]['exitCode'] == receipt['root']['exitCode'],
         'Original root wait status was not preserved exactly once')
    if name in ('normal-exit', 'nonzero-exit', 'root-exit-orphan'):
        need(receipt['root']['exitCode'] == expected, 'Root exit status differs')
    if expected == 124:
        need(receipt.get('timedOut') is True, 'Real timeout was not observed')
    if observer:
        need(observer.error is None and observer.observation is not None, 'Ready observer failed: ' + str(observer.error))
        escaped = observer.observation['escapedIdentity']
        need(helper.identity_key(escaped) in owned, 'Observed real fixture descendant never admitted')
        if name in ('setsid-timeout', 'doublefork-timeout', 'root-exit-orphan', 'ignore-term', 'bystander'):
            observed = [event[field] for event in journal for field in ('identity', 'target')
                        if isinstance(event.get(field), dict) and 'uids' in event[field]
                        and helper.identity_key(event[field]) == helper.identity_key(escaped)]
            need(escaped['pid'] != root_pid and escaped['sid'] != root_pid and
                 escaped['pgid'] == escaped['sid'] and
                 any(row['sid'] == escaped['sid'] and row['pgid'] == escaped['pgid'] for row in observed),
                 'Actual journal did not observe the escaped SID/PGID identity')
            if name in ('doublefork-timeout', 'root-exit-orphan'):
                need(any(row['ppid'] == receipt['supervisorIdentity']['pid'] for row in observed),
                     'Actual journal did not observe the doublefork adoption')
        signal_times = [datetime.fromisoformat(row['observedUtc']) for row in signals]
        need(not signal_times or datetime.fromisoformat(observer.observation['observedUtc']) <= min(signal_times),
             'Teardown began before live escape observation')
        if name == 'ignore-term':
            delivered = {row['signal'] for row in signals if helper.identity_key(row['target']) == helper.identity_key(escaped)}
            need({int(signal.SIGTERM), int(signal.SIGKILL)} <= delivered,
                 'Ignoring live descendant did not receive actual TERM then KILL')
        if name == 'interrupt':
            after = observer.interrupt_phase == 'after-timeout-during-cleanup'
            need(receipt.get('interrupted') is True and receipt.get('timedOut') is after
                 and observer.observation.get('supervisorSigint', {}).get('sent') is True,
                 'Actual own-supervisor interruption was not observed')
            requested = observer.observation['sigintRequestedMonotonic']
            deadline = spec['executionEndMonotonic']
            need((requested >= deadline) if after else (requested < deadline),
                 'SIGINT did not occur in its required actual deadline phase')
    return 0


def command_case(helper, args, name, case_root, fixture, parent, interrupt_phase=None):
    mode, expected = COMMANDS[name]
    label = name if interrupt_phase is None else ('interrupt-after-timeout' if
        interrupt_phase == 'after-timeout-during-cleanup' else 'interrupt-before-deadline')
    folder, containment = case_root / 'fixture', case_root / 'containment'
    folder.mkdir(parents=True, exist_ok=False)
    token, cap = uuid.uuid4().hex, args.cap
    policy = {'helperPath': str(args.helper), 'helperSha256': args.helper_sha256,
              'evidenceRoot': str(containment), 'fixturePath': str(fixture),
              'fixtureSha256': args.fixture_sha256, 'fixtureOutputRoot': str(args.batch_root)}
    controller = helper.Controller(policy, args.repository_root, test_mode=True)
    lane = Lane(controller, cap)
    observer = None
    dummy = None
    details = {'passed': False, 'expectedExitCode': expected, 'fixtureMode': mode,
               'token': token, 'originals': [], 'interruptPhase': interrupt_phase}
    abort = False
    try:
        if name == 'bystander':
            dummy = OwnedDummy(helper, fixture, case_root / 'bystander', uuid.uuid4().hex, lane.env, parent)
            dummy.launch()
            ready = dummy.wait_ready()
            details['bystanderBefore'] = {'identity': dummy.identity, 'ready': ready,
                                          'alive': alive(helper, dummy.identity, dummy.fd)}
            write_json(case_root / 'bystander-before.json', details['bystanderBefore'])
        if name in ('setsid-timeout', 'doublefork-timeout', 'root-exit-orphan', 'ignore-term', 'interrupt', 'bystander'):
            observer = Observer(helper, name, folder, token, containment, parent, cap, interrupt_phase)
            observer.start()
        try:
            event = lane.child(label, [sys.executable, str(fixture), mode, str(folder), token],
                               cap=cap, log=case_root / 'actual-command.stdout.log')
        except BaseException:
            abort = True  # No subsequent command after unproved Controller cleanup.
            raise
        finally:
            if observer:
                observer.stop_requested.set()
                observer.join(timeout=3)
                need(not observer.is_alive(), 'Observer failed to stop')
        receipt_path = containment / ('01-' + label) / 'supervisor-result.json'
        receipt = json.loads(receipt_path.read_bytes())
        spec = json.loads((receipt_path.parent / 'command-spec.json').read_bytes())
        journal = [json.loads(line) for line in (receipt_path.parent / 'lineage-events.jsonl').read_text().splitlines() if line]
        details['containmentReceipt'] = event['containmentReceipt']
        need(details['containmentReceipt'] == fullref(receipt_path, args.repository_root),
             'Actual Controller receipt reference was not repository-relative')
        need(event['exitCode'] == expected, 'Controller event outcome differs')
        need(receipt['helperSha256'] == spec['helperSha256'] == args.helper_sha256 and
             receipt['argv'] == spec['argv'] and spec['argv'][:2] == [sys.executable, str(fixture)],
             'Exact executed helper/fixture command binding differs')
        verify_receipt(helper, receipt, spec, journal, expected, name, observer)
        if observer:
            details['escapeEvidence'] = fullref(folder / 'escape-ready.json', args.repository_root)
            details['escapeAck'] = fullref(folder / 'observer-ack.json', args.repository_root)
            details['readyObservation'] = fullref(case_root / 'ready-observation.json', args.repository_root)
        if dummy:
            same = alive(helper, dummy.identity, dummy.fd)
            key = helper.identity_key(dummy.identity)
            need(key in {helper.identity_key(row) for row in receipt['baselineIdentities']},
                 'Preexisting bystander absent from actual baseline')
            need(key not in {helper.identity_key(row['identity']) for row in receipt['ownedIdentities']},
                 'Preexisting bystander wrongly admitted as owned')
            need(not any(row.get('kind') == 'owned-signal' and row.get('sent') is True and
                         helper.identity_key(row['target']) == key for row in journal), 'Bystander was signalled')
            details['bystanderAfter'] = {'identity': helper.process_identity(dummy.identity['pid']),
                                         'aliveSameIdentity': same}
            write_json(case_root / 'bystander-after.json', details['bystanderAfter'])
            need(same, 'Preexisting bystander did not remain alive unchanged')
        details['passed'] = True
    except BaseException as error:
        details.update(error=type(error).__name__ + ': ' + str(error), traceback=traceback.format_exc())
    finally:
        if dummy:
            cleanup = dummy.finish()
            details['ownerCleanup'] = cleanup
            if not cleanup['cleanupComplete']:
                details['passed'], abort = False, True
            bystander_receipt = {'kind': 'real-preexisting-bystander',
                'helperSha256': args.helper_sha256, 'beforeIdentity': dummy.identity,
                'afterIdentity': details.get('bystanderAfter', {}).get('identity'),
                'aliveBefore': details.get('bystanderBefore', {}).get('alive') is True,
                'aliveAfter': details.get('bystanderAfter', {}).get('aliveSameIdentity') is True,
                'protectedFromSignals': details.get('passed') is True,
                'cleanupComplete': cleanup['cleanupComplete'], 'ownerCleanup': cleanup,
                'originals': originals([case_root / 'bystander', case_root / 'owner-dummy.stdout.log',
                                        case_root / 'owner-cleanup.json'], args.repository_root)}
            bystander_path = case_root / 'bystander-receipt.json'
            write_json(bystander_path, bystander_receipt)
            details['bystanderReceipt'] = fullref(bystander_path, args.repository_root)
        write_json(case_root / 'lane-events.json', lane.events)
        write_json(case_root / 'case-observations.json', {k: v for k, v in details.items() if k != 'originals'})
        details['originals'] = originals([case_root, containment], args.repository_root)
        if 'containmentReceipt' not in details:
            receipt_path = containment / ('01-' + label) / 'supervisor-result.json'
            if regular(receipt_path):
                details['containmentReceipt'] = fullref(receipt_path, args.repository_root)
    return details, abort


def stale_case(helper, args, case_root, fixture, parent):
    details = {'passed': False, 'originals': []}
    result = {'kind': 'real-dummy-identity-refusal', 'helperSha256': args.helper_sha256,
              'metadataMismatchOnly': True, 'hostPidReuseClaimed': False,
              'startTicksMismatchRefused': False, 'uidMismatchRefused': False,
              'aliveBefore': False, 'aliveAfterRefusals': False,
              'signalsSentDuringRefusals': 0, 'cleanupComplete': False, 'attempts': []}
    dummy = None
    original_signal = helper.signal
    calls = []
    class CountActualSignalCalls:
        def __getattr__(self, name):
            return getattr(original_signal, name)
        def pidfd_send_signal(self, fd, signum, info=None, flags=0):
            calls.append({'signal': int(signum), 'observedUtc': utc()})
            return original_signal.pidfd_send_signal(fd, signum, info, flags)
    try:
        env = dict(os.environ, PYTHONDONTWRITEBYTECODE='1', PYTHONUNBUFFERED='1')
        dummy = OwnedDummy(helper, fixture, case_root / 'fixture', uuid.uuid4().hex, env, parent)
        dummy.launch()
        result['ready'] = dummy.wait_ready()
        result['identity'] = dummy.identity
        result['aliveBefore'] = alive(helper, dummy.identity, dummy.fd)
        need(result['aliveBefore'], 'Actual own stale-test dummy is not alive')
        helper.signal = CountActualSignalCalls()
        for field in ('startTicks', 'uids'):
            changed = dict(dummy.identity)
            if field == 'startTicks':
                changed['startTicks'] += 1
            else:
                changed['uids'] = [dummy.identity['uids'][0] + 1] * 4
            attempt = {'changedField': field, 'declaredIdentity': changed, 'refused': False}
            try:
                # SIGCONT is still a real signal if a regression sends it;
                # counting forwards actual calls instead of fabricating refusal.
                returned = helper.signal_identity(changed, dummy.fd, signal.SIGCONT,
                    dummy.identity['uids'][0], {helper.identity_key(changed)}, set())
                attempt['unexpectedReturn'] = returned
            except ValueError as error:
                attempt.update(refused=str(error).startswith('Signal refused:'), reason=str(error))
            result['attempts'].append(attempt)
            result['startTicksMismatchRefused' if field == 'startTicks' else 'uidMismatchRefused'] = attempt['refused']
        result['signalsSentDuringRefusals'] = len(calls)
        result['actualSignalCallsDuringRefusals'] = calls
        result['aliveAfterRefusals'] = alive(helper, dummy.identity, dummy.fd)
        need(result['startTicksMismatchRefused'] and result['uidMismatchRefused'] and
             result['aliveAfterRefusals'] and result['signalsSentDuringRefusals'] == 0,
             'Actual live startTicks/UID refusal assertions failed')
        details['passed'] = True
    except BaseException as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
        result['traceback'] = traceback.format_exc()
    finally:
        helper.signal = original_signal
        result['signalsSentDuringRefusals'] = len(calls)
        result['actualSignalCallsDuringRefusals'] = calls
        if dummy:
            cleanup = dummy.finish()
            result['ownerCleanup'] = cleanup
            result['cleanupComplete'] = cleanup['cleanupComplete']
        details['passed'] = details['passed'] and result['cleanupComplete']
        path = case_root / 'identity-refusal-receipt.json'
        write_json(path, result)
        details['identityRefusalReceipt'] = fullref(path, args.repository_root)
        details['originals'] = originals([case_root], args.repository_root)
    return details, not result['cleanupComplete']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository-root', type=canonical, required=True)
    parser.add_argument('--helper', type=canonical, required=True)
    parser.add_argument('--helper-sha256', required=True)
    parser.add_argument('--fixture', type=canonical, required=True)
    parser.add_argument('--fixture-sha256', required=True)
    parser.add_argument('--runner-sha256', required=True)
    parser.add_argument('--batch-root', type=canonical, required=True)
    parser.add_argument('--cap', type=float, default=2.)
    args = parser.parse_args()
    need(.5 <= args.cap <= 2., 'Each execution cap must remain between .5 and 2 seconds')
    need(args.repository_root.is_dir() and args.batch_root.is_relative_to(args.repository_root)
         and not args.batch_root.exists(), 'Fresh absent batch under repository required; previous attempts are immutable')
    runner = canonical(str(Path(__file__).absolute()))
    source_paths = {'helper': args.helper, 'fixture': args.fixture, 'runner': runner}
    expected = {'helper': args.helper_sha256, 'fixture': args.fixture_sha256, 'runner': args.runner_sha256}
    raw = {}
    for name, path in source_paths.items():
        need(path.is_relative_to(args.repository_root) and regular(path), 'Source must be a regular repository original')
        raw[name] = path.read_bytes()
        need(raw_digest(raw[name]) == expected[name], 'Pinned ' + name + ' bytes differ')
    args.batch_root.mkdir(parents=True, exist_ok=False)
    sources = {name: fullref(path, args.repository_root) for name, path in source_paths.items()}
    for name, content in raw.items():
        destination = args.batch_root / 'source-copies' / (name + '.py')
        destination.parent.mkdir(parents=True, exist_ok=True)
        with destination.open('xb') as stream:
            stream.write(content)
    report = {'mode': 'isolated-no-production', 'status': 'failed', 'startedUtc': utc(),
              'helperSha256': args.helper_sha256, 'sourceLoadPolicy': LOAD_POLICY,
              'executedSources': sources, 'sourceCopies': originals([args.batch_root / 'source-copies'], args.repository_root),
              'productionInvocationCount': 0, 'baselineSignalsSent': 0,
              'unresolvedOwnedChildren': None, 'publishReady': False, 'baselineReady': False,
              'rootApprovalCreated': False, 'cases': {}, 'executionCapsSeconds': args.cap}
    exit_code = 1
    try:
        module_name = '_isolated_owned_containment_' + uuid.uuid4().hex
        helper = types.ModuleType(module_name)
        helper.__file__ = str(args.helper)
        sys.modules[module_name] = helper
        # Exact captured raw bytes, no importlib loader and no cached bytecode.
        exec(compile(raw['helper'], str(args.helper), 'exec'), helper.__dict__)
        need(tuple(helper.REQUIRED_CASES) == REQUIRED_CASES, 'Helper required cases changed')
        parent = helper.process_identity(os.getpid())
        need(parent['uids'] == [os.getuid()] * 4, 'Runner UID vector must be stable')
        aborted = False
        for name in REQUIRED_CASES:
            if aborted:
                report['cases'][name] = {'passed': False, 'status': 'not-run-after-unproved-cleanup', 'originals': []}
                continue
            case_root = args.batch_root / 'cases' / name
            case_root.mkdir(parents=True, exist_ok=False)
            if name == 'stale-identity':
                details, aborted = stale_case(helper, args, case_root, args.fixture, parent)
            elif name == 'interrupt':
                before_root = case_root / 'before-deadline'
                before_root.mkdir(exist_ok=False)
                before, aborted = command_case(helper, args, name, before_root, args.fixture, parent,
                                                interrupt_phase='before-deadline')
                after = {'passed': False, 'status': 'not-run-after-unproved-cleanup', 'originals': []}
                if not aborted:
                    after_root = case_root / 'after-timeout-during-cleanup'
                    after_root.mkdir(exist_ok=False)
                    after, aborted = command_case(helper, args, name, after_root, args.fixture, parent,
                        interrupt_phase='after-timeout-during-cleanup')
                details = {'passed': before['passed'] and after['passed'],
                           'fixtureMode': 'sleep', 'expectedExitCode': 130,
                           'beforeDeadline': before, 'afterTimeoutDuringCleanup': after,
                           'originals': before['originals'] + after['originals']}
                for field in ('containmentReceipt', 'escapeEvidence', 'escapeAck', 'readyObservation'):
                    if field in before:
                        details[field] = before[field]
                if 'containmentReceipt' in after:
                    details['afterTimeoutReceipt'] = after['containmentReceipt']
                for field in ('escapeEvidence', 'escapeAck', 'readyObservation'):
                    if field in after:
                        details['afterTimeout' + field[0].upper() + field[1:]] = after[field]
            else:
                details, aborted = command_case(helper, args, name, case_root, args.fixture, parent)
            report['cases'][name] = details
        for name, path in source_paths.items():
            need(fullref(path, args.repository_root) == sources[name], 'Executed source changed during run')
        baseline_signals = 0
        unresolved = 0
        for name, detail in report['cases'].items():
            receipts = [detail[field] for field in ('containmentReceipt', 'afterTimeoutReceipt') if field in detail]
            for reference in receipts:
                path = args.repository_root / reference['file']
                child = json.loads(path.read_bytes())
                baseline = {helper.identity_key(row) for row in child.get('baselineIdentities', [])}
                journal_path = path.parent / 'lineage-events.jsonl'
                journal = [json.loads(line) for line in journal_path.read_text().splitlines() if line]
                baseline_signals += sum(row.get('kind') == 'owned-signal' and row.get('sent') is True and
                    helper.identity_key(row['target']) in baseline for row in journal)
                report['productionInvocationCount'] += int(child.get('productionInvocation') is True)
                unresolved += int(child.get('cleanup', {}).get('complete') is not True)
            if name != 'stale-identity' and (not receipts or (name == 'interrupt' and len(receipts) != 2)):
                unresolved += 1
            if name == 'stale-identity' and not detail.get('passed'):
                unresolved += 1
            if detail.get('ownerCleanup', {}).get('cleanupComplete') is False:
                unresolved += 1
        report['baselineSignalsSent'], report['unresolvedOwnedChildren'] = baseline_signals, unresolved
        passed = (set(report['cases']) == set(REQUIRED_CASES) and
                  all(case.get('passed') is True for case in report['cases'].values()) and
                  baseline_signals == 0 and unresolved == 0 and report['productionInvocationCount'] == 0)
        report['status'] = 'passed' if passed else 'failed'
        exit_code = 0 if passed else 1
    except BaseException as error:
        report['fatalError'] = type(error).__name__ + ': ' + str(error)
        report['traceback'] = traceback.format_exc()
    finally:
        report['finishedUtc'] = utc()
        write_json(args.batch_root / 'isolated-test-report.json', report)
    print(json.dumps({'report': str(args.batch_root / 'isolated-test-report.json'),
                      'status': report['status'], 'publishReady': False, 'baselineReady': False}))
    return exit_code


if __name__ == '__main__':
    raise SystemExit(main())
