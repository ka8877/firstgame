#!/usr/bin/env python3
"""Unexecuted post-checkpoint maintenance proposal; no product approval.

Remove only Card Shader/Burst cache files and two generated PlayerData blobs.
Retain every directory, PlayerData metadata, immutable checkpoint and all
source, complete protected Card product, Library Artifacts/packages and proofs.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import sys

sys.dont_write_bytecode = True
ROOT = Path('/workspace/firstgame')
ACTIVE = ROOT / 'Artifacts/Validation/Autonomy/GospelNprCameraResume-2026-11-11'
DRAFT = ROOT / 'Artifacts/Validation/Drafts/GospelNprPostCheckpoint20261111CacheSpace'
V2 = ROOT / 'Artifacts/Validation/Drafts/GospelNpr20261111CardBeeCompilerSpaceProposalV2'
LIBRARY = ROOT / 'Games/FantasyCards/Library'
CACHES = tuple(LIBRARY / name for name in ('ShaderCache', 'BurstCache', 'PlayerDataCache'))
SOURCES = ACTIVE / 'Release/card-bee-compiler-CardBeeCompilerSpace02.source-before.json'
BASELINE = ACTIVE / 'Release/card-bee-compiler-CardBeeCompilerSpace02.card-baseline-before.json'
CHECKPOINT = ROOT / 'Docs/Autonomy/Evidence/GospelNpr20261111/Checkpoint01/index.json'
ARCHIVE_RUN = ACTIVE / 'CommandRuns/ArchiveNprCamera01'
SOURCE_SHA = '946bbbceadf02802c2f4b829260deb9c9d4f5959be58e5997dc54190621c5e56'
BASELINE_SHA = '68e7ede3709a340a7608a743de0423d8441ce11d4c9139efa39d6abe86dd86c4'
CHECKPOINT_SHA = '44f4c91f30db296767c8448589225f92b8c954e065ce3285ce01b81fae0a9c65'
ROOT_CHILDREN = {'ShaderCache': {'temp', 'compute', 'urtshader', 'builtin', 'shader', 'shadergraph', 'EditorEncounteredVariants'},
                 'BurstCache': {'AotSettings_WebGL.hash', 'JIT'}, 'PlayerDataCache': {'WebGL'}}
PLAYER_FILES = {'WebGL/ScriptsOnlyCache.yaml', 'WebGL/DataBuildDirtyInfo.json', 'WebGL/Data/data.unity3d',
                'WebGL/Data/RuntimeInitializeOnLoads.json', 'WebGL/Data/ScriptingAssemblies.json',
                'WebGL/Data/boot.config', 'WebGL/Data/resources.resource'}
PLAYER_BLOBS = {'WebGL/Data/data.unity3d', 'WebGL/Data/resources.resource'}


def need(value, message):
    if not value:
        raise ValueError(message)


def load(path, expected, name):
    need(path.is_file() and not any(p.is_symlink() for p in (path, *path.parents))
         and hashlib.sha256(path.read_bytes()).hexdigest() == expected, 'Exact reviewed read-only support required')
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def owned(m, label):
    need(os.environ.get('FIRSTGAME_CONTROL_ROOT') == str(ACTIVE)
         and os.environ.get('FIRSTGAME_VALIDATION_ROOT') == str(m.OUT)
         and os.environ.get('FIRSTGAME_RELEASE_PROFILE') == 'gospel-npr', 'Explicit active NPR owner required')
    run = ACTIVE / 'CommandRuns' / label
    invocation = m.read(run / 'invocation.json')
    need(invocation.get('label') == label
         and invocation.get('requestedArgv') == ['python3', str(Path(__file__).absolute()), '--owned-run-label', label, '--confirm-post-checkpoint-cache-prune']
         and not (run / 'summary.json').exists(), 'Exact actual in-progress owned wrapper required')
    event = m.read(run / 'Processes/idleBefore.finished.json')
    need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
         and event.get('supervisorStillAlive') is False
         and event.get('argv', [])[1:] == ['Tools/Environment/assert-runtime-idle.py'], 'Completed independent owned idleBefore required')
    found, pid = False, os.getppid()
    for _ in range(64):
        if pid <= 1:
            break
        proc = Path('/proc') / str(pid)
        words = (proc / 'cmdline').read_bytes().decode(errors='replace').split('\0')
        found |= any(word == str(ACTIVE / 'run-owned-npr-resume.py') and index + 1 < len(words) and words[index + 1] == label
                     for index, word in enumerate(words))
        pid = int((proc / 'stat').read_text().split(') ', 1)[1].split()[1])
    need(found, 'Actual live owned wrapper must be an ancestor')
    return {'invocation': m.ref(run / 'invocation.json'), 'idleBefore': m.ref(run / 'Processes/idleBefore.finished.json')}


def completed_checkpoint(m):
    need(m.ref(CHECKPOINT)['sha256'] == CHECKPOINT_SHA, 'Exact immutable completed checkpoint required')
    index = m.read(CHECKPOINT)
    need(index.get('releaseProfile') == 'gospel-npr' and index.get('joinedBytes') == 2_601_792_134
         and index.get('files') and index.get('fileCount') == len(index['files']), 'Actual checkpoint index required')
    result = m.read(ARCHIVE_RUN / 'summary.json')
    need(result.get('label') == 'ArchiveNprCamera01' and result.get('status') == 'command-completed', 'Archive must have completed before maintenance')
    for label in ('guardBefore', 'idleBefore', 'command', 'guardAfter'):
        event = result.get(label, {}).get('event', {})
        need(event.get('exitCode') == 0 and event.get('containmentProvedEmpty') is True
             and event.get('supervisorStillAlive') is False, 'Completed archive cleanup/guards required')
    stdout = Path(result['command']['stdout']['file'])
    stdout = stdout if stdout.is_absolute() else ROOT / stdout
    need(m.ref(stdout) == result['command']['stdout'], 'Actual archive stdout original differs')
    raw = next(line for line in reversed(stdout.read_bytes().splitlines()) if line.strip())
    emitted = json.loads(raw)
    need(emitted.get('status') == 'actual-gospel-npr-proof-preserved' and emitted.get('index') == m.ref(CHECKPOINT)
         and emitted.get('joinedBytes') == index['joinedBytes'], 'Actual archive completion must bind this checkpoint')
    return {'index': m.ref(CHECKPOINT), 'archiveWrapper': m.ref(ARCHIVE_RUN / 'summary.json')}


def inventory(m):
    rows, directories = [], []
    for cache in CACHES:
        need(m.safe(cache) == cache and stat.S_ISDIR(cache.lstat().st_mode), 'Exact regular Card cache root required')
        need({p.name for p in cache.iterdir()} == ROOT_CHILDREN[cache.name], 'Unknown or already-pruned cache root')
        stack = [cache]
        while stack:
            parent = stack.pop()
            directories.append({'path': str(parent), 'metadata': m.attrs(parent.lstat())})
            with os.scandir(parent) as children:
                for child in sorted(children, key=lambda item: item.name):
                    path = Path(child.path)
                    st = child.stat(follow_symlinks=False)
                    need(not child.is_symlink(), 'Cache symlink forbidden')
                    if stat.S_ISDIR(st.st_mode):
                        stack.append(path)
                        continue
                    need(stat.S_ISREG(st.st_mode), 'Unknown special cache member')
                    row = m.file_row(path, single_link=True)
                    relative = path.relative_to(cache).as_posix()
                    if cache.name == 'ShaderCache':
                        need(path.suffix == '.bin' or relative == 'EditorEncounteredVariants', 'Unknown ShaderCache file')
                        remove = True
                    elif cache.name == 'BurstCache':
                        need(path.suffix in ('.bhc', '.gbc', '.o', '.so', '.hash', '.cm'), 'Unknown BurstCache file')
                        remove = True
                    else:
                        need(relative in PLAYER_FILES, 'Unknown PlayerData cache member')
                        remove = relative in PLAYER_BLOBS
                    row.update(remove=remove, allocatedBytes=st.st_blocks * 512)
                    rows.append(row)
    return {'files': sorted(rows, key=lambda row: row['file']), 'directories': sorted(directories, key=lambda row: row['path'])}


def tree_signature(m, path):
    # Read every retained imported/package/Bee byte, but store only the
    # deterministic aggregate to avoid duplicating large immutable originals.
    m.safe(path)
    need(stat.S_ISDIR(path.lstat().st_mode), 'Regular protected tree required')
    digest, count, size = hashlib.sha256(), 0, 0
    for entry in [path, *sorted(path.rglob('*'))]:
        m.safe(entry)
        if entry.is_dir():
            row = {'directory': str(entry), 'metadata': m.attrs(entry.lstat())}
        else:
            row = m.file_row(entry)
            count += 1
            size += row['metadata']['bytes']
        digest.update((json.dumps(row, sort_keys=True, separators=(',', ':')) + '\n').encode())
    return {'root': str(path), 'files': count, 'bytes': size, 'sha256OfCompleteByteAndMetadataRows': digest.hexdigest()}


def protected(m, v, capture, verify_card_baseline):
    sources = {game: capture(game) for game in ('FantasyCards', 'PathOfDawn')}
    need(m.ref(SOURCES)['sha256'] == SOURCE_SHA and sources == m.read(SOURCES), 'Complete current source arrays must match immutable actual originals')
    baseline = verify_card_baseline()
    need(m.ref(BASELINE)['sha256'] == BASELINE_SHA and baseline == m.read(BASELINE), 'Fresh whole protected Card baseline must equal immutable actual original')
    need(baseline['sourceFingerprint'] == v.CARD_SOURCE and baseline['verifiedFiles'] == 15_379
         and baseline['verifiedBytes'] == 869_460_781 and sources['PathOfDawn']['fingerprint'] == v.GOSPEL_SOURCE, 'Exact source and Card whole product required')
    return {'pins': v.pins(m), 'sourceSnapshot': m.ref(SOURCES), 'cardBaselineSnapshot': m.ref(BASELINE),
            'reviewedSupportSources': [m.ref(V2 / name) for name in ('prune-idle-compiler-caches.py', 'prune-idle-card-bee-compiler-output.py')],
            'readOnlyGospelLibraryAlias': m.readonly_library_alias(), 'gospelRuntimeProofAndDatabases': m.runtime_rows(),
            'cardDatabases': [m.file_row(LIBRARY / name) for name in ('SourceAssetDB', 'ArtifactDB')],
            'retainedCardTrees': [tree_signature(m, LIBRARY / name) for name in ('Artifacts', 'PackageCache', 'Bee')],
            'checkpoint': completed_checkpoint(m)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--owned-run-label', required=True)
    parser.add_argument('--confirm-post-checkpoint-cache-prune', action='store_true', required=True)
    args = parser.parse_args()
    need(__debug__ and os.environ.get('PYTHONOPTIMIZE', '0') == '0', 'Assertions remain enabled')
    need(Path(__file__).absolute().parent == DRAFT and re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', args.owned_run_label), 'Reviewed new Draft and safe unique label required')
    m = load(V2 / 'prune-idle-compiler-caches.py', '754dfcce0571faeadcb5b7c90b804311c564679ad511df123bfdd36183f4581b', 'npr_reviewed_v2_shared_read_only')
    v = load(V2 / 'prune-idle-card-bee-compiler-output.py', '15bc5fb607e284349ff32a818bf1283436f548f769c598e4628cef6ab6227378', 'npr_reviewed_v2_card_read_only')
    executed_source = m.ref(Path(__file__).absolute())
    stem = ACTIVE / 'Release' / ('post-checkpoint-card-cache-' + args.owned_run_label)
    inventory_path, result_path = Path(str(stem) + '.inventory.json'), Path(str(stem) + '.result.json')
    need(not inventory_path.exists() and not result_path.exists(), 'New no-clobber maintenance destinations required')
    context = owned(m, args.owned_run_label)
    first_idle = m.idle()
    sys.path.insert(0, str(ROOT / 'Tools/Validation'))
    from restoration_source_binding import capture
    from GospelNpr.card_baseline import verify_card_baseline
    before_protected = protected(m, v, capture, verify_card_baseline)
    before = inventory(m)
    selected = [row for row in before['files'] if row['remove']]
    need(len(before['files']) == 2_010 and len(selected) == 2_005
         and sum(row['metadata']['bytes'] for row in selected) == 197_744_059
         and sum(row['allocatedBytes'] for row in selected) == 202_637_312,
         'Only the exact independently inspected generated cache subset is admitted')
    free_before = m.free()
    m.write_new(inventory_path, {'schema': 1, 'status': 'actual-pre-removal-post-checkpoint-cache-inventory',
                'createdUtc': m.utc(), 'executedSource': executed_source, 'ownedContext': context,
                'independentIdle': first_idle, 'entries': before, 'retainedOriginals': before_protected,
                'scope': 'Only Card Shader/Burst generated cache files and two exact PlayerData blobs. Every directory, five PlayerData metadata files, complete source/Card product/imported Artifacts/packages/Bee/proof/LastBuild/DB/checkpoint retained.',
                'workspaceFreeBeforeInventory': free_before})
    second_idle = m.idle()
    need(inventory(m) == before and protected(m, v, capture, verify_card_baseline) == before_protected
         and m.ref(Path(__file__).absolute()) == executed_source,
         'Cache or retained original changed before deletion; preserve inventory and stop')
    for row in selected:
        path = Path(row['file'])
        need(m.file_row(path, single_link=True) == {key: value for key, value in row.items() if key not in ('remove', 'allocatedBytes')}, 'Cache member changed immediately before unlink')
        path.unlink()
    after = inventory_after(m, before)
    need(protected(m, v, capture, verify_card_baseline) == before_protected
         and m.ref(Path(__file__).absolute()) == executed_source, 'Protected original or executed source changed after deletion')
    m.write_new(result_path, {'schema': 1, 'status': 'post-checkpoint-card-regenerable-cache-subset-pruned',
                'checkedUtc': m.utc(), 'inventory': m.ref(inventory_path), 'secondIndependentIdle': second_idle,
                'removedRegularFiles': len(selected), 'removedLogicalBytes': 197_744_059,
                'removedPreviouslyAllocatedFileBytes': 202_637_312, 'retainedCache': after,
                'allProtectedOriginalsEqual': True, 'retainedOriginals': before_protected,
                'workspaceFreeBeforeInventory': free_before, 'workspaceFreeAfter': m.free(),
                'limits': 'Actual free-space gain includes inventory/result overhead and filesystem allocation effects. No product/rendering/rebuild/test/public/archive approval; future Unity regenerates these caches.'})
    print(json.dumps({'status': 'post-checkpoint-cache-maintenance-completed-not-product-approval', 'result': m.ref(result_path)}))


def inventory_after(m, before):
    kept = [row for row in before['files'] if not row['remove']]
    for row in before['files']:
        path = Path(row['file'])
        if row['remove']:
            need(not path.exists() and not path.is_symlink(), 'Selected cache file remains')
        else:
            need(m.file_row(path, single_link=True) == {key: value for key, value in row.items() if key not in ('remove', 'allocatedBytes')}, 'Retained PlayerData metadata changed')
    expected_directories = {row['path']: row['metadata'] for row in before['directories']}
    actual_directories, actual_files = set(), set()
    for root in CACHES:
        for path in [root, *root.rglob('*')]:
            m.safe(path)
            mode = path.lstat().st_mode
            need(stat.S_ISDIR(mode) or stat.S_ISREG(mode), 'Unknown special cache member after cleanup')
            (actual_directories if stat.S_ISDIR(mode) else actual_files).add(str(path))
    need(actual_directories == set(expected_directories), 'All cache directories must remain and no new directory may appear')
    need(actual_files == {row['file'] for row in kept}, 'Unexpected cache member after cleanup')
    for name, metadata in expected_directories.items():
        path = Path(name)
        m.safe(path)
        need(stat.S_ISDIR(path.lstat().st_mode) and all(m.attrs(path.lstat())[key] == metadata[key] for key in ('dev', 'ino', 'mode', 'uid', 'gid', 'nlink')), 'Retained directory identity/ownership changed')
    return {'retainedMetadataFiles': len(kept), 'retainedDirectories': len(expected_directories), 'allMetadataBytesAndMetadataEqual': True}


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, StopIteration) as error:
        raise SystemExit(str(error))
