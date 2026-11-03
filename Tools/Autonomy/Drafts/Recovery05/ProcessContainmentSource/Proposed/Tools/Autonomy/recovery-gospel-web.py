#!/usr/bin/env python3
"""Unexecuted draft: fresh Gospel Web build/proof, never release approval.

Install the reviewed bytes at Tools/Autonomy/recovery-gospel-web.py. Existing
Unity/common runners are unchanged. Failure preserves state; no automatic
restore, retry, lease acquisition/release or historical-proof reconstruction.
"""
from pathlib import Path, PurePosixPath
from datetime import datetime, timezone
import argparse
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import time
import types
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
FP = '255e2d75bcae088f33f6f2134d44da36296aee85614d726189f79e3c941b8026'
MANIFEST_SHA = '0a9767bc92a48147bd38765608d1d8ea17f1ebeb6a788928d39bcf4eeb0754f8'
PROOF_SHA = 'c5978445e59c3c1fa19650013434a00830bd09c1efb04c5a2f2dffed472fa106'
GUARD_SHA = '2207d7c20da4eea5c17be87409991e09fd57c6427248695dedf1b6decd46f39f'
CONTAINMENT_SHA = '9d9082f038566b3dacc74ca8821a5758b85697f246af947f4f7a808bab1e2dad'
LIBRARY_LOGICAL = 'Games/PathOfDawn/Library'
LIBRARY_TARGET = '/tmp/firstgame-reprise-cache/PathOfDawn/Library'
JOURNAL_LEAF = 'FirstGameLegacyResourceBuild.json'
VOICE = {'file': 'Tools/Voice/Nativity/production.json', 'sha256':
         '635008782a6a364b79f0b607a27c43cc8a37545d42a21eafa41e85af0f19d869', 'bytes': 35716}
EXACT_NAMES = (
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.EncounterHudDirectionUsesTheActualCameraAndKeepsInteractionState',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.FieldConversationPreservesItsPhysicalEncounterAndSaveCursorAcrossBookAndReload',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.IllustratedBirthStillRetainsItsVoicedLineAcrossTheBible',
    'PathOfDawn.SmokeTests.GospelEncounterSmokeTests.TempleGuideHeadRemainsVisibleAndLegacyEncounterSaveKeepsItsPosition',
    'PathOfDawn.SmokeTests.GospelFacingSmokeTests.AcceptedKeyboardEncounterPlantsFacingBeforeSavingAndKeepsItAcrossBookAndReload',
    'PathOfDawn.SmokeTests.GospelFacingSmokeTests.LegacyReadingHeadingPositionAndOriginalBytesSurviveBookAndRepeatedContinue',
    'PathOfDawn.SmokeTests.GospelNativityAudioSmokeTests.ExactNarrationSourcesRejectChangedTextStaleLoadsAndMutedOrDisabledPlayback',
    'PathOfDawn.SmokeTests.GospelNativityAudioSmokeTests.NewBirthArrangementAndSilentOpeningCuesDuckOnlyForActualNarration',
    'PathOfDawn.SmokeTests.GospelNativityCharacterSmokeTests.ImportedWholeBodyCyclesBothStepsFacesFourWaysAndSettlesAtActualFeet',
    'PathOfDawn.SmokeTests.GospelNativityFiniteRenderingTests.CedarSourceAndRawHdrRemainFiniteBeforeThePostGuardAtMaryAndJordan',
    'PathOfDawn.SmokeTests.GospelNativityMovementAudioSmokeTests.ChapterVoicesPhysicalCuesAndPreferenceMuteHaveActualSources',
    'PathOfDawn.SmokeTests.GospelNativityStorySmokeTests.DetailedOpeningResumesQuietlyAndTheEntirePhysicalBibleStillAcquiresNineteenTimes',
    'PathOfDawn.SmokeTests.GospelNativityStorySmokeTests.RealPreviousLateralSaveForksWithoutTouchingItsOriginalBytes',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.PaintedChaptersKeepEveryEncounterOnTheContinuousPhysicalFloor',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.RealTouchAxesPlantAtRestAndTheCameraKeepsTheLandscapeSteadyThroughAJump',
    'PathOfDawn.SmokeTests.GospelNativityVisualSmokeTests.ReleasedExplorationDashPlantsWhenThePhysicalBurstEnds',
)
EXECUTION_FILES = {
    'Tools/Build/nativity-task.sh', 'Tools/Build/nativity-pipeline-state.py',
    'Tools/Build/restore-nativity-build-additions.py', 'Tools/Environment/activate-shell.sh',
    'Tools/Environment/assert-runtime-idle.py', 'Tools/Environment/repair-generated-cache-links.py',
    'Tools/Validation/restoration_source_binding.py', 'Tools/Validation/nativity_release_evidence.py',
    'Tools/Validation/nativity_storage.py', 'Games/PathOfDawn/Assets/Editor/BuildCommands.cs',
    'Games/PathOfDawn/Assets/Editor/BuildSourceBinding.cs',
    'Games/PathOfDawn/Assets/Editor/LegacyResourceIsolation.cs',
    'Games/PathOfDawn/Assets/Editor/ProjectSetup.cs',
    'Games/PathOfDawn/Assets/Editor/PilgrimageProjectSetup.cs',
    'Games/PathOfDawn/ProjectSettings/ProjectVersion.txt',
    'Games/PathOfDawn/ProjectSettings/FirstGameSetup.version',
}
ROLES = {'index': 'index.html', 'data': '.data.unityweb', 'wasm': '.wasm.unityweb',
         'framework': '.framework.js.unityweb', 'loader': '.loader.js'}


def require(value, message):
    if not value:
        raise ValueError(message)


def utc():
    return datetime.now(timezone.utc).isoformat()


def stamp(value):
    parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    require(parsed.utcoffset() is not None, 'An aware original timestamp is required')
    return parsed


def relative(value):
    require(isinstance(value, str) and value and '\\' not in value and '\0' not in value,
            'A safe relative path is required')
    path = PurePosixPath(value)
    require(not path.is_absolute() and '..' not in path.parts and str(path) == value,
            'A canonical relative path is required')
    return Path(value)


def regular(path):
    path = Path(path)
    return (path.is_file() and stat.S_ISREG(path.lstat().st_mode)
            and not any(item.is_symlink() for item in (path, *path.parents)))


def no_links(path):
    require(not any(item.is_symlink() for item in (path, *path.parents)), 'Linked path is not accepted: ' + str(path))


class JournalAliasError(ValueError):
    def __init__(self, observation, reason):
        super().__init__('Known Library journal mapping could not be verified: ' + reason)
        self.observation = observation


