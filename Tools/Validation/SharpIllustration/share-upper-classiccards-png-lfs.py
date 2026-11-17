#!/usr/bin/env python3
"""Share only606 frozen upper-inode ClassicCards PNGs with their LFS cache aliases.

Root must first admit the atomic-output generator source and an actual current
owned four-test success. Every existing source/cache inode remains immutable;
any later regeneration must atomically replace the source pathname.

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
SELF = ROOT / 'Tools/Validation/SharpIllustration/share-upper-classiccards-png-lfs.py'
ALLOW = ROOT / 'Tools/Validation/SharpIllustration/immutable-upper-classiccards-png-allowlist.json'
ALLOW_SHA = '284ac22830501657ff01b4030127c832fd58d5e2935c0b488f1d58e0d157a20d'
WRAPPER = CONTROL / 'run-owned-sharp-illustration-current.py'
ENV = {'FIRSTGAME_RELEASE_PROFILE': 'sharp-illustration', 'FIRSTGAME_CONTROL_ROOT': str(CONTROL),
       'FIRSTGAME_VALIDATION_ROOT': str(ROOT / 'Artifacts/Validation/SharpIllustration-2026-11-17'),
       'FIRSTGAME_BUILD_ROOT': '/tmp/firstgame-sharp-illustration-20261117-builds',
       'FIRSTGAME_PROOF_ROOT': str(CONTROL / 'BuildProofCurrent'),
       'FIRSTGAME_STAGE_ROOT': str(ROOT / 'Artifacts/SharpIllustrationWebPlaytest')}
GIT_REDIRECTIONS = ('GIT_DIR', 'GIT_COMMON_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE',
                    'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_QUARANTINE_PATH')
RESERVE = 8 * 1024 ** 2
ADMISSION = CONTROL / 'Release/upper-classiccards-png-source-admission01.json'
GENERATOR = ROOT / 'Tools/Art/ClassicCards/prepare-media.py'
TEST = ROOT / 'Tools/Art/ClassicCards/test_atomic_png_output.py'
GENERATOR_SHA = '1ee0f54c93a46bbf75b31d86d26a7d92aba4ee3348ae97c9f8a34a940119a7ac'
TEST_SHA = '1f55ef5428b4d0b40f722050c1f0c3b20c6578b041851d83cc00875457fe1bd1'
WRAPPER_SHA = '0615afae26319b6bd9783d8481c37f1711665228815ae8123b4b6592b11eb519'
SUPERVISOR = ROOT / 'Tools/Environment/owned-process-containment.py'
SUPERVISOR_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
TEST_SCRIPT = 'import runpy,sys; test_path=sys.argv[1]; sys.argv=[test_path,"-v"]; runpy.run_path(test_path,run_name="__main__")'
TEST_ARGV = ['python3', '-c', TEST_SCRIPT, str(TEST.relative_to(ROOT)), str(GENERATOR.relative_to(ROOT))]
REQUIRED_TESTS = {
    'test_normal_png_preserves_encoder_bytes_pixels_and_syncs_payload_then_parent',
    'test_regeneration_detaches_hardlink_and_preserves_cache_bytes_and_output_mode',
    'test_partial_save_failure_leaves_shared_original_and_no_new_temporary',
    'test_identical_content_reruns_never_write_into_the_cache_inode',
}



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


def atomic_generator_admission(args, current_run):
    need(args.source_admission == ADMISSION, 'Exact separate Root source admission path required')
    admission_ref = ref(ADMISSION); admission = read(ADMISSION)
    need(ref(current_run / 'RecordedHarness' / ADMISSION.relative_to(ROOT)) == {
        **admission_ref, 'file': str((current_run / 'RecordedHarness' / ADMISSION.relative_to(ROOT)).relative_to(ROOT))},
        'Current wrapper must capture the exact separate Root source admission')
    need(admission.get('schema') == 1 and admission.get('status') == 'reviewed-and-admitted-for-cache-aliases'
         and admission.get('reviewer') == '/root'
         and admission.get('sourceWritePolicy') == 'existing-shared-inodes-immutable-future-output-pathname-atomic-replacement-only'
         and set(admission.get('requiredTests', [])) == REQUIRED_TESTS, 'Actual explicit Root atomic-generator source admission required')
    when = datetime.fromisoformat(admission['reviewedUtc'])
    need(when.utcoffset() is not None and when <= datetime.now(timezone.utc), 'Actual aware Root source review time required')
    need(checked(admission['helper']) == SELF and checked(admission['allowlist']) == ALLOW,
         'Root must admit this exact helper and606 allowlist')
    expected_sources = {str(GENERATOR.relative_to(ROOT)): GENERATOR_SHA, str(TEST.relative_to(ROOT)): TEST_SHA}
    sources = admission.get('atomicGeneratorSources', [])
    need(len(sources) == 2 and {x['file']: x['sha256'] for x in sources} == expected_sources,
         'Exact separately reviewed atomic generator and meaningful four-test source SHA pins required')
    for row in sources:
        checked(row)
    summary_path = safe(args.atomic_test_summary)
    need(summary_path == checked(admission['atomicGeneratorTestSummary'])
         and summary_path.parent.parent == CONTROL / 'CommandRuns' and summary_path.name == 'summary.json'
         and summary_path.parent != current_run, 'Actual preceding owned generator test summary required')
    test_summary = read(summary_path)
    need(test_summary.get('status') == 'command-completed' and test_summary.get('scope') == 'generic-owned-command-only'
         and all(good(test_summary.get(k, {}).get('event', {})) for k in ('guardBefore', 'idleBefore', 'command', 'guardAfter')),
         'Actual test wrapper success, final guard and proved-empty containment all required')
    invocation = read(summary_path.parent / 'invocation.json')
    need(checked(test_summary['invocation']) == summary_path.parent / 'invocation.json'
         and invocation.get('requestedArgv') == invocation.get('executedArgv') == TEST_ARGV
         and invocation.get('display') is False
         and all(invocation.get('environmentOverrides', {}).get(k) == v for k, v in ENV.items())
         and checked(invocation['ownedLeaseSnapshot']).read_bytes() == safe(CONTROL / 'lease.json').read_bytes(),
         'Actual exact current-owned four-test invocation, lease and environment required')
    captured = invocation.get('executionSources', [])
    need(ref(WRAPPER)['sha256'] == WRAPPER_SHA and ref(SUPERVISOR)['sha256'] == SUPERVISOR_SHA,
         'Current accepted wrapper and containment supervisor source pins required')
    for row in captured:
        original, recorded = row['source'], row['recorded']
        expected_recording = summary_path.parent / 'RecordedHarness' / safe(original['file']).relative_to(ROOT)
        need(checked(recorded) == expected_recording
             and recorded['sha256'] == original['sha256'] and recorded['bytes'] == original['bytes'],
             'Every captured source recording must retain its exact original execution bytes')
    # Unrelated historical captured contracts remain authenticated by their saved
    # execution bytes, rather than requiring unrelated current files to stay frozen.
    for row in [*sources, ref(WRAPPER), ref(SUPERVISOR)]:
        matches = [x for x in captured if x.get('source') == row]
        need(len(matches) == 1 and ref(checked(matches[0]['recorded']))['sha256'] == row['sha256'],
             'Both actual test and imported generator sources must be captured before execution')
    for key in ('guardBefore', 'idleBefore', 'command', 'guardAfter'):
        event = test_summary[key]['event']; containment = read(checked(event['containmentReceipt']))
        cleanup = containment.get('cleanup', {})
        need(containment.get('status') == 'owned-descendants-proved-empty'
             and containment.get('helperSha256') == SUPERVISOR_SHA and containment.get('root', {}).get('exitCode') == 0
             and containment.get('outcomeExitCode') == 0 and containment.get('subreaperVerifiedBeforeSpawn') is True
             and all(cleanup.get(k) is True for k in ('complete', 'kernelEchild', 'heldPidfdsExited', 'subreaperVerifiedAtCompletion'))
             and cleanup.get('liveOwnedIdentities') == cleanup.get('violations') == cleanup.get('discoveryErrors') == []
             and cleanup.get('error') is None, 'Actual test supervisor receipt must prove child exit0 and empty cleanup')
    output = checked(test_summary['command']['stdout'])
    need(checked(admission['atomicGeneratorTestOutput']) == output,
         'Root admission must bind the exact actual four-test output')
    text = output.read_text()
    observed = re.findall(r'(?m)^(test_[a-z0-9_]+) \((__main__\.AtomicPngOutputTests(?:\.test_[a-z0-9_]+)?)\) \.\.\. ok$', text)
    need(len(observed) == 4 and {name for name, _ in observed} == REQUIRED_TESTS
         and all(parent == '__main__.AtomicPngOutputTests' or parent == '__main__.AtomicPngOutputTests.' + name for name, parent in observed)
         and re.search(r'(?m)^Ran 4 tests in [0-9.]+s$', text)
         and re.search(r'(?m)^OK$', text) and not re.search(r'(?m)^(FAILED|ERROR:|FAIL:)', text),
         'All exact four actual verbose tests must pass with no skipped/substituted or failed result')
    need(datetime.fromisoformat(test_summary['finishedUtc']) <= when,
         'Root admission must follow actual completed test execution')
    return {'rootAdmission': admission_ref, 'actualGeneratorTestSummary': ref(summary_path),
            'actualGeneratorTestOutput': ref(output), 'atomicGeneratorSources': sources}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-run-label', required=True)
    parser.add_argument('--allowlist', type=Path, required=True)
    parser.add_argument('--source-admission', type=Path, required=True)
    parser.add_argument('--atomic-test-summary', type=Path, required=True)
    parser.add_argument('--confirm-upper-originals-and-cache-aliases-remain-immutable', action='store_true', required=True)
    args = parser.parse_args()
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0'
         and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.owned_run_label), 'Assertions/safe unique label required')
    need(args.allowlist == ALLOW and ref(ALLOW)['sha256'] == ALLOW_SHA, 'Exact frozen explicit allowlist required')
    need(not any(k in os.environ for k in GIT_REDIRECTIONS), 'Inherited Git context/object redirects forbidden')
    run, lease_raw, deadline = owned(args.owned_run_label, ['python3', str(SELF), *sys.argv[1:]])
    generator_admission = atomic_generator_admission(args, run)
    def controls():
        need(safe(CONTROL / 'lease.json').read_bytes() == lease_raw
             and (deadline - datetime.now(timezone.utc)).total_seconds() > 90, 'Actual acquired lease/cleanup reserve changed')
    for argv in ([sys.executable, 'Tools/Autonomy/cycle_guard.py', 'check', '--record', str(CONTROL / 'lease.json')],
                 [sys.executable, 'Tools/Environment/assert-runtime-idle.py']):
        subprocess.run(argv, cwd=ROOT, check=True, timeout=45)
    allow = read(ALLOW); items = allow['files']
    need(allow.get('schema') == 1 and allow.get('count') == len(items) == 606
         and sum(x['bytes'] for x in items) == allow.get('declaredLogicalDuplicateBytes') == 145141701
         and len({x['source'] for x in items}) == len({x['oid'] for x in items}) == len({x['cache'] for x in items}) == 606
         and items == sorted(items, key=lambda x: (x['bytes'], x['source'])), 'Exact606 unique ordered original/cache pairs required')
    need(allow.get('sourceStatus') == 'separate-Root-source-admission-and-owned-four-test-success-required', 'Separate PNG admission required')
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
    observations, inodes, cache_parents, source_parents = [], set(), {}, {}
    manifest = read(ROOT / 'Tools/Art/ClassicCards/manifest.json')
    prints = manifest['cards'] + manifest['decorations']
    by_source = {x['texture']: x for x in prints}
    need(len(prints) == len(by_source) == 606 and set(by_source) == {x['source'] for x in items}, 'All exact605 manifest cards plus original card back required')
    for item in items:
        controls(); source, cache = safe(item['source']), safe(item['cache']); oid = item['oid']
        need(re.fullmatch(r'[0-9a-f]{64}', oid) and cache == ROOT / '.git/lfs/objects' / oid[:2] / oid[2:4] / oid
             and (source.parent == ROOT / 'Games/FantasyCards/Assets/Resources/Art/ClassicCards' and re.fullmatch(r'fm_[0-9]+\.png', source.name)
                  or source == ROOT / 'Games/FantasyCards/Assets/Resources/Art/ClassicCards/Decor/card-back.png'),
             'Only exact current manifest card/back source PNGs and their cache targets permitted')
        contract = item['pixelContract']; manifest_row = by_source[item['source']]
        need(manifest_row['id'] == contract['manifestId'] and manifest_row['textureSha256'] == contract['textureSha256'] == oid
             and manifest_row['textureBytes'] == contract['textureBytes'] == item['bytes']
             and manifest_row['size'] == contract['dimensions']
             and manifest_row['decodedRgbaSha256'] == contract['decodedRgbaSha256']
             and manifest_row['original'] == contract['original'] and manifest_row['originalSha256'] == contract['originalSha256']
             and manifest_row['decodedPixelsIdentical'] is contract['decodedPixelsIdentical'] is True,
             'Frozen current manifest pixel/container/original contract differs')
        meta = safe(str(source) + '.meta'); meta_ref = {k: item['metaBefore'][k] for k in ('file', 'bytes', 'sha256')}
        need(checked(meta_ref) == meta and re.findall(r'(?m)^guid: ([0-9a-f]{32})$', meta.read_text()) == [item['metaBefore']['guid']],
             'Original meta pathname, bytes and GUID must remain exact')
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
            key = (metadata['dev'], metadata['ino']); need(key not in inodes, 'All1212 original/cache inodes must be independent'); inodes.add(key)
        need(ref(source)['sha256'] == ref(cache)['sha256'] == oid, 'Actual original/cache SHA256 must both equal the indexed LFS OID')
        cache_parents.setdefault(str(cache.parent), sorted(x.name for x in cache.parent.iterdir()))
        source_parents.setdefault(str(source.parent), sorted(x.name for x in source.parent.iterdir()))
        observations.append({'pair': item, 'sourceBefore': a, 'cacheBefore': b, 'metaBefore': attrs(meta),
                             'metaReference': meta_ref, 'pointerGitBlob': tracked[item['source']]})
    need(ref(index) == index_before, 'Original index changed during read-only admission')
    ledger = run / 'ImmutableClassicCardsPngLfsSharing'; ledger.mkdir(exist_ok=False)
    write(ledger / 'before.json', {'scope': 'exact-upper-classiccards-png-cache-aliases-only', 'publishReady': False,
        'allowlist': ref(ALLOW), 'actualLease': ref(CONTROL / 'lease.json'), 'index': index_before,
        'actualIndexedPairs': observations, 'provenance': provenance, 'cacheParentNames': cache_parents,
        'sourceParentNames': source_parents, 'atomicGeneratorAdmission': generator_admission,
        'availableBytesBefore': available(), 'declaredLogicalDuplicateBytes': 145141701,
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
            temporary = cache.with_name('.firstgame-classiccards-png-upper-' + args.owned_run_label + '-' + item['oid'])
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
        for original in observations:
            need(attrs(checked(original['metaReference'])) == original['metaBefore'], 'Original PNG.meta content/GUID/inode/mode/owner/mtime changed')
        for parent, names in source_parents.items():
            need(sorted(x.name for x in safe(parent).iterdir()) == names, 'Original source directory paths changed')
        need(atomic_generator_admission(args, run) == generator_admission, 'Generator source/admission/test proof changed')
        for parent, names in cache_parents.items():
            need(sorted(x.name for x in safe(parent).iterdir()) == names, 'Unexpected cache-parent path added/removed')
        need([ref(checked(x)) for x in allow['provenance']] == provenance, 'Original notices/provenance changed')
        write(ledger / 'completed.json', {'status': 'six-hundred-six-source-print-png-inodes-retained-cache-aliases-verified',
            'pairs': 606, 'declaredLogicalDuplicateBytes': 145141701, 'actualPairs': finished,
            'availableBytesAfter': available(), 'publishReady': False, 'indexUnchanged': index_before,
            'sourcePathsInodesBytesModesOwnersMtimesRetained': True, 'sourcePngContentOrMetaOrAnyLibraryWrites': False, 'only606LfsCacheAliasesReplaced': True,
            'atomicGeneratorAdmission': generator_admission,
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
