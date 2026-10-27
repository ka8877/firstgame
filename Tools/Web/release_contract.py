"""Explicit release presets; an unchanged game keeps its earlier evidence."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
MODERN = {
    'name': 'modern',
    'revisions': {'FantasyCards': 'fm-modern-2026-10-26', 'PathOfDawn': 'gospel-modern-2026-10-26'},
    'contents': {'FantasyCards': 'fm-classic-0.1.0', 'PathOfDawn': 'gospel-0.1.0'},
    'reportRoots': {game: 'Artifacts/Validation/Modern' for game in ('FantasyCards', 'PathOfDawn')},
    'preservedGames': []
}
FIDELITY = {
    'name': 'fm-fidelity',
    'revisions': {'FantasyCards': 'fm-fidelity-2026-10-27', 'PathOfDawn': 'gospel-modern-2026-10-26'},
    'contents': dict(MODERN['contents']),
    'reportRoots': {'FantasyCards': 'Artifacts/Validation/FmFidelity', 'PathOfDawn': 'Artifacts/Validation/Modern'},
    'preservedGames': ['PathOfDawn']
}
DETAIL = {
    'name': 'detail',
    'revisions': {'FantasyCards': 'fm-detail-2026-10-27', 'PathOfDawn': 'gospel-natural-2026-10-27'},
    'contents': dict(MODERN['contents']),
    'reportRoots': {game: 'Artifacts/Validation/Detail' for game in ('FantasyCards', 'PathOfDawn')},
    'preservedGames': []
}
ARCANA = {
    'name': 'arcana',
    'revisions': {'FantasyCards': 'fm-arcana-2026-10-27', 'PathOfDawn': 'gospel-authored-2026-10-27'},
    'contents': dict(MODERN['contents']),
    'reportRoots': {game: 'Artifacts/Validation/Arcana' for game in ('FantasyCards', 'PathOfDawn')},
    'preservedGames': []
}

RESTORATION = {
    'name': 'restoration',
    'revisions': {'FantasyCards': 'fm-restoration-2026-10-27', 'PathOfDawn': 'gospel-toon-2026-10-27'},
    'contents': dict(MODERN['contents']),
    'reportRoots': {game: 'Artifacts/Validation/Restoration' for game in ('FantasyCards', 'PathOfDawn')},
    'preservedGames': []
}


def contract(name):
    if name == 'restoration':
        return RESTORATION
    if name == 'modern':
        return MODERN
    if name == 'fm-fidelity':
        return FIDELITY
    if name == 'detail':
        return DETAIL
    if name == 'arcana':
        return ARCANA
    raise ValueError('Unknown explicit release profile: ' + str(name))


def manifest_contract(manifest):
    value = contract(manifest.get('releaseProfile', 'modern'))
    if manifest.get('gameRevisions') != value['revisions'] or manifest.get('contentVersions') != value['contents']:
        raise ValueError('Manifest revisions/content differ from its explicit release profile')
    return value


def preserved_public():
    folder = ROOT / 'Artifacts/Validation/FmFidelity'
    record = json.loads((folder / 'preserved-gospel.json').read_text())
    for row in record['publicFiles']:
        path = folder / 'PreservedPublic' / row['file']
        if (not path.is_file() or path.stat().st_size != row['bytes']
                or hashlib.sha256(path.read_bytes()).hexdigest() != row['sha256']):
            raise ValueError('Preserved Gospel public file changed: ' + row['file'])
    return folder / 'PreservedPublic', record