def library_journal_target(observation=None):
    observed = observation if observation is not None else {}
    logical = ROOT / LIBRARY_LOGICAL
    target = Path(LIBRARY_TARGET)
    observed.update(logicalLibrary=LIBRARY_LOGICAL, expectedLinkTarget=LIBRARY_TARGET,
                    logicalJournal=LIBRARY_LOGICAL + '/' + JOURNAL_LEAF,
                    expectedResolvedJournal=str(target / JOURNAL_LEAF),
                    logicalIsSymlink=logical.is_symlink())
    try:
        no_links(logical.parent)
        require(logical.is_symlink(), 'Logical Library is not the explicitly approved alias')
        observed['actualLinkTarget'] = str(logical.readlink())
        require(logical.readlink() == target, 'Library link target differs from the single approved target')
        no_links(target)
        require(target.is_dir() and target.resolve(strict=True) == target, 'Approved physical Library is missing or linked')
        observed['resolvedLibrary'] = str(logical.resolve(strict=True))
        require(logical.resolve(strict=True) == target, 'Library resolution differs from the approved physical target')
        observed['mappingVerified'] = True
        return target / JOURNAL_LEAF
    except (OSError, RuntimeError, ValueError) as error:
        observed.update(mappingVerified=False, diagnostic=type(error).__name__ + ': ' + str(error))
        raise JournalAliasError(observed, observed['diagnostic']) from error


def preserve_library_journal(run, stop, temporary=False):
    observed = {'captureComplete': False, 'status': 'blocked-or-partial',
                'automaticRestorationAttempted': False,
                'journalKind': 'pre-move-temporary' if temporary else 'primary'}
    destination = run / ('FailureState/LibraryJournal.original.json.tmp' if temporary else 'FailureState/LibraryJournal.original.json')
    try:
        source = library_journal_target(observed)
        if temporary:
            source = source.with_name(JOURNAL_LEAF + '.tmp')
        observed['resolvedObservedJournal'] = str(source)
        observed['exists'] = source.exists() or source.is_symlink()
        if not observed['exists']:
            library_journal_target(observed)
            observed.update(captureComplete=True, status='absent-verified-known-alias')
            return observed
        require(regular(source), 'Resolved journal must itself and through its physical parents be regular')
        before = source.stat()
        data = bytearray()
        with source.open('rb') as incoming:
            while True:
                require(time.monotonic() < stop, 'Bounded journal capture deadline exhausted')
                block = incoming.read(1024 * 1024)
                if not block:
                    break
                data.extend(block)
        after = source.stat()
        require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
                (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), 'Resolved journal changed during capture')
        source_sha = hashlib.sha256(data).hexdigest()
        observed['resolvedSource'] = {'file': str(source), 'sha256': source_sha, 'bytes': len(data)}
        write_raw(destination, data)
        saved = ref(destination, stop)
        require(saved['sha256'] == source_sha and saved['bytes'] == source.stat().st_size == len(data)
                and sha(source, stop) == source_sha, 'Resolved original and preserved journal raw bytes differ')
        library_journal_target(observed)
        observed.update(captureComplete=True, status='preserved-exact-resolved-journal', preserved=saved)
    except (Exception, KeyboardInterrupt) as error:
        observed.update(captureComplete=False, status='blocked-or-partial',
                        diagnostic=type(error).__name__ + ': ' + str(error))
        if destination.exists():
            observed['partialPreservedPath'] = str(destination.relative_to(ROOT))
            observed['partialPreservedBytes'] = destination.stat().st_size
    return observed


def sha(path, stop=None):
    require(regular(path), 'A regular original is required: ' + str(path))
    value = hashlib.sha256()
    before = path.stat()
    with path.open('rb') as stream:
        while True:
            if stop is not None and time.monotonic() >= stop:
                raise TimeoutError('Bounded hashing deadline reached')
            block = stream.read(1024 * 1024)
            if not block:
                break
            value.update(block)
    after = path.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns), 'Original changed while read')
    return value.hexdigest()


def ref(path, stop=None):
    return {'file': str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
            'sha256': sha(path, stop), 'bytes': path.stat().st_size}


def checked(row, stop=None):
    require(isinstance(row, dict), 'An actual original full reference is required')
    path = ROOT / relative(row.get('file'))
    require(path.resolve().is_relative_to(ROOT) and regular(path), 'Pinned input must be a repository regular original')
    require(ref(path, stop) == row, 'Pinned original SHA/size/path differs: ' + str(path))
    return path


def raw_checked(row, stop=None):
    path = checked(row, stop)
    raw = path.read_bytes()
    require(len(raw) == row['bytes'] and hashlib.sha256(raw).hexdigest() == row['sha256'], 'Parsed original bytes differ')
    return path, raw


