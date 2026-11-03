#!/usr/bin/env python3
"""UNEXECUTED containment successor: a dedicated Linux subreaper per command.

Import has no process, prctl or signal side effects. Production callers must
present actual isolated-test and Root receipts for these exact helper bytes.
Never targets a process by name/session/PGID or reconstructs a missing receipt.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import ctypes
import errno
import hashlib
import json
import os
import select
import signal
import stat
import subprocess
import sys
import time

WALL = 0x40000000
VERSION = 'owned-descendants-v1'
REQUIRED_CASES = ('normal-exit', 'nonzero-exit', 'setsid-timeout', 'doublefork-timeout',
                  'root-exit-orphan', 'ignore-term', 'interrupt', 'bystander', 'stale-identity')


def need(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def absolute(value):
    path = Path(value)
    need(path.is_absolute() and os.path.normpath(str(path)) == str(path)
         and '\\' not in str(path) and '\0' not in str(path), 'Canonical absolute path required')
    return path


def regular(path):
    path = Path(path)
    return path.is_file() and stat.S_ISREG(path.lstat().st_mode) and not any(p.is_symlink() for p in (path, *path.parents))


def digest(path):
    need(regular(path), 'Expected regular original: ' + str(path))
    value = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def fullref(path, repo):
    path = Path(path)
    return {'file': str(path.relative_to(repo)) if path.is_relative_to(repo) else str(path),
            'sha256': digest(path), 'bytes': path.stat().st_size}


def exact_ref(row, repo):
    need(isinstance(row, dict) and isinstance(row.get('file'), str), 'Actual original full reference required')
    path = Path(row['file'])
    path = path if path.is_absolute() else repo / path
    need(path.resolve().is_relative_to(repo.resolve()) and fullref(path, repo) == row, 'Original reference differs')
    return path


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n')


def process_identity(pid):
    """Kernel metadata only; no command-line arguments or credentials are read."""
    folder = Path('/proc') / str(int(pid))
    text = (folder / 'stat').read_bytes()
    suffix = text.rsplit(b') ', 1)[1].split()
    status = (folder / 'status').read_bytes()
    uids = next(line.split()[1:] for line in status.splitlines() if line.startswith(b'Uid:'))
    return {'pid': int(pid), 'ppid': int(suffix[1]), 'pgid': int(suffix[2]), 'sid': int(suffix[3]),
            'state': suffix[0].decode('ascii'), 'startTicks': int(suffix[19]), 'uids': [int(x) for x in uids]}


def identity_key(identity):
    return (identity['pid'], identity['startTicks'], tuple(identity['uids']))


def pidfd_exited(fd):
    poller = select.poll()
    poller.register(fd, select.POLLIN | select.POLLHUP | select.POLLERR)
    events = poller.poll(0)
    need(not any(flags & select.POLLNVAL for _, flags in events), 'Held pidfd became invalid')
    return bool(events)


def open_identity_pidfd(identity):
    fd = os.pidfd_open(identity['pid'], 0)
    try:
        need(identity_key(process_identity(identity['pid'])) == identity_key(identity), 'Identity changed around pidfd_open')
        return fd
    except BaseException:
        os.close(fd)
        raise


def signal_identity(identity, pidfd, signum, expected_uid, owned_keys, baseline_keys):
    """Only a held pidfd for a proven identity can be signalled. No kill fallback."""
    key = identity_key(identity)
    need(key in owned_keys and key not in baseline_keys, 'Signal lacks owned non-baseline identity proof')
    if pidfd_exited(pidfd):
        return {'sent': False, 'reason': 'held-process-already-exited'}
    try:
        current = process_identity(identity['pid'])
    except (FileNotFoundError, ProcessLookupError):
        if pidfd_exited(pidfd):
            return {'sent': False, 'reason': 'held-process-exited-at-identity-read'}
        raise
    if identity_key(current) != key or current['uids'] != [expected_uid] * 4:
        if pidfd_exited(pidfd):
            return {'sent': False, 'reason': 'original-held-process-exited-new-pid-not-targeted'}
        raise ValueError('Signal refused: changed startTicks/UID or unresolved identity')
    if pidfd_exited(pidfd):
        return {'sent': False, 'reason': 'held-process-exited-before-signal'}
    try:
        signal.pidfd_send_signal(pidfd, signum, None, 0)
    except ProcessLookupError:
        if pidfd_exited(pidfd):
            return {'sent': False, 'reason': 'held-process-exited-at-signal'}
        raise
    return {'sent': True, 'signal': int(signum), 'identity': current}


def snapshot(stop=None):
    rows = {}
    for folder in Path('/proc').iterdir():
        need(stop is None or time.monotonic() < stop, 'Bounded process snapshot deadline exhausted')
        if folder.name.isdigit():
            try:
                rows[int(folder.name)] = process_identity(int(folder.name))
            except (FileNotFoundError, ProcessLookupError, PermissionError, StopIteration, IndexError):
                pass
    return rows


def configure_subreaper(expected_parent):
    need(sys.platform == 'linux' and hasattr(os, 'pidfd_open') and hasattr(signal, 'pidfd_send_signal')
         and hasattr(os, 'waitid') and hasattr(os, 'WNOWAIT'), 'Required Linux pidfd/waitid features unavailable')
    need(len(list(Path('/proc/self/task').iterdir())) == 1, 'A dedicated single-threaded supervisor is required')
    signal.signal(signal.SIGCHLD, signal.SIG_DFL)
    libc = ctypes.CDLL(None, use_errno=True)
    need(libc.prctl(36, 1, 0, 0, 0) == 0, 'PR_SET_CHILD_SUBREAPER failed')
    actual = ctypes.c_int()
    need(libc.prctl(37, ctypes.byref(actual), 0, 0, 0) == 0 and actual.value == 1,
         'PR_GET_CHILD_SUBREAPER did not confirm ownership boundary')
    need(libc.prctl(1, int(signal.SIGTERM), 0, 0, 0) == 0, 'PR_SET_PDEATHSIG failed')
    need(os.getppid() == expected_parent['pid'] and identity_key(process_identity(os.getppid())) == identity_key(expected_parent),
         'Outer parent died or changed around PDEATHSIG setup')
    # The optional /proc/.../children interface is deliberately not required.
    # A scan is discovery only. Non-consuming kernel ECHILD is the empty proof.
    try:
        os.waitid(os.P_ALL, 0, os.WEXITED | os.WNOHANG | os.WNOWAIT | WALL)
    except ChildProcessError:
        return True
    raise ValueError('Fresh dedicated supervisor must establish kernel ECHILD before launch')


class Tracker:
    def __init__(self, root, baseline, expected_uid, journal):
        self.root, self.baseline, self.uid, self.journal = root, baseline, expected_uid, journal
        self.owner = os.getpid()
        self.records = {}
        self.by_pid = {}
        self.violations = []
        self.reaped = []
        self.root_exit = None
        self.root_finished = None
        self.echild = False

    def record(self, value):
        with self.journal.open('a') as stream:
            stream.write(json.dumps(dict(observedUtc=utc(), **value), ensure_ascii=False) + '\n')

    def parent_proven(self, identity):
        if identity['ppid'] == self.owner:
            return 'direct-or-kernel-adopted-child'
        parent = self.by_pid.get(identity['ppid'])
        if parent and not self.terminal(parent):
            try:
                if identity_key(process_identity(identity['ppid'])) == identity_key(parent['identity']):
                    return 'live-pidfd-owned-parent'
            except (FileNotFoundError, ProcessLookupError, PermissionError):
                pass
        return None

    def admit(self, identity, proof=None):
        key = identity_key(identity)
        if key in self.records:
            return True
        origin = proof or self.parent_proven(identity)
        if not origin:
            return False
        if key in self.baseline or identity['uids'] != [self.uid] * 4:
            self.violations.append({'kind': 'owned-identity-blocked', 'identity': identity, 'origin': origin})
            return False
        fd, stored = None, False
        try:
            fd = open_identity_pidfd(identity)
            current = process_identity(identity['pid'])
            need(identity_key(current) == key and current['uids'] == [self.uid] * 4, 'Identity changed after pidfd capture')
            if not self.parent_proven(current) and proof != 'kernel-wait-child':
                os.close(fd)
                return False
            row = {'identity': current, 'origin': origin, 'fd': fd, 'signals': []}
            self.records[key] = row; self.by_pid[current['pid']] = row
            stored = True
            self.record({'kind': 'owned-admission', 'identity': current, 'origin': origin})
            return True
        except (FileNotFoundError, ProcessLookupError):
            if fd is not None and not stored:
                os.close(fd)
            return False
        except (OSError, ValueError) as error:
            if fd is not None and not stored:
                os.close(fd)
            self.violations.append({'kind': 'owned-admission-unresolved', 'identity': identity, 'reason': str(error)})
            return False

    def discover(self, stop):
        rows = snapshot(stop)
        pending = list(rows.values())
        for _ in range(len(pending) + 1):
            changed = False
            for identity in pending:
                need(time.monotonic() < stop, 'Bounded lineage discovery deadline exhausted')
                if identity_key(identity) not in self.records and self.parent_proven(identity):
                    changed = self.admit(identity) or changed
            if not changed:
                break
        # Unreadable/unattributed living children cannot disappear into an
        # "empty scan": waitid returns None, rather than ECHILD, while they live.

    def reap_root(self):
        if self.root_exit is not None:
            return
        try:
            pid, status = os.waitpid(self.root.pid, os.WNOHANG | WALL)
        except ChildProcessError:
            self.violations.append({'kind': 'root-exit-status-lost'})
            return
        if pid == self.root.pid:
            self.root_exit = os.waitstatus_to_exitcode(status)
            self.root.returncode = self.root_exit  # No Popen.poll/wait competes.
            self.root_finished = utc()
            self.reaped.append({'pid': pid, 'exitCode': self.root_exit, 'waitpidStatus': status, 'root': True})

    def reap_and_check(self, stop):
        self.reap_root()
        self.echild = False
        while True:
            need(time.monotonic() < stop, 'Bounded child reaping deadline exhausted')
            try:
                peek = os.waitid(os.P_ALL, 0, os.WEXITED | os.WNOHANG | os.WNOWAIT | WALL)
            except ChildProcessError:
                self.echild = self.root_exit is not None
                return
            if peek is None:
                return  # Child exists; none is waitable. Never call this empty.
            pid = peek.si_pid
            if self.root_exit is None and pid == self.root.pid:
                self.reap_root()
                if self.root_exit is None:
                    return
                continue
            try:
                self.admit(process_identity(pid), proof='kernel-wait-child')
            except (OSError, ValueError, IndexError, StopIteration) as error:
                self.violations.append({'kind': 'waitable-owned-identity-unresolved', 'pid': pid, 'reason': str(error)})
            got, status = os.waitpid(pid, os.WNOHANG | WALL)  # Exact peeked child only.
            if got == pid:
                self.reaped.append({'pid': pid, 'exitCode': os.waitstatus_to_exitcode(status), 'root': False,
                                    'waitid': {'pid': peek.si_pid, 'uid': peek.si_uid, 'code': peek.si_code, 'status': peek.si_status}})
            else:
                return

    def terminal(self, row):
        if row.get('exited'):
            return True
        if pidfd_exited(row['fd']):
            os.close(row['fd']); row['fd'] = None; row['exited'] = True
            return True
        return False

    def live(self):
        return [row for row in self.records.values() if not self.terminal(row)]

    def stop(self, signum):
        for row in self.live():
            if int(signum) in row['signals']:
                continue
            try:
                result = signal_identity(row['identity'], row['fd'], signum, self.uid, set(self.records), self.baseline)
                row['signals'].append(int(signum))
                self.record({'kind': 'owned-signal', 'target': row['identity'], **result})
            except (OSError, ValueError) as error:
                self.violations.append({'kind': 'signal-refused', 'identity': row['identity'], 'reason': str(error)})

    def close(self):
        for row in self.records.values():
            if row['fd'] is not None:
                os.close(row['fd']); row['fd'] = None


def validate_acceptance(policy, repo, helper_sha):
    """Production is blocked until actual isolated results and Root are pinned."""
    receipt = policy.get('acceptance')
    need(isinstance(receipt, dict), 'Actual containment acceptance references required')
    result = json.loads(exact_ref(receipt.get('report'), repo).read_bytes())
    review = json.loads(exact_ref(receipt.get('rootReview'), repo).read_bytes())
    need(result.get('status') == 'passed' and result.get('mode') == 'isolated-no-production'
         and result.get('helperSha256') == helper_sha and result.get('productionInvocationCount') == 0
         and result.get('baselineSignalsSent') == 0 and result.get('unresolvedOwnedChildren') == 0,
         'Exact actual isolated containment results required, never a source-only review')
    cases = result.get('cases', {})
    need(set(cases) == set(REQUIRED_CASES) and all(case.get('passed') is True for case in cases.values()),
         'All required real isolated cases must pass, not a subset')
    sources = result.get('executedSources', {})
    need(set(sources) == {'helper', 'fixture', 'runner'}, 'Actual helper/fixture/runner source originals required')
    source_paths = {key: exact_ref(row, repo) for key, row in sources.items()}
    need(sources['helper']['sha256'] == helper_sha and result.get('sourceLoadPolicy') == 'compile-exec-exact-hashed-bytes-no-pyc',
         'Actual exact loaded helper source and test source policy required')
    need(review.get('status') == 'approved' and review.get('scope') == 'isolated-process-containment-tests'
         and review.get('reviewer') in ('root', '/root') and review.get('helperSha256') == helper_sha
         and review.get('report') == receipt['report'] and review.get('publishReady') is False
         and review.get('baselineReady') is False and review.get('executedSources') == sources,
         'Actual Root test approval for the same helper/fixture/runner required')
    need(datetime.fromisoformat(review['reviewedUtc'].replace('Z', '+00:00')) >=
         datetime.fromisoformat(result['finishedUtc'].replace('Z', '+00:00')), 'Root review must follow actual tests')
    outcomes = {'normal-exit': 0, 'nonzero-exit': 7, 'setsid-timeout': 124, 'doublefork-timeout': 124,
                'root-exit-orphan': 0, 'ignore-term': 124, 'interrupt': 130, 'bystander': 124}
    for name, case in cases.items():
        need(isinstance(case.get('originals'), list) and case['originals'], 'Each real test must retain actual originals')
        originals = {row['file']: row for row in case['originals']}
        need(len(originals) == len(case['originals']), 'Duplicate isolated-test original paths')
        for row in case['originals']:
            exact_ref(row, repo)
        if name == 'stale-identity':
            refused = json.loads(exact_ref(case.get('identityRefusalReceipt'), repo).read_bytes())
            need(refused.get('kind') == 'real-dummy-identity-refusal' and refused.get('helperSha256') == helper_sha
                 and refused.get('startTicksMismatchRefused') is True and refused.get('uidMismatchRefused') is True
                 and refused.get('aliveBefore') is True and refused.get('aliveAfterRefusals') is True
                 and refused.get('signalsSentDuringRefusals') == 0 and refused.get('cleanupComplete') is True,
                 'Actual live dummy startTicks/UID refusal observations required')
            continue
        child_path = exact_ref(case.get('containmentReceipt'), repo)
        child = json.loads(child_path.read_bytes())
        command_path, journal_path = child_path.parent / 'command-spec.json', child_path.parent / 'lineage-events.jsonl'
        need(regular(command_path) and regular(journal_path), 'Actual command spec and lineage originals required')
        command = json.loads(command_path.read_bytes())
        for path in (child_path, command_path, journal_path, Path(command['stdoutPath']), child_path.parent / 'supervisor.stdout.log'):
            row = fullref(path, repo)
            need(originals.get(row['file']) == row, 'Every actual supervisor/spec/lineage/stdout original must be hash-bound')
        need(child.get('mode') == command.get('mode') == 'isolation-test' and child.get('productionInvocation') is False
             and child.get('helperSha256') == command.get('helperSha256') == helper_sha
             and child.get('argv') == command.get('argv') and command['argv'][:2] == [sys.executable, str(source_paths['fixture'])]
             and command['policy']['fixtureSha256'] == sources['fixture']['sha256'] and child.get('outcomeExitCode') == outcomes[name],
             'Actual same-source isolated root command/outcome required')
        cleanup = child['cleanup']
        need(cleanup.get('complete') is True and cleanup.get('kernelEchild') is True
             and cleanup.get('heldPidfdsExited') is True and cleanup.get('subreaperVerifiedAtCompletion') is True
             and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'), 'Actual isolated ECHILD/identity cleanup proof required')
        baseline = {identity_key(row) for row in child['baselineIdentities']}
        owned = {identity_key(row['identity']) for row in child['ownedIdentities']}
        for line in journal_path.read_text().splitlines():
            event = json.loads(line)
            if event.get('kind') == 'owned-signal' and event.get('sent') is True:
                need(identity_key(event['target']) in owned and identity_key(event['target']) not in baseline,
                     'Any baseline/unproven signal invalidates test acceptance')
    return receipt


def verify_test_command(policy, argv):
    fixture = absolute(policy['fixturePath'])
    need(regular(fixture) and fixture.suffix == '.py' and digest(fixture) == policy['fixtureSha256'], 'Exact dummy fixture required')
    need(len(argv) >= 5 and argv[0] == sys.executable and Path(argv[1]) == fixture,
         'Isolation mode can launch only its pinned Python dummy fixture')
    allowed = absolute(policy.get('fixtureOutputRoot', str(Path(policy['evidenceRoot']).parent)))
    need(absolute(argv[3]).is_relative_to(allowed), 'Dummy fixture output must remain under its isolated test directory')


def supervise(spec_path):
    """Runs in its own new process. It never uses the caller's process group."""
    need(regular(spec_path), 'Regular exact command spec required')
    raw = spec_path.read_bytes(); spec = json.loads(raw)
    helper_sha = digest(Path(__file__).absolute())
    need(spec.get('helperSha256') == helper_sha and spec.get('version') == VERSION, 'Supervisor source/spec version differs')
    repo = Path(spec['repositoryRoot']); argv = spec['argv']
    need(isinstance(argv, list) and argv and all(isinstance(value, str) and '\0' not in value for value in argv), 'Actual argv required')
    if spec['mode'] == 'isolation-test':
        verify_test_command(spec['policy'], argv)
    else:
        need(spec['mode'] == 'production-caller', 'Unknown supervisor mode')
        validate_acceptance(spec['policy'], repo, helper_sha)
    out = spec_path.parent
    result_path, journal = out / 'supervisor-result.json', out / 'lineage-events.jsonl'
    need(not result_path.exists() and not journal.exists(), 'Preserve earlier supervisor records')
    flags = {'cancelled': False}
    def cancelled(signum, frame):
        flags['cancelled'] = True
    for signum in (signal.SIGTERM, signal.SIGINT, signal.SIGHUP):
        signal.signal(signum, cancelled)
    report = {'version': VERSION, 'mode': spec['mode'], 'status': 'containment-unproven',
              'publishReady': False, 'baselineReady': False, 'helperSha256': helper_sha,
              'startedUtc': utc(), 'argv': argv, 'executionEndMonotonic': spec['executionEndMonotonic'],
              'productionInvocation': spec['mode'] == 'production-caller', 'root': {'spawned': False}}
    root, tracker = None, None
    execution_end = float(spec['executionEndMonotonic'])
    cleanup_limit = float(spec['cleanupSeconds'])
    need(0 < cleanup_limit <= 10, 'At most10 seconds of separate cleanup allowed')
    parent = spec['parentIdentity']
    try:
        configure_subreaper(parent)
        report['subreaperVerifiedBeforeSpawn'] = True
        owner = process_identity(os.getpid()); need(len(set(owner['uids'])) == 1, 'Supervisor UID vector is not stable')
        report['supervisorIdentity'] = owner; report['parentIdentity'] = parent
        baseline_rows = snapshot(execution_end); baseline = {identity_key(value) for value in baseline_rows.values()}
        report['baselineIdentities'] = list(baseline_rows.values())
        need(not flags['cancelled'] and time.monotonic() < execution_end, 'Cancelled/deadline before actual root spawn')
        need(os.getppid() == parent['pid'] and identity_key(process_identity(parent['pid'])) == identity_key(parent), 'Outer parent changed before spawn')
        stdout = Path(spec['stdoutPath'])
        need(not stdout.exists() and not any(path.is_symlink() for path in (stdout, *stdout.parents)), 'Actual stdout must be new regular path')
        stdout.parent.mkdir(parents=True, exist_ok=True)
        with stdout.open('xb') as stream:
            root_started = utc()
            root = subprocess.Popen(argv, cwd=spec['cwd'], env=dict(os.environ), stdout=stream,
                                    stderr=subprocess.STDOUT, start_new_session=True)
        tracker = Tracker(root, baseline, owner['uids'][0], journal)
        tracker.admit(process_identity(root.pid), proof='direct-root-popen-child')
        report['root'] = {'spawned': True, 'pid': root.pid, 'startedUtc': root_started, 'argv': argv}
        while not flags['cancelled'] and time.monotonic() < execution_end:
            tracker.discover(execution_end); tracker.reap_and_check(execution_end)
            if tracker.root_exit is not None:
                break
            time.sleep(min(.025, max(0, execution_end - time.monotonic())))
        timed_out = tracker.root_exit is None and not flags['cancelled'] and time.monotonic() >= execution_end
        report.update(timedOut=timed_out, interrupted=flags['cancelled'])
    except (Exception, KeyboardInterrupt) as error:
        report['error'] = type(error).__name__ + ': ' + str(error)
        report.setdefault('timedOut', root is not None and time.monotonic() >= execution_end)
        report.setdefault('interrupted', bool(flags['cancelled']))
    finally:
        cleanup_started = time.monotonic()
        cleanup_end = min(cleanup_started + cleanup_limit, execution_end + cleanup_limit)
        cleanup_error = None
        if tracker is not None:
            discovery_errors = []
            while time.monotonic() < cleanup_end:
                try:
                    tracker.discover(cleanup_end); tracker.reap_and_check(cleanup_end)
                except (Exception, KeyboardInterrupt) as error:
                    discovery_errors.append(type(error).__name__ + ': ' + str(error))
                try:
                    if tracker.echild and not tracker.live():
                        break
                except (Exception, KeyboardInterrupt) as error:
                    discovery_errors.append(type(error).__name__ + ': ' + str(error))
                try:
                    # Discovery failure must not skip already proved held
                    # descendants. This still never signals an unknown target.
                    tracker.stop(signal.SIGTERM if time.monotonic() - cleanup_started < .4 else signal.SIGKILL)
                except (Exception, KeyboardInterrupt) as error:
                    discovery_errors.append(type(error).__name__ + ': ' + str(error))
                time.sleep(min(.025, max(0, cleanup_end - time.monotonic())))
            try:
                if not tracker.echild and time.monotonic() < cleanup_end:
                    tracker.discover(cleanup_end); tracker.reap_and_check(cleanup_end)
            except (Exception, KeyboardInterrupt) as error:
                discovery_errors.append(type(error).__name__ + ': ' + str(error))
            if discovery_errors:
                cleanup_error = '; '.join(discovery_errors)
            try:
                live = [row['identity'] for row in tracker.live()]
                subreaper = ctypes.c_int()
                libc = ctypes.CDLL(None, use_errno=True)
                still_subreaper = libc.prctl(37, ctypes.byref(subreaper), 0, 0, 0) == 0 and subreaper.value == 1
            except (Exception, KeyboardInterrupt) as error:
                live = [{'identityUnresolved': True}]
                still_subreaper = False
                cleanup_error = type(error).__name__ + ': ' + str(error)
            complete = tracker.echild and not live and not tracker.violations and cleanup_error is None and still_subreaper
            report['root'].update(exitCode=tracker.root_exit, finishedUtc=tracker.root_finished)
            report['ownedIdentities'] = [dict(identity=row['identity'], origin=row['origin'], signals=row['signals']) for row in tracker.records.values()]
            report['reaped'] = tracker.reaped
            report['cleanup'] = {'complete': complete, 'kernelEchild': tracker.echild, 'heldPidfdsExited': not live,
                                 'subreaperVerifiedAtCompletion': still_subreaper,
                                 'discoveryErrors': discovery_errors,
                                 'liveOwnedIdentities': live, 'violations': tracker.violations, 'error': cleanup_error,
                                 'seconds': time.monotonic() - cleanup_started, 'maximumSeconds': cleanup_limit}
            try:
                tracker.close()
            except OSError as error:
                report['cleanup'].update(complete=False, closeError=str(error))
        else:
            report['cleanup'] = {'complete': False, 'kernelEchild': False, 'error': 'No proved root/tracker boundary established'}
        report['interrupted'] = bool(report.get('interrupted') or flags['cancelled'])
        if report.get('timedOut'):
            code = 124
        elif report.get('interrupted'):
            code = 130
        elif not report['cleanup']['complete'] or report.get('error'):
            code = 125
        else:
            code = report['root']['exitCode']
        report.update(status='owned-descendants-proved-empty' if report['cleanup']['complete'] else 'containment-unproven',
                      outcomeExitCode=code, finishedUtc=utc(), signalTargetScope='held pidfds for proven non-baseline descendants only')
        write_json(result_path, report)
    return int(report['outcomeExitCode'])


