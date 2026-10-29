#!/usr/bin/env python3
"""Bind complete Windows shipping trees to successful current build evidence."""
from datetime import datetime, timezone
from pathlib import Path
import argparse
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
BUILD = Path('/tmp/firstgame-renewal-builds')
OUT = ROOT / 'Artifacts/Validation/Renewal/Payloads'

def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): value.update(block)
    return value.hexdigest()

def require(condition, message):
    if not condition: raise ValueError(message)

def windows_files(game):
    folder = BUILD / game / 'Windows'
    files = {}
    for path in sorted(folder.rglob('*')):
        if not path.is_file(): continue
        relative = path.relative_to(folder)
        if relative.parts[0] not in ('D3D12', 'MonoBleedingEdge', game + '_Data') and relative.as_posix() not in (game + '.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'GameAssembly.dll'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in relative.parts): continue
        if path.suffix.lower() in ('.log', '.pdb', '.mdb'): continue
        if path.suffix.lower() == '.map' and not (path.name == 'settings.map' and 'MonoBleedingEdge' in relative.parts): continue
        require(not path.is_symlink(), 'Windows runtime symlink: ' + str(path))
        files[relative.as_posix()] = path
    essential = [game + '.exe', 'UnityPlayer.dll', game + '_Data/boot.config', game + '_Data/globalgamemanagers',
        game + '_Data/resources.assets', game + '_Data/level0', game + '_Data/Managed/' + game + '.Core.dll',
        game + '_Data/Managed/' + game + '.Presentation.dll', game + '_Data/Managed/UnityEngine.CoreModule.dll',
        'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll', 'MonoBleedingEdge/etc/mono/config']
    for name in essential:
        require(name in files and files[name].stat().st_size > 0, 'Essential Windows runtime missing: ' + name)
    if game == 'FantasyCards':
        media = json.loads((ROOT / 'Tools/Data/Renewal/media-manifest.json').read_text())
        required = {game + '_Data/StreamingAssets/Cards/fm_' + str(row['sourceId']) + '.jpg': row for row in media['cards']}
        actual = {name for name in files if name.startswith(game + '_Data/StreamingAssets/Cards/')}
        require(len(required) == 15350 and actual == set(required), 'Complete Windows card image set required')
        for name, row in required.items():
            require(files[name].stat().st_size == row['deliveryBytes'] and digest(files[name]) == row['deliverySha256'], 'Windows card image differs: ' + name)
    return folder, files

def observe(game):
    folder, files = windows_files(game)
    evidence = folder / 'BuildEvidence.txt'
    inputs = folder / 'BuildInputs.json'
    values = dict(line.split(': ', 1) for line in evidence.read_text().splitlines() if ': ' in line)
    require(values.get('Result') == 'Succeeded' and values.get('Errors') == '0' and values.get('Target') == 'StandaloneWindows64', 'Successful Windows build evidence required')
    require(sum(path.stat().st_size for path in files.values()) == int(values['Bytes']), 'Complete shipping byte total differs from Unity BuildReport: ' + game)
    return {'game': game, 'target': 'StandaloneWindows64', 'sourceFingerprint': values['SourceFingerprint'],
        'buildEvidenceSha256': digest(evidence), 'buildInputsSha256': digest(inputs),
        'files': [{'file': name, 'bytes': path.stat().st_size, 'sha256': digest(path)} for name, path in files.items()]}

def verify_windows_payload(game):
    path = OUT / (game + '-Windows.json')
    record = json.loads(path.read_text())
    current = observe(game)
    require(record.get('status') == 'passed' and all(record.get(key) == value for key, value in current.items()), 'Windows shipping tree changed since its exact payload binding: ' + game)
    return {'file': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size, 'sha256': digest(path),
        'runtimeFiles': len(current['files']), 'runtimeBytes': sum(row['bytes'] for row in current['files'])}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', choices=('FantasyCards', 'PathOfDawn'))
    parser.add_argument('--capture', action='store_true')
    args = parser.parse_args()
    if args.capture:
        record = observe(args.game)
        OUT.mkdir(parents=True, exist_ok=True)
        path = OUT / (args.game + '-Windows.json')
        if path.exists():
            previous = json.loads(path.read_text())
            require(all(previous.get(key) == value for key, value in record.items()), 'Preserve/review changed prior payload binding before recapture')
        else:
            record.update(status='passed', capturedUtc=datetime.now(timezone.utc).isoformat(),
                scope='Complete current shipping tree after its successful source-bound build, before packaging. Essential engine/managed/data files and all15350 card images where applicable checked. No Windows hardware execution claim; package notes/notices are separately CRC/SHA checked.')
            path.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(verify_windows_payload(args.game)))

if __name__ == '__main__': main()