def write_raw(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(raw)


def write_json(path, value):
    write_raw(path, (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode())


def get_path(value, path):
    require(isinstance(path, list) and 0 < len(path) <= 8
            and all(isinstance(key, str) and key for key in path), 'Actual Root field selectors must be reviewed')
    for key in path:
        require(isinstance(value, dict) and key in value, 'Pinned Root approval field is absent')
        value = value[key]
    return value


def exact_xml(raw):
    tree = ET.fromstring(raw)
    cases = tree.findall('.//test-case')
    names = [case.get('fullname') for case in cases]
    require(tree.get('result') == 'Passed' and tree.get('passed') == '16'
            and tree.get('failed') == tree.get('skipped') == '0'
            and len(names) == len(set(names)) == 16 and set(names) == set(EXACT_NAMES)
            and all(case.get('result') == 'Passed' for case in cases), 'Fresh exact16 XML must pass without skip/omission')


def same_root_ref(value, expected):
    require(isinstance(value, dict) and value.get('file') == expected['file']
            and value.get('sha256') == expected['sha256']
            and ('bytes' not in value or value['bytes'] == expected['bytes']), 'Root approval does not bind the actual original')


def role_map(rows):
    result = {}
    for row in rows:
        name = row['file']
        role = next((key for key, suffix in ROLES.items()
                     if (name == suffix if key == 'index' else name.startswith('Build/') and name.endswith(suffix))), None)
        require(role is not None and role not in result, 'Exact index/data/wasm/framework/loader roles are required')
        result[role] = row
    require(set(result) == set(ROLES), 'Exactly five core runtime files are required')
    return result


def runtime_paths(folder):
    result = {}
    for path in sorted(folder.rglob('*')):
        no_links(path)
        if path.is_dir():
            continue
        require(regular(path), 'Unexpected nonregular build entry')
        local = path.relative_to(folder)
        if local.as_posix() != 'index.html' and local.parts[0] not in ('Build', 'StreamingAssets', 'TemplateData'):
            continue
        if any('DoNotShip' in part or 'DontShip' in part for part in local.parts) or path.suffix in ('.map', '.pdb', '.mdb', '.log') or '.symbols.' in path.name:
            continue
        result[local.as_posix()] = path
    require('index.html' in result and len(result) >= 5, 'Complete original runtime is required')
    return result




def observed_runtime_idle(stop):
    active = []
    for folder in Path('/proc').iterdir():
        require(time.monotonic() < stop, 'Bounded idle observation deadline reached')
        if not folder.name.isdigit() or int(folder.name) == os.getpid():
            continue
        try:
            argv = (folder / 'cmdline').read_bytes().split(b'\0')
            first = argv[0].decode(errors='replace')
            state = (folder / 'stat').read_text().rsplit(') ', 1)[1].split()[0]
            if state not in ('Z', 'X') and (first.endswith('/Editor/Unity') or Path(first).name in ('chromium', 'chrome') and b'--remote-debugging-pipe' in argv and not any(value.startswith(b'--type=') for value in argv)):
                active.append({'pid': int(folder.name), 'process': Path(first).name})
        except (FileNotFoundError, ProcessLookupError, PermissionError, IndexError):
            continue
    return {'idle': not active, 'active': active, 'method': 'Read-only proc observation using the pinned existing idle-helper process selection; no other group is stopped.'}


class Lane:
    def __init__(self, lease_path, lease_bytes):
        self.started = time.monotonic()
        self.safe_end = self.started + 900
        self.lease_path, self.lease_bytes = lease_path, lease_bytes
        self.lease = json.loads(lease_bytes)
        require(self.lease.get('state') == 'acquired', 'Actual acquired ownership is required')
        self.deadline = stamp(self.lease['lease']['deadlineUtc'])
        self.run = None
        self.events = []
        self.logs = []
        self.env = dict(os.environ)

    def remaining(self):
        now = time.monotonic()
        self.safe_end = min(self.safe_end, self.started + 900,
                            now + (self.deadline - datetime.now(timezone.utc)).total_seconds() - 60)
        value = self.safe_end - now
        require(value > 0, 'Shared900 / lease-60 execution deadline exhausted')
        return value

    def child(self, label, argv, cap=900, log=None):
        require(hasattr(self, 'containment'), 'Verified process containment is required before any child')
        return self.containment.child(self, label, argv, cap=cap, log=log)

    def guard(self, label):
        require(self.lease_path.read_bytes() == self.lease_bytes, 'Lease raw bytes changed')
        guard = ROOT / 'Tools/Autonomy/cycle_guard.py'
        require(sha(guard, self.safe_end) == GUARD_SHA, 'Existing reviewed guard source differs')
        result = self.child(label, [sys.executable, str(guard), 'check', '--record', str(self.lease_path)], 45)
        require(result['exitCode'] == 0 and self.lease_path.read_bytes() == self.lease_bytes, 'Actual current ownership check failed or changed')
        self.remaining()  # Recompute after the actual remote guard, never extend original shared budget.


def full_source(module, expected, stop):
    rows = []
    project = ROOT / 'Games/PathOfDawn'
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        directory = project / folder
        no_links(directory)
        require(directory.is_dir(), 'Complete authored directories required')
        for path in directory.rglob('*'):
            require(time.monotonic() < stop, 'Source verification deadline exhausted')
            no_links(path)
            if path.is_dir():
                continue
            local = path.relative_to(project).as_posix()
            if local in ('Assets/Scenes/Boot.unity', 'Assets/Resources/Fonts/Korean.asset', 'Assets/Resources/Fonts/Display.asset') or local.startswith('Assets/Resources/Data/Cards/') and path.suffix == '.asset':
                continue
            rows.append(ref(path, stop))
    for name in ('Tools/Art/GospelAuthoredProps/material-spec.json', 'Tools/Art/GospelWell/material-spec.json'):
        rows.append(ref(ROOT / name, stop))
    rows.sort(key=lambda row: row['file'])
    require(time.monotonic() < stop and rows == expected['files']
            and expected.get('game') == 'PathOfDawn' and module.fingerprint(rows) == expected['fingerprint'] == FP,
            'Entire current authored source differs from fresh Native')
    return {'game': 'PathOfDawn', 'observedUtc': utc(), 'fingerprint': FP, 'files': rows}


def failure_state(run, expected, lease_deadline):
    stop = min(time.monotonic() + 30,
               time.monotonic() + max(0, (lease_deadline - datetime.now(timezone.utc)).total_seconds() - 15))
    state = {'status': 'pending-restoration', 'publishReady': False, 'baselineReady': False,
             'capturedUtc': utc(), 'automaticRestorationAttempted': False, 'sourceRows': [], 'complete': False}
    state['libraryJournal'] = preserve_library_journal(run, stop)
    state['libraryJournalTemporary'] = preserve_library_journal(run, stop, temporary=True)
    try:
        state['runtimeObservation'] = observed_runtime_idle(stop)
        paths = []
        project = ROOT / 'Games/PathOfDawn'
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            for path in (project / folder).rglob('*'):
                if time.monotonic() >= stop:
                    raise TimeoutError('Failure source capture deadline reached')
                if path.is_dir():
                    no_links(path)
                    continue
                local = path.relative_to(project).as_posix()
                if local in ('Assets/Scenes/Boot.unity', 'Assets/Resources/Fonts/Korean.asset', 'Assets/Resources/Fonts/Display.asset') or local.startswith('Assets/Resources/Data/Cards/') and path.suffix == '.asset':
                    continue
                paths.append(path)
        paths += [ROOT / name for name in ('Tools/Art/GospelAuthoredProps/material-spec.json', 'Tools/Art/GospelWell/material-spec.json')]
        for path in sorted(paths):
            state['sourceRows'].append(ref(path, stop))
        actual = {row['file']: row for row in state['sourceRows']}
        prior = {row['file']: row for row in expected['files']}
        state.update(complete=state['libraryJournal']['captureComplete'] and state['libraryJournalTemporary']['captureComplete'], sourceCaptureComplete=True,
                     missing=sorted(set(prior) - set(actual)), added=sorted(set(actual) - set(prior)),
                     changed=sorted(name for name in set(prior) & set(actual) if prior[name] != actual[name]),
                     sourceByteIdentity=(actual == prior))
    except Exception as error:
        state['captureError'] = str(error)
    state['pipelineAndAdditionsEvidence'] = []
    for path in sorted((run / 'PathOfDawn').rglob('*')):
        if time.monotonic() >= stop:
            state['pipelineIndexComplete'] = False
            break
        if path.is_file() and ('PipelineState' in path.parts or 'GeneratedProjectAdditions' in path.parts):
            try:
                state['pipelineAndAdditionsEvidence'].append(ref(path, stop))
            except Exception as error:
                state['pipelineIndexError'] = str(error)
                break
    else:
        state['pipelineIndexComplete'] = True
    write_json(run / 'FailureState/state.json', state)
    return ref(run / 'FailureState/state.json')


def configure_containment(lane, contract, planned_run):
    row = contract.get('processContainment', {}).get('helper')
    require(isinstance(row, dict) and row.get('file') == 'Tools/Environment/owned-process-containment.py'
            and row.get('sha256') == CONTAINMENT_SHA, 'Exact reviewed containment helper is mandatory')
    helper_path, raw = raw_checked(row, lane.safe_end)
    policy = contract['processContainment']
    expected = planned_run.parent.parent / 'ContainmentControls' / planned_run.name
    require(policy.get('evidenceRoot') == str(expected), 'Exact fresh batch containment evidence path required')
    module = types.ModuleType('firstgame_owned_process_containment')
    module.__file__ = str(helper_path)
    sys.modules[module.__name__] = module
    exec(compile(raw, str(helper_path), 'exec', dont_inherit=True, optimize=0), module.__dict__)
    lane.containment = module.Controller({'helperPath': str(helper_path), 'helperSha256': CONTAINMENT_SHA,
        'evidenceRoot': str(expected), 'acceptance': policy.get('acceptance')}, ROOT, test_mode=False)


def main():
    def interrupted(signum, frame):
        raise KeyboardInterrupt()
    for signum in (signal.SIGTERM, signal.SIGHUP, signal.SIGINT):
        signal.signal(signum, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--contract', type=Path, required=True)
    parser.add_argument('--lease-record', type=Path, required=True)
    args = parser.parse_args()
    require(Path(__file__).resolve() == ROOT / 'Tools/Autonomy/recovery-gospel-web.py', 'Install only at the reviewed exact Tools path')
    contract_path = args.contract if args.contract.is_absolute() else ROOT / args.contract
    lease_path = args.lease_record if args.lease_record.is_absolute() else ROOT / args.lease_record
    c1 = ROOT / 'Artifacts/Validation/Autonomy/C1-2026-11-03'
    require(regular(contract_path) and contract_path.resolve().is_relative_to(c1)
            and regular(lease_path) and lease_path.resolve().is_relative_to(ROOT / 'Artifacts/Validation/Autonomy'), 'Trusted persistent contract and ownership originals required')
    contract_raw, lease_raw = contract_path.read_bytes(), lease_path.read_bytes()
    contract = json.loads(contract_raw)
    require(contract.get('scope') == 'fresh-build-and-witness-only' and contract.get('publishReady') is False
            and contract.get('baselineReady') is False and contract.get('game') == 'PathOfDawn'
            and contract.get('revision') == 'gospel-facing-2026-11-03' and contract.get('engine') == '6000.3.26f1'
            and contract.get('contentVersion') == 'gospel-0.1.0' and contract.get('expectedSourceFingerprint') == FP,
            'This is unchanged Gospel build proof only, never public-baseline approval')
    require(sha(Path(__file__)) == contract.get('reviewedWrapperSha256'), 'Reviewed wrapper SHA differs')
    require(contract.get('expectedFullNames') == list(EXACT_NAMES) and len(EXACT_NAMES) == 16, 'Immutable exact16 contract required')
    require(contract.get('libraryJournalAlias') == {'logicalLibrary': LIBRARY_LOGICAL,
            'expectedLinkTarget': LIBRARY_TARGET, 'journalLeaf': JOURNAL_LEAF,
            'temporaryLeaf': JOURNAL_LEAF + '.tmp'}, 'Only the single reviewed Library journal alias and two exact leaves are allowed')
    lane = Lane(lease_path, lease_raw)
    attempt = contract['attempt']
    require(re.fullmatch(r'GospelWeb[0-9]{2,3}', attempt), 'Use a new safe GospelWebNN attempt')
    run = ROOT / relative(contract['runRoot'])
    build_root = Path(contract['buildRoot'])
    require(run == c1 / 'Recovery03/WebRuns' / attempt
            and str(build_root) == '/tmp/firstgame-c1-recovery03-' + attempt.lower() + '-builds', 'Use exact unique current run/build roots')
    build = build_root / 'PathOfDawn/Web'
    require(contract.get('buildFolder') == str(build), 'Declared build folder differs')
    configure_containment(lane, contract, run)
    for path in (run, build_root):
        no_links(path)
        require(not path.exists(), 'Previous outputs must be preserved; use new unique paths')
    native_rows = contract['actualFreshNative']
    native_raw = {}
    for key in ('binding', 'xml', 'inputManifest', 'productSummary', 'rootReview'):
        path, raw = raw_checked(native_rows.get(key), lane.safe_end)
        require(path.resolve().is_relative_to(c1), 'Use actual current C1 fresh Native originals, never historical G2 results')
        native_raw[key] = raw
    exact_xml(native_raw['xml'])
    binding, inputs, summary, review = (json.loads(native_raw[key]) for key in ('binding', 'inputManifest', 'productSummary', 'rootReview'))
    require(binding.get('status') == 'passed' and binding.get('exitCode') == 0
            and binding.get('sourceChangedDuringRun') is False and binding.get('sourceFingerprint') == FP
            and binding.get('xmlSha256') == native_rows['xml']['sha256']
            and binding.get('inputManifestSha256') == native_rows['inputManifest']['sha256'], 'Fresh actual Native16 source/XML/input binding required')
    require(summary.get('status') == 'passed' and summary.get('scope') == 'product-native-only'
            and summary.get('publishReady') is False and summary.get('baselineReady') is False
            and summary.get('sourceFingerprint') == FP and summary.get('nativeXml') == native_rows['xml']
            and summary.get('binding') == native_rows['binding'] and summary.get('inputManifest') == native_rows['inputManifest'], 'Actual fresh product summary differs')
    index_path = checked(summary['wholeAttemptIndex'], lane.safe_end)
    index = json.loads(index_path.read_bytes())
    require(index.get('fileCount') == len(index.get('files', [])), 'Fresh complete Native file index differs')
    for row in index['files']:
        checked(row, lane.safe_end)
    extra = binding.get('extraFixtureInputs')
    require(isinstance(extra, list) and len(extra) == 1 and all(extra[0].get(key) == value for key, value in VOICE.items()), 'Actual one exact voice fixture required')
    checked(VOICE, lane.safe_end)
    voice_saved = {'file': extra[0]['preservedFile'], 'sha256': VOICE['sha256'], 'bytes': VOICE['bytes']}
    checked(voice_saved, lane.safe_end)
    require(summary.get('voiceFixture') == voice_saved, 'Native voice source and actual preserved fixture differ')
    selectors = contract.get('rootReviewSelectors')
    selector_keys = {'status', 'reviewer', 'sourceFingerprint', 'nativeXml', 'binding', 'productSummary', 'completeOriginalsLedger', 'reviewedUtc', 'originals'}
    require(isinstance(selectors, dict) and set(selectors) == selector_keys, 'Owner must pin the actual completed Root schema selectors')
    require(get_path(review, selectors['status']) == 'approved' and get_path(review, selectors['reviewer']) in ('root', '/root')
            and get_path(review, selectors['sourceFingerprint']) == FP
            and stamp(summary['finishedUtc']) <= stamp(get_path(review, selectors['reviewedUtc'])) <= datetime.now(timezone.utc),
            'Actual Root approval must follow the completed Native result')
    for key in ('nativeXml', 'binding', 'productSummary', 'completeOriginalsLedger'):
        same_root_ref(get_path(review, selectors[key]), native_rows['xml' if key == 'nativeXml' else key])
    complete_path = checked(native_rows['completeOriginalsLedger'], lane.safe_end)
    complete_native = json.loads(complete_path.read_bytes())
    require(complete_native.get('count') == len(complete_native.get('files', []))
            and complete_native.get('publishReady') is False and complete_native.get('baselineReady') is False,
            'Root complete Native originals ledger required')
    for row in complete_native['files']:
        checked(row, lane.safe_end)
    originals = summary.get('originals', [])
    original_map = {row['file']: row for row in originals}
    require(originals and len(original_map) == len(originals), 'Actual complete current Native originals required')
    for row in originals:
        checked(row, lane.safe_end)
    indexed = {row['file']: row for row in index['files']}
    require(len(indexed) == len(index['files']) and all(indexed.get(row['file']) == row for row in originals),
            'Fresh full Native file index must bind every actual original')
    approved = get_path(review, selectors['originals'])
    require(isinstance(approved, list) and len(approved) >= 2
            and len({row.get('file') for row in approved if isinstance(row, dict)}) == len(approved),
            'Root must bind distinct actual Native PNG originals')
    for row in approved:
        require(isinstance(row, dict) and str(row.get('file', '')).endswith('.png') and row['file'] in original_map, 'Root must review actual new original PNGs')
        same_root_ref(row, original_map[row['file']])
    rows = contract['executionSources']
    require(len(rows) == 16 and {row['file'] for row in rows} == EXECUTION_FILES, 'All16 current execution/source pins are required')
    source_raw = {}
    for row in rows:
        path, raw = raw_checked(row, lane.safe_end)
        source_raw[row['file']] = raw
    require(contract['existingTrustedGuard']['file'] == 'Tools/Autonomy/cycle_guard.py'
            and contract['existingTrustedGuard']['sha256'] == GUARD_SHA, 'Exact existing reviewed guard pin required')
    guard_path, guard_raw = raw_checked(contract['existingTrustedGuard'], lane.safe_end)
    helper = ROOT / 'Tools/Validation/preserve-completed-build-proof.py'
    proof_spec = contract['afterSuccessProof']
    require(proof_spec.get('expectedInstalledFile') == str(helper.relative_to(ROOT))
            and proof_spec.get('expectedInstalledSha256') == PROOF_SHA and proof_spec.get('copyCoreRuntime') is True
            and sha(helper, lane.safe_end) == PROOF_SHA and helper.stat().st_size == 16139, 'Install exact reviewed after-success proof helper')
    helper_raw = helper.read_bytes()
    require(hashlib.sha256(helper_raw).hexdigest() == PROOF_SHA, 'Installed helper raw bytes changed')
    plan_path, plan_raw = raw_checked(contract['commandPlan'], lane.safe_end)
    plan = json.loads(plan_raw)
    env_values = {'FIRSTGAME_VALIDATION_ROOT': str(run), 'FIRSTGAME_BUILD_ROOT': str(build_root),
                  'PYTHONPYCACHEPREFIX': str(run / 'GeneratedPythonCache'), 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': '0'}
    common_argv = ['bash', 'Tools/Build/nativity-task.sh', 'PathOfDawn', 'web', attempt]
    proof_dir = run / 'BuildProof/PathOfDawn-Web01'
    proof_argv = ['python3', 'Tools/Validation/preserve-completed-build-proof.py', '--repository-root', str(ROOT),
                  '--game', 'PathOfDawn', '--revision', 'gospel-facing-2026-11-03', '--expected-source-fingerprint', FP,
                  '--build-folder', str(build), '--output', str(proof_dir), '--build-exit-code', '0', '--copy-core-runtime']
    require(plan.get('publishReady') is False and plan.get('baselineReady') is False and plan.get('cwd') == str(ROOT)
            and plan['commonBuild']['argv'] == common_argv and plan['commonBuild']['environmentOverrides'] == env_values
            and plan['commonBuild']['stdoutStderr'] == str(run / 'launcher-build.log')
            and plan['commonBuild']['unityLog'] == str(run / 'PathOfDawn' / attempt / 'web.log')
            and plan['afterSuccessOnly']['argv'] == proof_argv
            and plan['afterSuccessOnly']['stdoutStderr'] == str(run / 'launcher-proof.log'), 'Reviewed argv/env/output plan differs')
    binding_path = ROOT / 'Tools/Validation/restoration_source_binding.py'
    module = types.ModuleType('restoration_source_binding')
    module.__file__ = str(binding_path)
    sys.modules['restoration_source_binding'] = module
    exec(compile(source_raw[str(binding_path.relative_to(ROOT))], str(binding_path), 'exec', dont_inherit=True, optimize=0), module.__dict__)
    before = full_source(module, inputs, lane.safe_end)
    comparison = contract['oldRuntimeComparison']
    manifest_path, manifest_raw = raw_checked(comparison['manifest'], lane.safe_end)
    require(hashlib.sha256(manifest_raw).hexdigest() == MANIFEST_SHA, 'Original old G2 manifest is immutable')
    manifest = json.loads(manifest_raw)
    restore_path = checked(comparison['restorationReport'], lane.safe_end)
    restore = json.loads(restore_path.read_bytes())
    old_root = Path(comparison['oldRuntimeRoot'])
    no_links(old_root)
    require(old_root == Path(restore['stage']) / 'games/path-of-dawn' and comparison['oldSiteRoot'] == restore['stage']
            and restore.get('manifestSha256') == MANIFEST_SHA, 'Exact restored old rollback path required')
    old_rows = {row['role']: row for row in comparison['core5']}
    old_manifest_core = {name[len('games/path-of-dawn/'):]: value for name, value in manifest['files'].items()
                         if name == 'games/path-of-dawn/index.html' or name.startswith('games/path-of-dawn/Build/')}
    require(len(old_rows) == 5 and set(old_rows) == set(ROLES) and len(old_manifest_core) == 5, 'Original manifest has exact old core5')
    restored_rows = {row['file']: row for row in restore['files']}
    for role, row in old_rows.items():
        local = relative(row['runtimeFile'])
        require(row['publicFile'] == 'games/path-of-dawn/' + str(local)
                and old_manifest_core.get(str(local)) == row['sha256']
                and restored_rows[row['publicFile']]['sha256'] == row['sha256']
                and restored_rows[row['publicFile']]['bytes'] == row['bytes']
                and (str(local) == ROLES[role] if role == 'index' else str(local).startswith('Build/') and str(local).endswith(ROLES[role])), 'Old core original role/name/SHA/size differs')
        require(sha(old_root / local, lane.safe_end) == row['sha256'] and (old_root / local).stat().st_size == row['bytes'], 'Actual old rollback core differs')
    journal_mapping = {}
    journal = library_journal_target(journal_mapping)
    journal_tmp = journal.with_name(JOURNAL_LEAF + '.tmp')
    require(not journal.exists() and not journal.is_symlink() and not journal_tmp.exists() and not journal_tmp.is_symlink(),
            'Preserve and explicitly inspect an existing primary or temporary Library journal before starting')
    lane.env.update(env_values)
    lane.guard('00-initial-guard')
    require(lane.child('01-initial-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0, 'Another Unity/browser runtime is active')
    for path in (run, build_root):
        no_links(path)
        require(not path.exists(), 'New run/build became occupied during preflight')
    run.mkdir(parents=True)
    lane.run = run
    (run / 'GeneratedPythonCache').mkdir()
    for event in lane.events:
        write_json(run / 'Processes' / (event['label'] + '.finished.json'), event)
    for name, raw in lane.logs:
        write_raw(run / 'Controls' / name, raw)
    wrapper_raw = Path(__file__).read_bytes()
    write_raw(run / 'RecordedHarness/Tools/Autonomy/recovery-gospel-web.py', wrapper_raw)
    write_raw(run / 'contract.executed.json', contract_raw)
    write_raw(run / 'command-plan.executed.json', plan_raw)
    write_raw(run / 'owned-lease.executed.json', lease_raw)
    write_json(run / 'library-journal-mapping-before.json', journal_mapping)
    write_raw(run / 'current-source-before.json', (json.dumps(before, ensure_ascii=False, indent=2) + '\n').encode())
    for key, raw in native_raw.items():
        write_raw(run / 'RecordedInputs/Native' / (key + ('.xml' if key == 'xml' else '.json')), raw)
    write_raw(run / 'Native/PathOfDawn/inputs-before.json', native_raw['inputManifest'])
    write_raw(run / 'RecordedInputs/old-manifest.original.json', manifest_raw)
    write_raw(run / 'RecordedInputs/voice-production.original.json', (ROOT / VOICE['file']).read_bytes())
    for name, raw in {**source_raw, str(guard_path.relative_to(ROOT)): guard_raw,
                      str(helper.relative_to(ROOT)): helper_raw}.items():
        write_raw(run / 'RecordedHarness' / name, raw)
    result = {'status': 'pending-restoration', 'scope': 'fresh-build-and-witness-only', 'publishReady': False,
              'baselineReady': False, 'startedUtc': utc(), 'expectedSourceFingerprint': FP,
              'historicalProofReconstructed': False, 'automaticRetryOrRestoration': False,
              'oldRollbackReadOnly': True, 'oldManifest': comparison['manifest'],
              'oldRestorationReport': comparison['restorationReport'], 'actualFreshNative': native_rows,
              'wrapper': ref(run / 'RecordedHarness/Tools/Autonomy/recovery-gospel-web.py'),
              'contract': ref(run / 'contract.executed.json'), 'commandPlan': ref(run / 'command-plan.executed.json'),
              'leaseSnapshot': ref(run / 'owned-lease.executed.json')}
    try:
        require(hashlib.sha256(wrapper_raw).hexdigest() == contract['reviewedWrapperSha256']
                and contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw,
                'Actual copied wrapper/contract/plan bytes changed')
        total = sum(row['bytes'] for row in old_rows.values())
        temporary_device = Path('/tmp').stat().st_dev
        workspace_device = run.stat().st_dev
        minimum = total * (2 if temporary_device == workspace_device else 1) + 32_000_000
        disk = {'temporaryFreeBytes': shutil.disk_usage('/tmp').free, 'workspaceFreeBytes': shutil.disk_usage(run).free,
                'sameFilesystem': temporary_device == workspace_device, 'oldCoreBytesEstimate': total,
                'minimumBeforeBuildBytes': minimum, 'note': 'Estimate only; actual new core proof space is rechecked after build.'}
        write_json(run / 'disk-before-build.json', disk)
        require(disk['temporaryFreeBytes'] >= minimum and disk['workspaceFreeBytes'] >= minimum,
                'Insufficient free space for unique actual build plus persistent core5 proof and reserve')
        lane.guard('02-before-build-guard')
        require(contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw, 'Contract/plan changed before actual build')
        no_links(build_root)
        require(not build_root.exists() and not (run / 'PathOfDawn' / attempt).exists()
                and not proof_dir.exists(), 'New build/task/proof paths became occupied')
        require(lane.child('02b-before-build-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0,
                'Another Unity/browser runtime became active')
        for row in rows:
            checked(row, lane.safe_end)
        common = lane.child('03-common-build', common_argv, log=run / 'launcher-build.log')
        result['commonBuild'] = common
        require(common['exitCode'] == 0, 'Actual common build or original restoration failed')
        require(lane.child('03b-after-build-idle', [sys.executable, str(ROOT / 'Tools/Environment/assert-runtime-idle.py')], 30)['exitCode'] == 0,
                'Actual runtime idleness after the owned group exit is required')
        journal = library_journal_target()
        journal_tmp = journal.with_name(JOURNAL_LEAF + '.tmp')
        require(not journal.exists() and not journal.is_symlink() and not journal_tmp.exists() and not journal_tmp.is_symlink(),
                'Library primary or temporary isolation journal remains unresolved')
        no_links(build)
        require(all(regular(build / name) for name in ('BuildInputs.json', 'BuildEvidence.txt', 'LegacyResourceExclusions.json')),
                'Regular actual fresh build witnesses required')
        fresh_inputs_raw = (build / 'BuildInputs.json').read_bytes()
        fresh_inputs = json.loads(fresh_inputs_raw)
        require(fresh_inputs.get('game') == 'PathOfDawn' and fresh_inputs.get('files') == inputs['files']
                and fresh_inputs.get('fingerprint') == FP
                and stamp(common['startedUtc']) <= stamp(fresh_inputs['startedUtc']) <= stamp(common['finishedUtc']), 'Actual fresh BuildInputs/source/time differs')
        pairs = [line.split(': ', 1) for line in (build / 'BuildEvidence.txt').read_text().splitlines() if ': ' in line]
        require(len(pairs) == len(dict(pairs)), 'Duplicate actual build-evidence fields')
        evidence = dict(pairs)
        require(all(evidence.get(key) == value for key, value in {'Revision': 'gospel-facing-2026-11-03', 'Content': 'gospel-0.1.0', 'Engine': '6000.3.26f1', 'Target': 'WebGL', 'Result': 'Succeeded', 'Errors': '0', 'SourceFingerprint': FP, 'LegacyResourcesRestored': 'true'}.items()), 'Actual current successful/restored Web build required')
        legacy_path = build / 'LegacyResourceExclusions.json'
        require(evidence.get('LegacyResourceExclusions') == str(legacy_path), 'Actual legacy journal path differs')
        legacy = json.loads(legacy_path.read_bytes())
        require(legacy.get('status') == 'restored' and legacy.get('revision') == 'gospel-facing-2026-11-03'
                and legacy.get('target') == 'Web' and int(evidence['ExcludedLegacyAssets']) == len(legacy['moves'])
                and stamp(fresh_inputs['startedUtc']) <= stamp(legacy['startedUtc']) <= stamp(legacy['restoredUtc']) <= stamp(common['finishedUtc']), 'Actual complete legacy restoration journal required')
        for row in legacy['files']:
            original = ROOT / 'Games/PathOfDawn' / relative(row['path'])
            require(sha(original, lane.safe_end) == row['sha256'] and original.stat().st_size == row['bytes'], 'Legacy source/meta was not restored exactly')
        task = run / 'PathOfDawn' / attempt
        pipeline_dir = task / 'PipelineState/web'
        pipeline = json.loads((pipeline_dir / 'record.json').read_bytes())
        require(pipeline.get('status') == 'restored' and {row['file'] for row in pipeline['files']} == {
            'Games/PathOfDawn/Assets/Settings/QuestDesktopURP.asset', 'Games/PathOfDawn/Assets/Settings/QuestWebURP.asset',
            'Games/PathOfDawn/Assets/UniversalRenderPipelineGlobalSettings.asset'}, 'All three actual pipeline restorations required')
        for row in pipeline['files']:
            original = ROOT / relative(row['file'])
            before_path, after_path = pipeline_dir / 'Before' / original.name, pipeline_dir / 'After' / original.name
            require(sha(before_path, lane.safe_end) == sha(original, lane.safe_end) == row['sha256']
                    and before_path.stat().st_size == original.stat().st_size == row['bytes']
                    and sha(after_path, lane.safe_end) == row['generatedAfterSHA256'], 'Pipeline before/current/after originals differ')
        additions_dir = task / 'GeneratedProjectAdditions'
        additions = json.loads((additions_dir / 'record.json').read_bytes())
        require(additions.get('status') == 'restored-approved-inputs' and additions.get('sourceFingerprint') == FP, 'Actual generated-addition restoration required')
        for row in additions['files']:
            retained = additions_dir / relative(row['file'])
            require(sha(retained, lane.safe_end) == row['sha256'] and retained.stat().st_size == row['bytes'], 'Generated addition original was not retained')
        after = full_source(module, inputs, lane.safe_end)
        write_json(run / 'current-source-after-common.json', after)
        for row in rows:
            checked(row, lane.safe_end)
        require(sha(helper, lane.safe_end) == PROOF_SHA and sha(Path(__file__), lane.safe_end) == contract['reviewedWrapperSha256'], 'Execution producer/helper source changed')
        actual_runtime = runtime_paths(build)
        actual_rows = [dict(file=name, sha256=sha(path, lane.safe_end), bytes=path.stat().st_size) for name, path in actual_runtime.items()]
        core = role_map([row for row in actual_rows if row['file'] == 'index.html' or row['file'].startswith('Build/')])
        copy_bytes = sum(row['bytes'] for row in core.values())
        require(shutil.disk_usage(run).free >= copy_bytes + 16_000_000, 'Insufficient actual workspace space for persistent new core5 copy')
        lane.guard('04-before-proof-guard')
        require(contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw, 'Contract/plan changed before proof')
        no_links(proof_dir)
        require(not proof_dir.exists(), 'The actual proof output must still be absent')
        proof_event = lane.child('05-after-success-proof', proof_argv, log=run / 'launcher-proof.log')
        result['proofProcess'] = proof_event
        require(proof_event['exitCode'] == 0, 'Actual after-success proof did not finish')
        proof_path = proof_dir / 'build-proof-index.json'
        proof = json.loads(proof_path.read_bytes())
        require(proof.get('status') == 'preserved' and proof.get('publishReady') is False
                and proof.get('game') == 'PathOfDawn' and proof.get('revision') == 'gospel-facing-2026-11-03'
                and proof.get('target') == 'WebGL' and proof.get('sourceFingerprint') == FP
                and proof.get('reportedBuildExitCode') == 0 and proof.get('coreRuntimeBytesCopied') is True
                and proof.get('runtimeByteIdentityBeforeAfter') is True and proof.get('completeRuntimeFiles') == actual_rows
                and proof.get('originalBuildStartedUtc') == fresh_inputs['startedUtc'], 'Actual whole-runtime/source/core-byte witness differs')
        copied = {row['file']: row for row in proof['copiedOriginals']}
        for name in ('BuildInputs.json', 'BuildEvidence.txt', 'LegacyResourceExclusions.json'):
            require(sha(proof_dir / name, lane.safe_end) == sha(build / name, lane.safe_end) == copied[name]['sha256'], 'Original preserved build metadata differs')
        require(sha(proof_dir / 'CapturedSources/preserve_completed_build_proof.py', lane.safe_end) == PROOF_SHA, 'Actual proof helper source identity differs')
        comparisons = []
        for role, new in core.items():
            old = old_rows[role]
            old_path, new_path = old_root / relative(old['runtimeFile']), build / relative(new['file'])
            saved_path = proof_dir / 'Runtime' / relative(new['file'])
            require(sha(old_path, lane.safe_end) == old['sha256'] and old_path.stat().st_size == old['bytes'], 'Old rollback bytes changed')
            require(sha(new_path, lane.safe_end) == sha(saved_path, lane.safe_end) == new['sha256']
                    and new_path.stat().st_size == saved_path.stat().st_size == new['bytes'], 'Actual fresh and persistent core bytes differ')
            byte_equal = True
            with old_path.open('rb') as left, saved_path.open('rb') as right:
                while True:
                    lane.remaining()
                    a, b = left.read(1024 * 1024), right.read(1024 * 1024)
                    if a != b:
                        byte_equal = False
                        break
                    if not a:
                        break
            require(sha(old_path, lane.safe_end) == old['sha256'] and old_path.stat().st_size == old['bytes']
                    and sha(saved_path, lane.safe_end) == sha(new_path, lane.safe_end) == new['sha256']
                    and saved_path.stat().st_size == new_path.stat().st_size == new['bytes'],
                    'Old/current/persistent raw bytes changed during stream comparison')
            comparisons.append({'role': role, 'oldRuntimeFile': old['runtimeFile'], 'oldSha256': old['sha256'], 'oldBytes': old['bytes'],
                                'new': new, 'newPersistentFile': str(saved_path.relative_to(ROOT)), 'rawByteStreamsEqual': byte_equal,
                                'sameRelativeName': old['runtimeFile'] == new['file'],
                                'shaAndSizeEqual': old['sha256'] == new['sha256'] and old['bytes'] == new['bytes']})
        exact = (set(actual_runtime) == set(old_manifest_core)
                 and all(row['rawByteStreamsEqual'] and row['sameRelativeName'] and row['shaAndSizeEqual'] for row in comparisons))
        identity = {'status': 'compared', 'publishReady': False, 'baselineReady': False, 'comparedUtc': utc(),
                    'runtimeIdentity': 'exact-runtime-witness-candidate' if exact else 'paired-recovery-required',
                    'exactCore5AndCompleteRuntime': exact, 'core5': comparisons, 'completeFreshRuntime': actual_rows,
                    'originalManifest': comparison['manifest'], 'oldRestorationReport': comparison['restorationReport'],
                    'limit': 'No new metadata is attached to old runtime; all final Native/browser/Root/public/Core/CI/whole-site/rollback paired gates remain separate.'}
        write_json(run / 'runtime-identity-comparison.json', identity)
        final_source = full_source(module, inputs, lane.safe_end)
        write_json(run / 'current-source-after-proof.json', final_source)
        for row in rows:
            checked(row, lane.safe_end)
        checked(VOICE, lane.safe_end)
        for key in ('binding', 'xml', 'inputManifest', 'productSummary', 'rootReview', 'completeOriginalsLedger'):
            checked(native_rows[key], lane.safe_end)
        lane.guard('06-final-guard')
        require(sha(Path(__file__), lane.safe_end) == sha(run / 'RecordedHarness/Tools/Autonomy/recovery-gospel-web.py', lane.safe_end) == contract['reviewedWrapperSha256']
                and contract_path.read_bytes() == contract_raw and plan_path.read_bytes() == plan_raw
                and lease_path.read_bytes() == lease_raw, 'Wrapper/control inputs changed during actual execution')
        result.update(status='completed-build-and-witness-only', sourceFingerprint=FP,
                      runtimeIdentity=identity['runtimeIdentity'], comparison=ref(run / 'runtime-identity-comparison.json', lane.safe_end),
                      proof=ref(proof_path, lane.safe_end), currentSourceByteIdentity=True)
    except (Exception, KeyboardInterrupt) as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
        try:
            result['failureState'] = failure_state(run, inputs, lane.deadline)
        except (Exception, KeyboardInterrupt) as preserve_error:
            result['failureStateCaptureError'] = str(preserve_error)
    try:
        result['finishedUtc'] = utc()
        result['processEvents'] = lane.events
        result['executionLimit'] = {'sharedMaximumSeconds': 900, 'leaseReserveSeconds': 60,
                                    'originalMonotonicBatchStart': lane.started, 'finalMonotonicSafeEnd': lane.safe_end}
        stop = min(time.monotonic() + 8, time.monotonic() + max(0, (lane.deadline - datetime.now(timezone.utc)).total_seconds() - 5))
        leaf = {'status': 'partial', 'excludes': ['whole-attempt-before-summary.json', 'web-build-summary.json'], 'files': []}
        try:
            for path in sorted(run.rglob('*')):
                if path.is_file():
                    leaf['files'].append(ref(path, stop))
            leaf['status'] = 'complete'
        except (Exception, KeyboardInterrupt) as error:
            leaf['error'] = str(error)
        if leaf['status'] != 'complete':
            result['status'] = 'pending-restoration'
            result['error'] = 'Complete attempt evidence index did not finish; actual build/proof events remain recorded.'
        write_json(run / 'whole-attempt-before-summary.json', leaf)
        result['wholeAttemptIndex'] = ref(run / 'whole-attempt-before-summary.json')
        destination = run / 'web-build-summary.json'
        write_json(destination, result)
        print(json.dumps({'status': result['status'], 'publishReady': False, 'baselineReady': False, 'report': ref(destination)}, ensure_ascii=False))
        return 0 if result['status'] == 'completed-build-and-witness-only' else 1
    except KeyboardInterrupt:
        # Preserve any interrupted originals without overwriting them. This last
        # small metadata write owns no running Unity group and stays in reserve.
        previous = {signum: signal.signal(signum, signal.SIG_IGN) for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
        try:
            result.update(status='pending-restoration', finalizationInterrupted=True, finishedUtc=utc())
            leaf = locals().get('leaf', {'files': []})
            leaf.update(status='partial', error='Interrupted during finalization; original partial files retained.')
            fallback_index = run / 'whole-attempt-interrupted-index.json'
            write_json(fallback_index, leaf)
            result['wholeAttemptIndex'] = ref(fallback_index)
            destination = run / 'web-build-interrupted-summary.json'
            write_json(destination, result)
            print(json.dumps({'status': 'pending-restoration', 'publishReady': False, 'baselineReady': False, 'report': ref(destination)}, ensure_ascii=False))
            return 130
        finally:
            for signum, handler in previous.items():
                signal.signal(signum, handler)


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (Exception, KeyboardInterrupt) as error:
        result = {'status': 'pending-restoration' if isinstance(error, JournalAliasError) else 'preflight-rejected-or-preservation-error',
                  'publishReady': False, 'baselineReady': False, 'error': type(error).__name__ + ': ' + str(error)}
        if isinstance(error, JournalAliasError):
            result['libraryJournalDiagnostic'] = error.observation
        print(json.dumps(result, ensure_ascii=False))
        raise SystemExit(1)