class Controller:
    """Lane.child-compatible outer control. It never kills the subreaper group."""
    def __init__(self, policy, repository_root, test_mode=False):
        self.repo = absolute(repository_root)
        self.policy = dict(policy)
        self.helper = absolute(policy['helperPath'])
        self.helper_sha = policy['helperSha256']
        need(digest(self.helper) == self.helper_sha, 'Exact reviewed containment helper required')
        self.source_bytes = self.helper.read_bytes()
        need(hashlib.sha256(self.source_bytes).hexdigest() == self.helper_sha, 'Captured helper raw source differs')
        self.test_mode = bool(test_mode)
        if self.test_mode:
            need(regular(absolute(policy['fixturePath'])) and digest(Path(policy['fixturePath'])) == policy['fixtureSha256'], 'Pinned dummy fixture required')
        else:
            validate_acceptance(policy, self.repo, self.helper_sha)
        self.evidence = absolute(policy['evidenceRoot'])
        need(self.evidence.is_relative_to(self.repo) and not self.evidence.exists()
             and not any(p.is_symlink() for p in (self.evidence, *self.evidence.parents)), 'New regular persistent containment evidence root required')
        if not self.test_mode:
            need(self.evidence.is_relative_to(self.repo / 'Artifacts/Validation/Autonomy'), 'Production control evidence must remain in persistent Autonomy')
        self.started = False
        self.counter = 0
        self.finished_cases = []
        self.copied = {}

    def flush(self, run):
        if run is None:
            return
        run = Path(run)
        need(run.is_dir() and not any(p.is_symlink() for p in (run, *run.parents)), 'Actual run evidence must be regular')
        helper_copy = run / 'RecordedHarness/Tools/Environment/owned-process-containment.py'
        if helper_copy.exists():
            need(digest(helper_copy) == self.helper_sha, 'Recorded executed containment helper differs')
        else:
            helper_copy.parent.mkdir(parents=True, exist_ok=True)
            with helper_copy.open('xb') as stream:
                stream.write(self.source_bytes)
        for folder in self.finished_cases:
            for source in sorted(folder.iterdir()):
                need(regular(source), 'Nonregular containment original')
                destination = run / 'Containment' / folder.name / source.name
                value = digest(source)
                if str(destination) in self.copied:
                    need(digest(destination) == value == self.copied[str(destination)], 'Containment original/copy changed')
                    continue
                destination.parent.mkdir(parents=True, exist_ok=True)
                with source.open('rb') as incoming, destination.open('xb') as outgoing:
                    for block in iter(lambda: incoming.read(1024 * 1024), b''):
                        outgoing.write(block)
                need(digest(destination) == value, 'Containment byte copy differs')
                self.copied[str(destination)] = value

    def child(self, lane, label, argv, cap=900, log=None):
        need(isinstance(label, str) and label and all(c.isalnum() or c in '-_' for c in label), 'Safe unique event label required')
        if self.test_mode:
            verify_test_command(self.policy, argv)
        else:
            validate_acceptance(self.policy, self.repo, self.helper_sha)
        need(digest(self.helper) == self.helper_sha, 'Containment source changed before invocation')
        budget = min(float(cap), lane.remaining())
        execution_end = min(lane.safe_end, time.monotonic() + budget)
        if not self.started:
            self.evidence.mkdir(parents=True, exist_ok=False); self.started = True
        self.counter += 1
        folder = self.evidence / ('%02d-' % self.counter + label)
        folder.mkdir(exist_ok=False)
        self.flush(lane.run)
        parent = process_identity(os.getpid())
        stdout = Path(log) if log is not None else folder / 'actual-command.stdout.log'
        spec = {'version': VERSION, 'mode': 'isolation-test' if self.test_mode else 'production-caller',
                'repositoryRoot': str(self.repo), 'helperSha256': self.helper_sha, 'policy': self.policy,
                'argv': list(argv), 'cwd': str(self.repo), 'stdoutPath': str(stdout),
                'executionEndMonotonic': execution_end, 'cleanupSeconds': 10,
                'parentIdentity': parent}
        spec_path = folder / 'command-spec.json'; write_json(spec_path, spec)
        event = {'label': label, 'argv': list(argv), 'cwd': str(self.repo), 'startedUtc': utc(),
                 'timeoutSeconds': max(0, execution_end - time.monotonic()), 'privateProcessGroup': True,
                 'processContainment': VERSION, 'environmentOverrides': {key: lane.env[key] for key in (
                     'FIRSTGAME_VALIDATION_ROOT', 'FIRSTGAME_BUILD_ROOT', 'PYTHONPYCACHEPREFIX', 'PYTHONDONTWRITEBYTECODE', 'PYTHONOPTIMIZE') if key in lane.env}}
        lane.events.append(event)
        if lane.run:
            write_json(Path(lane.run) / 'Processes' / (label + '.started.json'), event)
        supervisor, supervisor_fd, supervisor_identity = None, None, None
        outer_interrupted, supervisor_done, launch_error = False, False, None
        try:
            need(time.monotonic() < execution_end, 'Shared execution deadline exhausted before supervisor spawn')
            with (folder / 'supervisor.stdout.log').open('xb') as control_log:
                supervisor = subprocess.Popen([sys.executable, str(self.helper), '--supervise', str(spec_path)],
                    cwd=self.repo, env=lane.env, stdout=control_log, stderr=subprocess.STDOUT, start_new_session=True)
                supervisor_identity = process_identity(supervisor.pid)
                need(supervisor_identity['ppid'] == os.getpid() and supervisor_identity['uids'] == parent['uids'], 'New supervisor direct-child identity differs')
                supervisor_fd = open_identity_pidfd(supervisor_identity)
                hard_end = execution_end + 12
                try:
                    supervisor.wait(timeout=max(.01, hard_end - time.monotonic()))
                    supervisor_done = True
                except KeyboardInterrupt:
                    outer_interrupted = True
                except subprocess.TimeoutExpired:
                    pass
                if not supervisor_done:
                    # Request cleanup of this exact supervisor; never SIGKILL
                    # its group while owned descendants might still be alive.
                    signal_identity(supervisor_identity, supervisor_fd, signal.SIGTERM,
                                    parent['uids'][0], {identity_key(supervisor_identity)}, set())
                    try:
                        supervisor.wait(timeout=max(.01, min(hard_end, time.monotonic() + 12) - time.monotonic()))
                        supervisor_done = True
                    except (subprocess.TimeoutExpired, KeyboardInterrupt):
                        pass
        except (Exception, KeyboardInterrupt) as error:
            launch_error = type(error).__name__ + ': ' + str(error)
            if supervisor_fd is not None and not pidfd_exited(supervisor_fd):
                try:
                    signal_identity(supervisor_identity, supervisor_fd, signal.SIGTERM,
                                    parent['uids'][0], {identity_key(supervisor_identity)}, set())
                except (OSError, ValueError):
                    pass
        finally:
            if supervisor_fd is not None:
                event['supervisorStillAlive'] = not pidfd_exited(supervisor_fd)
                os.close(supervisor_fd)
        result_path = folder / 'supervisor-result.json'
        result = None
        if supervisor_done and regular(result_path):
            try:
                result = json.loads(result_path.read_bytes())
                need(result.get('version') == VERSION and result.get('helperSha256') == self.helper_sha
                     and result.get('argv') == list(argv) and result.get('mode') == spec['mode'], 'Actual supervisor receipt differs')
                outcome = int(result['outcomeExitCode'])
                need(supervisor.returncode == (outcome & 255), 'Supervisor process exit and receipt disagree')
                cleanup = result['cleanup']
                proved = (cleanup.get('complete') is True and cleanup.get('kernelEchild') is True
                          and cleanup.get('heldPidfdsExited') is True and cleanup.get('subreaperVerifiedAtCompletion') is True
                          and not cleanup.get('liveOwnedIdentities') and not cleanup.get('violations'))
                need(proved, 'Owned descendants not proved empty; no success/restore may be inferred')
                event.update(exitCode=130 if outer_interrupted else outcome,
                             startedUtc=result['root'].get('startedUtc', event['startedUtc']),
                             pid=result['root'].get('pid'), containmentProvedEmpty=True,
                             containmentReceipt=fullref(result_path, self.repo))
                self.finished_cases.append(folder)
            except (Exception, KeyboardInterrupt) as error:
                launch_error = type(error).__name__ + ': ' + str(error)
        if 'exitCode' not in event:
            event.update(exitCode=130 if outer_interrupted else 125, containmentProvedEmpty=False,
                         cleanupIncomplete=True, containmentEvidence=str(folder))
            if regular(result_path):
                event['containmentReceipt'] = fullref(result_path, self.repo)
        if launch_error:
            event['containmentError'] = launch_error
        event['finishedUtc'] = utc()
        if regular(stdout):
            if log is None:
                lane.logs.append((label + '.log', stdout.read_bytes()))
            else:
                event['log'] = fullref(stdout, self.repo)
        if lane.run:
            write_json(Path(lane.run) / 'Processes' / (label + '.finished.json'), event)
            self.flush(lane.run)
        need(event.get('containmentProvedEmpty') is True, 'Process containment unproven; preserve originals and stop')
        return event


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--supervise', type=Path, required=True)
    args = parser.parse_args()
    return supervise(args.supervise)


if __name__ == '__main__':
    raise SystemExit(main())
