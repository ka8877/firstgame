#!/usr/bin/env python3
"""Root-owned bounded sharing of four regenerated failed-stage copies only."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat

ROOT = Path('/workspace/firstgame')
CONTROL = ROOT / 'Artifacts/Validation/Autonomy/ClassicNprResume-2026-11-16'
PROOF = ROOT / 'Artifacts/Validation/Autonomy/ClassicNpr-2026-11-16/BuildProof'
PARTIAL = ROOT / 'Artifacts/ClassicNprWebPlaytest.previous-20261116T093737612993Z'
CHECKPOINT = ROOT / 'Docs/Autonomy/Evidence/ClassicNpr20261116/Checkpoint01'
PAIRS = (
    ('FantasyCards', 'fantasy-cards', '11bdb36299a7c576dd77b8712965ef38.data.unityweb', 99658082),
    ('FantasyCards', 'fantasy-cards', 'e689e9007795a790ef309ebb7d32c91e.wasm.unityweb', 14208896),
    ('PathOfDawn', 'path-of-dawn', '043d0ef83f182d9142d7deefa8e8dbf2.data.unityweb', 81993222),
    ('PathOfDawn', 'path-of-dawn', 'ab378ebb2fb3aefd6469cc451e1d0db7.wasm.unityweb', 15912135),
)


def need(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    need(path.is_relative_to(ROOT) and not any(p.is_symlink() for p in (path, *path.parents)), 'Exact nonsymlink repository path required')
    need(stat.S_ISREG(path.lstat().st_mode), 'Regular file required')
    return path


def digest(path):
    h = hashlib.sha256()
    with regular(path).open('rb') as f:
        for data in iter(lambda: f.read(1024 * 1024), b''):
            h.update(data)
    return h.hexdigest()


def row(path):
    s = regular(path).stat()
    return dict(file=str(path.relative_to(ROOT)), sha256=digest(path), bytes=s.st_size,
                mode=stat.S_IMODE(s.st_mode), uid=s.st_uid, gid=s.st_gid,
                device=s.st_dev, inode=s.st_ino, links=s.st_nlink, mtimeNs=s.st_mtime_ns,
                blocks=s.st_blocks)


def tree_content(root):
    rows = []
    for directory, dirs, files in os.walk(root, followlinks=False):
        for name in dirs:
            need(not (Path(directory) / name).is_symlink(), 'No linked directory is admitted')
        for name in files:
            value = row(Path(directory) / name)
            rows.append({key: value[key] for key in ('file', 'sha256', 'bytes', 'mode', 'uid', 'gid', 'mtimeNs')})
    return sorted(rows, key=lambda value: value['file'])


def write_new(path, value):
    with path.open('x') as f:
        json.dump(value, f, ensure_ascii=False, indent=2)
        f.write('\n')


def main():
    need(Path(__file__) == CONTROL / 'Release/share-partial-stage01-four-derived-copies.py', 'Exact reviewed installed helper required')
    lease = json.loads((CONTROL / 'lease.json').read_bytes())
    need(lease['state'] == 'acquired' and lease['lease']['owner'] == '555529fe-af82-4e24-85a4-e7627ae70ccb'
         and lease['commitSha'] == '9495e3c124a89fd0abdf0c8ffde34ceb96ffd3d7', 'Actual current owner required')
    before_path = CONTROL / 'Release/partial-stage01-four-sharing.before.json'
    after_path = CONTROL / 'Release/partial-stage01-four-sharing.after.json'
    need(not before_path.exists() and not after_path.exists(), 'No history overwrite')
    # Fully freeze failed-stage bytes and checkpoint bytes before touching any copy.
    partial_before, checkpoint_before = tree_content(PARTIAL), tree_content(CHECKPOINT)
    pairs = []
    for game, slug, name, size in PAIRS:
        source = PROOF / (game + '-Web') / 'Runtime/Build' / name
        target = PARTIAL / 'games' / slug / 'Build' / name
        a, b = row(source), row(target)
        need(a['bytes'] == b['bytes'] == size and a['sha256'] == b['sha256'], 'Derived copy bytes must exactly equal retained proof original')
        need(all(a[key] == b[key] for key in ('mode', 'uid', 'gid', 'device')), 'Same device and permissions required')
        need(a['inode'] != b['inode'] and b['links'] == 1 and a['links'] == 1, 'Only separate single-link copies admitted')
        temporary = target.with_name(target.name + '.classic-npr-share-four')
        need(not temporary.exists() and not temporary.is_symlink(), 'New exclusive sibling only')
        pairs.append(dict(source=a, target=b, temporary=str(temporary.relative_to(ROOT))))
    before = dict(schema=1, scope='Four regenerated failed Stage01 copy paths only; every retained proof original is kept.',
                  pairs=pairs, partialTree=partial_before, checkpointTree=checkpoint_before,
                  workspaceFree=shutil.disk_usage(ROOT).free)
    write_new(before_path, before)
    for item in pairs:
        source, target, temporary = (ROOT / item[key] if key == 'temporary' else ROOT / item[key]['file'] for key in ('source', 'target', 'temporary'))
        need(row(source) == item['source'] and row(target) == item['target'], 'Pair changed before its bounded mutation')
        os.link(source, temporary, follow_symlinks=False)
        need(temporary.stat().st_ino == source.stat().st_ino and digest(temporary) == item['source']['sha256'], 'New link must retain source inode and bytes')
        os.replace(temporary, target)
    after_pairs = []
    for item in pairs:
        a, b = row(ROOT / item['source']['file']), row(ROOT / item['target']['file'])
        for key in ('file', 'sha256', 'bytes', 'mode', 'uid', 'gid', 'device'):
            need(a[key] == item['source'][key] and b[key] == item['target'][key], 'Path, bytes or metadata changed unexpectedly')
        need(a['mtimeNs'] == item['source']['mtimeNs'] == b['mtimeNs'] and a['blocks'] == item['source']['blocks'] == b['blocks'], 'Retained original time/allocation must stay exact')
        need(a['inode'] == item['source']['inode'] == b['inode'] and a['links'] == b['links'] == 2, 'Retained original inode must own exactly the two paths')
        need(not (ROOT / item['temporary']).exists(), 'No temporary sibling may remain')
        after_pairs.append(dict(source=a, target=b))
    partial_after = tree_content(PARTIAL)
    target_times = {item['target']['file']: item['source']['mtimeNs'] for item in pairs}
    expected_partial = [dict(item, mtimeNs=target_times.get(item['file'], item['mtimeNs'])) for item in partial_before]
    need(partial_after == expected_partial and tree_content(CHECKPOINT) == checkpoint_before, 'Only the four derived target times may adopt retained originals; all bytes/checkpoint metadata stay exact')
    result = dict(schema=1, status='four-derived-failed-stage-copies-shared', pairs=after_pairs,
                  logicalDuplicateBytes=sum(item['target']['bytes'] for item in pairs),
                  beforeWorkspaceFree=before['workspaceFree'], afterWorkspaceFree=shutil.disk_usage(ROOT).free,
                  checkpointUnchanged=True, failedStageBytesUnchanged=True,
                  targetMetadataChanges='Four derived targets adopt retained source inode/mtime/allocation/nlink; source bytes/mode/uid/gid/inode/mtime/allocation stay exact. Parent directory mtime/ctime and source ctime/nlink may change.',
                  scope='Only four failed-stage regenerated copies now share retained proof originals. No original, raw evidence, cache, source or public path was removed.')
    write_new(after_path, result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
