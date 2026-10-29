#!/usr/bin/env python3
"""Reclaim bounded inactive generated caches, byte-checking all four players."""
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BUILD = Path('/tmp/firstgame-polish-builds')
OUT = ROOT / 'Artifacts/Validation/Polish/Storage'


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def players():
    rows = []
    for game in ('FantasyCards', 'PathOfDawn'):
        for target in ('Windows', 'Web'):
            folder = BUILD / game / target
            assert folder.is_dir() and not folder.is_symlink(), folder
            for path in sorted(folder.rglob('*')):
                if path.is_file():
                    assert not path.is_symlink(), path
                    rows.append({'file': str(path), 'bytes': path.stat().st_size,
                                 'sha256': digest(path)})
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player-data-cache', action='store_true',
                        help='Reclaim only the two inactive generated PlayerDataCache folders.')
    args = parser.parse_args()
    processes = [line.split(None, 1) for line in
                 subprocess.check_output(['ps', '-eo', 'stat=,comm='], text=True).splitlines()]
    assert not any(not state.startswith('Z') and name in ('Unity', 'UnityShaderComp', 'bee_backend')
                   for state, name in processes), 'Unity or a build worker is active'
    if args.player_data_cache:
        folders = [Path('/tmp/firstgame-arcana-generated-cache') / game / 'PlayerDataCache'
                   for game in ('FantasyCards', 'PathOfDawn')]
        ledger_name = 'inactive-player-data-cache-reclamation.json'
        scope = 'Both final game builds have exited. Only the two generated PlayerDataCache folders in the Arcana cache; no Artifacts, PackageCache, source, original evidence, player or archive deletion.'
    else:
        cache = (ROOT / 'Games/FantasyCards/Library/Bee/artifacts/WebGL').resolve()
        assert cache == Path('/tmp/firstgame-luminous-generated-cache/FantasyCards/Library/Bee/artifacts/WebGL')
        folders = [cache / name for name in ('il2cppOutput', 'GameAssembly')]
        ledger_name = 'cards-inactive-compiler-reclamation.json'
        scope = 'Successful final Card Web build has exited. Only inactive Card WebGL il2cppOutput and GameAssembly regular files; no source, shared cache, original evidence, player or archive deletion.'
    candidates = []
    for folder in folders:
        assert folder.is_dir() and not folder.is_symlink(), folder
        for path in sorted(folder.rglob('*')):
            assert not path.is_symlink(), path
            if path.is_file():
                candidates.append({'file': str(path), 'bytes': path.stat().st_size,
                                   'sha256': digest(path)})
    assert candidates, 'Nothing to reclaim'
    OUT.mkdir(parents=True, exist_ok=True)
    ledger = OUT / ledger_name
    assert not ledger.exists(), 'Existing ledger is preserved'
    record = {'state': 'prepared', 'createdUtc': datetime.now(timezone.utc).isoformat(),
              'scope': scope,
              'files': candidates, 'playersBefore': players()}
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    for row in candidates:
        path = Path(row['file'])
        assert path.is_file() and not path.is_symlink() and digest(path) == row['sha256'], path
        path.unlink()
    after = players()
    assert after == record['playersBefore'], 'A current player changed during reclamation'
    record.update(state='reclaimed', checkedUtc=datetime.now(timezone.utc).isoformat(),
                  reclaimedFiles=len(candidates), reclaimedBytes=sum(r['bytes'] for r in candidates),
                  allFourPlayersUnchanged=True)
    ledger.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps({k: record[k] for k in ('state', 'reclaimedFiles', 'reclaimedBytes', 'allFourPlayersUnchanged')}))


if __name__ == '__main__':
    main()
