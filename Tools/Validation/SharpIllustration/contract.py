"""Strict source admission for two current Sharp/Illustration games.

Importing reads nothing and grants no approval. Root must review final test
sources and ordinary-input routes and explicitly freeze the source contracts.
Retained obligations are source IDs, never reused historical product passes.
"""
from pathlib import Path, PurePosixPath
import hashlib
import json
import math
import re

ROOT = Path(__file__).resolve().parents[3]
FOLDER = ROOT / 'Tools/Validation/SharpIllustration'
NATIVE_CONTRACT = FOLDER / 'native-contract.json'
BASELINE = FOLDER / 'baseline-obligations.json'
BASELINE_SHA = '0f33b4fb8ebdfd3862f6ed1f0c8da316b085b3eb4d88efbbe24c7147ae9e1581'
REVISIONS = {'FantasyCards': 'fm-sharp-client-2026-11-17',
             'PathOfDawn': 'gospel-illustrated-anime-2026-11-17'}
ROLES = {'FantasyCards': 'current-sharp-classic-client',
         'PathOfDawn': 'current-illustrated-anime-gospel'}
NEW_ORIGINALS = {'FantasyCards': ('sharp-client-lobby', 'sharp-owned-editor', 'sharp-encyclopedia', 'sharp-battle'), 'PathOfDawn': ('illustrated-named-speaker', 'illustrated-mary-speaker', 'illustrated-player-hud', 'illustrated-bible-return')}



def need(value, message):
    if not value:
        raise ValueError(message)


def regular(path):
    path = Path(path)
    need(path.is_file() and not any(item.is_symlink() for item in (path, *path.parents)),
         'Existing regular source original required: ' + str(path))
    return path


def baseline():
    raw = regular(BASELINE).read_bytes()
    need(hashlib.sha256(raw).hexdigest() == BASELINE_SHA,
         'Retained source-obligation set changed; do not drop previous cases/checkpoints')
    value = json.loads(raw)
    need(value.get('schema') == 1 and value.get('profile') == 'sharp-illustration'
         and set(value.get('nativeFullnames', {})) == set(REVISIONS)
         and len(value['nativeFullnames']['PathOfDawn']) == 17
         and len(value['nativeFullnames']['FantasyCards']) == 11,
         'Exact previous Gospel17 and Card11 source obligations required')
    return value


def native_full_names(game):
    need(game in REVISIONS, 'Both current games have explicit Native contracts')
    value = json.loads(regular(NATIVE_CONTRACT).read_bytes())
    need(value.get('schema') == 1 and value.get('profile') == 'sharp-illustration'
         and value.get('sourceStatus') == 'frozen'
         and set(value.get('games', {})) == set(REVISIONS),
         'Sharp/Illustration Native source is draft; Root must freeze actual final sources')
    selected = value['games'][game]
    prior = baseline()
    retained = [prior['nativeReplacements'].get(name, name) for name in prior['nativeFullnames'][game]]
    names = selected.get('fullnames')
    need(selected.get('revision') == REVISIONS[game]
         and isinstance(names, list) and names
         and all(isinstance(name, str) and re.fullmatch(
             game + r'(?:\.[A-Za-z_][A-Za-z0-9_]*){2,}', name) for name in names)
         and len(names) == len(set(names))
         and type(selected.get('count')) is int and selected['count'] == len(names)
         and selected.get('retainedFullnames') == retained
         and set(retained) <= set(names),
         'Retain every current case and admit only explicit additive unique Native names')
    rows = selected.get('sourceFiles')
    need(isinstance(rows, list) and rows and all(isinstance(row, dict) for row in rows),
         'Exact source and metadata rows required for both current Native selections')
    paths = [row.get('file') for row in rows]
    need(all(isinstance(path, str) for path in paths) and len(paths) == len(set(paths)),
         'Native source rows must have distinct explicit paths')
    case_sources, assigned = set(), []
    prefix = 'Games/' + game + '/Assets/'
    for row in rows:
        name = row['file']
        relative = PurePosixPath(name)
        need(not relative.is_absolute() and '..' not in relative.parts and str(relative) == name
             and name.startswith(prefix)
             and (name.endswith('.cs') or name.endswith('.cs.meta')),
             'Native source must be a regular C# or metadata original of this game')
        source = regular(ROOT / name)
        raw = source.read_bytes()
        need(type(row.get('bytes')) is int and row['bytes'] == len(raw)
             and isinstance(row.get('sha256'), str)
             and re.fullmatch(r'[0-9a-f]{64}', row['sha256'])
             and hashlib.sha256(raw).hexdigest() == row['sha256'],
             'Selected Native source bytes/SHA changed: ' + name)
        cases = row.get('fullnames')
        need(isinstance(cases, list) and all(isinstance(case, str) for case in cases)
             and len(cases) == len(set(cases)), 'Explicit unique per-source case assignments required')
        if name.endswith('.cs'):
            need(cases and all(case in names and case.split('.')[-2] == source.stem for case in cases),
                 'Every case must belong to the named current C# source class')
            case_sources.add(name)
        else:
            need(not cases, 'Metadata originals declare no executable cases')
        assigned.extend(cases)
    need(set(paths) == case_sources | {name + '.meta' for name in case_sources}
         and len(assigned) == len(set(assigned)) == len(names)
         and set(assigned) == set(names),
         'Every selected case and its exact C#/metadata pair must be assigned once')
    originals = selected.get('requiredOriginalSuffixes')
    need(isinstance(originals, list) and originals
         and all(isinstance(name, str) and re.fullmatch(r'[a-z0-9][a-z0-9-]{0,99}', name)
                 for name in originals) and len(originals) == len(set(originals)),
         'Root must explicitly select the actual changed-area Native original suffixes')
    return tuple(names)


