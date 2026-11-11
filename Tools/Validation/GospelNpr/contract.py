"""Explicit NPR Native selection. Importing never admits or runs a product."""
from pathlib import Path, PurePosixPath
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[3]
NATIVE_CONTRACT = ROOT / 'Tools/Validation/GospelNpr/native-contract.json'
BASELINE_RECORD = ROOT / 'Artifacts/Validation/Autonomy/GospelNpr-2026-11-11/CardBaseline/baseline.json'
SOURCE_PREFIX = 'Games/PathOfDawn/Assets/SmokeTests/'
SOURCE_CLASSES = {
    'GospelNprSmokeTests', 'GospelNativityAudioSmokeTests',
    'GospelNativityMovementAudioSmokeTests', 'GospelNativityStorySmokeTests',
    'GospelScriptureReadingSmokeTests',
}


def need(value, message):
    if not value:
        raise ValueError(message)


def native_full_names():
    """Called lazily only by actual NPR Native/filter/Web/product admission."""
    path = NATIVE_CONTRACT
    need(path.is_file() and not any(item.is_symlink() for item in (path, *path.parents)),
         'NPR Native contract must be an existing regular source file')
    value = json.loads(path.read_bytes())
    need(isinstance(value, dict), 'NPR Native selection must be an object')
    need(value.get('schema') == 2 and value.get('sourceStatus') == 'frozen'
         and value.get('game') == 'PathOfDawn' and value.get('profile') == 'gospel-npr'
         and value.get('revision') == 'gospel-npr-2026-11-11',
         'NPR Native contract awaits Root source selection; explicitly review/freeze exact authored15 before admission')
    names = value.get('fullnames')
    need(isinstance(names, list) and len(names) == 15
         and all(isinstance(name, str) and re.fullmatch(r'PathOfDawn(?:\.[A-Za-z_][A-Za-z0-9_]*){2,}', name) for name in names)
         and len(set(names)) == len(names)
         and type(value.get('count')) is int and value['count'] == 15
         and type(value.get('newCaseCount')) is int and value['newCaseCount'] == 9
         and type(value.get('retainedCurrentSourceCaseCount')) is int and value['retainedCurrentSourceCaseCount'] == 6,
         'NPR Native must contain exactly15 unique qualified authored fullnames (new9 + retained fresh6)')
    rows = value.get('sourceFiles')
    need(isinstance(rows, list) and len(rows) == 10 and all(isinstance(row, dict) for row in rows),
         'Exact five selected Native C# originals and five metadata originals required')
    paths = [row.get('file') for row in rows]
    expected = {SOURCE_PREFIX + name + suffix for name in SOURCE_CLASSES for suffix in ('.cs', '.cs.meta')}
    need(all(isinstance(name, str) for name in paths) and len(set(paths)) == len(paths)
         and set(paths) == expected, 'Native source set must be complete, unique and exact')
    selected = []
    for row in rows:
        name = row['file']
        relative = PurePosixPath(name)
        source = ROOT / name
        need(not relative.is_absolute() and '..' not in relative.parts and str(relative) == name
             and source.is_file() and not any(item.is_symlink() for item in (source, *source.parents)),
             'Native source must be an unchanged regular repository original')
        raw = source.read_bytes()
        need(type(row.get('bytes')) is int and row['bytes'] == len(raw)
             and isinstance(row.get('sha256'), str) and re.fullmatch(r'[0-9a-f]{64}', row['sha256'])
             and hashlib.sha256(raw).hexdigest() == row['sha256'],
             'NPR Native selected source bytes/SHA changed: ' + name)
        cases = row.get('fullnames')
        need(isinstance(cases, list) and all(isinstance(case, str) for case in cases)
             and len(set(cases)) == len(cases), 'Unique explicit per-source Native names required')
        if name.endswith('.cs'):
            class_name = source.stem
            need(cases and all(case.startswith('PathOfDawn.SmokeTests.' + class_name + '.') for case in cases),
                 'Native fullname must belong to its exact selected C# class')
            need(class_name != 'GospelNprSmokeTests' or len(cases) == 9,
                 'Exactly nine actual new NPR cases required')
        else:
            need(not cases, 'Metadata originals cannot declare executable Native cases')
        selected.extend(cases)
    need(len(selected) == len(set(selected)) == len(names) and set(selected) == set(names),
         'Native source assignments must bind every selected fullname exactly once')
    return tuple(names)

NPR_BROWSER_ORIGINALS = (
    'npr-live-skinned-character-field', 'npr-orbit-front', 'npr-orbit-side',
    'npr-actual-speaker-portrait', 'npr-bible-return-same-3d-encounter',
    'npr-persisted-3d-people',
)


def require_browser_plan(plan, game):
    """Source admission only; no input, capture, result or visual approval."""
    need(game in ('PathOfDawn', 'FantasyCards') and isinstance(plan, dict),
         'Explicit current Gospel or preserved Card browser plan required')
    role = 'current-gospel-npr' if game == 'PathOfDawn' else 'preserved-card-regression'
    need(plan.get('sourceStatus') == 'frozen' and plan.get('evidenceRole') == role,
         'NPR browser plan is draft: Root must establish actual input geometry and freeze the exact role')
    if game == 'FantasyCards':
        return
    route = plan.get('nprRouteAdmission', {})
    required = plan.get('requiredCheckpoints')
    steps = plan.get('steps')
    need(isinstance(route, dict) and route.get('sourceStatus') == 'frozen'
         and route.get('ordinaryControlsOnly') is True
         and route.get('requiredNewOriginals') == list(NPR_BROWSER_ORIGINALS),
         'NPR camera-relative/browser route remains unresolved; Native selection cannot freeze it')
    need(isinstance(required, list) and all(isinstance(name, str) for name in required)
         and len(set(required)) == len(required) and set(NPR_BROWSER_ORIGINALS) <= set(required)
         and isinstance(steps, list) and all(isinstance(step, dict) for step in steps)
         and set(NPR_BROWSER_ORIGINALS) <= {step.get('name') for step in steps if step.get('type') == 'checkpoint'},
         'Every actual new3D original checkpoint must be required and authored before browser admission')
    native_full_names()


