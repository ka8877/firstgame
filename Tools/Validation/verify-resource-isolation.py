#!/usr/bin/env python3
"""Rehash preserved Gospel source after current build-only Resources exclusion."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'Games/PathOfDawn'
REVISION = 'gospel-toon-2026-10-27'


def verify(path: Path, require_build: bool = True):
    path = path.resolve()
    assert path.is_relative_to(ROOT/'Artifacts') or path.is_relative_to(Path('/tmp/firstgame-restoration-builds/PathOfDawn')), 'Unexpected restoration ledger location'
    value = json.loads(path.read_text())
    assert value['revision'] == REVISION and value['status'] in ({'restored'} if require_build else {'restored', 'recovered-after-interruption'}), 'Current restoration required'
    assert value.get('startedUtc') and value.get('restoredUtc') and value['startedUtc'] <= value['restoredUtc']
    assert value['moves'] and value['files'], 'No recorded source exclusions'
    recorded = set()
    for row in value['files']:
        name = row['path']
        assert name not in recorded and name.startswith('Assets/Resources/')
        recorded.add(name)
        source = (PROJECT / name).resolve()
        assert source.is_relative_to(PROJECT / 'Assets/Resources') and source.is_file(), name
        data = source.read_bytes()
        assert len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256'], name
    for row in value['moves']:
        assert row['source'].startswith('Assets/Resources/') and row['destination'].startswith('Assets/Art/Archive/BuildExcludedLegacy/')
        source = PROJECT / row['source']
        assert source.exists() and not (PROJECT / row['destination']).exists(), row['source']
        match = re.search(r'^guid: ([0-9a-f]{32})$', Path(str(source) + '.meta').read_text(), re.M)
        assert match and match.group(1) == row['guid'], row['source']
    assert not (PROJECT / 'Library/FirstGameLegacyResourceBuild.json').exists(), 'Unrestored recovery journal remains'
    assert not (PROJECT / 'Assets/Art/Archive/BuildExcludedLegacy').exists(), 'Excluded source directory remains'
    if require_build:
        text = (path.parent / 'BuildEvidence.txt').read_text()
        assert 'Revision: ' + REVISION in text and 'Result: Succeeded' in text and 'LegacyResourcesRestored: true' in text
    return {'status': 'passed', 'revision': REVISION, 'ledger': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'ledgerSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'files': len(recorded), 'moves': len(value['moves']), 'sourceBytes': sum(row['bytes'] for row in value['files']),
            'scope': 'Actual restored source byte SHA256, asset GUIDs, absent holding/journal; no hardware claim.'}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--target', choices=('Windows', 'Web'))
    p.add_argument('--ledger', type=Path)
    p.add_argument('--lifecycle', action='store_true')
    a = p.parse_args()
    paths = ([a.ledger] if a.ledger else [ROOT / 'Artifacts/Builds/PathOfDawn' / target / 'LegacyResourceExclusions.json'
                                        for target in ([a.target] if a.target else ['Windows', 'Web'])])
    print(json.dumps({'status': 'passed', 'records': [verify(path, not a.lifecycle) for path in paths]}, indent=2))


if __name__ == '__main__': main()