def native_original_suffixes(game):
    native_full_names(game)
    return tuple(json.loads(regular(NATIVE_CONTRACT).read_bytes())['games'][game]['requiredOriginalSuffixes'])


def browser_role(game):
    need(game in ROLES, 'Only the two current Sharp/Illustration games are admitted')
    return ROLES[game]


def require_browser_plan(plan, game, *, expected_kind):
    need(game in REVISIONS and isinstance(plan, dict), 'Explicit current game plan required')
    need(plan.get('revision') == REVISIONS[game]
         and plan.get('suite') == 'modern-real-input-v1'
         and plan.get('sourceStatus') == 'frozen'
         and plan.get('evidenceRole') == browser_role(game),
         'Root must freeze the new current route/revision/role before browser execution')
    route = plan.get('sharpIllustrationRouteAdmission', {})
    kind = route.get('inputPlan') if isinstance(route, dict) else None
    need(expected_kind in ('desktop', 'mobile', 'public') and kind == expected_kind,
         'Plan input kind must match the actual executing desktop/touch/public profile')
    need(kind in ('desktop', 'mobile', 'public')
         and route.get('sourceStatus') == 'frozen'
         and route.get('ordinaryControlsOnly') is True
         and route.get('requiredNewOriginals') == list(NEW_ORIGINALS[game]),
         'New library/two-actor source routes still require actual ordinary-control geometry review')
    previous = baseline()['browser'][game][kind]
    required, steps = plan.get('requiredCheckpoints'), plan.get('steps')
    need(isinstance(required, list) and all(isinstance(name, str) for name in required)
         and len(required) == len(set(required))
         and set(previous['requiredCheckpoints']) <= set(required)
         and set(NEW_ORIGINALS[game]) <= set(required)
         and isinstance(steps, list) and all(isinstance(step, dict) for step in steps)
         and set(required) <= {step.get('name') for step in steps if step.get('type') == 'checkpoint'},
         'Every retained and new original checkpoint must be required and actually authored')
    need(sum(step.get('type') == 'audio' for step in steps) == 4
         and sum(step.get('type') == 'reload' for step in steps) == 2,
         'Retain all four real audio windows and both actual full reloads')
    need(len(steps) >= previous['steps'], 'Do not shrink the retained ordinary-input source flow')
    require_clarity_plan(plan, expected_kind)
    native_full_names(game)


def require_clarity_plan(plan, expected_kind):
    clarity = plan.get('renderingClarity', {})
    scale = 1 if expected_kind == 'desktop' else 2
    need(expected_kind in ('desktop', 'mobile', 'public')
         and clarity.get('deviceScaleFactor') == clarity.get('minimumBackingScale') == scale
         and clarity.get('maxBackingPixelError') == 2
         and clarity.get('requiredNavigations') == [0, 1, 2],
         'Actual desktop DPR1 and touch/public DPR2 need explicit full backing-buffer obligations')
    return scale


def require_browser_clarity(report, plan, *, expected_kind):
    scale = require_clarity_plan(plan, expected_kind)
    evidence = report.get('renderingClarity', {})
    samples = evidence.get('samples', [])
    need(evidence.get('requestedDeviceScaleFactor') == scale and isinstance(samples, list) and samples,
         'Actual shipping canvas measurements, not configured quality alone, are required')
    for sample in samples:
        require_clarity_sample(sample, scale)
    need({0, 1, 2} <= {sample.get('navigation') for sample in samples if sample.get('reason') == 'loaded'},
         'Actual backing clarity must survive initial boot and both full continued-save reloads')
    need(set(plan['requiredCheckpoints']) <= {sample.get('checkpoint') for sample in samples if sample.get('reason') == 'checkpoint'},
         'Every required gameplay original must retain an actual current canvas/drawing-buffer measurement')
    return evidence


def require_clarity_sample(sample, scale):
    need(sample.get('shippingCanvas') is True and sample.get('ready') == 'ready'
         and sample.get('devicePixelRatio') == scale,
         'Measured live shipping canvas and real browser DPR must match this executing profile')
    css, backing, drawing = sample.get('css', {}), sample.get('backing', {}), sample.get('drawingBuffer', {})
    need(all(isinstance(css.get(axis), (int, float)) and not isinstance(css.get(axis), bool)
             and math.isfinite(css[axis]) and css[axis] > 0 for axis in ('width', 'height')),
         'Finite actual DOM canvas CSS dimensions required')
    need(sample.get('contextLost') is False and sample.get('matchWebGLToCanvasSize') is True
         and backing == drawing and all(type(backing.get(axis)) is int
             and abs(backing[axis] - css[axis] * scale) <= 2 for axis in ('width', 'height')),
         'Actual WebGL drawing buffer must equal backing and honor full CSS×DPR without low-resolution upscale')
