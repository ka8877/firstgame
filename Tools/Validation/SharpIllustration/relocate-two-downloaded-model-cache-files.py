#!/usr/bin/env python3
"""Relocate only two pinned public downloaded model-cache files to shared scratch.

This is not evidence preservation or model synthesis. Only selected cache inode numbers change; original file pathnames remain
readable through two explicit absolute per-file symlinks. Other twelve files and
all directory inodes/modes/owners remain; containing-directory entry timestamps
change and are recorded. Root must first prove scratch persistence in two independent calls.
The ephemeral cache can be restored using the pinned download manifests.
Root alone executes after full source admission and an actual idle check.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import stat
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/SharpIllustration-2026-11-17'
SELF = ROOT / 'Tools/Validation/SharpIllustration/relocate-two-downloaded-model-cache-files.py'
ALLOW = ROOT / 'Tools/Validation/SharpIllustration/two-downloaded-model-cache-files-allowlist.json'
ALLOW_SHA = '16ebb0f138e4cd2b5ee0df1e1d700394d00340fddc6bc68d0a2f4cc7028434f5'
ADMISSION = CONTROL / 'Release/two-downloaded-model-cache-files-source-admission02.json'
PROC_OBSERVATION = CONTROL / 'Release/model-cache-process-observation01.json'
PROC_OBSERVATION_SHA = '11bd12da5216504f40b995dd650079ecade02e8e6634ff0f17b76dfce60e8028'
BASELINE_SERVICES = (
    {'pid': 1, 'ppid': 0, 'comm': 'tail', 'uid': 1000, 'startTicks': 29},
    {'pid': 2, 'ppid': 1, 'comm': 'cloud-agent-log', 'uid': 1000, 'startTicks': 43},
    {'pid': 3, 'ppid': 2, 'comm': 'caas-prefix-tra', 'uid': 1000, 'startTicks': 47},
)
WRAPPER = CONTROL / 'run-owned-sharp-illustration-current.py'
WRAPPER_SHA = '0615afae26319b6bd9783d8481c37f1711665228815ae8123b4b6592b11eb519'
SOURCE = Path('/workspace/tools/voice-models')
DEST = Path('/tmp/firstgame-sharp-illustration-20261117-voice-models01')
GIB = 1024 ** 3
EXPECTED_BYTES = 691407650
CACHE_TOTAL_BYTES = 1175821362
SCRATCH_RESERVE = 128 * 1024 ** 2
SELECTED = ('MeloTTS-Korean/checkpoint.pth', 'faster-whisper-small/model.bin')
ENV = {
    'FIRSTGAME_RELEASE_PROFILE': 'sharp-illustration',
    'FIRSTGAME_CONTROL_ROOT': str(CONTROL),
    'FIRSTGAME_VALIDATION_ROOT': str(ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'),
    'FIRSTGAME_BUILD_ROOT': '/tmp/firstgame-sharp-illustration-20261117-builds',
    'FIRSTGAME_PROOF_ROOT': str(CONTROL / 'BuildProofCurrent'),
    'FIRSTGAME_STAGE_ROOT': str(ROOT / 'Artifacts/SharpIllustrationWebPlaytest'),
}


def need(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def attrs(path):
    s = path.lstat()
    return {'dev': s.st_dev, 'ino': s.st_ino, 'bytes': s.st_size,
            'mode': stat.S_IMODE(s.st_mode), 'uid': s.st_uid, 'gid': s.st_gid,
            'mtimeNs': s.st_mtime_ns, 'ctimeNs': s.st_ctime_ns, 'nlink': s.st_nlink}


def regular(path):
    need(path.is_absolute() and not any(p.is_symlink() for p in (path, *path.parents))
         and stat.S_ISREG(path.lstat().st_mode), 'Regular non-symlink file required: ' + str(path))
    return path


def digest(path, expected=None):
    regular(path)
    before = attrs(path)
    if expected is not None:
        need(before == expected, 'Source/cache metadata changed: ' + str(path))
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    need(attrs(path) == before, 'File changed while hashing: ' + str(path))
    return h.hexdigest()


def ref(path):
    return {'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size,
            'sha256': digest(path)}


def checked(row):
    path = ROOT / row['file']
    need(path.is_relative_to(ROOT) and '..' not in path.parts and ref(path) == row,
         'Explicit source reference differs')
    return path


def read(path):
    return json.loads(regular(path).read_bytes())


def sync_dir(path):
    descriptor = os.open(path, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def write(path, value):
    with path.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
    sync_dir(path.parent)


def space(path):
    s = os.statvfs(path)
    return s.f_bavail * s.f_frsize


def memory_capacity(remaining_bytes=EXPECTED_BYTES):
    fields = {}
    for line in Path('/proc/meminfo').read_text().splitlines():
        name, value = line.split(':', 1)
        if value.strip().endswith(' kB'):
            fields[name] = int(value.split()[0]) * 1024
    available = fields['MemAvailable']
    need(available > remaining_bytes + GIB,
         'Keep remaining model payload plus1GiB actual MemAvailable reserve')
    cgroup = {}
    for name in ('memory.max', 'memory.current'):
        path = Path('/sys/fs/cgroup') / name
        if path.is_file():
            cgroup[name] = path.read_text().strip()
    if cgroup.get('memory.max', 'max') != 'max':
        need(int(cgroup['memory.max']) - int(cgroup['memory.current']) > remaining_bytes + 512 * 1024 ** 2,
             'Keep remaining model payload plus512MiB actual cgroup memory reserve')
    need(space(DEST.parent) > remaining_bytes + SCRATCH_RESERVE,
         'Shared scratch must retain remaining payload plus128MiB reserve')
    return {'memAvailableBytes': available, 'cgroup': cgroup, 'remainingPayloadBytes': remaining_bytes,
            'scratchAvailableBytes': space(DEST.parent), 'workspaceAvailableBytes': space(ROOT)}


def process_identity(process):
    fields = (process / 'stat').read_text().rsplit(') ', 1)[1].split()
    return {'pid': int(process.name), 'ppid': int(fields[1]), 'state': fields[0],
            'startTicks': int(fields[19]), 'uid': process.stat().st_uid,
            'comm': (process / 'comm').read_text().rstrip('\n')}


def idle_readers(*roots, service_witness):
    """Record bounded visibility limits, never invent a PID1/global-reader proof."""
    need(service_witness.get('actualRootObservation', {}).get('sha256') == PROC_OBSERVATION_SHA,
         'Actual Root observation and owned-supervisor baseline witness required')
    found, failures, limitations, zombies = [], [], [], []
    current = os.getpid()
    ancestors, parent, boundary = {}, os.getppid(), None
    for _ in range(128):
        need(parent > 0 and parent not in ancestors, 'Visible ancestor chain must not be truncated or cyclic')
        identity = process_identity(Path('/proc') / str(parent))
        ancestors[parent] = identity
        if identity['ppid'] == 0:
            boundary = {'kind': 'actual-visible-chain-ends-at-parent-zero',
                        'terminalIdentity': identity, 'pid1InVisibleAncestors': 1 in ancestors}
            break
        parent = identity['ppid']
    need(boundary is not None, 'Complete visible chain must end at an actually observed PPID0 boundary')
    for identity in ancestors.values():
        actual = process_identity(Path('/proc') / str(identity['pid']))
        need(all(actual[k] == identity[k] for k in ('pid', 'ppid', 'uid', 'startTicks', 'comm')),
             'Visible ancestor identity changed during observation')
    services = {row['pid']: row for row in BASELINE_SERVICES}
    for expected in BASELINE_SERVICES:
        actual = process_identity(Path('/proc') / str(expected['pid']))
        need(all(actual[k] == expected[k] for k in expected), 'Pinned preexisting service identity changed')
    for process in Path('/proc').iterdir():
        if not process.name.isdigit() or int(process.name) == current:
            continue
        try:
            identity = process_identity(process)
            if identity['uid'] != os.geteuid():
                continue
            if identity['state'] in ('Z', 'X'):
                zombies.append(identity); continue
            for kind in ('fd', 'maps'):
                try:
                    if kind == 'fd':
                        for fd in (process / 'fd').iterdir():
                            try:
                                target = os.readlink(fd)
                            except FileNotFoundError:
                                continue
                            if any(target == str(root) or target.startswith(str(root) + '/') for root in roots):
                                found.append({'identity': identity, 'fd': fd.name, 'path': target})
                    else:
                        for line in (process / 'maps').read_text().splitlines():
                            if any(str(root) + '/' in line for root in roots):
                                found.append({'identity': identity, 'mapping': line})
                except PermissionError as error:
                    observation = {'identity': identity, 'kind': kind, 'error': str(error)}
                    ancestor = ancestors.get(identity['pid'])
                    if ancestor is not None and ancestor['startTicks'] == identity['startTicks']:
                        limitations.append({**observation, 'scope': 'exact-current-visible-ancestor'})
                    elif identity['pid'] in services and all(identity[k] == services[identity['pid']][k]
                                                           for k in services[identity['pid']]):
                        limitations.append({**observation, 'scope': 'exact-three-pinned-preexisting-nonvoice-services'})
                    else:
                        failures.append(observation)
            need(process_identity(process)['startTicks'] == identity['startTicks'], 'Process PID reused during reader observation')
        except FileNotFoundError:
            continue
        except PermissionError as error:
            failures.append({'pid': int(process.name), 'error': str(error)})
    need(not found and not failures, 'Cache readers/mappings or inaccessible non-ancestor: ' + json.dumps({'found': found, 'failures': failures}))
    return {'sameUidObservedCacheReaders': found, 'inaccessibleUnadmittedNonAncestorErrors': failures,
            'boundedInaccessibleObservations': limitations, 'kernelZombiesWithoutFdsOrMappings': zombies,
            'actualAncestors': list(ancestors.values()), 'actualVisibleBoundary': boundary,
            'serviceWitness': service_witness, 'completeProcessInspection': not limitations,
            'limits': 'Only same-UID processes are considered. Inaccessible exact current ancestors and only three exact Root-observed supervisor-baseline services are bounded unknown FD/mapping observations, never proof of no readers. All other inaccessible processes and any positive cache reader reject. Runtime-idle and immutable byte-copy/atomic-path replacement remain independently required; existing unknown readers retain old inode bytes.'}


def baseline_service_witness(idle, admission):
    need(checked(admission['procObservationReceipt']) == PROC_OBSERVATION
         and digest(PROC_OBSERVATION) == PROC_OBSERVATION_SHA, 'Exact actual Root process observation required')
    observation = read(PROC_OBSERVATION)
    observed = observation.get('baselineServices', [])
    need(len(observed) == 3 and all(any(all(row.get(k) == expected[k] for k in expected)
         and row.get('sameActualSupervisorBaseline') is True for row in observed) for expected in BASELINE_SERVICES),
         'Actual Root observation must bind the exact three preexisting services')
    current_receipt_path = checked(idle['containmentReceipt'])
    current_receipt = read(current_receipt_path)
    prior = observation['sourceFailedSupervisor']
    prior_path = regular(ROOT / prior['file'])
    need(digest(prior_path) == prior['sha256'], 'Actual failed command supervisor witness differs')
    prior_receipt = read(prior_path)
    for receipt in (current_receipt, prior_receipt):
        for expected in BASELINE_SERVICES:
            matches = [row for row in receipt.get('baselineIdentities', []) if row.get('pid') == expected['pid']]
            need(len(matches) == 1 and all(matches[0].get(k) == expected[k] for k in ('pid', 'ppid', 'startTicks'))
                 and matches[0].get('uids') == [expected['uid']] * 4,
                 'Both actual owned-supervisor baselines must contain each exact preexisting service')
    return {'actualRootObservation': ref(PROC_OBSERVATION),
            'currentIndependentIdleSupervisor': ref(current_receipt_path),
            'actualPriorFailedCommandSupervisor': ref(prior_path), 'exactPreexistingServices': list(BASELINE_SERVICES)}


def tree(root):
    need(not root.is_symlink() and stat.S_ISDIR(root.lstat().st_mode), 'Real cache directory required')
    files, directories = {}, {}
    for current, children, names in os.walk(root, followlinks=False):
        parent = Path(current)
        need(not parent.is_symlink(), 'Directory symlink forbidden')
        relative = '.' if parent == root else parent.relative_to(root).as_posix()
        directories[relative] = attrs(parent)
        for name in children:
            child = parent / name
            need(not child.is_symlink() and stat.S_ISDIR(child.lstat().st_mode), 'Special directory member forbidden')
        for name in names:
            child = regular(parent / name)
            files[child.relative_to(root).as_posix()] = attrs(child)
    return {'files': files, 'directories': directories}


def invariant(a, b):
    return all(a[k] == b[k] for k in ('bytes', 'mode', 'uid', 'gid', 'mtimeNs'))


def temporary_link(name):
    path = SOURCE / name
    return path.with_name('.' + path.name + '.firstgame-two-cache-20261117.link01')


def verify_target(name, observed):
    target = DEST / name
    need(invariant(attrs(target), observed['before'])
         and digest(target) == observed['sha256'], 'Copied pinned model bytes/metadata differ: ' + name)
    return attrs(target)


def logical_digest(name, expected_target_metadata):
    path = SOURCE / name
    need(path.is_symlink() and os.readlink(path) == str(DEST / name)
         and path.resolve() == DEST / name
         and not any(p.is_symlink() for p in path.parents),
         'Only the exact selected per-file cache link is permitted')
    h = hashlib.sha256()
    with path.open('rb') as stream:
        metadata = os.fstat(stream.fileno())
        need((metadata.st_dev, metadata.st_ino) ==
             (expected_target_metadata['dev'], expected_target_metadata['ino']),
             'Logical cache file must open the verified new target inode')
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    need(attrs(DEST / name) == expected_target_metadata
         and path.is_symlink() and os.readlink(path) == str(DEST / name),
         'Logical model target/link changed during streaming readthrough')
    return h.hexdigest()


def verify_source_cache(observations, before_directories, replaced, target_metadata):
    """Other twelve files and every directory inode remain; only2 link types change."""
    need(not SOURCE.is_symlink() and SOURCE.is_dir(), 'Original model-cache directory remains a real directory')
    files, directories, links = {}, {}, {}
    for current, children, names in os.walk(SOURCE, followlinks=False):
        parent = Path(current)
        need(not parent.is_symlink(), 'Cache directory symlinks are forbidden')
        relative = '.' if parent == SOURCE else parent.relative_to(SOURCE).as_posix()
        directories[relative] = attrs(parent)
        for name in children:
            child = parent / name
            need(not child.is_symlink() and child.is_dir(), 'Cache directory topology changed')
        for name in names:
            child = parent / name; relative_file = child.relative_to(SOURCE).as_posix()
            need(relative_file in observations, 'Unlisted source cache pathname')
            observed = observations[relative_file]
            if relative_file in replaced:
                need(child.is_symlink() and os.readlink(child) == str(DEST / relative_file), 'Selected link differs')
                need(logical_digest(relative_file, target_metadata[relative_file]) == observed['sha256'],
                     'Selected logical model SHA differs')
                links[relative_file] = {'lstat': attrs(child), 'target': os.readlink(child),
                                        'logicalSha256': observed['sha256']}
            else:
                need(not child.is_symlink() and attrs(child) == observed['before']
                     and digest(child, observed['before']) == observed['sha256'],
                     'A retained cache file changed: ' + relative_file)
            files[relative_file] = attrs(child)
    need(set(files) == set(observations) and set(directories) == set(before_directories),
         'Exact fourteen-file/seven-directory original cache topology required')
    changed_parents = {str(Path(name).parent) for name in replaced}
    for name, before in before_directories.items():
        after = directories[name]
        need(all(after[k] == before[k] for k in ('dev', 'ino', 'mode', 'uid', 'gid', 'nlink')),
             'All original directory inodes/modes/owners/link counts must remain')
        if name not in changed_parents:
            need(after == before, 'Unchanged cache directory metadata differs: ' + name)
    return {'files': files, 'directories': directories, 'selectedFileLinks': links,
            'containingDirectoryEntryTimestampChangesPermitted': sorted(changed_parents)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-run-label', required=True)
    parser.add_argument('--allowlist', type=Path, required=True)
    parser.add_argument('--source-admission', type=Path, required=True)
    parser.add_argument('--confirm-two-reproducible-cache-files-only', action='store_true', required=True)
    args = parser.parse_args()
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0'
         and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.owned_run_label), 'Assertions and safe unique label required')
    need(Path(__file__).absolute() == SELF and args.allowlist == ALLOW
         and args.source_admission == ADMISSION and digest(ALLOW) == ALLOW_SHA,
         'Exact installed helper, allowlist and separate Root admission required')
    need(all(os.environ.get(k) == v for k, v in ENV.items()) and digest(WRAPPER) == WRAPPER_SHA,
         'Exact accepted current owner wrapper/environment required')
    admission = read(ADMISSION)
    need(admission.get('schema') == 1 and admission.get('reviewer') == '/root'
         and admission.get('status') == 'reviewed-and-admitted-for-two-downloaded-cache-file-relocation'
         and admission.get('readerInspectionPolicy') == 'visible-ppid0-ancestors-and-three-pinned-service-fd-maps-limitations'
         and checked(admission['helper']) == SELF and checked(admission['allowlist']) == ALLOW,
         'Actual Root source review of only these two downloaded model files required')
    reviewed = datetime.fromisoformat(admission['reviewedUtc'])
    need(reviewed.utcoffset() is not None and reviewed <= datetime.now(timezone.utc), 'Actual aware Root review time required')
    persistence = admission.get('sharedScratchPersistence')
    need(isinstance(persistence, dict) and persistence.get('destinationParent') == str(DEST.parent)
         and persistence.get('twoIndependentToolCallsVerified') is True,
         'Root must separately observe shared scratch persistence in two independent calls')
    checked(persistence['receipt'])
    run = CONTROL / 'CommandRuns' / args.owned_run_label
    invocation = read(run / 'invocation.json')
    need(invocation.get('requestedArgv') == ['python3', str(SELF), *sys.argv[1:]]
         and invocation.get('display') is False and not (run / 'summary.json').exists(),
         'Actual current wrapper invocation required')
    lease_raw = regular(CONTROL / 'lease.json').read_bytes()
    need(checked(invocation['ownedLeaseSnapshot']).read_bytes() == lease_raw, 'Actual acquired lease snapshot differs')
    lease = json.loads(lease_raw)
    deadline = datetime.fromisoformat(lease['lease']['deadlineUtc'])
    need(lease.get('state') == 'acquired' and deadline.utcoffset() is not None, 'Acquired fixed ownership required')
    def controls():
        need(regular(CONTROL / 'lease.json').read_bytes() == lease_raw
             and (deadline - datetime.now(timezone.utc)).total_seconds() > 120,
             'Actual lease/cleanup reserve changed')
        need(space(ROOT) > 4 * 1024 ** 2, 'Retain4MiB workspace reporting reserve')
    for original in (SELF, ALLOW, ADMISSION):
        need(digest(run / 'RecordedHarness' / original.relative_to(ROOT)) == digest(original),
             'Wrapper must capture exact helper/allowlist/admission')
    idle = read(run / 'Processes/idleBefore.finished.json')
    need(idle.get('exitCode') == 0 and idle.get('containmentProvedEmpty') is True,
         'Actual independent successful idle receipt required')
    service_witness = baseline_service_witness(idle, admission)
    found, pid = False, os.getppid()
    for _ in range(64):
        if pid <= 1:
            break
        process = Path('/proc') / str(pid)
        words = (process / 'cmdline').read_bytes().decode(errors='replace').split('\0')
        found |= any(word == str(WRAPPER) and i + 1 < len(words) and words[i + 1] == args.owned_run_label for i, word in enumerate(words))
        pid = int((process / 'stat').read_text().rsplit(') ', 1)[1].split()[1])
    need(found, 'Actual accepted wrapper ancestry required')
    controls()
    for argv in ([sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(CONTROL / 'lease.json')],
                 [sys.executable, 'Tools/Environment/assert-runtime-idle.py']):
        subprocess.run(argv, cwd=ROOT, check=True, timeout=45)
    allow = read(ALLOW)
    need(allow['source'] == str(SOURCE) and allow['destination'] == str(DEST)
         and allow['count'] == len(allow['files']) == 14 and len(allow['directories']) == 7
         and allow['bytes'] == sum(x['observed']['bytes'] for x in allow['files']) == CACHE_TOTAL_BYTES
         and allow['selectedRelativeNames'] == list(SELECTED) and allow['selectedCount'] == 2
         and allow['selectedBytes'] == EXPECTED_BYTES, 'Exact full-cache and selected-two contract required')
    for row in allow['provenance']:
        checked(row)
    need(not os.path.lexists(DEST) and all(not os.path.lexists(temporary_link(name)) for name in SELECTED),
         'Every new destination/link path must be absent; no automatic retry/overwrite')
    need(not any(p.is_symlink() for p in (SOURCE, *SOURCE.parents, DEST.parent, *DEST.parent.parents)),
         'Original cache directories and shared scratch parent must remain real paths')
    before = tree(SOURCE)
    expected_files = {x['relative']: x for x in allow['files']}
    before_dirs = {x['relative']: x['observed'] for x in allow['directories']}
    need(before['files'] == {n: x['observed'] for n, x in expected_files.items()}
         and before['directories'] == before_dirs, 'Original full cache metadata/topology differs')
    need(space(ROOT) > 8 * 1024 ** 2, 'Retain8MiB workspace reserve before operation')
    observations = {}
    for name, row in expected_files.items():
        need(not Path(name).is_absolute() and '..' not in Path(name).parts
             and row['observed']['uid'] == os.geteuid() and row['observed']['mode'] == 0o600
             and row['observed']['nlink'] == 1 and 0 < row['observed']['ino'] < 2 ** 63,
             'Only the exact owned downloaded regular cache files permitted')
        observed_hash = digest(SOURCE / name, row['observed'])
        need(observed_hash == row['expectedSha256'], 'Actual cached file differs from pinned public source: ' + name)
        observations[name] = {'before': row['observed'], 'sha256': observed_hash}
    need(sum(observations[name]['before']['bytes'] for name in SELECTED) == EXPECTED_BYTES,
         'Only the exact two selected file sizes are admitted')
    capacity, readers = memory_capacity(), idle_readers(SOURCE, DEST, service_witness=service_witness)
    ledger = run / 'TwoDownloadedModelCacheFiles'; ledger.mkdir(exist_ok=False)
    write(ledger / 'before.json', {'status': 'two-cache-files-only-before-copy', 'utc': utc(),
        'allowlist': ref(ALLOW), 'rootAdmission': ref(ADMISSION), 'actualLease': ref(CONTROL / 'lease.json'),
        'sourceTree': before, 'allFourteenFileHashes': observations, 'selected': list(SELECTED),
        'capacity': capacity, 'readerCheck': readers,
        'limits': 'Only two external downloaded cache file inodes will change. Other twelve files and every directory inode/mode/owner remain; two parent entry timestamps necessarily change.'})
    phase, copied, replaced, active_link = 'before-target-create', [], [], None
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    target_metadata = {}
    try:
        controls(); DEST.mkdir(mode=0o700, exist_ok=False); phase = 'copying-only-two-selected-files'
        for name in SELECTED:
            (DEST / name).parent.mkdir(mode=0o700, exist_ok=False)
        copied_bytes = 0
        for name in SELECTED:
            controls(); memory_capacity(EXPECTED_BYTES - copied_bytes)
            original, source, target = observations[name], SOURCE / name, DEST / name
            need(attrs(source) == original['before'], 'Selected source changed before copy')
            h = hashlib.sha256()
            with source.open('rb') as payload, target.open('xb') as output:
                os.fchmod(output.fileno(), original['before']['mode'])
                for block in iter(lambda: payload.read(1024 * 1024), b''):
                    output.write(block); h.update(block)
                output.flush(); os.fsync(output.fileno())
            need(h.hexdigest() == original['sha256'] and attrs(source) == original['before'], 'Selected source changed during copy')
            os.utime(target, ns=(target.stat().st_atime_ns, original['before']['mtimeNs']))
            descriptor = os.open(target, os.O_RDONLY)
            try:
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
            target_metadata[name] = verify_target(name, original)
            sync_dir(target.parent); copied.append(name); copied_bytes += original['before']['bytes']
        sync_dir(DEST); sync_dir(DEST.parent)
        need(tree(SOURCE) == before, 'Entire original cache must remain intact before first replacement')
        expected_destination_dirs = {'.', *(str(Path(name).parent) for name in SELECTED)}
        need(set(tree(DEST)['files']) == set(SELECTED)
             and set(tree(DEST)['directories']) == expected_destination_dirs,
             'New destination must contain exactly two selected files and their three directories')
        for name in SELECTED:
            need(verify_target(name, observations[name]) == target_metadata[name], 'Copied target changed')
        controls(); readers = idle_readers(SOURCE, DEST, service_witness=service_witness)
        write(ledger / 'copy-complete.json', {'status': 'two-complete-copy-sha-fsync-readbacks-original-cache-intact',
            'utc': utc(), 'destinationTree': tree(DEST), 'targetMetadata': target_metadata,
            'capacity': memory_capacity(0), 'readerCheck': readers})
        for number, name in enumerate(SELECTED):
            controls(); original, source, target = observations[name], SOURCE / name, DEST / name
            need(attrs(source) == original['before'] and digest(source, original['before']) == original['sha256']
                 and attrs(target) == target_metadata[name] and digest(target, target_metadata[name]) == original['sha256'],
                 'Both source and fully copied target must remain exact immediately before replacement')
            readers = idle_readers(SOURCE, DEST, service_witness=service_witness)
            link = temporary_link(name); need(not os.path.lexists(link), 'Fresh selected-file link path required')
            write(ledger / ('file-%02d-before.json' % number), {'utc': utc(), 'selected': name,
                'sourceBefore': original, 'targetMetadata': target_metadata[name], 'targetSha256': original['sha256'],
                'newLinkPath': str(link), 'readerCheck': readers, 'workspaceAvailableBytesBefore': space(ROOT)})
            os.symlink(str(target), link); active_link = str(link); sync_dir(link.parent)
            need(link.is_symlink() and os.readlink(link) == str(target)
                 and attrs(source) == original['before'], 'Only the new exact per-file link is ready')
            phase = 'immediately-before-atomic-selected-file-replacement'
            os.replace(link, source); active_link = None; replaced.append(name); sync_dir(source.parent)
            phase = 'selected-file-replaced-and-parent-synced'
            need(logical_digest(name, target_metadata[name]) == original['sha256'], 'Actual selected logical file readthrough differs')
            write(ledger / ('file-%02d-after.json' % number), {'utc': utc(), 'selected': name,
                'sourcePathAfter': {'lstat': attrs(source), 'target': os.readlink(source)},
                'retainedTargetMetadata': attrs(target), 'logicalSha256': original['sha256'],
                'workspaceAvailableBytesAfter': space(ROOT), 'selectedOriginalCacheInodeRetained': False})
        controls()
        final_source = verify_source_cache(observations, before_dirs, set(replaced), target_metadata)
        need(set(tree(DEST)['files']) == set(SELECTED)
             and set(tree(DEST)['directories']) == expected_destination_dirs
             and all(not os.path.lexists(temporary_link(name)) for name in SELECTED), 'Final destination/link path set differs')
        for row in allow['provenance']:
            checked(row)
        write(ledger / 'completed.json', {'status': 'two-public-downloaded-cache-files-relocated-and-full-cache-verified',
            'completedUtc': utc(), 'selected': list(SELECTED), 'bytesCopiedAndVerified': EXPECTED_BYTES,
            'filesCopied': copied, 'originalCachePathsAtomicallyReplaced': replaced,
            'sourceCacheAfter': final_source, 'destinationTree': tree(DEST),
            'workspaceAvailableBytesAfter': space(ROOT), 'scratchAvailableBytesAfter': space(DEST.parent),
            'otherTwelveFileBytesInodesModesOwnersMtimesRetained': True,
            'allSevenDirectoryInodesModesOwnersRetained': True,
            'selectedOriginalCacheInodesRetained': False,
            'existingProjectAssetsEvidenceProtectedBeeOrGitObjectsReplacedOrDeleted': False,
            'publishReady': False, 'durability': allow['durability'],
            'restore': 'If a selected exact model-cache symlink dangles after environment reset, Tools/Voice/prepare-runtime.py --with-asr downloads to a new .download file and atomically replaces only that selected pathname after pinned SHA/size verification. Other twelve cache files/directories are retained.'})
        print(json.dumps({'status': 'two-reproducible-model-cache-files-relocated', 'receipt': ref(ledger / 'completed.json')}))
    except BaseException as error:
        write(ledger / 'failure.json', {'status': 'failed-no-automatic-retry-or-rollback', 'utc': utc(),
            'phase': phase, 'error': type(error).__name__ + ': ' + str(error),
            'filesCopied': copied, 'selectedPathsAlreadyReplaced': replaced,
            'activeNewTemporaryLink': active_link,
            'selectedActualPaths': {name: {'lstat': attrs(SOURCE / name),
                'isSymlink': (SOURCE / name).is_symlink(),
                'linkTarget': os.readlink(SOURCE / name) if (SOURCE / name).is_symlink() else None} for name in SELECTED},
            'destinationExists': os.path.lexists(DEST), 'workspaceAvailableBytesAfter': space(ROOT),
            'limits': 'All copied target bytes remain. Replaced paths already point to fully verified copies; unprocessed source paths remain original. Only a new owned temporary link may remain for Root inspection. No folder rename, broad cleanup or automatic rollback occurs.'})
        raise


if __name__ == '__main__':
    main()


