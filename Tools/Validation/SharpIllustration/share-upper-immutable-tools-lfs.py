#!/usr/bin/env python3
"""Share only22 frozen upper-inode Tools originals with their LFS cache aliases.

No original source pathname/inode/content/mode/owner/mtime is replaced. Only an
independent duplicate cache inode is atomically replaced after index, bytes,
inode, ownership, idle and space checks. Lower-origin candidates are forbidden.
Root alone executes; metadata selection does not prove equality or reclaimed
space. Every source/cache alias must remain immutable after this operation.
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
SELF = ROOT / 'Tools/Validation/SharpIllustration/share-upper-immutable-tools-lfs.py'
ALLOW = ROOT / 'Tools/Validation/SharpIllustration/immutable-upper-tools-lfs-allowlist.json'
ALLOW_SHA = '41e4e5c0f66fdb1b7385663c6f5e53ac35494df6a97218c3683d21c0d0067585'
WRAPPER = CONTROL / 'run-owned-sharp-illustration-current.py'
ENV = {'FIRSTGAME_RELEASE_PROFILE': 'sharp-illustration', 'FIRSTGAME_CONTROL_ROOT': str(CONTROL),
       'FIRSTGAME_VALIDATION_ROOT': str(ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'),
       'FIRSTGAME_BUILD_ROOT': '/tmp/firstgame-sharp-illustration-20261117-builds',
       'FIRSTGAME_PROOF_ROOT': str(CONTROL / 'BuildProofCurrent'),
       'FIRSTGAME_STAGE_ROOT': str(ROOT / 'Artifacts/SharpIllustrationWebPlaytest')}
GIT_REDIRECTIONS = ('GIT_DIR', 'GIT_COMMON_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE',
                    'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_QUARANTINE_PATH')
RESERVE = 8 * 1024 ** 2


def need(value, message):
    if not value:
        raise ValueError(message)


def safe(value):
    p = Path(value); p = p if p.is_absolute() else ROOT / p
    need(p.is_relative_to(ROOT) and '..' not in p.parts and p.absolute() == p
         and not any(x.is_symlink() for x in (p, *p.parents)), 'Regular fixed workspace path required: ' + str(p))
    return p


def attrs(p):
    s = safe(p).lstat(); need(stat.S_ISREG(s.st_mode), 'Regular original/cache required')
    return {'dev': s.st_dev, 'ino': s.st_ino, 'bytes': s.st_size, 'mode': stat.S_IMODE(s.st_mode),
            'uid': s.st_uid, 'gid': s.st_gid, 'mtimeNs': s.st_mtime_ns, 'ctimeNs': s.st_ctime_ns,
            'nlink': s.st_nlink, 'blocks': s.st_blocks}


def ref(p):
    p = safe(p); before = attrs(p); h = hashlib.sha256()
    with p.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    need(attrs(p) == before, 'Original changed during byte observation')
    return {'file': str(p.relative_to(ROOT)), 'bytes': before['bytes'], 'sha256': h.hexdigest()}


def read(p):
    attrs(p)
    return json.loads(safe(p).read_bytes())


def checked(row):
    p = safe(row['file']); need(ref(p) == row, 'Explicit original reference differs: ' + str(p))
    return p


def sync_dir(p):
    fd = os.open(safe(p), os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def write(p, value):
    safe(p.parent)
    with p.open('x') as stream:
        json.dump(value, stream, ensure_ascii=False, separators=(',', ':'))
        stream.write('\n'); stream.flush(); os.fsync(stream.fileno())
    sync_dir(p.parent)


def available():
    s = os.statvfs(ROOT)
    return s.f_bavail * s.f_frsize


def good(event):
    return event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True and event.get('supervisorStillAlive') is False


def owned(label, requested):
    need(Path(__file__).absolute() == SELF and all(os.environ.get(k) == v for k, v in ENV.items()), 'Exact owned current source/environment required')
    raw = safe(CONTROL / 'lease.json').read_bytes(); lease = json.loads(raw)
    need(lease.get('state') == 'acquired' and lease.get('lease', {}).get('runId') == CONTROL.name
         and re.fullmatch(r'[0-9a-f]{40}', lease.get('commitSha', '')), 'Actual acquired current lease required')
    deadline = datetime.fromisoformat(lease['lease']['deadlineUtc'])
    need(deadline.utcoffset() is not None and (deadline - datetime.now(timezone.utc)).total_seconds() > 180, 'Actual lease reserve exhausted')
    run = CONTROL / 'CommandRuns' / label; invocation = read(run / 'invocation.json')
    need(invocation.get('requestedArgv') == requested and invocation.get('label') == label
         and invocation.get('scope') == 'generic-owned-command-only' and not (run / 'summary.json').exists()
         and checked(invocation['ownedLeaseSnapshot']).read_bytes() == raw, 'Actual live invocation/lease snapshot required')
    idle = read(run / 'Processes/idleBefore.finished.json')
    need(good(idle) and idle.get('argv', [])[1:] == ['Tools/Environment/assert-runtime-idle.py'], 'Actual independent idle required')
    found, pid = False, os.getppid()
    for _ in range(64):
        if pid <= 1:
            break
        proc = Path('/proc') / str(pid); words = proc.joinpath('cmdline').read_bytes().decode(errors='replace').split('\0')
        found |= any(x == str(WRAPPER) and i + 1 < len(words) and words[i + 1] == label for i, x in enumerate(words))
        pid = int(proc.joinpath('stat').read_text().split(') ', 1)[1].split()[1])
    need(found and ref(run / 'RecordedHarness' / SELF.relative_to(ROOT))['sha256'] == ref(SELF)['sha256']
         and ref(run / 'RecordedHarness' / ALLOW.relative_to(ROOT))['sha256'] == ALLOW_SHA, 'Actual wrapper ancestry/captured helper/allowlist differs')
    return run, raw, deadline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-run-label', required=True)
    parser.add_argument('--allowlist', type=Path, required=True)
    parser.add_argument('--confirm-upper-originals-and-cache-aliases-remain-immutable', action='store_true', required=True)
    args = parser.parse_args()
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0'
         and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.owned_run_label), 'Assertions/safe unique label required')
    need(args.allowlist == ALLOW and ref(ALLOW)['sha256'] == ALLOW_SHA, 'Exact frozen explicit allowlist required')
    need(not any(k in os.environ for k in GIT_REDIRECTIONS), 'Inherited Git context/object redirects forbidden')
    run, lease_raw, deadline = owned(args.owned_run_label, ['python3', str(SELF), *sys.argv[1:]])
    def controls():
        need(safe(CONTROL / 'lease.json').read_bytes() == lease_raw
             and (deadline - datetime.now(timezone.utc)).total_seconds() > 90, 'Actual acquired lease/cleanup reserve changed')
    for argv in ([sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(CONTROL / 'lease.json')],
                 [sys.executable, 'Tools/Environment/assert-runtime-idle.py']):
        subprocess.run(argv, cwd=ROOT, check=True, timeout=45)
    allow = read(ALLOW); items = allow['files']
    need(allow.get('schema') == 1 and allow.get('count') == len(items) == 22
         and sum(x['bytes'] for x in items) == allow.get('declaredLogicalDuplicateBytes') == 130148333
         and len({x['source'] for x in items}) == len({x['oid'] for x in items}) == len({x['cache'] for x in items}) == 22
         and items == sorted(items, key=lambda x: (x['bytes'], x['source'])), 'Exact22 unique ordered original/cache pairs required')
    checked(allow['sourceCandidateSnapshot'])
    provenance = [ref(checked(x)) for x in allow['provenance']]
    index = safe(ROOT / '.git/index'); index_before = ref(index)
    need(not os.path.lexists(ROOT / '.git/index.lock'), 'Concurrent Git index operation forbidden')
    git_env = {**os.environ, 'GIT_OPTIONAL_LOCKS': '0', 'GIT_NO_LAZY_FETCH': '1'}
    def git(*argv):
        return subprocess.check_output(['git', *argv], cwd=ROOT, env=git_env, timeout=30)
    raw_index = git('--literal-pathspecs', 'ls-files', '--stage', '-z', '--', *[x['source'] for x in items])
    tracked = {}
    for entry in raw_index.split(b'\0'):
        if not entry:
            continue
        head, filename = entry.decode('utf-8').split('\t', 1); mode, oid, stage = head.split()
        need(mode == '100644' and stage == '0' and re.fullmatch(r'[0-9a-f]{40}', oid) and filename not in tracked,
             'Exact normal tracked stage0 originals required')
        tracked[filename] = oid
    need(set(tracked) == {x['source'] for x in items}, 'All frozen original paths must exist in the actual index')
    observations, inodes, cache_parents = [], set(), {}
    for item in items:
        controls(); source, cache = safe(item['source']), safe(item['cache']); oid = item['oid']
        need(re.fullmatch(r'[0-9a-f]{64}', oid) and cache == ROOT / '.git/lfs/objects' / oid[:2] / oid[2:4] / oid
             and source.is_relative_to(ROOT / 'Tools') and 'Animation-UAL1_Standard.fbx' not in source.parts,
             'Exact raw Tools original/cache target required; bootstrap UAL is excluded')
        need(0 < int(git('cat-file', '-s', tracked[item['source']]).strip()) < 512, 'Actual index blob must be a bounded LFS pointer')
        pointer = git('cat-file', 'blob', tracked[item['source']]).decode('ascii')
        need(pointer == 'version https://git-lfs.github.com/spec/v1\noid sha256:' + oid + '\nsize ' + str(item['bytes']) + '\n',
             'Actual tracked index LFS OID/size differs')
        a, b = attrs(source), attrs(cache)
        need(a['ino'] == item['observedSourceInode'] and b['ino'] == item['observedCacheInode']
             and 0 < a['ino'] < 2 ** 63 and 0 < b['ino'] < 2 ** 63
             and a['dev'] == b['dev'] == item['observedDevice'] == ROOT.stat().st_dev
             and a['ino'] != b['ino'] and a['nlink'] == b['nlink'] == 1
             and a['mode'] == b['mode'] == item['mode'] == 0o600
             and a['uid'] == b['uid'] == os.geteuid() and a['gid'] == b['gid']
             and a['bytes'] == b['bytes'] == item['bytes'], 'Observed independent upper original/cache inode/mode/owner/device differs')
        for metadata in (a, b):
            key = (metadata['dev'], metadata['ino']); need(key not in inodes, 'All44 original/cache inodes must be independent'); inodes.add(key)
        need(ref(source)['sha256'] == ref(cache)['sha256'] == oid, 'Actual original/cache SHA256 must both equal the indexed LFS OID')
        cache_parents.setdefault(str(cache.parent), sorted(x.name for x in cache.parent.iterdir()))
        observations.append({'pair': item, 'sourceBefore': a, 'cacheBefore': b, 'pointerGitBlob': tracked[item['source']]})
    need(ref(index) == index_before, 'Original index changed during read-only admission')
    ledger = run / 'ImmutableToolsLfsSharing'; ledger.mkdir(exist_ok=False)
    write(ledger / 'before.json', {'scope': 'exact-upper-immutable-tools-cache-aliases-only', 'publishReady': False,
        'allowlist': ref(ALLOW), 'actualLease': ref(CONTROL / 'lease.json'), 'index': index_before,
        'actualIndexedPairs': observations, 'provenance': provenance, 'cacheParentNames': cache_parents,
        'availableBytesBefore': available(), 'declaredLogicalDuplicateBytes': 130148333,
        'allocationLimit': 'No freed-byte or overlay sharing claim follows from inode class, st_blocks or logical equality alone.'})
    finished, temporary, temporary_created, current_pair = [], None, False, None
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(signum, interrupted)
    try:
        for number, original in enumerate(observations):
            controls(); item = original['pair']; source, cache = safe(item['source']), safe(item['cache'])
            current_pair = {'index': number, 'pair': item, 'phase': 'before-link', 'temporaryAlias': None}
            need(attrs(source) == original['sourceBefore'] and attrs(cache) == original['cacheBefore']
                 and ref(source)['sha256'] == ref(cache)['sha256'] == item['oid'], 'Original/cache changed before its operation')
            temporary = cache.with_name('.firstgame-tools-upper-' + args.owned_run_label + '-' + item['oid'])
            need(not os.path.lexists(temporary), 'Exclusive fresh cache-parent source-link path required')
            current_pair['temporaryAlias'] = str(temporary)
            free_before = available()
            need(free_before > item['bytes'] + RESERVE, 'Keep worst-case current-payload copy-up plus8MiB reporting reserve before each link')
            write(ledger / ('pair-%02d-before.json' % number), {**original, 'temporaryAlias': str(temporary), 'availableBytesBefore': free_before})
            os.link(source, temporary)
            temporary_created = True
            current_pair['phase'] = 'fresh-source-alias-created'
            fd = os.open(temporary, os.O_RDONLY)
            try:
                os.fsync(fd)
            finally:
                os.close(fd)
            sync_dir(cache.parent)
            linked = attrs(source)
            need(all(linked[k] == original['sourceBefore'][k] for k in ('dev', 'ino', 'bytes', 'mode', 'uid', 'gid', 'mtimeNs', 'blocks'))
                 and linked['nlink'] == 2 and attrs(temporary) == linked
                 and ref(source)['sha256'] == ref(temporary)['sha256'] == item['oid'], 'Original source inode/content/mode/owner/mtime changed while linking')
            free_linked = available()
            need(free_linked >= free_before - 1024 ** 2 and free_linked > RESERVE,
                 'Unexpected allocation after upper-only link; stop before replacing any cache inode')
            controls(); need(attrs(cache) == original['cacheBefore'] and ref(cache)['sha256'] == item['oid'], 'Independent cache changed immediately before atomic replacement')
            current_pair['phase'] = 'immediately-before-atomic-cache-replacement'
            os.replace(temporary, cache)
            current_pair['phase'] = 'atomic-cache-alias-replaced'
            temporary = None; temporary_created = False; sync_dir(cache.parent)
            after = attrs(source)
            need(attrs(cache) == after and all(after[k] == linked[k] for k in ('dev', 'ino', 'bytes', 'mode', 'uid', 'gid', 'mtimeNs', 'nlink', 'blocks'))
                 and ref(source)['sha256'] == ref(cache)['sha256'] == item['oid'],
                 'Atomic cache alias must retain exact original source inode and raw bytes')
            receipt = {'pair': item, 'sourceBefore': original['sourceBefore'], 'cacheBefore': original['cacheBefore'],
                'sourceAfter': after, 'cacheAfter': attrs(cache), 'availableBytesBefore': free_before,
                'availableBytesAfterLink': free_linked, 'availableBytesAfterAtomicReplacement': available(),
                'status': 'source-original-retained-cache-path-byte-identical',
                'limits': 'Logical duplicate bytes are not measured reclaimed bytes; only these actual available-space observations describe this operation.'}
            write(ledger / ('pair-%02d-after.json' % number), receipt); finished.append(receipt)
            current_pair['phase'] = 'after-receipt-durable'
        controls(); need(ref(index) == index_before, 'Original tracked index changed')
        for original, result in zip(observations, finished):
            item = original['pair']; source, cache = safe(item['source']), safe(item['cache'])
            need(attrs(source) == attrs(cache) == result['sourceAfter'] and ref(source)['sha256'] == ref(cache)['sha256'] == item['oid'],
                 'Final immutable original/cache aliases changed')
        for parent, names in cache_parents.items():
            need(sorted(x.name for x in safe(parent).iterdir()) == names, 'Unexpected cache-parent path added/removed')
        need([ref(checked(x)) for x in allow['provenance']] == provenance, 'Original notices/provenance changed')
        write(ledger / 'completed.json', {'status': 'twenty-two-immutable-originals-retained-cache-aliases-verified',
            'pairs': 22, 'declaredLogicalDuplicateBytes': 130148333, 'actualPairs': finished,
            'availableBytesAfter': available(), 'publishReady': False, 'indexUnchanged': index_before,
            'sourcePathsInodesBytesModesOwnersMtimesRetained': True, 'gameAssetsLibraryGeneratedFilesTouched': False,
            'limits': 'No product/test/Root/publication approval and no automatic future cache/source edit permission.'})
        print(json.dumps({'status': 'immutable-cache-aliases-verified', 'receipt': ref(ledger / 'completed.json')}))
    except BaseException as error:
        cleanup = 'no-outstanding-owned-temporary-alias'
        cleanup_error = None
        try:
            if temporary is not None and os.path.lexists(temporary):
                observed = attrs(temporary)
                if temporary_created and (observed['dev'], observed['ino']) == (original['sourceBefore']['dev'], original['sourceBefore']['ino']):
                    temporary.unlink(); sync_dir(temporary.parent); cleanup = 'only-new-owned-temporary-alias-removed'
                else:
                    cleanup = 'unrecognized-or-unproved-temporary-path-retained'
        except BaseException as failure:
            cleanup = 'owned-temporary-cleanup-incomplete-inspect-actual-path'
            cleanup_error = type(failure).__name__ + ': ' + str(failure)
        def observation(path):
            try:
                return {'path': str(path), 'metadata': attrs(path)}
            except (OSError, ValueError) as failure:
                return {'path': str(path), 'observationError': type(failure).__name__ + ': ' + str(failure)}
        failed_pair = None
        if current_pair is not None:
            failed_pair = {**current_pair, 'sourceActualAfterCleanup': observation(current_pair['pair']['source']),
                'cacheActualAfterCleanup': observation(current_pair['pair']['cache']),
                'temporaryActualAfterCleanup': observation(Path(current_pair['temporaryAlias'])) if current_pair['temporaryAlias'] else None}
        write(ledger / 'failure.json', {'status': 'failed-no-automatic-retry', 'error': type(error).__name__ + ': ' + str(error),
            'failedPairActualAfterCleanup': failed_pair,
            'completedPairs': finished, 'temporaryAliasCleanup': cleanup, 'temporaryCleanupError': cleanup_error,
            'availableBytesAfterFailure': available(), 'publishReady': False,
            'limits': 'No source original was intentionally rewritten or deleted; inspect actual recorded source/cache state. Completed aliases remain; no automatic retry or rollback.'})
        raise


if __name__ == '__main__':
    main()
