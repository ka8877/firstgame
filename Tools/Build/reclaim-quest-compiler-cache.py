#!/usr/bin/env python3
"""Reclaim bounded inactive compiler outputs after exact current Web verification."""
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
from restoration_source_binding import capture,verify
BUILD = Path('/tmp/firstgame-quest-builds')
REVISIONS = {'FantasyCards': 'fm-quest-2026-10-30', 'PathOfDawn': 'gospel-journey-2026-10-30'}


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def completed_players():
    rows = []
    for game, revision in REVISIONS.items():
        folder = BUILD / game / 'Web'
        evidence = folder / 'BuildEvidence.txt'
        if not evidence.is_file():
            continue
        data = dict(line.split(': ', 1) for line in evidence.read_text().splitlines() if ': ' in line)
        assert data.get('Revision') == revision and data.get('Result') == 'Succeeded' and data.get('Errors') == '0'
        assert folder.is_dir() and not folder.is_symlink()
        for path in sorted(folder.rglob('*')):
            if path.is_file():
                assert not path.is_symlink(), path
                rows.append({'file': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)})
    return rows


def free():
    stat = os.statvfs(ROOT)
    return stat.f_bavail * stat.f_frsize


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=REVISIONS)
    parser.add_argument('--post-build-caches', action='store_true')
    parser.add_argument('--player-data-caches', action='store_true',
                        help='Only inactive generated PlayerDataCache and BuildPlayerData regular files; preserve all roots/imported assets/packages.')
    args = parser.parse_args()
    processes = [line.split(None, 3) for line in subprocess.check_output(['ps', '-eo', 'pid=,stat=,comm=,args='], text=True).splitlines()]
    active = []
    for pid, state, name, command in processes:
        if state.startswith('Z') or name not in ('Unity', 'UnityShaderComp', 'bee_backend'):
            continue
        cwd = Path('/proc') / pid / 'cwd'
        assert cwd.exists(), 'Cannot prove compiler process ownership: ' + pid
        actual_cwd = cwd.resolve()
        assert not actual_cwd.is_relative_to(ROOT / 'Games' / args.game), 'Target project compiler is active'
        assert args.game not in command, 'Target project appears in active compiler command'
        assert any(actual_cwd.is_relative_to(ROOT / 'Games' / other) for other in REVISIONS if other != args.game), 'Unknown active compiler ownership'
        active.append({'pid': int(pid), 'process': name, 'cwd': str(actual_cwd), 'scope': 'Unrelated project; its cache is untouched'})
    assert (BUILD / args.game / 'Web/BuildEvidence.txt').is_file(), 'Successful current Web evidence is required'
    cache = ROOT / 'Artifacts/QuestWork/cache' / args.game
    bee = cache / 'Library/Bee'
    if not bee.exists():bee=cache/'Bee'
    assert not (args.post_build_caches and args.player_data_caches), 'Choose one bounded cache scope'
    if args.player_data_caches:
        folders = [cache / 'PlayerDataCache', cache / 'Library/BuildPlayerData']
    elif args.post_build_caches:
        folders = [bee] + [cache / name for name in ('BurstCache', 'ShaderCache')]
    else:
        folders = [bee / 'artifacts/WebGL' / name for name in ('il2cppOutput', 'GameAssembly')]
    rows = []
    for folder in folders:
        assert folder.is_dir() and not folder.is_symlink(), folder
        for path in sorted(folder.rglob('*')):
            assert not path.is_symlink(), path
            if path.is_file():
                rows.append({'file': str(path), 'bytes': path.stat().st_size, 'sha256': digest(path)})
    assert rows
    source_before=capture(args.game)
    before = completed_players()
    assert any(row['file'].startswith(str(BUILD / args.game / 'Web') + '/') for row in before)
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    ledger = ROOT / 'Artifacts/Validation/Quest/Storage' / (args.game + '-inactive-' + stamp + '.json')
    ledger.parent.mkdir(parents=True, exist_ok=True)
    record = {'state': 'prepared', 'scope': 'Only listed inactive generated compiler/cache files. All roots, PackageCache, imported Artifacts, original source/evidence and all archives retained. Only the explicit Quest workspace migrated generated compiler roots are allowed.',
              'game': args.game, 'sourceFingerprint':source_before['fingerprint'], 'createdUtc': datetime.now(timezone.utc).isoformat(), 'roots': list(map(str, folders)),
              'files': rows, 'completedPlayersBefore': before, 'workspaceFreeBytesBefore': free(), 'unrelatedActiveCompilers': active}
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    for row in rows:
        path = Path(row['file'])
        assert path.is_file() and not path.is_symlink() and digest(path) == row['sha256'], path
        path.unlink()
    assert completed_players() == before, 'A completed current player changed'
    assert verify(source_before)==source_before['fingerprint']
    record.update(state='reclaimed', checkedUtc=datetime.now(timezone.utc).isoformat(),
                  reclaimedFiles=len(rows), matchedLogicalBytes=sum(row['bytes'] for row in rows),
                  workspaceFreeBytesAfter=free(), allCompletedPlayersUnchanged=True,sourceInputsUnchanged=True)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({key: record[key] for key in ('state', 'game', 'reclaimedFiles', 'matchedLogicalBytes', 'workspaceFreeBytesAfter', 'allCompletedPlayersUnchanged')}))


if __name__ == '__main__':
    main()
